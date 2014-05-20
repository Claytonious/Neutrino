using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System;
using System.Net;
using System.Linq;
using Neutrino.Core.Messages;

namespace Neutrino.Core
{
	public class OutboundQueue
	{
		private List<OutboundMessage> outboundMessages = new List<OutboundMessage>();
		private Pool<OutboundMessage> outboundMessagePool = new Pool<OutboundMessage>(100);
		private Dictionary<ushort,OutboundMessage> outboundMessagesBySequence = new Dictionary<ushort, OutboundMessage>();
		private List<byte[]> pendingResetOutboundMessages = new List<byte[]>();
		private ushort nextSequence = 0;
		private bool isResetPending;
		private IPEndPoint remoteEndpoint;
		private Socket socket;
		private byte[] outboundBuffer = new byte[NeutrinoConfig.MaxMessageSize];
		private const int maxGuaranteedBeforeReset = NeutrinoConfig.MaxPendingGuaranteedMessages - 1;
		private NetworkMessageFactory msgFactory;
		private Node node;

		public OutboundQueue(Node node, IPEndPoint remoteEndpoint, Socket socket)
		{
			this.remoteEndpoint = remoteEndpoint;
			this.socket = socket;
			this.node = node;
			msgFactory = node.MessageFactory;
		}

		public bool IsVerbose
		{
			get { return NeutrinoConfig.LogLevel == NeutrinoLogLevel.Debug; }
		}

		public void Enqueue(NetworkMessage msg)
		{
			if (isResetPending && msg.IsGuaranteed)
			{
				if (IsVerbose)
					NeutrinoConfig.Log(node.Name + " reset pending enqueuing for later: " + msg);
				byte[] buffer = new byte[NeutrinoConfig.MaxMessageSize];
				msg.Write(buffer);
				pendingResetOutboundMessages.Add(buffer);
			}
			else
			{
				var outboundMessage = outboundMessagePool.Pop();
				Assign(msg, outboundMessage);
				outboundMessages.Add(outboundMessage);
			}
		}

		public void HandleAckMessage(AckMessage ackMessage)
		{
			OutboundMessage outboundMessage = null;
			if (outboundMessagesBySequence.TryGetValue(ackMessage.AckedSequenceNumber, out outboundMessage))
			{
				outboundMessages.Remove(outboundMessage);
				outboundMessagesBySequence.Remove(ackMessage.AckedSequenceNumber);
				outboundMessagePool.Push(outboundMessage);
			}
			if (isResetPending && outboundMessages.FirstOrDefault(x => x.NeedsAck) == null)
			{
				if (IsVerbose)
					NeutrinoConfig.Log(node.Name + " drained all outbound - resetting sequence and sending queued");
				isResetPending = false;
				nextSequence = 0;
				foreach (byte[] buffer in pendingResetOutboundMessages)
					Enqueue(msgFactory.Read(buffer));
				pendingResetOutboundMessages.Clear();
			}
		}

		private void Assign(NetworkMessage msg, OutboundMessage target)
		{
			target.ContainedMessageType = msg.GetType();
			if (msg.IsGuaranteed)
			{
				target.SequenceNumber = nextSequence++;
				outboundMessagesBySequence[target.SequenceNumber] = target;
				msg.SequenceNumber = target.SequenceNumber;
			}
			target.PayloadLength = msg.Write(target.Payload);
			target.NeedsAck = msg.IsGuaranteed;
			target.PreviousSendTicks = Environment.TickCount - resendGuaranteedPeriodTicks - 1;
			if (!(msg is ResetNetworkIdsMessage) && nextSequence == maxGuaranteedBeforeReset)
			{
				if (IsVerbose)
					NeutrinoConfig.Log (node.Name + " reached max sequence - resetting...");
				Enqueue(msgFactory.Get<ResetNetworkIdsMessage>());
				isResetPending = true;
			}
		}

		internal int NumberOfActiveMessages
		{
			get
			{
				return outboundMessages.Count;
			}
		}

		internal const int resendGuaranteedPeriodTicks = 50;

		internal void Send()
		{
			int numInBatch = 0;
			int offset = 0;
			int nowTicks = Environment.TickCount;
			for (int i = 0; i < outboundMessages.Count; i++)
			{
				var outboundMessage = outboundMessages[i];
				if (!outboundMessage.NeedsAck || (nowTicks - outboundMessage.PreviousSendTicks >= resendGuaranteedPeriodTicks))
				{
					if (offset + outboundMessage.PayloadLength >= outboundBuffer.Length)
					{
						socket.SendTo(outboundBuffer, offset, SocketFlags.None, remoteEndpoint);
						offset = 0;
						numInBatch = 0;
					}
					Array.Copy(outboundMessage.Payload, 0, outboundBuffer, offset, outboundMessage.PayloadLength);
					offset += outboundMessage.PayloadLength;
					numInBatch++;
					outboundMessage.PreviousSendTicks = nowTicks;

					if (i == outboundMessages.Count - 1)
					{
						socket.SendTo(outboundBuffer, offset, SocketFlags.None, remoteEndpoint);
						offset = 0;
						numInBatch = 0;
					}
				}
			}

			for (int i = outboundMessages.Count - 1; i >= 0; i--)
			{
				if (!outboundMessages[i].NeedsAck)
					outboundMessages.RemoveAt(i);
			}
		}
	}
}
