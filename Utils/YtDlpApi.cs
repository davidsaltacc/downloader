using Downloader.Utils.Songs;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Downloader.Utils
{
    internal abstract class YtDlpApi
    {

        public static async Task<string> DownloadSong(YoutubeMusicSong song, string folder, Action<int> onProgressUpdate)
        {

            const string type = "mp3"; // mp3 or aac TODO make choosable in ui

            var fullFilePath = Path.Join(folder, HttpUtility.ParseQueryString(new Uri(song.YoutubeSongUrl).Query).Get("v") + "." + type).Replace("\\", "/");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "yt-dlp.exe",
                    WorkingDirectory = Environment.CurrentDirectory,
                    Arguments = $"--no-simulate --quiet --no-warnings --no-js-runtimes --js-runtimes quickjs --no-part --newline --progress -o \"{fullFilePath}\" --progress -t {type} {song.YoutubeSongUrl}",
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

            return fullFilePath;

        }

    }
}
