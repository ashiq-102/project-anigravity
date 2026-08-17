using ML_25_26_05_Python_Code_Tool_Interpreter_for_Chart_Creation;
using ML_25_26_05_Python_Code_Tool_Interpreter_for_Chart_Creation.Storage;
using Moq;

namespace UnitTestProject
{
    [TestClass]
    public class ChartPluginTests
    {
        private Mock<IStorageStore> _mockStore = null!;
        private StorageTool _storageTool = null!;
        private PythonCodeValidator _validator = null!;
        private PythonExecutor _executor = null!;
        private ErrorMappingStore _errorStore = null!;
        private ChartManifest _manifest = null!;
        private string _outputDir = null!;
        private ChartPlugin _plugin = null!;

        // Note: This is NOT a test itself. This is an initialization hook that automatically
        // runs BEFORE every single TestMethod to guarantee a brand-new, isolated environment.
        [TestInitialize]
        public void Setup()
        {
            _outputDir = Path.Combine(Path.GetTempPath(), "ChartPluginTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_outputDir);

            _mockStore = new Mock<IStorageStore>();
            _storageTool = new StorageTool(_mockStore.Object);
            _validator = new PythonCodeValidator();
            _executor = new PythonExecutor();
            _errorStore = new ErrorMappingStore(Path.Combine(_outputDir, "error_memory.json"));
            _manifest = new ChartManifest(_outputDir);

            _plugin = new ChartPlugin(_storageTool, _validator, _executor, _errorStore, _manifest, _outputDir);
        }

        // Note: This is NOT a test. This is an automated teardown hook that runs AFTER every 
        // test finishes to safely clean up the temporary files off the host machine's physical disk.
        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_outputDir))
                Directory.Delete(_outputDir, recursive: true);
        }

        [TestMethod]
        [Description("Ensures ChartPlugin rejects empty python code input with an error message.")]
        public async Task GenerateAndRunChart_EmptyCode_ReturnsError()
        {
            // Try to pass a completely blank script payload into the chart generator
            var result = await _plugin.GenerateAndRunChart("", "c1");

            // The plugin must intercept this immediately and complain that the code is missing
            StringAssert.Contains(result, "ERROR: pythonCode is empty");
        }

        [TestMethod]
        [Description("Ensures ChartPlugin rejects requests lacking a generated chart ID.")]
        public async Task GenerateAndRunChart_EmptyId_ReturnsError()
        {
            // Give the plugin valid code but deliberately 'forget' to assign it an ID
            var result = await _plugin.GenerateAndRunChart("print('hello')", "");

            // It should yell at us, since it cannot save a chart without a known filename ID
            StringAssert.Contains(result, "ERROR: chartId is required");
        }


        [TestMethod]
        [Description("Verifies that validation failures (like forbidden imports) are trapped and properly returned to the LLM.")]
        public async Task GenerateAndRunChart_ValidationError_ReturnsErrorMessage()
        {
            // Here's a script full of lethal security violations, primarily 'os' and 'plt.show()'
            var code = @"
import os
import matplotlib.pyplot as plt
plt.plot([1,2,3])
plt.show()
";
            // Drop it into the generator
            var result = await _plugin.GenerateAndRunChart(code, "bad_chart");

            // The static validation engine must intercept this immediately before Python even breathes
            StringAssert.StartsWith(result, "VALIDATION ERROR");

            // It needs to specifically call out the illegal OS module ...
            StringAssert.Contains(result, "Forbidden import");

            // ... and loudly warn the LLM that 'plt.show()' freezes headless environments and is banned
            StringAssert.Contains(result, "must not contain plt.show()");
        }


        [TestMethod]
        [Description("Proves that looking up a reference name correctly delegates to the underlying mocked storage mechanisms.")]
        public void ResolveFilePath_DelegatesToStorageTool()
        {
            // Tell our mock storage to pretend 'MY_DATA' maps to 'C:\store\file.csv'
            _mockStore.Setup(s => s.GetAbsolutePath("MY_DATA")).Returns(@"C:\store\file.csv");

            // Check what the plugin resolves the abstract reference to
            var result = _plugin.ResolveFilePath("MY_DATA");

            // It should be flawlessly integrated with python's forward slash preference 
            Assert.AreEqual("C:/store/file.csv", result); // Checks for forward slash conversion
        }
    }
}
