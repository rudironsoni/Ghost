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
        // Delegate to the central provider to avoid duplicating reflection logic.
        return EntityMetadataProvider.GetMetadata<T>();
    }
}
