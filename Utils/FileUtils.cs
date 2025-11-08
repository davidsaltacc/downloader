
using downloader.Utils.Songs;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using TagLib.Id3v2;

namespace downloader.Utils
{
    internal abstract class FileUtils
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

            foreach (var entry in archive.Entries)
            {
                if (!entry.Key?.Replace('\\', '/').Equals(targetFile, StringComparison.OrdinalIgnoreCase) ?? true)
                {
                    continue;
                }
                
                Directory.CreateDirectory(Path.GetDirectoryName(extractFolder)!);
                entry.WriteToFile(Path.Combine(extractFolder, Path.GetFileName(targetFile)), new ExtractionOptions { Overwrite = true, ExtractFullPath = false });
                break;
            }
        }

        public static void ApplyId3ToFile(string file, Song song, string comment = "")
        {

            Tag.DefaultVersion = 3;
            Tag.ForceDefaultVersion = true;
            var taggedFile = TagLib.File.Create(file);

            taggedFile.Tag.Title = song.Title;
            taggedFile.Tag.Album = song.Album;
            taggedFile.Tag.Performers = song.Artists;
            taggedFile.Tag.Comment = comment;
            taggedFile.Tag.Track = (uint) song.IndexOnDisk;
            taggedFile.Tag.Disc = (uint) song.DiskIndex;
            taggedFile.Tag.Year = (uint) song.ReleaseYear;

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

            taggedFile.Tag.Pictures = [ cover ];

            taggedFile.Save();

        }

    }
}
