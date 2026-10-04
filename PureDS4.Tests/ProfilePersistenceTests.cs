using DS4Windows;
using DS4WinWPF;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DS4WindowsTests;

[TestClass]
[DoNotParallelize]
public class ProfilePersistenceTests
{
    private string root;
    private const string Original = "<DS4Windows><Color>1,2,3</Color></DS4Windows>";
    private const string Updated = "<DS4Windows><Color>4,5,6</Color></DS4Windows>";

    [TestInitialize]
    public void Initialize() => root = Directory.CreateTempSubdirectory("PureDS4-profile-").FullName;

    [TestCleanup]
    public void Cleanup()
    {
        foreach (string path in Directory.GetFiles(root)) File.SetAttributes(path, FileAttributes.Normal);
        Directory.Delete(root, recursive: true);
    }

    [TestMethod]
    public void InterruptedWritePreservesLastValidProfileAndRemovesTemporaryFile()
    {
        string path = Path.Combine(root, "Default.xml");
        File.WriteAllText(path, Original);
        Assert.ThrowsException<IOException>(() => ProfilePersistence.Save(path, Updated,
            writeTemporary: (temp, xml) =>
            {
                File.WriteAllText(temp, "incomplete");
                throw new IOException("simulated disk-full write");
            }));
        Assert.AreEqual(Original, File.ReadAllText(path));
        Assert.AreEqual(1, Directory.GetFiles(root).Length);
    }

    [TestMethod]
    public void SuccessfulReplacementAndRenamePersistCompleteXml()
    {
        string original = Path.Combine(root, "Default.xml");
        string renamed = Path.Combine(root, "Renamed.xml");
        File.WriteAllText(original, Original);
        ProfilePersistence.Save(original, Updated);
        Assert.AreEqual(Updated, File.ReadAllText(original));
        ProfilePersistence.Save(renamed, Original, original);
        Assert.IsFalse(File.Exists(original));
        Assert.AreEqual(Original, File.ReadAllText(renamed));
        Assert.AreEqual(1, Directory.GetFiles(root).Length);
    }

    [TestMethod]
    public void RenameCollisionAndFailedWritePreserveOriginalAndOtherProfile()
    {
        string original = Path.Combine(root, "Default.xml");
        string target = Path.Combine(root, "Other.xml");
        File.WriteAllText(original, Original);
        File.WriteAllText(target, Updated);
        Assert.ThrowsException<IOException>(() => ProfilePersistence.Save(target, Original, original));
        Assert.AreEqual(Updated, File.ReadAllText(target));
        File.Delete(target);
        Assert.ThrowsException<IOException>(() => ProfilePersistence.Save(target, Updated, original,
            writeTemporary: (_, _) => throw new IOException("write failed")));
        Assert.AreEqual(Original, File.ReadAllText(original));
        Assert.IsFalse(File.Exists(target));
    }

    [TestMethod]
    public void FailedOriginalRemovalPreservesBothCopiesAndReportsIncompleteRename()
    {
        string original = Path.Combine(root, "Default.xml");
        string target = Path.Combine(root, "Renamed.xml");
        File.WriteAllText(original, Original);
        using var held = new FileStream(original, FileMode.Open, FileAccess.Read, FileShare.Read);
        var error = Assert.ThrowsException<IOException>(() => ProfilePersistence.Save(target, Updated, original));
        StringAssert.Contains(error.Message, "Both copies");
        Assert.AreEqual(Original, File.ReadAllText(original));
        Assert.AreEqual(Updated, File.ReadAllText(target));
    }

    [TestMethod]
    public void CreationCannotOverwriteAnExistingProfile()
    {
        string path = Path.Combine(root, "Default.xml");
        File.WriteAllText(path, Original);
        Assert.ThrowsException<IOException>(() => ProfilePersistence.Save(path, Updated, overwrite: false));
        Assert.AreEqual(Original, File.ReadAllText(path));
    }

    [TestMethod]
    public void ReadOnlyDestinationPreservesOriginalAndCleansIncompleteWrite()
    {
        string path = Path.Combine(root, "Default.xml");
        File.WriteAllText(path, Original);
        File.SetAttributes(path, FileAttributes.ReadOnly);
        Assert.ThrowsException<UnauthorizedAccessException>(() => ProfilePersistence.Save(path, Updated));
        Assert.AreEqual(Original, File.ReadAllText(path));
        Assert.AreEqual(1, Directory.GetFiles(root).Length);
    }

    [TestMethod]
    public void SaveFailureDoesNotRenameEntityOrAnnounceSuccess()
    {
        string previous = Global.appdatapath;
        try
        {
            Global.appdatapath = root;
            Directory.CreateDirectory(Path.Combine(root, "Profiles"));
            string original = Path.Combine(root, "Profiles", "Default.xml");
            string collision = Path.Combine(root, "Profiles", "Other.xml");
            File.WriteAllText(original, Original);
            File.WriteAllText(collision, Updated);
            var entity = new ProfileEntity { Name = "Default" };
            int changed = 0;
            entity.NameChanged += (_, _) => changed++;
            entity.GameOutputDisplayChanged += (_, _) => changed++;
            entity.ProfileSaved += (_, _) => changed++;
            Assert.IsFalse(entity.SaveProfile(0, "Other"));
            Assert.AreEqual("Default", entity.Name);
            Assert.AreEqual(0, changed);
            Assert.AreEqual(Original, File.ReadAllText(original));
            Assert.AreEqual(Updated, File.ReadAllText(collision));
        }
        finally { Global.appdatapath = previous; }
    }
}
