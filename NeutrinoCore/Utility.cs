using System;
using System.Text;

namespace Neutrino.Core
{
	public static class Utility
	{
		public static readonly object[] emptyArgs = new object[] {};

		public static string ToByteString(byte[] buffer, int length)
		{
			StringBuilder result = new StringBuilder(length * 3);
			for (int i = 0; i < length; i++)
				result.Append(string.Format ("{0:x} ",buffer[i]));
			return result.ToString();
		}
	}
}

