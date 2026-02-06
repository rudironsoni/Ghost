namespace Ghost.Sdk.Spider.Configuration.Models;

/// <summary>
/// Configuration for an entity to extract.
/// </summary>
public sealed class EntityConfiguration
{
    /// <summary>
    /// Gets or sets the unique name of this entity.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the selector for the entity container (for list extraction).
    /// </summary>
    public SelectorConfiguration? Container { get; set; }

    /// <summary>
    /// Gets or sets whether this entity represents a list of items.
    /// </summary>
    public bool IsList { get; set; }

    /// <summary>
    /// Gets or sets the fields to extract for this entity.
    /// </summary>
    public List<FieldConfiguration> Fields { get; set; } = new();

    /// <summary>
    /// Gets or sets nested entities.
    /// </summary>
    public List<EntityConfiguration> NestedEntities { get; set; } = new();

    /// <summary>
    /// Gets or sets validation rules for this entity.
    /// </summary>
    public EntityValidationConfiguration? Validation { get; set; }
}

/// <summary>
/// Validation configuration for entities.
/// </summary>
public sealed class EntityValidationConfiguration
{
    /// <summary>
    /// Gets or sets whether this entity is required.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets the minimum number of items (for lists).
    /// </summary>
    public int? MinItems { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of items (for lists).
    /// </summary>
    public int? MaxItems { get; set; }

    /// <summary>
    /// Gets or sets custom validation expressions.
    /// </summary>
    public List<string> CustomRules { get; set; } = new();
}
