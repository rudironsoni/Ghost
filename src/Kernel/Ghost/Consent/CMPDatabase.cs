namespace Ghost.Consent;

/// <summary>
/// Database of known Consent Management Platform (CMP) configurations.
/// Contains detection and acceptance selectors for 25+ CMPs.
/// </summary>
public static class CMPDatabase
{
    /// <summary>
    /// Gets all registered CMP configurations.
    /// </summary>
    public static IReadOnlyList<CMPConfig> GetAllConfigs() => AllConfigs;

    /// <summary>
    /// Gets a specific CMP configuration by name.
    /// </summary>
    /// <param name="name">The CMP identifier.</param>
    /// <returns>The CMP configuration if found, otherwise null.</returns>
    public static CMPConfig? GetConfig(string name)
    {
        return AllConfigs.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly List<CMPConfig> AllConfigs =
    [
        // OneTrust (CookiePro)
        new CMPConfig
        {
            Name = "onetrust-cookiepro",
            Detectors = ["#onetrust-consent-sdk", "#onetrust-banner-sdk", "[class*='onetrust']"],
            AcceptButton = ".cmp-button__accept",
            AlternativeAcceptSelectors = ["#onetrust-accept-btn-handler", "#accept-recommended-btn-handler", "[class*='onetrust'] .accept-btn"]
        },

        // OneTrust (Optanon)
        new CMPConfig
        {
            Name = "onetrust-optanon",
            Detectors = ["#onetrust-banner-sdk", "#optanon-root", "#onetrust-pc-sdk"],
            AcceptButton = ".optanon-allow-all",
            AlternativeAcceptSelectors = ["#accept-recommended-btn-handler", ".save-preference-btn-handler"]
        },

        // OneTrust (CookieLaw)
        new CMPConfig
        {
            Name = "onetrust-cookielaw",
            Detectors = ["#onetrust-banner-sdk", "[id*='onetrust']"],
            AcceptButton = "#cookielaw_accept",
            AlternativeAcceptSelectors = ["#accept-recommended-btn-handler"]
        },

        // CookieBot
        new CMPConfig
        {
            Name = "cookiebot",
            Detectors = ["#CybotCookiebotDialog", "[data-cybot]", "[id*='CookiebotDialog']"],
            AcceptButton = "#CybotCookiebotDialogBodyLevelButtonLevelOptinAllowAll",
            AlternativeAcceptSelectors = ["#CybotCookiebotDialogBodyLevelButtonAccept", "#CybotCookiebotDialog [data-controller*='accept']"]
        },

        // CookieYes
        new CMPConfig
        {
            Name = "cookieyes",
            Detectors = [".cky-consent-container", "#cky-consent-container", "[class*='cookieyes']"],
            AcceptButton = ".cky-btn-accept",
            AlternativeAcceptSelectors = ["[data-cky-tag='accept-button']", ".cky-consent-container button.cky-btn-accept"]
        },

        // UserCentrics
        new CMPConfig
        {
            Name = "usercentrics",
            Detectors = ["#usercentrics-root", "[data-testid='uc-accept-all-button']", "#uc-main-view"],
            AcceptButton = "[data-testid='uc-accept-all-button']",
            AlternativeAcceptSelectors = ["#uc-btn-accept-banner", "cmm-cookie-banner .button--accept-all", "#uc-deny-all-button + .uc-button-primary"]
        },

        // Quantcast
        new CMPConfig
        {
            Name = "quantcast",
            Detectors = ["#qc-cmp2-ui", ".qc-cmp2-persistent-link", "[id*='qc-cmp']"],
            AcceptButton = "#qc-cmp2-ui button[mode='primary']",
            AlternativeAcceptSelectors = [".qc-cmp2-consent-button", ".qc-cmp2-button[label='Accept All']"]
        },

        // Didomi
        new CMPConfig
        {
            Name = "didomi",
            Detectors = ["#didomi-host", ".didomi-popup", "[id*='didomi']"],
            AcceptButton = "#didomi-notice-agree-button",
            AlternativeAcceptSelectors = [".didomi-continue-without-agreeing", ".Cmp__action--yes", "#didomi-policy-accept-all"]
        },

        // CookieFirst
        new CMPConfig
        {
            Name = "cookiefirst",
            Detectors = ["[data-cookiefirst-action]", "#cookiefirst", "#cf-root"],
            AcceptButton = "[data-cookiefirst-action='accept']",
            AlternativeAcceptSelectors = ["#cf-accept", "[data-cf-action='accept']"]
        },

        // Osano
        new CMPConfig
        {
            Name = "osano",
            Detectors = [".osano-cm-window", "#osano-cm"],
            AcceptButton = ".osano-cm-accept-all",
            AlternativeAcceptSelectors = [".osano-cm-btn-accept-all"]
        },

        // TrustArc
        new CMPConfig
        {
            Name = "trustarc",
            Detectors = ["#truste-consent-track", ".trustarc-banner", "[id*='trustarc']"],
            AcceptButton = ".trustarc-agree-btn",
            AlternativeAcceptSelectors = ["#truste-consent-button", ".trustarc-banner button.accept"]
        },

        // Sourcepoint
        new CMPConfig
        {
            Name = "sourcepoint",
            Detectors = ["[id*='sp_message_iframe']", "[class*='sp_message']", "#sp_message"],
            AcceptButton = "button.sp_choice_type_11",
            AlternativeAcceptSelectors = ["[title*='accept' i]", "[title*='agree' i]", "[title*='akzept']", "[title*='ustimmen']"],
            IsIframe = true
        },

        // Google Funding Choices
        new CMPConfig
        {
            Name = "google-funding-choices",
            Detectors = [".fc-cta-consent", ".fc-consent-root", "[aria-label*='consent' i]", "[data-ved*='consent']"],
            AcceptButton = ".fc-cta-consent",
            AlternativeAcceptSelectors = ["button.fc-cta-consent", ".fc-consent-root .fc-button.fc-cta-consent"]
        },

        // Cookiebot (Alternative variant)
        new CMPConfig
        {
            Name = "cookiebot-dialog",
            Detectors = ["#CookiebotDialog", "[data-cookieconsent]"],
            AcceptButton = "#CookiebotDialogBodyButtonAccept",
            AlternativeAcceptSelectors = ["a.CookiebotDialogBodyButton"]
        },

        // Termly
        new CMPConfig
        {
            Name = "termly",
            Detectors = ["#termly-code-snippet-support", "[data-termly]"],
            AcceptButton = "[data-tid='banner-accept']",
            AlternativeAcceptSelectors = ["button[aria-label='Accept']"]
        },

        // Complianz
        new CMPConfig
        {
            Name = "complianz",
            Detectors = [".cmplz-cookiebanner", "#cmplz-cookiebanner"],
            AcceptButton = ".cmplz-accept",
            AlternativeAcceptSelectors = ["button.cmplz-accept-all"]
        },

        // Cookie Notice
        new CMPConfig
        {
            Name = "cookie-notice",
            Detectors = ["#cookie-notice", ".cookie-notice-container"],
            AcceptButton = "#cn-accept-cookie",
            AlternativeAcceptSelectors = [".cn-button.cn-accept-cookie"]
        },

        // Evidon
        new CMPConfig
        {
            Name = "evidon",
            Detectors = ["#_evidon_banner", "[id*='evidon']"],
            AcceptButton = "#_evidon-accept-button",
            AlternativeAcceptSelectors = [".evidon-consent-button"]
        },

        // Iubenda
        new CMPConfig
        {
            Name = "iubenda",
            Detectors = ["#iubenda-cs-banner", ".iubenda-cs-container"],
            AcceptButton = ".iubenda-cs-accept-btn",
            AlternativeAcceptSelectors = ["button[data-iub-action='accept']"]
        },

        // Civic Cookie Control
        new CMPConfig
        {
            Name = "civic-cookie-control",
            Detectors = ["#ccc-notify", ".ccc-widget"],
            AcceptButton = "#ccc-notify-accept",
            AlternativeAcceptSelectors = [".ccc-accept-button"]
        },

        // Crownpeak
        new CMPConfig
        {
            Name = "crownpeak",
            Detectors = ["#evidon-banner", "[data-evidon]"],
            AcceptButton = "#evidon-consent-button",
            AlternativeAcceptSelectors = ["button[data-evidon='accept']"]
        },

        // Cookiehub
        new CMPConfig
        {
            Name = "cookiehub",
            Detectors = ["#cookiehub-dialog", "[data-cookiehub]"],
            AcceptButton = "#cookiehub-accept",
            AlternativeAcceptSelectors = ["button[data-cookiehub='accept']"]
        },

        // LiveRamp
        new CMPConfig
        {
            Name = "liveramp",
            Detectors = ["#ats-privacy", "[data-ats-privacy]"],
            AcceptButton = "#ats-privacy-accept",
            AlternativeAcceptSelectors = ["button[data-ats='accept']"]
        },

        // CookieScript
        new CMPConfig
        {
            Name = "cookiescript",
            Detectors = ["#cookiescript_injected", "[data-cookiescript]"],
            AcceptButton = "#cookiescript_accept",
            AlternativeAcceptSelectors = ["button[data-cs-action='accept']"]
        },

        // Piwik PRO
        new CMPConfig
        {
            Name = "piwik-pro",
            Detectors = ["[data-ppms_cm]", ".ppms_cm"],
            AcceptButton = "[data-ppms_cm='allow-all']",
            AlternativeAcceptSelectors = ["button.ppms_cm_consent_allow_all"]
        },

        // Cookie Information
        new CMPConfig
        {
            Name = "cookie-information",
            Detectors = ["#CookieInformationConsent", "[data-culture-consent]"],
            AcceptButton = "#CookieInformationConsentAccept",
            AlternativeAcceptSelectors = ["button[data-consent-action='accept']"]
        },

        // Generic - Accept All buttons (fallback)
        new CMPConfig
        {
            Name = "generic-accept",
            Detectors = [".cookie-banner", "#cookie-banner", "[class*='cookie-consent']", "[id*='cookie-consent']"],
            AcceptButton = "button[class*='accept']:not([class*='reject']):not([class*='decline'])",
            AlternativeAcceptSelectors =
            [
                "button[id*='accept']:not([id*='reject']):not([id*='decline'])",
                "a[class*='accept']:not([class*='reject']):not([class*='decline'])",
                "[class*='accept-all']",
                "[id*='accept-all']",
                "button:has-text('Accept'):not(:has-text('Reject')):not(:has-text('Decline'))",
                "button:has-text('Agree'):not(:has-text('Disagree'))",
                "button:has-text('OK')",
                "button:has-text('Yes')",
                "button:has-text('Aceptar')",
                "button:has-text('Akzeptieren')",
                "button:has-text('Accepter')",
                "button[aria-label*='accept' i]",
                "[title*='accept' i]",
                "button[title*='Accept' i]",
                "button[title*='Aceptar' i]"
            ]
        },

        // Generic - iframe-based consent (fallback)
        new CMPConfig
        {
            Name = "generic-iframe",
            Detectors = ["iframe[src*='consent']", "iframe[src*='cookie']", "iframe[src*='gdpr']"],
            AcceptButton = "button:has-text('Accept')",
            AlternativeAcceptSelectors = ["button:has-text('Agree')", "button:has-text('OK')"],
            IsIframe = true
        }
    ];
}
