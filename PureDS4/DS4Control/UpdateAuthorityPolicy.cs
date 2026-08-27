namespace DS4Windows
{
    /// <summary>
    /// The inherited hbashton release channel is not an update authority for
    /// this derivative. A future updater must replace this policy only after
    /// it has an independent signed channel and rollback design.
    /// </summary>
    public static class UpdateAuthorityPolicy
    {
        public static readonly bool ProductUpdatesEnabled = false;
        public static readonly bool ProductReleaseNotesEnabled = false;

        public const string DisabledMessage =
            "Product updates are temporarily disabled while an independent " +
            "signed update channel is established.";

        public const string ReleaseNotesDisabledMarkdown =
            "## Release notes unavailable\n\n" +
            ProductIdentity.Name + " release notes will become available when " +
            "the independent release channel is established. No upstream " +
            "release feed is queried by this build.";
    }
}
