using System.Collections.Generic;

namespace Downloader.Utils;

public abstract class Settings
{

    // codec name -> container file ending
    public static readonly Dictionary<string, string> AllCodecsAndFormats = new Dictionary<string, string>{
        { "aac", "aac" },
        { "alac", "m4a" },
        { "flac", "flac" },
        { "mp3", "mp3" },
        { "opus", "opus" },
        { "vorbis", "ogg" },
        { "wav", "wav" },
    };
    
    public static string Codec = "mp3";
    public static readonly string DefaultCodec = "mp3";

    public static int Threads = 5;
    public static readonly int DefaultThreads = 5;

}