using System;
using System.Net;

namespace Neutrino.Core
{
	public class ReceivedBuffer
	{
		public ReceivedBuffer()
		{
			Buffer = new byte[NeutrinoConfig.MaxMessageSize];
		}

		public IPEndPoint Endpoint { get; set; }
		public byte[] Buffer { get; set; }
		public int NumBytesReceived { get; set; }
	}
}

