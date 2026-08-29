using DS4WinWPF.DS4Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DS4WindowsTests
{
    /// <summary>
    /// The copy shown when PureDS4 requires the old application closed
    /// before continuing (step 2 of the replacement flow). Checked
    /// separately from the window so wording changes cannot silently start
    /// claiming PureDS4 closes the other application for the user.
    /// </summary>
    [TestClass]
    public class LegacyApplicationRunningPresentationTests
    {
        [DataTestMethod]
        [DataRow("DS4Windows")]
        [DataRow("DS4Windows Reworked")]
        public void NamesTheDetectedProductInBothHeadingAndExplanation(
            string detectedProductName)
        {
            LegacyApplicationClosePresentation presentation =
                LegacyApplicationRunningWindow.CreatePresentation(
                    detectedProductName);

            StringAssert.Contains(presentation.Heading, detectedProductName);
            StringAssert.Contains(presentation.Explanation,
                detectedProductName);
        }

        [TestMethod]
        public void NeverClaimsPureDS4ClosesTheOtherApplication()
        {
            LegacyApplicationClosePresentation presentation =
                LegacyApplicationRunningWindow.CreatePresentation(
                    "DS4Windows Reworked");

            StringAssert.Contains(presentation.Explanation,
                "will not close it for you");
        }

        [TestMethod]
        public void FallsBackToTheGenericNameWhenNoneIsGiven()
        {
            LegacyApplicationClosePresentation presentation =
                LegacyApplicationRunningWindow.CreatePresentation(null);

            StringAssert.Contains(presentation.Heading, "DS4Windows");
        }
    }
}
