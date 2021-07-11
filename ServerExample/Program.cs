using EasyWebsockets;
using EasyWebsockets.Classes;
using System;
using System.Net;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;

namespace ServerExample
{
	class Program
	{
		private WebSocketServer? server;

		private static void Main()
		=> new Program().MainAsync().GetAwaiter().GetResult();

		private async Task MainAsync()
		{
			var config = new WSServerConfig(
				mainkey: null,
				version: SslProtocols.Tls13 | SslProtocols.Tls12
			);

			config.BufferSize = 65535;

			//config.LoadCertFromPfx("corey is cool 123 321");

			server = new WebSocketServer("192.168.1.111", 80, config);

			server.OnClientHandshake += OnConnect; ;
			server.OnDisconnect += ClientDisconnect;
			server.OnReceive += OnReceive; ;
			server.Log += OnLog;

			await server.StartAsync();
			await Task.Delay(-1);
		}

		private async Task ClientDisconnect(WebSocketInstance arg1, byte[] arg2, System.Net.WebSockets.WebSocketCloseStatus arg3)
		{
		}

		private Task OnLog(IWSLogMessage arg)
		{
			if(arg.Type != WSLogMessageType.Receive)
				Console.WriteLine($"{arg.Process}({arg.Thread}) at {arg.Time}] {arg.Content}");
			return Task.CompletedTask;
		}

		private async Task OnReceive(WebSocketInstance arg1, byte[] arg2, WSOpcodeType arg3)
		{
			if (arg3 == WSOpcodeType.Text)
			{
				Console.WriteLine(Encoding.UTF8.GetString(arg2));
			}


		}

		private async Task OnConnect(WebSocketInstance arg)
		{
			await arg.SendAsync(Encoding.UTF8.GetBytes("Neeshko"), WSOpcodeType.Text);

			await arg.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "yeah mate");
		}
	}
}
