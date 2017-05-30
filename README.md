[![Build Status](https://travis-ci.org/Claytonious/Neutrino.svg?branch=master)](https://travis-ci.org/Claytonious/Neutrino)

# Neutrino
A low-latency, high performance network library for real-time apps and games, based entirely on UDP and written in C#. Neutrino works with either the full-blown CLR or with Unity3D, Xamarin, and other constrained and/or AOT runtimes. It is cross-platform and is regularly tested on Linux, Mac, Windows, iOS, and Android. It's well suited for use on both server and client or in a peer-to-peer topology.

# Features
Neutrino is built on UDP. It supports both guaranteed and unguaranteed messages, but does so purely with UDP. For reasoning about our avoidance of TCP even for guaranteed messages, see [this article](http://gafferongames.com/networking-for-game-programmers/udp-vs-tcp/) and [this paper](http://www.isoc.org/INET97/proceedings/F3/F3_1.HTM#s2).

Neutrino handles UDP socket traffic asynchronously on your behalf. It does so with basic threading that is safe and performant even on mobile devices. This pattern is suitable for one or hundreds of network peers per machine, so it's great for things like clients and real-time servers. However, this pattern isn't well suited to very high scale single-server scenarios (such as tens of thousands of clients on a single machine).

# Using Neutrino
## Nodes
Each process that participates in a network session with Neutrino is called a "Node". Typically, a single process on a machine will contain a single Neutrino node. Neutrino is agnostic to your network topology, but a common configuration is for a client to contain a node and for a server to contain a node. This is often the same codebase but with different configuration of the node to act as either a server or client as needed.

This node is simply an instance of the `Node` class. You create it and then periodically call its `Update` method.

## Servers vs. Clients
There's little distinction between the two. The only difference is that a node which is setup as a "server" opens a UDP socket on a well-known port number for others to connect to, whereas a "client" lets the operating system find a free port and connects to a well-known port number on a remote machine (typically a "server"). Aside from this detail of the initial setup, "clients" and "servers" are identical.

## Configuration
First you will want to configure Neutrino to suit your application. Use the static methods and properties on the `NeutrinoConfig` class.

You can configure how Neutrino writes log messages by specifying your own delegate to be called when log messages are emitted. The default is to simply write to `Console.Out` at a log level of `WARN` or higher. To customize this behavior, specify your own delegate for `NeutrinoConfig.OnLog` like this:
```c#
NeutrinoConfig.OnLog = (level, logMsg) =>
{
	// Use the log level if you wish...
	if (level == LogLevel.Error)
	{
		// Send the text of the log message somewhere...
		YourCustomLoggingSystem(level, logMsg);
	}
};
```

You can configure how Neutrino should create instances of `NetworkPeer` objects when it connects to other peers (or when they connect to it). The default behavior is to simply create new instances of the built-in `NetworkPeer` class, but you can specify a delegate to be called instead where you can create your own custom objects. You might use this, for example, to create instances of your application's specific user class, or just to track and manage all instances of `NetworkPeer` over time. For example:
```c#
NeutrinoConfig.CreatePeer = () =>
{
	var newPeer = new NetworkPeer();
	MyCustomListOfPeers.Add(newPeer);
	return newPeer;
};
```

You can configure the timeout for disconnections. Since Neutrino is based on UDP, which is a connectionless protocol, clients are considered to be disconnected whenever no message has been received from them within a specified timeout period. The default period is 10 seconds. That means that, in an interactive application, you will need to make sure your clients talk to one another at least once every 10 seconds in order to remain connected to Neutrino. (If you have no actual application traffic at some points in your app, then send a simple heartbeat message periodically). You can change the default to any number of milliseconds like this:
```c#
// Change the timeout to 20 seconds...
NeutrinoConfig.PeerTimeoutMillis = 20000;
```

## Messages
The heart of network communication in Neutrino is messages. You send messages from one peer to another. Each message can either be guaranteed or unguaranteed. Guaranteed messages are guaranteed to eventually arrive at the receiving peer, in the same order as it was sent relative to other guaranteed messages. Unguaranteed messages might get dropped before reaching the destination peer and can arrive in any order.

Unguaranteed messages are fast and have low overhead. Use them whenever you can. They make sense for things that are constantly changing, such as the position of objects in a real-time game or the colors of pixels in a video stream. Use guaranteed messages only for things that absolutely must arrive (and must arrive in order) for your application to work correctly.

Messages are instances of your own C# classes that derive from the `NetworkMessage` base class. They need to have a default, parameterless constructor. Define which properties of the class will be sent across the network by using the `MessagePackSequence` attribute, like this:

```c#
public class PlayerPositionMessage : NetworkMessage
{
	[MessagePackSequence(0)]
	int PositionX { get; set; }

	[MessagePackSequence(1)]
	int PositionY { get; set; }
}
```

Use a simple sequence number to uniquely identify each property. Inheritance is supported. All primitives are supported, as well as references to other messages, and collections of `List<T>` and `Dictionary<T,U>`.

*IMPORTANT*: Neutrino minimizes garbage collection pressure by aggressively reusing instances of your messages. Once you've defined a message type, don't manually create new instances of it for transmission to other peers. Instead, use the `Node.GetMessage<T>()` method to get instances. This will be covered below.

## Setup
Create a `Node` so that you can communicate with other peers. This entails creating a new instance of `Node` using one of its two constructors: the one for a server on a well-known port or the one for a client with an automatically selected port. In either case, you will need to specify a set of assemblies that will be scanned for subclasses of `NetworkMessage`. This should include all of the assemblies where you have defined your application's messages.

You can set custom delegates on your instance of `Node` to do things like handle the connection and disconnection of remote peers as well as handle the receipt of messages from those peers.

For example, to setup a server:
```c#
const int serverPort = 29877;
var serverNode = new Node(serverPort, typeof(PlayerPositionMessage).Assembly);
serverNode.OnPeerConnected += peer => Console.Out.WriteLine("New peer connected: " + peer);
serverNode.OnPeerDisconnected += peer => Console.Out.WriteLine("Peer disconnected: " + peer);
serverNode.OnReceived += msg => Console.Out.WriteLine("Received message: " + msg);
serverNode.Name = "Server"; // Any name that's sensible for you application is fine - this is mainly for clarity in logging
```

To setup a client:
```c#
const int serverPort = 29877;
var serverNode = new Node("NewUser1", "awesomegame.com", serverPort, typeof(PlayerPositionMessage).Assembly);
serverNode.OnPeerConnected += peer => Console.Out.WriteLine("New peer connected: " + peer);
serverNode.OnPeerDisconnected += peer => Console.Out.WriteLine("Peer disconnected: " + peer);
serverNode.OnReceived += msg => Console.Out.WriteLine("Received message: " + msg);
serverNode.Name = "Client"; // Any name that's sensible for you application is fine - this is mainly for clarity in logging
```

Once you've setup your `Node`, call its `Start()` method to open a UDP socket and prepare for usage.

## Running
Once you've instantiated a Node, to "run" the network layer simply call its `Update()` method periodically from within your app. Each invocation of this method pumps the event queue, handling incoming messages and sending outgoing messages as needed. How often you call this method depends on the needs of your app:
* Calling more frequently will lead to lower latency in getting and sending network messages, but will use more CPU.
* Calling less frequently will use less CPU, but introduce higher lag between the time a network message is sent/received vs its being handled.
You should probably call this method at something slightly higher than the rate at which your app needs to actually handle messages. For example, an action based first person shooter might want to call this method at something like 12 times per second, whereas a slower paced tactical wargame or physics-based vehicular game might only need to call this 4 times per second. Data oriented, non-game apps might get away with calling this extremely rarely, such as once every few seconds. Experiment with this rate and choose the lowest rate that still delivers acceptable performance for your users.

The `Update()` method is thread-safe so it can be called from a background thread, but make sure to consistently call it from the *same* thread.

## Dependencies
Neutrino makes use of [the MsgPack-Sharp library](https://github.com/scopely/msgpack-sharp) to serialize and deserialize network messages in an extremely lean binary format that minimizes GC heap churn.

