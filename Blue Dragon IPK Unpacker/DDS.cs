using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DirectXTex.DirectXTexUtility;
using System.Windows.Forms;
using static Blue_Dragon_IPK_Unpacker.Utils;

namespace Blue_Dragon_IPK_Unpacker
{
    public static class DDS
    {


        public static byte[] ConvertBDtoDDS(string DDSFile)
        {
            List<byte> NewDDS = new List<byte>();
            byte[] oldDDS = File.ReadAllBytes(DDSFile);

            using (BinaryReader file = new BinaryReader(
            File.Open(DDSFile, FileMode.Open)))
            {
                file.BaseStream.Seek(0x2A, SeekOrigin.Begin);
                int UnknownScalingVal = file.ReadByte();


                int Magic = file.ReadInt32();
                //if (Magic == 0x20534444) { listBox1.Items.Add("Standard DDS file detected, Skipping conversion."); return oldDDS; }


                file.BaseStream.Seek(0x21, SeekOrigin.Begin);
                int WidthMod = file.ReadByte();

                file.BaseStream.Seek(0x20, SeekOrigin.Begin);
                int Width = (file.ReadByte() - 0x80);
                if (WidthMod == 0xc0) { /*listBox1.Items.Add("WIDTH MOD EXAMPLE: " + DDSFile);*/ Width = Width * 160; }
                else { Width = Width * 128; }

                file.BaseStream.Seek(0x29, SeekOrigin.Begin);
                int Height = (file.ReadByte() + 1) * 8;

                file.BaseStream.Seek(0x24, SeekOrigin.Begin);
                int Format = ToBigE(file.ReadInt32());
                DXGIFormat PixelFormat;
                switch (Format)
                {
                    default:
                        PixelFormat = DXGIFormat.BC1UNORM; //I dunno :3
                        break;
                    case 0x52:
                        PixelFormat = DXGIFormat.BC1UNORM;
                        break;
                    case 0x53:
                        PixelFormat = DXGIFormat.BC2UNORM;
                        break;
                    case 0x54:
                        PixelFormat = DXGIFormat.BC3UNORM;
                        break;
                    case 0x86:
                        PixelFormat = DXGIFormat.R8G8B8A8UNORM;
                        break;
                }

                //DEBUG
                //DDSdatalog = DDSdatalog + Path.GetFileName(DDSFile) + "|0x" + UnknownScalingVal.ToString("X2") + "|" + Width + "|" + Height + "\n";

                TexMetadata MetaData = GenerateMataData(Width, Height, 1, PixelFormat, false);
                MetaData.MiscFlags2 = TexMiscFlags2.TEXMISC2ALPHAMODEMASK;

                GenerateDDSHeader(MetaData, DDSFlags.NONE, out var header, out var dx10Header, false);
                NewDDS.AddRange(EncodeDDSHeader(header, dx10Header));

                NewDDS.AddRange(GetDeSwizzled(oldDDS, Width, Height, (DrSwizzler.DDS.DXEnums.DXGIFormat)PixelFormat));


            }

            return NewDDS.ToArray();
            //File.WriteAllBytes(DDSFile + ".new.dds", NewDDS.ToArray());
        }

        public static byte[] ConvertDDStoBD(string DDSFile, int ScalingVal1, int ScalingVal2)
        {
            byte[] DDSdata = File.ReadAllBytes(DDSFile);
            //listBox1.Items.Add("Converting DDS file: " + DDSFile);

            using (BinaryReader file = new BinaryReader(
            File.Open(DDSFile, FileMode.Open)))
            {
                int Magic = file.ReadInt32();
                if (Magic != 0x20534444) { /*listBox1.Items.Add("Not a standard DDS file, Skipping conversion.");*/ return DDSdata; }

                file.BaseStream.Seek(0xC, SeekOrigin.Begin);
                int Height = file.ReadInt32();
                int Width = file.ReadInt32();
                //listBox1.Items.Add("Width: " + Width);
                //listBox1.Items.Add("Height: " + Height);

                file.BaseStream.Seek(0x54, SeekOrigin.Begin);
                int Format = file.ReadInt32();
                DrSwizzler.DDS.DXEnums.DXGIFormat PixelFormat;


                switch (Format)
                {
                    default:
                        PixelFormat = DrSwizzler.DDS.DXEnums.DXGIFormat.R8G8B8A8UNORM;
                        break;
                    case 0x31545844:
                        PixelFormat = DrSwizzler.DDS.DXEnums.DXGIFormat.BC1UNORM;
                        break;
                    case 0x33545844:
                        PixelFormat = DrSwizzler.DDS.DXEnums.DXGIFormat.BC2UNORM;
                        break;
                    case 0x35545844:
                        PixelFormat = DrSwizzler.DDS.DXEnums.DXGIFormat.BC3UNORM;
                        break;
                }

                //0x31545844 dxt1
                //0x33545844 dxt3
                //0x35545844 dxt5
                //0x0 R8G8B8A8UNORM



                //Write header here
                List<byte> Header = new List<byte>();

                ListAddInt(Header, DDSdata.Length - 0x80); //Data minus Original DDS header

                ListAddInt(Header, 3);
                ListAddInt(Header, 1);
                for (int i = 0; i < 12; i++)
                {
                    Header.Add(0);
                }
                ListAddInt(Header, -65536);
                ListAddInt(Header, -65536);

                Header.Add((byte)((Width / 128) + 0x80));
                Header.Add(0);

                Header.Add(0);
                Header.Add(2);

                switch (PixelFormat)
                {
                    default:
                        ListAddInt(Header, 0x86);
                        break;
                    case DrSwizzler.DDS.DXEnums.DXGIFormat.BC1UNORM:
                        ListAddInt(Header, 0x52);
                        break;
                    case DrSwizzler.DDS.DXEnums.DXGIFormat.BC2UNORM:
                        ListAddInt(Header, 0x53);
                        break;
                    case DrSwizzler.DDS.DXEnums.DXGIFormat.BC3UNORM:
                        ListAddInt(Header, 0x54);
                        break;
                }

                Header.Add(0);

                Header.Add((byte)((Height / 8) - 1));

                //Unknown scaling value, 
                //Header.Add(0xE0);
                //Header.Add(0xFF);
                Header.Add((byte)ScalingVal1);
                Header.Add((byte)ScalingVal2);

                ListAddInt(Header, 3344);
                ListAddInt(Header, 0);
                ListAddInt(Header, 512);

                for (int i = 0; i < 0x7c8; i++)
                {
                    Header.Add(0);
                }


                //Write Swizzed shizz here

                List<byte> SwizzledData = GetSwizzled(DDSdata, Width, Height, PixelFormat).ToList();
                Header.AddRange(SwizzledData);
                DDSdata = Header.ToArray();
            }

            return DDSdata;
            //File.WriteAllBytes(DDSFile, DDSdata);
        }


        public static byte[] GetSwizzled(byte[] DDSFile, int Width, int Height, DrSwizzler.DDS.DXEnums.DXGIFormat Format)
        {
            //Remove DDS header
            List<byte> DDSList = DDSFile.ToList();
            DDSList.RemoveRange(0, 0x80);
            DDSFile = DDSList.ToArray();
            return DrSwizzler.Swizzler.Xbox360Swizzle(DDSFile, Width, Height, Format);
        }
        public static byte[] GetDeSwizzled(byte[] DDSFile, int Width, int Height, DrSwizzler.DDS.DXEnums.DXGIFormat Format)
        {
            //Remove Blue Dragon header
            List<byte> DDSList = DDSFile.ToList();
            DDSList.RemoveRange(0, 0x800);
            DDSFile = DDSList.ToArray();
            return DrSwizzler.Deswizzler.Xbox360Deswizzle(DDSFile, Width, Height, Format);
        }

    }
}
