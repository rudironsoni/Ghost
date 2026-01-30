# API Credential Requirements: InfoJobs & Tecnoempleo

Date: 2026-01-31

This document explains why the InfoJobs and Tecnoempleo platforms require real API credentials to function, how to obtain them, and how to configure the project to use them. Do NOT add real credentials to source control. This file intentionally contains only placeholder formats.

---

Overview
--------
- Both InfoJobs and Tecnoempleo use HTTP APIs that require Basic Authentication using a ClientId and ClientSecret pair.
- There are no public or test credentials available in the wild (search results confirmed no public keys on GitHub).
- Using placeholder or fake credentials results in server errors (HTTP 500) or blocked responses; these endpoints expect a valid, registered client.

Why real credentials are required
-------------------------------
- Authenticated endpoints: The API clients in this repository attach a Basic Authorization header when ClientId and ClientSecret are provided. See implementation:
  - src/Platforms/Ghost.Platform.InfoJobs/Jobs/Internal/InfoJobsApiClient.cs
  - src/Platforms/Ghost.Platform.Tecnoempleo/Jobs/Internal/TecnoempleoApiClient.cs
- The platforms validate client credentials and in many cases gate access to production endpoints behind those credentials. Requests without valid credentials either return errors or empty/blocked responses.
- Cloud/anti-bot defenses and server-side validation make unauthenticated HTTP requests unreliable (e.g., Indeed was blocked by Cloudflare during testing).

Observed behaviour with placeholder credentials
---------------------------------------------
- Placeholder credentials produce server-side errors. From earlier runs and developer notes:
  - Both InfoJobs and Tecnoempleo returned HTTP 500 when using fake or placeholder ClientId/ClientSecret.
  - The repository's logs/api_credentials_search.md notes Cloudflare blocking for Indeed and no public creds for InfoJobs/Tecnoempleo.

Relevant implementation details
------------------------------
- InfoJobs client (InfoJobsApiClient.cs):
  - Adds Authorization: Basic <Base64(ClientId:ClientSecret)> when options contain credentials.
  - Adds platform-specific headers from InfoJobsConstants.ApiHeaders.
  - Constructs search URL and parses JSON responses; if response is empty, it logs and returns no results.

- Tecnoempleo client (TecnoempleoApiClient.cs):
  - Also sends Authorization: Basic <Base64(ClientId:ClientSecret)> when options are configured.
  - Expects successful HTTP status codes (calls EnsureSuccessStatusCode()).
  - If credentials are invalid/missing, the request will not succeed and exceptions are thrown/logged.

Registration & obtaining credentials
-----------------------------------

InfoJobs
- Developer/Partner registration: InfoJobs provides API access to partners and registered clients. Typically you must register an application to obtain a ClientId and ClientSecret.
- Registration URL (Spain / corporate): https://www.infojobs.net/empresas
- Developer docs (general starting point): https://www.infojobs.net/empresas/servicios-para-empresas

Tecnoempleo
- Developer/Partner registration: Tecnoempleo provides API access to partners; client credentials must be requested via their business/developer channel.
- Main site / contact: https://www.tecnoempleo.com/
- There is no public developer portal with instant API key issuance; contact sales/partnerships or the technical contact listed on their site.

Notes about public/test credentials
----------------------------------
- No public/test credentials were found in repository searches. It is standard practice for these platforms not to publish API keys publicly.
- Do NOT use fake or randomly generated credentials — doing so causes server errors or blocked responses.

Known bug fixes
---------------
- A previous issue with Basic Auth in the Tecnoempleo client was fixed in this repository. However, even after fixing the Basic Auth implementation, valid credentials are still required for successful API responses.

Example error messages
----------------------
- Using placeholder credentials commonly produced server-side errors in tests; typical observations included:
  - HTTP 500 Internal Server Error
  - Response body empty or not containing expected JSON (client logs `Received empty response`)
  - Exceptions thrown from EnsureSuccessStatusCode() in Tecnoempleo client

Configuration: .env.example placeholders
--------------------------------------
Add the following placeholders to your .env.example (DO NOT commit real credentials):

INFOJOBS_CLIENT_ID=your-infojobs-client-id
INFOJOBS_CLIENT_SECRET=your-infojobs-client-secret
INFOJOBS_API_ENDPOINT=https://api.infojobs.net/
INFOJOBS_LANGUAGE=es-ES

TECNOEMPLEO_CLIENT_ID=your-tecnoempleo-client-id
TECNOEMPLEO_CLIENT_SECRET=your-tecnoempleo-client-secret
TECNOEMPLEO_API_BASE=https://www.tecnoempleo.com

Example usage (local development)
---------------------------------
1. Register and obtain credentials from the platform (see registration URLs above).
2. Create a .env file from .env.example and fill in the ClientId/ClientSecret values.
3. Restart the application so DI/options pick up the new environment variables.

Security best practices
-----------------------
- Never store real credentials in source control.
- Use secrets managers (Azure Key Vault, GitHub Secrets, Vault) for production deployments.
- Limit credential scope and rotate keys periodically if supported by the provider.

If you cannot obtain credentials
-------------------------------
- Implement a browser-based fallback using the Ghost kernel (headless browser automation) similar to the LinkedIn implementation. This approach requires careful handling of anti-bot defenses and legal considerations.
- Note: Browser-based scraping is a fallback and may be blocked by Cloudflare or platform protections. Always check Terms of Service before scraping.

Appendix: Internal references
---------------------------
- Code: src/Platforms/Ghost.Platform.InfoJobs/Jobs/Internal/InfoJobsApiClient.cs
- Code: src/Platforms/Ghost.Platform.Tecnoempleo/Jobs/Internal/TecnoempleoApiClient.cs
- Search results: logs/api_credentials_search.md
