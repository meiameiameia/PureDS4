namespace DS4Windows
{
    /// <summary>
    /// Every name this product owns, both the public identity and the names it
    /// claims on the host.
    ///
    /// PureDS4 replaces DS4Windows or an earlier DS4Windows Reworked install
    /// rather than running beside one, but it still needs its own names for
    /// everything it claims. Shared names would let a Windows Installer
    /// package upgrade over a different product, make it impossible to audit
    /// or clean up what PureDS4 actually owns, and let a half-finished
    /// transition leave two products fighting over the same configuration,
    /// logs and single-instance handles. Every owned name is defined here once
    /// rather than repeated as a literal so ownership stays reviewable.
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
        /// Files. Distinct from DS4Windows so PureDS4 never reads or writes
        /// the other product's profiles, settings or logs.
        /// </summary>
        public const string DataFolderName = "PureDS4";

        /// <summary>
        /// Satellite assembly that carries a translated resource set. Derived
        /// from the assembly name, so it has to move whenever that moves or
        /// the language-pack list silently finds nothing.
        /// </summary>
        public const string LanguageAssemblyName = Name + ".resources.dll";

        /// <summary>
        /// Per-user Startup shortcut. PureDS4 creates and removes only this
        /// file; the shortcuts of DS4Windows and DS4Windows Reworked belong to
        /// those products and to an explicit replacement flow.
        /// </summary>
        public const string StartupShortcutFileName = Name + ".lnk";

        /// <summary>Elevated logon task that starts the application.</summary>
        public const string StartupTaskName = "RunPureDS4";

        /// <summary>Elevated logon task that serves the VIIPER backend.</summary>
        public const string ViiperStartupTaskName = "RunPureDS4VIIPER";

        /// <summary>
        /// Staging root under Program Files used by the elevated backend
        /// setup host.
        /// </summary>
        public const string SetupStagingFolderName = Name + ".Setup";

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
        /// Serialises concurrent command-line clients around the result-data
        /// shared memory. Owned with the map it guards, so it carries the same
        /// product-specific prefix.
        /// </summary>
        public const string IpcResultDataSingleTaskMutexName =
            "PureDS4_IPCResultData_SingleTaskMtx";

        /// <summary>
        /// Machine-wide lock guarding HidHide blacklist mutation.
        ///
        /// This name is intentionally NOT product-specific and must not be
        /// renamed. The blacklist is a single shared resource owned by HidHide,
        /// so any product that mutates it has to serialise against the same
        /// lock. Giving PureDS4 its own would let it and a DS4Windows install
        /// that had not yet been removed mutate the list at once, which is the
        /// corruption this guard exists to prevent.
        /// </summary>
        public const string HidHideBlacklistMutexName =
            @"Global\DS4Windows-Reworked-HidHide-Blacklist";
    }
}
