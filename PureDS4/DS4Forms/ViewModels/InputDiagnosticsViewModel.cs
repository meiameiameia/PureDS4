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
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;

namespace DS4WinWPF.DS4Forms.ViewModels
{
    /// <summary>One connected controller's measured report health.</summary>
    public class InputDiagnosticsEntry : INotifyPropertyChanged
    {
        private string title = string.Empty;
        private string identity = string.Empty;
        private string connection = string.Empty;
        private string verdict = string.Empty;
        private StatusVisualState verdictState = StatusVisualState.Neutral;
        private string explanation = string.Empty;
        private string rate = "—";
        private string typicalInterval = "—";
        private string p95Interval = "—";
        private string p99Interval = "—";
        private string worstInterval = "—";
        private string stalls = "—";
        private string lostReports = "—";
        private string window = "—";
        private string hostNote = string.Empty;
        private Visibility hostNoteVisibility = Visibility.Collapsed;

        public int Slot { get; set; }

        /// <summary>"Controller 1: DS4 v.2", assembled once rather than in the view.</summary>
        public string Title { get => title; set => Set(ref title, value); }

        public string Identity { get => identity; set => Set(ref identity, value); }

        public string Connection { get => connection; set => Set(ref connection, value); }

        public string Verdict { get => verdict; set => Set(ref verdict, value); }

        public StatusVisualState VerdictState { get => verdictState; set => Set(ref verdictState, value); }

        public string Explanation { get => explanation; set => Set(ref explanation, value); }

        public string Rate { get => rate; set => Set(ref rate, value); }

        public string TypicalInterval { get => typicalInterval; set => Set(ref typicalInterval, value); }

        public string P95Interval { get => p95Interval; set => Set(ref p95Interval, value); }

        public string P99Interval { get => p99Interval; set => Set(ref p99Interval, value); }

        public string WorstInterval { get => worstInterval; set => Set(ref worstInterval, value); }

        public string Stalls { get => stalls; set => Set(ref stalls, value); }

        public string LostReports { get => lostReports; set => Set(ref lostReports, value); }

        public string Window { get => window; set => Set(ref window, value); }

        /// <summary>Shown only when the PC, not the controller, delivered late.</summary>
        public string HostNote { get => hostNote; set => Set(ref hostNote, value); }

        public Visibility HostNoteVisibility
        {
            get => hostNoteVisibility;
            set => Set(ref hostNoteVisibility, value);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>
    /// Reads each controller's report statistics and describes them. The view
    /// polls Refresh on a timer: the input thread only records samples, and no
    /// measurement work happens while nobody is looking at this screen.
    /// </summary>
    public class InputDiagnosticsViewModel : INotifyPropertyChanged
    {
        private readonly ControlService service;
        private Visibility emptyStateVisibility = Visibility.Visible;

        public InputDiagnosticsViewModel(ControlService service)
        {
            this.service = service;
        }

        public ObservableCollection<InputDiagnosticsEntry> Controllers { get; } =
            new ObservableCollection<InputDiagnosticsEntry>();

        public Visibility EmptyStateVisibility
        {
            get => emptyStateVisibility;
            private set
            {
                if (emptyStateVisibility == value)
                {
                    return;
                }

                emptyStateVisibility = value;
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(EmptyStateVisibility)));
            }
        }

        public void Refresh()
        {
            DS4Device[] devices = service?.DS4Controllers ?? Array.Empty<DS4Device>();
            int slot = 0;
            for (int i = 0; i < devices.Length; i++)
            {
                DS4Device device = devices[i];
                if (device == null)
                {
                    continue;
                }

                if (Controllers.Count <= slot)
                {
                    Controllers.Add(new InputDiagnosticsEntry());
                }

                Describe(Controllers[slot], device, i + 1);
                slot++;
            }

            while (Controllers.Count > slot)
            {
                Controllers.RemoveAt(Controllers.Count - 1);
            }

            EmptyStateVisibility = slot == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Start measuring again, for instance after changing adapter.</summary>
        public void ResetMeasurements()
        {
            DS4Device[] devices = service?.DS4Controllers ?? Array.Empty<DS4Device>();
            foreach (DS4Device device in devices)
            {
                device?.ReportStatistics.Reset();
            }

            Refresh();
        }

        /// <summary>A plain-text copy of what the screen shows, for sharing.</summary>
        public string BuildReport()
        {
            var text = new StringBuilder();
            text.AppendLine("PureDS4 input diagnostics");
            text.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture));
            if (Controllers.Count == 0)
            {
                text.AppendLine("No controllers connected.");
                return text.ToString();
            }

            foreach (InputDiagnosticsEntry entry in Controllers)
            {
                text.AppendLine();
                text.AppendLine($"Controller {entry.Slot}: {entry.Identity} ({entry.Connection})");
                text.AppendLine($"  {entry.Verdict}: {entry.Explanation}");
                text.AppendLine($"  rate {entry.Rate}, typical {entry.TypicalInterval}, " +
                    $"95th {entry.P95Interval}, 99th {entry.P99Interval}, worst {entry.WorstInterval}");
                text.AppendLine($"  stalls {entry.Stalls}, lost {entry.LostReports}, window {entry.Window}");
                if (entry.HostNoteVisibility == Visibility.Visible)
                {
                    text.AppendLine($"  {entry.HostNote}");
                }
            }

            return text.ToString();
        }

        private static void Describe(InputDiagnosticsEntry entry, DS4Device device, int slot)
        {
            InputReportStatisticsSnapshot snapshot = device.ReportStatistics.Snapshot();
            bool wireless = device.ConnectionType != ConnectionType.USB;
            InputHealthVerdict verdict = InputDiagnosticsPresentation.Judge(snapshot);

            entry.Slot = slot;
            entry.Identity = device.DisplayName;
            entry.Title = string.Format(CultureInfo.CurrentCulture, "Controller {0}: {1}",
                slot, device.DisplayName);
            entry.Connection = wireless ? "Bluetooth" : "USB";
            entry.Verdict = InputDiagnosticsPresentation.VerdictLabel(verdict);
            entry.VerdictState = verdict switch
            {
                InputHealthVerdict.Steady => StatusVisualState.Success,
                InputHealthVerdict.Occasional => StatusVisualState.Warning,
                InputHealthVerdict.Frequent => StatusVisualState.Error,
                _ => StatusVisualState.Neutral,
            };
            entry.Explanation = InputDiagnosticsPresentation.Explain(snapshot, wireless);
            entry.Rate = InputDiagnosticsPresentation.FormatRate(snapshot.RateHz);
            entry.TypicalInterval = InputDiagnosticsPresentation.FormatInterval(snapshot.TypicalIntervalMs);
            entry.P95Interval = InputDiagnosticsPresentation.FormatInterval(snapshot.P95IntervalMs);
            entry.P99Interval = InputDiagnosticsPresentation.FormatInterval(snapshot.P99IntervalMs);
            entry.WorstInterval = InputDiagnosticsPresentation.FormatInterval(snapshot.WorstIntervalMs);
            entry.Stalls = snapshot.HasData
                ? snapshot.StallCount.ToString(CultureInfo.CurrentCulture) : "—";
            entry.LostReports = InputDiagnosticsPresentation.FormatLoss(snapshot);
            entry.Window = InputDiagnosticsPresentation.FormatWindow(snapshot);

            bool hostLate = InputDiagnosticsPresentation.HostDeliveryIsWorse(snapshot);
            entry.HostNoteVisibility = hostLate ? Visibility.Visible : Visibility.Collapsed;
            entry.HostNote = hostLate
                ? "The controller kept cadence, but Windows delivered reports late: worst " +
                    InputDiagnosticsPresentation.FormatInterval(snapshot.HostWorstIntervalMs) +
                    ". Background load or a busy USB port usually causes this."
                : string.Empty;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
