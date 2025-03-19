using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Button;
using static Blue_Dragon_IPK_Unpacker.DDS;
using AuroraLib.Core.IO;
using ICSharpCode.SharpZipLib.Zip.Compression;
using System.Security.Cryptography;

namespace Blue_Dragon_IPK_Unpacker
{
    public static class IPK
    {
        public static void UnpackIPK(string FileName, string OutPathOverride = "", bool ConvertDDS = true)
        {
            //DEBUG
            //DDSdatalog = "";

            List<string> DDSScaling = new List<string>();
            List<string> OutDebug = new List<string>();

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
                //listBox1.Items.Add("Found " + FileCount + " files to unpack");

                for (int i = 0; i < FileCount; i++)
                {
                    string PackedFileName = System.Text.Encoding.UTF8.GetString(file.ReadBytes(64)).Split("\0")[0];

                    int Zip = file.ReadInt32(); //Unknown (Seemingly always 1)
                    int SizeCompressed = file.ReadInt32(); //Length of data to pull out of file
                    int FileOffset = file.ReadInt32(); //Location of compressed file in pack
                    int SizeDecompressed = file.ReadInt32(); //Use for error checking

                    OutDebug.Add(PackedFileName + "," + Zip + "," + SizeCompressed + "," + SizeDecompressed);

                    file.ReadBytes(16); //Covering for current unknown


                    long HoldPosition = file.BaseStream.Position;

                    file.BaseStream.Seek(FileOffset, SeekOrigin.Begin);


                    Console.WriteLine("Decompressing index: " + i + "|" + (Zip + "|" + PackedFileName));

                    byte[] PackedFileBytes; Stream OutBytes;
                    if (Zip == 1)
                    {
                        PackedFileBytes = file.ReadBytes(SizeCompressed);
                        OutBytes = Utils.Decompress(PackedFileBytes);
                    }
                    else
                    {
                        PackedFileBytes = file.ReadBytes(SizeDecompressed);
                        OutBytes = new MemoryStream(PackedFileBytes);
                    }

                    string OutFileName = Path.GetDirectoryName(FileName) + "/" + Path.GetFileNameWithoutExtension(FileName) + "/" + PackedFileName;
                    if(OutPathOverride != "") { OutFileName = OutPathOverride + "/" + Path.GetFileNameWithoutExtension(FileName) + "/" + PackedFileName; }
                    Directory.CreateDirectory(Path.GetDirectoryName(OutFileName));
                    using (var fileStream = File.Create(OutFileName))
                    {
                        OutBytes.CopyTo(fileStream);
                    }

                    //listBox1.Items.Add("Writing file: " + OutFileName);

                    if (Path.GetExtension(OutFileName) == ".dds" && ConvertDDS)
                    {
                        //listBox1.Items.Add("Convering to real DDS");

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
                //File.WriteAllLines(Path.GetDirectoryName(FileName) + "/" + Path.GetFileNameWithoutExtension(FileName) + "/FileInfo.txt", OutDebug);
            }

            //File.WriteAllText("./dataDebug.txt", DDSdatalog);

        }


        public static void PackIPK(string pathName, string FileExtension, bool ConvertDDS = true)
        {
            string NewFilePath = pathName + FileExtension;
            //listBox1.Items.Add("Packing file: " + NewFilePath);

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

            //listBox1.Items.Add("Found " + FilesToWrite.Count() + " files to pack");

            if (!Directory.Exists(Path.GetDirectoryName(NewFilePath))) { Directory.CreateDirectory(Path.GetDirectoryName(NewFilePath)); }
            File.Create(NewFilePath).Close();

            using (var fileStream = new FileStream(NewFilePath, FileMode.Append, FileAccess.Write, FileShare.None))
            using (var file = new BinaryWriter(fileStream))
            {
                //listBox1.Items.Add("Writing Header");
                //Header
                file.Write(0x314B5049); //"IPK1"
                file.Write(0x800); //Still unknown, something to do with compression
                file.Write(FilesToWrite.Count()); //File Count
                file.Write(0); //Total file size REMEMBER TO UPDATE AT END

                for (int i = 0; i < FilesToWrite.Count; i++)
                {
                    //listBox1.Items.Add("Writing info for: " + FilePathsToWrite[i]);

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
                    //listBox1.Items.Add("Writing data for: " + FilePathsToWrite[i]);

                    byte[] BytesToWrite;
                    if (Path.GetExtension(FilePathsToWrite[i]) == ".dds" && ConvertDDS)
                    {
                        int ScaleVal = 0xE0;
                        int ScaleVal2 = 0xFF;
                        if (File.Exists(pathName + "/TextureScaling.txt"))
                        {
                            List<string> scalingVals = File.ReadAllLines(pathName + "/TextureScaling.txt").ToList();

                            string FileName = Path.GetFileName(FilePathsToWrite[i]);
                            for (int j = 0; j < scalingVals.Count; j++)
                            {
                                if (scalingVals[j].Contains("|") && scalingVals[j].Split("|")[0] == FileName)
                                {
                                    ScaleVal = Int32.Parse(scalingVals[j].Split("|")[1]);
                                    ScaleVal2 = Int32.Parse(scalingVals[j].Split("|")[2]);
                                    //listBox1.Items.Add("FOUND SCALING VALUE FOR: " + FileName);
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

                //listBox1.Items.Add("Writing Total file size");
                int TotalFileSize = (int)file.BaseStream.Position;
                file.Seek(12, SeekOrigin.Begin);
                file.Write(TotalFileSize);

                //listBox1.Items.Add("File sucessfully written");
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



    public class IPK_Pack
    {
        string IPKName;
        //Header Data
        int Magic = 0x314B5049; //"1KPI" written as "IPK1" after endian swap
        int Unknown = 80000;
        //int FileCount;
        //int FileSize;

        List<IPK_File> Files = new List<IPK_File>();

        public bool ReadIPKFile(string Filepath)
        {
            if(!File.Exists(Filepath)) return false;
            IPKName = Path.GetFileNameWithoutExtension(Filepath);

            using (BinaryReader file = new BinaryReader(
            File.Open(Filepath, FileMode.Open)))
            {
                //Header
                Magic = file.ReadInt32();
                Unknown = file.ReadInt32();
                int FileCount = file.ReadInt32();
                int FileSize = file.ReadInt32();

                //First pass files, gets data from table at start
                for(int i = 0; i <  FileCount; i++)
                {
                    IPK_File tempFile = new IPK_File()
                    {
                        FileNameRaw = file.ReadBytes(64),
                        CompressionType = file.ReadInt32(),
                        FileSizeCompressed = file.ReadInt32(),
                        FileOffset = file.ReadInt32(),
                        RawFileSize = file.ReadInt32(),
                        Unknown = file.ReadInt32(),
                    };
                    tempFile.FileName = Encoding.Default.GetString(tempFile.FileNameRaw);
                    tempFile.FileName = tempFile.FileName.Replace("\0", "");
                    file.ReadBytes(12);
                    Files.Add(tempFile);
                }
                //Second pass files, reads in actual data from file and decompresses if needed
                foreach(IPK_File PackedFile in Files)
                {
                    file.BaseStream.Seek(PackedFile.FileOffset, 0);
                    byte[] data = file.ReadBytes(PackedFile.FileSizeCompressed);
                    PackedFile.DataCompressed = data;
                    //If compression is enabled then decompress
                    if(PackedFile.CompressionType == 1)
                    {
                        PackedFile.DataUncompressed = Utils.DecompressArray(data);
                    }
                    else
                    {
                        //If not then the data will be identical
                        PackedFile.DataUncompressed = data;
                    }
                }
            }

            return true;
        }

        public void WriteFiles(string folderPath)
        {
            foreach(IPK_File file in Files)
            {
                string FinalPath = folderPath + "/" + IPKName + "/" + file.FileName;
                Directory.CreateDirectory(Path.GetDirectoryName(FinalPath));

                byte[] DataToWrite = file.DataUncompressed;

                if (FinalPath.EndsWith(".dds"))
                {
                    //CODE TO CONVER BD DDS INTO REAL DDS
                }

                File.WriteAllBytes(FinalPath, DataToWrite);
            }
        }






    }
    public class IPK_File
    {
        public string FileName = "";
        public byte[] FileNameRaw = new byte[64];
        public int CompressionType; //0 for none, 1 for zlib. none other observed currently
        public int FileSizeCompressed;
        public int FileOffset;
        public int RawFileSize;
        public int Unknown; //Seemingly identical across the same file but different between files
        //12 empty bytes here :3

        public byte[] DataCompressed;
        public byte[] DataUncompressed;
    }







}
