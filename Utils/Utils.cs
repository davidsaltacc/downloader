using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Downloader.Utils.Songs;
using SharpCompress.Archives.Zip;
using TagLib.Id3v2;

namespace Downloader.Utils
{
    internal abstract class Utils
    {

        public static async Task<string> DownloadFile(string url, string folder)
        {

            using HttpClient client = new();
            await using var stream = await client.GetStreamAsync(url);
            var path = Path.Combine(folder, Path.GetFileName(new Uri(url).AbsolutePath));
            await using var file = File.Create(path);
            await stream.CopyToAsync(file);
            return path;

        }

        public static void ExtractFileFrom7ZipArchive(string archiveFile, string targetFile, string extractFolder)
        {
            using var archive = SevenZipArchive.Open(archiveFile);
            ExtractFileFromArchive(archive, targetFile, extractFolder);
        }

        public static void ExtractAllFilesFromZipArchive(string archiveFile, string extractFolder)
        {
            using var archive = ZipArchive.Open(archiveFile);
            ExtractAllFilesFromArchive(archive, extractFolder);
        }

        private static void ExtractFileFromArchive(IArchive archive, string targetFile, string extractFolder)
        {
            foreach (var entry in archive.Entries)
            {
                if (!entry.Key?.Replace('\\', '/').Equals(targetFile, StringComparison.OrdinalIgnoreCase) ?? true)
                {
                    continue;
                }

                var extractDir = Path.GetDirectoryName(extractFolder);
                if (extractDir != null && extractDir.Length > 0) {
                    Directory.CreateDirectory(extractDir);
                }
                entry.WriteToFile(Path.Combine(extractFolder, Path.GetFileName(targetFile)), new ExtractionOptions { Overwrite = true, ExtractFullPath = false });
                break;
            }
        }

        private static void ExtractAllFilesFromArchive(IArchive archive, string extractFolder)
        {
            foreach (var entry in archive.Entries)
            {
                if (entry.Key == null)
                {
                    continue;
                }
                var extractDir = Path.GetDirectoryName(extractFolder);
                if (extractDir != null && extractDir.Length > 0) {
                    Directory.CreateDirectory(extractDir);
                }
                entry.WriteToFile(Path.Combine(extractFolder, Path.GetFileName(entry.Key)), new ExtractionOptions { Overwrite = true, ExtractFullPath = true });
            }
        }

        public static void ApplyId3ToFile(string file, Song song, string comment = "")
        {

            Tag.DefaultVersion = 3;
            Tag.ForceDefaultVersion = true;
            var taggedFile = TagLib.File.Create(file);

            if (song.Title.Length > 0)
            {
                taggedFile.Tag.Title = song.Title;
            }
            if (song.Album.Length > 0)
            {
                taggedFile.Tag.Album = song.Album;
            }
            if (String.Join("", song.Artists).Length > 0)
            {
                taggedFile.Tag.Performers = song.Artists;
            }
            taggedFile.Tag.Comment = comment;
            if (song.IndexOnDisk != -1)
            {
                taggedFile.Tag.Track = (uint) song.IndexOnDisk;
            }
            if (song.DiskIndex != -1)
            {
                taggedFile.Tag.Disc = (uint) song.DiskIndex;
            }
            if (song.ReleaseYear != -1)
            {
                taggedFile.Tag.Year = (uint) song.ReleaseYear;
            }
            if (song.ImageUrl.Length > 0)
            {
                byte[] imageBytes;
                using (var client = new HttpClient())
                {
                    imageBytes = client.GetByteArrayAsync(song.ImageUrl).GetAwaiter().GetResult();
                }

                var cover = new AttachmentFrame
                {
                    Type = TagLib.PictureType.FrontCover,
                    Description = "Cover",
                    MimeType = System.Net.Mime.MediaTypeNames.Image.Jpeg,
                    Data = imageBytes
                };

                taggedFile.Tag.Pictures = [cover];
            }

            taggedFile.Save();

        }
        
        public static JsonNode? NavigateJsonNode(JsonNode? node, params object[] path)
        {
            foreach (var key in path) {
                node = key switch
                {
                    string s => node?[s],
                    int i => i == -1 ? node?.AsArray().Last() : node?.AsArray()[i],
                _ => null
                };
            }
            return node;
        }

    }
}
