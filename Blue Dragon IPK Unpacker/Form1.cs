using AuroraLib.Compression.Algorithms;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using DirectXTex;
using System;
using DrSwizzler;
using static DirectXTex.DirectXTexUtility;
using AuroraLib.Core.IO;

namespace Blue_Dragon_IPK_Unpacker
{
    public partial class Form1 : Form
    {
        //DEBUG
        //string DDSdatalog = "";

     
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {

            OpenFileDialog openFileDialog1 = new OpenFileDialog();

            openFileDialog1.InitialDirectory = "/";
            openFileDialog1.Filter = "Blue Dragon IPK1 Files (*.ipk,*.mpk)|*.ipk;*.mpk";
            openFileDialog1.FilterIndex = 0;
            openFileDialog1.RestoreDirectory = true;

            if (openFileDialog1.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            listBox1.Items.Clear();

            string selectedFileName = openFileDialog1.FileName;
            listBox1.Items.Add("Unpacking file: " + selectedFileName);

            UnpackIPK(selectedFileName);

            listBox1.Items.Add("Successfully unpacked file");
        }


        public void UnpackIPK(string FileName)
        {
            //DEBUG
            //DDSdatalog = "";

            List<string> DDSScaling = new List<string>();

            byte[] fileBytes = File.ReadAllBytes(FileName);

            using (BinaryReader file = new BinaryReader(
            File.Open(FileName, FileMode.Open)))
            {
                int Header = file.ReadInt32();
                if (Header != 0x314B5049) //Fail if header not "IPK1"
                {
                    return;
                }

                int CompressionType = file.ReadInt32(); //Still further researched needed but if 0x800 use zlib method
                int FileCount = file.ReadInt32(); //Total Number of files
                int PackSize = file.ReadInt32(); //Total size of IPK file including header and file info
                listBox1.Items.Add("Found " + FileCount + " files to unpack");

                for (int i = 0; i < FileCount; i++)
                {
                    string PackedFileName = System.Text.Encoding.UTF8.GetString(file.ReadBytes(64)).Split("\0")[0];

                    int Zip = file.ReadInt32(); //Unknown (Seemingly always 1)
                    int SizeCompressed = file.ReadInt32(); //Length of data to pull out of file
                    int FileOffset = file.ReadInt32(); //Location of compressed file in pack
                    int SizeDecompressed = file.ReadInt32(); //Use for error checking

                    file.ReadBytes(16); //Covering for current unknown


                    long HoldPosition = file.BaseStream.Position;

                    file.BaseStream.Seek(FileOffset, SeekOrigin.Begin);


                    Console.WriteLine("Decompressing index: " + i + "|" + (Zip + "|" + PackedFileName));

                    byte[] PackedFileBytes; Stream OutBytes;
                    if (Zip == 1)
                    {
                        PackedFileBytes = file.ReadBytes(SizeCompressed);
                        OutBytes = Decompress(PackedFileBytes);
                    }
                    else
                    {
                        PackedFileBytes = file.ReadBytes(SizeDecompressed);
                        OutBytes = new MemoryStream(PackedFileBytes);
                    }

                    string OutFileName = Path.GetDirectoryName(FileName) + "/" + Path.GetFileNameWithoutExtension(FileName) + "/" + PackedFileName;
                    Directory.CreateDirectory(Path.GetDirectoryName(OutFileName));
                    using (var fileStream = File.Create(OutFileName))
                    {
                        OutBytes.CopyTo(fileStream);
                    }

                    listBox1.Items.Add("Writing file: " + OutFileName);

                    if (Path.GetExtension(OutFileName) == ".dds" && checkBox1.Checked)
                    {
                        listBox1.Items.Add("Convering to real DDS");

                        using (BinaryReader DDSfile = new BinaryReader(
                        File.Open(OutFileName, FileMode.Open)))
                        {
                            DDSfile.BaseStream.Seek(0x2A, SeekOrigin.Begin);
                            int ScalingVal1 = DDSfile.ReadByte();
                            int ScalingVal2 = DDSfile.ReadByte();

                            //DDSfile.BaseStream.Seek(0x30, SeekOrigin.Begin);
                            //int ScalingVal3 = DDSfile.ReadInt32();
                            //int ScalingVal4 = DDSfile.ReadInt32();


                            DDSScaling.Add(Path.GetFileName(OutFileName) + "|" + ScalingVal1 + "|" + ScalingVal2);
                        }

                        byte[] convertedDDS = ConvertBDtoDDS(OutFileName);
                        File.WriteAllBytes(OutFileName, convertedDDS);
                    }

                    file.BaseStream.Seek(HoldPosition, SeekOrigin.Begin);

                }

                File.WriteAllLines(Path.GetDirectoryName(FileName) + "/" + Path.GetFileNameWithoutExtension(FileName) + "/TextureScaling.txt", DDSScaling);
            }

            //File.WriteAllText("./dataDebug.txt", DDSdatalog);

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

        private void button2_Click(object sender, EventArgs e)
        {
            string FileExtension = ".ipk";
            if (Control.ModifierKeys == Keys.Shift)
            {
                FileExtension = ".mpk";
            }


            FolderBrowserDialog folderDialog = new FolderBrowserDialog();

            folderDialog.InitialDirectory = "/";

            if (folderDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            listBox1.Items.Clear();

            string selectedFilePath = folderDialog.SelectedPath;
            PackIPK(selectedFilePath, FileExtension);

        }


        public void PackIPK(string pathName, string FileExtension)
        {
            string NewFilePath = pathName + FileExtension;
            listBox1.Items.Add("Packing file: " + NewFilePath);

            List<string> FilesToWrite = new List<string>();
            List<string> FilePathsToWrite = new List<string>();

            foreach (string file in Directory.EnumerateFiles(pathName, "*.*", SearchOption.AllDirectories))
            {
                if (file.Contains("TextureScaling.txt")) { continue; }
                string ShortenedFilePath = file.Replace(pathName + "\\", "");
                Console.WriteLine(ShortenedFilePath);
                FilesToWrite.Add(file);
                FilePathsToWrite.Add(ShortenedFilePath);
            }

            listBox1.Items.Add("Found " + FilesToWrite.Count() + " files to pack");

            if (!Directory.Exists(Path.GetDirectoryName(NewFilePath))) { Directory.CreateDirectory(Path.GetDirectoryName(NewFilePath)); }
            File.Create(NewFilePath).Close();

            using (var fileStream = new FileStream(NewFilePath, FileMode.Append, FileAccess.Write, FileShare.None))
            using (var file = new BinaryWriter(fileStream))
            {
                listBox1.Items.Add("Writing Header");
                //Header
                file.Write(0x314B5049); //"IPK1"
                file.Write(0x800); //Still unknown, something to do with compression
                file.Write(FilesToWrite.Count()); //File Count
                file.Write(0); //Total file size REMEMBER TO UPDATE AT END

                for (int i = 0; i < FilesToWrite.Count; i++)
                {
                    listBox1.Items.Add("Writing info for: " + FilePathsToWrite[i]);

                    file.Write(Encoding.UTF8.GetBytes(FilePathsToWrite[i].PadRight(64, '\0')));
                    file.Write(0); //No compression, hoping game will allow all file types to be left like this
                    file.Write(0); //File Size, REMEMBER TO UPDATE AFTER WRITING RESPECTIVE FILE IN
                    file.Write(0); //File Offset, REMEMBER TO UPDATE AFTER WRITING RESPECTIVE FILE IN
                    file.Write(0); //File Size 2, REMEMBER TO UPDATE AFTER WRITING RESPECTIVE FILE IN

                    file.Write(0x357070D3); //As yet unknown value but always this I think
                    file.Write(new byte[12]); //Null 12 Bytes
                }

                for (int i = 0; i < FilesToWrite.Count; i++)
                {
                    listBox1.Items.Add("Writing data for: " + FilePathsToWrite[i]);

                    byte[] BytesToWrite;
                    if (Path.GetExtension(FilePathsToWrite[i]) == ".dds" && checkBox1.Checked)
                    {
                        int ScaleVal = 0xE0;
                        int ScaleVal2 = 0xFF;
                        if (File.Exists(pathName + "/TextureScaling.txt"))
                        {
                            List<string> scalingVals = File.ReadAllLines(pathName + "/TextureScaling.txt").ToList();

                            string FileName = Path.GetFileName(FilePathsToWrite[i]);
                            for(int j = 0; j < scalingVals.Count; j++) 
                            {
                                if (scalingVals[j].Contains("|") && scalingVals[j].Split("|")[0] == FileName)
                                {
                                    ScaleVal = Int32.Parse(scalingVals[j].Split("|")[1]);
                                    ScaleVal2 = Int32.Parse(scalingVals[j].Split("|")[2]);
                                    listBox1.Items.Add("FOUND SCALING VALUE FOR: " + FileName);
                                    break;
                                }
                                
                            }
                        }

                        BytesToWrite = ConvertDDStoBD(FilesToWrite[i], ScaleVal, ScaleVal2);
                    }
                    else
                    {
                        BytesToWrite = File.ReadAllBytes(FilesToWrite[i]);
                    }

                    int FileSize = BytesToWrite.Length;
                    int FileOffset = (int)file.BaseStream.Position;
                    BytesToWrite = PadArray(BytesToWrite, 128);

                    file.Write(BytesToWrite);

                    long HoldPosition = file.BaseStream.Position;

                    //Write file info
                    int FileInfoPos = 16 + (i * 96) + 64 + 4;

                    file.Seek(FileInfoPos, SeekOrigin.Begin);
                    file.Write(FileSize);
                    file.Write(FileOffset);
                    file.Write(FileSize);

                    file.BaseStream.Seek(HoldPosition, SeekOrigin.Begin);
                }

                listBox1.Items.Add("Writing Total file size");
                int TotalFileSize = (int)file.BaseStream.Position;
                file.Seek(12, SeekOrigin.Begin);
                file.Write(TotalFileSize);

                listBox1.Items.Add("File sucessfully written");
            }


            byte[] PadArray(byte[] Data, int Size)
            {
                int Padding = Size - (Data.Length % Size);
                if (Padding == Size) { Padding = 0; }
                int ArraySize = Data.Length + Padding;

                byte[] newArray = new byte[ArraySize];
                Data.CopyTo(newArray, 0);

                return newArray;
            }

        }


        public byte[] ConvertBDtoDDS(string DDSFile)
        {
            List<byte> NewDDS = new List<byte>();
            byte[] oldDDS = File.ReadAllBytes(DDSFile);

            using (BinaryReader file = new BinaryReader(
            File.Open(DDSFile, FileMode.Open)))
            {
                file.BaseStream.Seek(0x2A, SeekOrigin.Begin);
                int UnknownScalingVal = file.ReadByte();


                int Magic = file.ReadInt32();
                if (Magic == 0x20534444) { listBox1.Items.Add("Standard DDS file detected, Skipping conversion."); return oldDDS; }


                file.BaseStream.Seek(0x21, SeekOrigin.Begin);
                int WidthMod = file.ReadByte();

                file.BaseStream.Seek(0x20, SeekOrigin.Begin);
                int Width = (file.ReadByte() - 0x80);
                if (WidthMod == 0xc0) { listBox1.Items.Add("WIDTH MOD EXAMPLE: " + DDSFile); Width = Width * 160; }
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

        public byte[] ConvertDDStoBD(string DDSFile, int ScalingVal1, int ScalingVal2)
        {
            byte[] DDSdata = File.ReadAllBytes(DDSFile);
            listBox1.Items.Add("Converting DDS file: " + DDSFile);

            using (BinaryReader file = new BinaryReader(
            File.Open(DDSFile, FileMode.Open)))
            {
                int Magic = file.ReadInt32();
                if (Magic != 0x20534444) { listBox1.Items.Add("Not a standard DDS file, Skipping conversion."); return DDSdata; }

                file.BaseStream.Seek(0xC, SeekOrigin.Begin);
                int Height = file.ReadInt32();
                int Width = file.ReadInt32();
                listBox1.Items.Add("Width: " + Width);
                listBox1.Items.Add("Height: " + Height);

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


        public byte[] GetSwizzled(byte[] DDSFile, int Width, int Height, DrSwizzler.DDS.DXEnums.DXGIFormat Format)
        {
            //Remove DDS header
            List<byte> DDSList = DDSFile.ToList();
            DDSList.RemoveRange(0, 0x80);
            DDSFile = DDSList.ToArray();
            return DrSwizzler.Swizzler.Xbox360Swizzle(DDSFile, Width, Height, Format);
        }
        public byte[] GetDeSwizzled(byte[] DDSFile, int Width, int Height, DrSwizzler.DDS.DXEnums.DXGIFormat Format)
        {
            //Remove Blue Dragon header
            List<byte> DDSList = DDSFile.ToList();
            DDSList.RemoveRange(0, 0x800);
            DDSFile = DDSList.ToArray();
            return DrSwizzler.Deswizzler.Xbox360Deswizzle(DDSFile, Width, Height, Format);
        }


        public int ToBigE(int Input)
        {
            byte[] bytes = BitConverter.GetBytes(Input);
            Array.Reverse(bytes, 0, bytes.Length);
            return BitConverter.ToInt32(bytes, 0);
        }

        //Auto converts everything to big endian
        public void ListAddInt(List<byte> list, int Value)
        {
            byte[] ByteArr = BitConverter.GetBytes(ToBigE(Value));
            for (int i = 0; i < ByteArr.Length; i++)
            {
                list.Add(ByteArr[i]);
            }
        }


    }

}
