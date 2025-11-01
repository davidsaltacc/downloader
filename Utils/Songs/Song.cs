namespace downloader.Utils.Songs
{
    public class Song(string album, string[] artists, string title, int indexOnDisk, int diskIndex, int releaseYear, string imageUrl)
    {
        public readonly string Album = album;
        public readonly string[] Artists = artists;
        public readonly string Title = title;
        public readonly int indexInAlbum = indexOnDisk;
        public readonly int diskIndex = diskIndex;
        public readonly int releaseYear = releaseYear;
        public readonly string imageUrl = imageUrl;
    }
}
