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
using System.IO;

namespace EasyWebsockets
{
	/// <summary>
	/// Represents an instance of an upgraded WebSocket connection.
	/// </summary>
	public class WebSocketInstance : IAsyncDisposable
	{
		private WSServerConfig? config;
		private TcpClient client;
		private readonly int? readTimeOut;
		private Stream? stream;
		private bool secure;
		private bool disposed;

		public WebSocketState SocketState { get; set; } = WebSocketState.Connecting;

		/// <summary>
		/// To be used internally OR when extending <seealso cref="GWebSocketServer{T}"/> or <seealso cref="WebSocketServer"/>
		/// </summary>
		public TcpClient Client { 
			set 
			{
				if (client == null)
					client = value;
			} 
		}

		/// <summary>
		/// To be used internally OR when extending <seealso cref="GWebSocketServer{T}"/> or <seealso cref="WebSocketServer"/>
		/// </summary>
		public WSServerConfig? Config {
			set
			{
				if (config == null)
				{
					config = value;
					secure = config.Certificate != null;
				}
			}
		}

		/// <summary>
		/// The handshake message the client sent.
		/// </summary>
		public GRequestHandler? HandshakeMessage { get; private set; }

		/// <summary>
		/// The Sec-WebSocket-Key in the original handshake.
		/// </summary>
		public string? SecWebSocketKey { get; private set; }

		/// <summary>
		/// The default constructor for upgrading a <seealso cref="TcpClient"/> to a WebSocket connection.
		/// </summary>
		/// <param name="client">The <seealso cref="TcpClient"/> to be upgraded.</param>
		/// <param name="timeout">The timeout number.</param>
		/// <param name="config">The config for the client.</param>
		public WebSocketInstance(TcpClient client, int? timeout = null, WSServerConfig? config = null)
		{
			Client = client;
			Config = config;
			readTimeOut = timeout;
		}

		/// <summary>
		/// To be used internally OR when extending <seealso cref="GWebSocketServer{T}"/> or <seealso cref="WebSocketServer"/>
		/// </summary>
		public WebSocketInstance()
		{
			
		}

		/// <summary>
		/// Sends a WebSocket frame to the client.
		/// </summary>
		/// <param name="dt">The data to be sent.</param>
		/// <param name="msgType">The type of message sent.</param>
		/// <returns>Completed Task</returns>
		public virtual async Task SendAsync(ArraySegment<byte> dt, WSOpcodeType msgType)
		{
			if (SocketState == WebSocketState.Closed ||
				SocketState == WebSocketState.Aborted ||
				SocketState == WebSocketState.CloseSent ||
				SocketState == WebSocketState.None ||
				SocketState == WebSocketState.Connecting)
				return;
			if (disposed)
				return;

			var data = dt.Array.SubArray(dt.Offset, dt.Count);

			byte[] d = WSArrayHelpers.ToFrameData(data, msgType);

			await stream.WriteAsync(d, 0, d.Length);

			if(msgType == WSOpcodeType.Close && !disposed)
			{
				if(SocketState == WebSocketState.Open)
				{
					SocketState = WebSocketState.CloseSent; // Close request is sent to websocket
				}
				else if(SocketState == WebSocketState.CloseReceived)
				{
					await DisposeAsync(); // Close request was sent back to client, dispose the client now
					SocketState = WebSocketState.Closed; // Set state
				}
			}
		}
		/// <summary>
		/// Receives a WebSocket packet from the client.
		/// </summary>
		/// <returns>Returns the external data, the status code of closing the connection (if there is one) and the opcode type of the message.</returns>
		public virtual async Task<Tuple<byte[], WebSocketCloseStatus, WSOpcodeType>?> ReceiveAsync()
		{
			if (SocketState == WebSocketState.Closed || 
				SocketState == WebSocketState.CloseReceived || 
				SocketState == WebSocketState.Aborted || 
				SocketState == WebSocketState.None || 
				SocketState == WebSocketState.Connecting)
				return null;
			if (disposed)
				return null;

			uint buff = 0;

			if (config.BufferSize != null)
				buff = config.BufferSize.GetValueOrDefault();
			else if (config.DynamicBufferSize != null)
				buff = await config.DynamicBufferSize(this);
			else
				buff = 65535;

			byte[] data = new byte[buff];

			int i = await stream.ReadAsync(data, 0, data.Length);
			data = data.SubArray(0, i);

			var t = WSArrayHelpers.ConvertFrame(data);
			if (t == null)
			{
				
				await DisposeAsync(true);
				return null;
			}

			if(t.Item4 != WSOpcodeType.Close && t.Item4 != WSOpcodeType.Ping && t.Item4 != WSOpcodeType.Pong && SocketState == WebSocketState.CloseSent)
			{
				await DisposeAsync();
				return null;
			}

			if (t.Item4 == WSOpcodeType.Close)
			{
				if(SocketState == WebSocketState.Open)
				{
					SocketState = WebSocketState.CloseReceived;
					await SendAsync(new ArraySegment<byte>(t.Item1), WSOpcodeType.Close);
					await DisposeAsync();

					SocketState = WebSocketState.Closed;
				}
				else if(SocketState == WebSocketState.CloseSent)
				{
					Console.WriteLine("yeah, got it matee");
					await DisposeAsync();
					SocketState = WebSocketState.Closed;
				}
			}

			return new Tuple<byte[], WebSocketCloseStatus, WSOpcodeType>(t.Item1, t.Item2, t.Item4);
		}
		
		/// <summary>
		/// Closes the websocket connection
		/// </summary>
		/// <param name="closeStatus">Close status</param>
		/// <param name="statusDescription">Optional status description</param>
		/// <returns>The completed task.</returns>
		public Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription = null)
		{
			return SendAsync(BitConverter.GetBytes((ushort)closeStatus).Join(Encoding.UTF8.GetBytes(statusDescription ?? "")), WSOpcodeType.Close);
		}

		/// <summary>
		/// Sends a ping packet to the client
		/// </summary>
		/// <returns>The completed task.</returns>
		public Task PingAsync()
		{
			return SendAsync(new ArraySegment<byte>(new byte[0]), WSOpcodeType.Ping);
		}
		/// <summary>
		/// Sends a pong packet to the client
		/// </summary>
		/// <returns>The completed task.</returns>
		public Task PongAsync()
		{
			return SendAsync(new ArraySegment<byte>(new byte[0]), WSOpcodeType.Pong);
		}

		/// <summary>
		/// Initiates a handshake with the TCP client.
		/// </summary>
		/// <returns>Returns if the handshake was successful or not.</returns>
		public virtual async Task<bool> HandShakeAsync()
		{
			if (SocketState != WebSocketState.Connecting)
				return false;

			var tcpStream = client.GetStream();

			if (secure)
			{
				var secureStream = new SslStream(tcpStream, false);
				stream = secureStream;


				try
				{
					await secureStream.AuthenticateAsServerAsync(
							config.Certificate,
							enabledSslProtocols: config.SslVersion,
							checkCertificateRevocation: false,
							clientCertificateRequired: false
					);
				}
				catch (System.ComponentModel.Win32Exception)
				{
					await DisposeAsync(true);
					return false;
				}
				catch(IOException)
				{
					await DisposeAsync(true);
					return false;
				}
				catch
				{
					throw;
				}
			}
			else
			{
				stream = tcpStream;

			}

			byte[] bytes = new byte[65536];
			stream.ReadTimeout = 10000;

			int i = await stream.ReadAsync(bytes, 0, bytes.Length);

			stream.ReadTimeout = -1;
			bytes = bytes.SubArray(0, i);

			var text = Encoding.UTF8.GetString(bytes);

			if(String.IsNullOrWhiteSpace(text))
			{
				await DisposeAsync(true);
				return false;
			}

			HandshakeMessage = GRequestHandler.ParseHeaders(bytes);

			return true;
		}

		/// <summary>
		/// Makes the server send a websocket handshake response
		/// </summary>
		/// <returns>If successful</returns>
		public virtual async Task<bool> FinalizeHandShakeAsync()
		{
			if (HandshakeMessage.StartHeaders["method"].ToLower() == "get")
			{
				SecWebSocketKey = HandshakeMessage.Headers["Sec-WebSocket-Key"];
				byte[] res = CreateHandShakeResponse();
				await stream.WriteAsync(res, 0, res.Length);

				SocketState = WebSocketState.Open; // The handshake is finalized.
				return true;
			}
			else
			{
				await DisposeAsync(false);
				return false;
			}
		}
		public byte[] CreateHandShakeResponse()
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
		/// Disposes this object asynchronously
		/// </summary>
		/// <returns>The completed task.</returns>
		public ValueTask DisposeAsync()
		{
			return DisposeAsync(false);
		}

		/// <summary>
		/// Disposes the current object and sends a close frame. To be used in response to a close request from the client.
		/// </summary>
		/// <param name="aborted">If the connection was aborted</param>
		/// <returns>Returns the completed task.</returns>
		public async ValueTask DisposeAsync(bool aborted)
		{
			if (disposed)
				return;
			disposed = true;

			if (SocketState == WebSocketState.Aborted ||
				SocketState == WebSocketState.None)
				return;

			if (aborted)
				SocketState = WebSocketState.Aborted;

			client.Close();
		}
	}
}
