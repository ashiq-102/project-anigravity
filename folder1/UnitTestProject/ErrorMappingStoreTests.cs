using ML_25_26_05_Python_Code_Tool_Interpreter_for_Chart_Creation;

namespace UnitTestProject
{
    [TestClass]
    public class ErrorMappingStoreTests
    {
        private string _tempFile = null!;
        private ErrorMappingStore _store = null!;

        // Note: This is NOT a test itself. This is an initialization hook that automatically
        // runs BEFORE every single TestMethod to guarantee a brand-new, isolated environment.
        [TestInitialize]
        public void Setup()
        {
            _tempFile = Path.Combine(Path.GetTempPath(), "ErrorMappingStoreTests_" + Guid.NewGuid().ToString("N"), "errors.json");
            _store = new ErrorMappingStore(_tempFile);
        }

        // Note: This is NOT a test. This is an automated teardown hook that runs AFTER every 
        // test finishes to safely clean up the temporary files off the host machine's physical disk.
        [TestCleanup]
        public void Cleanup()
        {
            var dir = Path.GetDirectoryName(_tempFile);
            if (dir != null && Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [TestMethod]
        [Description("Validates that a JSON error file is generated on disk and correctly serializes an incoming error condition.")]
        public async Task RecordErrorAsync_CreatesFileAndSavesEntry()
        {
            // First, let's pretend a syntax error happened in our python script and save it
            await _store.RecordErrorAsync("SyntaxError", "print(hello", "Add quotes");

            // Look on the disk to check if the error file was actually created 
            Assert.IsTrue(File.Exists(_tempFile));

            // Now, pull the most recent errors (up to 10 of them) and see if we caught ours
            var entries = await _store.GetRecentErrorsAsync(10);

            // We expect exactly 1 entry here since we just started fresh
            Assert.AreEqual(1, entries.Count);

            // Make sure the error type matches what we shoved into the store
            Assert.AreEqual("SyntaxError", entries[0].Error);
        }

        [TestMethod]
        [Description("Asserts that when fetching recent historical errors, the collection guarantees a newest-first (LIFO) timestamp ordering restriction.")]
        public async Task GetRecentErrorsAsync_ReturnsNewestFirst()
        {
            // We are logging two consecutive errors to test chronological retrieval 
            await _store.RecordErrorAsync("Error1", "code1");

            // We add a tiny delay to ensure their timestamps are undeniably different
            await Task.Delay(10);
            await _store.RecordErrorAsync("Error2", "code2");

            // Grab the errors to verify they are sorted exactly how the LLM needs them (newest first)
            var entries = await _store.GetRecentErrorsAsync(10);

            // We added two errors, so we should pull two records out
            Assert.AreEqual(2, entries.Count);

            // Confirm that 'Error2'—being the most recent—is sitting pretty at the top
            Assert.AreEqual("Error2", entries[0].Error); // Newest first

            // 'Error1' should follow right behind it 
            Assert.AreEqual("Error1", entries[1].Error);
        }

        [TestMethod]
        [Description("Testing prompt generation utility to guarantee it formats historical Python errors as explicit 'warning clauses' to the LLM agent.")]
        public async Task GetErrorContextForPromptAsync_FormatsCorrectly()
        {
            // We simulate a common file access error scenario to verify prompt building
            await _store.RecordErrorAsync("FileNotFoundError", "open('x.csv')", "Use correct path");

            // Ask the store to build a prompt injection block for the AI using up to 5 history entries
            var context = await _store.GetErrorContextForPromptAsync(5);

            // Check if the resulting text properly warns the AI using our specific template phrases
            StringAssert.Contains(context, "Common past errors to avoid:");

            // The text must actually include the explicit error type
            StringAssert.Contains(context, "FileNotFoundError");

            // And it should include the human-readable 'Fix' string so the AI learns from it
            StringAssert.Contains(context, "Fix: Use correct path");
        }
    }
}
