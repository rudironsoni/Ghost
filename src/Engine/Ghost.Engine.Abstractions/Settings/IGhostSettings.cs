namespace Ghost.Engine.Abstractions.Settings;

public interface IGhostSettings
{
    bool TryGet<T>(string key, out T? value);

    T GetOrDefault<T>(string key, T defaultValue);
}
