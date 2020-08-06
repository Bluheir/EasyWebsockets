using EasyWebsockets.Helpers;
using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Net.WebSockets;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EasyWebsockets.Classes;

namespace EasyWebsockets
{
	/// <summary>
	/// Represents a generic WebSocket server.
	/// </summary>
	/// <typeparam name="T">The type of WebSocket client. Must inherit from <seealso cref="WebSocketInstance"/>.</typeparam>
	public class GWebSocketServer<T> : IAsyncDisposable where T : WebSocketInstance, new()
	{
		private readonly Dictionary<T, CancellationTokenSource> _clients;
		private readonly TcpListener _listener;
		private readonly WSServerConfig cert;
		private readonly bool secure;
		private readonly CancellationTokenSource token;

		private bool disposed;
		private Func<T, Task>? onConnect = x => Task.CompletedTask;
		private Func<T, byte[]?, WebSocketCloseStatus, Task>? onDisconnect = (x, y, z) => Task.CompletedTask;
		private Func<T, byte[], WSOpcodeType, Task>? onReceive = (w, x, y) => Task.CompletedTask;
		private Func<IWSLogMessage, Task>? log = x => Task.CompletedTask;

		/// <summary>
		/// The function to invoke when a client connects.
		/// </summary>
		public event Func<T, Task> OnConnect { add => onConnect += value; remove => onConnect -= value; }
		/// <summary>
		/// The function to invoke when a client disconnects.
		/// </summary>
		public event Func<T, byte[]?, WebSocketCloseStatus, Task> OnDisconnect { add => onDisconnect += value; remove => onDisconnect -= value; }
		/// <summary>
		/// The function to invoke when a client sends bytes with the Opcode type.
		/// </summary>
		public event Func<T, byte[], WSOpcodeType, Task> OnReceive { add => onReceive += value; remove => onReceive -= value; }
		/// <summary>
		/// The logging function. Has logs actions like AbruptDisconnect, Disconnect, Receive, Handshake, and Connect.
		/// </summary>
		public event Func<IWSLogMessage, Task> Log { add => log += value; remove => log -= value; }

		/// <summary>
		/// The IPEndpoint the server is listening on.
		/// </summary>
		public IPEndPoint Endpoint { get; }

		/// <summary>
		/// Constructor with an <see cref="IPEndPoint"/>, certificate and password.
		/// </summary>
		/// <param name="endpoint">The <see cref="IPEndPoint"/> to listen on.</param>
		/// <param name="cert">The configuration for the WebSocket server.</param>
		public GWebSocketServer(IPEndPoint endpoint, WSServerConfig? cert)
		{
			_clients = new Dictionary<T, CancellationTokenSource>();
			_listener = new TcpListener(endpoint);

			if (cert != null && cert.Certificate == null)
			{
				cert.LoadCertFromPfx();
			}
			secure = cert != null && cert.Certificate != null;

			this.cert = cert ?? new WSServerConfig();

			Endpoint = endpoint;
			token = new CancellationTokenSource();
		}
		/// <summary>
		/// Constructor with a string ip endpoint.
		/// </summary>
		/// <param name="endpoint">The ip endpoint to listen on.</param>
		/// <param name="cert">The configuration for the WebSocket server.</param>
		public GWebSocketServer(string endpoint, WSServerConfig? cert = null) : this(ParseIPEndPoint(endpoint), cert) { } // will fix later
		/// <summary>
		/// Constructor with an <see cref="IPAddress"/> and port, certificate file path, and password to certificate.
		/// </summary>
		/// <param name="ip">The ip address to listen on.</param>
		/// <param name="port">The port to listen on.</param>
		/// <param name="cert">The configuration for the WebSocket server.</param>
		public GWebSocketServer(IPAddress ip, int port, WSServerConfig? cert = null) : this(new IPEndPoint(ip, port), cert) { }
		/// <summary>
		/// Constructor with a string ip address and int port, certificate file path, and password to certificate.
		/// </summary>
		/// <param name="ip">The string ip address to listen on.</param>
		/// <param name="port">The port to listen on.</param>
		/// <param name="cert">The configuration for the WebSocket server.</param>
		public GWebSocketServer(string ip, int port, WSServerConfig? cert = null) : this(IPAddress.Parse(ip), port, cert) { }

		/// <summary>
		/// Starts listening on the specified ip address and port.
		/// </summary>
		/// <returns>The completed task once the server stops or is disposed.</returns>
		public async Task StartAsync()
		{
			_listener.Start();

			Task.Run(async () =>
			{
				try
				{
					while (true)
					{
						var client = await _listener.AcceptTcpClientAsync();
						if (log != null)
							await log(new WSLogMessage("Client connected", DateTime.Now, Thread.CurrentThread.ManagedThreadId, "Connections", WSLogMessageType.Connect));
						_ = HandleClientAsync(client);
					}
				}
				catch (TaskCanceledException)
				{
					// This is empty because the only time this exception will happen is when this object is disposed. When this object is disposed, everything will be handled.
				}
				catch
				{
					throw;
				}
			}, token.Token);
		}
		internal virtual async Task HandleClientAsync(TcpClient c)
		{
			var client = new T();
			client.Client = c;
			client.Config = cert;
			var token = new CancellationTokenSource();
			_clients.Add(client, token);

			if (!await client.HandShakeAsync())
			{
				_clients.Remove(client);
				if (log != null)
					await log(new WSLogMessage("Unsuccessful client handshake", DateTime.Now, Thread.CurrentThread.ManagedThreadId, "Receives", WSLogMessageType.UnsuccessfulHandshake));
				return;
			}

			if (log != null)
				await log(new WSLogMessage("Client handshaked", DateTime.Now, Thread.CurrentThread.ManagedThreadId, "Receives", WSLogMessageType.Handshake));
			if (onConnect != null)
				await onConnect(client);

			try
			{
				await Task.Run(async () =>
				{
					while (true)
					{
						var t = await client.ReceiveAsync();

						if (t == null)
						{
							if (log != null)
								await log(new WSLogMessage("Client abruptly disconnected", DateTime.Now, Thread.CurrentThread.ManagedThreadId, "Receives", WSLogMessageType.AbruptDisconnect));
							if (onDisconnect != null)
								await onDisconnect(client, null, default);
							break;
						}
						else if (t.Item3 == WSOpcodeType.Close)
						{
							_clients.Remove(client);
							if (log != null)
								await log(new WSLogMessage("Client disconnected", DateTime.Now, Thread.CurrentThread.ManagedThreadId, "Receives", WSLogMessageType.Disconnect));
							if (onDisconnect != null)
								await onDisconnect(client, t.Item1, t.Item2);
							break;
						}

						if (log != null)
							await log(new WSLogMessage("Client sent message", DateTime.Now, Thread.CurrentThread.ManagedThreadId, "Receives", WSLogMessageType.Receive));
						if (onReceive != null)
							await onReceive(client, t.Item1, t.Item3);
					}
				}, token.Token);
			}
			///*
			catch (TaskCanceledException)
			{
				await client.DisposeAsync();
				_clients.Remove(client);
				if (log != null)
					await log(new WSLogMessage("Client abruptly disconnected", DateTime.Now, Thread.CurrentThread.ManagedThreadId, "Receives", WSLogMessageType.AbruptDisconnect));
				if (onDisconnect != null)
					await onDisconnect(client, null, default);
			}
			catch (IndexOutOfRangeException)
			{
				await client.DisposeAsync();
				_clients.Remove(client);
				if (log != null)
					await log(new WSLogMessage("Client abruptly disconnected", DateTime.Now, Thread.CurrentThread.ManagedThreadId, "Receives", WSLogMessageType.AbruptDisconnect));
				if (onDisconnect != null)
					await onDisconnect(client, null, default);
			}
			catch (SocketException)
			{
				await client.DisposeAsync(false);
				if (log != null)
					await log(new WSLogMessage("Client abruptly disconnected", DateTime.Now, Thread.CurrentThread.ManagedThreadId, "Receives", WSLogMessageType.AbruptDisconnect));
				if (onDisconnect != null)
					await onDisconnect(client, null, default);
			}
			catch
			{
				throw;
			}
		}

		/// <summary>
		/// Aborts the connection with the client passed in
		/// </summary>
		/// <param name="instance">The connection to abort.</param>
		public virtual void AbortClient(T instance)
		{
			var token = _clients[instance];
			token.Cancel();
			token.Dispose();
			_clients.Remove(instance);
		}
		/// <summary>
		/// Disposes the current object
		/// </summary>
		/// <returns>A task the represents a Dispose operation.</returns>
		public async ValueTask DisposeAsync()
		{
			if (disposed)
				return;

			disposed = true;

			foreach(var item in _clients)
			{
				await item.Key.DisposeAsync();
				_clients.Remove(item.Key);
			}

			token.Cancel();
			token.Dispose();

			cert.Dispose();
			token.Dispose();
		}
		private static IPEndPoint ParseIPEndPoint(string text)
		{
			Uri uri;
			if (Uri.TryCreate(text, UriKind.Absolute, out uri))
				return new IPEndPoint(IPAddress.Parse(uri.Host), uri.Port < 0 ? 0 : uri.Port);
			if (Uri.TryCreate(String.Concat("tcp://", text), UriKind.Absolute, out uri))
				return new IPEndPoint(IPAddress.Parse(uri.Host), uri.Port < 0 ? 0 : uri.Port);
			if (Uri.TryCreate(String.Concat("tcp://", String.Concat("[", text, "]")), UriKind.Absolute, out uri))
				return new IPEndPoint(IPAddress.Parse(uri.Host), uri.Port < 0 ? 0 : uri.Port);
			throw new FormatException("Failed to parse text to IPEndPoint");
		}
	}
}
