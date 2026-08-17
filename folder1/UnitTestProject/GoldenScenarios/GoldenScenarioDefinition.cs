namespace UnitTestProject.GoldenScenarios;

/// <summary>
/// Machine-readable golden scenario (paired with a .py script on disk).
/// </summary>
public sealed class GoldenScenarioDefinition
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string UserPrompt { get; set; } = "";
    public List<string>? ExpectedOutcomes { get; set; }
    public string? Fixture { get; set; }
    public string? PythonScriptFile { get; set; }
    public int? LargeFixtureRowCount { get; set; }
    public List<GoldenScenarioStep>? Steps { get; set; }
}

public sealed class GoldenScenarioStep
{
    public string ChartId { get; set; } = "";
    public string PythonScriptFile { get; set; } = "";
}
