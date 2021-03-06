using System;
using System.IO;
using System.Text;

namespace JPEGrip
{
    // ReSharper disable once InconsistentNaming
    internal static class JPEGrip
    {
        private enum JpegMarker : byte //JPEG marker codes
        {
            M_TEM = 0x01,
            M_SOF0 = 0xc0, M_RST0 = 0xd0, M_APP0 = 0xe0, M_JPG0 = 0xf0,
            M_SOF1 = 0xc1, M_RST1 = 0xd1, M_APP1 = 0xe1,
            M_SOF2 = 0xc2, M_RST2 = 0xd2, M_APP2 = 0xe2,
            M_SOF3 = 0xc3, M_RST3 = 0xd3, M_APP3 = 0xe3,
            M_DHT = 0xc4, M_RST4 = 0xd4, M_APP4 = 0xe4,
            M_SOF5 = 0xc5, M_RST5 = 0xd5, M_APP5 = 0xe5,
            M_SOF6 = 0xc6, M_RST6 = 0xd6, M_APP6 = 0xe6,
            M_SOF7 = 0xc7, M_RST7 = 0xd7, M_APP7 = 0xe7,
            M_JPG = 0xc8, M_SOI = 0xd8, M_APP8 = 0xe8,
            M_SOF9 = 0xc9, M_EOI = 0xd9, M_APP9 = 0xe9,
            M_SOF10 = 0xca, M_SOS = 0xda, M_APP10 = 0xea,
            M_SOF11 = 0xcb, M_DQT = 0xdb, M_APP11 = 0xeb,
            M_DAC = 0xcc, M_DNL = 0xdc, M_APP12 = 0xec,
            M_SOF13 = 0xcd, M_DRI = 0xdd, M_APP13 = 0xed, M_JPG13 = 0xfd,
            M_SOF14 = 0xce, M_DHP = 0xde, M_APP14 = 0xee, M_COM = 0xfe,
            M_SOF15 = 0xcf, M_EXP = 0xdf, M_APP15 = 0xef
        }

        private static bool _kill, _recurse, _replace = true, _ignore, _verbose, _warningRaised;
        private static long _warningsCount, _rippedBytesTotal, _filesCount;
        private static readonly ConsoleColor DefaultColor = Console.ForegroundColor;

        private static void Warning(string s)
        {
            Console.Write(" ∙ ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("WARNING: " + s); _warningRaised = true; _warningsCount++;
            Console.ForegroundColor = DefaultColor;
        }

        private static void MinorWarning(string s)
        {
            Console.Write(" ∙ ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("WARNING: " + s);
            Console.ForegroundColor = DefaultColor;
        }

        private static void Error(string s)
        {
            Console.Write(" ∙ ");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERROR: " + s); _warningRaised = true; _warningsCount++;
            Console.ForegroundColor = DefaultColor;
        }

        public static void Main(string[] args)
        {

            if (args.Length == 0)
            {
                Console.WriteLine(
@"Utility for ripping unimportant info from JPEGs. Release 7.
Image quality is not affected. Will generate cleaned files with .jif extension.

Usage: JPEGRip.exe path [switches]
Switches:
    /D - Don`t replace original files with cleaned
    /G - Go! Run this tool with most used parameters ""*.jpg /I /R""
    /I - Ignore data after EOI marker
    /K - Kill bad files
    /R - Recurse subdirectories
    /V - Verbose output

You can use wildcards in file names, or simply provide path to an directory and all of files there will be processed.");
                return;
            }

            var path = args[0];

            foreach (var s in args)
            {
                if (s.Length == 2 && s[0] == '/')
                    switch (s[1])
                    {
                        case 'D':
                        case 'd': _replace = false; break;
                        case 'G':
                        case 'g': _ignore = true; _recurse = true; path = "*.jpg"; break;
                        case 'I':
                        case 'i': _ignore = true; break;
                        case 'K':
                        case 'k': _kill = true; break;
                        case 'R':
                        case 'r': _recurse = true; break;
                        case 'V':
                        case 'v': _verbose = true; break;
                        default: Console.WriteLine("Unknown switch: " + s); return;
                    }
            }

            string fileFilter = Path.GetFileName(path),
                   directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory)) directory = ".";
            if (string.IsNullOrEmpty(fileFilter) || fileFilter.IndexOfAny(new[] { '*', '?' }) == -1)
            {
                fileFilter = "*.*";
                if (File.Exists(path)) ProcessFile(path);
                else if (Directory.Exists(path)) ProcessDirectory(path, fileFilter);
                else { Error($"\"{path}\" is not a valid file or directory."); return; }
            }
            else
            {
                if (File.Exists(path)) ProcessFile(path);
                else if (Directory.Exists(directory)) ProcessDirectory(directory, fileFilter);
                else { Error($"\"{path}\" is not a valid file or directory."); return; }
            }

            if (_warningsCount > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nThere was {_warningsCount} warning(s)!");
                Console.ForegroundColor = DefaultColor;
            }
            Console.WriteLine($"\n{(decimal)_rippedBytesTotal / 1048576:N2} Mb ripped from {_filesCount} file{(_filesCount != 1 ? "s" : "")}.");
        }

        private static void ProcessDirectory(string targetDirectory, string targetFilter)
        {
            string[] fileEntries;
            try { fileEntries = Directory.GetFiles(targetDirectory, targetFilter); }
            catch (UnauthorizedAccessException) { Error($"Insufficient privileges to access \"{targetDirectory}\""); return; }

            foreach (var fileName in fileEntries) ProcessFile(fileName);

            if (!_recurse) return;
            var directories = Directory.GetDirectories(targetDirectory);
            foreach (var subdirectory in directories) ProcessDirectory(subdirectory, targetFilter);
        }

        private static void ProcessFile(string path)
        {
            int i;
            _warningRaised = false; _filesCount++;

            Console.ForegroundColor = ConsoleColor.Blue; Console.Write("Ripping ");
            Console.ForegroundColor = DefaultColor; Console.Write("\"{0}\"", path);

            FileStream fs;
            try { fs = File.OpenRead(path); }
            catch (UnauthorizedAccessException) { Error($"Insufficient privileges to access \"{path}\""); return; }

            if (fs.Length < 1024) { Error("File too short"); return; }

            var file = new byte[fs.Length];
            fs.Read(file, 0, file.Length); fs.Close();

            for (i = 0; i < file.Length - 1 && (file[i] != 0xFF || file[i + 1] != (byte)JpegMarker.M_SOI); i++) { }
            if (i >= file.Length - 1) Warning("There`s no SOI marker. Not JPEG?");
            else
            {
                var outfile = Path.ChangeExtension(path, ".jif");
                if (File.Exists(outfile)) { Error($"\"{outfile}\" already exists"); return; }
                fs = File.OpenWrite(outfile);
                fs.Write(file, i, 2); //write SOI
                i += 2;

                var foundEOI = false;
                var foundAPP0 = false;
                var foundSOF = false;

                do
                {
                    var drop = false;
                    for (; i + 3 < file.Length && (file[i] != 0xFF || file[i + 1] == 0xFF); i++) { } //get next marker

                    int frameLength;
                    if (i + 3 < file.Length) frameLength = (int)(((uint)file[i + 2] << 8) + file[i + 3]); else frameLength = 2;
                    if (frameLength < 2) { Warning("Invalid frame length"); frameLength = 2; }
                    frameLength -= 2; //less than 64K by 2 bytes of length!
                    var buf = i + 4;
                    if (buf + frameLength >= file.Length) drop = true;

                    switch (file[i + 1])
                    {
                        case (byte)JpegMarker.M_DAC: //Arithmetic DC & AC tables
                        case (byte)JpegMarker.M_DHT: //Huffman DC & AC tables
                        case (byte)JpegMarker.M_DQT: //Quantization table
                            break;
                        case (byte)JpegMarker.M_APP0: //JFIF header
                            drop = true; i = i + frameLength + 4;
                            if (_verbose && !foundAPP0 && Encoding.ASCII.GetString(file, buf, 4) == "JFIF")
                            {
                                foundAPP0 = true;
                                Console.Write(" ∙ JFIF v.{0}.{1}; density: {2} x {3} {4}.",
                                    file[buf + 5], file[buf + 6],
                                    ((uint)file[buf + 8] << 8) + file[buf + 9],
                                    ((uint)file[buf + 10] << 8) + file[buf + 11],
                                    file[buf + 7] switch
                                    {
                                        0 => "pixels",
                                        1 => "dpi",
                                        2 => "dots per cm",
                                        _ => "unknown"
                                    });
                            }
                            break;
                        case (byte)JpegMarker.M_SOF0: //Start of Frame
                        case (byte)JpegMarker.M_SOF1:
                        case (byte)JpegMarker.M_SOF2:
                        case (byte)JpegMarker.M_SOF3:
                        case (byte)JpegMarker.M_SOF5:
                        case (byte)JpegMarker.M_SOF6:
                        case (byte)JpegMarker.M_SOF7:
                        case (byte)JpegMarker.M_SOF9:
                        case (byte)JpegMarker.M_SOF10:
                        case (byte)JpegMarker.M_SOF11:
                        case (byte)JpegMarker.M_SOF13:
                        case (byte)JpegMarker.M_SOF14:
                        case (byte)JpegMarker.M_SOF15:
                            if (foundSOF) { drop = true; i = i + frameLength + 4; Warning("Multiple SOF markers"); }
                            else foundSOF = true;

                            if (_verbose) Console.Write(" ∙ Size: {0} x {1}; {2} component(s); {3} bits per sample. {4} coding.",
                                          ((uint)file[buf + 3] << 8) + file[buf + 4], //width
                                          ((uint)file[buf + 1] << 8) + file[buf + 2], //height
                                          file[buf + 5], file[buf],
                                          file[i + 1] switch
                                          {
                                              (byte)JpegMarker.M_SOF0 => "Baseline",
                                              (byte)JpegMarker.M_SOF1 => "Extended sequential, Huffman",
                                              (byte)JpegMarker.M_SOF2 => "Progressive, Huffman",
                                              (byte)JpegMarker.M_SOF3 => "Lossless, Huffman",
                                              (byte)JpegMarker.M_SOF5 => "Differential sequential, Huffman",
                                              (byte)JpegMarker.M_SOF6 => "Differential progressive, Huffman",
                                              (byte)JpegMarker.M_SOF7 => "Differential lossless, Huffman",
                                              (byte)JpegMarker.M_SOF9 => "Extended sequential, arithmetic",
                                              (byte)JpegMarker.M_SOF10 => "Progressive, arithmetic",
                                              (byte)JpegMarker.M_SOF11 => "Lossless, arithmetic",
                                              (byte)JpegMarker.M_SOF13 => "Differential sequential, arithmetic",
                                              (byte)JpegMarker.M_SOF14 => "Differential progressive, arithmetic",
                                              (byte)JpegMarker.M_SOF15 => "Differential lossless, arithmetic",
                                              _ => "Unknown"
                                          });

                            if (frameLength != 6 + file[buf + 5] * 3) Warning("Incorrect SOF marker length");
                            break;
                        case (byte)JpegMarker.M_APP8: Warning("SPIFF header removed"); goto case (byte)JpegMarker.M_APP15;
                        case (byte)JpegMarker.M_COM: //kill comments
                        case (byte)JpegMarker.M_APP1:
                        case (byte)JpegMarker.M_APP2:
                        case (byte)JpegMarker.M_APP3:
                        case (byte)JpegMarker.M_APP4:
                        case (byte)JpegMarker.M_APP5:
                        case (byte)JpegMarker.M_APP6:
                        case (byte)JpegMarker.M_APP7:
                        case (byte)JpegMarker.M_APP9:
                        case (byte)JpegMarker.M_APP10:
                        case (byte)JpegMarker.M_APP11:
                        case (byte)JpegMarker.M_APP12:
                        case (byte)JpegMarker.M_APP13: //Adobe
                        case (byte)JpegMarker.M_APP15: drop = true; i = i + frameLength + 4; break;
                        case (byte)JpegMarker.M_APP14:
                            drop = true; i = i + frameLength + 4;
                            if (_verbose && Encoding.ASCII.GetString(file, buf + 5, 5) == "Adobe")
                                Console.Write(" ∙ Adobe v.{0}; flags: {1}, {2}; colorspace: {3}.",
                                        ((uint)file[buf + 5] << 8) + file[buf + 6],
                                        ((uint)file[buf + 7] << 8) + file[buf + 8],
                                        ((uint)file[buf + 9] << 8) + file[buf + 10],
                                        file[buf + 11] switch
                                        {
                                            0 => "Grayscale",
                                            1 => "YCbCr (YUV)",
                                            2 => "YCbCrK (CMYK)",
                                            _ => "Unknown"
                                        });
                            break;
                        case (byte)JpegMarker.M_DRI: if (frameLength != 2) Warning("Incorrect DRI marker length"); break;
                        case (byte)JpegMarker.M_SOS: //Only FF00 are data, others - markers
                            if (!foundSOF) Warning("There`s no SOF marker before scan");
                            if (frameLength != 4 + file[buf] * 2) Warning("Incorrect SOS marker length");
                            fs.Write(file, i, 4 + frameLength); i = i + 4 + frameLength; drop = true;

                            var loop = false;
                            do
                            {
                                for (; i < file.Length && file[i] != 0xFF; i++) fs.WriteByte(file[i]);
                                for (; i < file.Length && file[i] == 0xFF; i++) { }

                                if (i >= file.Length) continue;
                                if (file[i] == 0)
                                {
                                    fs.WriteByte(file[i - 1]);
                                    loop = true;
                                }
                                else
                                    switch (file[i])
                                    {
                                        case (byte)JpegMarker.M_RST0: // these are all parameterless
                                        case (byte)JpegMarker.M_RST1:
                                        case (byte)JpegMarker.M_RST2:
                                        case (byte)JpegMarker.M_RST3:
                                        case (byte)JpegMarker.M_RST4:
                                        case (byte)JpegMarker.M_RST5:
                                        case (byte)JpegMarker.M_RST6:
                                        case (byte)JpegMarker.M_RST7:
                                            fs.Write(file, i - 1, 2);
                                            i++; loop = true; break;
                                        case (byte)JpegMarker.M_TEM:
                                            Warning("TEM marker removed from scan");
                                            i++; loop = true; break;
                                        default: i--; loop = false; break;
                                    }
                            } while (i < file.Length && loop);
                            break;
                        case (byte)JpegMarker.M_EOI:
                            drop = true;
                            if (!foundEOI) fs.Write(file, i, 2);
                            if (file.Length > i + 2)
                                if (_ignore) MinorWarning("There`s something after EOI");
                                else Warning("There`s something after EOI");
                            i = file.Length; foundEOI = true; break;
                        case (byte)JpegMarker.M_DNL:
                            Warning("DNL marker removed");
                            drop = true; i = i + frameLength + 4; break;
                        case (byte)JpegMarker.M_JPG:  //JPEG part 3 extensions
                        case (byte)JpegMarker.M_JPG0:
                        case (byte)JpegMarker.M_JPG13:
                        case (byte)JpegMarker.M_DHP:
                        case (byte)JpegMarker.M_EXP:
                            Warning("JPEG part 3 marker removed");
                            drop = true; i = i + frameLength + 4; break;
                        case 0:     // it`s not a marker but a bug!
                        case (byte)JpegMarker.M_TEM: //these are all parameterless
                        case (byte)JpegMarker.M_SOI:
                        case (byte)JpegMarker.M_RST0:
                        case (byte)JpegMarker.M_RST1:
                        case (byte)JpegMarker.M_RST2:
                        case (byte)JpegMarker.M_RST3:
                        case (byte)JpegMarker.M_RST4:
                        case (byte)JpegMarker.M_RST5:
                        case (byte)JpegMarker.M_RST6:
                        case (byte)JpegMarker.M_RST7:
                            drop = true; i += 2;
                            if (file.Length > i) Warning("Unexpected marker removed"); break;
                        default: Warning("Unknown marker removed"); drop = true; break;
                    } //end of markers switching

                    if (drop) continue;
                    fs.Write(file, i, frameLength + 4);
                    i = i + frameLength + 4;
                } while (i < file.Length); //end markers parsing

                fs.Close();
                if (!foundEOI) Warning("Unexpected end of file");

                var iF = new FileInfo(path);
                var oF = new FileInfo(outfile);

                if (!_warningRaised)
                {
                    var bytesRemoved = iF.Length - oF.Length;
                    _rippedBytesTotal += bytesRemoved;
                    Console.WriteLine($" ∙ {bytesRemoved} bytes removed ∙ {bytesRemoved * 100L / iF.Length}%");

                    if (!_replace) return;
                    iF.Attributes = FileAttributes.Normal;
                    iF.Delete(); oF.MoveTo(path);
                }
                else if (_kill)
                {
                    iF.Attributes = FileAttributes.Normal;
                    iF.Delete();
                }
            }
        }
    }
}