using System;
using Neutrino.Core.Messages;
using System.Net.Sockets;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

#if DEBUG
using CenterSpace.Free;
#endif

namespace Neutrino.Core
{
	public class Node : IDisposable
	{
		private Socket serverSocket;
		private byte[] receiveBuffer = new byte[NeutrinoConfig.MaxMessageSize];
		private EndPoint receivedEndPoint = new IPEndPoint(IPAddress.Any, 0);
		private NetworkMessageFactory msgFactory = new NetworkMessageFactory();
		private Dictionary<IPEndPoint, NetworkPeer> peersByEndpoint = new Dictionary<IPEndPoint, NetworkPeer>();
		private Dictionary<NetworkPeer, IPEndPoint> endpointsByPeer = new Dictionary<NetworkPeer, IPEndPoint>();
		private string localNickname;
		private ConcurrentPool<ReceivedBuffer> receivedBuffers = new ConcurrentPool<ReceivedBuffer>(10);
		private List<ReceivedBuffer> readyBuffers = new List<ReceivedBuffer>();
#if DEBUG
		private MersenneTwister randomGenerator;
		private List<DeferredReceivable> receivedBuffersForShuffling = new List<DeferredReceivable>();
#endif
		private List<NetworkPeer> peersPendingDisconnect = new List<NetworkPeer>();

		public Node(int wellKnownPort, params Assembly[] messageAssemblies)
		{
			Init(wellKnownPort, messageAssemblies);
		}

		public Node(string nickname, string serverHostname, int serverWellKnownPort, params Assembly[] messageAssemblies)
		{
			ServerHostname = serverHostname;
			localNickname = nickname;
			Init(serverWellKnownPort, messageAssemblies);
		}

#if DEBUG
		public double SimulatedPacketLossRate { get; set; }
		public double SimulatedPacketShuffleRate { get; set; }
#endif

		public T GetMessage<T>() where T : NetworkMessage
		{
			return msgFactory.Get<T>();
		}

		internal NetworkMessageFactory MessageFactory
		{
			get { return msgFactory; }
		}

		private void Init(int wellKnownPort, Assembly[] messageAssemblies)
		{
			ServerPort = wellKnownPort;
			msgFactory.Init(messageAssemblies);
#if DEBUG
			randomGenerator = new MersenneTwister();
#endif
		}

		public string Name { get; set; }

		public Socket CurrentSocket
		{
			get { return serverSocket; }
		}

		public Action<NetworkMessage> OnReceived { get; set; }
		public Action<NetworkPeer> OnPeerConnected { get; set; }
		public Action<NetworkPeer> OnPeerDisconnected { get; set; }

		public string ServerHostname { get; set; }
		public int ServerPort { get; set; }

		public void Start()
		{
			NeutrinoConfig.Log ("Node starting...");
			serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			if (ServerHostname == null)
				serverSocket.Bind(new IPEndPoint(IPAddress.Any, ServerPort));
			else
			{
				serverSocket.Bind(new IPEndPoint(IPAddress.Any, 0));
				var addresses = Dns.GetHostAddresses(ServerHostname);
				if (addresses == null || addresses.Length == 0)
					throw new ApplicationException("Unable to resolve server [" + ServerHostname + "]");
				else
				{
					IPAddress address = addresses.FirstOrDefault<IPAddress>(x => x.AddressFamily == AddressFamily.InterNetwork);
					if (address == null)
						address = addresses.FirstOrDefault<IPAddress>(x => x.AddressFamily == AddressFamily.InterNetworkV6);
					if (address == null)
						throw new ApplicationException("Unable to find an IP address for server [" + ServerHostname + "]");
					var serverPeer = NeutrinoConfig.CreatePeer();
					serverPeer.Init(this, serverSocket, address, ServerPort, "Server");
					IPEndPoint serverEndpoint = new IPEndPoint(address, ServerPort);
					peersByEndpoint[serverEndpoint] = serverPeer;
					endpointsByPeer[serverPeer] = serverEndpoint;
					if (OnPeerConnected != null)
						OnPeerConnected(serverPeer);

					var connectMsg = msgFactory.Get<ConnectMessage>();
					connectMsg.Nickname = localNickname;
					SendToAll(connectMsg);
				}
			}
			var asyncResult = serverSocket.BeginReceiveFrom(receiveBuffer, 0, NeutrinoConfig.MaxMessageSize, SocketFlags.None, ref receivedEndPoint, new AsyncCallback(HandleMessageReceived), null);
			if (asyncResult.CompletedSynchronously)
				HandleMessageReceived(asyncResult);
			NeutrinoConfig.Log ("Node started");
		}

		private void HandleMessageReceived(IAsyncResult result)
		{
			if (serverSocket != null)
			{
				try
				{
#if DEBUG
					if (SimulatedPacketLossRate > 0.0 && (randomGenerator.NextDoublePositive() <= SimulatedPacketLossRate))
					{
						NeutrinoConfig.LogWarning("SIMULATING PACKET LOSS!");
						receivedEndPoint = new IPEndPoint(IPAddress.Any, 0);
						IAsyncResult asyncResult = serverSocket.BeginReceiveFrom(receiveBuffer, 0, NeutrinoConfig.MaxMessageSize, SocketFlags.None, ref receivedEndPoint, new AsyncCallback(HandleMessageReceived), null);
						if (asyncResult.CompletedSynchronously)
							HandleMessageReceived(asyncResult);
						return;
					}
					else if (SimulatedPacketShuffleRate > 0.0 && (randomGenerator.NextDoublePositive() <= SimulatedPacketShuffleRate))
					{
						NeutrinoConfig.LogWarning("SIMULATING PACKET OUT OF ORDER!");
						int numReceived = serverSocket.EndReceiveFrom(result, ref receivedEndPoint);
						byte[] receivedForShuffle = new byte[numReceived];
						Array.Copy(receiveBuffer, receivedForShuffle, numReceived);
						lock(receivedBuffersForShuffling)
						{
							receivedBuffersForShuffling.Add(new DeferredReceivable() 
							{ 
								ReceivedBuffer = receivedForShuffle,
								TimeToReceiveTicks = Environment.TickCount + (int)(randomGenerator.NextDoublePositive() * 100.0),
								Endpoint = (IPEndPoint)receivedEndPoint
							});
						}
						receivedEndPoint = new IPEndPoint(IPAddress.Any, 0);
						IAsyncResult asyncResult = serverSocket.BeginReceiveFrom(receiveBuffer, 0, NeutrinoConfig.MaxMessageSize, SocketFlags.None, ref receivedEndPoint, new AsyncCallback(HandleMessageReceived), null);
						if (asyncResult.CompletedSynchronously)
							HandleMessageReceived(asyncResult);
						return;
					}
#endif
					int numBytesReceived = serverSocket.EndReceiveFrom(result, ref receivedEndPoint);
					var receivedBuffer = receivedBuffers.Pop();
					Array.Copy(receiveBuffer, receivedBuffer.Buffer, numBytesReceived);
					receivedBuffer.NumBytesReceived = numBytesReceived;
					receivedBuffer.Endpoint = (IPEndPoint)receivedEndPoint;
					lock(readyBuffers)
						readyBuffers.Add(receivedBuffer);
				}
				catch (Exception ex)
				{
					NeutrinoConfig.LogError("Error handling message: " + ex);
				}

				// TBD: When tests are complete, test whether we need to reallocate here?
				receivedEndPoint = new IPEndPoint(IPAddress.Any, 0);
				IAsyncResult repeatAsyncResult = serverSocket.BeginReceiveFrom(receiveBuffer, 0, NeutrinoConfig.MaxMessageSize, SocketFlags.None, ref receivedEndPoint, new AsyncCallback(HandleMessageReceived), null);
				if (repeatAsyncResult.CompletedSynchronously)
					HandleMessageReceived(repeatAsyncResult);
			}
		}

		private void HandleMessageReceived(IPEndPoint receivedFrom, byte[] buffer, int numBytesReceived)
		{
			NetworkPeer peer = null;
			if (peersByEndpoint.TryGetValue(receivedFrom, out peer))
			{
				peer.HandleMessageReceived(buffer, numBytesReceived);
			}
			else
			{
				if (NeutrinoConfig.LogLevel == NeutrinoLogLevel.Debug)
					NeutrinoConfig.Log("Received from potentially new peer at " + receivedFrom);
				List<NetworkMessage> initialMessages = new List<NetworkMessage>(msgFactory.Read(buffer, numBytesReceived));
				var connectMsg = initialMessages.FirstOrDefault<NetworkMessage>(x => (x is ConnectMessage));
				if (connectMsg == null)
				{
					NeutrinoConfig.Log("Ignoring peer who didn't send a ConnectMessage with his initial traffic");
				}
				else
				{
					var newPeer = NeutrinoConfig.CreatePeer();
					newPeer.Init(this, serverSocket, receivedFrom.Address, receivedFrom.Port, ((ConnectMessage)connectMsg).Nickname);
					peersByEndpoint[(IPEndPoint)receivedFrom] = newPeer;
					endpointsByPeer[newPeer] = (IPEndPoint)receivedFrom;
					if (OnPeerConnected != null)
						OnPeerConnected(newPeer);
					newPeer.HandleMessageReceived(buffer, numBytesReceived);
				}
			}
		}

		public void Dispose()
		{
			NeutrinoConfig.Log("Node shutting down...");
			if (serverSocket != null)
			{
				serverSocket.Close(1000);
				serverSocket = null;
				NeutrinoConfig.Log("Node shutdown");
			}
		}

		private List<ReceivedBuffer> buffersToProcess = new List<ReceivedBuffer>();
		public void Update()
		{
			lock (readyBuffers)
			{
				buffersToProcess.AddRange(readyBuffers);
				readyBuffers.Clear();
			}
			foreach (var bufferToProcess in buffersToProcess)
			{
				HandleMessageReceived(bufferToProcess.Endpoint, bufferToProcess.Buffer, bufferToProcess.NumBytesReceived);
				receivedBuffers.Push(bufferToProcess);
			}
			buffersToProcess.Clear();
#if DEBUG
			lock(receivedBuffersForShuffling)
			{
				for (int i = receivedBuffersForShuffling.Count - 1; i >= 0; i--)
				{
					var deferred = receivedBuffersForShuffling[i];
					if (deferred.TimeToReceiveTicks <= Environment.TickCount)
					{
						NeutrinoConfig.LogWarning(Name + " injecting shuffled receipt...");
						HandleMessageReceived(deferred.Endpoint, deferred.ReceivedBuffer, deferred.ReceivedBuffer.Length);
						receivedBuffersForShuffling.RemoveAt(i);
					}
				}
			}
#endif
			foreach (NetworkPeer c in peersByEndpoint.Values)
			{
				c.Update();
				if (!c.IsConnected)
					peersPendingDisconnect.Add(c);
			}
			foreach (NetworkPeer c in peersPendingDisconnect)
				DisconnectPeer(c);
			peersPendingDisconnect.Clear();
		}

		public void SendToAll(NetworkMessage msg)
		{
			foreach (NetworkPeer peer in peersByEndpoint.Values)
				peer.SendNetworkMessage(msg);
		}

		public IEnumerable<NetworkPeer> ConnectedPeers
		{
			get
			{
				foreach (NetworkPeer c in peersByEndpoint.Values)
					yield return c;
			}
		}

		public int NumberOfOutboundMessages
		{
			get
			{
				int num = 0;
				foreach (NetworkPeer c in peersByEndpoint.Values)
					num += c.NumberOfOutboundMessages;
				return num;
			}
		}

		internal void DisconnectPeer(NetworkPeer peer)
		{
			if (NeutrinoConfig.LogLevel == NeutrinoLogLevel.Debug)
				NeutrinoConfig.Log("Peer disconnected: " + peer);
			if (OnPeerDisconnected != null)
				OnPeerDisconnected(peer);
			peersByEndpoint.Remove(peer.Endpoint);
			endpointsByPeer.Remove(peer);
		}
	}
}
