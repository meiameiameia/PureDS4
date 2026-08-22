using System.Windows;
using System.Windows.Input;

namespace DS4WinWPF.DS4Forms
{
    public enum ViiperSetupPromptDecision
    {
        NotNow,
        InstallStandard,
        InstallPortable,
    }

    public sealed class ViiperSetupPromptPresentation
    {
        public string Heading { get; init; }
        public string Summary { get; init; }
        public string RequirementsHeading { get; init; }
        public string Requirements { get; init; }
        public string PrimaryAction { get; init; }
        public string PortableAction { get; init; }
        public string ContinueAction { get; init; }
        public bool ShowPortableAction { get; init; } = true;
        public bool ShowSuppressPrompt { get; init; } = true;
    }

    public partial class ViiperSetupPrompt : Window
    {
        private readonly bool mandatoryRepairRequired;

        public ViiperSetupPromptDecision Decision { get; private set; } =
            ViiperSetupPromptDecision.NotNow;

        public bool SuppressFuturePrompts =>
            suppressPromptCheck.IsChecked == true;

        public bool ExitApplicationRequested => false;

        public ViiperSetupPrompt(string currentStatus,
            string existingViiperPath, bool citrixUsbMonitorConflict = false,
            bool verifiedUpdateRequired = false,
            bool usbipReplacementRequired = false,
            bool mandatoryRepairRequired = false)
        {
            this.mandatoryRepairRequired = mandatoryRepairRequired ||
                verifiedUpdateRequired;
            InitializeComponent();
            statusText.Text = currentStatus;

            ViiperSetupPromptPresentation presentation = CreatePresentation(
                citrixUsbMonitorConflict, verifiedUpdateRequired,
                usbipReplacementRequired, this.mandatoryRepairRequired);
            headingText.Text = presentation.Heading;
            summaryText.Text = presentation.Summary;
            requirementsHeadingText.Text = presentation.RequirementsHeading;
            requirementsText.Text = presentation.Requirements;
            installButton.Content = presentation.PrimaryAction;
            installPortableButton.Content = presentation.PortableAction;
            notNowButton.Content = presentation.ContinueAction;
            closeButton.ToolTip = presentation.ContinueAction;
            installPortableButton.Visibility = presentation.ShowPortableAction
                ? Visibility.Visible : Visibility.Collapsed;
            portableWarningPanel.Visibility = presentation.ShowPortableAction
                ? Visibility.Visible : Visibility.Collapsed;
            suppressPromptCheck.Visibility = presentation.ShowSuppressPrompt
                ? Visibility.Visible : Visibility.Collapsed;

            if (citrixUsbMonitorConflict)
            {
                return;
            }

            if (!verifiedUpdateRequired &&
                !string.IsNullOrWhiteSpace(existingViiperPath))
            {
                existingViiperPathText.Text = existingViiperPath;
                existingViiperPanel.Visibility = Visibility.Visible;
            }

        }

        public static ViiperSetupPromptPresentation CreatePresentation(
            bool citrixUsbMonitorConflict, bool verifiedUpdateRequired,
            bool usbipReplacementRequired, bool mandatoryRepairRequired)
        {
            if (citrixUsbMonitorConflict)
            {
                return new ViiperSetupPromptPresentation
                {
                    Heading = "Game output paused for system safety",
                    Summary = "A Windows USB component is conflicting with game output.",
                    RequirementsHeading = "What needs attention",
                    Requirements = "• Disable Citrix generic USB redirection only\n" +
                        "• Restart Windows, then try game output again",
                    PrimaryAction = "Resolve USB conflict",
                    PortableAction = string.Empty,
                    ContinueAction = "Continue without game output",
                    ShowPortableAction = false,
                    ShowSuppressPrompt = false,
                };
            }

            if (verifiedUpdateRequired)
            {
                return new ViiperSetupPromptPresentation
                {
                    Heading = "Game output needs repair",
                    Summary = usbipReplacementRequired
                        ? "A required Windows component must be safely replaced before game output can start."
                        : "The installed game-output components do not match this DS4Windows package.",
                    RequirementsHeading = "What setup will do",
                    Requirements = usbipReplacementRequired
                        ? "• Safely replace the unsupported Windows component\n" +
                          "• Restart Windows, then finish verifying game output"
                        : "• Install the verified game-output components\n" +
                          "• Keep unverified components from starting",
                    PrimaryAction = "Repair game output",
                    PortableAction = "Keep this folder portable",
                    ContinueAction = "Continue without game output",
                    ShowSuppressPrompt = false,
                };
            }

            if (mandatoryRepairRequired)
            {
                return new ViiperSetupPromptPresentation
                {
                    Heading = "Set up game output",
                    Summary = "You can configure controllers now. Set up game output to use them in games.",
                    RequirementsHeading = "What setup will do",
                    Requirements = "• Verify the components required for game output\n" +
                        "• Request Windows permission only when setup needs it\n" +
                        "• Keep profiles and settings in place",
                    PrimaryAction = "Set up game output",
                    PortableAction = "Keep this folder portable",
                    ContinueAction = "Continue without game output",
                    ShowSuppressPrompt = false,
                };
            }

            return new ViiperSetupPromptPresentation
            {
                Heading = "Game output setup",
                Summary = "Game output is ready. You can repair it if an issue returns.",
                RequirementsHeading = "What setup will do",
                Requirements = "• Verify the components required for game output\n" +
                    "• Keep profiles and settings in place",
                PrimaryAction = "Repair game output",
                PortableAction = "Keep this folder portable",
                ContinueAction = "Continue without game output",
            };
        }

        private void TitleBar_MouseLeftButtonDown(object sender,
            MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            Decision = ViiperSetupPromptDecision.InstallStandard;
            DialogResult = true;
        }

        private void InstallPortableButton_Click(object sender,
            RoutedEventArgs e)
        {
            Decision = ViiperSetupPromptDecision.InstallPortable;
            DialogResult = true;
        }

        private void NotNowButton_Click(object sender, RoutedEventArgs e)
        {
            Decision = ViiperSetupPromptDecision.NotNow;
            DialogResult = false;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Decision = ViiperSetupPromptDecision.NotNow;
            DialogResult = false;
        }
    }
}
