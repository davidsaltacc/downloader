
using Avalonia.Controls.ApplicationLifetimes;
using downloader.Utils;
using downloader.Utils.Songs;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Downloader.Apis
{
    internal abstract class YtDlpApi
    {

        public static async Task<bool> EnsureLatestYtDlpInstalled()
        {

            bool installed = false;
            bool latest = false;

            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "yt-dlp.exe",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (process == null)
                {
                    return false;
                }
                
                var version = await process.StandardOutput.ReadLineAsync();
                await process.WaitForExitAsync();
                installed = true;

                var response = await MainWindow.HttpClient.SendAsync(new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri("https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest"),
                    Headers = {
                        { "user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.3" }
                    }
                });
                var latestTag = JsonNode.Parse(await response.Content.ReadAsStringAsync())?["tag_name"]?.ToString();

                latest = version == latestTag;

            }
            catch (System.ComponentModel.Win32Exception) { }

            return installed && latest;

        }

        public static async Task DownloadLatestYtDlp()
        {
            await FileUtils.DownloadFile("https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe", ".");
        }

        public static async Task<string> DownloadSong(YoutubeMusicSong song, string folder)
        {

            const string type = "mp3"; // mp3 or aac

            var fullFilePath = Path.Join(folder, HttpUtility.ParseQueryString(new Uri(song.YoutubeSongUrl).Query).Get("v") + "." + type).Replace("\\", "/");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "yt-dlp.exe",
                    WorkingDirectory = Environment.CurrentDirectory,
                    Arguments = $"--no-simulate --quiet --no-warnings --no-part --newline --progress -o \"{fullFilePath}\" --progress -t {type} {song.YoutubeSongUrl}",
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
                var data = Regex.Replace(line ?? "", @"\s+", " ").Split(" ");
                if (!float.TryParse(data[1].Replace("%", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
                {
                    continue;
                }
                MainWindow.SetStatusText("Downloaded " + ((int) Math.Round(percent)) + "%");
            }

            await process.WaitForExitAsync();

            return fullFilePath;

        }

    }
}
