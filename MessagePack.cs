using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MessagePack
{
	/// <summary>
	/// Class to handle the messagepack serialization protocol
	/// </summary>
	public class MessagePack
	{
		#region Delegates
		/// <summary>
		/// Delegate for a deserializer. Takes in a 1 byte format and a reader.
		/// </summary>
		private delegate object DeserializeDelegate (byte format, BinaryReader reader);


		/// <summary>
		/// Delegate for handling how far to jump ahead for delayed deserialization.
		/// </summary>
		private delegate void JumpDelegate (byte format, BinaryReader reader);
		#endregion


		#region Static vars
		/// <summary>
		/// Default text encoding method. Not sure if UTF8 is right.
		/// </summary>
		private static Encoding encoding = Encoding.UTF8;


		/// <summary>
		/// Table mapping 1 byte format id to the deserializer
		/// </summary>
		private static DeserializeDelegate[] DeserializeTable = new DeserializeDelegate[256];


		/// <summary>
		/// TAble mapping 1 byte format id to the jump delegate
		/// </summary>
		private static JumpDelegate[] JumpTable = new JumpDelegate[256];
		#endregion


		#region Static Initializer
		/// <summary>
		/// Static initializer. Initializes all tables.
		/// </summary>
		static MessagePack ()
		{
			int i;

			// default to empty
			for (i = 0; i < 256; ++i)
				JumpTable [i] = JumpEmpty;

			for (i = 0; i < 0x80; ++i) {
				DeserializeTable [i] = DeserializePositiveFixInt;
			}

			for (i = 0x80; i < 0x90; ++i) {
				DeserializeTable [i] = DeserializeFixMap;
				JumpTable [i] = JumpFixMap;
			}

			for (i = 0x90; i < 0xa0; ++i) {
				DeserializeTable [i] = DeserializeFixArray;
				JumpTable [i] = JumpFixArray;
			}

			for (i = 0xa0; i < 0xc0; ++i) {
				DeserializeTable [i] = DeserializeFixString;
				JumpTable [i] = JumpFixString;
			}

			DeserializeTable [0xc0] = DeserializeNull;
			DeserializeTable [0xc2] = DeserializeFalse;
			DeserializeTable [0xc3] = DeserializeTrue;
			DeserializeTable [0xc4] = DeserializeBin8;
			DeserializeTable [0xc5] = DeserializeBin16;
			DeserializeTable [0xc6] = DeserializeBin32;
		}
		#endregion


		#region Generic serialization
		/// <summary>
		/// Reads a format from the reader and deserializes the object
		/// </summary>
		/// <returns>The object.</returns>
		/// <param name="reader">Reader.</param>
		private static object DeserializeObject (BinaryReader reader)
		{
			byte format = reader.ReadByte ();
			DeserializeDelegate del = DeserializeTable [format];

			return del (format, reader);
		}


		/// <summary>
		/// Reads a format from the reader and jumps past the object
		/// </summary>
		/// <param name="reader">Reader.</param>
		private static void JumpObject (BinaryReader reader)
		{
			byte format = reader.ReadByte ();
			JumpDelegate del = JumpTable [format];

			del (format, reader);
		}
		#endregion


		#region Serializers
		// --- Serializers ---
		// 0x00 - 0x7f
		private static object DeserializePositiveFixInt (byte format, BinaryReader reader)
		{
			return format;
		}


		// 0x80 - 0x8f
		private static object DeserializeFixMap (byte format, BinaryReader reader)
		{
			int len = format & 0x0f;
			return DeserializeMap (format, reader, len);
		}

		private static void JumpFixMap (byte format, BinaryReader reader)
		{
			int len = format & 0x0f;
			JumpMap (format, reader, len);
		}


		// 0x90 - 0x9f;
		private static object DeserializeFixArray (byte format, BinaryReader reader)
		{
			int len = format & 0x0f;
			return DeserializeArray (format, reader, len);
		}

		private static void JumpFixArray (byte format, BinaryReader reader)
		{
			int len = format & 0x0f;
			JumpArray (format, reader, len);
		}


		// 0xa0 - 0xbf
		private static object DeserializeFixString (byte format, BinaryReader reader) {
			// 5 bit len
			int len = format & 0x1f;
			return DeserializeString (format, reader, len);
		}

		private static void JumpFixString (byte format, BinaryReader reader) {
			// 5 bit len
			int len = format & 0x1f;
			reader.BaseStream.Seek (len, SeekOrigin.Current);
		}


		// 0xc0
		private static object DeserializeNull (byte format, BinaryReader reader) {
			return null;
		}


		// 0xc2
		private static object DeserializeFalse (byte format, BinaryReader reader) {
			return false;
		}


		// 0xc3
		private static object DeserializeTrue (byte format, BinaryReader reader) {
			return true;
		}


		// 0xc4
		private static object DeserializeBin8 (byte format, BinaryReader reader) {
			int len = reader.ReadByte ();
			return reader.ReadBytes (len);
		}

		private static void JumpBin8 (byte format, BinaryReader reader) {
			int len = reader.ReadByte ();
			reader.BaseStream.Seek (len, SeekOrigin.Current);
		}


		// 0xc5
		private static object DeserializeBin16 (byte format, BinaryReader reader) {
			int len = reader.ReadUInt16 ();
			return reader.ReadBytes (len);
		}

		private static void JumpBin16 (byte format, BinaryReader reader) {
			int len = reader.ReadUInt16 ();
			reader.BaseStream.Seek (len, SeekOrigin.Current);
		}


		// 0xc6
		private static object DeserializeBin32 (byte format, BinaryReader reader) {
			int len = reader.ReadInt32 ();
			return reader.ReadBytes (len);
		}

		private static void JumpBin32 (byte format, BinaryReader reader) {
			int len = reader.ReadInt32 ();
			reader.BaseStream.Seek (len, SeekOrigin.Current);
		}


		#endregion


		#region Serialization utilities
		private static Dictionary<string, object> DeserializeMap (byte format, BinaryReader reader, int len)
		{
			Dictionary<string, object> result = new Dictionary<string, object> ();

			for (int i = 0; i < len; ++i) {
				string key = (string)DeserializeObject (reader);
				object value = DeserializeObject (reader);

				result [key] = value;
			}

			return result;
		}

		private static void JumpMap (byte format, BinaryReader reader, int len)
		{
			for (int i = 0; i < len; ++i) {
				// key
				JumpObject (reader);

				// value
				JumpObject (reader);
			}
		}


		private static object[] DeserializeArray (byte format, BinaryReader reader, int len)
		{
			object[] result = new object[len];

			for (int i = 0; i < len; ++i) {
				object value = DeserializeObject (reader);

				result [i] = value;
			}

			return result;
		}

		private static void JumpArray (byte format, BinaryReader reader, int len) {
			for (int i = 0; i < len; ++i) {
				JumpObject (reader);
			}
		}


		private static string DeserializeString (byte format, BinaryReader reader, int len) {
			byte[] bytes = reader.ReadBytes (len);
			return encoding.GetString (bytes);
		}


		private static void JumpEmpty (byte format, BinaryReader reader) {
		}
		#endregion
	}
}