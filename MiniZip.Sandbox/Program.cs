﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Knapcode.MiniZip.Sandbox
{
    class Program
    {
        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            var bclTestData = @"C:\Users\joelv\Downloads\System.IO.Compression.TestData.1.0.6-prerelease\content\ZipTestData";
            var sharpZipLibTestData = @"C:\Users\joelv\Downloads\SharpZipLib-TestData";

            var testZips = new string[]
            {
                bclTestData,
                sharpZipLibTestData,
                @"E:\data\nuget.org\top5000",
                @"C:\Users\joelv\.nuget\packages",
                //@"C:\Users\joelv\.nuget\packages\System.Runtime.InteropServices\4.4.0-beta-24903-02"
            }.SelectMany(p => Directory.EnumerateFiles(
                p,
                 "*.nupkg",
                SearchOption.AllDirectories).Concat(Directory.EnumerateFiles(
                p,
                 "*.zip",
                SearchOption.AllDirectories)))
            // .Where(x => x.Contains(@"system.runtime.interopservices.4.4.0-beta-24903-02.nupkg"))
            ;

            foreach (var p in Directory.EnumerateFiles(".", "*.zip"))
            {
                File.Delete(p);
            }

            using (var httpClient = new HttpClient())
            {
                foreach (var file in testZips)
                {
                    // Console.WriteLine(file);
                    var outcomes = new List<object>();
                    var sharpZipLibErrors = new List<string>();

                    if (false)
                    {
                        try
                        {
                            var count = await ReadUsingBclAsync(httpClient, file);
                            // Console.WriteLine("  System.IO.Compression:       " + count);
                            outcomes.Add(count);
                        }
                        catch (Exception e)
                        {
                            // Console.WriteLine("  System.IO.Compression error: " + e.Message);
                            outcomes.Add(false);
                        }
                    }

                    if (false)
                    {
                        try
                        {
                            var count = await ReadUsingSharpZipLibBufferedAsync(httpClient, file);
                            // Console.WriteLine("  SharpZipLib (buffer):        " + count);
                            outcomes.Add(count);
                        }
                        catch (Exception e)
                        {
                            // Console.WriteLine("  SharpZipLib (buffer) error:  " + e.Message);
                            outcomes.Add(false);
                            sharpZipLibErrors.Add(e.Message + "/" + e.Source + "/" + e.GetType().FullName);
                        }
                    }

                    if (false)
                    {
                        try
                        {
                            var count = await ReadUsingSharpZipLib(httpClient, file);
                            // Console.WriteLine("  SharpZipLib:                 " + count);
                            outcomes.Add(count);
                        }
                        catch (Exception e)
                        {
                            // Console.WriteLine("  SharpZipLib error:           " + e.Message);
                            outcomes.Add(false);
                            sharpZipLibErrors.Add(e.Message + "/" + e.Source + "/" + e.GetType().FullName);
                        }
                    }

                    if (false)
                    {
                        try
                        {
                            var count = await ReadUsingDotNetZipAsync(httpClient, file);
                            // Console.WriteLine("  DotNetZip:                   " + count);
                            outcomes.Add(count);
                        }
                        catch (Exception e)
                        {
                            // Console.WriteLine("  DotNetZip error:             " + e.Message);
                            outcomes.Add(false);
                        }
                    }

                    if (true)
                    {
                        try
                        {
                            var zipDirectory = await ReadUsingMiniZipAsync(httpClient, file);
                            var count = zipDirectory.Entries.Count;
                            // Console.WriteLine("  MiniZip:                     " + count);
                            outcomes.Add(JsonConvert.SerializeObject(zipDirectory));
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(file);
                            Console.WriteLine("  MiniZip:                     " + e.Message);
                            outcomes.Add(false);
                        }
                    }

                    if (true)
                    {
                        try
                        {
                            var zipDirectory = await ReadUsingMiniZipBufferedAsync(httpClient, file);
                            var count = zipDirectory.Entries.Count;
                            // Console.WriteLine("  MiniZip (buffer):            " + count);
                            outcomes.Add(JsonConvert.SerializeObject(zipDirectory));
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(file);
                            Console.WriteLine("  MiniZip (buffer) error:      " + e.Message);
                            outcomes.Add(false);
                        }
                    }

                    // Console.WriteLine("  Same behavior:               " + (outcomes.Distinct().Count() == 1));
                    // Console.WriteLine("  Same errors:                 " + (sharpZipLibErrors.Distinct().Count() <= 1));

                    if (outcomes.Distinct().Count() > 1 || sharpZipLibErrors.Distinct().Count() > 1)
                    {
                        Console.WriteLine(file);
                    }

                    // Console.WriteLine();
                }
            }
        }

        private static async Task<long> ReadUsingBclAsync(HttpClient client, string file)
        {
            using (var stream = await GetFileStreamAsync(client, file))
            using (var zipArchive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                return zipArchive.Entries.Count;
            }
        }

        private static async Task<long> ReadUsingSharpZipLib(HttpClient client, string file)
        {
            using (var stream = await GetFileStreamAsync(client, file))
            using (var bufferedStream = new BufferedStream(stream, bufferSize: 0x20000))
            using (var zipArchive = new ICSharpCode.SharpZipLib.Zip.ZipFile(bufferedStream))
            {
                return zipArchive.Count;
            }
        }

        private static async Task<FileStream> GetFileStreamAsync(HttpClient client, string file)
        {
            string TempPath = $"{Guid.NewGuid()}.zip";
            if (IsUrl(file))
            {
                var tempStream = new FileStream(TempPath, FileMode.Create, FileAccess.ReadWrite);

                using (var networkStream = await client.GetStreamAsync(file))
                {
                    await networkStream.CopyToAsync(tempStream);
                }

                tempStream.Position = 0;

                return tempStream;
            }

            return new FileStream(file, FileMode.Open);
        }

        private static bool IsUrl(string file)
        {
            return file.StartsWith("http://") || file.StartsWith("https://");
        }

        private static async Task<long> ReadUsingSharpZipLibBufferedAsync(HttpClient httpClient, string file)
        {
            long entryCount;
            using (var stream = await GetBufferedRangeStreamAsync(httpClient, file))
            using (var zipArchive = new ICSharpCode.SharpZipLib.Zip.ZipFile(stream))
            {
                entryCount = zipArchive.Count;
            }

            return entryCount;
        }

        private static async Task<ZipDirectory> ReadUsingMiniZipAsync(HttpClient httpClient, string file)
        {

            long entryCount;
            using (var stream = await GetFileStreamAsync(httpClient, file))
            using (var zipArchive = new ZipDirectoryReader(stream))
            {
                return await zipArchive.ReadAsync();
            }
        }

        private static async Task<ZipDirectory> ReadUsingMiniZipBufferedAsync(HttpClient httpClient, string file)
        {

            long entryCount;
            using (var stream = await GetBufferedRangeStreamAsync(httpClient, file))
            using (var zipArchive = new ZipDirectoryReader(stream))
            {
                return await zipArchive.ReadAsync();
            }
        }

        private static async Task<BufferedRangeStream> GetBufferedRangeStreamAsync(HttpClient httpClient, string file)
        {
            long length;
            int readCount = 0;

            ReadRangeAsync readRangeAsync;

            if (IsUrl(file))
            {
                using (var request = new HttpRequestMessage(HttpMethod.Head, file))
                using (var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead))
                {
                    response.EnsureSuccessStatusCode();
                    length = response.Content.Headers.ContentLength.Value;
                }

                readRangeAsync = async (buffer, offset, count) =>
                {
                    // Console.WriteLine($"Read HTTP offset={offset} count={count}");

                    using (var request = new HttpRequestMessage(HttpMethod.Get, file))
                    {
                        request.Headers.Range = new RangeHeaderValue(offset, (offset + count) - 1);
                        using (var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead))
                        {
                            response.EnsureSuccessStatusCode();
                            readCount++;

                            using (var stream = await response.Content.ReadAsStreamAsync())
                            {
                                var currentCount = count;
                                var currentOffset = 0;

                                int read;
                                do
                                {
                                    read = await stream.ReadAsync(buffer, 0, currentCount);
                                    currentOffset += read;
                                    currentCount -= read;
                                }
                                while (read > 0 && currentCount > 0);

                                return currentOffset;
                            }
                        }
                    }
                };
            }
            else
            {
                length = new FileInfo(file).Length;

                readRangeAsync = async (buffer, offset, count) =>
                {
                    // Console.WriteLine($"Read DISK offset={offset} count={count}");

                    using (var stream = new FileStream(file, FileMode.Open))
                    {
                        stream.Position = offset;

                        var currentCount = count;
                        var currentOffset = 0;

                        int read;
                        do
                        {
                            read = await stream.ReadAsync(buffer, 0, currentCount);
                            currentOffset += read;
                            currentCount -= read;
                        }
                        while (read > 0 && currentCount > 0);

                        return currentOffset;
                    }
                };
            }

            return new BufferedRangeStream(
                readRangeAsync,
                length,
                new ZipBufferSizeProvider(4096, 4096, 2));
        }

        private static async Task<long> ReadUsingDotNetZipAsync(HttpClient client, string file)
        {
            string realFile;
            using (var stream = await GetFileStreamAsync(client, file))
            {
                realFile = stream.Name;
            }

            using (var zipArchive = new Ionic.Zip.ZipFile(realFile))
            {
                return zipArchive.Count;
            }
        }
    }
}