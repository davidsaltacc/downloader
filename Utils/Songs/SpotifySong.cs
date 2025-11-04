
namespace downloader.Utils.Songs
{
    internal class SpotifySong(string album, string[] artists, string title, int indexOnDisk, int diskIndex, int releaseYear, string imageUrl, string spotifySongUrl) : Song(album, artists, title, indexOnDisk, diskIndex, releaseYear, imageUrl)
    {
        public readonly string spotifySongUrl = spotifySongUrl;

        public override int GetHashCode() => (base.GetHashCode(), spotifySongUrl).GetHashCode();

    }
}
