using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Downloader.Utils;

public class FFMpegApi
{

    public static async Task<string> DetectAudioCodec(string file)
    {
        var ffprobeProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffprobe.exe",
                WorkingDirectory = Environment.CurrentDirectory,
                Arguments = "-v error -select_streams a:0 -show_entries stream=codec_name -of default=noprint_wrappers=1:nokey=1 \"" + file + "\"", // just print codec
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        var codecDetected = "";
        ffprobeProcess.OutputDataReceived += (_, a) => codecDetected += a.Data ?? "";
        ffprobeProcess.Start();
        ffprobeProcess.BeginOutputReadLine();
        await ffprobeProcess.WaitForExitAsync();

        return codecDetected.Trim()
            .Replace("pcm_s16le", "wav").Replace("pcm_s24le", "wav").Replace("pcm_s32le", "wav");
    }

    public static async Task<string> ReEncode(string originalFile, string codec, bool deleteOriginalAfterwards)
    {

        var codecDetected = await DetectAudioCodec(originalFile);
        var args = "";

        if (codec == "original" || codecDetected.Contains(codec))
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

        var newFname = String.Join(".", originalFile.Split(".").SkipLast(1)) + "_reenc." + Settings.AllCodecsAndFormats[(codec == "original" || codecDetected.Contains(codec)) ? codecDetected : codec];

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
        ffmpegProcess.ErrorDataReceived += (_, a) => ffmpegOut += a.Data ?? "";
        ffmpegProcess.Start();
        ffmpegProcess.BeginErrorReadLine();
        await ffmpegProcess.WaitForExitAsync();
        
        Logger.Log("FFmpeg output: " + ffmpegOut); 
        
        if (deleteOriginalAfterwards)
        {
            File.Delete(originalFile);
        }

        return newFname;

    }
    
}