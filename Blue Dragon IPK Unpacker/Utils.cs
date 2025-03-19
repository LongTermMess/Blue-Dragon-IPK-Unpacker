using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using ICSharpCode.SharpZipLib.Zip.Compression;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blue_Dragon_IPK_Unpacker
{
    public static class Utils
    {
        public static void DebugLog(string Message)
        {
            Console.WriteLine(Message);
        }



        public static int ToBigE(int Input)
        {
            byte[] bytes = BitConverter.GetBytes(Input);
            Array.Reverse(bytes, 0, bytes.Length);
            return BitConverter.ToInt32(bytes, 0);
        }
        public static short ToBigE_Short(short Input)
        {
            byte[] bytes = BitConverter.GetBytes(Input);
            Array.Reverse(bytes, 0, bytes.Length);
            return BitConverter.ToInt16(bytes, 0);
        }
        public static float ToBigE_Float(float Input)
        {
            byte[] bytes = BitConverter.GetBytes(Input);
            Array.Reverse(bytes, 0, bytes.Length);
            return BitConverter.ToSingle(bytes, 0);
        }

        //Auto converts everything to big endian
        public static void ListAddInt(List<byte> list, int Value)
        {
            byte[] ByteArr = BitConverter.GetBytes(ToBigE(Value));
            for (int i = 0; i < ByteArr.Length; i++)
            {
                list.Add(ByteArr[i]);
            }
        }

        public static Stream Decompress(byte[] data)
        {
            var outputStream = new MemoryStream();
            using (var compressedStream = new MemoryStream(data))
            using (var inputStream = new InflaterInputStream(compressedStream))
            {
                inputStream.CopyTo(outputStream);
                outputStream.Position = 0;
                return outputStream;
            }

        }
        public static byte[] DecompressArray(byte[] data)
        {
            var outputStream = new MemoryStream();
            using (var compressedStream = new MemoryStream(data))
            using (var inputStream = new InflaterInputStream(compressedStream))
            {
                inputStream.CopyTo(outputStream);
                outputStream.Position = 0;
                return outputStream.ToArray();
            }

        }
        public static byte[] Compress(byte[] data)
        {
            var outputStream = new MemoryStream();
            Deflater deflater = new Deflater(Deflater.DEFAULT_COMPRESSION);
            using (var compressedStream = new DeflaterOutputStream(outputStream, deflater))
            {
                compressedStream.Write(data);
                return outputStream.ToArray();//Moved but if crashing move out of the brackets again
            }

        }

    }


    public class BigEndianReader : BinaryReader
    {
        public BigEndianReader(Stream dataStream) : base(dataStream)
        {

        }

        public override int ReadInt32()
        {
            int value = base.ReadInt32();
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes, 0, bytes.Length);
            return BitConverter.ToInt32(bytes, 0);
        }
        public override short ReadInt16()
        {
            short value = base.ReadInt16();
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes, 0, bytes.Length);
            return BitConverter.ToInt16(bytes, 0);
        }
        public override float ReadSingle()
        {
            float value = base.ReadSingle();
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes, 0, bytes.Length);
            return BitConverter.ToSingle(bytes, 0);
        }


    }

    public class BigEndianWriter : BinaryWriter
    {
        public BigEndianWriter(Stream dataStream) : base(dataStream)
        {

        }

        public override void Write(int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes, 0, bytes.Length);
            base.Write(BitConverter.ToInt32(bytes, 0));
        }
        public override void Write(short value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes, 0, bytes.Length);
            base.Write(BitConverter.ToInt16(bytes, 0));
        }
        public override void Write(float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes, 0, bytes.Length);
            base.Write(BitConverter.ToSingle(bytes, 0));
        }

    }
}
