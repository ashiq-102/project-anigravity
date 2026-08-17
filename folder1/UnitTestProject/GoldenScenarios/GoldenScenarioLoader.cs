using System.Text.Json;

namespace UnitTestProject.GoldenScenarios;

internal static class GoldenScenarioLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static string GoldenScenariosDirectory =>
        Path.Combine(AppContext.BaseDirectory, "GoldenScenarios");

    public static GoldenScenarioDefinition LoadDefinition(string jsonFileName)
    {
        var path = Path.Combine(GoldenScenariosDirectory, jsonFileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Golden scenario not found: {path}");

        var json = File.ReadAllText(path);
        var def = JsonSerializer.Deserialize<GoldenScenarioDefinition>(json, JsonOptions);
        if (def == null || string.IsNullOrWhiteSpace(def.Id))
            throw new InvalidOperationException($"Invalid golden scenario: {jsonFileName}");

        return def;
    }

    public static string ReadScript(string scriptFileName)
    {
        var path = Path.Combine(GoldenScenariosDirectory, scriptFileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Golden script not found: {path}");

        return File.ReadAllText(path);
    }

    /// <summary>Native path for .NET file APIs.</summary>
    public static string ResolveFixtureNativePath(string? fixtureRelativePath)
    {
        if (string.IsNullOrWhiteSpace(fixtureRelativePath))
            return "";

        var path = Path.GetFullPath(Path.Combine(GoldenScenariosDirectory, fixtureRelativePath));
        if (!File.Exists(path))
            throw new FileNotFoundException($"Fixture not found: {path}");

        return path;
    }

    /// <summary>Forward-slash path for embedding in Python source.</summary>
    public static string ToPythonPath(string nativePath) =>
        string.IsNullOrEmpty(nativePath) ? "" : nativePath.Replace("\\", "/", StringComparison.Ordinal);
}
