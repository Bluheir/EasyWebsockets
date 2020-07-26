using EasyWebsockets;
using EasyWebsockets.Classes;
using System;
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
			server = new WebSocketServer("192.168.1.107", 443, @"C:\Users\User\Desktop\cert.pfx", "asdf");

			server.OnConnect += OnConnect; ;
			server.OnReceive += OnReceive; ;
			server.Log += OnLog;

			await server.StartAsync();
			await Task.Delay(-1);
		}

		private Task OnLog(IWSLogMessage arg)
		{
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

		private Task OnConnect(WebSocketInstance arg)
		{
			return Task.CompletedTask;
		}
	}
}
