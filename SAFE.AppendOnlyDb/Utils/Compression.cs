using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace SAFE.AppendOnlyDb.Utils
{
    public static class CompressionHelper
    {
        public static List<byte> Compress(this List<byte> data)
         => data.ToArray().Compress().ToList();

        public static List<byte> Decompress(this List<byte> data)
         => data.ToArray().Decompress().ToList();

        public static byte[] Compress(this byte[] data)
        {
            byte[] compressedArray = null;
            try
            {
                using (var memoryStream = new MemoryStream())
                {
                    using (var deflateStream = new DeflateStream(memoryStream, CompressionMode.Compress))
                    {
                        deflateStream.Write(data, 0, data.Length);
                    }
                    compressedArray = memoryStream.ToArray();
                }
            }
            catch (Exception ex)
            {
                // do something !
                throw ex;
            }
            return compressedArray;
        }

        public static byte[] Decompress(this byte[] data)
        {
            byte[] decompressedArray = null;
            try
            {
                using (var decompressedStream = new MemoryStream())
                {
                    using (var compressStream = new MemoryStream(data))
                    using (var deflateStream = new DeflateStream(compressStream, CompressionMode.Decompress))
                    {
                        deflateStream.CopyTo(decompressedStream);
                    }
                    decompressedArray = decompressedStream.ToArray();
                }
            }
            catch (InvalidDataException ex) when (ex.HResult == -2146233087) { return data; };
            // "The archive entry was compressed using an unsupported compression method."
            // i.e. it was never compressed to start with.
            
            //catch
            //{
            //    // do something !
            //}

            return decompressedArray;
        }
    }
}