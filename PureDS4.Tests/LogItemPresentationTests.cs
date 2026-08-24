using DS4WinWPF;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DS4WindowsTests;

[TestClass]
public class LogItemPresentationTests
{
    [TestMethod]
    public void SeverityUsesWordsInsteadOfColorAlone()
    {
        Assert.AreEqual("Information", new LogItem().Severity);
        Assert.AreEqual("Warning", new LogItem { Warning = true }.Severity);
    }
}
