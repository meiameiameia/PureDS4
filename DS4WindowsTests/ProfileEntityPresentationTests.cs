using DS4WinWPF;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DS4WindowsTests;

[TestClass]
public class ProfileEntityPresentationTests
{
    [TestMethod]
    public void ReadGameOutputDisplayUsesPersistedProfileValue()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path,
                "<DS4Windows><OutputContDevice>ViiperX360</OutputContDevice></DS4Windows>");

            Assert.AreEqual("Xbox 360", ProfileEntity.ReadGameOutputDisplay(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void ReadGameOutputDisplayDegradesForUnreadableProfile()
    {
        Assert.AreEqual("Not saved",
            ProfileEntity.ReadGameOutputDisplay(Path.Combine(Path.GetTempPath(),
                $"missing-{Guid.NewGuid():N}.xml")));
    }
}
