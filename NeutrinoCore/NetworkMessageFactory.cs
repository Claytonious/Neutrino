using System.Collections.Generic;
using System;
using scopely.msgpacksharp;
using Neutrino.Core.Messages;
using System.Reflection;

namespace Neutrino.Core
{
	public class NetworkMessageFactory
	{
		private Dictionary<byte, NetworkMessage> messages = new Dictionary<byte, NetworkMessage>();
		private Dictionary<Type, NetworkMessage> messagesByType = new Dictionary<Type, NetworkMessage>();

		internal NetworkMessageFactory()
		{
		}

		public void Init(params Assembly[] messageAssemblies)
		{
			BuildInstances(messageAssemblies);
		}

		public T Get<T>() where T : NetworkMessage
		{
			NetworkMessage result = null;
			if (!messagesByType.TryGetValue(typeof(T), out result))
				throw new ApplicationException("Attempt to get network message of type " + typeof(T) + " failed because that message type hasn't been registered");
			return (T)result;
		}

		internal IEnumerable<NetworkMessage> Read(byte[] buffer, int length)
		{
			int overallOffset = 0;
			while (overallOffset < length)
			{
				int offset = 1;
				var result = messages[buffer[overallOffset]];
				if (result.IsGuaranteed)
				{
					result.SequenceNumber = buffer[overallOffset + 1];
					offset = 2;
				}
				//if (NeutrinoConfig.LogLevel == NeutrinoLogLevel.Debug)
				//NeutrinoConfig.Log("Factory read message at offset " + overallOffset + " type " + buffer[0] + " which is " + result);
				try
				{
					overallOffset = MsgPackSerializer.DeserializeObject(result, buffer, overallOffset + offset);
				}
				catch(Exception)
				{
					NeutrinoConfig.LogError("Failed to deserialize " + result.GetType() + " from buffer at position " + (overallOffset + offset) + ": " + Utility.ToByteString(buffer, buffer.Length));
					throw;
				}
				yield return result;
			}
		}

		internal NetworkMessage Read(byte[] buffer)
		{
			int offset = 1;
			var result = messages[buffer[0]];
			if (result.IsGuaranteed)
			{
				result.SequenceNumber = buffer[1];
				offset = 2;
			}
			MsgPackSerializer.DeserializeObject(result, buffer, offset);
			return result;
		}

		private void BuildInstances(params Assembly[] messageAssemblies)
		{
			BuildInstances(typeof(NetworkMessage).Assembly);
			foreach (Assembly a in messageAssemblies)
				BuildInstances(a);
			NeutrinoConfig.Log("Built " + messages.Count + " registered network messages");
		}

		private void BuildInstances(Assembly messageAssembly)
		{
			Type networkMsgType = typeof(NetworkMessage);
			foreach(Type t in messageAssembly.GetTypes())
			{
				if (t.IsSubclassOf(networkMsgType) && !messagesByType.ContainsKey(t))
				{
					if (messages.Count == Byte.MaxValue)
						throw new ApplicationException("The maximum number of network messages has been reached - you need to use fewer message types in this project");
					var msg = (NetworkMessage)t.GetConstructor(Type.EmptyTypes).Invoke(Utility.emptyArgs);
					msg.Id = (byte)messages.Count;
					messages[msg.Id] = msg;
					messagesByType[msg.GetType()] = msg;
					NeutrinoConfig.Log("Registered message type " + msg.GetType() + " as Id " + msg.Id);
				}
			}
		}
	}
}
