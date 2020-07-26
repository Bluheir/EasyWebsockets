using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Security.Authentication;
using System.Text;
using EasyWebsockets.Helpers;
using EasyWebsockets.Classes;

namespace EasyWebsockets
{
	public class WebSocketInstance : IAsyncDisposable
	{
		private readonly X509Certificate2? cert;
		private readonly TcpClient client;
		private readonly int? readTimeOut;
		private SslStream? secureStream;
		private readonly bool secure;
		private bool handShaked;
		private bool closed;
		private bool disposed;

		/// <summary>
		/// The handshake message the client sent.
		/// </summary>
		public GRequestHandler? HandshakeMessage { get; private set; }
		/// <summary>
		/// The Sec-WebSocket-Key in the original handshake.
		/// </summary>
		public string? SecWebSocketKey { get; private set; }

		/// <summary>
		/// Default constructor with a client to be upgraded, timeout for receiving, and certificate.
		/// </summary>
		/// <param name="client">The <see cref="TcpClient"/> to be upgraded to a websocket connection.</param>
		/// <param name="timeout">The timeout for receiving messages.</param>
		/// <param name="cert">The X509 certificate for secure connections.</param>
		public WebSocketInstance(TcpClient client, int? timeout = null, X509Certificate2? cert = null)
		{
			this.client = client;
			this.secure = cert != null;
			this.cert = cert;
			readTimeOut = timeout;
		}
		/// <summary>
		/// Sends a WebSocket frame to the client.
		/// </summary>
		/// <param name="data">The data to be sent.</param>
		/// <param name="msgType">The type of message sent.</param>
		/// <returns></returns>
		public virtual async Task SendAsync(ArraySegment<byte> data, WSOpcodeType msgType)
		{
			if (!handShaked)
				return;
			if (disposed)
				return;
			if (closed)
				return;
			if (data.Array == null)
				throw new ArgumentNullException($"Inner array of argument \"{nameof(data)}\" cannot be equal to null.");
			byte[] d = ToFrameData(data.Array, msgType);

			if (secure)
				await secureStream.WriteAsync(d, 0, d.Length);
			else
				await client.GetStream().WriteAsync(d, 0, d.Length);

			if (msgType == WSOpcodeType.Close)
			{
				closed = true;
				await DisposeAsync();
			}
		}
		/// <summary>
		/// Receives a WebSocket packet from the client.
		/// </summary>
		/// <returns>Returns the external data, the status code of closing the connection (if there is one) and the opcode type of the message.</returns>
		public async Task<Tuple<byte[], ushort?, WSOpcodeType>?> ReceiveAsync()
		{
			if (!handShaked)
				return null;
			if (disposed)
				return null;
			if (closed)
				return null;

			byte[] data = new byte[65535];
			if (secure)
			{
				int i = await secureStream.ReadAsync(data, 0, data.Length);
				Console.WriteLine("booyahhhhhhh");
				data = SubArray(data, 0, i);
			}
			else
			{
				int i = await client.GetStream().ReadAsync(data, 0, data.Length);
				Console.WriteLine("booyah");
				data = SubArray(data, 0, i);
			}
			var t = ConvertFrame(data);

			if (t == null)
			{
				await DisposeAsync();
			}
			return new Tuple<byte[], ushort?, WSOpcodeType>(t.Item1, t.Item2, t.Item4);
		}
		private static byte[] ToFrameData(ArraySegment<byte> data, WSOpcodeType msgType)
		{
			var arr = data.Array;
			if (arr == null)
				throw new ArgumentNullException($"Inner array of argument \"{nameof(data)}\" cannot be equal to null.");
			byte op = (byte)msgType;
			byte len = 125;
			int offset = 2;
			byte[] retval;

			if (arr.Length > 125 && arr.Length < ushort.MaxValue)
			{
				len = 126;
				offset = 4;
				retval = new byte[arr.Length + offset - 1];
				var b = BitConverter.GetBytes((ushort)arr.Length);
				retval[0] = op;
				retval[1] = len;
				retval[2] = b[0];
				retval[3] = b[1];

			}
			else if (arr.Length <= 125)
			{
				retval = new byte[arr.Length + offset - 1];
				retval[0] = op;
				retval[1] = len;
			}
			else
			{
				len = 127;
				offset = 6;
				retval = new byte[arr.Length + offset - 1];
				var b = BitConverter.GetBytes(arr.Length);
				retval[0] = op;
				retval[1] = len;
				retval[2] = b[0];
				retval[3] = b[1];
				retval[4] = b[2];
				retval[5] = b[3];
			}

			for (int i = 0; i < arr.Length; i++)
			{
				retval[offset + i] = arr[i];
			}

			return retval;
		}
		public virtual Task CloseAsync()
		{
			return SendAsync(new ArraySegment<byte>(new byte[0]), WSOpcodeType.Close);
		}
		/// <summary>
		/// Initiates a handshake with the TCP client.
		/// </summary>
		/// <returns>Returns if the handshake was successful or not.</returns>
		public virtual async Task<bool> HandShakeAsync()
		{
			if (handShaked)
				return false;
			var stream = client.GetStream();

			if (secure)
			{
				secureStream = new SslStream(stream, false);

				if (readTimeOut != null)
				{
					secureStream.ReadTimeout = readTimeOut.GetValueOrDefault();
				}
				;
				await secureStream.AuthenticateAsServerAsync(cert, enabledSslProtocols: SslProtocols.Tls12, checkCertificateRevocation: false, clientCertificateRequired: false);
				;
				byte[] bytes = new byte[65536];
				int i = await secureStream.ReadAsync(bytes, 0, bytes.Length);
				bytes = SubArray(bytes, 0, i);

				HandshakeMessage = GRequestHandler.ParseHeaders(bytes);

				if (HandshakeMessage.StartHeaders["method"].ToLower() == "get")
				{
					SecWebSocketKey = HandshakeMessage.Headers["Sec-WebSocket-Key"];
					byte[] res = CreateHandShakeResponse();
					await secureStream.WriteAsync(res, 0, res.Length);
				}
				else
					return false;
			}
			else
			{
				if (readTimeOut != null)
				{
					stream.ReadTimeout = readTimeOut.GetValueOrDefault();
				}
				while (client.Available < 3) { }
				byte[] bytes = new byte[client.Available];

				await stream.ReadAsync(bytes, 0, bytes.Length);

				HandshakeMessage = GRequestHandler.ParseHeaders(bytes);

				if (HandshakeMessage.StartHeaders["method"].ToLower() == "get")
				{
					SecWebSocketKey = HandshakeMessage.Headers["Sec-WebSocket-Key"];
					byte[] res = CreateHandShakeResponse();
					await stream.WriteAsync(res, 0, res.Length);
				}
				else
					return false;
			}
			handShaked = true;
			return true;
		}
		private byte[] CreateHandShakeResponse()
		{
			const string eol = "\r\n";

			string msg = ""
				+ "HTTP/1.1 101 Switching Protocols" + eol
				+ "Connection: Upgrade" + eol
				+ "Upgrade: websocket" + eol
				+ "Sec-WebSocket-Accept: " + Convert.ToBase64String(System.Security.Cryptography.SHA1.Create().ComputeHash(
					Encoding.UTF8.GetBytes(
					SecWebSocketKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")))
				+ eol
				+ eol;

			;
			return Encoding.UTF8.GetBytes(msg);
		}

		private static Tuple<byte[], ushort?, ulong, WSOpcodeType>? ConvertFrame(byte[] frame)
		{
			bool fin = (frame[0] & 0b10000000) != 0;
			bool mask = (frame[1] & 0b10000000) != 0;

			if (!mask)
			{
				return null;
			}

			int opcode = frame[0] & 0b00001111;
			ulong msgLen = (ulong)(frame[1] - 128);
			int offset = 2;

			if (msgLen == 126)
			{
				msgLen = (ulong)BitConverter.ToInt16(new byte[] { frame[3], frame[2] });
				offset = 4;
			}
			else if (msgLen == 127)
			{
				msgLen = BitConverter.ToUInt64(new byte[] { frame[9], frame[8], frame[7], frame[6], frame[5], frame[4], frame[3], frame[2] });
				offset = 6;
			}

			byte[] maskingKey = new byte[] { frame[offset], frame[offset + 1], frame[offset + 2], frame[offset + 3] };
			offset += 4;

			byte[] data = new byte[msgLen];
			int m = 0;

			for (int i = offset; i < frame.Length; i++)
			{
				data[m] = (byte)(frame[i] ^ maskingKey[m % 4]);
				m++;
			}

			ushort? statusCode = null;
			if (opcode == (int)WSOpcodeType.Close && msgLen > 125)
			{
				statusCode = BitConverter.ToUInt16(new byte[] { data[0], data[1] });
			}

			return new Tuple<byte[], ushort?, ulong, WSOpcodeType>(data, statusCode, msgLen, (WSOpcodeType)opcode);
		}
		/// <summary>
		/// Disposes the current object.
		/// </summary>
		/// <returns>Returns the completed task.</returns>
		public async ValueTask DisposeAsync()
		{
			if (disposed)
				return;
			if (!closed)
			{
				closed = true;
				await CloseAsync();
			}
			if (secure)
				secureStream?.Dispose();
			else
				client.GetStream().Dispose();

			client.Dispose();
			disposed = true;

		}
		private static T[] SubArray<T>(T[] data, int index, int length)
		{
			T[] result = new T[length];
			Array.Copy(data, index, result, 0, length);
			return result;
		}
	}
}
