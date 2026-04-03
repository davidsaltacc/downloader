using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Downloader.Utils
{
    internal abstract class YtDlpApi
    {

        public static async Task<string> DownloadSong(Song song, string downloadUrl, string folder, Action<int> onProgressUpdate, string? uniqueSongIdentifier)
        {
            
            var fullFilePath = Path.Join(folder, uniqueSongIdentifier != null ? Helpers.SafeFileName(uniqueSongIdentifier) : Helpers.SafeFileName(String.Join(", ", song.Artists) + " - " + song.Title)).Replace("\\", "/");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "yt-dlp.exe",
                    WorkingDirectory = Environment.CurrentDirectory,
                    Arguments = $"--no-simulate --quiet --no-warnings --no-js-runtimes --js-runtimes quickjs --no-part --newline --progress --print filename -o \"{fullFilePath}.%(ext)s\" -f \"ba/b\" \"{downloadUrl}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            var filename = "";

            while (true)
            {
                var line = await process.StandardOutput.ReadLineAsync();
                if (line == null)
                {
                    break;
                }
                var data = Regex.Replace(line, @"\s+", " ").Split(" ");
                if (!float.TryParse(data[0].Replace("%", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
                {
                    if (filename.Length == 0)
                    {
                        filename = line.Trim();
                    }
                    continue;
                }
                onProgressUpdate((int) Math.Round(percent));
            }

            await process.WaitForExitAsync();

            return fullFilePath + "." + filename.Split(".").Last();

        }

    }
}
