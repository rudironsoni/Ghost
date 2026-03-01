using System.Reflection;

namespace Ghost.Sdk.Spider.Core.Entities;

/// <summary>
/// Internal helpers for EntityBase to host static members that would otherwise
/// be declared on a generic type to satisfy analyzer guidance (CA1000).
/// </summary>
internal static class EntityBaseHelpers
{
    internal static EntityMetadata GetMetadata<T>() where T : EntityBase<T>, new()
    {
        Type type = typeof(T);

        var selector = type.GetCustomAttribute<Attributes.EntitySelectorAttribute>();

        var properties = new List<PropertyMetadata>();

        foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var valueSelector = prop.GetCustomAttribute<Attributes.ValueSelectorAttribute>();
            if (valueSelector is null)
                continue;

            var formatters = prop.GetCustomAttributes<Attributes.FormatterAttribute>().ToList();
            var fieldAttr = prop.GetCustomAttribute<Attributes.FieldAttribute>();

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
