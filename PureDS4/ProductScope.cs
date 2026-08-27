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
        /// Whether ordinary UI may present DualShock 3 as something the user
        /// can enable, configure, or expect to work.
        /// </summary>
        public const bool DualShock3Offered = false;

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
