using System.Collections;
using MsgPack.Serialization;

namespace Neutrino.Core.Messages
{
	public class ResetNetworkIdsMessage : NetworkMessage
	{
		public ResetNetworkIdsMessage() : base()
		{
			IsGuaranteed = true;
		}

		[MessagePackMember(0)]
		public byte Data { get; set; }

		public override string ToString()
		{
			return string.Format("[ResetNetworkIdsMessage]");
		}
	}
}
