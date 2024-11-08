using AuroraLib.Compression.Algorithms;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

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


    }
}
