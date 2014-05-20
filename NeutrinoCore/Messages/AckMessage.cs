using System.Collections;
using MsgPack.Serialization;

namespace Neutrino.Core.Messages
{
	public class AckMessage : NetworkMessage
	{
		public AckMessage() : base()
		{
		}

		[MessagePackMember(0)]
		public ushort AckedSequenceNumber { get; set; }

		public override string ToString()
		{
			return string.Format("[AckMessage: AckedSequenceNumber={0}]", AckedSequenceNumber);
		}
	}
}
