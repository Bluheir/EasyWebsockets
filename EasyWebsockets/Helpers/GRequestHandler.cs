using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace EasyWebsockets.Helpers
{
	public class GRequestHandler : IHttpHeadersHandler, IHttpRequestLineHandler
	{
		private readonly Dictionary<string, string> startHeaders;
		private readonly Dictionary<string, string> headers;

		/// <summary>
		/// The start headers of the HTTP request, such as method, version, target, and path.
		/// </summary>
		public IReadOnlyDictionary<string, string> StartHeaders => startHeaders;
		/// <summary>
		/// The additional headers of the HTTP request.
		/// </summary>
		public IReadOnlyDictionary<string, string> Headers => headers;

		/// <summary>
		/// The main constructor.
		/// </summary>
		public GRequestHandler()
		{
			startHeaders = new Dictionary<string, string>();
			headers = new Dictionary<string, string>();
		}

		public void OnHeader(Span<byte> name, Span<byte> value)
		{
			headers.Add(Encoding.UTF8.GetString(name.ToArray()), Encoding.UTF8.GetString(value.ToArray()));
		}

		public void OnStartLine(HttpMethod method, HttpVersion version, Span<byte> target, Span<byte> path, Span<byte> query, Span<byte> customMethod, bool pathEncoded)
		{
			startHeaders.Add("method", method.ToString());
			startHeaders.Add("version", version.ToString());
			startHeaders.Add("target", Encoding.UTF8.GetString(target.ToArray()));
			startHeaders.Add("path", Encoding.UTF8.GetString(path.ToArray()));
			startHeaders.Add("query", Encoding.UTF8.GetString(query.ToArray()));
			startHeaders.Add("customMethod", Encoding.UTF8.GetString(customMethod.ToArray()));
			startHeaders.Add("pathEncoded", pathEncoded.ToString());
		}
		public static GRequestHandler ParseHeaders(byte[] data)
		{
			var buffer = new ReadOnlySequence<byte>(data);
			var headers = new GRequestHandler();
			var p = new HttpParser<GRequestHandler>();

			p.ParseRequestLine(headers, in buffer, out var consumed, out var examined);
			buffer = buffer.Slice(consumed);

			p.ParseHeaders(headers, in buffer, out consumed, out examined, out var b);

			return headers;
		}
		public static GRequestHandler ParseHeaders(string data)
		=> ParseHeaders(Encoding.UTF8.GetBytes(data));

	}
}
