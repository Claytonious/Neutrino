using System;
using Neutrino.Core;
using System.Threading;

namespace Neutrino.Tests
{
	public static class TestBootstrap
	{
		public const int testServerPort = 55333;
		private static Thread networkThread;

		public static Node BuildServer()
		{
			NeutrinoConfig.LogLevel = NeutrinoLogLevel.Debug;
			NeutrinoConfig.PeerTimeoutMillis = 120000;

			var serverNode = new Node(testServerPort, typeof(TestBootstrap).Assembly);
			serverNode.OnPeerConnected += client => Console.Out.WriteLine("Server: new client connected: " + client);
			serverNode.OnPeerDisconnected += client => Console.Out.WriteLine("Server: client disconnected: " + client);
			serverNode.OnReceived += msg => Console.Out.WriteLine("Server: received message: " + msg);
			serverNode.Name = "Server";
			return serverNode;
		}

		public static Node BuildClient()
		{
			var node = new Node(Environment.UserName, "localhost", testServerPort, typeof(TestBootstrap).Assembly);
			node.OnPeerConnected += client => Console.Out.WriteLine("Client: new client connected: " + client);
			node.OnPeerDisconnected += client => Console.Out.WriteLine("Client: client disconnected: " + client);
			node.OnReceived += msg => Console.Out.WriteLine("Client: received message: " + msg);
			node.Name = "Client";
			return node;
		}

		public static void RunNetwork(Func<bool> runWhile, params Node[] nodes)
		{
			networkThread = new Thread(delegate()
			{
				while (runWhile())
				{
					foreach(var node in nodes)
						node.Update();
					Thread.Sleep(10);
				}
			}) { IsBackground = true, Name = "Network Thread" };
			networkThread.Start();
		}

		public static void StopNetwork()
		{
			if (networkThread != null)
			{
				if (!networkThread.Join(5000))
					networkThread.Abort();
				networkThread = null;
			}
		}
	}
}
