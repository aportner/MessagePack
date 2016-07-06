using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace MessagePack
{
	public static class MessagePackSerializer
	{
		private delegate void SerializeDelegate(object obj, BinaryWriter writer);


		private static Dictionary<Type, SerializeDelegate> SerializeTable = new Dictionary<Type, SerializeDelegate> ();


		static MessagePackSerializer() {
			SerializeTable [typeof(byte)] = SerializeByte;
		}



		public static byte[] Serialize (object obj)
		{
			byte[] result = null;

			using (MemoryStream stream = new MemoryStream ()) {
				using (BinaryWriter writer = new BinaryWriter (stream)) {
					SerializeObject (obj, writer);
					result = stream.ToArray ();
				}
			}

			return result;
		}


		public static void SerializeObject (object obj, BinaryWriter writer)
		{
			
		}


		private static void SerializeByte (object obj, BinaryWriter writer) {
			byte value = (byte)obj;

			if (!WritePositiveFixedInt ((int)value, writer)) {
				writer.Write ((byte)MessagePackConst.Formats.UInt8);
				writer.Write (value);
			}
		}


		private static bool WriteFixedInt(int value, BinaryWriter writer) {
			return WritePositiveFixedInt (value, writer) || WriteNegativeFixedInt(value, writer);
		}


		private static bool WritePositiveFixedInt(byte value, BinaryWriter writer) {
			if ((value & 0x7f) == value) {
				writer.Write (value);
				return true;
			}

			return false;
		}


		private static bool WriteNegativeFixedInt(byte value, BinaryWriter writer) {
			if (value < 0 && 
		}
	}
}

