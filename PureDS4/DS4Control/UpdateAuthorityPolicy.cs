namespace DS4Windows
{
    /// <summary>
    /// The inherited hbashton release channel is not an update authority for
    /// this derivative. Read-only PureDS4 release notifications are separate
    /// from installation authority. A future updater needs an independent
    /// signed channel and rollback design.
    /// </summary>
    public static class UpdateAuthorityPolicy
    {
        public static readonly bool ProductUpdatesEnabled = false;
        public static readonly bool ProductReleaseNotesEnabled = false;
        public static readonly bool ReleaseNotificationsEnabled = true;

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
