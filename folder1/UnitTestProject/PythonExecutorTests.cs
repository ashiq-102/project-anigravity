using ML_25_26_05_Python_Code_Tool_Interpreter_for_Chart_Creation;


namespace UnitTestProject
{
    [TestClass]
    public class PythonExecutorTests
    {
        private string _tempDir = null!;
        private PythonExecutor _executor = null!;

        // Note: This is NOT a test itself. This is an initialization hook that automatically
        // runs BEFORE every single TestMethod to guarantee a brand-new, isolated environment.
        [TestInitialize]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "PythonExecutorTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _executor = new PythonExecutor();
        }

        // Note: This is NOT a test. This is an automated teardown hook that runs AFTER every 
        // test finishes to safely clean up the temporary files off the host machine's physical disk.
        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        [TestMethod]
        [Description("Ensures an optimal path correctly executes python, tracks an exit code of 0, and captures standard output stream strings.")]
        public async Task ExecuteAsync_ValidScript_ReturnsSuccessAndOutput()
        {
            // First we need to write out a simple, valid python file to our temporary directory setup
            var script = Path.Combine(_tempDir, "hello.py");
            await File.WriteAllTextAsync(script, "print('hello world')");

            // Hand the script off to our executor sandbox to process asynchronously
            var result = await _executor.ExecuteAsync(script);

            // A successful run must flag itself as Success internally
            Assert.IsTrue(result.Success);

            // The exit code representing a clean run for most CLI apps is 0
            Assert.AreEqual(0, result.ExitCode);

            // Finally, verify that our executor actually intercepted and parsed the stdout natively
            StringAssert.Contains(result.StandardOutput, "hello world");
        }

        [TestMethod]
        [Description("Asserts that an unhandled python execution failure triggers a non-zero exit code and pipes data through the stderr payload.")]
        public async Task ExecuteAsync_SyntaxError_ReturnsFailure()
        {
            // Let's create an obviously malformed python piece of syntax designed to crash the interpreter
            var script = Path.Combine(_tempDir, "bad.py");
            await File.WriteAllTextAsync(script, "def broken(");

            // Execute it in the sandbox and await its inevitable demise
            var result = await _executor.ExecuteAsync(script);

            // The executor needs to correctly flag that this was *not* a successful run
            Assert.IsFalse(result.Success);

            // Expected: not 0, meaning an error code was thrown upwards from python.exe
            Assert.AreNotEqual(0, result.ExitCode);

            // The exact error needs to have been piped back accurately into our standard error field
            StringAssert.Contains(result.StandardError, "SyntaxError");
        }

        [TestMethod]
        [Description("Ensures infinite-loop protection forcibly kills instances exceeding the prescribed thread execution allowance period.")]
        public async Task ExecuteAsync_Timeout_ReturnsFailure()
        {
            // We want to force Python to sleep and simulate hanging/an infinite processing loop  
            var script = Path.Combine(_tempDir, "slow.py");

            // Setting a 60 second wait ensures we purposefully exceed our very tiny timeout cap
            await File.WriteAllTextAsync(script, "import time\ntime.sleep(60)");

            // Ask the executor to aggressively kill the process after only 2000 milliseconds have elapsed
            var result = await _executor.ExecuteAsync(script, timeoutMs: 2000);

            // It should definitely consider this overall execution operation a failure
            Assert.IsFalse(result.Success);

            // We expect the exit code to be mapped back to -1 when arbitrarily slaughtered mid-process
            Assert.AreEqual(-1, result.ExitCode);

            // The standard error string must report that it 'timed out' so the LLM knows why it failed
            Assert.IsTrue(result.StandardError.Contains("timed out", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        [Description("Validates exception bubbling from native File I/O checks exclusively before attempting to hit the python executable system hooks.")]
        public async Task ExecuteAsync_ScriptNotFound_ThrowsFileNotFoundException()
        {
            try
            {
                // Let's ask the executor to run a completely invisible, non-existent file path
                await _executor.ExecuteAsync(Path.Combine(_tempDir, "nonexistent.py"));

                // Oops, the method didn't crash before attempting process start. We purposely fail the test here.
                Assert.Fail("A FileNotFoundException should have been thrown, but the execution succeeded unexpectedly.");
            }
            catch (FileNotFoundException)
            {
                // Test passes! (The expected exception was caught properly during our manual validation logic)
            }
        }

        [TestMethod]
        [Description("Catches null or stripped argument executions targeting empty scripts.")]
        public async Task ExecuteAsync_EmptyPath_ThrowsArgumentException()
        {
            try
            {
                // Feeding the executor a fully empty pathway deliberately breaks input requirements
                await _executor.ExecuteAsync("");

                // If it attempts to launch Python rather than instantly dying, the test must fail
                Assert.Fail("An ArgumentException should have been thrown, but the execution succeeded unexpectedly.");
            }
            catch (ArgumentException)
            {
                // Test passes! (The expected exception was completely blocked and caught properly)
            }
        }
    }
}
