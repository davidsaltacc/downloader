
namespace Downloader.Utils.Songs
{
    internal class SpotifySong(string album, string[] artists, string title, int durationMs, int indexOnDisk, int diskIndex, int releaseYear, string imageUrl, string spotifySongUrl) : Song(album, artists, title, durationMs, indexOnDisk, diskIndex, releaseYear, imageUrl)
    {
        public readonly string SpotifySongUrl = spotifySongUrl;

        public override int GetHashCode() => (base.GetHashCode(), SpotifySongUrl).GetHashCode();

    }
}
