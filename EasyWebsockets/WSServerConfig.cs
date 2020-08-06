using EasyWebsockets.Helpers;
using System;
using System.IO;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace EasyWebsockets
{

	/// <summary>
	/// The configuration for a WebSocket server or WebSocket client
	/// </summary>
	[Serializable]
	public class WSServerConfig : IDisposable
	{
		private bool disposed;

		/// <summary>
		/// The password of the certificate file.
		/// </summary>
		public string? Password { get; set; }
		/// <summary>
		/// The private certificate private key file path.
		/// </summary>
		public string? PrivKeyPath { get; }
		/// <summary>
		/// The main certificate file path.
		/// </summary>
		public string? MainKeyPath { get; }
		/// <summary>
		/// The version of SSL to use.
		/// </summary>
		public SslProtocols SslVersion { get; }
		/// <summary>
		/// The X509Certificate loaded.
		/// </summary>
		public X509Certificate2? Certificate { get; private set; }
		/// <summary>
		/// The buffer size for packets. If null, dynamic buffer size will be used.
		/// </summary>
		public uint? BufferSize { get; set; }
		/// <summary>
		/// The dynamic buffer size. If this is null as well, the value will be 65535. Invoking <seealso cref="WebSocketInstance.ReceiveAsync"/> in this method will get a <seealso cref="StackOverflowException"/>.
		/// </summary>
		public Func<WebSocketInstance, Task<uint>>? DynamicBufferSize { get; set; }

		/// <summary>
		/// Default constructor of TLS Config, with a file path for the main certificate, file path for the private key, and SSL version
		/// </summary>
		/// <param name="mainkey">The file path of the main certificate.</param>
		/// <param name="privkey">The file path of the private key for the certificate. If the main certificate file contains the private key, this parameter can be null.</param>
		/// <param name="version">The version of SSL to use.</param>
		public WSServerConfig(string? mainkey = null, string? privkey = null, SslProtocols? version = null)
		{
			MainKeyPath = mainkey;
			PrivKeyPath = privkey;

			SslVersion = version.GetValueOrDefault();
		}

		/// <summary>
		/// Sets the value of <see cref="Certificate"/> to the PFX file loaded.
		/// </summary>
		/// <returns>The current instance.</returns>
		public WSServerConfig LoadCertFromPfx()
		{
			if (Certificate != null)
				return this;
			if (Password == null || Password == "")
				Certificate = new X509Certificate2(MainKeyPath);
			else
				Certificate = new X509Certificate2(MainKeyPath, Password);

			return this;
		}

		/// <summary>
		/// Sets the value of <see cref="Certificate"/> to the two PEM files loaded.
		/// </summary>
		/// <returns>The current instance.</returns>
		[Obsolete("This method is being fixed. Right now it doesn't work.")]
		public WSServerConfig LoadCertFromPem()
		{
			if (Certificate != null)
				return this;

			string pem = File.ReadAllText(MainKeyPath);
			if (PrivKeyPath != null && PrivKeyPath != "")
				pem += File.ReadAllText(PrivKeyPath);

			Certificate = new X509Certificate2(GetBytesFromPEM(pem, "CERTIFICATE").Join(GetBytesFromPEM(pem, "PRIVATE KEY")));
			return this;
		}

		
		private static byte[]? GetBytesFromPEM(string pemString, string section)
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

		public void Dispose()
		{
			if (disposed)
				return;
			disposed = true;
			if (Certificate != null)
				Certificate.Dispose();
		}
	}
}
