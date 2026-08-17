using ML_25_26_05_Python_Code_Tool_Interpreter_for_Chart_Creation;


namespace UnitTestProject
{
    [TestClass]
    public class PythonCodeValidatorTests
    {
        private readonly PythonCodeValidator _validator = new();

        [TestMethod]
        [Description("Verifies that a barebones matplotlib visualization script successfully clears all system security checks.")]
        public async Task ValidateAsync_ValidMinimalCode_ReturnsTrue()
        {
            // First we write a highly basic, entirely valid script using standard matplotlib syntax
            var code = @"
import matplotlib
import matplotlib.pyplot as plt
plt.plot([1,2], [3,4])
plt.savefig('test.png')
";
            // Hand it to the static analyzer to review before we even think about executing it
            var result = await _validator.ValidateAsync(code, "test.png");

            // Because it's a perfectly safe script, the validator must flag it as completely valid
            Assert.IsTrue(result.IsValid, string.Join(" | ", result.Errors));

            // And naturally, there should be zero error messages tracked against it
            Assert.AreEqual(0, result.Errors.Count);
        }

        [TestMethod]
        [Description("Triggers the sandbox firewall by attempting to import a strict OS-level python module, ensuring absolute failure.")]
        public async Task ValidateAsync_BlockedImport_ReturnsFalse()
        {
            // Now we try to trick the system by sneaking in the lethal 'os' module
            var code = @"
import os
import matplotlib.pyplot as plt
plt.savefig('test.png')
";
            // The static analyzer reviews the script line by line
            var result = await _validator.ValidateAsync(code, "test.png");

            // It absolutely must reject this script entirely to protect the host machine
            Assert.IsFalse(result.IsValid);

            // Let's verify that it specifically caught 'os' in its forbidden import net
            Assert.IsTrue(result.Errors.Any(e => e.Contains("Forbidden import") && e.Contains("os")));
        }

        [TestMethod]
        [Description("Guarantees validation flags any code bypassing the required savefig() method, since images cannot be extracted otherwise.")]
        public async Task ValidateAsync_MissingSavefig_ReturnsFalse()
        {
            // Here's a script that plots something but completely forgets to actually save the image locally
            var code = @"
import matplotlib.pyplot as plt
plt.plot([1,2], [3,4])
";
            // Send it through the structural checkout tier
            var result = await _validator.ValidateAsync(code, "test.png");

            // The validator needs to realize this code is useless for our pipeline and block it
            Assert.IsFalse(result.IsValid);

            // It must specifically remind the LLM to use the savefig method to write the bytes out
            Assert.IsTrue(result.Errors.Any(e => e.Contains("must contain plt.savefig")));
        }

        [TestMethod]
        [Description("Ensures the sandbox violently rejects plt.show() since it freezes headless Python interpreters natively via the interactive GUI lock.")]
        public async Task ValidateAsync_ContainsShow_ReturnsFalse()
        {
            // We write a script that attempts to fire up matplotlib's interactive viewing GUI
            var code = @"
import matplotlib.pyplot as plt
plt.plot([1,2], [3,4])
plt.savefig('test.png')
plt.show()
";
            // Check it against our headless execution restrictions
            var result = await _validator.ValidateAsync(code, "test.png");

            // It needs to fail because interactive mode will infinitely hang the background thread
            Assert.IsFalse(result.IsValid);

            // And it should extract the exact reason why to tell the LLM never to do it again
            Assert.IsTrue(result.Errors.Any(e => e.Contains("must not contain plt.show()")));
        }

        [TestMethod]
        [Description("Invokes the AST parser strictly for identifying malformed Python brackets prior to live file execution.")]
        public async Task ValidateAsync_SyntaxError_ReturnsFalse()
        {
            // We intentionally bork the brackets so Python can't even compile this
            var code = @"
import matplotlib.pyplot as plt
plt.plot([1,2, [3,4])
plt.savefig('test.png')
";
            // Ask the validator to dry-run parse it via ast
            var result = await _validator.ValidateAsync(code, "test.png");

            // The AST scanner should choke and flag it
            Assert.IsFalse(result.IsValid);

            // And feed back the specific syntax parse breakdown to the AI
            Assert.IsTrue(result.Errors.Any(e => e.Contains("Python syntax error")));
        }

        [TestMethod]
        [Description("Traps the eval() operator usage completely to strictly forbid the execution of arbitrary un-sanitized string loads.")]
        public async Task ValidateAsync_BlockedCall_ReturnsFalse()
        {
            // Let's sneak in a malicious string execution payload using Python's native eval()
            var code = @"
import matplotlib.pyplot as plt
eval('print(1)')
plt.savefig('test.png')
";
            // Run the blocklist sweeps across the script body
            var result = await _validator.ValidateAsync(code, "test.png");

            // The validator must stop eval() from running dead in its tracks
            Assert.IsFalse(result.IsValid);

            // And ensure we know eval() was the exact culprit pulled from the text
            Assert.IsTrue(result.Errors.Any(e => e.Contains("Forbidden call detected") && e.Contains("eval")));
        }
    }
}
