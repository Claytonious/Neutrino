using NUnit.Framework;
using System;
using Neutrino.Core;
using System.Threading;
using Neutrino.Core.Messages;

namespace Neutrino.Tests
{
	[TestFixture]
	public class LiveSocketTests
	{
		[TearDown]
		public void Teardown()
		{
			TestBootstrap.StopNetwork();
		}

		[Test]
		public void TestStartStopServer()
		{
			for (int i = 0; i < 2; i++)
			{
				var serverNode = TestBootstrap.BuildServer();
				serverNode.Start();
				Thread.Sleep(5000);
				serverNode.Dispose();
				Assert.IsNull(serverNode.CurrentSocket);
			}
		}

		[Test]
		public void TestConnect()
		{
			bool serverReceivedConnect = false;

			using (var serverNode = TestBootstrap.BuildServer())
			{
				serverNode.OnPeerConnected += newClient =>
				{
					Console.Out.WriteLine("Server received connect: " + newClient);
					serverReceivedConnect = true;
				};
				serverNode.OnReceived += msg =>
				{
					Console.Out.WriteLine("Server received msg: " + msg);
					if (msg is ConnectMessage)
						serverReceivedConnect = true;
				};
				serverNode.Start();

				using (var clientNode = TestBootstrap.BuildClient())
				{
					clientNode.Start();

					bool continueRunning = true;
					TestBootstrap.RunNetwork(() =>
					{
						return continueRunning && !serverReceivedConnect;
					}, serverNode, clientNode);

					Thread.Sleep(5000);
					continueRunning = false;
				}
			}
			Assert.IsTrue(serverReceivedConnect, "Server should have received connect message from client");
		}

		[Test]
		public void TestDisconnect()
		{
			int serverReceivedConnectMillis = 0;
			int clientReceivedConnectMillis = 0;
			int serverReceivedDisconnectMillis = 0;
			int clientReceivedDisconnectMillis = 0;
			int quietTime = 0;

			using (var serverNode = TestBootstrap.BuildServer())
			{
				serverNode.OnPeerConnected = newClient =>
				{
					Console.Out.WriteLine("Server received connect: " + newClient);
					serverReceivedConnectMillis = Environment.TickCount;
				};
				serverNode.OnPeerDisconnected = newClient =>
				{
					Console.Out.WriteLine("Server received disconnect: " + newClient);
					serverReceivedDisconnectMillis = Environment.TickCount;
				};
				serverNode.OnReceived = msg =>
				{
					serverNode.SendToAll(serverNode.GetMessage<HelloReply>());
				};
				serverNode.Start();

				using (var clientNode = TestBootstrap.BuildClient())
				{
					NeutrinoConfig.PeerTimeoutMillis = 5000;

					clientNode.OnPeerConnected = newPeer =>
					{
						Console.Out.WriteLine("Client received connect: " + newPeer);
						clientReceivedConnectMillis = Environment.TickCount;
					};
					clientNode.OnPeerDisconnected = newPeer =>
					{
						Console.Out.WriteLine("Client received disconnect: " + newPeer);
						clientReceivedDisconnectMillis = Environment.TickCount;
					};
					clientNode.OnReceived = receivedMsg =>
					{
					};

					clientNode.Start();

					var msg = clientNode.GetMessage<HelloMessage>();
					for (int i = 0; i < 600; i++)
					{
						clientNode.SendToAll(msg);
						clientNode.Update();
						serverNode.Update();
						Thread.Sleep(10);
					}

					quietTime = Environment.TickCount;
					for (int i = 0; i < 1000; i++)
					{
						clientNode.Update();
						serverNode.Update();
						Thread.Sleep(10);
					}
				}
			}
			Assert.Greater(serverReceivedConnectMillis, 0);
			Assert.Greater(clientReceivedConnectMillis, 0);
			Assert.GreaterOrEqual(serverReceivedDisconnectMillis - quietTime, 5000);
			Assert.LessOrEqual(serverReceivedDisconnectMillis - quietTime, 10000);
			Assert.GreaterOrEqual(clientReceivedDisconnectMillis - quietTime, 5000);
			Assert.LessOrEqual(clientReceivedDisconnectMillis - quietTime, 10000);
		}

		[Test]
		public void TestSimpleMessage()
		{
			int numReceivedServer = 0;
			int numReceivedClient = 0;
			int numConnectsReceivedServer = 0;
			int numConnectsReceivedClient = 0;

			using (var serverNode = TestBootstrap.BuildServer())
			{
				serverNode.OnPeerConnected += client =>
				{
					++numConnectsReceivedServer;
				};
				serverNode.OnReceived += msg =>
				{
					Console.Out.WriteLine("Server received msg: " + msg);
					if (msg is HelloMessage)
					{
						var reply = serverNode.GetMessage<HelloReply>();
						reply.NumReceived = ++numReceivedServer;
						serverNode.SendToAll(reply);
					}
				};
				serverNode.Start();

				using (var clientNode = TestBootstrap.BuildClient())
				{
					clientNode.OnPeerConnected += client =>
					{
						++numConnectsReceivedClient;
					};
					clientNode.OnReceived += msg =>
					{
						Console.Out.WriteLine("Client received msg: " + msg);
						if (msg is HelloReply)
							++numReceivedClient;
					};

					clientNode.Start();

					var helloMsg = clientNode.GetMessage<HelloMessage>();
					helloMsg.Text = "Hello there, my little friend";
					clientNode.SendToAll(helloMsg);

					for (int i = 0; i < 10; i++)
					{
						serverNode.Update();
						clientNode.Update();
						if (numReceivedClient == 1 && numReceivedServer == 1)
							break;
						Thread.Sleep(100);
					}

					Assert.AreEqual(1, numConnectsReceivedServer, "Should only get 1 connect message on server");
					Assert.AreEqual(1, numConnectsReceivedClient, "Should only get 1 connect message on client");
					Assert.AreEqual(1, numReceivedServer, "Server should have received 1 HelloMessage");
					Assert.AreEqual(1, numReceivedClient, "Client should have received 1 HelloReply");
				}
			}
		}

		[Test]
		public void TestSimpleReplies()
		{
			int numReceivedServer = 0;
			int numReceivedClient = 0;
			int numConnectsReceivedServer = 0;
			int numConnectsReceivedClient = 0;
			const int numToSend = 1000;
			int[] receivedServer = new int[numToSend];

			using (var serverNode = TestBootstrap.BuildServer())
			{
				serverNode.OnPeerConnected += client =>
				{
					++numConnectsReceivedServer;
				};
				serverNode.OnReceived = msg =>
				{
					Console.Out.WriteLine("Server received msg: " + msg);

					HelloMessage helloMsg = msg as HelloMessage;
					if (helloMsg != null)
					{
						receivedServer[helloMsg.Num] = receivedServer[helloMsg.Num] + 1;
						var reply = serverNode.GetMessage<HelloReply>();
						reply.NumReceived = ++numReceivedServer;
						serverNode.SendToAll(reply);
					}
				};
				serverNode.Start();

				using (var clientNode = TestBootstrap.BuildClient())
				{
					clientNode.OnPeerConnected += client =>
					{
						++numConnectsReceivedClient;
					};
					clientNode.OnReceived = msg =>
					{
						if (msg is HelloReply)
						{
							Console.Out.WriteLine("Client received msg: " + msg);
							++numReceivedClient;
						}
					};

					clientNode.Start();

					for (int i = 0; i < numToSend; i++)
					{
						var helloMsg = clientNode.GetMessage<HelloMessage>();
						helloMsg.Text = "Hello there, my little friend number " + i;
						helloMsg.Num = i;
						clientNode.SendToAll(helloMsg);
					}

					for (int i = 0; i < 5; i++)
					{
						serverNode.Update();
						clientNode.Update();
						Thread.Sleep(100);
					}

					for (int i = 0; i < numToSend; i++)
					{
						Assert.AreEqual(1, receivedServer[i], "Should have received message #" + i + " one time");
					}

					Assert.AreEqual(1, numConnectsReceivedServer, "Should only get 1 connect message on server");
					Assert.AreEqual(1, numConnectsReceivedClient, "Should only get 1 connect message on client");
					Assert.AreEqual(numToSend, numReceivedServer);
					Assert.AreEqual(numToSend, numReceivedClient);
				}
			}
		}

		[Test]
		public void TestIdempotence()
		{
			int numReceivedServer = 0;
			int numReceivedClient = 0;

			using (var serverNode = TestBootstrap.BuildServer())
			{
				serverNode.OnReceived += msg =>
				{
					Console.Out.WriteLine("Server received msg: " + msg);
					if (msg is SampleGuaranteedMessage)
					{
						++numReceivedServer;
						Assert.AreEqual(1, ((SampleGuaranteedMessage)msg).TheNumber);
						var reply = serverNode.GetMessage<SampleGuaranteedMessage>();
						reply.TheNumber = 2;
						serverNode.SendToAll(reply);
					}
				};
				serverNode.Start();

				using (var clientNode = TestBootstrap.BuildClient())
				{
					clientNode.OnReceived += msg =>
					{
						Console.Out.WriteLine("Client received msg: " + msg);
						if (msg is SampleGuaranteedMessage)
						{
							++numReceivedClient;
							Assert.AreEqual(2, ((SampleGuaranteedMessage)msg).TheNumber);
						}
					};

					clientNode.Start();

					var clientMsg = clientNode.GetMessage<SampleGuaranteedMessage>();
					clientMsg.TheNumber = 1;
					clientNode.SendToAll(clientMsg);
					// Make the client update without the server having a chance so that guaranteeds are queued up
					for (int i = 0; i < 10; i++)
						clientNode.Update();
					// Now let the server catch up
					for (int i = 0; i < 1000; i++)
					{
						serverNode.Update();
						clientNode.Update();
						Thread.Sleep(10);
					}

					// Because these are guaranteed messages, they should only be handled once in spite of having been spammed...
					Assert.AreEqual(1, numReceivedClient);
					Assert.AreEqual(1, numReceivedServer);
				}
			}
		}

		[Test]
		public void TestGuaranteed()
		{
			int previousReceivedServer = 0;
			int previousReceivedClient = 0;

			using (var serverNode = TestBootstrap.BuildServer())
			{
				serverNode.OnReceived = msg =>
				{
					Console.Out.WriteLine("Server received msg: " + msg);
					SampleGuaranteedMessage sampleMsg = msg as SampleGuaranteedMessage;
					if (sampleMsg != null)
					{
						Assert.AreEqual(previousReceivedServer + 1, sampleMsg.TheNumber, "Server received " + sampleMsg.TheNumber + " out of sequence");
						previousReceivedServer = sampleMsg.TheNumber;
						var reply = serverNode.GetMessage<SampleGuaranteedMessage>();
						reply.TheNumber = sampleMsg.TheNumber;
						serverNode.SendToAll(reply);
					}
				};

				serverNode.Start();

				using (var clientNode = TestBootstrap.BuildClient())
				{
					clientNode.OnReceived = msg =>
					{
						Console.Out.WriteLine("Client received msg: " + msg);
						SampleGuaranteedMessage sampleMsg = msg as SampleGuaranteedMessage;
						if (sampleMsg != null)
						{
							Assert.AreEqual(previousReceivedClient + 1, sampleMsg.TheNumber);
							previousReceivedClient = sampleMsg.TheNumber;
						}
					};

					clientNode.Start();

					NeutrinoConfig.LogLevel = NeutrinoLogLevel.Warn;

					// Finish connect msg, etc.
					for (int i = 0; i < 100; i++)
					{
						clientNode.Update();
						serverNode.Update();
						Thread.Sleep(10);
					}

					int numOutbound = 0;
					foreach (var client in clientNode.ConnectedPeers)
						numOutbound += client.NumberOfOutboundMessages;
					Assert.AreEqual(0, numOutbound, "Client should have purged and ack'ed all messages at this point");

					numOutbound = 0;
					foreach (var client in serverNode.ConnectedPeers)
						numOutbound += client.NumberOfOutboundMessages;
					Assert.AreEqual(0, numOutbound, "Server should have purged and ack'ed all messages at this point");

					NeutrinoConfig.Log("Beginning guaranteed messages...");

					var clientMsg = clientNode.GetMessage<SampleGuaranteedMessage>();
					for (int i = 0; i < 100; i++)
					{
						clientMsg.TheNumber = i + 1;
						clientNode.SendToAll(clientMsg);
						clientNode.Update();
						serverNode.Update();
						//Thread.Sleep(1);
					}

					for (int i = 0; i < 10; i++)
					{
						clientNode.Update();
						serverNode.Update();
						Thread.Sleep(100);
					}

					Assert.AreEqual(100, previousReceivedServer);
					Assert.AreEqual(100, previousReceivedClient);
				}
			}
		}

		[Test]
		public void TestLossAndOrder()
		{
			int previousReceivedServer = 0;
			int previousReceivedClient = 0;

			using (var serverNode = TestBootstrap.BuildServer())
			{
				serverNode.OnReceived = msg =>
				{
					Console.Out.WriteLine("Server received msg: " + msg);
					SampleGuaranteedMessage sampleMsg = msg as SampleGuaranteedMessage;
					if (sampleMsg != null)
					{
						Assert.AreEqual(previousReceivedServer + 1, sampleMsg.TheNumber, "Server received " + sampleMsg.TheNumber + " out of sequence");
						previousReceivedServer = sampleMsg.TheNumber;
						var reply = serverNode.GetMessage<SampleGuaranteedMessage>();
						reply.TheNumber = sampleMsg.TheNumber;
						serverNode.SendToAll(reply);
					}
				};

				serverNode.Start();

				using (var clientNode = TestBootstrap.BuildClient())
				{
					clientNode.OnReceived = msg =>
					{
						Console.Out.WriteLine("Client received msg: " + msg);
						SampleGuaranteedMessage sampleMsg = msg as SampleGuaranteedMessage;
						if (sampleMsg != null)
						{
							Assert.AreEqual(previousReceivedClient + 1, sampleMsg.TheNumber, "Client received " + sampleMsg.TheNumber + " out of sequence");
							previousReceivedClient = sampleMsg.TheNumber;
						}
					};

					clientNode.Start();

					// Finish connect msg, etc.
					for (int i = 0; i < 100; i++)
					{
						clientNode.Update();
						serverNode.Update();
						Thread.Sleep(10);
					}

					int numOutbound = 0;
					foreach (var client in clientNode.ConnectedPeers)
						numOutbound += client.NumberOfOutboundMessages;
					Assert.AreEqual(0, numOutbound, "Client should have purged and ack'ed all messages at this point");

					numOutbound = 0;
					foreach (var client in serverNode.ConnectedPeers)
						numOutbound += client.NumberOfOutboundMessages;
					Assert.AreEqual(0, numOutbound, "Server should have purged and ack'ed all messages at this point");

#if DEBUG
					serverNode.SimulatedPacketLossRate = 0.1;
					serverNode.SimulatedPacketShuffleRate = 0.1;
#else
					Console.Error.WriteLine("Running packet loss unit test on a RELEASE build, which is fruitless - packet loss simulation only works on DEBUG builds - you should run this test on a DEBUG configuration");
#endif
					Console.Out.WriteLine("Beginning guaranteed messages...");

					const int numToSend = 1000;
					var clientMsg = clientNode.GetMessage<SampleGuaranteedMessage>();
					for (int i = 0; i < numToSend; i++)
					{
						clientMsg.TheNumber = i + 1;
						clientNode.SendToAll(clientMsg);
						clientNode.Update();
						serverNode.Update();
						Thread.Sleep(10);
					}

					for (int i = 0; i < 1000; i++)
					{
						clientNode.Update();
						serverNode.Update();
						Thread.Sleep(10);
					}

					Assert.AreEqual(numToSend, previousReceivedServer);
					Assert.AreEqual(numToSend, previousReceivedClient);
					Assert.AreEqual(0, clientNode.NumberOfOutboundMessages);
					Assert.AreEqual(0, serverNode.NumberOfOutboundMessages);
				}
			}
		}	
	}
}

