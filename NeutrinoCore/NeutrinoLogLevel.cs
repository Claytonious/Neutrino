using System;

namespace Neutrino.Core
{
	// These log level values match those of Unity3d, to easy integration in that environment. But they can be easily
	// adapted for any environment in the logging delegate itself.
	public enum NeutrinoLogLevel : int
	{
		Debug = 0,
		Warn = 1,
		Error = 2
	}
}

