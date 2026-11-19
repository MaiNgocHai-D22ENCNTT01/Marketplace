using System.Net.Security;
using Microsoft.Extensions.Configuration;

namespace Marketplace.Deployment;

internal static class CloudHttp
{
    public static SocketsHttpHandler Create(IConfiguration config, string baseUrl)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 128
        };
        // Explicit opt-in for an exact host only. Never use a global TLS bypass.
        var host = new Uri(baseUrl).DnsSafeHost;
        var allowed = config.GetSection("Http:InsecureTlsHosts").Get<string[]>() ?? [];
        if (config.GetValue<bool>("Http:AllowInvalidInternalCertificates") &&
            allowed.Contains(host, StringComparer.OrdinalIgnoreCase))
            handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        return handler;
    }
}
