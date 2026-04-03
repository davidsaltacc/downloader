
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

        public static async Task DownloadLatestFFmpeg(Action<int>? onProgressUpdate = null)
        {
            var file = await Helpers.DownloadFile("https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-n7.1-latest-win64-lgpl-shared-7.1.zip", ".", onProgressUpdate);
            Helpers.ExtractAllFilesFromZipArchive(file, ".");
            Directory.GetFiles("./ffmpeg-n7.1-latest-win64-lgpl-shared-7.1/bin").ToList().ForEach(f =>
            {
                File.Move(f, Path.Combine(".", Path.GetFileName(f)), true);
            });
            File.Delete(file);
            Directory.Delete("./ffmpeg-n7.1-latest-win64-lgpl-shared-7.1", true);
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

        public static async Task DownloadLatestQjs(Action<int>? onProgressUpdate = null)
        {
            var latest = JsonNode.Parse(await (await MainWindow.HttpClient.GetAsync("https://bellard.org/quickjs/binary_releases/LATEST.json")).Content.ReadAsStringAsync())?["version"]?.ToString();
            var file = await Helpers.DownloadFile("https://bellard.org/quickjs/binary_releases/quickjs-win-x86_64-" + latest + ".zip", ".");
            Helpers.ExtractAllFilesFromZipArchive("quickjs-win-x86_64-" + latest + ".zip", ".", true);
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

        public static async Task DownloadLatestYtDlp(Action<int>? onProgressUpdate = null)
        {
            await Helpers.DownloadFile("https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe", ".", onProgressUpdate);
        }
        
        private static async Task<JsonNode?> GetLatestEmbeddablePythonVersion()
        {
            
            var response =
                await MainWindow.HttpClient.GetAsync("https://www.python.org/ftp/python/index-windows-recent.json");

            var content = await response.Content.ReadAsStringAsync();
            var data = JsonNode.Parse(content);

            if (data == null)
            {
                return null;
            }

            return data["versions"]?.AsArray().FirstOrDefault(v => v?["company"]?.ToString() == "PythonEmbed" && (v["install-for"]?.AsArray().Select(n => n?.ToString() ?? "").Contains("3-64") ?? false));

        }
        
        public static async Task<bool> EnsureLatestEmbeddablePythonInstalled()
        {
            
            var installed = false;
            var latest = false;

            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "./python/python.exe",
                    WorkingDirectory = "./python",
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

                var latestVersion = await GetLatestEmbeddablePythonVersion();

                latest = latestVersion?["display-name"]?.ToString().Contains(version?.Trim() ?? "nothing!") ?? false;

            } catch { }

            return installed && latest;

        }

        public static async Task DownloadLatestEmbeddablePython(Action<int>? onProgressUpdate = null)
        {
            var latestVersion = await GetLatestEmbeddablePythonVersion();
            var url = latestVersion?["url"]?.ToString();
            if (url == null)
            {
                return;
            }
            
            Directory.Delete("./python", true);
            
            // download python
            var file = await Helpers.DownloadFile(url, ".", onProgressUpdate);
            Helpers.ExtractAllFilesFromZipArchive(file, "./python");
            File.Delete(file);

            // download pip
            await Helpers.DownloadFile("https://bootstrap.pypa.io/pip/get-pip.py", "./python");
            await File.AppendAllTextAsync(Directory.GetFiles("./python", "*._pth")[0], "\nimport site");
            
            var process = new Process{
                StartInfo = new ProcessStartInfo{
                    FileName = "./python/python.exe",
                    WorkingDirectory = "./python",
                    Arguments = "-I get-pip.py",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    Environment =
                    {
                        { "PYTHONHOME", "" },
                        { "PYTHONPATH", "" },
                        { "PYTHONNOUSERSITE", "1" }
                    }
                }
            };

            var processErr = "";
            process.ErrorDataReceived += (_, a) => processErr += a.Data ?? "" + "\n";
            process.Start();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            if (processErr.Trim().Length > 0)
            {
                Logger.Log("pip installation stderr: " + processErr);
            }

        }

        public static async Task DownloadPythonPackage(string packages)
        {
            
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "./python/python.exe",
                WorkingDirectory = "./python",
                Arguments = "-m pip install " + packages,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            
            if (process == null)
            {
                return;
            }

            await process.WaitForExitAsync();
            
        }

    }
}
