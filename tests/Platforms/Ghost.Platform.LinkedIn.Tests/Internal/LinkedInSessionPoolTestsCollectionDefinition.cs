using Xunit;

namespace Ghost.Plugin.LinkedIn.Tests.Internal;

/// <summary>
/// Disables test parallelization for LinkedInSessionPoolTests to prevent race conditions with Timer callbacks.
/// </summary>
[CollectionDefinition("Sequential", DisableParallelization = true)]
public class LinkedInSessionPoolTestsCollectionDefinition
{
}
