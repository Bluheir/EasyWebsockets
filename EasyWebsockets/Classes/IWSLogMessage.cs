using System;

namespace EasyWebsockets.Classes
{
	/// <summary>
	/// Represents a log message.
	/// </summary>
	public interface IWSLogMessage
	{
		/// <summary>
		/// Content of log message.
		/// </summary>
		string Content { get; }
		/// <summary>
		/// Time of log message.
		/// </summary>
		DateTime Time { get; }
		/// <summary>
		/// Thread id of where log originated.
		/// </summary>
		int Thread { get; }
		/// <summary>
		/// The process of where the log originated.
		/// </summary>
		string Process { get; }
		/// <summary>
		/// The type of log message.
		/// </summary>
		WSLogMessageType Type { get; }
	}
}
