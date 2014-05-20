using System;

namespace Neutrino.Core
{
	internal class OutboundMessage
	{
		public OutboundMessage() 
		{
			Payload = new byte[NeutrinoConfig.MaxMessageSize];
		}

		internal ushort SequenceNumber { get; set; }
		internal byte[] Payload { get; set; }
		internal bool NeedsAck { get; set; }
		internal int PayloadLength { get; set; }
		internal Type ContainedMessageType { get; set; }
		internal int PreviousSendTicks { get; set; }

		public override string ToString()
		{
			return string.Format("[OutboundMessage: SequenceNumber={0}, Payload={1}, NeedsAck={2}, PayloadLength={3}, ContainedMessageType={4}]", SequenceNumber, Payload, NeedsAck, PayloadLength, ContainedMessageType);
		}
	}
}