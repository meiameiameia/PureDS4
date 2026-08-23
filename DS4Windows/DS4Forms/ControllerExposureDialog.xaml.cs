using System.Windows;

namespace DS4WinWPF.DS4Forms
{
    public partial class ControllerExposureDialog : Window
    {
        public ControllerExposureDialog()
        {
            InitializeComponent();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
