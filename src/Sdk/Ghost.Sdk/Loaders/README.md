# Item Loaders

Item Loaders provide a declarative way to extract and transform scraped data in Ghost.

## Overview

Item loaders separate extraction logic from transformation logic, making your code cleaner and more maintainable.

## Features

- **Fluent API**: Chainable methods for building extraction pipelines
- **XPath Support**: Extract data using XPath selectors
- **CSS Selectors**: Extract data using CSS selectors (basic implementation)
- **Static Values**: Add static values to items
- **Processor Pipeline**: Chain transformation functions
- **Built-in Processors**: Common transformations like Strip, ToLower, Replace, etc.

## Quick Start

```csharp
using Ghost.Sdk.Loaders;

// Define your item class
public class Product
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Price { get; set; }
}

// Create a loader and define extraction rules
var product = new ItemLoader<Product>()
    .AddXPath("Name", "//h1")
    .AddProcessor("Name", ItemLoaderProcessors.Strip())
    
    .AddCss("Description", ".description")
    .AddProcessor("Description", ItemLoaderProcessors.StripHtml())
    .AddProcessor("Description", ItemLoaderProcessors.NormalizeWhitespace())
    
    .AddCss("Price", ".price")
    .AddProcessor("Price", ItemLoaderProcessors.Strip())
    .AddProcessor("Price", ItemLoaderProcessors.RegexExtract(@"\d+\.\d+"))
    
    .LoadItem(html);
```

## Built-in Processors

### Strip()
Removes leading and trailing whitespace.

```csharp
.AddProcessor("Name", ItemLoaderProcessors.Strip())
```

### ToLower() / ToUpper()
Converts text to lowercase or uppercase.

```csharp
.AddProcessor("Name", ItemLoaderProcessors.ToLower())
```

### Join(separator)
Replaces commas with a custom separator when multiple values are extracted.

```csharp
.AddProcessor("Categories", ItemLoaderProcessors.Join(" > "))
```

### Take(count)
Truncates string to specified number of characters.

```csharp
.AddProcessor("Summary", ItemLoaderProcessors.Take(100))
```

### Replace(oldValue, newValue)
Replaces all occurrences of a substring.

```csharp
.AddProcessor("Price", ItemLoaderProcessors.Replace("$", ""))
```

### RegexExtract(pattern)
Extracts the first match of a regular expression.

```csharp
.AddProcessor("Sku", ItemLoaderProcessors.RegexExtract(@"SKU:\s*(\w+)"))
```

### StripHtml()
Removes HTML tags from content.

```csharp
.AddProcessor("Description", ItemLoaderProcessors.StripHtml())
```

### NormalizeWhitespace()
Normalizes multiple spaces to single space and trims.

```csharp
.AddProcessor("Description", ItemLoaderProcessors.NormalizeWhitespace())
```

### DefaultIfEmpty(defaultValue)
Provides a default value if the extracted value is null or empty.

```csharp
.AddProcessor("Status", ItemLoaderProcessors.DefaultIfEmpty("Available"))
```

## Extraction Methods

### AddXPath(field, xpath)
Extracts data using XPath selector.

```csharp
.AddXPath("Title", "//h1[@class='product-title']")
```

### AddCss(field, selector)
Extracts data using CSS selector (basic implementation).

```csharp
.AddCss("Price", ".price")
.AddCss("Title", "#product-title")
```

### AddValue(field, value)
Adds a static value to a field.

```csharp
.AddValue("Category", "Electronics")
```

## Multiple Extractors

You can add multiple extractors for the same field. Values will be concatenated with commas.

```csharp
.AddXPath("Category", "//span[@class='category-main']")
.AddXPath("Category", "//span[@class='category-sub']")
// Results in: "Main Category, Sub Category"
```

## Processor Chaining

Processors are applied in the order they are added:

```csharp
.AddXPath("Title", "//h1")
.AddProcessor("Title", ItemLoaderProcessors.StripHtml())
.AddProcessor("Title", ItemLoaderProcessors.Strip())
.AddProcessor("Title", ItemLoaderProcessors.ToLower())
// Applies: HTML stripping → whitespace trimming → lowercase conversion
```

## Custom Processors

You can create custom processors as functions:

```csharp
.AddProcessor("Price", price => 
{
    // Remove currency symbols and convert to decimal
    var cleaned = price.Replace("$", "").Replace(",", "");
    return decimal.TryParse(cleaned, out var value) 
        ? value.ToString("F2") 
        : "0.00";
})
```

## Testing

The Loaders module includes comprehensive tests:

- **ItemLoaderTests.cs**: Unit tests for extraction and processing
- **ItemLoaderProcessorsTests.cs**: Tests for all built-in processors
- **ItemLoaderIntegrationTests.cs**: Real-world HTML extraction scenarios

Run tests:
```bash
./tests/scripts/run-tests.sh tests/Sdk/Ghost.Sdk.Tests --filter "FullyQualifiedName~Loaders"
```

## Notes

- CSS selector support is basic (class, ID, element selectors). For complex selectors, use XPath.
- Processors receive and return strings. Type conversion happens at property assignment.
- LoadItems() currently returns a single-item list. Multi-item extraction will be added in future versions.
