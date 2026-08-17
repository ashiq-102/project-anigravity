using ChartCreationMCPServer.Storage;

namespace UnitTestProject
{
    [TestClass]
    public class PathSafetyTests
    {
        // ── SanitizeReference ───────────────────────────────────────────────

        [TestMethod]
        [Description("Ensures strict alphanumeric strings are returned unchanged without mutation.")]
        public void SanitizeReference_LettersAndDigits_ReturnsUnchanged()
        {
            var result = PathSafety.SanitizeReference("Sales2024");
            Assert.AreEqual("Sales2024", result);
        }

        [TestMethod]
        [Description("Permits inherently safe punctuation characters (underscore, dash, dot) to pass through sanitization.")]
        public void SanitizeReference_AllowsUnderscoreDashDot()
        {
            var result = PathSafety.SanitizeReference("my_file-v2.csv");
            Assert.AreEqual("my_file-v2.csv", result);
        }

        [TestMethod]
        [Description("Demonstrates that unsupported shell characters are swapped with an underscore.")]
        public void SanitizeReference_SpecialCharsReplacedWithUnderscore()
        {
            var result = PathSafety.SanitizeReference("file@name#1!");
            Assert.AreEqual("file_name_1_", result);
        }

        [TestMethod]
        [Description("Secures against path traversal attacks by collapsing '..' combinations into safe underscores.")]
        public void SanitizeReference_DoubleDotReplacedToPreventTraversal()
        {
            var result = PathSafety.SanitizeReference("..secret");
            Assert.AreEqual("_secret", result);
        }

        [TestMethod]
        [Description("Tests that the system throws an ArgumentException when the input file path is empty.")]
        public void SanitizeReference_EmptyString_ThrowsArgumentException()
        {
            try
            {
                PathSafety.SanitizeReference("");
                Assert.Fail("An ArgumentException should have been thrown.");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        [Description("Tests that the system throws an ArgumentException to trap null injection requests immediately.")]
        public void SanitizeReference_Null_ThrowsArgumentException()
        {
            try
            {
                PathSafety.SanitizeReference(null!);
                Assert.Fail("An ArgumentException should have been thrown.");
            }
            catch (ArgumentException) { }
        }

        // ── SanitizeTeam ────────────────────────────────────────────────────

        [TestMethod]
        [Description("A simple lowercase team name passes through unchanged.")]
        public void SanitizeTeam_SimpleName_ReturnsUnchanged()
        {
            Assert.AreEqual("sohel", PathSafety.SanitizeTeam("sohel"));
        }

        [TestMethod]
        [Description("Uppercase is lowered and spaces become single hyphens.")]
        public void SanitizeTeam_MixedCaseWithSpaces_IsNormalised()
        {
            Assert.AreEqual("team-alpha", PathSafety.SanitizeTeam("Team Alpha"));
        }

        [TestMethod]
        [Description("Special characters collapse into single hyphens, no leading/trailing hyphen.")]
        public void SanitizeTeam_SpecialChars_CollapseToSingleHyphen()
        {
            Assert.AreEqual("team-alpha", PathSafety.SanitizeTeam("!!Team @@ Alpha!!"));
        }

        [TestMethod]
        [Description("Null, empty, or whitespace-only names fall back to 'default'.")]
        public void SanitizeTeam_EmptyOrNull_ReturnsDefault()
        {
            Assert.AreEqual("default", PathSafety.SanitizeTeam(null));
            Assert.AreEqual("default", PathSafety.SanitizeTeam(""));
            Assert.AreEqual("default", PathSafety.SanitizeTeam("   "));
        }

        [TestMethod]
        [Description("A name made entirely of special characters falls back to 'default'.")]
        public void SanitizeTeam_AllSpecialChars_ReturnsDefault()
        {
            Assert.AreEqual("default", PathSafety.SanitizeTeam("!@#$%"));
        }
    }
}