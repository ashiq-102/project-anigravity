using ML_25_26_05_Python_Code_Tool_Interpreter_for_Chart_Creation.Storage;
using Moq;

namespace UnitTestProject
{
    [TestClass]
    public class StorageToolTests
    {
        private Mock<IStorageStore> _mockStore = null!;
        private StorageTool _tool = null!;

        // Note: This is NOT a test itself. This is an initialization hook that automatically
        // runs BEFORE every single TestMethod to guarantee a brand-new, isolated environment.
        [TestInitialize]
        public void Setup()
        {
            _mockStore = new Mock<IStorageStore>();
            _tool = new StorageTool(_mockStore.Object);
        }

        [TestMethod]
        [Description("Validates that upload proxies successfully translate native completion tasks into consumer 'OK:' formatted protocol responses.")]
        public async Task UploadAsync_ReturnsOkMessage()
        {
            // First we train our mocked filesystem to expect an upload command and pretend it succeeded instantly
            _mockStore.Setup(s => s.UploadAsync("src.csv", "REF")).Returns(Task.CompletedTask);

            // Now we trigger the tool's upload routine just as the AI might call it
            var result = await _tool.UploadAsync("src.csv", "REF");

            // Check if the resulting string is formatted exactly as we promised the LLM
            StringAssert.StartsWith(result, "OK:");
            StringAssert.Contains(result, "REF");
        }

        [TestMethod]
        [Description("Confirms list fetching behaviors neatly convert absolute empty arrays into explicit 'No files' strings instead of leaving parsing ambiguity.")]
        public void List_NoFiles_ReturnsNoFilesMessage()
        {
            // We tell our mock store that there are absolutely zero files available right now
            _mockStore.Setup(s => s.List(null)).Returns(Enumerable.Empty<string>());

            // Ask the tool for its human-readable list payload
            var result = _tool.List();

            // Make sure it translates "empty" into something the AI actually understands
            Assert.AreEqual("OK: No files.", result);
        }

        [TestMethod]
        [Description("Validates accurate conversion of mock directory collections into string buffers via native list mapping.")]
        public void List_WithFiles_ReturnsFileNames()
        {
            // Let's seed the mock store with a couple of fake CSV files
            _mockStore.Setup(s => s.List(null)).Returns(new[] { "a.csv", "b.csv" });

            // Grab the formatted string from the tool abstraction
            var result = _tool.List();

            // The resulting text buffer must include the exact names of the files we just fed it
            StringAssert.Contains(result, "a.csv");
            StringAssert.Contains(result, "b.csv");
        }

        [TestMethod]
        [Description("Connects mock deletion cascades cleanly via expected payload count feedback loops.")]
        public void Delete_ReturnsDeletedCount()
        {
            // We pretend that firing a delete command purged exactly 3 files from disk
            _mockStore.Setup(s => s.Delete(null)).Returns(3);

            // Trigger the tool's delete cascade function
            var result = _tool.Delete();

            // And finally, ensure the string output explicitly tells the LLM that '3' files are gone
            StringAssert.Contains(result, "3");
        }

        [TestMethod]
        [Description("Translates raw database aggregation queries via memory enumeration counts efficiently into standard strings.")]
        public void Count_ReturnsCount()
        {
            // Let's provide a mock list of two fake files
            _mockStore.Setup(s => s.List(null)).Returns(new[] { "x.csv", "y.csv" });

            // Ask the tool how many files it thinks it has
            var result = _tool.Count();

            // The tool must correctly count them and embed the number '2' in its response text
            StringAssert.Contains(result, "2");
        }

        [TestMethod]
        [Description("Maps string inputs against physical disk references exactly matching mock definitions.")]
        public void ResolvePath_DelegatesAndReturnsPath()
        {
            // Teach the store to convert our short reference 'DATA' into an absolute mock C: path
            _mockStore.Setup(s => s.GetAbsolutePath("DATA")).Returns(@"C:\store\DATA.csv");

            // Now run the actual path resolution function that the LLM utilizes dynamically
            var result = _tool.ResolvePath("DATA");

            // The string returned must be the fully qualified, mocked absolute path
            Assert.AreEqual(@"C:\store\DATA.csv", result);
        }
    }
}
