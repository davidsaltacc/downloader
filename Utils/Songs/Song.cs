using System;
using System.Linq;

namespace downloader.Utils.Songs
{
    public class Song(
        string album,
        string[] artists,
        string title,
        int durationMs,
        int indexOnDisk,
        int diskIndex,
        int releaseYear,
        string imageUrl)
    {
        public readonly string Album = album;
        public readonly string[] Artists = artists;
        public readonly string Title = title;
        public readonly int DurationMs = durationMs;
        public readonly int IndexOnDisk = indexOnDisk;
        public readonly int DiskIndex = diskIndex;
        public readonly int ReleaseYear = releaseYear;
        public readonly string ImageUrl = imageUrl;

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
                left.ImageUrl == right.ImageUrl
            );
        }

        public static bool operator !=(Song? left, Song? right) => !(left == right);

        public override int GetHashCode() => (Album, String.Join(", ", Artists), Title,  DurationMs, IndexOnDisk, DiskIndex, ReleaseYear, ImageUrl).GetHashCode();

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
