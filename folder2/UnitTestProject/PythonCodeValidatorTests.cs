using ChartCreationMCPServer.Execution;


namespace UnitTestProject
{
    [TestClass]
    public class PythonCodeValidatorTests
    {
        private readonly PythonCodeValidator _validator = new();

        [TestMethod]
        [Description("Verifies that a barebones matplotlib visualization script successfully clears all security checks.")]
        public async Task ValidateAsync_ValidMinimalCode_ReturnsTrue()
        {
            var code = @"
import matplotlib
import matplotlib.pyplot as plt
plt.plot([1,2], [3,4])
plt.savefig('test.png')
";
            var result = await _validator.ValidateAsync(code, "test.png");

            Assert.IsTrue(result.IsValid, string.Join(" | ", result.Errors));
            Assert.AreEqual(0, result.Errors.Count);
        }

        [TestMethod]
        [Description("Triggers the sandbox firewall by attempting to import an OS-level python module.")]
        public async Task ValidateAsync_BlockedImport_ReturnsFalse()
        {
            var code = @"
import os
import matplotlib.pyplot as plt
plt.savefig('test.png')
";
            var result = await _validator.ValidateAsync(code, "test.png");

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("Forbidden import") && e.Contains("os")));
        }

        [TestMethod]
        [Description("Guarantees validation flags any code bypassing the required savefig() method.")]
        public async Task ValidateAsync_MissingSavefig_ReturnsFalse()
        {
            var code = @"
import matplotlib.pyplot as plt
plt.plot([1,2], [3,4])
";
            var result = await _validator.ValidateAsync(code, "test.png");

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("must contain plt.savefig")));
        }

        [TestMethod]
        [Description("Ensures the sandbox rejects plt.show() since it freezes headless Python interpreters.")]
        public async Task ValidateAsync_ContainsShow_ReturnsFalse()
        {
            var code = @"
import matplotlib.pyplot as plt
plt.plot([1,2], [3,4])
plt.savefig('test.png')
plt.show()
";
            var result = await _validator.ValidateAsync(code, "test.png");

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("must not contain plt.show()")));
        }

        [TestMethod]
        [Description("Invokes the AST parser strictly for identifying malformed Python syntax prior to execution.")]
        public async Task ValidateAsync_SyntaxError_ReturnsFalse()
        {
            var code = @"
import matplotlib.pyplot as plt
plt.plot([1,2, [3,4])
plt.savefig('test.png')
";
            var result = await _validator.ValidateAsync(code, "test.png");

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("Python syntax error")));
        }

        [TestMethod]
        [Description("Traps the eval() operator usage to forbid execution of arbitrary string loads.")]
        public async Task ValidateAsync_BlockedCall_ReturnsFalse()
        {
            var code = @"
import matplotlib.pyplot as plt
eval('print(1)')
plt.savefig('test.png')
";
            var result = await _validator.ValidateAsync(code, "test.png");

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("Forbidden call detected") && e.Contains("eval")));
        }

        [TestMethod]
        [Description("Verifies that submodules of blocked packages (e.g. from os.path import exists) are properly intercepted.")]
        public async Task ValidateAsync_FromImportBlockedSubmodule_ReturnsFalse()
        {
            var code = @"
from os.path import exists
import matplotlib.pyplot as plt
plt.savefig('test.png')
";
            var result = await _validator.ValidateAsync(code, "test.png");

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("Forbidden import")));
        }
    }
}