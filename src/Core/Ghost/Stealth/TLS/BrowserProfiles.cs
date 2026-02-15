namespace Ghost.Stealth.TLS;

/// <summary>
/// Provides realistic JA3 profiles based on popular browsers.
/// These profiles are based on actual browser TLS handshakes.
/// </summary>
public static class BrowserProfiles
{
    /// <summary>
    /// Chrome 120+ TLS 1.3 profile.
    /// Based on real Chrome handshake analysis.
    /// </summary>
    public static JA3Profile Chrome120 => new()
    {
        TLSVersion = 771, // TLS 1.2 (Chrome negotiates up to 1.3)
        CipherSuites =
        [
            4865, 4866, 4867,  // TLS_AES_128_GCM_SHA256, TLS_AES_256_GCM_SHA384, TLS_CHACHA20_POLY1305_SHA256
            49195, 49199, 49196, 49200, 52393, 52392,  // ECDHE-RSA/ECDSA variants
            49171, 49172, 156, 157, 47, 53  // Legacy ciphers
        ],
        Extensions =
        [
            0,     // server_name
            23,    // extended_master_secret
            65281, // renegotiation_info
            10,    // supported_groups
            11,    // ec_point_formats
            35,    // session_ticket
            16,    // application_layer_protocol_negotiation
            5,     // status_request
            13,    // signature_algorithms
            18,    // signed_certificate_timestamp
            51,    // key_share
            45,    // psk_key_exchange_modes
            43,    // supported_versions
            27,    // compress_certificate
            17513  // application_settings
        ],
        EllipticCurves =
        [
            29, // X25519
            23, // secp256r1
            24  // secp384r1
        ],
        ECPointFormats = [0] // uncompressed
    };

    /// <summary>
    /// Firefox 121+ TLS 1.3 profile.
    /// Based on real Firefox handshake analysis.
    /// </summary>
    public static JA3Profile Firefox121 => new()
    {
        TLSVersion = 771, // TLS 1.2
        CipherSuites =
        [
            4865, 4866, 4867,  // TLS 1.3 ciphers
            49195, 49199, 52393, 52392,  // ECDHE variants
            49196, 49200, 49162, 49161, 49171, 49172,
            156, 157, 47, 53
        ],
        Extensions =
        [
            0,     // server_name
            23,    // extended_master_secret
            65281, // renegotiation_info
            10,    // supported_groups
            11,    // ec_point_formats
            35,    // session_ticket
            13,    // signature_algorithms
            16,    // application_layer_protocol_negotiation
            5,     // status_request
            51,    // key_share
            43,    // supported_versions
            45,    // psk_key_exchange_modes
            28     // record_size_limit
        ],
        EllipticCurves =
        [
            29, // X25519
            23, // secp256r1
            24, // secp384r1
            25  // secp521r1
        ],
        ECPointFormats = [0] // uncompressed
    };

    /// <summary>
    /// Safari 17+ TLS 1.3 profile.
    /// Based on real Safari handshake analysis.
    /// </summary>
    public static JA3Profile Safari17 => new()
    {
        TLSVersion = 771, // TLS 1.2
        CipherSuites =
        [
            4865, 4866, 4867,  // TLS 1.3 ciphers
            49196, 49195, 52393, 49200, 49199, 52392,
            49162, 49161, 49172, 49171,
            157, 156, 53, 47,
            49160, 49170, 10
        ],
        Extensions =
        [
            0,     // server_name
            23,    // extended_master_secret
            65281, // renegotiation_info
            10,    // supported_groups
            11,    // ec_point_formats
            16,    // application_layer_protocol_negotiation
            35,    // session_ticket
            13,    // signature_algorithms
            5,     // status_request
            18,    // signed_certificate_timestamp
            51,    // key_share
            43,    // supported_versions
            45,    // psk_key_exchange_modes
            21     // padding
        ],
        EllipticCurves =
        [
            29, // X25519
            23, // secp256r1
            24, // secp384r1
            25  // secp521r1
        ],
        ECPointFormats = [0] // uncompressed
    };

    /// <summary>
    /// Edge (Chromium-based) TLS 1.3 profile.
    /// Very similar to Chrome with minor differences.
    /// </summary>
    public static JA3Profile Edge => new()
    {
        TLSVersion = 771, // TLS 1.2
        CipherSuites =
        [
            4865, 4866, 4867,  // TLS 1.3 ciphers
            49195, 49199, 49196, 49200, 52393, 52392,
            49171, 49172, 156, 157, 47, 53
        ],
        Extensions =
        [
            0,     // server_name
            23,    // extended_master_secret
            65281, // renegotiation_info
            10,    // supported_groups
            11,    // ec_point_formats
            35,    // session_ticket
            16,    // application_layer_protocol_negotiation
            5,     // status_request
            13,    // signature_algorithms
            18,    // signed_certificate_timestamp
            51,    // key_share
            45,    // psk_key_exchange_modes
            43,    // supported_versions
            27,    // compress_certificate
            17513  // application_settings
        ],
        EllipticCurves =
        [
            29, // X25519
            23, // secp256r1
            24  // secp384r1
        ],
        ECPointFormats = [0] // uncompressed
    };

    /// <summary>
    /// Gets all available browser profiles.
    /// </summary>
    public static IReadOnlyList<JA3Profile> AllProfiles =>
    [
        Chrome120,
        Firefox121,
        Safari17,
        Edge
    ];

    /// <summary>
    /// Gets a random base profile from available browsers.
    /// </summary>
    public static JA3Profile GetRandomProfile(Random? random = null)
    {
        random ??= Random.Shared;
        IReadOnlyList<JA3Profile> profiles = AllProfiles;
        int index = random.Next(profiles.Count);
        return profiles[index].Clone();
    }
}
