namespace PureDS4.Bootstrapper;

/// <summary>
/// Side-effect-free ownership decisions used by Burn planning. Tests compile
/// this same source; they never construct Burn or run an installer.
/// </summary>
internal static class InstallerPlanningPolicy
{
    internal static bool DeferInfrastructure(bool recoveryPass, bool installingOrRepairing,
        bool managedRelatedUpgrade) => !recoveryPass && installingOrRepairing && managedRelatedUpgrade;

    internal static bool ParentOwnsRelatedUninstall(bool upgradeRelation, bool uninstalling) =>
        upgradeRelation && uninstalling;

    internal static bool RunOwnedUninstallCleanup(bool recoveryPass, bool uninstalling,
        bool upgradeRelation) => !recoveryPass && uninstalling && !upgradeRelation;
}
