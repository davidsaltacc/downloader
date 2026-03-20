using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Downloader.Utils;

public class FFMpegApi
{

    public static async Task<string> ReEncode(string originalFile, string codec, bool deleteOriginalAfterwards)
    {
        
        var ffprobeProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffprobe.exe",
                WorkingDirectory = Environment.CurrentDirectory,
                Arguments = "-v error -select_streams a:0 -show_entries stream=codec_name -of default=noprint_wrappers=1:nokey=1 \"" + originalFile + "\"", // just print codec
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        var data = "";
        ffprobeProcess.OutputDataReceived += (_, a) => data += a.Data ?? "";
        ffprobeProcess.Start();
        await ffprobeProcess.WaitForExitAsync();

        var args = "";

        if (codec != "wav" && data.Trim().Contains(codec) ||
            (codec == "wav" && data.Trim().Contains("pcm_s16le")) ||
            (codec == "wav" && data.Trim().Contains("pcm_s24le")) ||
            (codec == "wav" && data.Trim().Contains("pcm_s32le")))
        {
            args += "-c copy";
        }
        else
        {
            args += codec switch
            {
                "aac" => "-c:a aac -b:a 320k -movflags +faststart",
                "alac" => "-c:a alac",
                "flac" => "-c:a flac -compression_level 12",
                "mp3" => "-c:a libmp3lame -b:a 320k",
                "opus" => "-c:a libopus -b:a 510k -vbr on -compression_level 10",
                "vorbis" => "-c:a libvorbis -q:a 10",
                "wav" => "-c:a pcm_s24le",
                _ => "-c copy"
            };
        }

        var newFname = String.Join(".", originalFile.Split(".").SkipLast(1)) + "_reenc." + Settings.AllCodecsAndFormats[codec];

        args = "-i \"" + originalFile + "\" " + args + " \"" + newFname + "\"";
        
        var ffmpegProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg.exe",
                WorkingDirectory = Environment.CurrentDirectory,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        var ffmpegOut = "";
        ffmpegProcess.OutputDataReceived += (_, a) => ffmpegOut += a.Data ?? "";
        ffmpegProcess.Start();
        await ffmpegProcess.WaitForExitAsync();
        
        Logger.Log("ffmpeg output: " + ffmpegOut); 
        // TODO 1. ffmpreg output doesnt show here, probably update this thingy
        // TODO 2. test/fix same-codec encoding (should copy, does not) - ffprobe output is broken for same reason as this - test with ytm - opus 2 opus
        // TODO 3. see if bitrate can be limited to original or something, kinda stupid to see a file inflate in size (ytm-opus with like 1?? kb/s to whatever with 300+ kb/s)
        // TODO 4. fix rest of re-encoding stuff
        // TODO 5. fix tidal downloading (lossy)
        // TODO 6. try tidal lossless downloading
        // TODO 7. spotify TOTP generation/extraction for not relying on spotDL - important!
        
        if (deleteOriginalAfterwards)
        {
            File.Delete(originalFile);
        }

        return newFname;

    }
    
}