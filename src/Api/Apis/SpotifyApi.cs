using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Downloader.Utils;
using OtpNet;
using SoftCircuits.HtmlMonkey;

namespace Downloader.Api.Apis;

public class SpotifyApi : ISongDataSource
{
    
    private SpotifyApi() {} // new spotify api loosely ported from Aran404/SpotAPI

    private static SpotifyApi? _instance = null;
    public static SpotifyApi Instance
    {
        get
        {
            _instance ??= new SpotifyApi();
            return _instance;
        }
    }

    private TlsSession? _session = null;
    private string? _accessToken = null; 
    private string? _clientToken = null;
    private string? _clientVersion = null;
    private Dictionary<string, string> _hashes = new ();

    private static readonly string QUERY_API_URL = "https://api-partner.spotify.com/pathfinder/v2/query";

    public async Task Init()
    {

        _accessToken = _clientToken = _clientVersion = null;
        _hashes = new Dictionary<string, string>();

        var tlsSession = _session = new TlsSession(new TlsSession.TlsSessionInit
        {
            DefaultHeaders =
            {
                { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36" },
                { "Accept-Language", "en-US,en;q=0.9" },
                { "Accept-Encoding", "gzip, deflate, br, zstd" }
            },
            DefaultRandomTLSExtensionOrder = true,
            DefaultTlsClientIdentifier = "chrome_120"
        });
        
        var data = tlsSession.SendRequest(new TlsSession.TlsRequest
        {
            RequestMethod = "GET",
            RequestUrl = "https://open.spotify.com"
        }).Body;
        var document = HtmlDocument.FromHtml(data);

        var webPlayerPackSrc = document.FindOfType<HtmlElementNode>(e => e.TagName == "script" && e.Attributes.Contains("src"))
            .Select(e => e.Attributes.TryGetValue("src", out var src) ? src.Value : null)
            .Where(s => s != null && s.Contains("web-player/web-player") && s.EndsWith(".js")).ToList().First();

        if (webPlayerPackSrc == null)
        {
            throw new Exception("Failed to init Spotify API - webPlayerPackSrc was null");
        }
        
        var webPlayerPack = tlsSession.SendRequest(new TlsSession.TlsRequest
        {
            RequestMethod = "GET",
            RequestUrl = webPlayerPackSrc
        }).Body;

        var mappings = Regex.Matches(webPlayerPack, @"\{\d+:\""[^\""]+\""(?:,\d+:\""[^\""]+\"")*\}");
        var mappingNames = JsonNode.Parse(Regex.Replace(
            mappings[2].Value,
            @"(?<=\{|,)\s*(\d+)\s*:",
            m => $"\"{m.Groups[1].Value}\":"
        ));
        var mappingHashes = JsonNode.Parse(Regex.Replace(
            mappings[3].Value,
            @"(?<=\{|,)\s*(\d+)\s*:",
            m => $"\"{m.Groups[1].Value}\":"
        ));

        if (mappingNames == null || mappingHashes == null)
        {
            throw new Exception("Failed to init Spotify API - mappingNames or mappingHashes was null - this should not happen");
        }
        
        var hashRegex= @"""([^""]*)"",""(?:query|mutation)"",""([0-9a-fA-F]+)""";

        Regex.Matches(webPlayerPack, hashRegex)
            .ToList().ForEach(match => _hashes.TryAdd(match.Groups[1].Value, match.Groups[2].Value));

        foreach (var namePair in mappingNames.AsObject().AsEnumerable())
        {
            if (mappingHashes.AsObject().ContainsKey(namePair.Key))
            {
                Regex.Matches(
                    tlsSession.SendRequest(new TlsSession.TlsRequest
                        {
                            RequestMethod = "GET",
                            RequestUrl = "https://open.spotifycdn.com/cdn/build/web-player/" + namePair.Value + "." + mappingHashes[namePair.Key] + ".js"
                        }
                    ).Body, hashRegex
                ).ToList().ForEach(match => _hashes.TryAdd(match.Groups[1].Value, match.Groups[2].Value));
            }
        }

        var serverConfig = JsonNode.Parse(Convert.FromBase64String(
            data.Split("<script id=\"appServerConfig\" type=\"text/plain\">")[1].Split("</script>")[0]));

        if (serverConfig == null)
        {
            throw new Exception("Failed to init Spotify API - serverConfig was null");
        }

        _clientVersion = serverConfig["clientVersion"]?.ToString();
        
        var deviceId = tlsSession.GetCookies("https://open.spotify.com")
            .Where(c => c.Name == "sp_t")
            .Select(c => c.Value)
            .FirstOrDefault() ?? "";

        var secretsJson = JsonNode.Parse(await (await MainWindow.HttpClient.GetAsync(
            "https://code.thetadev.de/ThetaDev/spotify-secrets/raw/branch/main/secrets/secretDict.json")).Content.ReadAsStringAsync());
        var totpVersion = secretsJson?.AsObject().ToList()
            .Select(p => Int32.Parse(p.Key)).Max();
        var totpSecret = secretsJson?[totpVersion?.ToString() ?? ""]?.AsArray()
            .Where(i => i != null)
            .Select(i => i.GetValue<int>())
            .ToArray();

        if (totpSecret == null)
        {
            throw new Exception("Failed to init Spotify API - totpSecret was null");
        }
        
        var newTotpSecret = new int[totpSecret.Length];
        var i = 0;
        foreach (var number in totpSecret)
        {
            newTotpSecret[i] = number ^ (i % 33 + 9);
            i++;
        }
        
        var totp = new Totp(
            Base32Encoding.ToBytes(Base32Encoding.ToString(
                Encoding.ASCII.GetBytes(
                    string.Concat(newTotpSecret)
                )
            ).TrimEnd('='))
        );
        var totpGenerated = totp.ComputeTotp();
        
        var url = "https://open.spotify.com/api/token" +
            "?reason=init" +
            "&productType=web-player" +
            "&totpVer=" + totpVersion +
            "&totp=" + totpGenerated +
            "&totpServer=" + totpGenerated;

        var content = tlsSession.SendRequest(new TlsSession.TlsRequest
        {
            RequestMethod = "GET",
            RequestUrl = url
        }).Body;
        var authData = JsonNode.Parse(content);

        if (authData == null)
        {
            throw new Exception("Failed to init Spotify API - authData was null");
        }

        _accessToken = authData["accessToken"]?.ToString();
        var clientId = authData["clientId"]?.ToString();

        var payload = new
        {
            client_data = new
            {
                client_version = _clientVersion,
                client_id = clientId,
                js_sdk_data = new
                {
                    device_brand = "unknown",
                    device_model = "unknown",
                    os = "windows",
                    os_version = "NT 10.0",
                    device_id = deviceId,
                    device_type = "computer"
                }
            }
        };

        var response = tlsSession.SendRequest(new TlsSession.TlsRequest
        {
            RequestMethod = "POST",
            RequestUrl = "https://clienttoken.spotify.com/v1/clienttoken",
            RequestBody = JsonSerializer.Serialize(payload),
            Headers =
            {
                { "Content-Type", "application/json" },
                { "Authority", "clienttoken.spotify.com" },
                { "Accept", "application/json" }
            }
        });
        content = response.Body;
        JsonNode? tokenData;
        
        try
        {
            tokenData = JsonNode.Parse(content);
        } catch (Exception e)
        {
            Logger.Log("Error - spotify did not return proper json: " + content);
            throw e;
        }

        if (tokenData?["response_type"]?.ToString() != "RESPONSE_GRANTED_TOKEN_RESPONSE")
        {
            throw new Exception("Failed to init Spotify API - token was not granted - actual response: " + tokenData);
        }

        _clientToken = tokenData["granted_token"]?["token"]?.ToString();

        if (_clientToken == null)
        {
            throw new Exception("Failed to init Spotify API - _clientToken was null");
        }
        
    }

    public string GetName()
    {
        return "Spotify";
    }

    public string GetId()
    {
        return "spotify";
    }

    public bool UrlPartOfPlatform(string url)
    {
        return new Uri(url).Host.Contains("open.spotify", StringComparison.OrdinalIgnoreCase);
    }

    public JsonNode? SendQueryApiRequest(string opName, object variables)
    {
        
        if (_session == null)
        {
            return null;
        }

        var query = new
        {
            operationName = opName,
            extensions = new {
                persistedQuery = new {
                    version = 1,
                    sha256Hash = _hashes[opName]
                }
            },
            variables
        };
        
        return JsonNode.Parse(_session.SendRequest(new TlsSession.TlsRequest
        {
            RequestMethod = "POST",
            RequestUrl = QUERY_API_URL,
            RequestBody = JsonSerializer.Serialize(query),
            Headers =
            {
                { "Authorization", "Bearer " + _accessToken },
                { "Client-Token", _clientToken ?? "" },
                { "Spotify-App-Version", _clientVersion ?? "" }
            }
        }).Body);
        
    }

    private async Task<Song[]> GetSongsFromUrls(string[] urls)
    {
        List<Song> songs = [];

        foreach (var url in urls)
        {
            var data = SendQueryApiRequest("getTrack", new
            {
                uri = "spotify:track:" + url.Split("/").Last(p => p.Length > 1)
            });
            if (data == null)
            {
                continue;
            }

            var discIndex = 1;
            var onDiscIndex = 1;

            foreach (var track in Helpers.NavigateJsonNode(data, "data", "trackUnion", "albumOfTrack", "tracks", "items")?.AsArray() ?? [])
            {
                if (Int32.Parse(track?["trackNumber"]?.ToString() ?? (onDiscIndex + 1).ToString()) < onDiscIndex)
                {
                    discIndex += 1;
                    onDiscIndex = 0;
                }
                onDiscIndex += 1;
                if (track?["uri"]?.ToString() ==
                    Helpers.NavigateJsonNode(data, "data", "trackUnion", "uri")?.ToString())
                {
                    break;
                }
            }
            
            songs.Add(
                new Song(
                    Helpers.NavigateJsonNode(data, "data", "trackUnion", "albumOfTrack", "name")?.ToString() ?? "",
                    new List<string?>([
                        Helpers.NavigateJsonNode(data, "data", "trackUnion", "firstArtist", "items", 0, "profile", "name")?.ToString(),
                    ]).Concat(
                            Helpers.NavigateJsonNode(data, "data", "trackUnion", "otherArtists", "items")?.AsArray().Select(a => a?["profile"]?["name"]?.ToString()) ?? []
                    ).Where(s => s != null).ToArray()!,
                    Helpers.NavigateJsonNode(data, "data", "trackUnion", "name")?.ToString() ?? "",
                    Int32.Parse(Helpers.NavigateJsonNode(data, "data", "trackUnion", "duration", "totalMilliseconds")?.ToString() ?? "-1"),
                    Int32.Parse(Helpers.NavigateJsonNode(data, "data", "trackUnion", "trackNumber")?.ToString() ?? "-1"),
                    discIndex, 
                    DateTime.TryParse(Helpers.NavigateJsonNode(data, "data", "trackUnion", "albumOfTrack", "date", "year")?.ToString(), out var time) ? time.Year : -1,
                    Helpers.NavigateJsonNode(data, "data", "trackUnion", "albumOfTrack", "coverArt", "sources")?.AsArray().OrderByDescending(s => Int32.Parse(s?["width"]?.ToString() ?? "0")).First()?["url"]?.ToString() ?? "",
                    url,
                    GetId()
                )
            );
        }

        return songs.ToArray();
    }
    
    private (JsonArray, JsonNode, int) GetAlbumSongs(string id, int offset, int limit)
    {
        var response = SendQueryApiRequest("getAlbum", new
        {
            uri = "spotify:album:" + id,
            offset,
            limit
        });
        return (Helpers.NavigateJsonNode(response, "data", "albumUnion", "tracksV2", "items")?.AsArray() ?? [],
            Helpers.NavigateJsonNode(response, "data", "albumUnion")!,
            Int32.Parse(Helpers.NavigateJsonNode(response, "data", "albumUnion", "tracksV2", "totalCount")?.ToString() ?? limit.ToString())); 
    }

    private async Task<Song[]> GetSongsInAlbum(string albumUrl)
    {

        List<JsonNode?> allSongs = [];
        var id = albumUrl.Split("/").Last(p => p.Length > 1);
        
        var (arr, data, total) = GetAlbumSongs(id, 0, 100); 

        allSongs.AddRange(arr);
        if (total > 100)
        {
            var offset = 100;
            var received = 100;
            while (received < total)
            {
                var (arr2, _, _) = GetAlbumSongs(id, offset, 100);
                allSongs.AddRange(arr2);
                received += arr2.Count;
                offset += 100;
            }
        }

        var result = allSongs.Select(
            s => new Song(
                data["name"]?.ToString() ?? "",
                new List<string?>(
                    Helpers.NavigateJsonNode(s, "track", "artists", "items")?.AsArray().Select(a => a?["profile"]?["name"]?.ToString()) ?? []
                ).Where(s2 => s2 != null).ToArray()!,
                Helpers.NavigateJsonNode(s, "track", "name")?.ToString() ?? "",
                Int32.Parse(Helpers.NavigateJsonNode(s, "track", "duration", "totalMilliseconds")?.ToString() ?? "-1"),
                Int32.Parse(Helpers.NavigateJsonNode(s, "track", "trackNumber")?.ToString() ?? "-1"),
                Int32.Parse(Helpers.NavigateJsonNode(s, "track", "discNumber")?.ToString() ?? "-1"),
                DateTime.TryParse(Helpers.NavigateJsonNode(data, "date", "isoString")?.ToString(), out var time) ? time.Year : -1,
                Helpers.NavigateJsonNode(data, "coverArt", "sources")?.AsArray().OrderByDescending(s2 => Int32.Parse(s2?["width"]?.ToString() ?? "0")).First()?["url"]?.ToString() ?? "",
                "https://open.spotify.com/track/" + (Helpers.NavigateJsonNode(s, "track", "uri")?.ToString() ?? "").Split(":")[2],
                GetId()
            )
        ).ToArray();
        return result;

    }
    
    private (JsonArray, int) GetPlaylistData(string id, int offset, int limit)
    {
        var response = SendQueryApiRequest("fetchPlaylist", new
        {
            uri = "spotify:playlist:" + id,
            offset,
            limit,
            enableWatchFeedEntrypoint = false
        });
        return (Helpers.NavigateJsonNode(response, "data", "playlistV2", "content", "items")?.AsArray() ?? [],
            Int32.Parse(Helpers.NavigateJsonNode(response, "data", "playlistV2", "content", "totalCount")?.ToString() ?? limit.ToString())); 
    }

    private async Task<Song[]> GetSongsInPlaylist(string playlistUrl)
    {

        List<JsonNode?> allSongs = [];
        var id = playlistUrl.Split("/").Last(p => p.Length > 1);
        
        var (arr, total) = GetPlaylistData(id, 0, 100); 

        allSongs.AddRange(arr);
        if (total > 100)
        {
            var offset = 100;
            var received = 100;
            while (received < total)
            {
                var (arr2, _) = GetPlaylistData(id, offset, 100);
                allSongs.AddRange(arr2);
                received += arr2.Count;
                offset += 100;
            }
        }
        
        var result = allSongs.Select(
            s =>
            {
                var (_, albumData, _) = GetAlbumSongs((Helpers.NavigateJsonNode(s, "itemV2", "data", "albumOfTrack", "uri")?.ToString() ?? "").Split(":")[2], 0, 1);
                return new Song(
                    Helpers.NavigateJsonNode(s, "itemV2", "data", "albumOfTrack", "name")?.ToString() ?? "",
                    new List<string?>(Helpers.NavigateJsonNode(s, "itemV2", "data", "artists", "items")?.AsArray().Select(a => a?["profile"]?["name"]?.ToString()) ?? []
                    ).Where(s2 => s2 != null).ToArray()!,
                    Helpers.NavigateJsonNode(s, "itemV2", "data", "name")?.ToString() ?? "",
                    Int32.Parse(Helpers.NavigateJsonNode(s, "itemV2", "data", "trackDuration", "totalMilliseconds")?.ToString() ?? "-1"),
                    Int32.Parse(Helpers.NavigateJsonNode(s, "itemV2", "data", "trackNumber")?.ToString() ?? "-1"),
                    Int32.Parse(Helpers.NavigateJsonNode(s, "itemV2", "data", "discNumber")?.ToString() ?? "-1"),
                    DateTime.TryParse(Helpers.NavigateJsonNode(albumData, "date", "isoString")?.ToString(), out var time) ? time.Year : -1,
                    Helpers.NavigateJsonNode(s, "itemV2", "data", "albumOfTrack", "coverArt", "sources")?.AsArray().OrderByDescending(s2 => Int32.Parse(s2?["width"]?.ToString() ?? "0")).First()?["url"]?.ToString() ?? "",
                    "https://open.spotify.com/track/" + (Helpers.NavigateJsonNode(s, "itemV2", "data", "uri")?.ToString() ?? "").Split(":")[2],
                    GetId()
                );
            }
        ).ToArray();
        return result;

    }

    public async Task<Song[]> GetSongs(string url)
    {
        url = url.Split("?")[0];
        var uri = new Uri(url);
        if (uri.AbsolutePath.StartsWith("/track"))
        {
            return await GetSongsFromUrls([ url ]);
        } 
        if (uri.AbsolutePath.StartsWith("/album"))
        {
            return await GetSongsInAlbum(url);
        }
        if (uri.AbsolutePath.StartsWith("/playlist"))
        {
            return await GetSongsInPlaylist(url);
        }
        return [];
    }
    
}
