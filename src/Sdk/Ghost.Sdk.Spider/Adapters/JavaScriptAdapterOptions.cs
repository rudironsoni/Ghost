using Ghost.Sdk.Spider.Adapters.Contracts;

namespace Ghost.Sdk.Spider.Adapters;

/// <summary>
/// Configuration options specific to the JavaScriptAdapter.
/// </summary>
/// <remarks>
/// This class extends the base <see cref="AdapterOptions"/> with browser-specific
/// configuration options for rendering JavaScript-heavy pages using Playwright.
/// </remarks>
public class JavaScriptAdapterOptions : AdapterOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to run the browser in headless mode.
    /// </summary>
    /// <value>
    /// <c>true</c> to run headless (no UI); otherwise, <c>false</c>.
    /// Defaults to <c>true</c>.
    /// </value>
    public bool Headless { get; set; } = true;

    /// <summary>
    /// Gets or sets the browser viewport width.
    /// </summary>
    /// <value>The viewport width in pixels. Defaults to 1920.</value>
    public int ViewportWidth { get; set; } = 1920;

    /// <summary>
    /// Gets or sets the browser viewport height.
    /// </summary>
    /// <value>The viewport height in pixels. Defaults to 1080.</value>
    public int ViewportHeight { get; set; } = 1080;

    /// <summary>
    /// Gets or sets a value indicating whether to enable JavaScript.
    /// </summary>
    /// <value>
    /// <c>true</c> to enable JavaScript execution; otherwise, <c>false</c>.
    /// Defaults to <c>true</c>.
    /// </value>
    public bool JavaScriptEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to block images.
    /// </summary>
    /// <value>
    /// <c>true</c> to block image loading for faster page loads; otherwise, <c>false</c>.
    /// Defaults to <c>false</c>.
    /// </value>
    public bool BlockImages { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to block CSS.
    /// </summary>
    /// <value>
    /// <c>true</c> to block CSS loading; otherwise, <c>false</c>.
    /// Defaults to <c>false</c>.
    /// </value>
    public bool BlockCss { get; set; }

    /// <summary>
    /// Gets or sets the wait condition after page load.
    /// </summary>
    /// <value>The wait condition. Defaults to "networkidle".</value>
    /// <remarks>
    /// Valid values: "load", "domcontentloaded", "networkidle"
    /// </remarks>
    public string WaitUntil { get; set; } = "networkidle";

    /// <summary>
    /// Gets or sets additional browser launch arguments.
    /// </summary>
    /// <value>A list of command-line arguments for the browser.</value>
    /// <remarks>
    /// Common arguments:
    /// - "--disable-gpu": Disable GPU hardware acceleration
    /// - "--no-sandbox": Disable sandbox (required in some Docker environments)
    /// - "--disable-dev-shm-usage": Overcome limited resource problems
    /// </remarks>
    public List<string> BrowserArgs { get; set; } = new()
    {
        "--no-sandbox",
        "--disable-setuid-sandbox",
        "--disable-dev-shm-usage"
    };

    /// <summary>
    /// Gets or sets a value indicating whether to take screenshots on errors.
    /// </summary>
    /// <value>
    /// <c>true</c> to capture screenshots when errors occur; otherwise, <c>false</c>.
    /// Defaults to <c>false</c>.
    /// </value>
    public bool ScreenshotOnError { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="JavaScriptAdapterOptions"/> class.
    /// </summary>
    public JavaScriptAdapterOptions()
    {
    }

    /// <summary>
    /// Validates the JavaScriptAdapter-specific options.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when configuration values are invalid.</exception>
    public override void Validate()
    {
        base.Validate();

        if (ViewportWidth <= 0)
        {
            throw new ArgumentException("ViewportWidth must be greater than zero.", nameof(ViewportWidth));
        }

        if (ViewportHeight <= 0)
        {
            throw new ArgumentException("ViewportHeight must be greater than zero.", nameof(ViewportHeight));
        }

        if (string.IsNullOrWhiteSpace(WaitUntil))
        {
            throw new ArgumentException("WaitUntil cannot be null or whitespace.", nameof(WaitUntil));
        }

        string[] validWaitStates = new[] { "load", "domcontentloaded", "networkidle" };
        if (!validWaitStates.Contains(WaitUntil.ToLowerInvariant()))
        {
            throw new ArgumentException(
                $"WaitUntil must be one of: {string.Join(", ", validWaitStates)}",
                nameof(WaitUntil));
        }
    }

    /// <summary>
    /// Creates a copy of the current options instance.
    /// </summary>
    /// <returns>A new instance with the same configuration values.</returns>
    public override AdapterOptions Clone()
    {
        var clone = (JavaScriptAdapterOptions)base.Clone();
        clone.BrowserArgs = new List<string>(BrowserArgs);
        return clone;
    }
}
