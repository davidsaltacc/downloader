
using downloader.Utils;
using downloader.Utils.Songs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Downloader.Apis
{
    internal class YtDlpApi
    {

        public static async Task<bool> ensureLatestYtDlpInstalled()
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
                string? version = process?.StandardOutput.ReadLine();
                process?.WaitForExit();
                installed = true;

                var response = await MainWindow.httpClient.SendAsync(new HttpRequestMessage
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

        public static async Task downloadLatestYtDlp()
        {
            await FileUtils.downloadFile("https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe", ".");
        }

        public static async Task<string> downloadSong(YoutubeMusicSong song, string folder)
        {

            var type = "mp3"; // mp3 or aac

            var randomFileNameBytes = new byte[8];
            Random.Shared.NextBytes(randomFileNameBytes);
            var randomFileName = Convert.ToHexString(randomFileNameBytes);
            var fullFilePath = Path.Join(folder, randomFileName + "." + type).Replace("\\", "/");

            using var process = new Process{
                StartInfo = new ProcessStartInfo
                {
                    FileName = "yt-dlp.exe",
                    WorkingDirectory = Environment.CurrentDirectory,
                    Arguments = $"--no-simulate --quiet --no-warnings --no-part --progress -o \"{fullFilePath}\" --progress -t {type} {song.youtubeSongUrl}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            // i think in stderr, not stdout:
            //
            //[download] 0.0 % of    2.96MiB at  Unknown B/ s ETA Unknown
            //[download]   0.1 % of    2.96MiB at  Unknown B/ s ETA Unknown
            //[download]   0.2 % of    2.96MiB at    1.74MiB / s ETA 00:01
            //[download]   0.5 % of    2.96MiB at    3.72MiB / s ETA 00:00
            //[download]   1.0 % of    2.96MiB at    7.69MiB / s ETA 00:00
            //[download]   2.1 % of    2.96MiB at    2.51MiB / s ETA 00:01
            //[download]   4.2 % of    2.96MiB at    2.00MiB / s ETA 00:01
            //[download]   8.4 % of    2.96MiB at    2.59MiB / s ETA 00:01
            //[download]  16.8 % of    2.96MiB at    3.12MiB / s ETA 00:00
            //[download]  33.7 % of    2.96MiB at    2.65MiB / s ETA 00:00
            //[download]  67.5 % of    2.96MiB at    3.14MiB / s ETA 00:00
            //[download] 100.0 % of    2.96MiB at    2.93MiB / s ETA 00:00
            //[download] 100 % of    2.96MiB in 00:00:01 at 2.66MiB / s

            // TODO nice ui logging

            return fullFilePath;

        }

    }
}
