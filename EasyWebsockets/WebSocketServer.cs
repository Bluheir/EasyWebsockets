using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using EasyWebsockets.Classes;

using System.Security.Cryptography;
using System.Text;
using System.IO;

namespace EasyWebsockets
{
	public class WebSocketServer
	{
		private readonly Dictionary<WebSocketInstance, CancellationTokenSource> _clients;
		private readonly TcpListener _listener;
		private readonly X509Certificate2? cert;
		private readonly bool secure;
		private readonly CancellationTokenSource token;

		private Func<WebSocketInstance, Task>? onConnect = x => Task.CompletedTask;
		private Func<WebSocketInstance, byte[]?, ushort?, Task>? onDisconnect = (x, y, z) => Task.CompletedTask;
		private Func<WebSocketInstance, byte[], WSOpcodeType, Task>? onReceive = (w, x, y) => Task.CompletedTask;
		private Func<IWSLogMessage, Task>? log = x => Task.CompletedTask;

		/// <summary>
		/// The function to invoke when a client connects.
		/// </summary>
		public event Func<WebSocketInstance, Task> OnConnect { add => onConnect += value; remove => onConnect -= value; }
		/// <summary>
		/// The function to invoke when a client disconnects.
		/// </summary>
		public event Func<WebSocketInstance, byte[]?, ushort?, Task> OnDisconnect { add => onDisconnect += value; remove => onDisconnect -= value; }
		/// <summary>
		/// The function to invoke when a client sends bytes with the Opcode type.
		/// </summary>
		public event Func<WebSocketInstance, byte[], WSOpcodeType, Task> OnReceive { add => onReceive += value; remove => onReceive -= value; }
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
		/// <param name="certificate">The path of TLS certificate. If null, connections won't be tunneled over TLS.</param>
		/// <param name="password">The password to access certificate.</param>
		public WebSocketServer(IPEndPoint endpoint, string? certificate = null, string? password = null)
		{
			_clients = new Dictionary<WebSocketInstance, CancellationTokenSource>();
			_listener = new TcpListener(endpoint);
			secure = certificate != null;

			if (secure)
			{
				//Load certificate

				if(password == null)
					cert = new X509Certificate2(certificate);
				else
					cert = new X509Certificate2(certificate, password);
			}

			Endpoint = endpoint;
			token = new CancellationTokenSource();
		}
		//public WebSocketServer(string endpoint, string? certificate = null) : this(IPEndPoint.Parse(endpoint), certificate) { } // will fix later
		/// <summary>
		/// Constructor with an <see cref="IPAddress"/> and port, certificate file path, and password to certificate.
		/// </summary>
		/// <param name="ip">The ip address to listen on.</param>
		/// <param name="port">The port to listen on.</param>
		/// <param name="certificate">The path of TLS certificate. If null, connections won't be tunneled over TLS.</param>
		/// <param name="password">The password to access certificate.</param>
		public WebSocketServer(IPAddress ip, int port, string? certificate = null, string? password = null) : this(new IPEndPoint(ip, port), certificate, password) { }
		/// <summary>
		/// Constructor with a string ip address and int port, certificate file path, and password to certificate.
		/// </summary>
		/// <param name="ip">The string ip address to listen on.</param>
		/// <param name="port">The port to listen on.</param>
		/// <param name="certificate">The path of TLS certificate. If null, connections won't be tunneled over TLS.</param>
		/// <param name="password">The password to access certificate.</param>
		public WebSocketServer(string ip, int port, string? certificate = null, string? password = null) : this(IPAddress.Parse(ip), port, certificate, password) { }

		/// <summary>
		/// Starts listening on the specified ip address and port.
		/// </summary>
		/// <returns></returns>
		public virtual async Task StartAsync()
		{
			_listener.Start();
			try
			{
				await Task.Run(async () =>
				{
					while (true)
					{
						_ = HandleClientAsync(await _listener.AcceptTcpClientAsync());
						if (log != null)
							await log(new WSLogMessage("Client connected", DateTime.Now, Thread.CurrentThread.ManagedThreadId, "Connections", WSLogMessageType.Connect));
					}
				}, token.Token);
			}
			catch (TaskCanceledException)
			{
				foreach (var client in _clients)
				{
					DisconnectClient(client.Key);
				}
			}
		}
		internal virtual async Task HandleClientAsync(TcpClient c)
		{
			var client = new WebSocketInstance(c, cert: cert);
			var token = new CancellationTokenSource();
			_clients.Add(client, token);

			await client.HandShakeAsync();

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
								await onDisconnect(client, null, null);
							break;
						}
						else if (t.Item3 == WSOpcodeType.Close)
						{
							await client.DisposeAsync();
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
			catch (TaskCanceledException)
			{
				await client.DisposeAsync();
				if (log != null)
					await log(new WSLogMessage("Client disconnected", DateTime.Now, Thread.CurrentThread.ManagedThreadId, "Receives", WSLogMessageType.Disconnect));
				if (onDisconnect != null)
					await onDisconnect(client, null, null);
			}
			catch (SocketException e) when (e.SocketErrorCode == SocketError.ConnectionAborted)
			{
				await client.DisposeAsync();
				if (log != null)
					await log(new WSLogMessage("Client abruptly disconnected", DateTime.Now, Thread.CurrentThread.ManagedThreadId, "Receives", WSLogMessageType.AbruptDisconnect));
				if (onDisconnect != null)
					await onDisconnect(client, null, null);
			}
		}
		private static X509Certificate2 LoadCertificate(string filePath)
		{
			var pem = System.IO.File.ReadAllText(filePath);
			byte[] certBuffer = GetBytesFromPEM(pem, "CERTIFICATE");
			return new X509Certificate2(certBuffer);
		}
		private static byte[] GetBytesFromPEM(string pemString, string section)
		{
			var header = string.Format("-----BEGIN {0}-----", section);
			var footer = string.Format("-----END {0}-----", section);

			var start = pemString.IndexOf(header, StringComparison.Ordinal);
			if (start < 0)
				return null;

			start += header.Length;
			var end = pemString.IndexOf(footer, start, StringComparison.Ordinal) - start;

			if (end < 0)
				return null;

			return Convert.FromBase64String(pemString.Substring(start, end));
		}

		/// <summary>
		/// Disconnects the client passed in.
		/// </summary>
		/// <param name="instance">The client to disconnect.</param>
		public virtual void DisconnectClient(WebSocketInstance instance)
		{
			var token = _clients[instance];
			token.Cancel();
			token.Dispose();
			_clients.Remove(instance);
		}
	}
}