using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Security.Authentication;
using System.Text;
using EasyWebsockets.Helpers;
using EasyWebsockets.Classes;
using System.Net.WebSockets;
using System.Threading;

namespace EasyWebsockets
{
	public class WebSocketInstance : IAsyncDisposable
	{
		private readonly TlsConfig? cert;
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
		public WebSocketInstance(TcpClient client, int? timeout = null, TlsConfig? cert = null)
		{
			this.client = client;
			this.secure = cert != null && cert.Certificate != null;
			this.cert = cert;
			readTimeOut = timeout;
		}
		/// <summary>
		/// Sends a WebSocket frame to the client.
		/// </summary>
		/// <param name="data">The data to be sent.</param>
		/// <param name="msgType">The type of message sent.</param>
		/// <returns>Completed Task</returns>
		public virtual async Task SendAsync(byte[] data, WSOpcodeType msgType)
		{
			if (!handShaked)
				return;
			if (disposed)
				return;
			if (closed)
				return;
			//if (data.Array == null)
				//throw new ArgumentNullException($"Inner array of argument \"{nameof(data)}\" cannot be equal to null.");
			byte[] d = WSArrayHelpers.ToFrameData(data, msgType);

			if (secure)
				await secureStream.WriteAsync(d, 0, d.Length);
			else
				await client.GetStream().WriteAsync(d, 0, d.Length);

			if(msgType == WSOpcodeType.Close && !disposed)
			{
				await DisposeAsync();
			}
		}
		/// <summary>
		/// Receives a WebSocket packet from the client.
		/// </summary>
		/// <returns>Returns the external data, the status code of closing the connection (if there is one) and the opcode type of the message.</returns>
		public async Task<Tuple<byte[], WebSocketCloseStatus, WSOpcodeType>?> ReceiveAsync()
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
				data = data.SubArray(0, i);
			}
			else
			{
				int i = await client.GetStream().ReadAsync(data, 0, data.Length);
				data = data.SubArray(0, i);
			}
			;
			var t = WSArrayHelpers.ConvertFrame(data);

			if (t == null)
			{
				await DisposeAsync();
				return null;
			}
			if(t.Item4 == WSOpcodeType.Close)
			{
				await CloseAsync(WebSocketCloseStatus.Empty, null);
				await DisposeAsync(false);
			}
			return new Tuple<byte[], WebSocketCloseStatus, WSOpcodeType>(t.Item1, t.Item2, t.Item4);
		}
		
		public virtual Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription)
		{
			return SendAsync(BitConverter.GetBytes((ushort)closeStatus).Join(Encoding.UTF8.GetBytes(statusDescription ?? "")), WSOpcodeType.Close);
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


				try
				{
					await secureStream.AuthenticateAsServerAsync(
							cert.Certificate,
							enabledSslProtocols: cert.SslVersion,
							checkCertificateRevocation: false,
							clientCertificateRequired: false
					);
				}
				catch (System.ComponentModel.Win32Exception)
				{
					await DisposeAsync(false);
					return false;
				}
				catch
				{
					throw;
				}
				
				if (readTimeOut != null)
				{
					secureStream.ReadTimeout = readTimeOut.GetValueOrDefault();
				}
				byte[] bytes = new byte[65536];
				int i = await secureStream.ReadAsync(bytes, 0, bytes.Length);
				bytes = bytes.SubArray(0, i);

				HandshakeMessage = GRequestHandler.ParseHeaders(bytes);

				if (HandshakeMessage.StartHeaders["method"].ToLower() == "get")
				{
					SecWebSocketKey = HandshakeMessage.Headers["Sec-WebSocket-Key"];
					byte[] res = CreateHandShakeResponse();
					await secureStream.WriteAsync(res, 0, res.Length);
				}
				else
				{
					await DisposeAsync(false);
					return false;
				}
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
				{
					await DisposeAsync(false);
					return false;
				}
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

		
		/// <summary>
		/// Disposes the current object.
		/// </summary>
		/// <returns>Returns the completed task.</returns>
		public async ValueTask DisposeAsync()
		{
			if (disposed)
				return;
			disposed = true;
			if (!closed)
			{
				closed = true;
				await CloseAsync(WebSocketCloseStatus.NormalClosure, "");
			}
			var stream = client.GetStream();
			if (secure)
				await secureStream.DisposeAsync();
			else
				await stream.DisposeAsync();
			client.Dispose();
		}
		/// <summary>
		/// Disposes the current object.
		/// </summary>
		/// <param name="closeConnection">Close the connection if it is running.</param>
		/// <returns>Returns the completed task.</returns>
		public async ValueTask DisposeAsync(bool closeConnection)
		{
			if (disposed)
				return;
			disposed = true;
			if (closeConnection && !closed)
			{
				closed = true;
				await CloseAsync(WebSocketCloseStatus.NormalClosure, "");
			}
			
			closed = true;
			var stream = client.GetStream();
			if (secure)
				await secureStream.DisposeAsync();
			else
				await stream.DisposeAsync();
			client.Dispose();
		}
	}
}
