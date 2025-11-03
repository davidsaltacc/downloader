
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace downloader.Utils
{
    internal class FileUtils
    {

        public static async Task<string> downloadFile(string url, string folder)
        {

            using HttpClient client = new();
            await using var stream = await client.GetStreamAsync(url);
            var path = Path.Combine(folder, Path.GetFileName(new Uri(url).AbsolutePath));
            await using var file = File.Create(path);
            await stream.CopyToAsync(file);
            return path;

        }

        public static void extractFileFrom7ZipArchive(string archiveFile, string targetFile, string extractFolder)
        {

            using var archive = SevenZipArchive.Open(archiveFile);

            foreach (var entry in archive.Entries)
            {
                if (entry.Key.Replace('\\', '/').Equals(targetFile, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(extractFolder)!);
                    } catch { }
                    entry.WriteToFile(Path.Combine(extractFolder, Path.GetFileName(targetFile)), new ExtractionOptions { Overwrite = true, ExtractFullPath = false });
                    break;
                }
            }
        }

    }
}
