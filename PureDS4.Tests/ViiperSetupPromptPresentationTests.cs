using DS4WinWPF.DS4Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DS4WindowsTests
{
    [TestClass]
    public class ViiperSetupPromptPresentationTests
    {
        [TestMethod]
        public void RequiredSetupUsesProductLanguageAndPreservesDegradedMode()
        {
            ViiperSetupPromptPresentation presentation =
                ViiperSetupPrompt.CreatePresentation(false, false, false,
                    mandatoryRepairRequired: true);

            Assert.AreEqual("Set up game output", presentation.Heading);
            Assert.AreEqual("Set up game output", presentation.PrimaryAction);
            Assert.AreEqual("Continue without game output",
                presentation.ContinueAction);
            Assert.IsFalse(presentation.ShowSuppressPrompt);
            Assert.IsFalse((presentation.Heading + presentation.Summary +
                presentation.Requirements + presentation.PrimaryAction)
                .Contains("VIIPER", System.StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse((presentation.Heading + presentation.Summary +
                presentation.Requirements + presentation.PrimaryAction)
                .Contains("USB-IP", System.StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void UnsafeUsbConflictKeepsSafeContinuePathAndHidesPortableChoice()
        {
            ViiperSetupPromptPresentation presentation =
                ViiperSetupPrompt.CreatePresentation(true, false, false,
                    mandatoryRepairRequired: true);

            Assert.AreEqual("Resolve USB conflict", presentation.PrimaryAction);
            Assert.AreEqual("Continue without game output",
                presentation.ContinueAction);
            Assert.IsFalse(presentation.ShowPortableAction);
            Assert.IsFalse(presentation.ShowSuppressPrompt);
        }

        [TestMethod]
        public void VerifiedRepairExplainsTheRestartWithoutBackendNames()
        {
            ViiperSetupPromptPresentation presentation =
                ViiperSetupPrompt.CreatePresentation(false, true, true,
                    mandatoryRepairRequired: true);

            Assert.AreEqual("Repair game output", presentation.PrimaryAction);
            StringAssert.Contains(presentation.Requirements, "Restart Windows");
            Assert.IsFalse((presentation.Heading + presentation.Summary +
                presentation.Requirements + presentation.PrimaryAction)
                .Contains("VIIPER", System.StringComparison.OrdinalIgnoreCase));
        }
    }
}
