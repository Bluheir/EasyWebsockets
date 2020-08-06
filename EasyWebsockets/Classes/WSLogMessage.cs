using System;

namespace EasyWebsockets.Classes
{
	/// <summary>
	/// Represents a log message.
	/// </summary>
	public class WSLogMessage : IWSLogMessage
	{
		public string Content { get; }
		public DateTime Time { get; }
		public int Thread { get; }
		public string Process { get; }
		public WSLogMessageType Type { get; }

		public WSLogMessage(string content, DateTime time, int thread, string process, WSLogMessageType type)
		{
			Content = content;
			Time = time;
			Thread = thread;
			Process = process;
			Type = type;
		}
	}
}
