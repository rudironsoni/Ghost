using System.Reflection;

namespace Ghost.Sdk.Spider.Core.Entities;

/// <summary>
/// Base class for all entity types that can be extracted from web pages.
/// Entities use attributes to define extraction rules and formatters.
/// </summary>
/// <typeparam name="T">The concrete entity type</typeparam>
public abstract class EntityBase<T> where T : EntityBase<T>, new()
{
    /// <summary>
    /// Gets or sets the unique identifier for this entity instance.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the source URL from which this entity was extracted.
    /// </summary>
    public string? SourceUrl { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this entity was extracted.
    /// </summary>
    public DateTime? ExtractedAt { get; set; }

    /// <summary>
    /// Gets the entity metadata including selector configurations and field mappings.
    /// </summary>
    /// <returns>An <see cref="EntityMetadata"/> instance containing the entity configuration.</returns>
    public static EntityMetadata GetMetadata()
    {
        var type = typeof(T);
        var entitySelectorAttr = type.GetCustomAttribute<Attributes.EntitySelectorAttribute>();

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.GetCustomAttribute<Attributes.ValueSelectorAttribute>() != null)
            .Select(p => new PropertyMetadata
            {
                PropertyInfo = p,
                ValueSelector = p.GetCustomAttribute<Attributes.ValueSelectorAttribute>()!,
                FieldAttribute = p.GetCustomAttribute<Attributes.FieldAttribute>(),
                Formatters = p.GetCustomAttributes<Attributes.FormatterAttribute>().ToList()
            })
            .ToList();

        return new EntityMetadata
        {
            EntityType = type,
            EntitySelector = entitySelectorAttr,
            Properties = properties
        };
    }

    /// <summary>
    /// Validates the entity instance according to any validation rules defined by attributes.
    /// </summary>
    /// <returns>True if the entity is valid; otherwise, false.</returns>
    public virtual bool Validate()
    {
        // Base validation - can be overridden by derived classes
        return true;
    }

    /// <summary>
    /// Creates a clone of this entity instance.
    /// </summary>
    /// <returns>A new instance with the same property values.</returns>
    public virtual T Clone()
    {
        var clone = new T
        {
            Id = Id,
            SourceUrl = SourceUrl,
            ExtractedAt = ExtractedAt
        };

        // Copy all property values
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite);

        foreach (var prop in properties)
        {
            if (prop.Name is nameof(Id) or nameof(SourceUrl) or nameof(ExtractedAt))
                continue;

            var value = prop.GetValue(this);
            prop.SetValue(clone, value);
        }

        return clone;
    }
}

/// <summary>
/// Contains metadata about an entity type including its selector configuration and property mappings.
/// </summary>
public class EntityMetadata
{
    /// <summary>
    /// Gets or sets the entity type.
    /// </summary>
    public required Type EntityType { get; init; }

    /// <summary>
    /// Gets or sets the entity selector attribute if defined.
    /// </summary>
    public Attributes.EntitySelectorAttribute? EntitySelector { get; init; }

    /// <summary>
    /// Gets or sets the list of property metadata for all extractable properties.
    /// </summary>
    public required List<PropertyMetadata> Properties { get; init; }
}

/// <summary>
/// Contains metadata about a property that can be extracted from web content.
/// </summary>
public class PropertyMetadata
{
    /// <summary>
    /// Gets or sets the property reflection information.
    /// </summary>
    public required PropertyInfo PropertyInfo { get; init; }

    /// <summary>
    /// Gets or sets the value selector attribute defining how to extract this property.
    /// </summary>
    public required Attributes.ValueSelectorAttribute ValueSelector { get; init; }

    /// <summary>
    /// Gets or sets the optional field attribute with additional configuration.
    /// </summary>
    public Attributes.FieldAttribute? FieldAttribute { get; init; }

    /// <summary>
    /// Gets or sets the list of formatter attributes to apply to extracted values.
    /// </summary>
    public required List<Attributes.FormatterAttribute> Formatters { get; init; }
}
