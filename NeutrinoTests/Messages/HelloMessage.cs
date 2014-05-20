using System;
using Neutrino.Core.Messages;
using MsgPack.Serialization;

namespace Neutrino.Tests
{
	public class HelloMessage : NetworkMessage
	{
		public HelloMessage()
		{
		}

		[MessagePackMember(0)]
		public int Num { get; set; }

		[MessagePackMember(1)]
		public string Text { get; set; }

		public override string ToString()
		{
			return string.Format("[HelloMessage: Num={0}]", Num);
		}
	}
}

