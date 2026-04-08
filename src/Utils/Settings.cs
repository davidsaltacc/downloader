using System;
using System.Collections.Generic;
using Downloader.Api;
using Downloader.Api.Apis;

namespace Downloader.Utils;

public abstract class Settings
{

    // codec name -> container file ending
    public static readonly Dictionary<string, string> AllCodecsAndFormats = new (){
        { "aac", "m4a" },
        { "alac", "m4a" },
        { "flac", "flac" },
        { "mp3", "mp3" },
        { "opus", "opus" },
        { "vorbis", "ogg" },
        { "wav", "wav" },
        { "original", "" }
    };
    
    public static readonly Dictionary<string, string> AllCodecsAndMimetypes = new (){
        { "aac", "audio/mp4" },
        { "alac", "audio/mp4" },
        { "flac", "audio/flac" },
        { "mp3", "audio/mpeg" },
        { "opus", "audio/opus" },
        { "vorbis", "audio/ogg" },
        { "wav", "audio/wav" },
        { "original", "" }
    };

    public static readonly List<string> AllPlaylistFormats = [
        "M3U8",
        "XSPF"
    ];

    public static readonly List<string> AllSongAudioSources = ISongAudioSource.AllSongAudioSources.ConvertAll(s => s.GetId());
    
    public static readonly string DefaultCodec = "original";
    public static string Codec = DefaultCodec;

    public static readonly int DefaultThreads = 5;
    public static int Threads = DefaultThreads;

    public static readonly string DefaultSongAudioSource = TidalApi.InstanceHighQuality.GetId();
    public static string SongAudioSource = DefaultSongAudioSource;

    public static readonly bool DefaultCreatePlaylistFile = true;
    public static bool CreatePlaylistFile = DefaultCreatePlaylistFile;
    
    public static readonly string DefaultPlaylistFileFormat = "M3U8";
    public static string PlaylistFileFormat = DefaultPlaylistFileFormat;
    
    public static readonly string DefaultPlaylistFileName = "! playlist";
    public static string PlaylistFileName = DefaultPlaylistFileName;
    
    public static readonly string DefaultSongFileName = "%allartists% - %title%";
    public static string SongFileName = DefaultSongFileName;
    
    public static readonly string DefaultDestinationFolder = "C:/Users/" + Environment.UserName + "/Music";
    public static string DestinationFolder = DefaultDestinationFolder;
    
    public static readonly string DefaultDestinationSubfolder = "%artist%/%album%";
    public static string DestinationSubfolder = DefaultDestinationSubfolder;
    
    public static readonly string DefaultPlaylistFolderName = "Downloaded Playlist - %random%";
    public static string PlaylistFolderName = DefaultPlaylistFolderName;

}