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

using DS4Windows;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace DS4WinWPF.DS4Forms
{
    /// <summary>
    /// Step 4 of the replacement flow described in AGENTS.md: an explicit,
    /// owner-visible action (reached from Settings, not shown
    /// automatically) that copies game profiles from a DS4Windows or
    /// DS4Windows Reworked installation into PureDS4. Strictly read-only
    /// against the source: it only ever copies, and it never overwrites a
    /// PureDS4 profile that already exists under the same name.
    ///
    /// Scoped to game profiles only for this pass — the individual XML
    /// files under the legacy Profiles folder. Auto Profiles rules,
    /// Actions, and Controller Configs are left for a later pass.
    /// </summary>
    public partial class LegacyProfileImportWindow : Window
    {
        private readonly string legacyConfigurationDirectory;
        private LegacyProfileImportSurvey survey;

        public LegacyProfileImportWindow()
            : this(LegacyConfigurationGuard.ResolveLegacyConfigurationDirectory())
        {
        }

        internal LegacyProfileImportWindow(string legacyConfigurationDirectory)
        {
            InitializeComponent();
            this.legacyConfigurationDirectory = legacyConfigurationDirectory;

            headingText.Text = "Import profiles from DS4Windows";
            RunScan();
        }

        private void RunScan()
        {
            survey = LegacyProfileImportScanner.Scan(
                legacyConfigurationDirectory);

            summaryText.Text = BuildSummary(survey);
            candidateList.ItemsSource = survey.Candidates
                .Select(candidate => candidate.FileName).ToList();
            candidateListBorder.Visibility = survey.HasImportableProfiles
                ? Visibility.Visible : Visibility.Collapsed;
            importButton.IsEnabled = survey.HasImportableProfiles;
        }

        internal static string BuildSummary(LegacyProfileImportSurvey survey)
        {
            if (survey == null || !survey.HasImportableProfiles)
            {
                return "No DS4Windows profiles were found to import. " +
                    "DS4Windows' own files are never changed by this screen.";
            }

            string profileWord = survey.Candidates.Count == 1
                ? "profile" : "profiles";
            return $"Found {survey.Candidates.Count} {profileWord} in " +
                "DS4Windows. Importing copies them into PureDS4; it never " +
                "changes or removes DS4Windows' own files. A profile " +
                "already present in PureDS4 under the same name is left " +
                "as it is.";
        }

        internal static string BuildResultSummary(
            IReadOnlyList<LegacyProfileImportResult> results)
        {
            int imported = results.Count(r =>
                r.Outcome == LegacyProfileImportOutcome.Imported);
            int skipped = results.Count(r =>
                r.Outcome == LegacyProfileImportOutcome.SkippedAlreadyExists);
            int failed = results.Count(r =>
                r.Outcome == LegacyProfileImportOutcome.Failed);

            StringBuilder text = new StringBuilder();
            text.Append(imported == 1
                ? "Imported 1 profile."
                : $"Imported {imported} profiles.");

            if (skipped > 0)
            {
                text.Append(skipped == 1
                    ? " 1 was already present in PureDS4 and was left" +
                        " unchanged."
                    : $" {skipped} were already present in PureDS4 and" +
                        " were left unchanged.");
            }

            if (failed > 0)
            {
                text.Append(failed == 1
                    ? " 1 could not be copied."
                    : $" {failed} could not be copied.");
            }

            return text.ToString();
        }

        private void ImportBtn_Click(object sender, RoutedEventArgs e)
        {
            IReadOnlyList<LegacyProfileImportResult> results =
                LegacyProfileImporter.Import(survey, Global.appdatapath);

            candidateList.ItemsSource = results
                .Select(DescribeResult).ToList();
            resultText.Text = BuildResultSummary(results);
            resultText.Visibility = Visibility.Visible;

            // A completed pass has already copied everything the source
            // held at scan time; nothing more happens by pressing it
            // again. Reopening this window later picks up anything new.
            importButton.IsEnabled = false;
        }

        private static string DescribeResult(LegacyProfileImportResult result)
        {
            string status = result.Outcome switch
            {
                LegacyProfileImportOutcome.Imported => "Imported",
                LegacyProfileImportOutcome.SkippedAlreadyExists =>
                    "Already in PureDS4",
                LegacyProfileImportOutcome.Failed => "Could not be copied",
                _ => result.Outcome.ToString(),
            };

            return $"{result.FileName} - {status}";
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
