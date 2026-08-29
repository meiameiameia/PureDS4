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
using System.Windows;

namespace DS4WinWPF.DS4Forms
{
    /// <summary>
    /// The copy this dialog shows, separated from the window so the wording
    /// can be checked without constructing a UI.
    /// </summary>
    internal sealed class LegacyApplicationClosePresentation
    {
        internal string Heading { get; init; }
        internal string Explanation { get; init; }
    }

    /// <summary>
    /// Step 2 of the replacement flow described in AGENTS.md: PureDS4
    /// replaces DS4Windows and DS4Windows Reworked rather than running
    /// beside either one, so if one of them is still running when PureDS4
    /// starts, both would compete for the same controller and for HidHide
    /// and VIIPER ownership. This window is the explicit, owner-visible
    /// step that requires the old application closed instead of PureDS4
    /// silently working around it, or closing it automatically.
    ///
    /// This window is intentionally passive: it never re-scans the machine
    /// itself. "Check Again" and "Exit PureDS4" both just close the window
    /// and report which one was pressed; the caller (<see
    /// cref="App"/>) owns re-scanning and decides whether to show this
    /// window again.
    /// </summary>
    public partial class LegacyApplicationRunningWindow : Window
    {
        /// <summary>
        /// True once the user has asked to exit PureDS4 instead of closing
        /// the other application. False (the default) means "Check Again"
        /// was pressed, or the window is still open.
        /// </summary>
        internal bool ExitRequested { get; private set; }

        internal LegacyApplicationRunningWindow(string detectedProductName,
            bool previousCheckStillFoundItRunning = false)
        {
            InitializeComponent();

            LegacyApplicationClosePresentation presentation =
                CreatePresentation(detectedProductName);
            headingText.Text = presentation.Heading;
            explanationText.Text = presentation.Explanation;

            if (previousCheckStillFoundItRunning)
            {
                statusText.Text = "PureDS4 checked again and " +
                    detectedProductName + " is still running.";
                statusText.Visibility = Visibility.Visible;
            }

            Loaded += (_, _) => checkAgainButton.Focus();
        }

        internal static LegacyApplicationClosePresentation CreatePresentation(
            string detectedProductName)
        {
            if (string.IsNullOrWhiteSpace(detectedProductName))
            {
                detectedProductName = "DS4Windows";
            }

            return new LegacyApplicationClosePresentation
            {
                Heading = $"{detectedProductName} is currently running",
                Explanation = "PureDS4 replaces " + detectedProductName +
                    " rather than running alongside it. With both open at " +
                    "once they can compete for the same controller, which " +
                    "leads to unpredictable input. Close " +
                    detectedProductName + " completely, including its " +
                    "notification area icon, then check again. PureDS4 " +
                    "will not close it for you.",
            };
        }

        private void CheckAgainBtn_Click(object sender, RoutedEventArgs e)
        {
            ExitRequested = false;
            Close();
        }

        private void ExitBtn_Click(object sender, RoutedEventArgs e)
        {
            ExitRequested = true;
            Close();
        }
    }
}
