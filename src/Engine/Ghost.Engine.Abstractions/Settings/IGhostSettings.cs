namespace Ghost.Engine.Abstractions.Settings;

public interface IGhostSettings
{
    public bool TryGet<T>(string key, out T? value);

    public T GetOrDefault<T>(string key, T defaultValue);
}
