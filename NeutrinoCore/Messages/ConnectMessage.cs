using System.Collections;
using MsgPack.Serialization;

namespace Neutrino.Core.Messages
{
	public class ConnectMessage : NetworkMessage
	{
		public ConnectMessage() : base()
		{
			IsGuaranteed = true;
		}

		[MessagePackMember(0)]
		public string Nickname { get; set; }

		public override string ToString ()
		{
			return string.Format ("[ConnectMessage: Nickname={0}]", Nickname);
		}
	}
}