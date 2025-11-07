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
        public readonly int durationMs = durationMs;
        public readonly int indexOnDisk = indexOnDisk;
        public readonly int diskIndex = diskIndex;
        public readonly int releaseYear = releaseYear;
        public readonly string imageUrl = imageUrl;

        public static bool operator == (Song? left, Song? right)
        {
            return right is not null && left is not null && (
                left.Album == right.Album &&
                left.Artists.SequenceEqual(right.Artists) &&
                left.Title == right.Title &&
                left.durationMs == right.durationMs &&
                left.indexOnDisk == right.indexOnDisk &&
                left.diskIndex == right.diskIndex &&
                left.releaseYear == right.releaseYear &&
                left.imageUrl == right.imageUrl
            );
        }

        public static bool operator !=(Song? left, Song? right) => !(left == right);

        public override int GetHashCode() => (Album, String.Join(", ", Artists), Title,  durationMs, indexOnDisk, diskIndex, releaseYear, imageUrl).GetHashCode();

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
