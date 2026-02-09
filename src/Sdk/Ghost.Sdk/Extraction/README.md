# Link Extractors

Link extractors provide automated URL discovery from HTML content, enabling crawlers to navigate websites systematically.

## Features

- **Multiple Extraction Strategies**:
  - `RegexLinkExtractor`: Fast regex-based extraction
  - `HtmlAgilityLinkExtractor`: Robust HTML parsing with XPath support

- **Filtering Options**:
  - Extension filtering (allow/deny)
  - Domain restrictions
  - XPath/CSS selector constraints
  - Fragment stripping
  - Duplicate removal

## Usage

### Basic Example

```csharp
using Ghost.Sdk.Extraction;

// Simple extraction
var extractor = new HtmlAgilityLinkExtractor();
var links = extractor.ExtractLinks(html, "https://example.com");

// With filters
var options = new LinkExtractorOptions
{
    DenyExtensions = new[] { ".jpg", ".png", ".pdf" },
    AllowedDomains = new[] { "example.com" },
    StripFragments = true,
    UniqueOnly = true
};
var filteredExtractor = new HtmlAgilityLinkExtractor(options);
var filteredLinks = filteredExtractor.ExtractLinks(html, "https://example.com");
```

### XPath Restrictions

Extract links only from specific page regions:

```csharp
var options = new LinkExtractorOptions
{
    RestrictXpaths = new[] { "//div[@id='main-content']", "//nav" }
};
var extractor = new HtmlAgilityLinkExtractor(options);
```

## Implementation

- **ILinkExtractor**: Core interface for all extractors
- **RegexLinkExtractor**: Uses compiled regex patterns for speed
- **HtmlAgilityLinkExtractor**: Uses HtmlAgilityPack for accurate HTML parsing
- **LinkExtractorOptions**: Configuration for filtering and extraction behavior

## Edge Cases Handled

- Malformed HTML
- Relative URLs (resolved to absolute)
- Fragment-only links (filtered out)
- JavaScript links (filtered out)
- Empty or whitespace-only hrefs
- Duplicate URLs
- Case-insensitive extension matching
- Subdomain matching for domain filters
