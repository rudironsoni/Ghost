namespace Ghostwright.Contracts.Jobs;

/// <summary>
/// Job types supported.
/// </summary>
public enum JobType
{
    /// <summary>
    /// Unknown/unspecified.
    /// </summary>
    Unknown,

    /// <summary>
    /// Full time role.
    /// </summary>
    FullTime,

    /// <summary>
    /// Part time role.
    /// </summary>
    PartTime,

    /// <summary>
    /// Contract role.
    /// </summary>
    Contract,

    /// <summary>
    /// Internship.
    /// </summary>
    Internship
}

/// <summary>
/// Experience level required for a role.
/// </summary>
public enum ExperienceLevel
{
    /// <summary>
    /// Unknown or unspecified.
    /// </summary>
    Unknown,

    /// <summary>
    /// Entry level / Junior.
    /// </summary>
    EntryLevel,

    /// <summary>
    /// Mid-level.
    /// </summary>
    MidLevel,

    /// <summary>
    /// Senior-level.
    /// </summary>
    Senior,

    /// <summary>
    /// Manager-level.
    /// </summary>
    Manager
}
