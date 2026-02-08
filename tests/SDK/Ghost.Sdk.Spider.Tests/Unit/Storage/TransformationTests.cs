using FluentAssertions;
using Ghost.Sdk.Spider.Storage.Contracts;
using Xunit;
using System.Text.RegularExpressions;

namespace Ghost.Sdk.Spider.Tests.Unit.Storage;

/// <summary>
/// Tests for data transformation operations in the storage pipeline.
/// These tests verify various transformation strategies applied before storage.
/// </summary>
public class TransformationTests
{
    [Fact]
    public async Task NormalizeTransformation_ShouldTrimAndLowercase()
    {
        // Arrange
        var transformation = new NormalizeTransformation();
        var item = new
        {
            Title = "  Test Product  ",
            Description = "  DESCRIPTION WITH SPACES  "
        };
        var context = StorageContext.Create("TestSpider");

        // Act
        var result = await transformation.TransformAsync(item, context);

        // Assert
        result.Should().NotBeNull();
        var resultDict = GetProperties(result);
        resultDict["Title"].Should().Be("test product");
        resultDict["Description"].Should().Be("description with spaces");
    }

    [Fact]
    public async Task FilterTransformation_WithPredicate_ShouldFilterCorrectly()
    {
        // Arrange
        var transformation = new FilterTransformation(item =>
        {
            dynamic? d = item;
            return d?.Price > 100;
        });

        var expensiveItem = new { Title = "Expensive", Price = 150 };
        var cheapItem = new { Title = "Cheap", Price = 50 };
        var context = StorageContext.Create("FilterSpider");

        // Act
        var result1 = await transformation.ShouldIncludeAsync(expensiveItem, context);
        var result2 = await transformation.ShouldIncludeAsync(cheapItem, context);

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeFalse();
    }

    [Fact]
    public async Task EnrichmentTransformation_ShouldAddMetadata()
    {
        // Arrange
        var transformation = new EnrichmentTransformation();
        var item = new { Title = "Test", Price = 99.99 };
        var context = new StorageContext
        {
            SpiderName = "EnrichSpider",
            SourceUrl = "https://example.com/page"
        };

        // Act
        var result = await transformation.TransformAsync(item, context);

        // Assert
        result.Should().NotBeNull();
        var resultDict = GetProperties(result);
        resultDict.Should().ContainKey("_spider");
        resultDict.Should().ContainKey("_source_url");
        resultDict.Should().ContainKey("_extracted_at");
        resultDict["_spider"].Should().Be("EnrichSpider");
    }

    [Fact]
    public async Task CleanHtmlTransformation_ShouldRemoveHtmlTags()
    {
        // Arrange
        var transformation = new CleanHtmlTransformation();
        var item = new
        {
            Title = "<h1>Title</h1>",
            Description = "<p>This is a <strong>test</strong> description.</p>"
        };
        var context = StorageContext.Create("CleanSpider");

        // Act
        var result = await transformation.TransformAsync(item, context);

        // Assert
        result.Should().NotBeNull();
        var resultDict = GetProperties(result);
        resultDict["Title"].Should().Be("Title");
        resultDict["Description"].Should().Be("This is a test description.");
    }

    [Fact]
    public async Task PriceNormalizationTransformation_ShouldNormalizePrices()
    {
        // Arrange
        var transformation = new PriceNormalizationTransformation();
        var item = new
        {
            Price = "$1,234.56",
            OriginalPrice = "€99.99"
        };
        var context = StorageContext.Create("PriceSpider");

        // Act
        var result = await transformation.TransformAsync(item, context);

        // Assert
        result.Should().NotBeNull();
        var resultDict = GetProperties(result);
        resultDict["Price"].Should().Be(1234.56m);
        resultDict["OriginalPrice"].Should().Be(99.99m);
    }

    [Fact]
    public async Task DateNormalizationTransformation_ShouldNormalizeDates()
    {
        // Arrange
        var transformation = new DateNormalizationTransformation();
        var item = new
        {
            PublishedDate = "2024-01-15T10:30:00",
            ModifiedDate = "January 20, 2024"
        };
        var context = StorageContext.Create("DateSpider");

        // Act
        var result = await transformation.TransformAsync(item, context);

        // Assert
        result.Should().NotBeNull();
        var resultDict = GetProperties(result);
        resultDict.Should().ContainKey("PublishedDate");
        resultDict.Should().ContainKey("ModifiedDate");
    }

    [Fact]
    public async Task GeocodeTransformation_ShouldAddCoordinates()
    {
        // Arrange
        var mockGeocoder = new MockGeocoder();
        var transformation = new GeocodeTransformation(mockGeocoder);
        var item = new
        {
            Address = "1600 Amphitheatre Parkway, Mountain View, CA"
        };
        var context = StorageContext.Create("LocationSpider");

        // Act
        var result = await transformation.TransformAsync(item, context);

        // Assert
        result.Should().NotBeNull();
        var resultDict = GetProperties(result);
        resultDict.Should().ContainKey("Latitude");
        resultDict.Should().ContainKey("Longitude");
        resultDict["Latitude"].Should().Be(37.4224);
        resultDict["Longitude"].Should().Be(-122.0856);
    }

    [Fact]
    public async Task UrlNormalizationTransformation_ShouldNormalizeUrls()
    {
        // Arrange
        var transformation = new UrlNormalizationTransformation();
        var item = new
        {
            Url = "HTTPS://EXAMPLE.COM/Page?utm_source=test&id=123",
            ImageUrl = "/images/product.jpg"
        };
        var context = new StorageContext
        {
            SpiderName = "UrlSpider",
            SourceUrl = "https://example.com"
        };

        // Act
        var result = await transformation.TransformAsync(item, context);

        // Assert
        result.Should().NotBeNull();
        var resultDict = GetProperties(result);
        resultDict["Url"].Should().Be("https://example.com/Page?id=123");
        resultDict["ImageUrl"].Should().Be("https://example.com/images/product.jpg");
    }

    [Fact]
    public async Task DeduplicationTransformation_ShouldDetectDuplicates()
    {
        // Arrange
        var transformation = new DeduplicationTransformation();
        var item1 = new { Id = "123", Title = "Product" };
        var item2 = new { Id = "123", Title = "Product" }; // Duplicate
        var item3 = new { Id = "456", Title = "Another Product" };
        var context = StorageContext.Create("DedupeSpider");

        // Act
        var result1 = await transformation.ShouldIncludeAsync(item1, context);
        var result2 = await transformation.ShouldIncludeAsync(item2, context);
        var result3 = await transformation.ShouldIncludeAsync(item3, context);

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeFalse(); // Duplicate should be filtered
        result3.Should().BeTrue();
    }

    [Fact]
    public async Task CompositeTransformation_ShouldApplyMultipleTransformations()
    {
        // Arrange
        var composite = new CompositeTransformation();
        composite.AddTransformation(new CleanHtmlTransformation());
        composite.AddTransformation(new NormalizeTransformation());

        var item = new
        {
            Title = "<h1>  TEST TITLE  </h1>"
        };
        var context = StorageContext.Create("CompositeSpider");

        // Act
        var result = await composite.TransformAsync(item, context);

        // Assert
        result.Should().NotBeNull();
        var resultDict = GetProperties(result);
        resultDict["Title"].Should().Be("test title");
    }

    [Fact]
    public async Task ValidationTransformation_ShouldValidateRequiredFields()
    {
        // Arrange
        var transformation = new ValidationTransformation(new[] { "Title", "Price" });
        var validItem = new { Title = "Product", Price = 99.99 };
        var invalidItem = new { Title = "Product" }; // Missing Price
        var context = StorageContext.Create("ValidationSpider");

        // Act
        var result1 = await transformation.ValidateAsync(validItem, context);
        var result2 = await transformation.ValidateAsync(invalidItem, context);

        // Assert
        result1.IsValid.Should().BeTrue();
        result2.IsValid.Should().BeFalse();
        result2.Errors.Should().Contain(e => e.Contains("Price"));
    }

    [Fact]
    public async Task TruncateTransformation_ShouldTruncateLongFields()
    {
        // Arrange
        var transformation = new TruncateTransformation(maxLength: 10);
        var item = new
        {
            Title = "This is a very long title that should be truncated",
            ShortField = "Short"
        };
        var context = StorageContext.Create("TruncateSpider");

        // Act
        var result = await transformation.TransformAsync(item, context);

        // Assert
        result.Should().NotBeNull();
        var resultDict = GetProperties(result);
        resultDict["Title"].ToString()!.Length.Should().BeLessOrEqualTo(10);
        resultDict["ShortField"].Should().Be("Short");
    }

    [Fact]
    public async Task DefaultValueTransformation_ShouldSetDefaults()
    {
        // Arrange
        var transformation = new DefaultValueTransformation(new Dictionary<string, object>
        {
            ["Status"] = "active",
            ["Priority"] = 1
        });
        var item = new { Title = "Test" };
        var context = StorageContext.Create("DefaultSpider");

        // Act
        var result = await transformation.TransformAsync(item, context);

        // Assert
        result.Should().NotBeNull();
        var resultDict = GetProperties(result);
        resultDict.Should().ContainKey("Status");
        resultDict.Should().ContainKey("Priority");
        resultDict["Status"].Should().Be("active");
        resultDict["Priority"].Should().Be(1);
    }

    #region Test Helper Classes

    private static Dictionary<string, object> GetProperties(object obj)
    {
        var dict = new Dictionary<string, object>();

        // Handle ExpandoObject
        if (obj is IDictionary<string, object> expando)
        {
            foreach (var kvp in expando)
            {
                dict[kvp.Key] = kvp.Value;
            }
            return dict;
        }

        // Handle regular objects
        foreach (var prop in obj.GetType().GetProperties())
        {
            dict[prop.Name] = prop.GetValue(obj)!;
        }
        return dict;
    }

    private class NormalizeTransformation
    {
        public Task<object> TransformAsync(object item, StorageContext context)
        {
            var result = new Dictionary<string, object>();
            var inputDict = GetProperties(item);

            foreach (var kvp in inputDict)
            {
                if (kvp.Value is string str)
                {
                    result[kvp.Key] = str.Trim().ToLowerInvariant();
                }
                else
                {
                    result[kvp.Key] = kvp.Value;
                }
            }
            return Task.FromResult<object>(DictToAnonymous(result));
        }
    }

    private class FilterTransformation
    {
        private readonly Func<object, bool> _predicate;

        public FilterTransformation(Func<object, bool> predicate)
        {
            _predicate = predicate;
        }

        public Task<bool> ShouldIncludeAsync(object item, StorageContext context)
        {
            return Task.FromResult(_predicate(item));
        }
    }

    private class EnrichmentTransformation
    {
        public Task<object> TransformAsync(object item, StorageContext context)
        {
            var result = new Dictionary<string, object>();
            foreach (var prop in item.GetType().GetProperties())
            {
                result[prop.Name] = prop.GetValue(item)!;
            }
            result["_spider"] = context.SpiderName!;
            result["_source_url"] = context.SourceUrl!;
            result["_extracted_at"] = DateTime.UtcNow;
            return Task.FromResult<object>(DictToAnonymous(result));
        }
    }

    private class CleanHtmlTransformation
    {
        public Task<object> TransformAsync(object item, StorageContext context)
        {
            var result = new Dictionary<string, object>();
            var inputDict = GetProperties(item);

            foreach (var kvp in inputDict)
            {
                if (kvp.Value is string str)
                {
                    result[kvp.Key] = Regex.Replace(str, "<.*?>", string.Empty);
                }
                else
                {
                    result[kvp.Key] = kvp.Value;
                }
            }
            return Task.FromResult<object>(DictToAnonymous(result));
        }
    }

    private class PriceNormalizationTransformation
    {
        public Task<object> TransformAsync(object item, StorageContext context)
        {
            var result = new Dictionary<string, object>();
            foreach (var prop in item.GetType().GetProperties())
            {
                var value = prop.GetValue(item);
                if (value is string str && (prop.Name.Contains("Price", StringComparison.OrdinalIgnoreCase)))
                {
                    var cleaned = Regex.Replace(str, @"[^\d.]", "");
                    if (decimal.TryParse(cleaned, out var price))
                    {
                        result[prop.Name] = price;
                    }
                    else
                    {
                        result[prop.Name] = value;
                    }
                }
                else
                {
                    result[prop.Name] = value!;
                }
            }
            return Task.FromResult<object>(DictToAnonymous(result));
        }
    }

    private class DateNormalizationTransformation
    {
        public Task<object> TransformAsync(object item, StorageContext context)
        {
            var result = new Dictionary<string, object>();
            foreach (var prop in item.GetType().GetProperties())
            {
                var value = prop.GetValue(item);
                if (value is string str && prop.Name.Contains("Date", StringComparison.OrdinalIgnoreCase))
                {
                    if (DateTime.TryParse(str, out var date))
                    {
                        result[prop.Name] = date;
                    }
                    else
                    {
                        result[prop.Name] = value;
                    }
                }
                else
                {
                    result[prop.Name] = value!;
                }
            }
            return Task.FromResult<object>(DictToAnonymous(result));
        }
    }

    private class MockGeocoder
    {
        public Task<(double Lat, double Lon)> GeocodeAsync(string address)
        {
            // Mock coordinates for testing
            return Task.FromResult((37.4224, -122.0856));
        }
    }

    private class GeocodeTransformation
    {
        private readonly MockGeocoder _geocoder;

        public GeocodeTransformation(MockGeocoder geocoder)
        {
            _geocoder = geocoder;
        }

        public async Task<object> TransformAsync(object item, StorageContext context)
        {
            var result = new Dictionary<string, object>();
            foreach (var prop in item.GetType().GetProperties())
            {
                result[prop.Name] = prop.GetValue(item)!;
            }

            if (result.ContainsKey("Address"))
            {
                var coords = await _geocoder.GeocodeAsync(result["Address"].ToString()!);
                result["Latitude"] = coords.Lat;
                result["Longitude"] = coords.Lon;
            }

            return DictToAnonymous(result);
        }
    }

    private class UrlNormalizationTransformation
    {
        public Task<object> TransformAsync(object item, StorageContext context)
        {
            var result = new Dictionary<string, object>();
            foreach (var prop in item.GetType().GetProperties())
            {
                var value = prop.GetValue(item);
                if (value is string str && prop.Name.Contains("Url", StringComparison.OrdinalIgnoreCase))
                {
                    var normalized = NormalizeUrl(str, context.SourceUrl!);
                    result[prop.Name] = normalized;
                }
                else
                {
                    result[prop.Name] = value!;
                }
            }
            return Task.FromResult<object>(DictToAnonymous(result));
        }

        private string NormalizeUrl(string url, string baseUrl)
        {
            if (url.StartsWith("/"))
            {
                var baseUri = new Uri(baseUrl);
                return $"{baseUri.Scheme}://{baseUri.Host}{url}";
            }

            // Remove UTM parameters except id
            var uri = new Uri(url);
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var filteredQuery = new System.Collections.Specialized.NameValueCollection();
            if (query["id"] != null)
            {
                filteredQuery["id"] = query["id"];
            }

            // Properly serialize the query string
            var queryString = string.Empty;
            if (filteredQuery.Count > 0)
            {
                var items = new List<string>();
                foreach (string? key in filteredQuery.AllKeys)
                {
                    if (key != null)
                    {
                        items.Add($"{key}={filteredQuery[key]}");
                    }
                }
                queryString = string.Join("&", items);
            }

            // Normalize scheme and host to lowercase, preserve path case
            var builder = new UriBuilder(uri);
            builder.Scheme = builder.Scheme.ToLowerInvariant();
            builder.Host = builder.Host.ToLowerInvariant();
            builder.Query = queryString;

            return builder.Uri.ToString();
        }
    }

    private class DeduplicationTransformation
    {
        private readonly HashSet<string> _seen = new();

        public Task<bool> ShouldIncludeAsync(object item, StorageContext context)
        {
            var hash = System.Text.Json.JsonSerializer.Serialize(item);
            return Task.FromResult(_seen.Add(hash));
        }
    }

    private class CompositeTransformation
    {
        private readonly List<dynamic> _transformations = new();

        public void AddTransformation(dynamic transformation)
        {
            _transformations.Add(transformation);
        }

        public async Task<object> TransformAsync(object item, StorageContext context)
        {
            var current = item;
            foreach (var transformation in _transformations)
            {
                current = await transformation.TransformAsync(current, context);
            }
            return current;
        }
    }

    private class ValidationTransformation
    {
        private readonly string[] _requiredFields;

        public ValidationTransformation(string[] requiredFields)
        {
            _requiredFields = requiredFields;
        }

        public Task<ValidationResult> ValidateAsync(object item, StorageContext context)
        {
            var errors = new List<string>();
            var props = item.GetType().GetProperties().Select(p => p.Name).ToHashSet();

            foreach (var field in _requiredFields)
            {
                if (!props.Contains(field))
                {
                    errors.Add($"Required field '{field}' is missing");
                }
            }

            return Task.FromResult(new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors
            });
        }
    }

    private class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    private class TruncateTransformation
    {
        private readonly int _maxLength;

        public TruncateTransformation(int maxLength)
        {
            _maxLength = maxLength;
        }

        public Task<object> TransformAsync(object item, StorageContext context)
        {
            var result = new Dictionary<string, object>();
            foreach (var prop in item.GetType().GetProperties())
            {
                var value = prop.GetValue(item);
                if (value is string str && str.Length > _maxLength)
                {
                    result[prop.Name] = str.Substring(0, _maxLength);
                }
                else
                {
                    result[prop.Name] = value!;
                }
            }
            return Task.FromResult<object>(DictToAnonymous(result));
        }
    }

    private class DefaultValueTransformation
    {
        private readonly Dictionary<string, object> _defaults;

        public DefaultValueTransformation(Dictionary<string, object> defaults)
        {
            _defaults = defaults;
        }

        public Task<object> TransformAsync(object item, StorageContext context)
        {
            var result = new Dictionary<string, object>();
            foreach (var prop in item.GetType().GetProperties())
            {
                result[prop.Name] = prop.GetValue(item)!;
            }
            foreach (var kvp in _defaults)
            {
                if (!result.ContainsKey(kvp.Key))
                {
                    result[kvp.Key] = kvp.Value;
                }
            }
            return Task.FromResult<object>(DictToAnonymous(result));
        }
    }

    private static object DictToAnonymous(Dictionary<string, object> dict)
    {
        var expando = new System.Dynamic.ExpandoObject() as IDictionary<string, object>;
        foreach (var kvp in dict)
        {
            expando[kvp.Key] = kvp.Value;
        }
        return expando;
    }

    #endregion
}
