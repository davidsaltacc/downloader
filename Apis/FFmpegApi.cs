
using downloader.Utils;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Downloader.Apis
{
    internal class FFmpegApi
    {

        public static async Task<bool> ensureFFmpegInstalled()
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
                await process?.WaitForExitAsync();
                return true;
            }
            catch {
                return false;
            }
        }

        public static async Task downloadLatestFFmpeg()
        {
            var file = await FileUtils.downloadFile("https://www.gyan.dev/ffmpeg/builds/ffmpeg-git-essentials.7z", ".");
            var latestFileVersion = await (await MainWindow.httpClient.GetAsync("https://www.gyan.dev/ffmpeg/builds/ffmpeg-git-essentials.7z.ver")).Content.ReadAsStringAsync();
            FileUtils.extractFileFrom7ZipArchive("ffmpeg-git-essentials.7z", "ffmpeg-" + latestFileVersion + "-essentials_build/bin/ffmpeg.exe", ".");
            File.Delete(file);
        }

    }
}
