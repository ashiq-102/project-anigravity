using ML_25_26_05_Python_Code_Tool_Interpreter_for_Chart_Creation.Storage;


namespace UnitTestProject
{
    [TestClass]
    public class PathSafetyTests
    {
        [TestMethod]
        [Description("Ensures strict alphanumeric strings are returned unchanged without mutation.")]
        public void SanitizeReference_LettersAndDigits_ReturnsUnchanged()
        {
            // First, let's sanitize a simple alphanumeric string
            var result = PathSafety.SanitizeReference("Sales2024");

            // It should come back exactly as we passed it in, untouched
            Assert.AreEqual("Sales2024", result);
        }

        [TestMethod]
        [Description("Permits inherently safe punctuation characters (underscore, dash, dot) to pass through sanitization without triggering security filters.")]
        public void SanitizeReference_AllowsUnderscoreDashDot()
        {
            // Now let's try sanitizing a string that has some common, safe punctuation
            var result = PathSafety.SanitizeReference("my_file-v2.csv");

            // These characters are perfectly safe, so the string shouldn't change
            Assert.AreEqual("my_file-v2.csv", result);
        }

        [TestMethod]
        [Description("Demonstrates that hostile or unsupported shell characters are universally swapped with an underscore.")]
        public void SanitizeReference_SpecialCharsReplacedWithUnderscore()
        {
            // Let's pass in a string loaded with potentially dangerous special characters
            var result = PathSafety.SanitizeReference("file@name#1!");

            // The sanitizer should aggressively replace those bad characters with safe underscores
            Assert.AreEqual("file_name_1_", result);
        }

        [TestMethod]
        [Description("Secures against local path traversal attacks by collapsing '..' combinations into safe underscores.")]
        public void SanitizeReference_DoubleDotReplacedToPreventTraversal()
        {
            // Time to test a classic path traversal attack pattern using double dots
            var result = PathSafety.SanitizeReference("..secret");

            // The sanitizer must catch the double dot and squash it into a safe underscore
            Assert.AreEqual("_secret", result);
        }

        [TestMethod]
        [Description("Tests that the system throws an ArgumentException when the input file path is completely empty.")]
        public void SanitizeReference_EmptyString_ThrowsArgumentException()
        {
            try
            {
                // Let's try to pass a completely empty string to the sanitizer
                PathSafety.SanitizeReference("");

                // If we get here, the sanitizer failed to throw an error! We must fail the test.
                Assert.Fail("An ArgumentException should have been thrown, but the code succeeded.");
            }
            catch (ArgumentException)
            {
                // Awesome, the sanitizer threw the expected ArgumentException. This test passes!
            }
        }

        [TestMethod]
        [Description("Tests that the system throws an ArgumentException if the path passed contains exclusively whitespace logic.")]
        public void SanitizeReference_WhitespaceOnly_ThrowsArgumentException()
        {
            try
            {
                // Here we attempt to trick the sanitizer using a string made entirely of blank spaces
                PathSafety.SanitizeReference("   ");

                // Oops, the method didn't crash like we wanted it to. Fail the test.
                Assert.Fail("An ArgumentException should have been thrown, but the code succeeded.");
            }
            catch (ArgumentException)
            {
                // Perfect, catching the ArgumentException means our validation works securely!
            }
        }

        [TestMethod]
        [Description("Tests that the system throws an ArgumentException to trap null injection requests immediately.")]
        public void SanitizeReference_Null_ThrowsArgumentException()
        {
            try
            {
                // Let's go ahead and pass a pure null value and hope the sanitizer intercepts it
                PathSafety.SanitizeReference(null!);

                // It didn't intercept the null! That means we have a bug, so we intentionally fail out here.
                Assert.Fail("An ArgumentException should have been thrown, but the code succeeded.");
            }
            catch (ArgumentException)
            {
                // Great job! The sanitizer blocked the null reference properly.
            }
        }
    }
}
