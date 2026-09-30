using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace PureDS4.Bootstrapper
{
    // This identifier binds consent to these exact, unmodified vendor texts.
    // Change it and the hashes when reviewing a new set of Microsoft terms.
    internal static class InstallerTerms
    {
        internal const string Version = "microsoft-20260929";
        internal const string Variable = "MicrosoftTermsAcceptedVersion";
        internal const string Argument = "--accept-microsoft-terms=" + Version;
        internal const string DotNetHash = "7F6839A61CE892B79C6549E2DC5A81FDBD240A0B260F8881216B45B7FDA8B45D";
        internal const string SdkHash = "DD07EB178E00C6BBA4148457FC00FF77CD4887EB521D504186FE59C9EC8BBE62";

        internal static bool HasExplicitConsent(string[] arguments) =>
            arguments != null && arguments.Count(a => a == Argument) == 1 &&
            !arguments.Any(a => a != Argument && a != null &&
                a.StartsWith("--accept-microsoft-terms", StringComparison.OrdinalIgnoreCase));

        internal static bool CanProceed(bool installsComponents, string acceptedVersion) =>
            !installsComponents || string.Equals(acceptedVersion, Version, StringComparison.Ordinal);

        internal static Stream OpenDocument(string name) =>
            typeof(InstallerTerms).Assembly.GetManifestResourceStream("PureDS4.Terms." + name)
            ?? throw new InvalidDataException("An offline license document is missing: " + name);

        internal static void VerifyDocuments()
        {
            Verify("DotNet-LICENSE.txt", DotNetHash);
            Verify("WindowsSdk-LICENSE.rtf", SdkHash);
            using var gpl = OpenDocument("COPYING");
            if (gpl.Length < 100) throw new InvalidDataException("The GPL document is incomplete.");
        }

        private static void Verify(string name, string expected)
        {
            using var document = OpenDocument(name);
            if (Convert.ToHexString(SHA256.HashData(document)) != expected)
                throw new InvalidDataException("An offline Microsoft license document changed: " + name);
        }
    }
}
