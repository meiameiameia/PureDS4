namespace DS4Windows
{
    /// <summary>
    /// What this release of PureDS4 offers a user, as distinct from what the
    /// code can do.
    ///
    /// The first public release is DualShock 4 only. DualShock 3 is a planned
    /// milestone with no schedule: the implementation, its device options and
    /// its tests are all still here and still build, but the release cannot
    /// claim or offer it, because DS3 depends on DsHidMini setup, on whatever
    /// ScpToolkit or ScpTools state a machine already carries, and on genuine
    /// hardware that has not been exercised. Offering an unvalidated setup
    /// path on a machine the owner cannot reach is the failure this guards
    /// against.
    ///
    /// Flipping <see cref="DualShock3Offered"/> is deliberately not enough on
    /// its own to ship DS3: the future gate also has to restore the presentation
    /// this release hides, and complete driver, runtime, game and uninstall
    /// validation on real hardware.
    /// </summary>
    public static class ProductScope
    {
        /// <summary>
        /// True only in a DualShock 3 hardware-validation build, produced by
        /// passing <c>-p:PureDS4ExperimentalDS3=true</c>. Nothing in the tree
        /// sets that property: not the default Debug or Release
        /// configurations, and not <c>installer/build-installer.ps1</c>, so a
        /// standard or installable artifact cannot acquire it by accident.
        ///
        /// Such a build stamps <c>+ds3-experimental</c> into its
        /// InformationalVersion, which reaches the About window, the log
        /// header, and startup diagnostics through
        /// <c>Global.exeDisplayVersion</c>. It exists to exercise genuine DS3
        /// hardware and must never be published or promoted to
        /// <c>Dev\current</c>.
        /// </summary>
        public const bool ExperimentalDualShock3Build =
#if PUREDS4_EXPERIMENTAL_DS3
            true;
#else
            false;
#endif

        /// <summary>
        /// Whether ordinary UI may present DualShock 3 as something the user
        /// can enable, configure, or expect to work.
        ///
        /// This stays a compile-time constant deliberately. In a standard
        /// build the compiler folds every <c>DualShock3Offered &amp;&amp; …</c>
        /// test to <c>false</c> and drops the branch, so the DS3 runtime path
        /// is not merely unreachable at run time — it is absent from the
        /// emitted code. Making it a static readonly field would weaken that
        /// guarantee to a run-time check.
        /// </summary>
        public const bool DualShock3Offered = ExperimentalDualShock3Build;

        /// <summary>
        /// The controller families this release supports, worded for product
        /// copy rather than for diagnostics.
        /// </summary>
        public const string SupportedControllerSummary = "DualShock 4";

        /// <summary>
        /// Where a user who needs another controller family should go. PureDS4
        /// is deliberately narrow, so this is a real answer rather than a
        /// deflection.
        /// </summary>
        public const string BroaderAlternativeUrl =
            "https://github.com/hbashton/DS4Windows";
    }
}
