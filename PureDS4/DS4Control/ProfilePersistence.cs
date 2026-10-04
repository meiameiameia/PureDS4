using System;
using System.IO;
using System.Text;

namespace DS4Windows
{
    internal static class ProfilePersistence
    {
        // Complete the write beside the destination before replacing its last
        // valid contents. A rename creates the new file before deleting the old
        // one; if deletion fails, both valid copies remain available.
        internal static void Save(string path, string xml, string originalPath = null,
            Action<string, string> writeTemporary = null, bool overwrite = true)
        {
            path = Path.GetFullPath(path);
            originalPath = originalPath == null ? null : Path.GetFullPath(originalPath);
            bool rename = originalPath != null &&
                !string.Equals(path, originalPath, StringComparison.OrdinalIgnoreCase);
            if (rename && !string.Equals(Path.GetDirectoryName(path),
                    Path.GetDirectoryName(originalPath), StringComparison.OrdinalIgnoreCase))
                throw new IOException("A profile rename must stay in the same directory.");
            if ((rename || !overwrite) && File.Exists(path))
                throw new IOException("A profile with the requested name already exists.");

            string temporary = Path.Combine(Path.GetDirectoryName(path),
                $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
            try
            {
                (writeTemporary ?? WriteTemporary)(temporary, xml);
                if (!rename && overwrite && File.Exists(path))
                    File.Replace(temporary, path, null);
                else
                    File.Move(temporary, path);

                if (rename)
                {
                    try { File.Delete(originalPath); }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                    {
                        throw new IOException("The new profile was saved, but the original could not be removed. " +
                            "Both copies have been preserved; the rename was not completed.", ex);
                    }
                }
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    try { File.Delete(temporary); }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                    {
                        AppLogger.LogToGui("Could not remove an incomplete profile write: " + ex.Message, false);
                    }
                }
            }
        }

        private static void WriteTemporary(string path, string xml)
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true))
            {
                writer.Write(xml);
                writer.Flush();
            }
            stream.Flush(flushToDisk: true);
        }
    }
}
