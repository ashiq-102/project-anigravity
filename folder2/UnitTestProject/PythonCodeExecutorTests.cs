using ChartCreationMCPServer.Execution;


namespace UnitTestProject
{
    [TestClass]
    public class PythonCodeExecutorTests
    {
        private string _tempDir = null!;
        private PythonCodeExecutor _executor = null!;

        [TestInitialize]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "PythonExecutorTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _executor = new PythonCodeExecutor();
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        [TestMethod]
        [Description("Ensures python executes, tracks an exit code of 0, and captures standard output stream strings.")]
        public async Task ExecuteAsync_ValidScript_ReturnsSuccessAndOutput()
        {
            var script = Path.Combine(_tempDir, "hello.py");
            await File.WriteAllTextAsync(script, "print('hello world')");

            var result = await _executor.ExecuteAsync(script);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.ExitCode);
            StringAssert.Contains(result.StandardOutput, "hello world");
        }

        [TestMethod]
        [Description("Asserts that an unhandled python failure triggers a non-zero exit code and pipes data through stderr.")]
        public async Task ExecuteAsync_SyntaxError_ReturnsFailure()
        {
            var script = Path.Combine(_tempDir, "bad.py");
            await File.WriteAllTextAsync(script, "def broken(");

            var result = await _executor.ExecuteAsync(script);

            Assert.IsFalse(result.Success);
            Assert.AreNotEqual(0, result.ExitCode);
            StringAssert.Contains(result.StandardError, "SyntaxError");
        }

        [TestMethod]
        [Description("Ensures infinite-loop protection forcibly kills instances exceeding the execution allowance period.")]
        public async Task ExecuteAsync_Timeout_ReturnsFailure()
        {
            var script = Path.Combine(_tempDir, "slow.py");
            await File.WriteAllTextAsync(script, "import time\ntime.sleep(60)");

            var result = await _executor.ExecuteAsync(script, timeoutMs: 2000);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(-1, result.ExitCode);
            Assert.IsTrue(result.StandardError.Contains("timed out", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        [Description("Validates exception bubbling from native File I/O checks when script does not exist.")]
        public async Task ExecuteAsync_ScriptNotFound_ThrowsFileNotFoundException()
        {
            try
            {
                await _executor.ExecuteAsync(Path.Combine(_tempDir, "nonexistent.py"));
                Assert.Fail("A FileNotFoundException should have been thrown.");
            }
            catch (FileNotFoundException) { }
        }

        [TestMethod]
        [Description("Catches null or empty path argument executions.")]
        public async Task ExecuteAsync_EmptyPath_ThrowsArgumentException()
        {
            try
            {
                await _executor.ExecuteAsync("");
                Assert.Fail("An ArgumentException should have been thrown.");
            }
            catch (ArgumentException) { }
        }
    }
}