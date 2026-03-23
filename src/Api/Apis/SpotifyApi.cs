using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Downloader.Utils;
using OtpNet;
using SoftCircuits.HtmlMonkey;

namespace Downloader.Api.Apis;

public class SpotifyApi : ISongDataSource
{
    
    private SpotifyApi() {}

    private static SpotifyApi? _instance = null;
    public static SpotifyApi Instance
    {
        get
        {
            _instance ??= new SpotifyApi();
            return _instance;
        }
    }
    
    private string? _accessToken = null; 
    private string? _clientToken = null;

    private static readonly string API_URL = "https://api-partner.spotify.com/pathfinder/v1/query";

    public async Task Init()
    {

        var tlsSession = new TlsSession(new TlsSession.TlsSessionInit
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

        var script = document.FindOfType<HtmlElementNode>(e => e.TagName == "script" && e.Attributes.Contains("src"))
            .Select(e => e.Attributes.TryGetValue("src", out var src) ? src.Value : null)
            .Where(s => s != null && s.Contains("web-player/web-player") && s.EndsWith(".js")).ToList().First();

        var serverConfig = JsonNode.Parse(Convert.FromBase64String(
            data.Split("<script id=\"appServerConfig\" type=\"text/plain\">")[1].Split("</script>")[0]));

        if (serverConfig == null)
        {
            throw new Exception("Failed to init Spotify API - serverConfig was null");
        }

        var clientVersion = serverConfig["clientVersion"]?.ToString();
        
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
                client_version = clientVersion,
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

    public Task<Song[]> GetSongs(string url)
    {
        throw new System.NotImplementedException();
    }
    
}
