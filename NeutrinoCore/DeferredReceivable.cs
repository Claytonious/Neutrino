using System;
using System.Net;

namespace Neutrino.Core
{
	public class DeferredReceivable
	{
		public DeferredReceivable()
		{
		}

		public byte[] ReceivedBuffer { get; set; }
		public int TimeToReceiveTicks { get; set; }
		public IPEndPoint Endpoint { get; set; }
	}
}

