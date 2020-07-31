using EasyWebsockets.Classes;
using System;
using System.Linq;
using System.Net.WebSockets;

namespace EasyWebsockets.Helpers
{
	public static class WSArrayHelpers
	{
		/// <summary>
		/// Joins two arrays together.
		/// </summary>
		/// <typeparam name="T">The type of the array.</typeparam>
		/// <param name="data">The first array.</param>
		/// <param name="data2">The second array.</param>
		/// <returns>The two arrays joined together.</returns>
		public static T[] Join<T>(this T[] data, T[] data2)
		{
			return data.Concat(data2).ToArray();
		}
		/// <summary>
		/// Gets subarray of a specific array.
		/// </summary>
		/// <typeparam name="T">The type of the array.</typeparam>
		/// <param name="data">The input array.</param>
		/// <param name="index">The index to cut off the array at.</param>
		/// <param name="length">Length of the array to leave in.</param>
		/// <returns>The array cut off at the index, with the specified length.</returns>
		public static T[] SubArray<T>(this T[] data, int index, int length)
		{
			T[] result = new T[length];
			Array.Copy(data, index, result, 0, length);
			return result;
		}
		/// <summary>
		/// Converts a WebSocket from into a tuple of the excess data, error code (if there is one), length of the message, and opcode type.
		/// </summary>
		/// <param name="frame">The frame data to be decoded.</param>
		/// <returns>The excess data, error code, length of message, and opcode type of the frame.</returns>
		public static Tuple<byte[], WebSocketCloseStatus, ulong, WSOpcodeType>? ConvertFrame(byte[] frame)
		{
			bool fin = (frame[0] & 0b10000000) != 0;
			bool mask = (frame[1] & 0b10000000) != 0;

			if (!mask)
			{
				return null;
			}

			int opcode = frame[0] & 0b00001111;
			ulong msgLen = (ulong)(frame[1] - 128);
			int offset = 2;

			if (msgLen == 126)
			{
				msgLen = (ulong)BitConverter.ToInt16(new byte[] { frame[3], frame[2] });
				offset = 4;
			}
			else if (msgLen == 127)
			{
				msgLen = BitConverter.ToUInt64(new byte[] { frame[9], frame[8], frame[7], frame[6], frame[5], frame[4], frame[3], frame[2] });
				offset = 6;
			}

			byte[] maskingKey = new byte[] { frame[offset], frame[offset + 1], frame[offset + 2], frame[offset + 3] };
			offset += 4;

			byte[] data = new byte[msgLen];
			int m = 0;

			for (int i = offset; i < frame.Length; i++)
			{
				data[m] = (byte)(frame[i] ^ maskingKey[m % 4]);
				m++;
			}

			WebSocketCloseStatus closeStatus = default;
			if (opcode == 0x8 && msgLen > 1)
			{
				closeStatus = (WebSocketCloseStatus)BitConverter.ToUInt16(new byte[] { data[1], data[0] });
				data = data.SubArray(2, data.Length - 2);
			}
			return new Tuple<byte[], WebSocketCloseStatus, ulong, WSOpcodeType>(data, closeStatus, msgLen, (WSOpcodeType)opcode);
		}
		/// <summary>
		/// Converts data into a WebSocket frame.
		/// </summary>
		/// <param name="data">The input data to be converted.</param>
		/// <param name="msgType">The Opcode type of the data.</param>
		/// <returns>A valid WebSocket frame built from the data and Opcode type.</returns>
		public static byte[] ToFrameData(ArraySegment<byte> data, WSOpcodeType msgType)
		{
			byte[] response;
			byte[] bytesRaw = data.Array;
			byte[] frame = new byte[10];

			int indexStartRawData = -1;
			int length = bytesRaw.Length;

			frame[0] = (byte)(128 + (int)msgType);
			if (length <= 125)
			{
				frame[1] = (byte)length;
				indexStartRawData = 2;
			}
			else if (length >= 126 && length <= 65535)
			{
				frame[1] = (byte)126;
				frame[2] = (byte)((length >> 8) & 255);
				frame[3] = (byte)(length & 255);
				indexStartRawData = 4;
			}
			else
			{
				frame[1] = (byte)127;
				frame[2] = (byte)((length >> 56) & 255);
				frame[3] = (byte)((length >> 48) & 255);
				frame[4] = (byte)((length >> 40) & 255);
				frame[5] = (byte)((length >> 32) & 255);
				frame[6] = (byte)((length >> 24) & 255);
				frame[7] = (byte)((length >> 16) & 255);
				frame[8] = (byte)((length >> 8) & 255);
				frame[9] = (byte)(length & 255);

				indexStartRawData = 10;
			}

			response = new byte[indexStartRawData + length];

			int i, reponseIdx = 0;

			//Add the frame bytes to the reponse
			for (i = 0; i < indexStartRawData; i++)
			{
				response[reponseIdx] = frame[i];
				reponseIdx++;
			}

			//Add the data bytes to the response
			for (i = 0; i < length; i++)
			{
				response[reponseIdx] = bytesRaw[i];
				reponseIdx++;
			}

			return response;
		}
	}
}
