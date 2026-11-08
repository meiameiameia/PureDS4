using PureDS4.Bootstrapper;

namespace DS4WindowsTests;

[TestClass]
public class InstallerPlanningPolicyTests
{
    [DataTestMethod]
    [DataRow(false, false, false, false)]
    [DataRow(false, false, true, false)]
    [DataRow(false, true, false, false)]
    [DataRow(false, true, true, true)]
    [DataRow(true, false, false, false)]
    [DataRow(true, false, true, false)]
    [DataRow(true, true, false, false)]
    [DataRow(true, true, true, false)]
    public void OnlyTheIncomingPrimaryManagedUpgradeDefersInfrastructure(
        bool recovery, bool installs, bool relatedUpgrade, bool expected)
    {
        Assert.AreEqual(expected, InstallerPlanningPolicy.DeferInfrastructure(recovery, installs, relatedUpgrade));
    }

    [DataTestMethod]
    [DataRow(false, false, false)]
    [DataRow(false, true, false)]
    [DataRow(true, false, false)]
    [DataRow(true, true, true)]
    public void OnlyAnOutgoingRelatedUninstallUsesTheParentsTransaction(
        bool upgradeRelation, bool uninstalls, bool expected)
    {
        Assert.AreEqual(expected, InstallerPlanningPolicy.ParentOwnsRelatedUninstall(upgradeRelation, uninstalls));
    }

    [DataTestMethod]
    [DataRow(false, false, false, false)]
    [DataRow(false, false, true, false)]
    [DataRow(false, true, false, true)]
    [DataRow(false, true, true, false)]
    [DataRow(true, false, false, false)]
    [DataRow(true, false, true, false)]
    [DataRow(true, true, false, false)]
    [DataRow(true, true, true, false)]
    public void OnlyADirectUninstallCanRemoveOwnedInfrastructure(
        bool recovery, bool uninstalls, bool upgradeRelation, bool expected)
    {
        Assert.AreEqual(expected, InstallerPlanningPolicy.RunOwnedUninstallCleanup(recovery, uninstalls, upgradeRelation));
    }
}
