
using System;
using downloader.Utils;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Downloader.Apis
{
    internal abstract class DependencyDownloader
    {

        public static async Task<bool> EnsureFFmpegInstalled()
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "ffmpeg.exe",
                    Arguments = "-version",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (process == null)
                {
                    return false;
                }
                await process.WaitForExitAsync();
                return true;
            }
            catch {
                return false;
            }
        }

        public static async Task DownloadLatestFFmpeg()
        {
            var file = await FileUtils.DownloadFile("https://www.gyan.dev/ffmpeg/builds/ffmpeg-git-essentials.7z", ".");
            var latestFileVersion = await (await MainWindow.HttpClient.GetAsync("https://www.gyan.dev/ffmpeg/builds/ffmpeg-git-essentials.7z.ver")).Content.ReadAsStringAsync();
            FileUtils.ExtractFileFrom7ZipArchive("ffmpeg-git-essentials.7z", "ffmpeg-" + latestFileVersion + "-essentials_build/bin/ffmpeg.exe", ".");
            File.Delete(file);
        }
        
        public static async Task<bool> EnsureLatestQjsInstalled()
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "qjs.exe",
                    Arguments = "--help",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (process == null)
                {
                    return false;
                }
                
                var version = (await process.StandardOutput.ReadLineAsync() ?? "").Split(" ").LastOrDefault("");
                await process.WaitForExitAsync();
                
                var latest = JsonNode.Parse(await (await MainWindow.HttpClient.GetAsync("https://bellard.org/quickjs/binary_releases/LATEST.json")).Content.ReadAsStringAsync())?["version"]?.ToString();
                
                return version == latest;
            }
            catch (System.ComponentModel.Win32Exception) {
                return false;
            }
        }

        public static async Task DownloadLatestQjs()
        {
            var latest = JsonNode.Parse(await (await MainWindow.HttpClient.GetAsync("https://bellard.org/quickjs/binary_releases/LATEST.json")).Content.ReadAsStringAsync())?["version"]?.ToString();
            var file = await FileUtils.DownloadFile("https://bellard.org/quickjs/binary_releases/quickjs-win-x86_64-" + latest + ".zip", ".");
            FileUtils.ExtractAllFilesFromZipArchive("quickjs-win-x86_64-" + latest + ".zip", ".");
            File.Delete(file);
        }
        
        public static async Task<bool> EnsureLatestYtDlpInstalled()
        {

            var installed = false;
            var latest = false;

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

    }
}
