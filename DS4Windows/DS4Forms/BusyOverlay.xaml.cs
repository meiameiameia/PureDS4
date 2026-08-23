using System.Windows;
using System.Windows.Controls;

namespace DS4WinWPF.DS4Forms
{
    public partial class BusyOverlay : UserControl
    {
        public static readonly DependencyProperty IsBusyProperty =
            DependencyProperty.Register(nameof(IsBusy), typeof(bool),
                typeof(BusyOverlay), new PropertyMetadata(false));
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string),
                typeof(BusyOverlay), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register(nameof(Message), typeof(string),
                typeof(BusyOverlay), new PropertyMetadata(string.Empty));

        public BusyOverlay()
        {
            InitializeComponent();
        }

        public bool IsBusy { get => (bool)GetValue(IsBusyProperty); set => SetValue(IsBusyProperty, value); }
        public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
        public string Message { get => (string)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
    }
}
