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
    internal abstract class Helpers
    {

        public static async Task<string> DownloadFile(string url, string folder, Action<int>? progress = null, string? customFileName = null)
        {

            var path = Path.Join(folder, customFileName ?? Path.GetFileName(new Uri(url).AbsolutePath));
            await using var file = File.Create(path);
            await DownloadFileToStream(url, file, progress);
            return path;

        }

        public static async Task DownloadFileToStream(string url, FileStream fStream, Action<int>? progress = null)
        {
            
            using HttpClient client = new();

            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var canShowProgress = totalBytes > 0 && progress != null;

            await using var stream = await response.Content.ReadAsStreamAsync();
            
            var buffer = new byte[8192];
            long totalRead = 0;
            int read;

            while ((read = await stream.ReadAsync(buffer)) > 0)
            {
                await fStream.WriteAsync(buffer.AsMemory(0, read));
                totalRead += read;

                if (!canShowProgress)
                {
                    continue;
                }
                var percent = (int) (totalRead * 100L / totalBytes);
                progress!(percent);
            }
            
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
                if (extractDir != null && extractDir.Length > 0)
                {
                    Directory.CreateDirectory(extractDir);
                }

                entry.WriteToFile(Path.Combine(extractFolder, Path.GetFileName(targetFile)),
                    new ExtractionOptions { Overwrite = true, ExtractFullPath = false });
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
                }
                catch (IOException _)
                {
                }

                if (flat)
                {
                    entry.WriteToFile(Path.Combine(extractFolder, Path.GetFileName(entry.Key)),
                        new ExtractionOptions { Overwrite = true });
                }
                else
                {
                    entry.WriteToDirectory(extractFolder,
                        new ExtractionOptions { Overwrite = true, ExtractFullPath = true });
                }
            }
        }

        public static async Task ApplyId3ToFile(string file, Song song, string comment = "")
        {

            Tag.DefaultVersion = 3;
            Tag.ForceDefaultVersion = true;
            var taggedFile = TagLib.File.Create(file,
                Settings.AllCodecsAndMimetypes[await FFMpegApi.DetectAudioCodec(file)], ReadStyle.Average);

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
                taggedFile.Tag.Track = (uint)song.IndexOnDisk;
            }

            if (song.DiskIndex != -1)
            {
                taggedFile.Tag.Disc = (uint)song.DiskIndex;
            }

            if (song.ReleaseYear != -1)
            {
                taggedFile.Tag.Year = (uint)song.ReleaseYear;
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
                    Type = PictureType.FrontCover,
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
            foreach (var key in path)
            {
                node = key switch
                {
                    string s => node?[s],
                    int i => i == -1 ? node?.AsArray().Last() : node?.AsArray()[i],
                    _ => null
                };
            }

            return node;
        }

        public static List<KeyValuePair<float, Song>> ScoreFoundSongs(List<Song> songs, Song originalSong,
            bool allowTitleArtistOverlap, bool allowEarlyIndexBoost)
        {
            List<KeyValuePair<float, Song>> scored = [];

            var i = -1;
            foreach (var song in songs)
            {
                i++;
                
                var scoreBasic = 0f;
                var maxBasic = 0f;
                var scoreOverlap = 0f;
                var maxOverlap = 0f;

                scoreBasic += FuzzySharp.Process.ExtractOne(originalSong.Title, [song.Title], s => s).Score / 100f;
                maxBasic += 1;

                if (originalSong.Album.Length > 0 && song.Album.Length > 0)
                {
                    scoreBasic += FuzzySharp.Process.ExtractOne(originalSong.Album, [song.Album], s => s).Score / 100f *
                                  0.85f;
                    maxBasic += 0.85f;
                }

                if (song.DurationMs > 0)
                {
                    scoreBasic += (30000f - Math.Abs(song.DurationMs - originalSong.DurationMs)) / 30000f * 0.8f;
                    maxBasic += 0.8f;
                }

                scoreBasic += song.Artists.Select(artist =>
                                  FuzzySharp.Process.ExtractOne(artist, originalSong.Artists, s => s).Score).Sum() /
                              (float) Math.Max(song.Artists.Length, originalSong.Artists.Length) / 100f;
                maxBasic += 1;

                if (allowEarlyIndexBoost || originalSong.Album.Length == 0 || song.Album.Length == 0) // if not album, force boost high ranking results
                {
                    var x = Math.Clamp((1f - (float) i / songs.Count - 0.3f) / 0.6f, 0f, 1f); 
                    scoreBasic += 0.25f * x * x * (3 - 2 * x); // simple smoothstep because it felt rude to fully cut off after a certain percentage
                    maxBasic += 0.25f;
                }

                scoreOverlap += FuzzySharp.Fuzz.TokenSortRatio(String.Join(" ", song.Artists) + " " + song.Title,
                    String.Join(" ", originalSong.Artists) + " " + originalSong.Title);
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

        public static string InsertSubstitutionsForPath(string baseString, Song song)
        {
            
            var replacements = new Dictionary<string, string>
            {
                { "artist", (song.Artists.Length == 0 || song.Artists[0].Length == 0) ? "Unknown Artist" : song.Artists[0] },
                { "allartists", (song.Artists.Length == 0 || song.Artists[0].Length == 0) ? "Unknown Artist" : String.Join(", ", song.Artists) },
                { "title", song.Title.Length == 0 ? "Unknown Title" : song.Title },
                { "album", song.Album.Length == 0 ? "Unknown Album" : song.Album },
                { "year", song.ReleaseYear == -1 ? "Unknown Year" : song.ReleaseYear.ToString() }
            };
            
            return Regex.Replace(baseString, @"%(?:[^%]|%%)+%", match =>
            {
                var content = match.Value.Substring(1, match.Value.Length - 2);
                content = content.Replace("%%", "%");
                return SafeFileName(replacements.GetValueOrDefault(content, content));
            });
        }
    }
    
}
