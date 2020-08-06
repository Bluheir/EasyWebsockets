namespace EasyWebsockets.Classes
{
	/// <summary>
	/// Represents a specific type of WebSocket log message
	/// </summary>
	public enum WSLogMessageType : byte
	{
		/// <summary>
		/// Log message of client disconnect.
		/// </summary>
		Disconnect,
		/// <summary>
		/// Log message of a client handshaking.
		/// </summary>
		Handshake,
		/// <summary>
		/// Log message of client initially connected.
		/// </summary>
		Connect,
		/// <summary>
		/// Log message of a client that sent bytes to the server.
		/// </summary>
		Receive,
		/// <summary>
		/// Log message of a client that had an unclean disconnect.
		/// </summary>
		AbruptDisconnect,
		/// <summary>
		/// Server attempted to handshake client, but was unsuccessful.
		/// </summary>
		UnsuccessfulHandshake
	}
}
