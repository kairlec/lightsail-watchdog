namespace lightsail_operator;

public static class EnvironmentHelper
{
    public static int? GetInt(string key)
    {
        var value = GetString(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.Parse(value);
    }

    public static int GetIntRequire(string key)
    {
        var value = GetInt(key);
        if (value == null)
        {
            throw new Exception($"Environment variable {key} is required");
        }

        return value.Value;
    }

    public static string? GetString(string key)
    {
        return Environment.GetEnvironmentVariable(key);
    }

    public static string GetStringRequire(string key)
    {
        var value = GetString(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new Exception($"Environment variable {key} is required");
        }

        return value;
    }
}