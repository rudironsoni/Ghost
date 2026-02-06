using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ghost.Net;

public class Socks5Bridge : IDisposable
{
    private readonly string _upstreamHost;
    private readonly int _upstreamPort;
    private readonly string? _username;
    private readonly string? _password;

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoopTask;

    private readonly List<TcpClient> _activeClients = new List<TcpClient>();
    private readonly object _lock = new object();

    public int Port { get; private set; }

    public Socks5Bridge(string upstreamHost, int upstreamPort, string? username, string? password)
    {
        _upstreamHost = upstreamHost ?? throw new ArgumentNullException(nameof(upstreamHost));
        _upstreamPort = upstreamPort;
        _username = username;
        _password = password;
    }

    public void Start()
    {
        if (_listener != null) throw new InvalidOperationException("Already started");

        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();

        if (_listener.LocalEndpoint is IPEndPoint ep)
        {
            Port = ep.Port;
        }

        var token = _cts.Token;
        _acceptLoopTask = AcceptLoopAsync(token);
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
        }
        catch { }

        try
        {
            _listener?.Stop();
        }
        catch { }

        lock (_lock)
        {
            foreach (var c in _activeClients.ToArray())
            {
                try { c.Close(); } catch { }
            }
            _activeClients.Clear();
        }

        try
        {
            _acceptLoopTask?.GetAwaiter().GetResult();
        }
        catch { }
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        if (_listener == null) return;

        while (!ct.IsCancellationRequested)
        {
            TcpClient? client = null;
            try
            {
                client = await _listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception)
            {
                // swallow and continue
                await Task.Delay(100, ct).ConfigureAwait(false);
                continue;
            }

            if (client == null) continue;

            lock (_lock) { _activeClients.Add(client); }

            _ = Task.Run(async () =>
            {
                try
                {
                    await HandleClientAsync(client, ct).ConfigureAwait(false);
                }
                catch { }
                finally
                {
                    try { client.Close(); } catch { }
                    lock (_lock) { _activeClients.Remove(client); }
                }
            }, ct);
        }
    }

    private static async Task<byte[]> ReadExactlyAsync(Stream stream, int count, CancellationToken ct)
    {
        var buffer = new byte[count];
        var offset = 0;
        while (offset < count)
        {
            var mem = new Memory<byte>(buffer, offset, count - offset);
            int read = await stream.ReadAsync(mem, ct).ConfigureAwait(false);
            if (read == 0) throw new EndOfStreamException("Stream closed");
            offset += read;
        }
        return buffer;
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            var clientStream = client.GetStream();

            // Client Handshake
            byte[] header;
            try
            {
                header = await ReadExactlyAsync(clientStream, 2, ct).ConfigureAwait(false);
            }
            catch
            {
                return;
            }

            if (header.Length < 2 || header[0] != 0x05)
            {
                return;
            }

            var nmethods = header[1];
            try
            {
                var methods = await ReadExactlyAsync(clientStream, nmethods, ct).ConfigureAwait(false);
            }
            catch
            {
                return;
            }

            // Reply: no auth required
            try
            {
                var resp = new byte[] { 0x05, 0x00 };
                await clientStream.WriteAsync(resp.AsMemory(0, 2), ct).ConfigureAwait(false);
            }
            catch
            {
                return;
            }

            // Read client request
            byte[] reqHeader;
            try
            {
                reqHeader = await ReadExactlyAsync(clientStream, 4, ct).ConfigureAwait(false);
            }
            catch
            {
                return;
            }

            if (reqHeader[0] != 0x05)
            {
                return;
            }

            var atyp = reqHeader[3];
            byte[] addr;
            try
            {
                switch (atyp)
                {
                    case 0x01: // IPv4
                        addr = await ReadExactlyAsync(clientStream, 4, ct).ConfigureAwait(false);
                        break;
                    case 0x03: // Domain
                        var lenBuf = await ReadExactlyAsync(clientStream, 1, ct).ConfigureAwait(false);
                        var len = lenBuf[0];
                        var name = await ReadExactlyAsync(clientStream, len, ct).ConfigureAwait(false);
                        addr = new byte[1 + len];
                        addr[0] = len;
                        Buffer.BlockCopy(name, 0, addr, 1, len);
                        break;
                    case 0x04: // IPv6
                        addr = await ReadExactlyAsync(clientStream, 16, ct).ConfigureAwait(false);
                        break;
                    default:
                        return;
                }

                var portBytes = await ReadExactlyAsync(clientStream, 2, ct).ConfigureAwait(false);

                // reconstruct request bytes
                using (var ms = new MemoryStream())
                {
                    ms.Write(reqHeader, 0, reqHeader.Length);
                    ms.Write(addr, 0, addr.Length);
                    ms.Write(portBytes, 0, 2);
                    var requestBytes = ms.ToArray();

                    // Connect to upstream
                    using (var upstream = new TcpClient())
                    {
                        try
                        {
                            await upstream.ConnectAsync(_upstreamHost, _upstreamPort).ConfigureAwait(false);
                        }
                        catch
                        {
                            // can't connect upstream, reply to client with general failure
                            try
                            {
                                var fail = new byte[] { 0x05, 0x01, 0x00, 0x01, 0, 0, 0, 0, 0, 0 };
                                await clientStream.WriteAsync(fail.AsMemory(0, fail.Length), ct).ConfigureAwait(false);
                            }
                            catch { }
                            return;
                        }

                        lock (_lock) { _activeClients.Add(upstream); }
                        try
                        {
                            var upStream = upstream.GetStream();

                            // Upstream handshake: offer both no-auth (0x00) and user/pass (0x02)
                            // Let the server choose the method it prefers
                            byte[] handshake;
                            if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
                            {
                                // Offer both methods: 0x00 (no auth) and 0x02 (username/password)
                                handshake = new byte[] { 0x05, 0x02, 0x00, 0x02 };
                            }
                            else
                            {
                                // Only offer no-auth method
                                handshake = new byte[] { 0x05, 0x01, 0x00 };
                            }

                            await upStream.WriteAsync(handshake.AsMemory(0, handshake.Length), ct).ConfigureAwait(false);
                            var methodResp = await ReadExactlyAsync(upStream, 2, ct).ConfigureAwait(false);
                            if (methodResp.Length < 2 || methodResp[0] != 0x05)
                            {
                                return;
                            }

                            var selectedMethod = methodResp[1];

                            // If server selected 0xFF, no acceptable methods
                            if (selectedMethod == 0xFF)
                            {
                                return;
                            }

                            // If server selected method 0x02 (username/password), authenticate
                            if (selectedMethod == 0x02)
                            {
                                if (string.IsNullOrEmpty(_username) || string.IsNullOrEmpty(_password))
                                {
                                    // Server requires auth but we don't have credentials
                                    return;
                                }

                                var userBytes = Encoding.ASCII.GetBytes(_username);
                                var passBytes = Encoding.ASCII.GetBytes(_password);
                                var auth = new List<byte> { 0x01, (byte)userBytes.Length };
                                auth.AddRange(userBytes);
                                auth.Add((byte)passBytes.Length);
                                auth.AddRange(passBytes);
                                var authBuf = auth.ToArray();
                                await upStream.WriteAsync(authBuf.AsMemory(0, authBuf.Length), ct).ConfigureAwait(false);

                                var authResp = await ReadExactlyAsync(upStream, 2, ct).ConfigureAwait(false);
                                if (authResp.Length < 2 || authResp[0] != 0x01 || authResp[1] != 0x00)
                                {
                                    // Authentication failed
                                    return;
                                }
                            }
                            // If server selected method 0x00 (no auth), proceed without authentication

                            // Forward client's request to upstream
                            await upStream.WriteAsync(requestBytes.AsMemory(0, requestBytes.Length), ct).ConfigureAwait(false);

                            // Read upstream response and forward back to client
                            var respHeader = await ReadExactlyAsync(upStream, 4, ct).ConfigureAwait(false);
                            var respAtyp = respHeader[3];
                            byte[] respAddr;
                            switch (respAtyp)
                            {
                                case 0x01:
                                    respAddr = await ReadExactlyAsync(upStream, 4, ct).ConfigureAwait(false);
                                    break;
                                case 0x03:
                                    var l = await ReadExactlyAsync(upStream, 1, ct).ConfigureAwait(false);
                                    var ln = l[0];
                                    var nm = await ReadExactlyAsync(upStream, ln, ct).ConfigureAwait(false);
                                    respAddr = new byte[1 + ln];
                                    respAddr[0] = ln;
                                    Buffer.BlockCopy(nm, 0, respAddr, 1, ln);
                                    break;
                                case 0x04:
                                    respAddr = await ReadExactlyAsync(upStream, 16, ct).ConfigureAwait(false);
                                    break;
                                default:
                                    return;
                            }
                            var respPort = await ReadExactlyAsync(upStream, 2, ct).ConfigureAwait(false);

                            using (var ms2 = new MemoryStream())
                            {
                                ms2.Write(respHeader, 0, respHeader.Length);
                                ms2.Write(respAddr, 0, respAddr.Length);
                                ms2.Write(respPort, 0, 2);
                                var respBytes = ms2.ToArray();
                                await clientStream.WriteAsync(respBytes.AsMemory(0, respBytes.Length), ct).ConfigureAwait(false);
                            }

                            // If upstream accepted (REP == 0), start tunnelling
                            if (respHeader[1] == 0x00)
                            {
                                var upstreamToClient = upStream.CopyToAsync(clientStream, ct);
                                var clientToUpstream = clientStream.CopyToAsync(upStream, ct);
                                await Task.WhenAny(upstreamToClient, clientToUpstream).ConfigureAwait(false);
                            }
                        }
                        finally
                        {
                            try { upstream.Close(); } catch { }
                            lock (_lock) { _activeClients.Remove(upstream); }
                        }
                    }
                }
            }
            catch
            {
                return;
            }
        }
    }

    public void Dispose()
    {
        Stop();
        try { _cts?.Dispose(); } catch { }
        GC.SuppressFinalize(this);
    }
}
