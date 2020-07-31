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
			var config = new TlsConfig(
				mainkey: "C:/Users/User/Desktop/cert1.pfx",
				privkey: "C:/Certbot/live/socket.gg/privkey.pem",
				version: SslProtocols.Tls13 | SslProtocols.Tls12 | SslProtocols.Tls
			);

			config.LoadCertFromPfx();

			server = new WebSocketServer("192.168.2.165", 25565, config);

			server.OnConnect += OnConnect; ;
			server.OnReceive += OnReceive; ;
			server.Log += OnLog;

			await server.StartAsync();
			await Task.Delay(-1);
		}

		private Task OnLog(IWSLogMessage arg)
		{
			if(arg.Type != WSLogMessageType.Receive)
				Console.WriteLine($"{arg.Process}({arg.Thread}) at {arg.Time}] {arg.Content}");
			return Task.CompletedTask;
		}

		private Task OnReceive(WebSocketInstance arg1, byte[] arg2, WSOpcodeType arg3)
		{
			if (arg3 == WSOpcodeType.Text)
			{
				Console.WriteLine(Encoding.UTF8.GetString(arg2));
			}
			return Task.CompletedTask;
		}

		private async Task OnConnect(WebSocketInstance arg)
		{
			await arg.SendAsync(Encoding.UTF8.GetBytes("Neeshko"), WSOpcodeType.Text);
		}
	}
}
