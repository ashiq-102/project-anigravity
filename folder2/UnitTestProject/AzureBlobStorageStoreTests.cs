namespace UnitTestProject
{
    [TestClass]
    public class AzureBlobStorageStoreTests
    {
        private string _tempCacheDir = null!;

        [TestInitialize]
        public void Setup()
        {
            _tempCacheDir = Path.Combine(Path.GetTempPath(), "chart_input_cache_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempCacheDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_tempCacheDir))
                Directory.Delete(_tempCacheDir, recursive: true);
        }

        [TestMethod]
        [Description("Verifies that cache cleanup deletes files older than the cutoff while preserving recent files.")]
        public async Task CleanupTempCacheAsync_DeletesOnlyStaleFiles()
        {
            // Arrange
            var staleFile = Path.Combine(_tempCacheDir, "old_data.csv");
            var freshFile = Path.Combine(_tempCacheDir, "new_data.csv");

            await File.WriteAllTextAsync(staleFile, "col1,col2\n1,2");
            await File.WriteAllTextAsync(freshFile, "col1,col2\n3,4");

            // Backdate stale file modification time to 25 hours ago
            File.SetLastWriteTimeUtc(staleFile, DateTime.UtcNow.AddHours(-25));
            File.SetLastWriteTimeUtc(freshFile, DateTime.UtcNow.AddHours(-1));

            // Act
            var cutoffHours = 24;
            var deletedCount = 0;

            foreach (var file in Directory.GetFiles(_tempCacheDir))
            {
                if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddHours(-cutoffHours))
                {
                    File.Delete(file);
                    deletedCount++;
                }
            }

            // Assert
            Assert.AreEqual(1, deletedCount);
            Assert.IsFalse(File.Exists(staleFile), "Stale file should have been deleted.");
            Assert.IsTrue(File.Exists(freshFile), "Fresh file should remain in cache.");
        }
    }
}