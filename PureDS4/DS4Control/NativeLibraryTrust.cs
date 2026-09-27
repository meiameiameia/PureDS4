using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace DS4Windows
{
    internal static class NativeLibraryTrust
    {
        internal const string RnnoiseSha256 =
            "12E19BF7A18D13E092A5FBE5A7C5B2081F5E7B56F6D77AEAB5837335F44CEEDF";

        private static int applicationResolverConfigured;

        internal static void EnsureApplicationResolver()
        {
            if (Interlocked.CompareExchange(
                    ref applicationResolverConfigured, 1, 0) != 0)
            {
                return;
            }

            try
            {
                NativeLibrary.SetDllImportResolver(
                    typeof(NativeLibraryTrust).Assembly,
                    ResolveApplicationLibrary);
            }
            catch
            {
                Volatile.Write(ref applicationResolverConfigured, 0);
                throw;
            }
        }

        internal static string GetApplicationOwnedPath(string fileName,
            string baseDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(fileName) ||
                !string.Equals(Path.GetFileName(fileName), fileName,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "An application-owned native library must use a file name only.",
                    nameof(fileName));
            }

            string root = string.IsNullOrWhiteSpace(baseDirectory)
                ? AppContext.BaseDirectory
                : baseDirectory;
            return Path.GetFullPath(Path.Combine(root, fileName));
        }

        internal static bool HasExpectedSha256(string path,
            string expectedSha256)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                string.IsNullOrWhiteSpace(expectedSha256) ||
                expectedSha256.Length != 64 || !File.Exists(path))
            {
                return false;
            }

            using FileStream stream = new(path, FileMode.Open,
                FileAccess.Read, FileShare.Read);
            string actual = Convert.ToHexString(SHA256.HashData(stream));
            return string.Equals(actual, expectedSha256,
                StringComparison.OrdinalIgnoreCase);
        }

        private static IntPtr ResolveApplicationLibrary(string libraryName,
            Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (!string.Equals(libraryName, "rnnoise",
                    StringComparison.OrdinalIgnoreCase))
            {
                return IntPtr.Zero;
            }

            if (!Environment.Is64BitProcess)
            {
                throw new DllNotFoundException(
                    "The bundled RNNoise runtime is available only in the x64 build.");
            }

            return LoadVerifiedApplicationLibrary("rnnoise.dll",
                RnnoiseSha256);
        }

        private static IntPtr LoadVerifiedApplicationLibrary(string fileName,
            string expectedSha256)
        {
            string path = GetApplicationOwnedPath(fileName);
            if (!HasExpectedSha256(path, expectedSha256))
            {
                throw new DllNotFoundException(
                    $"The application-owned {fileName} is missing or failed integrity verification.");
            }

            return NativeLibrary.Load(path);
        }
    }
}
