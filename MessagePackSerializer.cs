using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace MessagePack
{
	public static class MessagePackSerializer
	{
		private delegate void SerializeDelegate (object obj, BinaryWriter writer);


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


		private static void SerializeByte (byte value, BinaryWriter writer)
		{
			// optimization to write header if it's not a fixedint.
			if ((value & 0x80) != 0) {
				writer.Write ((byte)MessagePackConst.Formats.UInt8);
			}

			writer.Write (value);
		}


		private static void SerializeUInt16 (UInt16 value, BinaryWriter writer)
		{
			if ((value & 0xff) == value) {
				SerializeByte ((byte)value, writer);
			} else {
				writer.Write ((byte)MessagePackConst.Formats.UInt16);
				writer.Write (value);
			}
		}


		private static void SerializeUInt32 (UInt32 value, BinaryWriter writer)
		{
			if ((value & 0xffff) == value) {
				SerializeUInt16 ((UInt16)value, writer);
			} else {
				writer.Write ((byte)MessagePackConst.Formats.UInt32);
				writer.Write (value);
			}
		}


		private static void SerializeUInt64 (UInt64 value, BinaryWriter writer)
		{
			if ((value & 0xffffffff) == value) {
				SerializeUInt32 ((UInt32)value, writer);
			} else {
				writer.Write ((byte)MessagePackConst.Formats.UInt64);
				writer.Write (value);
			}
		}


		private static void SerializeSByte (sbyte value, BinaryWriter writer)
		{
			// only write the 8bit int header if the number is not in the
			// positive fixedint range and it's not in the negative fixedint
			// range
			if ((value & 0x80) != 0 && (value & 0xe0) != 0xe0)
				writer.Write ((byte)MessagePackConst.Formats.Int8);

			writer.Write (value);
		}


		private static void SerializeInt16 (Int16 value, BinaryWriter writer)
		{
			if ((value & 0xff) == value) {
				SerializeSByte ((sbyte)value, writer);
			} else {
				writer.Write ((byte)MessagePackConst.Formats.Int16);
				writer.Write (value);
			}
		}


		private static void SerializeInt32 (Int32 value, BinaryWriter writer)
		{
			if ((value & 0xffff) == value) {
				SerializeInt16 ((sbyte)value, writer);
			} else {
				writer.Write ((byte)MessagePackConst.Formats.Int32);
				writer.Write (value);
			}
		}


		private static void SerializeInt64 (Int64 value, BinaryWriter writer)
		{
			if ((value & 0xffffffff) == value) {
				SerializeInt32 ((sbyte)value, writer);
			} else {
				writer.Write ((byte)MessagePackConst.Formats.Int64);
				writer.Write (value);
			}
		}



		private static void SerializeMap (IDictionary dictionary, BinaryWriter writer)
		{
			if (!dictionary.GetType().IsGenericType)
				throw new NotSupportedException("Only generic dictionaries are supported.");
			
			Type keyType = dictionary.GetType().GetGenericArguments()[0];
			Type valueType = dictionary.GetType().GetGenericArguments()[1];

		}
	}
}

