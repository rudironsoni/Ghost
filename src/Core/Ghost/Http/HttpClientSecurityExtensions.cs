using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Security.Authentication;

namespace Ghost.Http;

public static class HttpClientSecurityExtensions
{
    public static RemoteCertificateValidationCallback CreateCertificateValidationCallback() =>
        (sender, certificate, chain, sslPolicyErrors) =>
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }

            return false;
        };
    public static HttpClientHandler ConfigureSecureHttpClientHandler(HttpClientHandler handler)
    {
        handler.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;

        handler.AutomaticDecompression = System.Net.DecompressionMethods.All;

        handler.MaxAutomaticRedirections = 10;

        handler.MaxConnectionsPerServer = 100;

        return handler;
    }

    public static HttpClientHandler ConfigureSecureHttpClientHandler(
        HttpClientHandler handler,
        bool ignoreSslErrors = false)
    {
        ConfigureSecureHttpClientHandler(handler);

        if (ignoreSslErrors)
        {
            handler.ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyErrors) => true;
        }

        return handler;
    }

    public static HttpClientHandler ConfigureSecureHttpClientHandler(
        HttpClientHandler handler,
        X509Certificate2? clientCertificate)
    {
        ConfigureSecureHttpClientHandler(handler);

        if (clientCertificate != null)
        {
            handler.ClientCertificateOptions = ClientCertificateOption.Manual;
            handler.ClientCertificates.Add(clientCertificate);
        }

        return handler;
    }
}
