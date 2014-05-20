using System;
using Neutrino.Core.Messages;
using MsgPack.Serialization;

namespace Neutrino.Tests
{
	public class SampleGuaranteedMessage : NetworkMessage
	{
		public SampleGuaranteedMessage()
		{
			IsGuaranteed = true;
		}

		[MessagePackMember(0)]
		public int TheNumber { get; set; }

		public override string ToString()
		{
			return string.Format("[SampleGuaranteedMessage: TheNumber={0} Sequence={1}]", TheNumber, SequenceNumber);
		}
	}
}

