/*
PureDS4
Copyright (C) 2026 meiameiameia

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
using DS4WinWPF.DS4Forms.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace DS4WinWPF.DS4Forms
{
    /// <summary>
    /// Shows how steadily each controller is reporting. The measuring happens
    /// on the input thread for free; this screen only reads snapshots, and only
    /// while it is on screen.
    /// </summary>
    public partial class InputDiagnosticsControl : UserControl
    {
        private readonly DispatcherTimer refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500),
        };

        private InputDiagnosticsViewModel diagnosticsVM;

        public InputDiagnosticsControl()
        {
            InitializeComponent();
            refreshTimer.Tick += RefreshTimer_Tick;
            IsVisibleChanged += InputDiagnosticsControl_IsVisibleChanged;
            Unloaded += InputDiagnosticsControl_Unloaded;
        }

        public void SetupDataContext(ControlService service)
        {
            diagnosticsVM = new InputDiagnosticsViewModel(service);
            DataContext = diagnosticsVM;
            diagnosticsVM.Refresh();
        }

        private void InputDiagnosticsControl_IsVisibleChanged(object sender,
            DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                diagnosticsVM?.Refresh();
                refreshTimer.Start();
            }
            else
            {
                refreshTimer.Stop();
            }
        }

        private void InputDiagnosticsControl_Unloaded(object sender, RoutedEventArgs e)
        {
            refreshTimer.Stop();
        }

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            diagnosticsVM?.Refresh();
        }

        private void ResetMeasurementsBtn_Click(object sender, RoutedEventArgs e)
        {
            diagnosticsVM?.ResetMeasurements();
        }

        private void CopyReportBtn_Click(object sender, RoutedEventArgs e)
        {
            if (diagnosticsVM == null)
            {
                return;
            }

            try
            {
                Clipboard.SetText(diagnosticsVM.BuildReport());
            }
            catch
            {
                // Another application can hold the clipboard open; a failed copy
                // is not worth interrupting the screen for.
            }
        }
    }
}
