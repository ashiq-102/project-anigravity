using ML_25_26_05_Python_Code_Tool_Interpreter_for_Chart_Creation.Storage;


namespace UnitTestProject
{
    [TestClass]
    public class LocalFileStoreTests
    {
        private string _tempRoot = null!;
        private string _sourceDir = null!;
        private LocalFileStore _store = null!;

        // Note: This is NOT a test itself. This is an initialization hook that automatically
        // runs BEFORE every single TestMethod to guarantee a brand-new, isolated environment.
        [TestInitialize]
        public void Setup()
        {
            var baseDir = Path.Combine(Path.GetTempPath(), "LocalFileStoreTests_" + Guid.NewGuid().ToString("N"));
            _tempRoot = Path.Combine(baseDir, "store");
            _sourceDir = Path.Combine(baseDir, "sources");
            Directory.CreateDirectory(_sourceDir);
            _store = new LocalFileStore(_tempRoot);
        }

        // Note: This is NOT a test. This is an automated teardown hook that runs AFTER every 
        // test finishes to safely clean up the temporary files off the host machine's physical disk.
        [TestCleanup]
        public void Cleanup()
        {
            var baseDir = Path.GetDirectoryName(_tempRoot)!;
            if (Directory.Exists(baseDir))
                Directory.Delete(baseDir, recursive: true);
        }

        // ── Upload + List ───────────────────────────────────────────────────

        [TestMethod]
        [Description("Ensures that uploading a file properly copies the stream to the destination and shows up in the listing payload.")]
        public async Task UploadAsync_CopiesFile_AndListReturnsIt()
        {
            // First we need to mock up a physical file to upload with some fake CSV data
            var srcFile = Path.Combine(_sourceDir, "source.csv");
            await File.WriteAllTextAsync(srcFile, "a,b\n1,2");

            // Tell the tool to slurp it across into the storage vault under the alias SALES
            await _store.UploadAsync(srcFile, "SALES");

            // Now, we ask the vault to list what it has currently archived
            var files = _store.List().ToList();

            // There should be exactly 1 file because we just created a fresh vault
            Assert.AreEqual(1, files.Count);

            // The file name must have acquired the .csv extension appended onto our SALES alias
            Assert.IsTrue(files[0].Contains("SALES.csv"));
        }

        [TestMethod]
        [Description("Asserts that substring matching queries restrict the results returned from the directory file listing.")]
        public async Task List_WithFilter_ReturnsOnlyMatching()
        {
            // Let's create two dummy files in our source directory to upload
            var src1 = Path.Combine(_sourceDir, "a.csv");
            var src2 = Path.Combine(_sourceDir, "b.csv");
            await File.WriteAllTextAsync(src1, "x");
            await File.WriteAllTextAsync(src2, "y");

            // Upload both of them with completely distinct reference aliases
            await _store.UploadAsync(src1, "ALPHA");
            await _store.UploadAsync(src2, "BETA");

            // Perform a queried list operation targeting only 'ALPHA'
            var filtered = _store.List("ALPHA").ToList();

            // We expect the vault to strictly ignore 'BETA' and only return 1 item
            Assert.AreEqual(1, filtered.Count);

            // And that singular item better be our ALPHA file
            Assert.IsTrue(filtered[0].Contains("ALPHA"));
        }

        // ── Delete ──────────────────────────────────────────────────────────

        [TestMethod]
        [Description("Validates that targeting a reference with Delete permanently purges it from the underlying file system.")]
        public async Task Delete_RemovesFile_ListReturnsEmpty()
        {
            // Set up our victim file that we're about to instantly delete
            var src = Path.Combine(_sourceDir, "c.csv");
            await File.WriteAllTextAsync(src, "data");

            // Shove it into the vault
            await _store.UploadAsync(src, "TEMP");

            // Drop the hammer and command the vault to purge anything matching 'TEMP'
            var deleted = _store.Delete("TEMP");

            // It should successfully report back that exactly 1 file was obliterated
            Assert.AreEqual(1, deleted);

            // Double check that the vault is now completely bare
            var remaining = _store.List().ToList();
            Assert.AreEqual(0, remaining.Count);
        }

        // ── ReadTextAsync ───────────────────────────────────────────────────

        [TestMethod]
        [Description("Confirms standard read payload delivery parses bytes to string successfully.")]
        public async Task ReadTextAsync_ReturnsFileContent()
        {
            // Once again we bake a dummy source file
            var src = Path.Combine(_sourceDir, "d.csv");
            await File.WriteAllTextAsync(src, "hello,world");

            // Upload it under the friendly name 'HELLO'
            await _store.UploadAsync(src, "HELLO");

            // Instruct the vault to extract the raw text back out into our memory scope
            var content = await _store.ReadTextAsync("HELLO.csv");

            // And ensure no data corruption happened along the way
            StringAssert.Contains(content, "hello,world");
        }

        [TestMethod]
        [Description("Verifies that attempting to read a massive payload triggers string truncation to protect process memory constraints.")]
        public async Task ReadTextAsync_TruncatesLongContent()
        {
            // Here we construct a ridiculously large string representation intentionally
            var src = Path.Combine(_sourceDir, "big.csv");
            var longText = new string('x', 5000);

            // Write it out and push it to the locked vault
            await File.WriteAllTextAsync(src, longText);
            await _store.UploadAsync(src, "BIG");

            // We severely cap the max characters it's allowed to return to just 100
            var content = await _store.ReadTextAsync("BIG.csv", maxChars: 100);

            // The method must have appended the '[truncated]' warning banner to alert the LLM
            StringAssert.Contains(content, "[truncated]");

            // And the length must absolutely be significantly shorter than our original 5000 chars
            Assert.IsTrue(content.Length < 5000);
        }

        // ── GetAbsolutePath ─────────────────────────────────────────────────

        [TestMethod]
        [Description("Guarantees that a query for an absolute file path successfully translates from an abstract reference string.")]
        public async Task GetAbsolutePath_ReturnsMatchingFile()
        {
            // Drop a generic file onto disk for us to target
            var src = Path.Combine(_sourceDir, "e.csv");
            await File.WriteAllTextAsync(src, "data");

            // Upload to the vault system
            await _store.UploadAsync(src, "RESOLVE");

            // Force the vault to cough up the hidden absolute directory path for this alias
            var path = _store.GetAbsolutePath("RESOLVE");

            // It better physically exist exactly where it says it does
            Assert.IsTrue(File.Exists(path));

            // And the filename itself needs to match our targeted reference
            StringAssert.Contains(path, "RESOLVE");
        }
    }
}
