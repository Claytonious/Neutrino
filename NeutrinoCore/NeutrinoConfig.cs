using System;
using Neutrino.Core.Messages;

namespace Neutrino.Core
{
	public static class NeutrinoConfig
	{
		static NeutrinoConfig()
		{
			MaxMessageSize = 1200; // Stay below common MTU
			LogLevel = NeutrinoLogLevel.Warn;
			OnLog = (level, msg) =>
			{
				if ((int)level >= (int)LogLevel)
					Console.Out.WriteLine("NTO " + level.ToString() + ": " + msg);
			};
			CreatePeer = () =>
			{
				return new NetworkPeer();
			};
			PeerTimeoutMillis = 10000;
		}

		public const int MaxPendingGuaranteedMessages = ushort.MaxValue - 1;

		public static Func<NetworkPeer> CreatePeer { get; set; }
		public static Action<NeutrinoLogLevel, string> OnLog { get; set; }
		public static NeutrinoLogLevel LogLevel { get; set; }
		public static int PeerTimeoutMillis { get; set; }

		public static int MaxMessageSize { get; set; }

		public static void Log(string msg)
		{
			OnLog(NeutrinoLogLevel.Debug, msg);
		}

		public static void LogWarning(string msg)
		{
			OnLog(NeutrinoLogLevel.Warn, msg);
		}

		public static void LogError(string msg)
		{
			OnLog(NeutrinoLogLevel.Error, msg);
		}
	}
}

