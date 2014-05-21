using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System;
using Neutrino.Core.Messages;

namespace Neutrino.Core
{
	public class NetworkPeer
	{
		private IPEndPoint endpoint;
		private NetworkMessageFactory msgFactory;
		private OutboundQueue outboundQueue;
		private Node node;
		private AckMessage ackMessage;
		private byte[] idempotentSequenceNumbers = new byte[NeutrinoConfig.MaxPendingGuaranteedMessages];
		private Dictionary<int, NetworkMessage> pendingOutOfSequenceMessages = new Dictionary<int, NetworkMessage>();
		private int previousActivityTimeTicks;
		private bool isConnected = true;

		public NetworkPeer()
		{
		}

		internal void Init(Node node, Socket socket, IPAddress address, int port, string nickname)
		{
			this.node = node;
			msgFactory = node.MessageFactory;
			Nickname = nickname;
			this.endpoint = new IPEndPoint(address, port);
			outboundQueue = new OutboundQueue(node, this.endpoint, socket);
			ackMessage = msgFactory.Get<AckMessage>();
			previousActivityTimeTicks = Environment.TickCount;
		}

		public IPEndPoint Endpoint
		{
			get { return endpoint; }
		}

		public int NumberOfOutboundMessages
		{
			get
			{
				return outboundQueue.NumberOfActiveMessages;
			}
		}

		public string Nickname { get; set; }

		public void SendNetworkMessage(NetworkMessage msg)
		{
			outboundQueue.Enqueue(msg);
		}

		public void Disconnect()
		{
			isConnected = false;
		}

		public bool IsConnected
		{
			get { return isConnected; }
		}

		internal void HandleMessageReceived(byte[] buffer, int numBytesReceived)
		{
			previousActivityTimeTicks = Environment.TickCount;
			foreach (var msg in msgFactory.Read(buffer, numBytesReceived))
			{
				msg.Source = this;
				AckMessage ackMsg = msg as AckMessage;
				if (ackMsg != null)
					outboundQueue.HandleAckMessage(ackMsg);
				else
				{
					bool shouldHandle = true;
					int sequenceNumber = 0;
					if (msg.IsGuaranteed)
					{
						sequenceNumber = msg.SequenceNumber;
						if (idempotentSequenceNumbers[sequenceNumber] == 1)
						{
							shouldHandle = false;
						}
						else
						{
							idempotentSequenceNumbers[sequenceNumber] = 1;
							if (sequenceNumber > 0 && (
								idempotentSequenceNumbers[sequenceNumber - 1] == 0 ||
								pendingOutOfSequenceMessages.ContainsKey(sequenceNumber - 1)))
							{
								pendingOutOfSequenceMessages[sequenceNumber] = msg.Clone();
								shouldHandle = false;
							}
						}
					}
					if (shouldHandle)
					{
						ProcessMessage(msg);
						for (int i = sequenceNumber + 1; i < NeutrinoConfig.MaxPendingGuaranteedMessages; i++)
						{
							NetworkMessage pendingMessage = null;
							if (pendingOutOfSequenceMessages.TryGetValue(i, out pendingMessage))
							{
								pendingOutOfSequenceMessages.Remove(i);
								ProcessMessage(pendingMessage);
							}
							else
								break;
						}
					}
					if (msg.IsGuaranteed)
					{
						ackMessage.AckedSequenceNumber = (ushort)sequenceNumber;
						SendNetworkMessage(ackMessage);
					}
				}
			}
		}

		private void ProcessMessage(NetworkMessage msg)
		{
			ResetNetworkIdsMessage resetMsg = msg as ResetNetworkIdsMessage;
			if (resetMsg != null)
			{
				Array.Clear(idempotentSequenceNumbers, 0, idempotentSequenceNumbers.Length);
				if (pendingOutOfSequenceMessages.Count != 0)
				{
					NeutrinoConfig.LogError(node.Name + " there were still pending messages when resetting id's!");
					pendingOutOfSequenceMessages.Clear();
				}
			}
			node.OnReceived(msg);
		}

		internal void Update()
		{
			outboundQueue.Send();
			int ticksSinceActivity = Environment.TickCount - previousActivityTimeTicks;
			if (ticksSinceActivity >= NeutrinoConfig.PeerTimeoutMillis)
			{
				if (NeutrinoConfig.LogLevel == NeutrinoLogLevel.Debug)
					NeutrinoConfig.Log("Disconnecting peer " + this + " because of inactivity for " + ticksSinceActivity + " millis");
				Disconnect();
			}
		}

		public override string ToString()
		{
			return string.Format("[NetworkPeer: Endpoint={0}, NumberOfOutboundMessages={1}, Nickname={2}]", Endpoint, NumberOfOutboundMessages, Nickname);
		}
	}
}
