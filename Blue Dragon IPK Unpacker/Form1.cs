using AuroraLib.Compression.Algorithms;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using DirectXTex;
using System;
using DrSwizzler;

namespace Blue_Dragon_IPK_Unpacker
{
    public partial class Form1 : Form
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        public Form1()
        {
            InitializeComponent();
            //AllocConsole();
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

                    file.BaseStream.Seek(HoldPosition, SeekOrigin.Begin);

                }

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

                    byte[] BytesToWrite = File.ReadAllBytes(FilesToWrite[i]);
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

        private void button3_Click(object sender, EventArgs e)
        {


            OpenFileDialog openFileDialog1 = new OpenFileDialog();

            openFileDialog1.InitialDirectory = "/";
            openFileDialog1.Filter = "Blue Dragon DDS Files (*.dds)|*.dds";
            openFileDialog1.FilterIndex = 0;
            openFileDialog1.RestoreDirectory = true;

            if (openFileDialog1.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            string selectedFileName = openFileDialog1.FileName;
            listBox1.Items.Clear();
            listBox1.Items.Add("Converting DDS: " + selectedFileName);

            ConvertDDS(selectedFileName);

            //listBox1.Items.Add("Successfully unpacked file");
        }

        public void ConvertDDS(string DDSFile)
        {
            byte[] DDSdata = File.ReadAllBytes(DDSFile);
            listBox1.Items.Add(DDSdata.Length - 0x80);

            using (BinaryReader file = new BinaryReader(
            File.Open(DDSFile, FileMode.Open)))
            {
                file.BaseStream.Seek(0xC, SeekOrigin.Begin);
                int Height = file.ReadInt32();
                int Width = file.ReadInt32();
                listBox1.Items.Add("Width: " + Width);
                listBox1.Items.Add("Height: " + Height);

                file.BaseStream.Seek(0x54, SeekOrigin.Begin);
                int Format = file.ReadInt32();
                DrSwizzler.DDS.DXEnums.DXGIFormat PixelFormat;


                switch(Format)
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
                for(int i = 0; i < 12; i++)
                {
                    Header.Add(0);
                }
                ListAddInt(Header, -65536);
                ListAddInt(Header, -65536);

                Header.Add((byte)((Width / 128) + 0x80));
                Header.Add(0);

                Header.Add(0);
                Header.Add(2);

                switch(PixelFormat)
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

                Header.Add(0xE0);
                Header.Add(0xFF);
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

            File.WriteAllBytes(DDSFile, DDSdata);
        }


        public byte[] GetSwizzled(byte[] DDSFile, int Width, int Height, DrSwizzler.DDS.DXEnums.DXGIFormat Format)
        {
            List<byte> DDSList = DDSFile.ToList();
            DDSList.RemoveRange(0, 0x80);
            DDSFile = DDSList.ToArray();
            return DrSwizzler.Swizzler.Xbox360Swizzle(DDSFile, Width, Height, Format);
        }
        public byte[] GetDeSwizzled(byte[] DDSFile, int Width, int Height, DrSwizzler.DDS.DXEnums.DXGIFormat Format)
        {
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
            for(int i = 0; i < ByteArr.Length; i++) 
            {
                list.Add(ByteArr[i]);
            }
        }
    }
    
}
