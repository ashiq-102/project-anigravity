using Microsoft.Extensions.Configuration;
using ChartCreationMCPServer.Execution;
using ChartCreationMCPServer.Storage;
using ChartCreationMCPServer.Tools;
using Microsoft.AspNetCore.Http;
using Moq;

namespace UnitTestProject
{
    [TestClass]
    public class ChartPluginTests
    {
        private Mock<IStorageStore> _mockStore = null!;
        private PythonCodeValidator _validator = null!;
        private PythonCodeExecutor _executor = null!;
        private IConfigurationSection _pythonConfig = null!;
        private ChartPlugin _plugin = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockStore = new Mock<IStorageStore>();
            _validator = new PythonCodeValidator();
            _executor = new PythonCodeExecutor();

            // Empty config — GetValue<T> falls back to its own defaults (true/true) when the
            // "Python" section has no keys, same as an appsettings.json with the section omitted.
            _pythonConfig = new ConfigurationBuilder()
                .Build()
                .GetSection("Python");

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["Team-Name"] = "test-team";

            var accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(httpContext);

            _plugin = new ChartPlugin(_mockStore.Object, _validator, _executor, accessor.Object, _pythonConfig);
        }

        [TestMethod]
        [Description("GenerateChart rejects empty python code with an error message.")]
        public async Task GenerateChart_EmptyCode_ReturnsError()
        {
            var result = await _plugin.GenerateChart("", "c1");
            StringAssert.Contains(result, "ERROR: pythonCode is empty");
        }

        [TestMethod]
        [Description("GenerateChart rejects a request with no chart ID.")]
        public async Task GenerateChart_EmptyId_ReturnsError()
        {
            var result = await _plugin.GenerateChart("print('hello')", "");
            StringAssert.Contains(result, "ERROR: chartId is required");
        }

        [TestMethod]
        [Description("Validation failures (forbidden imports, plt.show()) are trapped before execution.")]
        public async Task GenerateChart_ValidationError_ReturnsErrorMessage()
        {
            var code = @"
import os
import matplotlib.pyplot as plt
plt.plot([1,2,3])
plt.show()
";
            var result = await _plugin.GenerateChart(code, "bad_chart");

            StringAssert.StartsWith(result, "VALIDATION ERROR");
            StringAssert.Contains(result, "Forbidden import");
            StringAssert.Contains(result, "must not contain plt.show()");
        }

        [TestMethod]
        [Description("ResolveFilePath delegates to storage with the current team and converts to forward slashes.")]
        public void ResolveFilePath_DelegatesToStorage()
        {
            _mockStore
                .Setup(s => s.GetAbsolutePath("MY_DATA", "test-team"))
                .Returns(@"C:\store\file.csv");

            var result = _plugin.ResolveFilePath("MY_DATA");

            Assert.AreEqual("C:/store/file.csv", result);
        }

        [TestMethod]
        [Description("Verifies that when HttpContext is missing a Team-Name header, ChartPlugin falls back to 'default'.")]
        public void ResolveFilePath_MissingTeamHeader_FallsBackToDefaultTeam()
        {
            var emptyAccessor = new Mock<IHttpContextAccessor>();
            emptyAccessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());

            var plugin = new ChartPlugin(_mockStore.Object, _validator, _executor, emptyAccessor.Object, _pythonConfig);

            _mockStore
                .Setup(s => s.GetAbsolutePath("MY_DATA", "default"))
                .Returns(@"C:\store\file.csv");

            var result = plugin.ResolveFilePath("MY_DATA");

            Assert.AreEqual("C:/store/file.csv", result);
            _mockStore.Verify(s => s.GetAbsolutePath("MY_DATA", "default"), Times.Once);
        }

        [TestMethod]
        [Description("Ensures that if storage upload fails after chart generation, a user-friendly degraded error is returned.")]
        public async Task GenerateChart_StorageUploadFails_ReturnsDegradedMessage()
        {
            _mockStore
                .Setup(s => s.UploadChartAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Azure connection failed"));

            var code = @"
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
plt.plot([1, 2], [3, 4])
plt.savefig(r'OUTPUT_PATH', dpi=150, bbox_inches='tight')
";
            var result = await _plugin.GenerateChart(code, "test_chart");

            StringAssert.Contains(result, "could not be saved to cloud storage");
        }

        [TestMethod]
        [Description("UploadFileAsync rejects content that isn't valid Base64.")]
        public async Task UploadFileAsync_InvalidBase64_ReturnsError()
        {
            var result = await _plugin.UploadFileAsync("data.csv", "not-valid-base64!!!");
            StringAssert.Contains(result, "not valid Base64");
        }

        [TestMethod]
        [Description("UploadFileAsync rejects an empty file name.")]
        public async Task UploadFileAsync_EmptyFileName_ReturnsError()
        {
            var content = Convert.ToBase64String(new byte[] { 1, 2, 3 });
            var result = await _plugin.UploadFileAsync("", content);
            StringAssert.Contains(result, "fileName is required");
        }

        [TestMethod]
        [Description("UploadFileAsync decodes valid content and forwards it to storage under the current team, returning the derived reference name.")]
        public async Task UploadFileAsync_ValidContent_CallsStoreWithDerivedReferenceName()
        {
            _mockStore
                .Setup(s => s.UploadAsync(It.IsAny<string>(), "test-team"))
                .ReturnsAsync("sales");

            var content = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("col1,col2\n1,2"));

            var result = await _plugin.UploadFileAsync("sales.csv", content);

            StringAssert.Contains(result, "Reference name: 'sales'");
            _mockStore.Verify(s => s.UploadAsync(It.Is<string>(p => p.EndsWith("sales.csv")), "test-team"), Times.Once);
        }

        [TestMethod]
        [Description("ListUploadedFiles returns a friendly message when the store has no files.")]
        public void ListUploadedFiles_NoFiles_ReturnsNoFilesMessage()
        {
            _mockStore.Setup(s => s.List("test-team", null)).Returns(Array.Empty<string>());
            var result = _plugin.ListUploadedFiles();
            Assert.AreEqual("OK: No files in input store.", result);
        }

        [TestMethod]
        [Description("ListUploadedFiles passes a provided filter through to the store.")]
        public void ListUploadedFiles_WithFilter_PassesFilterToStore()
        {
            _mockStore.Setup(s => s.List("test-team", "sales")).Returns(new[] { "sales_2024" });
            var result = _plugin.ListUploadedFiles("sales");
            StringAssert.Contains(result, "sales_2024");
            _mockStore.Verify(s => s.List("test-team", "sales"), Times.Once);
        }

        [TestMethod]
        [Description("DeleteUploadedFiles reports the count returned by the store.")]
        public void DeleteUploadedFiles_WithFilter_PassesFilterToStore()
        {
            _mockStore.Setup(s => s.Delete("test-team", "sales")).Returns(3);
            var result = _plugin.DeleteUploadedFiles("sales");
            StringAssert.Contains(result, "Deleted 3 file(s)");
        }

        [TestMethod]
        [Description("PreviewUploadedFile delegates to the store's ReadTextAsync with the current team.")]
        public async Task PreviewUploadedFile_DelegatesToStore()
        {
            _mockStore
                .Setup(s => s.ReadTextAsync("sales", "test-team", 2000))
                .ReturnsAsync("col1,col2\n1,2");

            var result = await _plugin.PreviewUploadedFile("sales");

            Assert.AreEqual("col1,col2\n1,2", result);
        }

        [TestMethod]
        [Description("ListGeneratedCharts reports a friendly message when the team has no charts yet.")]
        public void ListGeneratedCharts_NoCharts_ReturnsFriendlyMessage()
        {
            _mockStore.Setup(s => s.ListCharts("test-team")).Returns(new List<(string, string)>());
            var result = _plugin.ListGeneratedCharts();
            Assert.AreEqual("No charts have been generated by this team yet.", result);
        }
    }
}