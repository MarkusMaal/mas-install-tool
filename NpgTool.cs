using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ICSharpCode.SharpZipLib.BZip2;
using ICSharpCode.SharpZipLib.Checksum;

namespace mas_install_tool;

public abstract class NpgTool
{
    private static uint[]? _crcTable;
    public static void Pack(string dir)
    {
        var dirInfo = new DirectoryInfo(dir);
        if (!dirInfo.Exists)
        {
            Console.WriteLine("Error: Directory does not exist");
            return;
        }
        if (!File.Exists(Path.Join(dirInfo.FullName, "Thumbnail.png")))
        {
            Console.WriteLine("Error: Thumbnail file not found");
            return;
        }

        Console.WriteLine("Enumerating files");
        ;
        List<FileInfo> packFiles =
        [
            .. from file in dirInfo.EnumerateFiles()
            where !file.Name.Equals("Thumbnail.png", StringComparison.InvariantCultureIgnoreCase) && !file.Name.EndsWith(".npg", StringComparison.InvariantCultureIgnoreCase)
            select file
        ];
        var maxSize = packFiles.Max(p => p.Name.Length) + 1; // ensure to include null terminator byte
        while (maxSize % 4 != 0) maxSize++;
        var tocChunkSize = maxSize + 0xC;
        var outputName = dirInfo.Name;
        if (!outputName.EndsWith(".npg", StringComparison.InvariantCultureIgnoreCase))
        {
            outputName += ".npg";
        }
        using var outputStream = File.Open(Path.Join(dirInfo.Parent?.FullName, outputName), FileMode.Create, FileAccess.ReadWrite);
        using var thumbStream = File.OpenRead(Path.Join(dirInfo.FullName, "Thumbnail.png"));
        Console.WriteLine("Writing PNG header");
        var buffer = new byte[0x21]; // fits PNG header
        var headerLength = (uint)packFiles.Count * tocChunkSize + 8;
        thumbStream.ReadExactly(buffer);
        outputStream.Write(buffer, 0, buffer.Length);
        outputStream.Position = 0x29 + headerLength + 4;
        Console.WriteLine("Copying thumbnail");
        while (thumbStream.Position < thumbStream.Length)
            outputStream.WriteByte((byte)thumbStream.ReadByte());
        thumbStream.Close();
        outputStream.Position -= 0xC;
        foreach (var (idx, pf) in packFiles.Index())
        {
            using var stream = File.OpenRead(pf.FullName);
            using var ms = new MemoryStream();
            Console.WriteLine($"Compressing {pf.Name}");
            BZip2.Compress(stream, ms, false,  9);
            stream.Close();
            var length = ms.Length;
            var offset = outputStream.Position + 8;
            var name = pf.Name;
            ms.Position = 0;
            outputStream.Write([.. BitConverter.GetBytes((uint)ms.Length).Reverse()]);
            outputStream.Write("nPGD"u8);
            outputStream.Write(ms.ToArray());
            var crc32 = new Crc32();
            ms.Position = 0;
            crc32.Update(ms.ToArray());
            var hdrCrc = new Crc32();
            hdrCrc.Update([.. "nPGD"u8]);
            outputStream.Write([
                .. BitConverter.GetBytes(PngCrc32(ms.ToArray(), 0, (int)ms.Length, (uint)hdrCrc.Value)).Reverse()
            ]);
            ms.Close();
            var backup = outputStream.Position;
            outputStream.Position = 0x29 + 8 + tocChunkSize * idx;
            outputStream.Write(Encoding.UTF8.GetBytes(name));
            outputStream.Position = 0x29 + 8 + tocChunkSize * idx + maxSize;
            outputStream.Write(BitConverter.GetBytes((uint)length));
            outputStream.Write(BitConverter.GetBytes((uint)offset));
            outputStream.Write(BitConverter.GetBytes((uint)crc32.Value));
            outputStream.Position = backup;
        }
        outputStream.Position = 0x21;
        Console.WriteLine("Generating NGP header");
        outputStream.Write([.. BitConverter.GetBytes((uint)headerLength).Reverse()]);
        outputStream.Write("nPGH"u8);
        outputStream.Write(BitConverter.GetBytes((uint)packFiles.Count));
        outputStream.Write(BitConverter.GetBytes((uint)tocChunkSize));
        var hdrData = new byte[headerLength];
        outputStream.Position = 0x29;
        outputStream.ReadExactly(hdrData);
        var hCrc = new Crc32();
        hCrc.Update([.. "nPGH"u8]);
        outputStream.Write([
            .. BitConverter.GetBytes(PngCrc32([.. hdrData], 0, hdrData.Length, (uint)hCrc.Value)).Reverse()
        ]);
        outputStream.Seek(0, SeekOrigin.End);
        outputStream.Write(BitConverter.GetBytes((uint)0));
        outputStream.Write("IEND"u8);
        outputStream.WriteByte(0xAE);
        outputStream.WriteByte(0x42);
        outputStream.WriteByte(0x60);
        outputStream.WriteByte(0x82);
        outputStream.Close();
        Console.WriteLine("Finished!");
    }

    public static void Unpack(string npg, string output)
    {
        var outDir = new DirectoryInfo(output);
        if (!outDir.Exists) outDir.Create();
        using var inputStream = File.OpenRead(npg);
        inputStream.Position = 0x29;
        var buffer = new byte[8];
        inputStream.ReadExactly(buffer);
        var entryCount = BitConverter.ToUInt32(buffer, 0);
        var alignment = BitConverter.ToUInt32(buffer, 4);
        for (var i = 0; i < entryCount; i++)
        {
            buffer = new byte[alignment];
            inputStream.ReadExactly(buffer);
            var dataFileName = Encoding.UTF8.GetString(buffer);
            dataFileName = dataFileName[..dataFileName.IndexOf('\0')];
            Console.Write($"Reading {dataFileName}");
            var size = BitConverter.ToUInt32(buffer, (int)alignment - 12);
            var location = BitConverter.ToUInt32(buffer, (int)alignment - 8);
            var checksum = BitConverter.ToUInt32(buffer, (int)alignment - 4);
            var lastPos = inputStream.Position;
            inputStream.Position = location;
            using var ms = new MemoryStream();
            for (var j = 0; j < size; j++)
            {
                ms.WriteByte((byte)inputStream.ReadByte());
            }
            Console.Write($"\rVerifying {dataFileName}    ");
            var crc32 = new Crc32();
            crc32.Update(ms.ToArray());
            if (checksum != crc32.Value)
            {
                Console.WriteLine("\nError: Cyclic Redundancy Check failure");
                return;
            }
            Console.Write($"\rExtracting {dataFileName}");
            using var os = new FileStream(Path.Join(output, dataFileName), FileMode.Create, FileAccess.Write);
            ms.Position = 0;
            BZip2.Decompress(ms, os, false);
            Console.WriteLine();
            os.Close();
            ms.Close();
            inputStream.Position = lastPos;
        }
        inputStream.Close();
    }
    
    // Call this function with the compressed image bytes, 
    // passing in idatCrc as the last parameter
    private static uint PngCrc32(byte[] stream, int offset, int length, uint crc)
    {
        uint c;
        if(_crcTable==null){
            _crcTable=new uint[256];
            for(uint n=0;n<=255;n++){
                c = n;
                for(var k=0;k<=7;k++){
                    if((c & 1) == 1)
                        c = 0xEDB88320^((c>>1)&0x7FFFFFFF);
                    else
                        c = ((c>>1)&0x7FFFFFFF);
                }
                _crcTable[n] = c;
            }
        }
        c = crc^0xffffffff;
        var endOffset=offset+length;
        for(var i=offset;i<endOffset;i++){
            c = _crcTable[(c^stream[i]) & 255]^((c>>8)&0xFFFFFF);
        }
        return c^0xffffffff;
    }
}