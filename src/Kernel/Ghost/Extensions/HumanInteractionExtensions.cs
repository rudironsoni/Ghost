using System;
using System.Threading;
using System.Threading.Tasks;
using Ghost;

namespace Ghost.Extensions;

/// <summary>
/// Extensions for simulating human-like interaction.
/// </summary>
public static class HumanInteractionExtensions
{
    private static readonly Random _random = new Random();

    /// <summary>
    /// Performs a click with human-like timing and hesitation.
    /// </summary>
    public static async Task HumanClickAsync(this IElement element, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(element);

        // 1. Scroll into view if needed
        await element.ScrollIntoViewAsync(ct).ConfigureAwait(false);

        // 2. Hover first with a small delay
        await element.HoverAsync(ct).ConfigureAwait(false);

        // 3. Random micro-delay (50-250ms) to simulate reaction/verification time
        await Task.Delay(_random.Next(50, 250), ct).ConfigureAwait(false);

        // 4. Click
        await element.ClickAsync(ct: ct).ConfigureAwait(false);

        // 5. Post-click delay (user usually waits or moves mouse away)
        // We add a tiny delay to prevent instant subsequent actions
        await Task.Delay(_random.Next(100, 300), ct).ConfigureAwait(false);
    }
}
