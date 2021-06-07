using System;
using System.IO;

namespace MPEGrip
{
    // ReSharper disable once InconsistentNaming
    internal static class MPEGrip
    {
        private static byte[] _file; static ulong _i;
        private static bool _croptag, _discard, _recurse, _replace = true, _skipDamaged;
        private static readonly ConsoleColor DefaultColor = Console.ForegroundColor;

        // ReSharper disable InconsistentNaming
        private static readonly uint[] br_m1l1 = { 0, 32, 64, 96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448, 0 };
        private static readonly uint[] br_m1l2 = { 0, 32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384, 0 };
        private static readonly uint[] br_m1l3 = { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0 };
        private static readonly uint[] br_m2l1 = { 0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256, 0 };
        private static readonly uint[] br_m2l2 = { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0 };
        // ReSharper restore InconsistentNaming

        private static void Info(string info)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("INFO: ");
            Console.ForegroundColor = DefaultColor;
            Console.WriteLine(info);
        }

        private static void NewLineInfo(string info)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("\nINFO: ");
            Console.ForegroundColor = DefaultColor;
            Console.Write(info);
        }

        private static void Error(string e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERROR: " + e);
            Console.ForegroundColor = DefaultColor;
        }

        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine(
@"MPEGRip. Release 7.4
Supported: MPEG 1.0 & 2.0; VBR & CBR. Note: Layer I wasn't tested

MPEGRip is utility for ripping digitally empty frames from audio MPEG`s.
MPEGRip will generate cleaned files with .cut extension, and then replace original files by cleaned ones.

Usage: MPEGRip.exe path [switches]
Switches:
    /A - Always discard incomplete last frame
         [By default last frame discarded if it is smaller than 3/4 of the proper size]
    /C - Remove ID3v1 tag. ID3v2 & APE tags will be removed anyway
    /D - Don`t replace original files with cleaned
    /R - Recurse subdirectories
    /S - Skip damaged frames

You can use wildcards in file names, or simply provide path to an directory and all of files there will be processed.");
                return;
            }

            foreach (var s in args)
            {
                if (s.Length == 2 && s[0] == '/')
                    switch (s[1])
                    {
                        case 'A':
                        case 'a': _discard = true; break;
                        case 'C':
                        case 'c': _croptag = true; break;
                        case 'D':
                        case 'd': _replace = false; break;
                        case 'R':
                        case 'r': _recurse = true; break;
                        case 'S':
                        case 's': _skipDamaged = true; break;
                        default: Error("Unknown switch: " + s); return;
                    }
            }

            string path = args[0],
                   fileFilter = Path.GetFileName(path),
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

            Console.WriteLine("\nDone.");
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
            ulong ripped = 0;
            long id3V2Size = 0, apeSizeB = 0, apeSizeE = 0;
            var hasId3V1Tag = false;
            var id3V1 = new byte[128];
            var id3V2 = new byte[10];
            var apeTag = new byte[32];

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\nScanning \"{0}\"\n", path);
            Console.ForegroundColor = DefaultColor;

            FileStream fs;
            try { fs = File.OpenRead(path); }
            catch (UnauthorizedAccessException) { Error($"Insufficient privileges to access \"{path}\""); return; }

            if (fs.Length < id3V1.Length + apeTag.Length + 2) { Error("File too short"); return; }

            fs.Position = fs.Length - id3V1.Length;
            fs.Read(id3V1, 0, id3V1.Length);
            if (id3V1[0] == 'T' && id3V1[1] == 'A' && id3V1[2] == 'G')
            {
                hasId3V1Tag = true;
                var s = System.Text.Encoding.Default.GetString(id3V1);
                Info("ID3v1 tag found");
                Info("Artist - " + s.Substring(33, 30));
                Info(" Album - " + s.Substring(63, 30));
                Info("  Year - " + s.Substring(93, 4));
                Info(" Title - " + s.Substring(3, 30) + "\n");
            }

            fs.Position = fs.Length - (hasId3V1Tag ? id3V1.Length : 0) - apeTag.Length;
            fs.Read(apeTag, 0, apeTag.Length);
            if (apeTag[0] == 'A' && apeTag[1] == 'P' && apeTag[2] == 'E' && apeTag[3] == 'T' && apeTag[4] == 'A' && apeTag[5] == 'G' && apeTag[6] == 'E' && apeTag[7] == 'X')
            {
                apeSizeE = (((apeSizeE + apeTag[15] << 8) + apeTag[14] << 8) + apeTag[13] << 8) + apeTag[12] + 32L * (apeTag[23] >> 7);
                if (fs.Length - (hasId3V1Tag ? id3V1.Length : 0) <= apeSizeE) { Error("Invalid length in APE tag footer"); return; }
                Info($"APE tag found at the end of file & ignored, version was {(((apeTag[11] << 8) + apeTag[10] << 8) + apeTag[9] << 8) + apeTag[8]}, size was {apeSizeE} bytes\n");
            }

            fs.Position = 0; fs.Read(id3V2, 0, id3V2.Length);
            if (id3V2[0] == 'I' && id3V2[1] == 'D' && id3V2[2] == '3')
            {
                id3V2Size = (((id3V2Size + id3V2[6] << 7) + id3V2[7] << 7) + id3V2[8] << 7) + id3V2[9] + 10L + ((id3V2[5] & 64) == 64 ? 10L : 0L);
                if (fs.Length <= id3V2Size) { Error("Invalid length in ID3 header"); return; }
                Info($"ID3v2 tag found & ignored, version was 2.{id3V2[3]}.{id3V2[4]}, size was {id3V2Size} bytes\n");
            }

            fs.Position = id3V2Size;

            fs.Read(apeTag, 0, apeTag.Length);
            if (apeTag[0] == 'A' && apeTag[1] == 'P' && apeTag[2] == 'E' && apeTag[3] == 'T' && apeTag[4] == 'A' && apeTag[5] == 'G' && apeTag[6] == 'E' && apeTag[7] == 'X')
            {
                apeSizeB = (((apeSizeB + apeTag[15] << 8) + apeTag[14] << 8) + apeTag[13] << 8) + apeTag[12] + 32L * (apeTag[23] >> 7);
                if (fs.Length - id3V2Size - apeSizeE - (hasId3V1Tag ? id3V1.Length : 0) <= apeSizeB) { Error("Invalid length in APE tag header"); return; }
                Info($"APE tag found at the beginning of file & ignored, version was {(((apeTag[11] << 8) + apeTag[10] << 8) + apeTag[9] << 8) + apeTag[8]}, size was {apeSizeB} bytes\n");
            }

            fs.Position = id3V2Size + apeSizeB;

            _file = new byte[fs.Length - id3V2Size - apeSizeB - apeSizeE - (hasId3V1Tag ? id3V1.Length : 0)];
            fs.Read(_file, 0, _file.Length); fs.Close(); _i = 0;

            if (!FoundNextFrame()) { Error("No frames found"); return; }

            var outfile = Path.ChangeExtension(path, ".cut");
            if (File.Exists(outfile)) { Error($"\"{outfile}\" already exists"); return; }
            fs = File.OpenWrite(outfile);

            for (long frameCount = 1; FoundNextFrame(); frameCount++)
            {
                byte ps = 1;
                var damaged = false;
                ulong lameCount = 0, emptyCount = 0, spf = 14400;

                if (_i + 2 > (ulong)_file.Length - 1) _i = (ulong)_file.Length;
                else
                {
                    ulong sf;
                    switch ((_file[_i + 2] & 12) >> 2)
                    {
                        case 0: sf = 4410; break;
                        case 1: sf = 4800; break;
                        case 2: sf = 3200; break;
                        default:
                            damaged = true;
                            Console.WriteLine();
                            Error($"Damaged frame. Signature: {_file[_i]:X} {_file[_i + 1]:X} {_file[_i + 2]:X}");
                            if (!_skipDamaged) return;
                            Error("Skipping...");
                            sf = 4410; break;
                    }

                    ulong bi;
                    switch ((_file[_i + 1] & 24) >> 3)
                    {
                        case 3: //MPEG 1.0
                            switch ((_file[_i + 1] & 6) >> 1)
                            {
                                case 1: bi = br_m1l3[(_file[_i + 2] & 0xF0) >> 4]; break; //Layer III
                                case 2: bi = br_m1l2[(_file[_i + 2] & 0xF0) >> 4]; break; //Layer II
                                case 3: //Layer I
                                    bi = br_m1l1[(_file[_i + 2] & 0xF0) >> 4];
                                    spf = 4800; ps = 4; break;
                                default: bi = 0; break;
                            }
                            break;
                        case 2: //MPEG 2.0
                            sf /= 2;
                            switch ((_file[_i + 1] & 6) >> 1)
                            {
                                case 1: spf = 7200; goto case 2; //Layer III, bitrates are the same as br_m2l2
                                case 2: bi = br_m2l2[(_file[_i + 2] & 0xF0) >> 4]; break; //Layer II
                                case 3: //Layer I
                                    bi = br_m2l1[(_file[_i + 2] & 0xF0) >> 4];
                                    spf = 4800; ps = 4; break;
                                default: bi = 0; break;
                            }
                            break;
                        default: //MPEG 2.5
                            damaged = true;
                            Console.WriteLine();
                            Error("Unsupported or badly damaged stream");
                            if (!_skipDamaged) return;
                            Error("Skipping..."); bi = 0; break;
                    }

                    var frameSize = spf * bi / sf + (ulong)((_file[_i + 2] & 2) >> 1) * ps;
                    if (frameSize < 13 || damaged)
                    {
                        Console.WriteLine();
                        Error($"Damaged frame. Signature: {_file[_i]:X} {_file[_i + 1]:X} {_file[_i + 2]:X}");

                        if (!_skipDamaged) return;
                        Error("Skipping...");
                        _i += 2; ripped++;
                    }
                    else
                    {
                        Console.Write($"\rProcessing frame: {frameCount} - size: {frameSize}");
                        var tb = _file[_i + 2] == 0x90 ? (byte)0xFF : (byte)0x00;
                        ulong n;

                        if (_i + frameSize > (ulong)_file.Length)
                        {
                            if ((ulong)_file.Length - _i > frameSize * 3 / 4 && !_discard)
                            {
                                fs.Write(_file, (int)_i, _file.Length - (int)_i);
                                for (n = 0; n < _i + frameSize - (ulong)_file.Length; n++) fs.WriteByte(tb);
                                NewLineInfo("Last frame corrected");
                            }
                            else NewLineInfo("Last frame discarded");

                            _i = (ulong)_file.Length;
                        }
                        else
                        {
                            if ((_file[_i + 3] & 192) >> 6 != 3 && (_file[_i + 1] & 24) >> 3 == 3) n = 36;
                            else
                                if ((_file[_i + 3] & 192) >> 6 == 3 && (_file[_i + 1] & 24) >> 3 != 3) n = 13; else n = 21;

                            for (; n < frameSize; n++)
                                switch (_file[_i + n])
                                {
                                    case 0xFF:
                                    case 0x00: emptyCount++; break;
                                    case 0x55:
                                    case 0xAA: lameCount++; break;
                                }

                            if (emptyCount * 100 / frameSize > 90 || lameCount * 100 / frameSize > 54) { _i += frameSize; ripped++; }
                            else { fs.Write(_file, (int)_i, (int)frameSize); _i += frameSize; }
                        }
                    }
                }
            }

            if (hasId3V1Tag && !_croptag)
            {
                fs.Write(id3V1, 0, id3V1.Length);
                NewLineInfo("ID3v1 tag appended");
            }

            fs.Close();
            var iF = new FileInfo(path);
            var oF = new FileInfo(outfile);
            NewLineInfo($"Ripped {ripped} empty frame{(ripped != 1 ? "s" : "")} ∙ {(iF.Length - oF.Length) / 1024L}Kb ∙ {(iF.Length - oF.Length) * 100L / iF.Length}%\n");

            if (!_replace) return;
            iF.Attributes = FileAttributes.Normal;
            iF.Delete(); oF.MoveTo(path);
        }

        private static bool FoundNextFrame()
        {
            //search for FF & synchroword
            for (; (int)_i < _file.Length - 1 && (_file[_i] != 0xFF || (_file[_i + 1] & 0xE0) != 0xE0); _i++) { }
            return !((int)_i >= _file.Length - 1);
        }
    }
}