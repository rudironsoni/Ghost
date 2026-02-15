using Microsoft.Playwright;

namespace Ghost.Consent;

/// <summary>
/// Helper for detecting and interacting with elements inside Shadow DOM.
/// Many modern CMPs use Shadow DOM to encapsulate their UI components.
/// </summary>
public static class ShadowDOMHelper
{
    /// <summary>
    /// Searches for an element within all shadow roots on the page.
    /// </summary>
    /// <param name="page">The page to search.</param>
    /// <param name="selector">CSS selector to find within shadow roots.</param>
    /// <returns>True if element found and visible, otherwise false.</returns>
    public static async Task<bool> FindInShadowDOMAsync(IPage page, string selector)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(selector);

        try
        {
            // Use Playwright's piercing selector to search through shadow DOMs
            IElement? element = await page.QuerySelectorAsync($"pierce/{selector}").ConfigureAwait(false);
            if (element != null)
            {
                return await element.IsVisibleAsync().ConfigureAwait(false);
            }
        }
        catch
        {
            // Fallback to JavaScript evaluation
            try
            {
                bool found = await page.EvaluateAsync<bool>($@"
                    () => {{
                        const findInShadow = (root, selector) => {{
                            // Try direct query first
                            const element = root.querySelector(selector);
                            if (element) return element;

                            // Recursively search shadow roots
                            const allElements = root.querySelectorAll('*');
                            for (const el of allElements) {{
                                if (el.shadowRoot) {{
                                    const found = findInShadow(el.shadowRoot, selector);
                                    if (found) return found;
                                }}
                            }}
                            return null;
                        }};

                        const element = findInShadow(document, '{selector.Replace("'", "\\'")}');
                        if (!element) return false;

                        // Check visibility
                        const style = window.getComputedStyle(element);
                        return style.display !== 'none' &&
                               style.visibility !== 'hidden' &&
                               style.opacity !== '0' &&
                               element.offsetWidth > 0 &&
                               element.offsetHeight > 0;
                    }}
                ").ConfigureAwait(false);
                return found;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Clicks an element inside a shadow DOM.
    /// </summary>
    /// <param name="page">The page containing the element.</param>
    /// <param name="selector">CSS selector to find within shadow roots.</param>
    /// <returns>True if successfully clicked, otherwise false.</returns>
    public static async Task<bool> ClickInShadowDOMAsync(IPage page, string selector)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(selector);

        try
        {
            // Try Playwright's piercing selector first
            IElement? element = await page.QuerySelectorAsync($"pierce/{selector}").ConfigureAwait(false);
            if (element != null)
            {
                await element.ClickAsync().ConfigureAwait(false);
                return true;
            }
        }
        catch
        {
            // Fallback to JavaScript click
            try
            {
                bool clicked = await page.EvaluateAsync<bool>($@"
                    () => {{
                        const findInShadow = (root, selector) => {{
                            const element = root.querySelector(selector);
                            if (element) return element;

                            const allElements = root.querySelectorAll('*');
                            for (const el of allElements) {{
                                if (el.shadowRoot) {{
                                    const found = findInShadow(el.shadowRoot, selector);
                                    if (found) return found;
                                }}
                            }}
                            return null;
                        }};

                        const element = findInShadow(document, '{selector.Replace("'", "\\'")}');
                        if (element) {{
                            element.click();
                            return true;
                        }}
                        return false;
                    }}
                ").ConfigureAwait(false);
                return clicked;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets all shadow roots on the page and their host elements.
    /// Useful for debugging shadow DOM structures.
    /// </summary>
    /// <param name="page">The page to search.</param>
    /// <returns>Count of shadow roots found.</returns>
    public static async Task<int> GetShadowRootCountAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        try
        {
            int count = await page.EvaluateAsync<int>(@"
                () => {
                    const countShadowRoots = (root) => {
                        let count = 0;
                        const allElements = root.querySelectorAll('*');
                        for (const el of allElements) {
                            if (el.shadowRoot) {
                                count++;
                                count += countShadowRoots(el.shadowRoot);
                            }
                        }
                        return count;
                    };
                    return countShadowRoots(document);
                }
            ").ConfigureAwait(false);
            return count;
        }
        catch
        {
            return 0;
        }
    }
}
