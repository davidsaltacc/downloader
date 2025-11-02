using System;

namespace downloader.Utils.Songs
{
    public class Song(string album, string[] artists, string title, int indexOnDisk, int diskIndex, int releaseYear, string imageUrl) : IEquatable<Song>
    {
        public readonly string Album = album;
        public readonly string[] Artists = artists;
        public readonly string Title = title;
        public readonly int indexOnDisk = indexOnDisk;
        public readonly int diskIndex = diskIndex;
        public readonly int releaseYear = releaseYear;
        public readonly string imageUrl = imageUrl;

        public bool Equals(Song? other)
        {
            return other != null && (
                this.Album == other.Album &&
                this.Artists == other.Artists &&
                this.Title == other.Title &&
                this.indexOnDisk == other.indexOnDisk &&
                this.diskIndex == other.diskIndex &&
                this.releaseYear == other.releaseYear &&
                this.imageUrl == other.imageUrl
            );
        }
    }

    
}
