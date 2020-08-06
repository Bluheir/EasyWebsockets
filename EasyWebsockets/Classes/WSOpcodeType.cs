namespace EasyWebsockets.Classes
{
	/// <summary>
	/// Represents an opcode of a WebSocket frame.
	/// </summary>
	public enum WSOpcodeType : byte
	{
		/// <summary>
		/// Opcode type of text.
		/// </summary>
		Text = 0x1,
		/// <summary>
		/// Opcode type of binary.
		/// </summary>
		Binary = 0x2,
		/// <summary>
		/// Opcode type of close.
		/// </summary>
		Close = 0x8,
		/// <summary>
		/// Opcode type of ping.
		/// </summary>
		Ping = 0x9,
		/// <summary>
		/// Opcode type of pong.
		/// </summary>
		Pong = 0xA
	}
}
