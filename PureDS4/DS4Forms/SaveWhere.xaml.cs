/*
DS4Windows
Copyright (C) 2023  Travis Nickles

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

using System.Windows;

namespace DS4WinWPF.DS4Forms
{
    /// <summary>
    /// Where PureDS4 keeps its profiles and settings.
    /// </summary>
    public enum FirstRunStorageLocation
    {
        /// <summary>%AppData%\PureDS4, private to the signed-in account.</summary>
        WindowsAccount,

        /// <summary>Beside PureDS4.exe, for a portable copy.</summary>
        PortableFolder,
    }

    /// <summary>
    /// The copy this dialog shows, separated from the window so the wording
    /// and the truthfulness of the claims can be tested without a UI.
    /// </summary>
    public sealed class FirstRunStoragePresentation
    {
        public string Title { get; init; }
        public string Heading { get; init; }
        public string Summary { get; init; }
        public string AccountAction { get; init; }
        public string AccountDescription { get; init; }
        public string PortableAction { get; init; }
        public string PortableDescription { get; init; }
        public bool ShowExistingDataNotice { get; init; }
        public string ExistingDataNotice { get; init; }

        /// <summary>
        /// This dialog never deletes anything. Choosing a location selects
        /// which store PureDS4 uses; it does not remove the other one.
        /// </summary>
        public bool DeletesTheOtherLocation => false;
    }

    /// <summary>
    /// Interaction logic for SaveWhere.xaml
    /// </summary>
    public partial class SaveWhere : Window
    {
        private readonly bool multisaves;

        public bool ChoiceMade { get; set; }

        public SaveWhere(bool multisavespots)
        {
            InitializeComponent();
            multisaves = multisavespots;

            FirstRunStoragePresentation presentation =
                CreatePresentation(multisavespots);
            Title = presentation.Title;
            headingText.Text = presentation.Heading;
            summaryText.Text = presentation.Summary;
            accountButton.Content = presentation.AccountAction;
            accountDescriptionText.Text = presentation.AccountDescription;
            portableButton.Content = presentation.PortableAction;
            portableDescriptionText.Text = presentation.PortableDescription;
            existingDataText.Text = presentation.ExistingDataNotice;
            existingDataText.Visibility = presentation.ShowExistingDataNotice
                ? Visibility.Visible : Visibility.Collapsed;

            if (DS4Windows.Global.AdminNeeded())
            {
                portablePanel.IsEnabled = false;
                portableDescriptionText.Text =
                    "Not available here: this folder is not writable without " +
                    "administrator rights.";
            }

            Loaded += (_, _) => accountButton.Focus();
        }

        public static FirstRunStoragePresentation CreatePresentation(
            bool multipleStores)
        {
            return new FirstRunStoragePresentation
            {
                Title = "Choose where PureDS4 stores its data",
                Heading = "Choose where PureDS4 stores its data",
                Summary = multipleStores
                    ? "Pick the location PureDS4 should use from now on. The " +
                        "other one is left exactly as it is."
                    : "PureDS4 keeps your profiles and settings in one " +
                        "folder. You can change this later by moving that " +
                        "folder yourself.",
                AccountAction = "For this Windows account (Recommended)",
                AccountDescription =
                    @"Store in %AppData%\PureDS4. Private to the account you " +
                    "are signed in with, and unaffected by where PureDS4 is " +
                    "installed.",
                PortableAction = "Portable folder",
                PortableDescription =
                    "Store beside PureDS4.exe, so settings travel with the " +
                    "application. This needs a folder PureDS4 can write to, " +
                    "which usually rules out Program Files.",
                ShowExistingDataNotice = multipleStores,
                ExistingDataNotice = multipleStores
                    ? "PureDS4 data already exists in both locations. " +
                        "Choosing one makes it the active location. Nothing " +
                        "is deleted, and the location you do not choose is " +
                        "left untouched."
                    : string.Empty,
            };
        }

        internal void SelectStorage(FirstRunStorageLocation location)
        {
            FirstRunStorage.Activate(location, freshInstall: !multisaves);
            ChoiceMade = true;
            Close();
        }

        private void PortableBtn_Click(object sender, RoutedEventArgs e)
        {
            SelectStorage(FirstRunStorageLocation.PortableFolder);
        }

        private void AccountBtn_Click(object sender, RoutedEventArgs e)
        {
            SelectStorage(FirstRunStorageLocation.WindowsAccount);
        }
    }
}
