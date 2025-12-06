using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SharpCompress.Archives.Zip;
using TagLib;
using TagLib.Id3v2;
using File = System.IO.File;
using Tag = TagLib.Id3v2.Tag;

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

        public static void ExtractFileFromZipArchive(string archiveFile, string targetFile, string extractFolder)
        {
            using var archive = ZipArchive.Open(archiveFile);
            ExtractFileFromArchive(archive, targetFile, extractFolder);
        }

        public static void ExtractFileFrom7ZipArchive(string archiveFile, string targetFile, string extractFolder)
        {
            using var archive = SevenZipArchive.Open(archiveFile);
            ExtractFileFromArchive(archive, targetFile, extractFolder);
        }

        public static void ExtractAllFilesFromZipArchive(string archiveFile, string extractFolder, bool flat = false)
        {
            using var archive = ZipArchive.Open(archiveFile);
            ExtractAllFilesFromArchive(archive, extractFolder, flat);
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

        private static void ExtractAllFilesFromArchive(IArchive archive, string extractFolder, bool flat = false)
        {
            foreach (var entry in archive.Entries)
            {
                if (entry.Key == null || entry.IsDirectory)
                {
                    continue;
                }

                try
                {
                    Directory.CreateDirectory(extractFolder);
                } catch (IOException _) {}

                if (flat)
                {
                    entry.WriteToFile(Path.Combine(extractFolder, Path.GetFileName(entry.Key)), new ExtractionOptions { Overwrite = true });
                } else {
                    entry.WriteToDirectory(extractFolder, new ExtractionOptions { Overwrite = true, ExtractFullPath = true });
                }
            }
        }

        public static void ApplyId3ToFile(string file, Song song, string comment = "")
        {

            Tag.DefaultVersion = 3;
            Tag.ForceDefaultVersion = true;
            var taggedFile = TagLib.File.Create(file, Settings.AllCodecsAndMimetypes[Settings.AllCodecsAndFormats.Keys.First(s => Settings.AllCodecsAndFormats[s] == file.Split(".").Last())], ReadStyle.Average);

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
        
        public static List<KeyValuePair<float, Song>> ScoreFoundSongs(List<Song> songs, Song originalSong, bool allowTitleArtistOverlap)
        {
            List<KeyValuePair<float, Song>> scored = [];

            foreach (var song in songs)
            {
                
                var scoreBasic = 0f;
                var maxBasic = 0f;
                var scoreOverlap = 0f;
                var maxOverlap = 0f;

                scoreBasic += FuzzySharp.Process.ExtractOne(originalSong.Title, [ song.Title ], s => s).Score / 100f;
                maxBasic += 1;
                
                scoreBasic += FuzzySharp.Process.ExtractOne(originalSong.Album, [ song.Album ], s => s).Score / 100f * 0.65f;
                maxBasic += 0.65f;
                
                if (song.DurationMs > 0) {
                    scoreBasic += (15000 - Math.Abs(song.DurationMs - originalSong.DurationMs)) / 15000f;
                    maxBasic += 1f;
                }
                
                scoreBasic += song.Artists.Select(artist => FuzzySharp.Process.ExtractOne(artist, originalSong.Artists, s => s).Score).Sum() /
                         (float) Math.Max(song.Artists.Length, originalSong.Artists.Length) / 100f;
                maxBasic += 1;

                scoreOverlap += FuzzySharp.Fuzz.TokenSortRatio(String.Join(" ", song.Artists) + " " + song.Title, String.Join(" ", originalSong.Artists) + " " + originalSong.Title);
                maxOverlap += 1;

                scoreBasic /= maxBasic;
                scoreOverlap /= maxOverlap;

                var finalScore = allowTitleArtistOverlap ? (scoreOverlap * 0.3f + scoreBasic * 0.2f) : scoreBasic;
                
                scored.Add(new KeyValuePair<float, Song>( finalScore, song ));

            }

            return scored;
        }

        public static string SafeFileName(string oldName)
        {
            return Regex.Replace(oldName, @"[\\\/:\*\?""<>\|\x00-\x1F]", "_");
        }

    }
}
