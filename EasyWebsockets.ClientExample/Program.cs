using System;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Threading;
using System.Text;
using System.Security.Authentication;

namespace EasyWebsockets.ClientExample
{
	class Program
	{
		private readonly ClientWebSocket client;
		private Program()
		{
			client = new ClientWebSocket();
		}
		static void Main(string[] args)
		=> new Program().MainAsync().GetAwaiter().GetResult();

		public async Task MainAsync()
		{
			await client.ConnectAsync(new Uri("ws://192.168.1.111"), new CancellationToken());
			Console.WriteLine("connected");
			while (true)
			{
				string t = Console.ReadLine();
				if(client.State == WebSocketState.Closed)
				{
					Console.WriteLine("server closed connection");
				}
				if(t == "close")
				{
					await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
					break;
				}
				byte[] b = Encoding.UTF8.GetBytes(t);
				await client.SendAsync(new ArraySegment<byte>(b), WebSocketMessageType.Text, true, new CancellationToken());
			}
		}
	}
}
