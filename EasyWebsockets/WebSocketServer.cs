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
using System.Security.Authentication;
using System.Net.WebSockets;

namespace EasyWebsockets
{
	/// <summary>
	/// Represents a normal WebSocket server.
	/// </summary>
	public class WebSocketServer : GWebSocketServer<WebSocketInstance>
	{
		/// <summary>
		/// Constructor with an <see cref="IPEndPoint"/>, certificate and password.
		/// </summary>
		/// <param name="endpoint">The <see cref="IPEndPoint"/> to listen on.</param>
		/// <param name="cert">The configuration for the WebSocket server.</param>
		public WebSocketServer(IPEndPoint endpoint, WSServerConfig? cert) : base(endpoint, cert) { }
		/// <summary>
		/// Constructor with a string ip endpoint.
		/// </summary>
		/// <param name="endpoint">The ip endpoint to listen on.</param>
		/// <param name="cert">The configuration for the WebSocket server.</param>
		public WebSocketServer(string endpoint, WSServerConfig? cert = null) : this(ParseIPEndPoint(endpoint), cert) { }
		/// <summary>
		/// Constructor with an <see cref="IPAddress"/> and port, certificate file path, and password to certificate.
		/// </summary>
		/// <param name="ip">The ip address to listen on.</param>
		/// <param name="port">The port to listen on.</param>
		/// <param name="cert">The configuration for the WebSocket server.</param>
		public WebSocketServer(IPAddress ip, int port, WSServerConfig? cert = null) : this(new IPEndPoint(ip, port), cert) { }
		/// <summary>
		/// Constructor with a string ip address and int port, certificate file path, and password to certificate.
		/// </summary>
		/// <param name="ip">The string ip address to listen on.</param>
		/// <param name="port">The port to listen on.</param>
		/// <param name="cert">The configuration for the WebSocket server.</param>
		public WebSocketServer(string ip, int port, WSServerConfig? cert = null) : this(IPAddress.Parse(ip), port, cert) { }

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