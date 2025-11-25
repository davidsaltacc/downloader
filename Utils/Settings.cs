using System.Collections.Generic;
using Downloader.Apis;

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

    public static readonly List<string> AllSongAudioSources = ISongAudioSource.AllSongAudioSources.ConvertAll(s => s.GetId());
    
    public static string Codec = "mp3";
    public static readonly string DefaultCodec = "mp3";

    public static int Threads = 5;
    public static readonly int DefaultThreads = 5;

    public static string SongAudioSource = YoutubeMusicApi.Instance.GetId();
    public static readonly string DefaultSongAudioSource = YoutubeMusicApi.Instance.GetId();

}