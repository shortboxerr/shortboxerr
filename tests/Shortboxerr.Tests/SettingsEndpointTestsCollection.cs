namespace Shortboxerr.Tests;

/// <summary>
/// Serializes <see cref="SettingsEndpointTests"/> so parallel runs do not share one client/DB while
/// regenerate changes the stored API key.
/// </summary>
[CollectionDefinition(nameof(SettingsEndpointTestsCollection), DisableParallelization = true)]
public class SettingsEndpointTestsCollection
{
}
