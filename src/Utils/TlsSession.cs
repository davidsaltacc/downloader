using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Downloader.Utils;

public class TlsSession : IDisposable
{
    
    [DllImport("tls-client-windows-64-1.14.0.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr request(string requestPayload);
    
    [DllImport("tls-client-windows-64-1.14.0.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr getCookiesFromSession(string requestPayload);
    
    [DllImport("tls-client-windows-64-1.14.0.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void destroySession(string requestPayload);
    
    [DllImport("tls-client-windows-64-1.14.0.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void freeMemory(string requestId);

    public class TlsSessionInit
    {
        public int TimeoutSeconds { get; set; } = 30;
        public Dictionary<string, string> DefaultHeaders { get; set; } = new();
        public string DefaultTlsClientIdentifier { get; set; } = "chrome_120";
        public bool DefaultRandomTLSExtensionOrder { get; set; } = false;
        public bool DefaultFollowRedirects { get; set; } = true;
        public bool DefaultCatchPanics { get; set; } = true;
    }

    public class TlsRequest
    {
        public required string RequestMethod { get; set; }
        public required string RequestUrl { get; set; }
        public string? RequestBody { get; set; } = null;
        public Dictionary<string, string> Headers { get; set; } = new ();
        public string? TlsClientIdentifier { get; set; } = null;
        public bool? RandomTLSExtensionOrder { get; set; } = null;
        public bool? FollowRedirects { get; set; } = null;
        public bool? CatchPanics { get; set; } = null;
    }

    private class _RequestInternal
    {
        public string SessionId { get; set; } = "";
        public int TimeoutSeconds { get; set; } = 30;
        public required string RequestMethod { get; set; }
        public required string RequestUrl { get; set; }
        public string? RequestBody { get; set; } = null;
        public Dictionary<string, string> Headers { get; set; } = new();
        public string TlsClientIdentifier { get; set; } = "chrome_120";
        public bool RandomTLSExtensionOrder { get; set; } = false;
        public bool FollowRedirects { get; set; } = true;
        public bool CatchPanics { get; set; } = true;
    }

    public class TlsResponse
    {
        public string Id { get; set; }
        public string SessionId { get; set; }
        public int Status { get; set; }
        public string Target { get; set; }
        public string Body { get; set; }
        public Dictionary<string, string[]> Headers { get; set; }
        public Dictionary<string, string> Cookies { get; set; }
        public string UsedProtocol { get; set; }
    }

    public class TlsCookie
    {
        public int Expires { get; set; }
        public string Domain { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public string Value { get; set; }
        public int MaxAge { get; set; }
        public bool Secure { get; set; }
        public bool HttpOnly { get; set; }
    }

    private readonly string _sessionId;
    private readonly TlsSessionInit _sessionInit;

    public TlsSession(TlsSessionInit sessionInit)
    { 
        _sessionId = Guid.NewGuid().ToString();
        _sessionInit = sessionInit;
    }

    private readonly JsonSerializerOptions _opts = new (){ PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    
    public TlsResponse SendRequest(TlsRequest request)
    {

        var req = new _RequestInternal
        {
            CatchPanics = request.CatchPanics ?? _sessionInit.DefaultCatchPanics,
            FollowRedirects = request.FollowRedirects ?? _sessionInit.DefaultFollowRedirects,
            Headers = MergeHeaders(_sessionInit.DefaultHeaders, request.Headers),
            RandomTLSExtensionOrder = request.RandomTLSExtensionOrder ?? _sessionInit.DefaultRandomTLSExtensionOrder,
            RequestBody = request.RequestBody,
            RequestMethod = request.RequestMethod,
            RequestUrl = request.RequestUrl,
            SessionId = _sessionId,
            TimeoutSeconds = _sessionInit.TimeoutSeconds,
            TlsClientIdentifier = request.TlsClientIdentifier ?? _sessionInit.DefaultTlsClientIdentifier
        };
        
        var requestJson = JsonSerializer.Serialize(req, _opts);
        var responsePtr = TlsSession.request(requestJson);
        var responseJson = Marshal.PtrToStringUTF8(responsePtr) ?? "{}";

        var response = JsonSerializer.Deserialize<TlsResponse>(responseJson, _opts);

        if (response == null)
        {
            throw new Exception("received invalid response from tls-client");
        }
        
        freeMemory(response.Id);

        return response;
        
    }

    private static Dictionary<string, string> MergeHeaders(
        Dictionary<string, string> baseHeaders, Dictionary<string, string>? newHeaders)
    {
        if (newHeaders == null)
        {
            return baseHeaders;
        }
        foreach (var headerName in newHeaders.Keys)
        {
            baseHeaders.Remove(headerName);
            baseHeaders.Add(headerName, newHeaders[headerName]);
        }

        return baseHeaders;
    }

    private class CookieResponse
    {
        public string Id { get; set; }
        public TlsCookie[] Cookies { get; set; } 
    }
    
    public TlsCookie[] GetCookies(string url)
    {
        
        var requestJson = JsonSerializer.Serialize(new
        {
            Url = url,
            SessionId = _sessionId
        }, _opts);
        var responsePtr = getCookiesFromSession(requestJson);
        var responseJson = Marshal.PtrToStringAnsi(responsePtr) ?? "{}";
        
        var response = JsonSerializer.Deserialize<CookieResponse>(responseJson, _opts);

        return response?.Cookies ?? throw new Exception("received invalid response from tls-client");
        
    }

    public void Dispose()
    {
        destroySession(_sessionId);
    }
    
}
