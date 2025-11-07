
using downloader.Utils;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Downloader.Apis
{
    internal abstract class FFmpegApi
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

    }
}
