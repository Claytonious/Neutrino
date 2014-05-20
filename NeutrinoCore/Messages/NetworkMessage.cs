using System;
using System.Collections;
using scopely.msgpacksharp;
using Neutrino.Core;

namespace Neutrino.Core.Messages
{
	public abstract class NetworkMessage
	{
		private static readonly object[] emptyArgs = new object[] {};
		private static readonly byte[] cloneBuffer = new byte[NeutrinoConfig.MaxMessageSize];

		public NetworkMessage()
		{
		}

		public byte Id { get; set; }
		public bool IsGuaranteed { get; set; }
		public NetworkPeer Source { get; set; }
		public byte SequenceNumber { get; set; }

		public int Write(byte[] buffer)
		{
			int offset = 1;
			buffer[0] = Id;
			if (IsGuaranteed)
			{
				buffer[1] = SequenceNumber;
				offset = 2;
			}
			return MsgPackSerializer.SerializeObject(this, buffer, offset);
		}

		public NetworkMessage Clone()
		{
			MsgPackSerializer.SerializeObject(this, cloneBuffer, 0);
			var result = (NetworkMessage)(GetType().GetConstructor(Type.EmptyTypes).Invoke(emptyArgs));
			MsgPackSerializer.DeserializeObject(result, cloneBuffer, 0);
			result.SequenceNumber = SequenceNumber;
			result.Source = Source;
			result.IsGuaranteed = IsGuaranteed;
			result.Id = Id;
			return result;
		}
	}
}