using System;
using System.Windows;
using System.Windows.Controls;

namespace DS4WinWPF.DS4Forms
{
    public partial class StatusBanner : UserControl
    {
        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register(nameof(IsOpen), typeof(bool),
                typeof(StatusBanner), new PropertyMetadata(false));
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string),
                typeof(StatusBanner), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register(nameof(Message), typeof(string),
                typeof(StatusBanner), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty ActionLabelProperty =
            DependencyProperty.Register(nameof(ActionLabel), typeof(string),
                typeof(StatusBanner), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty ShowActionProperty =
            DependencyProperty.Register(nameof(ShowAction), typeof(bool),
                typeof(StatusBanner), new PropertyMetadata(false));
        public static readonly DependencyProperty StateProperty =
            DependencyProperty.Register(nameof(State),
                typeof(StatusVisualState), typeof(StatusBanner),
                new PropertyMetadata(StatusVisualState.Neutral));

        public StatusBanner()
        {
            InitializeComponent();
        }

        public event EventHandler ActionRequested;

        public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
        public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
        public string Message { get => (string)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
        public string ActionLabel { get => (string)GetValue(ActionLabelProperty); set => SetValue(ActionLabelProperty, value); }
        public bool ShowAction { get => (bool)GetValue(ShowActionProperty); set => SetValue(ShowActionProperty, value); }
        public StatusVisualState State { get => (StatusVisualState)GetValue(StateProperty); set => SetValue(StateProperty, value); }

        private void ActionButton_Click(object sender, RoutedEventArgs e) =>
            ActionRequested?.Invoke(this, EventArgs.Empty);

        private void DismissButton_Click(object sender, RoutedEventArgs e) =>
            IsOpen = false;
    }
}
