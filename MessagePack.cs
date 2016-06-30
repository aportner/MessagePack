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

			// primitives
			DeserializeTable [0xc0] = DeserializeNull;
			DeserializeTable [0xc2] = DeserializeFalse;
			DeserializeTable [0xc3] = DeserializeTrue;

			// bytes
			DeserializeTable [0xc4] = DeserializeBin8;
			DeserializeTable [0xc5] = DeserializeBin16;
			DeserializeTable [0xc6] = DeserializeBin32;

			// c7-c9 ext

			// floats
			DeserializeTable [0xca] = DeserializeFloat32;
			JumpTable [0xca] = Jump32;
			DeserializeTable [0xcb] = DeserializeFloat64;
			JumpTable [0xcb] = Jump64;

			// uint
			DeserializeTable [0xcc] = DeserializeUInt8;
			JumpTable [0xcc] = Jump8;
			DeserializeTable [0xcd] = DeserializeUInt16;
			JumpTable [0xcd] = Jump16;
			DeserializeTable [0xce] = DeserializeUInt32;
			JumpTable [0xce] = Jump32;
			DeserializeTable [0xcf] = DeserializeUInt64;
			JumpTable [0xcf] = Jump64;

			// int
			DeserializeTable [0xd0] = DeserializeInt8;
			JumpTable [0xd0] = Jump8;
			DeserializeTable [0xd1] = DeserializeInt16;
			JumpTable [0xd1] = Jump16;
			DeserializeTable [0xd2] = DeserializeInt32;
			JumpTable [0xd2] = Jump32;
			DeserializeTable [0xd3] = DeserializeInt64;
			JumpTable [0xd3] = Jump64;

			// d4 - d8 ext


			// d9 - db string
			DeserializeTable [0xd9] = DeserializeString8;
			JumpTable [0xd9] = JumpLen8;
			DeserializeTable [0xda] = DeserializeString16;
			JumpTable [0xda] = JumpLen16;
			DeserializeTable [0xdb] = DeserializeString32;
			JumpTable [0xdb] = JumpLen32;

			// dc - dd array
			DeserializeTable [0xdc] = DeserializeArray16;
			JumpTable [0xdc] = JumpLen16;
			DeserializeTable [0xdd] = DeserializeArray32;
			JumpTable [0xdd] = JumpLen32;

			// de - df map
			DeserializeTable [0xde] = DeserializeMap16;
			JumpTable [0xde] = JumpLen16;
			DeserializeTable [0xdf] = DeserializeMap32;
			JumpTable [0xdf] = JumpLen32;

			for (i = 0xe0; i < 0x100; ++i) {
				DeserializeTable [i] = DeserializeNegativeFixInt;
			}
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
		private static object DeserializeFixString (byte format, BinaryReader reader)
		{
			// 5 bit len
			int len = format & 0x1f;
			return DeserializeString (format, reader, len);
		}

		private static void JumpFixString (byte format, BinaryReader reader)
		{
			// 5 bit len
			int len = format & 0x1f;
			reader.BaseStream.Seek (len, SeekOrigin.Current);
		}


		// 0xc0
		private static object DeserializeNull (byte format, BinaryReader reader)
		{
			return null;
		}


		// 0xc2
		private static object DeserializeFalse (byte format, BinaryReader reader)
		{
			return false;
		}


		// 0xc3
		private static object DeserializeTrue (byte format, BinaryReader reader)
		{
			return true;
		}


		// 0xc4
		private static object DeserializeBin8 (byte format, BinaryReader reader)
		{
			int len = reader.ReadByte ();
			return reader.ReadBytes (len);
		}

		private static void JumpBin8 (byte format, BinaryReader reader)
		{
			int len = reader.ReadByte ();
			reader.BaseStream.Seek (len, SeekOrigin.Current);
		}


		// 0xc5
		private static object DeserializeBin16 (byte format, BinaryReader reader)
		{
			int len = reader.ReadUInt16 ();
			return reader.ReadBytes (len);
		}

		private static void JumpBin16 (byte format, BinaryReader reader)
		{
			int len = reader.ReadUInt16 ();
			reader.BaseStream.Seek (len, SeekOrigin.Current);
		}


		// 0xc6
		private static object DeserializeBin32 (byte format, BinaryReader reader)
		{
			int len = reader.ReadInt32 ();
			return reader.ReadBytes (len);
		}

		private static void JumpBin32 (byte format, BinaryReader reader)
		{
			int len = reader.ReadInt32 ();
			reader.BaseStream.Seek (len, SeekOrigin.Current);
		}


		// 0xca
		private static object DeserializeFloat32 (byte format, BinaryReader reader)
		{
			return reader.ReadSingle ();
		}


		// 0xcb
		private static object DeserializeFloat64 (byte format, BinaryReader reader)
		{
			return reader.ReadDouble ();
		}


		// 0xcc
		private static object DeserializeUInt8 (byte format, BinaryReader reader)
		{
			return reader.ReadByte ();
		}


		// 0xcd
		private static object DeserializeUInt16 (byte format, BinaryReader reader)
		{
			return reader.ReadUInt16 ();
		}


		// 0xce
		private static object DeserializeUInt32 (byte format, BinaryReader reader)
		{
			return reader.ReadUInt32 ();
		}


		// 0xcf
		private static object DeserializeUInt64 (byte format, BinaryReader reader)
		{
			return reader.ReadUInt64 ();
		}


		// 0xd0
		private static object DeserializeInt8 (byte format, BinaryReader reader)
		{
			return reader.ReadSByte ();
		}


		// 0xd1
		private static object DeserializeInt16 (byte format, BinaryReader reader)
		{
			return reader.ReadInt16 ();
		}


		// 0xd2
		private static object DeserializeInt32 (byte format, BinaryReader reader)
		{
			return reader.ReadInt32 ();
		}


		// 0xd3
		private static object DeserializeInt64 (byte format, BinaryReader reader)
		{
			return reader.ReadInt64 ();
		}


		// 0xd9
		private static object DeserializeString8 (byte format, BinaryReader reader)
		{
			int len = reader.ReadByte ();
			return DeserializeString (format, reader, len);
		}


		// 0xda
		private static object DeserializeString16 (byte format, BinaryReader reader)
		{
			int len = reader.ReadUInt16 ();
			return DeserializeString (format, reader, len);
		}


		// 0xdb
		private static object DeserializeString32 (byte format, BinaryReader reader)
		{
			int len = reader.ReadInt32 ();
			return DeserializeString (format, reader, len);
		}


		// 0xdc
		private static object DeserializeArray16 (byte format, BinaryReader reader)
		{
			int len = reader.ReadUInt16 ();
			return DeserializeArray (format, reader, len);
		}


		// 0xdd
		private static object DeserializeArray32 (byte format, BinaryReader reader)
		{
			int len = reader.ReadInt32 ();
			return DeserializeArray (format, reader, len);
		}


		// 0xde
		private static object DeserializeMap16 (byte format, BinaryReader reader)
		{
			int len = reader.ReadUInt16 ();
			return DeserializeMap (format, reader, len);
		}


		// 0xdf
		private static object DeserializeMap32 (byte format, BinaryReader reader)
		{
			int len = reader.ReadInt32 ();
			return DeserializeMap (format, reader, len);
		}


		// 0xe0 - 0xff
		private static object DeserializeNegativeFixInt (byte format, BinaryReader reader)
		{
			return (sbyte)(0x80 | (format & 0x1f));
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

		private static void JumpArray (byte format, BinaryReader reader, int len)
		{
			for (int i = 0; i < len; ++i) {
				JumpObject (reader);
			}
		}


		private static string DeserializeString (byte format, BinaryReader reader, int len)
		{
			byte[] bytes = reader.ReadBytes (len);
			return encoding.GetString (bytes);
		}


		private static void JumpEmpty (byte format, BinaryReader reader)
		{
		}


		private static void Jump8 (byte format, BinaryReader reader)
		{
			reader.BaseStream.Seek (1, SeekOrigin.Current);
		}


		private static void Jump16 (byte format, BinaryReader reader)
		{
			reader.BaseStream.Seek (2, SeekOrigin.Current);
		}


		private static void Jump32 (byte format, BinaryReader reader)
		{
			reader.BaseStream.Seek (4, SeekOrigin.Current);
		}


		private static void Jump64 (byte format, BinaryReader reader)
		{
			reader.BaseStream.Seek (8, SeekOrigin.Current);
		}


		private static void JumpLen8 (byte format, BinaryReader reader)
		{
			int len = reader.ReadByte ();
			reader.BaseStream.Seek (len, SeekOrigin.Current);
		}


		private static void JumpLen16 (byte format, BinaryReader reader)
		{
			int len = reader.ReadUInt16 ();
			reader.BaseStream.Seek (len, SeekOrigin.Current);
		}


		private static void JumpLen32 (byte format, BinaryReader reader)
		{
			int len = reader.ReadInt32 ();
			reader.BaseStream.Seek (len, SeekOrigin.Current);
		}


		#endregion
	}
}