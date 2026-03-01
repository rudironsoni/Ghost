using System.Reflection;
using System.Collections.Generic;
using System.Linq;

namespace Ghost.Sdk.Spider.Core.Entities;

/// <summary>
/// Provides metadata for entity types. This class centralizes the logic that
/// was previously located on a generic type or internal helper to avoid static
/// members on generic types and to make the API more discoverable.
/// </summary>
public static class EntityMetadataProvider
{
    /// <summary>
    /// Returns the metadata for the specified entity type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to inspect.</typeparam>
    /// <returns>An <see cref="EntityMetadata"/> describing the entity.</returns>
    public static EntityMetadata GetMetadata<T>() where T : EntityBase<T>, new()
    {
        Type type = typeof(T);

        Attributes.EntitySelectorAttribute? selector = type.GetCustomAttribute<Attributes.EntitySelectorAttribute>();

        List<PropertyMetadata> properties = new List<PropertyMetadata>();

        foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Attributes.ValueSelectorAttribute? valueSelector = prop.GetCustomAttribute<Attributes.ValueSelectorAttribute>();
            if (valueSelector is null)
                continue;

            List<Attributes.FormatterAttribute> formatters = prop.GetCustomAttributes<Attributes.FormatterAttribute>().ToList();
            Attributes.FieldAttribute? fieldAttr = prop.GetCustomAttribute<Attributes.FieldAttribute>();

            properties.Add(new PropertyMetadata
            {
                PropertyInfo = prop,
                ValueSelector = valueSelector,
                FieldAttribute = fieldAttr,
                Formatters = formatters
            });
        }

        return new EntityMetadata
        {
            EntityType = type,
            EntitySelector = selector,
            Properties = properties
        };
    }
}
