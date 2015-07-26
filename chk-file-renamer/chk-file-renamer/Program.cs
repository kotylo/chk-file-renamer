using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace chk_file_renamer
{
    class Program
    {
        const string defaultPath = @"G:\found-files\";

        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int memcmp(byte[] b1, byte[] b2, long count);

        static bool ByteArrayCompare(byte[] b1, byte[] b2)
        {
            // Validate buffers are the same length.
            // This also ensures that the count does not exceed the length of either buffer.  
            return b1.Length == b2.Length && memcmp(b1, b2, b1.Length) == 0;
        }

        public class FileBytes
        {
            public byte[] Bytes { get; set; }
            public string Extension { get; set; }
        }

        static void Main(string[] args)
        {
            var fbList = new List<FileBytes>()
            {
                new FileBytes()
                {
                    Bytes = new byte[] {0x49, 0x44, 0x33},
                    Extension = "mp3"
                },
                new FileBytes()
                {
                    Bytes = new byte[] {0xff, 0xfb, 0xc0, 0x04},
                    Extension = "mp3"
                },
                new FileBytes()
                {
                    Bytes = new byte[] {0xff, 0xfb, 0xb0, 0x64},
                    Extension = "mp3"
                },
                new FileBytes()
                {
                    Bytes = new byte[] {0xff, 0xfb},
                    Extension = "mp3"
                },
                new FileBytes()
                {
                    Bytes = new byte[] {0x00, 0x00, 0x01, 0xba},
                    Extension = "mov"
                },
                new FileBytes()
                {
                    Bytes = new byte[] {0x1a, 0x45, 0xdf, 0xa3},
                    Extension = "mkv"
                },
                new FileBytes()
                {
                    Bytes = new byte[] {0x77, 0x76, 0x70, 0x6b},
                    Extension = "wav"
                },
                new FileBytes()
                {
                    Bytes = new byte[] {0x66, 0x4c, 0x61, 0x43},
                    Extension = "flac"
                },
            };

            var path = defaultPath;
            if (args.Length > 0)
            {
                path = args[0];
            }

            Console.WriteLine("Available commands: ");
            Console.WriteLine("\tc - starts the search for *.chk files in provided folder in args[0] and renames matched files");
            Console.WriteLine("\td - searches for *.mp3 duplicates and lists them in the duplicates.txt file");
            var command = Console.ReadLine();

            if (command == "c" || command == "")
            {
                Console.WriteLine("Starting search in path '{0}'", path);
                var di = new DirectoryInfo(path);

                var files = di.GetFiles("*.chk", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    Console.WriteLine("Processing {0}", file.Name);

                    var newName = string.Empty;
                    using (var fileStream = File.Open(file.FullName, FileMode.Open, FileAccess.Read))
                    using (var br = new BinaryReader(fileStream))
                    {
                        foreach (var fileBytese in fbList)
                        {
                            br.BaseStream.Seek(0, SeekOrigin.Begin);
                            var firstBytes = br.ReadBytes(fileBytese.Bytes.Length);

                            if (ByteArrayCompare(firstBytes, fileBytese.Bytes))
                            {
                                Console.WriteLine("Match found ({0})", fileBytese.Extension);
                                newName = file.FullName.Remove(file.FullName.Length - file.Name.Length) +
                                          file.Name.Remove(file.Name.LastIndexOf('.')) + "." +
                                          fileBytese.Extension;
                                break;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(newName))
                    {
                        File.Move(file.FullName, newName);
                    }
                }
            }
            else if (command == "d")
            {
                Console.WriteLine("Starting search for mp3 duplicates in path '{0}'", path);

                List<string> filesToRemove = new List<string>();

                var di = new DirectoryInfo(path);
                var files = di.GetFiles("*.mp3", SearchOption.TopDirectoryOnly);

                var i = 0;
                var orderedFiles = files.OrderByDescending(f => !f.Name.StartsWith("FILE")).ToList();
                foreach (FileInfo fileInfo in orderedFiles)
                {
                    i++;
                    if (filesToRemove.Contains(fileInfo.Name))
                    {
                        continue;
                    }

                    var matchedFiles = files.Where(f => f.Name != fileInfo.Name).Where(f => f.Length == fileInfo.Length).ToList();
                    if (matchedFiles.Count > 0)
                    {
                        Console.WriteLine("----");
                        using (var originalFileStream = File.OpenRead(fileInfo.FullName))
                        {
                            byte[] originalBuffer = new byte[512];

                            foreach (FileInfo matchedFile in matchedFiles)
                            {
                                originalFileStream.Seek(0, SeekOrigin.Begin);
                                Console.WriteLine("Comparing {0} and {1} [{2}/{3}]", fileInfo.Name, matchedFile.Name, i, orderedFiles.Count);
                                var isTheSame = true;
                                using (var fileStream = File.Open(matchedFile.FullName, FileMode.Open, FileAccess.Read))
                                {
                                    var buffer = new byte[512];
                                    int bytesRead;
                                    while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                                    {
                                        originalFileStream.Read(originalBuffer, 0, bytesRead);
                                        if (!ByteArrayCompare(buffer, originalBuffer))
                                        {
                                            Console.WriteLine("Files are not equal");
                                            isTheSame = false;
                                            break;
                                        }
                                    }
                                }

                                if (isTheSame)
                                {
                                    Console.WriteLine("Equal!");
                                    filesToRemove.Add(matchedFile.Name);
                                    break;
                                }
                            }
                        }
                    }
                }

                var sb = new StringBuilder();
                foreach (var fileName in filesToRemove)
                {
                    sb.AppendFormat(@"""{0}""", fileName);
                }

                File.WriteAllText(@"duplicates.txt", sb.ToString());
            }
        }
    }
}
