
using downloader.Utils.Songs;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using TagLib.Id3v2;

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

        public static void applyID3ToFile(string file, Song song, string description = "")
        {

            Tag.DefaultVersion = 3;
            Tag.ForceDefaultVersion = true;
            var tfile = TagLib.File.Create(file);

            tfile.Tag.Title = song.Title;
            tfile.Tag.Album = song.Album;
            tfile.Tag.Performers = song.Artists;
            tfile.Tag.Description = "";
            tfile.Tag.Track = (uint) song.indexOnDisk;
            tfile.Tag.Disc = (uint) song.diskIndex;
            tfile.Tag.Year = (uint) song.releaseYear;

            byte[] imageBytes;
            using (HttpClient client = new HttpClient())
            {
                imageBytes = client.GetByteArrayAsync(song.imageUrl).GetAwaiter().GetResult();
            }

            AttachmentFrame cover = new AttachmentFrame
            {
                Type = TagLib.PictureType.FrontCover,
                Description = "Cover",
                MimeType = System.Net.Mime.MediaTypeNames.Image.Jpeg,
                Data = imageBytes
            };

            tfile.Tag.Pictures = [ cover ];

            tfile.Save();

        }

    }
}
