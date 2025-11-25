using System;
using System.Linq;

namespace Downloader.Utils.Songs
{
    public class Song(
        string album,
        string[] artists,
        string title,
        int durationMs,
        int indexOnDisk,
        int diskIndex,
        int releaseYear,
        string imageUrl,
        string songUrl,
        string sourceApi)
    {
        public string Album = album;
        public string[] Artists = artists;
        public string Title = title;
        public int DurationMs = durationMs;
        public int IndexOnDisk = indexOnDisk;
        public int DiskIndex = diskIndex;
        public int ReleaseYear = releaseYear;
        public string ImageUrl = imageUrl;
        public string SongUrl = songUrl;
        public string SourceApi = sourceApi;

        public static bool operator == (Song? left, Song? right)
        {
            return right is not null && left is not null && (
                left.Album == right.Album &&
                left.Artists.SequenceEqual(right.Artists) &&
                left.Title == right.Title &&
                left.DurationMs == right.DurationMs &&
                left.IndexOnDisk == right.IndexOnDisk &&
                left.DiskIndex == right.DiskIndex &&
                left.ReleaseYear == right.ReleaseYear &&
                left.ImageUrl == right.ImageUrl &&
                left.SongUrl == right.SongUrl && 
                left.SourceApi == right.SourceApi
            );
        }

        public static bool operator !=(Song? left, Song? right) => !(left == right);

        public override int GetHashCode() => (Album, String.Join(", ", Artists), Title,  DurationMs, IndexOnDisk, DiskIndex, ReleaseYear, ImageUrl, SongUrl, SourceApi).GetHashCode();

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (ReferenceEquals(obj, null))
            {
                return false;
            }

            return this == (Song) obj;
        }
    }

    
}
