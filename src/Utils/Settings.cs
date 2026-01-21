using System;
using System.Collections.Generic;
using Downloader.Api;
using Downloader.Api.Apis;

namespace Downloader.Utils;

public abstract class Settings
{

    // codec name -> container file ending
    public static readonly Dictionary<string, string> AllCodecsAndFormats = new Dictionary<string, string>{
        { "aac", "m4a" },
        { "alac", "m4a" },
        { "flac", "flac" },
        { "mp3", "mp3" },
        { "opus", "opus" },
        { "vorbis", "ogg" },
        { "wav", "wav" }
    };
    
    public static readonly Dictionary<string, string> AllCodecsAndMimetypes = new Dictionary<string, string>{
        { "aac", "audio/mp4" },
        { "alac", "audio/mp4" },
        { "flac", "audio/flac" },
        { "mp3", "audio/mpeg" },
        { "opus", "audio/opus" },
        { "vorbis", "audio/ogg" },
        { "wav", "audio/wav" }
    };

    public static readonly List<string> AllSongAudioSources = ISongAudioSource.AllSongAudioSources.ConvertAll(s => s.GetId());
    
    public static string Codec = "mp3";
    public static readonly string DefaultCodec = "mp3";

    public static int Threads = 5;
    public static readonly int DefaultThreads = 5;

    public static string SongAudioSource = YoutubeMusicApi.Instance.GetId();
    public static readonly string DefaultSongAudioSource = YoutubeMusicApi.Instance.GetId();

    public static bool CreatePlaylistFile = true;
    public static readonly bool DefaultCreatePlaylistFile = true;
    
    public static string PlaylistFileName = "! playlist";
    public static readonly string DefaultPlaylistFileName = "! playlist";
    
    public static string SongFileName = "%allartists% - %title%";
    public static readonly string DefaultSongFileName = "%allartists% - %title%";
    
    public static string DestinationFolder = "C:/Users/" + Environment.UserName + "/Music";
    public static readonly string DefaultDestinationFolder = "C:/Users/" + Environment.UserName + "/Music";
    
    public static string DestinationSubfolder = "%artist%/%album%";
    public static readonly string DefaultDestinationSubfolder = "%artist%/%album%";

}