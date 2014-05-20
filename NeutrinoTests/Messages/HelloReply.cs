using System;
using MsgPack.Serialization;
using Neutrino.Core.Messages;

namespace Neutrino.Tests
{
	public class HelloReply : NetworkMessage
	{
		public HelloReply()
		{
		}

		[MessagePackMember(0)]
		public int NumReceived { get; set; }
	}
}

