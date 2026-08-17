using ML_25_26_05_Python_Code_Tool_Interpreter_for_Chart_Creation;
using UnitTestProject.GoldenScenarios;

namespace UnitTestProject;

/// <summary>
/// Golden scenarios: reference prompts + Python scripts that must validate and execute
/// the same way the agent-produced code would (no OpenAI calls).
/// </summary>
[TestClass]
public class GoldenScenarioTests
{
    private string _tempRoot = null!;
    private PythonCodeValidator _validator = null!;
    private PythonExecutor _executor = null!;

    // Note: This is NOT a test itself. This is an initialization hook that automatically
    // runs BEFORE every single TestMethod to guarantee a brand-new, isolated environment.
    [TestInitialize]
    public void Setup()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "GoldenScenarioTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _validator = new PythonCodeValidator();
        _executor = new PythonExecutor();
    }

    // Note: This is NOT a test. This is an automated teardown hook that runs AFTER every 
    // test finishes to safely clean up the temporary files off the host machine's physical disk.
    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    public static IEnumerable<object[]> MainScenarioJsonFiles
    {
        get
        {
            yield return new object[] { "scenario_01_inline_bar.json" };
            yield return new object[] { "scenario_02_small_csv_line.json" };
            yield return new object[] { "scenario_03_multi_dashboard.json" };
            yield return new object[] { "scenario_04_large_sample_scatter.json" };
            yield return new object[] { "scenario_05_timeseries_resample.json" };
        }
    }

    [TestMethod]
    [DynamicData(nameof(MainScenarioJsonFiles))]
    [Description("Full integration test parsing actual JSON reference flows. Validates that the pre-approved golden Python scripts can successfully traverse the compiler and validation sandbox.")]
    public async Task GoldenScenario_ValidatesAndExecutes(string jsonFileName)
    {
        // First we deserialize the JSON defining this specific golden test scenario
        var def = GoldenScenarioLoader.LoadDefinition(jsonFileName);

        // Figure out if this scenario specifically demands generating a massive fake CSV file for stress testing
        string? largeCsvPath = null;
        if (def.LargeFixtureRowCount is > 0)
        {
            // Okay, it does. Let's dump out a huge synthetic X/Y coordinate dataset right into our temp workspace
            largeCsvPath = Path.Combine(_tempRoot, "large_xy.csv");
            await WriteLargeNumericCsvAsync(largeCsvPath, def.LargeFixtureRowCount.Value);
        }

        // Now we resolve where our sample data fixture actually lives on the local disk
        var fixtureNative = GoldenScenarioLoader.ResolveFixtureNativePath(def.Fixture);

        // And convert it to a python-friendly format (forward slashes) so MS Python doesn't throw Unicode escapes
        var fixturePython = GoldenScenarioLoader.ToPythonPath(fixtureNative);

        // Before running Python, ensure our C# dataset analyzer can independently read the fixture columns properly
        if (!string.IsNullOrEmpty(fixtureNative))
        {
            var meta = await DatasetAnalyzer.AnalyzeAsync(fixtureNative);
            Assert.IsTrue(meta.ColumnCount > 0, $"DatasetAnalyzer should see columns for {def.Fixture}");
        }

        // Determine which CSV path we intend to inject into the raw Python script 
        var csvForPython = string.IsNullOrEmpty(fixturePython)
            ? GoldenScenarioLoader.ToPythonPath(largeCsvPath ?? "")
            : fixturePython;

        // If this test defines a multi-step sequence of scripts, iterate over them chronologically
        if (def.Steps is { Count: > 0 })
        {
            foreach (var step in def.Steps)
            {
                // Execute each partial script as an independent sandbox pass
                await RunSingleScriptAsync(def, step.PythonScriptFile, step.ChartId + ".png", csvForPython);
            }
        }
        else
        {
            // Otherwise, it's just a standard single-script run
            if (string.IsNullOrWhiteSpace(def.PythonScriptFile))
                throw new InvalidOperationException($"{def.Id} has no PythonScriptFile and no Steps.");

            // Run it through the compiler pipeline
            await RunSingleScriptAsync(def, def.PythonScriptFile, def.Id + ".png", csvForPython);
        }
    }

    private async Task RunSingleScriptAsync(
        GoldenScenarioDefinition def,
        string scriptFileName,
        string outputImageName,
        string csvPathForPlaceholder)
    {
        // Pluck the raw python text right out of the referenced script file
        var scriptText = GoldenScenarioLoader.ReadScript(scriptFileName);

        // Define where we expect the final chart image to land on disk
        var outputPng = Path.Combine(_tempRoot, outputImageName);
        var outputPngUnix = outputPng.Replace("\\", "/");

        // Swap out our magical template placeholders for the strict actual local string locations
        var code = scriptText
            .Replace("{OUTPUT_PNG}", outputPngUnix, StringComparison.Ordinal)
            .Replace("{CSV_PATH}", csvPathForPlaceholder, StringComparison.Ordinal);

        // Subject this code to the static validator to ensure we aren't allowing malicious logic to execute
        var validation = await _validator.ValidateAsync(code, outputPng);
        Assert.IsTrue(validation.IsValid,
            $"Scenario {def.Id} / {scriptFileName} validation failed: {string.Join("; ", validation.Errors)}");

        // Take our finalized, injected code and write it out directly as a true .py file for the process launcher
        var scriptPath = Path.Combine(_tempRoot, Path.GetFileNameWithoutExtension(scriptFileName) + ".py");
        await File.WriteAllTextAsync(scriptPath, code);

        // Boot up Python asynchronously, limiting it to exactly 60 seconds of execution allowance
        var result = await _executor.ExecuteAsync(scriptPath, timeoutMs: 60_000);
        Assert.IsTrue(result.Success,
            $"Scenario {def.Id} execution failed: {result.StandardError}");

        // Now for the ultimate check: did the Python script actually render an image out onto the hard drive?
        Assert.IsTrue(File.Exists(outputPng), $"Expected PNG: {outputPng}");

        // Make absolutely sure it's not a broken or structurally empty 0-byte file placeholder
        var len = new FileInfo(outputPng).Length;
        Assert.IsTrue(len > 200, $"PNG should be non-trivial size, got {len} bytes");
    }

    private static async Task WriteLargeNumericCsvAsync(string path, int rowCount)
    {
        await using var writer = new StreamWriter(path);
        await writer.WriteLineAsync("x,y");
        var rnd = new Random(42);
        for (var i = 0; i < rowCount; i++)
            await writer.WriteLineAsync($"{rnd.NextDouble():F6},{rnd.NextDouble():F6}");
    }
}
