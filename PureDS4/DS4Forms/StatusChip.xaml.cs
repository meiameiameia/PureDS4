using System.Windows;
using System.Windows.Controls;

namespace DS4WinWPF.DS4Forms
{
    public partial class StatusChip : UserControl
    {
        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string),
                typeof(StatusChip), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty DetailProperty =
            DependencyProperty.Register(nameof(Detail), typeof(string),
                typeof(StatusChip), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty StateProperty =
            DependencyProperty.Register(nameof(State),
                typeof(StatusVisualState), typeof(StatusChip),
                new PropertyMetadata(StatusVisualState.Neutral));

        public StatusChip()
        {
            InitializeComponent();
        }

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public string Detail
        {
            get => (string)GetValue(DetailProperty);
            set => SetValue(DetailProperty, value);
        }

        public StatusVisualState State
        {
            get => (StatusVisualState)GetValue(StateProperty);
            set => SetValue(StateProperty, value);
        }
    }
}
