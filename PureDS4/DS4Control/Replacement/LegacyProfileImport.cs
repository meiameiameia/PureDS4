/*
This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DS4Windows
{
    /// <summary>
    /// One game profile PureDS4 could import from a DS4Windows or DS4Windows
    /// Reworked installation: a single, self-contained XML file under its
    /// Profiles folder. PureDS4 discovers its own profiles the same way —
    /// by listing files in its Profiles folder — so a file that lands there
    /// needs nothing else to become one of PureDS4's own profiles.
    /// </summary>
    internal sealed class LegacyProfileImportCandidate
    {
        internal LegacyProfileImportCandidate(string fileName,
            string sourcePath)
        {
            FileName = fileName;
            SourcePath = sourcePath;
        }

        internal string FileName { get; }
        internal string SourcePath { get; }
    }

    /// <summary>
    /// What is available to import from a legacy configuration directory.
    /// Read-only: building this never opens a file for write and never
    /// deletes anything.
    /// </summary>
    internal sealed class LegacyProfileImportSurvey
    {
        internal LegacyProfileImportSurvey(bool profilesDirectoryPresent,
            IReadOnlyList<LegacyProfileImportCandidate> candidates)
        {
            ProfilesDirectoryPresent = profilesDirectoryPresent;
            Candidates = candidates ??
                Array.Empty<LegacyProfileImportCandidate>();
        }

        internal bool ProfilesDirectoryPresent { get; }
        internal IReadOnlyList<LegacyProfileImportCandidate> Candidates { get; }
        internal bool HasImportableProfiles => Candidates.Count > 0;
    }

    /// <summary>
    /// Step 4 of the replacement flow in AGENTS.md: optionally import old
    /// profiles read-only. This scope covers game profiles only — the
    /// individual XML files under Profiles\ — not Auto Profiles rules,
    /// Actions, or Controller Configs, which are left for a later pass.
    /// </summary>
    internal static class LegacyProfileImportScanner
    {
        internal const string ProfilesSubdirectoryName = "Profiles";

        /// <summary>
        /// Lists the game profile files under
        /// <paramref name="legacyConfigurationDirectory"/>\Profiles. Never
        /// opens a candidate file, so a corrupt or unreadable profile still
        /// appears in the survey; only the later import step can fail on
        /// one.
        /// </summary>
        internal static LegacyProfileImportSurvey Scan(
            string legacyConfigurationDirectory)
        {
            if (string.IsNullOrWhiteSpace(legacyConfigurationDirectory))
            {
                return new LegacyProfileImportSurvey(false, null);
            }

            string profilesDirectory = Path.Combine(
                legacyConfigurationDirectory, ProfilesSubdirectoryName);
            if (!Directory.Exists(profilesDirectory))
            {
                return new LegacyProfileImportSurvey(false, null);
            }

            List<LegacyProfileImportCandidate> candidates =
                Directory.GetFiles(profilesDirectory, "*.xml")
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .Select(path => new LegacyProfileImportCandidate(
                        Path.GetFileName(path), path))
                    .ToList();

            return new LegacyProfileImportSurvey(true, candidates);
        }
    }

    /// <summary>What happened to one candidate during an import.</summary>
    internal enum LegacyProfileImportOutcome
    {
        Imported,
        SkippedAlreadyExists,
        Failed,
    }

    internal sealed class LegacyProfileImportResult
    {
        internal LegacyProfileImportResult(string fileName,
            LegacyProfileImportOutcome outcome, string failureReason = null)
        {
            FileName = fileName;
            Outcome = outcome;
            FailureReason = failureReason ?? string.Empty;
        }

        internal string FileName { get; }
        internal LegacyProfileImportOutcome Outcome { get; }
        internal string FailureReason { get; }
    }

    /// <summary>
    /// Copies profiles a <see cref="LegacyProfileImportSurvey"/> found into
    /// PureDS4's own Profiles folder. Strictly read-only against the
    /// source: every candidate is opened for read and copied, never moved,
    /// never deleted, never opened for write. A destination file with the
    /// same name is never overwritten — importing again after an earlier
    /// import, or after the user has since edited a same-named PureDS4
    /// profile, changes nothing for that file and is reported as skipped.
    /// </summary>
    internal static class LegacyProfileImporter
    {
        internal static IReadOnlyList<LegacyProfileImportResult> Import(
            LegacyProfileImportSurvey survey,
            string destinationConfigurationDirectory)
        {
            ArgumentNullException.ThrowIfNull(survey);
            if (string.IsNullOrWhiteSpace(destinationConfigurationDirectory))
            {
                throw new ArgumentException(
                    "A destination configuration directory is required.",
                    nameof(destinationConfigurationDirectory));
            }

            // Defense in depth: SaveWhere already refuses to make
            // DS4Windows' own directory PureDS4's active store, so this
            // should be unreachable in practice. Never let an import write
            // into the very directory it reads from.
            LegacyConfigurationGuard.EnsureNotLegacyConfigurationPath(
                destinationConfigurationDirectory);

            List<LegacyProfileImportResult> results =
                new List<LegacyProfileImportResult>();
            if (!survey.HasImportableProfiles)
            {
                return results;
            }

            string destinationProfilesDirectory = Path.Combine(
                destinationConfigurationDirectory,
                LegacyProfileImportScanner.ProfilesSubdirectoryName);
            Directory.CreateDirectory(destinationProfilesDirectory);

            foreach (LegacyProfileImportCandidate candidate in
                survey.Candidates)
            {
                string destinationPath = Path.Combine(
                    destinationProfilesDirectory, candidate.FileName);
                if (File.Exists(destinationPath))
                {
                    results.Add(new LegacyProfileImportResult(
                        candidate.FileName,
                        LegacyProfileImportOutcome.SkippedAlreadyExists));
                    continue;
                }

                try
                {
                    File.Copy(candidate.SourcePath, destinationPath,
                        overwrite: false);
                    results.Add(new LegacyProfileImportResult(
                        candidate.FileName,
                        LegacyProfileImportOutcome.Imported));
                }
                catch (IOException ex)
                {
                    results.Add(new LegacyProfileImportResult(
                        candidate.FileName,
                        LegacyProfileImportOutcome.Failed, ex.Message));
                }
                catch (UnauthorizedAccessException ex)
                {
                    results.Add(new LegacyProfileImportResult(
                        candidate.FileName,
                        LegacyProfileImportOutcome.Failed, ex.Message));
                }
            }

            return results;
        }
    }
}
