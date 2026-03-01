using System.Reflection;
using Ghost.Sdk.Spider.Core.Entities.Attributes;

namespace Ghost.Sdk.Spider.Core.Entities;

internal static class EntityBaseHelpers
{
    public static EntityMetadata GetMetadata<TItem>() where TItem : EntityBase<TItem>, new()
    {
        Type type = typeof(TItem);
        EntitySelectorAttribute? entitySelectorAttr = type.GetCustomAttribute<EntitySelectorAttribute>();

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.GetCustomAttribute<ValueSelectorAttribute>() != null)
            .Select(p => new PropertyMetadata
            {
                PropertyInfo = p,
                ValueSelector = p.GetCustomAttribute<ValueSelectorAttribute>()!,
                FieldAttribute = p.GetCustomAttribute<FieldAttribute>(),
                Formatters = p.GetCustomAttributes<FormatterAttribute>().ToList()
            })
            .ToList();

        return new EntityMetadata
        {
            EntityType = type,
            EntitySelector = entitySelectorAttr,
            Properties = properties
        };
    }
}
