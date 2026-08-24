namespace DS4Windows
{
    /// <summary>
    /// Every name this product owns, both the public identity and the names it
    /// claims on the host.
    ///
    /// PureDS4 is a DS4Windows derivative and has to be installable next to
    /// DS4Windows, so anything both products could claim is defined here once
    /// rather than repeated as a literal. A collision between the two would
    /// look like data loss to the user: shared configuration directories, or a
    /// single-instance handle that makes each product believe the other is
    /// itself and refuse to start.
    /// </summary>
    public static class ProductIdentity
    {
        public const string Name = "PureDS4";
        public const string Publisher = "meiameiameia";
        public const string RepositoryUrl =
            "https://github.com/meiameiameia/pureds4";
        public const string IssuesUrl = RepositoryUrl + "/issues";
        public const string ReleasesApiUrl =
            "https://api.github.com/repos/meiameiameia/pureds4/releases";

        /// <summary>
        /// Directory name under %AppData%, %LocalAppData%, %Temp% and Program
        /// Files. Distinct from DS4Windows so neither product reads or writes
        /// the other's profiles, settings or logs.
        /// </summary>
        public const string DataFolderName = "PureDS4";

        /// <summary>
        /// Single-instance coordination handle. Deliberately a fresh
        /// identifier rather than the inherited DS4Windows one: sharing it
        /// would make each product treat the other as an existing instance of
        /// itself.
        /// </summary>
        public const string SingleInstanceEventName =
            "{83FC48C3-0DB1-4849-9C7A-F9D4C7A5F453}";

        /// <summary>Shared memory carrying the main window class name.</summary>
        public const string IpcClassNameMapName = "PureDS4_IPCClassName.dat";

        /// <summary>Shared memory carrying command-line result data.</summary>
        public const string IpcResultDataMapName = "PureDS4_IPCResultData.dat";

        /// <summary>Signals that command-line result data is ready to read.</summary>
        public const string IpcResultReadyEventName =
            "PureDS4_IPCResultData_ReadyEvent";

        /// <summary>
        /// Machine-wide lock guarding HidHide blacklist mutation.
        ///
        /// This name is intentionally NOT product-specific and must not be
        /// renamed. The blacklist is a single shared resource owned by HidHide,
        /// so PureDS4 and DS4Windows have to serialise against the same lock.
        /// Giving each product its own would let both mutate the list at once,
        /// which is the corruption this guard exists to prevent.
        /// </summary>
        public const string HidHideBlacklistMutexName =
            @"Global\DS4Windows-Reworked-HidHide-Blacklist";
    }
}
