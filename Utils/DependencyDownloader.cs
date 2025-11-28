
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Downloader.Utils
{
    internal abstract class DependencyDownloader
    {

        public static async Task<bool> EnsureFFmpegInstalled()
        {

            return await Test("ffmpeg.exe") && await Test("ffprobe.exe");
            
            async Task<bool> Test(string file) {
                try
                {
                    using var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = file,
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
        }

        public static async Task DownloadLatestFFmpeg()
        {
            var file = await Utils.DownloadFile("https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-lgpl-shared.zip", ".");
            Utils.ExtractAllFilesFromZipArchive(file, ".");
            Directory.GetFiles("./ffmpeg-master-latest-win64-lgpl-shared/bin").ToList().ForEach(f => File.Move(f, Path.Combine(".", Path.GetFileName(f))));
            File.Delete(file);
            Directory.Delete("./ffmpeg-master-latest-win64-lgpl-shared", true);
        }
        
        public static async Task<bool> EnsureLatestDenoInstalled()
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "deno.exe",
                    Arguments = "-v",
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

                var response = await MainWindow.HttpClient.SendAsync(new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri("https://api.github.com/repos/denoland/deno/releases/latest"),
                    Headers =
                    {
                        { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.3" }
                    }
                });
                var latest = JsonNode.Parse(await response.Content.ReadAsStringAsync())?["tag_name"]?.ToString();
                
                return version == latest?.Replace("v", "");
            }
            catch (System.ComponentModel.Win32Exception) {
                return false;
            }
        }

        public static async Task DownloadLatestDeno()
        {
            var file = await Utils.DownloadFile("https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip", ".");
            Utils.ExtractFileFromZipArchive(file, "deno.exe", ".");
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
            await Utils.DownloadFile("https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe", ".");
        }

    }
}
