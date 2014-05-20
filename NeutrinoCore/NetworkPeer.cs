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

		internal void HandleMessageReceived(byte[] buffer, int numBytesReceived)
		{
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
							shouldHandle = false;
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
						HandleMessageReceived(msg);
						for (int i = sequenceNumber + 1; i < NeutrinoConfig.MaxPendingGuaranteedMessages; i++)
						{
							NetworkMessage pendingMessage = null;
							if (pendingOutOfSequenceMessages.TryGetValue(i, out pendingMessage))
							{
								pendingOutOfSequenceMessages.Remove(i);
								HandleMessageReceived(pendingMessage);
							}
							else
								break;
						}
					}
					if (msg.IsGuaranteed)
					{
						ackMessage.AckedSequenceNumber = (byte)sequenceNumber;
						SendNetworkMessage(ackMessage);
					}
				}
			}
		}

		private void HandleMessageReceived(NetworkMessage msg)
		{
			ResetNetworkIdsMessage resetMsg = msg as ResetNetworkIdsMessage;
			if (resetMsg != null)
			{
				NeutrinoConfig.LogWarning(node.Name + " " + Nickname + " resetting guaranteed ids!");
				Array.Clear(idempotentSequenceNumbers, 0, idempotentSequenceNumbers.Length);
				pendingOutOfSequenceMessages.Clear();
			}
			node.OnReceived(msg);
		}

		internal void Update()
		{
			outboundQueue.Send();
		}

		public override string ToString()
		{
			return string.Format("[ConnectedClient: Nickname={0}]", Nickname);
		}
	}
}
