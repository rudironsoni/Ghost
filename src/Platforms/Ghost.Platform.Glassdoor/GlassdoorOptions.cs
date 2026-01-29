using Ghost.Models;

namespace Ghost.Platform.Glassdoor;

    public sealed class GlassdoorOptions
    {
        public bool Enabled { get; set; } = true;
        /// <summary>
        /// When true, the Glassdoor HTTP client will attempt to use the configured proxy provider.
        /// When false, the client will use a direct connection.
        /// </summary>
        public bool ProxyEnabled { get; set; }
        public CountryCode Country { get; set; } = CountryCode.US;
    /// <summary>
    /// Minimum delay between requests in milliseconds.
    /// </summary>
    public int DelayMinMs { get; set; } = 500;
}
