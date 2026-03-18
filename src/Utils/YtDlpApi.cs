using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Downloader.Utils
{
    internal abstract class YtDlpApi
    {

        public static async Task<string> DownloadSong(Song song, string folder, Action<int> onProgressUpdate, string? uniqueSongIdentifier)
        {
            
            var fullFilePath = Path.Join(folder, (uniqueSongIdentifier != null ? Helpers.SafeFileName(uniqueSongIdentifier) : Helpers.SafeFileName(String.Join(", ", song.Artists) + " - " + song.Title))).Replace("\\", "/");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "yt-dlp.exe",
                    WorkingDirectory = Environment.CurrentDirectory,
                    Arguments = $"--no-simulate --quiet --no-warnings --js-runtimes deno.exe --no-part --newline --progress -o \"{fullFilePath}\" -x -f \"ba/b\" --audio-format {Settings.Codec} {song.SongUrl}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            while (true)
            {
                var line = await process.StandardOutput.ReadLineAsync();
                if (line == null)
                {
                    break;
                }
                var data = Regex.Replace(line, @"\s+", " ").Split(" ");
                if (!float.TryParse(data[1].Replace("%", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
                {
                    continue;
                }
                onProgressUpdate((int) Math.Round(percent));
            }

            await process.WaitForExitAsync();

            return fullFilePath + "." + Settings.AllCodecsAndFormats[Settings.Codec];

        }

    }
}
