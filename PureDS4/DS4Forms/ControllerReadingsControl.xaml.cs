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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using NonFormTimer = System.Timers.Timer;
using DS4Windows;

namespace DS4WinWPF.DS4Forms
{
    /// <summary>
    /// Interaction logic for ControllerReadingsControl.xaml
    /// </summary>
    public partial class ControllerReadingsControl : UserControl
    {
        private enum LatencyWarnMode : uint
        {
            None,
            Caution,
            Warn,
        }

        private int deviceNum;
        private int profileDeviceNum;
        private event EventHandler DeviceNumChanged;
        private NonFormTimer readingTimer;
        private bool useTimer;
        private double lsDeadX;
        private double lsDeadY;
        private double rsDeadX;
        private double rsDeadY;

        private double sixAxisXDead;
        private double sixAxisZDead;
        private double l2Dead;
        private double r2Dead;

        private sbyte lsDriftX;
        private sbyte lsDriftY;
        private sbyte rsDriftX;
        private sbyte rsDriftY;

        public double LsDeadX
        {
            get => lsDeadX;
            set
            {
                lsDeadX = value;
                LsDeadXChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler LsDeadXChanged;

        public double LsDeadY
        {
            get => lsDeadY;
            set
            {
                lsDeadY = value;
                LsDeadYChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler LsDeadYChanged;

        public double RsDeadX
        {
            get => rsDeadX;
            set
            {
                rsDeadX = value;
                RsDeadXChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler RsDeadXChanged;

        public double RsDeadY
        {
            get => rsDeadY;
            set
            {
                rsDeadY = value;
                RsDeadYChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler RsDeadYChanged;

        public double SixAxisXDead
        {
            get => sixAxisXDead;
            set
            {
                sixAxisXDead = value;
                SixAxisDeadXChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler SixAxisDeadXChanged;

        public double SixAxisZDead
        {
            get => sixAxisZDead;
            set
            {
                sixAxisZDead = value;
                SixAxisDeadZChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler SixAxisDeadZChanged;

        public double L2Dead
        {
            get => l2Dead;
            set
            {
                l2Dead = value;
                L2DeadChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler L2DeadChanged;

        public double R2Dead
        {
            get => r2Dead;
            set
            {
                r2Dead = value;
                R2DeadChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler R2DeadChanged;


        public sbyte LsDriftX
        {
            get => lsDriftX;
            set
            {
                lsDriftX = value;
                LsDriftXChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler LsDriftXChanged;

        public sbyte LsDriftY
        {
            get => lsDriftY;
            set
            {
                lsDriftY = value;
                LsDriftYChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler LsDriftYChanged;

        public sbyte RsDriftX
        {
            get => rsDriftX;
            set
            {
                rsDriftX = value;
                RsDriftXChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler RsDriftXChanged;

        public sbyte RsDriftY
        {
            get => rsDriftY;
            set
            {
                rsDriftY = value;
                RsDriftYChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler RsDriftYChanged;


        private LatencyWarnMode warnMode;
        private LatencyWarnMode prevWarnMode;
        private DS4State baseState = new DS4State();
        private DS4State interState = new DS4State();
        private DS4StateExposed exposeState;
        private const int CANVAS_WIDTH = 130;
        private const int CANVAS_MIDPOINT = CANVAS_WIDTH / 2;
        // Half sizes of the input ring and output dot drawn in the round fields.
        private const double INPUT_DOT_RADIUS = 5.0;
        private const double OUTPUT_DOT_RADIUS = 3.0;
        private volatile bool showingReadings;

        public ControllerReadingsControl()
        {
            InitializeComponent();
            Color textColor = ThemeColor("TextPrimaryBrush",
                SystemColors.WindowTextColor);
            inpuDelayForeBrush.Color = textColor;
            l2ValLbBrush.Color = textColor;
            r2ValLbBrush.Color = textColor;
            inputContNum.Content = ControllerTitle();
            exposeState = new DS4StateExposed(baseState);

            readingTimer = new NonFormTimer();
            readingTimer.Interval = 1000 / 60.0;

            LsDeadXChanged += ChangeLsDeadControls;
            LsDeadYChanged += ChangeLsDeadControls;
            LsDeadXChanged += ChangeLsDriftControls;
            LsDeadYChanged += ChangeLsDriftControls;

            RsDeadXChanged += ChangeRsDeadControls;
            RsDeadYChanged += ChangeRsDeadControls;
            RsDeadXChanged += ChangeRsDriftControls;
            RsDeadYChanged += ChangeRsDriftControls;

            LsDriftXChanged += ChangeLsDriftControls;
            LsDriftYChanged += ChangeLsDriftControls;
            RsDriftXChanged += ChangeRsDriftControls;
            RsDriftYChanged += ChangeRsDriftControls;

            SixAxisDeadXChanged += ChangeSixAxisDeadControls;
            SixAxisDeadZChanged += ChangeSixAxisDeadControls;

            L2DeadChanged += (_, _) => ShowTriggerDeadZone(l2DeadColumn, l2LiveColumn, l2Dead);
            R2DeadChanged += (_, _) => ShowTriggerDeadZone(r2DeadColumn, r2LiveColumn, r2Dead);

            DeviceNumChanged += ControllerReadingsControl_DeviceNumChanged;
        }

        private string ControllerTitle() => $"Controller {deviceNum + 1}";

        private static void ShowTriggerDeadZone(ColumnDefinition dead, ColumnDefinition live, double fraction)
        {
            fraction = Math.Clamp(fraction, 0.0, 1.0);
            dead.Width = new GridLength(fraction, GridUnitType.Star);
            live.Width = new GridLength(1.0 - fraction, GridUnitType.Star);
        }

        private void ControllerReadingsControl_DeviceNumChanged(object sender, EventArgs e)
        {
            inputContNum.Content = ControllerTitle();
        }

        private void ChangeSixAxisDeadControls(object sender, EventArgs e)
        {
            sixAxisDeadEllipse.Width = sixAxisXDead * CANVAS_WIDTH;
            sixAxisDeadEllipse.Height = sixAxisZDead * CANVAS_WIDTH;
            Canvas.SetLeft(sixAxisDeadEllipse, CANVAS_MIDPOINT - (sixAxisXDead * CANVAS_WIDTH / 2.0));
            Canvas.SetTop(sixAxisDeadEllipse, CANVAS_MIDPOINT - (sixAxisZDead * CANVAS_WIDTH / 2.0));
        }

        private void ChangeRsDriftControls(object sender, EventArgs e)
        {
            rsDriftEllipse.Width = rsDeadX * CANVAS_WIDTH;
            rsDriftEllipse.Height = rsDeadY * CANVAS_WIDTH;
            Canvas.SetLeft(rsDriftEllipse, (1 + (RsDriftX / 127.0) - rsDeadX) * CANVAS_MIDPOINT);
            Canvas.SetTop(rsDriftEllipse, (1 + (RsDriftY / 127.0) - rsDeadY) * CANVAS_MIDPOINT);
            // Only worth drawing when it is off centre; otherwise it repeats the dead zone ring.
            rsDriftEllipse.Visibility = RsDriftX == 0 && RsDriftY == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ChangeLsDriftControls(object sender, EventArgs e)
        {
            lsDriftEllipse.Width = lsDeadX * CANVAS_WIDTH;
            lsDriftEllipse.Height = lsDeadY * CANVAS_WIDTH;
            Canvas.SetLeft(lsDriftEllipse, (1 + (LsDriftX / 127.0) - lsDeadX) * CANVAS_MIDPOINT);
            Canvas.SetTop(lsDriftEllipse, (1 + (LsDriftY / 127.0) - lsDeadY) * CANVAS_MIDPOINT);
            // Only worth drawing when it is off centre; otherwise it repeats the dead zone ring.
            lsDriftEllipse.Visibility = LsDriftX == 0 && LsDriftY == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ChangeRsDeadControls(object sender, EventArgs e)
        {
            rsDeadEllipse.Width = rsDeadX * CANVAS_WIDTH;
            rsDeadEllipse.Height = rsDeadY * CANVAS_WIDTH;
            Canvas.SetLeft(rsDeadEllipse, CANVAS_MIDPOINT - (rsDeadX * CANVAS_WIDTH / 2.0));
            Canvas.SetTop(rsDeadEllipse, CANVAS_MIDPOINT - (rsDeadY * CANVAS_WIDTH / 2.0));
        }

        private void ChangeLsDeadControls(object sender, EventArgs e)
        {
            lsDeadEllipse.Width = lsDeadX * CANVAS_WIDTH;
            lsDeadEllipse.Height = lsDeadY * CANVAS_WIDTH;
            Canvas.SetLeft(lsDeadEllipse, CANVAS_MIDPOINT - (lsDeadX * CANVAS_WIDTH / 2.0));
            Canvas.SetTop(lsDeadEllipse, CANVAS_MIDPOINT - (lsDeadY * CANVAS_WIDTH / 2.0));
        }

        public void UseDevice(int index, int profileDevIdx)
        {
            deviceNum = index;
            profileDeviceNum = profileDevIdx;
            DeviceNumChanged?.Invoke(this, EventArgs.Empty);
        }

        public void EnableControl(bool state)
        {
            if (state)
            {
                IsEnabled = true;
                useTimer = true;
                readingTimer.Elapsed += ControllerReadingTimer_Elapsed;
                readingTimer.Start();
            }
            else
            {
                IsEnabled = false;
                useTimer = false;
                readingTimer.Elapsed -= ControllerReadingTimer_Elapsed;
                readingTimer.Stop();
            }
        }

        private void ControllerReadingTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            readingTimer.Stop();

            DS4Device ds = Program.rootHub.DS4Controllers[deviceNum];
            if (ds != null)
            {
                // Don't bother waiting for UI thread to grab references
                //DS4StateExposed tmpexposeState = Program.rootHub.ExposedState[deviceNum];
                DS4State tmpbaseState = Program.rootHub.getDS4State(deviceNum);
                DS4State tmpinterState = Program.rootHub.getDS4StateTemp(deviceNum);
                long cntCalibrating = ds.SixAxis.CntCalibrating;

                // Wait for controller to be in a wait period
                ds.ReadWaitEv.Wait();
                ds.ReadWaitEv.Reset();

                // Make copy of current state values for UI thread
                tmpbaseState.CopyTo(baseState);
                tmpinterState.CopyTo(interState);

                if (deviceNum != profileDeviceNum)
                    Mapping.SetCurveAndDeadzone(profileDeviceNum, baseState, interState);

                // Done with copying. Allow input thread to resume
                ds.ReadWaitEv.Set();

                double latency = ds.Latency;
                int warnInterval = ds.getWarnInterval();
                ConnectionType connection = ds.getConnectionType();
                Dispatcher.Invoke(() => ShowReadings(latency, warnInterval, cntCalibrating, connection));
            }
            else if (showingReadings)
            {
                Dispatcher.Invoke(ShowNoController);
            }

            if (useTimer)
            {
                readingTimer.Start();
            }
        }

        /// <summary>
        /// Draws the latest copy of the controller state (baseState, interState).
        /// Runs on the UI thread; the preview tests call it with a fake state.
        /// </summary>
        internal void ShowReadings(double latency, int warnInterval, long cntCalibrating, ConnectionType connection)
        {
            if (!showingReadings)
            {
                showingReadings = true;
                readingsEmptyState.Visibility = Visibility.Collapsed;
                readingsContent.Visibility = Visibility.Visible;
            }
            readingsTransportLb.Content = connection switch
            {
                ConnectionType.USB => "USB",
                ConnectionType.BT => "Bluetooth",
                _ => "Wireless adapter",
            };

            int x = baseState.LX;
            int y = baseState.LY;

            Canvas.SetLeft(lsValRec, x / 255.0 * CANVAS_WIDTH - INPUT_DOT_RADIUS);
            Canvas.SetTop(lsValRec, y / 255.0 * CANVAS_WIDTH - INPUT_DOT_RADIUS);
            //bool mappedLS = interState.LX != x || interState.LY != y;
            //if (mappedLS)
            //{
            Canvas.SetLeft(lsMapValRec, interState.LX / 255.0 * CANVAS_WIDTH - OUTPUT_DOT_RADIUS);
            Canvas.SetTop(lsMapValRec, interState.LY / 255.0 * CANVAS_WIDTH - OUTPUT_DOT_RADIUS);
            //}

            x = baseState.RX;
            y = baseState.RY;
            Canvas.SetLeft(rsValRec, x / 255.0 * CANVAS_WIDTH - INPUT_DOT_RADIUS);
            Canvas.SetTop(rsValRec, y / 255.0 * CANVAS_WIDTH - INPUT_DOT_RADIUS);
            Canvas.SetLeft(rsMapValRec, interState.RX / 255.0 * CANVAS_WIDTH - OUTPUT_DOT_RADIUS);
            Canvas.SetTop(rsMapValRec, interState.RY / 255.0 * CANVAS_WIDTH - OUTPUT_DOT_RADIUS);

            x = exposeState.getAccelX() + 127;
            y = exposeState.getAccelZ() + 127;
            Canvas.SetLeft(sixAxisValRec, x / 255.0 * CANVAS_WIDTH - INPUT_DOT_RADIUS);
            Canvas.SetTop(sixAxisValRec, y / 255.0 * CANVAS_WIDTH - INPUT_DOT_RADIUS);
            Canvas.SetLeft(sixAxisMapValRec, Math.Min(Math.Max(interState.Motion.outputAccelX + 127.0, 0), 255.0) / 255.0 * CANVAS_WIDTH - OUTPUT_DOT_RADIUS);
            Canvas.SetTop(sixAxisMapValRec, Math.Min(Math.Max(interState.Motion.outputAccelZ + 127.0, 0), 255.0) / 255.0 * CANVAS_WIDTH - OUTPUT_DOT_RADIUS);

            l2Slider.Value = baseState.L2;
            l2ValLbTrans.X = interState.L2 / 255.0 * Math.Max(0.0, l2Slider.ActualWidth - l2OutputMarker.Width);

            r2Slider.Value = baseState.R2;
            r2ValLbTrans.X = interState.R2 / 255.0 * Math.Max(0.0, r2Slider.ActualWidth - r2OutputMarker.Width);

            gyroYawSlider.Value = baseState.Motion.gyroYawFull;
            gyroPitchSlider.Value = baseState.Motion.gyroPitchFull;
            gyroRollSlider.Value = baseState.Motion.gyroRollFull;

            accelXSlider.Value = exposeState.getAccelX();
            accelYSlider.Value = exposeState.getAccelY();
            accelZSlider.Value = exposeState.getAccelZ();

            gyroYawValueText.Text = baseState.Motion.gyroYawFull.ToString();
            gyroPitchValueText.Text = baseState.Motion.gyroPitchFull.ToString();
            gyroRollValueText.Text = baseState.Motion.gyroRollFull.ToString();
            accelXValueText.Text = exposeState.getAccelX().ToString();
            accelYValueText.Text = exposeState.getAccelY().ToString();
            accelZValueText.Text = exposeState.getAccelZ().ToString();

            touchXValLb.Content = baseState.TrackPadTouch0.X;
            touchYValLb.Content = baseState.TrackPadTouch0.Y;

            inputDelayLb.Content = string.Format(Properties.Resources.InputDelay,
                latency);

            if (latency > warnInterval)
            {
                warnMode = LatencyWarnMode.Warn;
                inputDelayLb.Content = $"Warning — {inputDelayLb.Content}";
                inpuDelayBackBrush.Color = Colors.Transparent;
                inpuDelayForeBrush.Color = ThemeColor(
                    "StateErrorBrush", Colors.Red);
            }
            else if (latency > (warnInterval * 0.5))
            {
                warnMode = LatencyWarnMode.Caution;
                inputDelayLb.Content = $"Caution — {inputDelayLb.Content}";
                inpuDelayBackBrush.Color = Colors.Transparent;
                inpuDelayForeBrush.Color = ThemeColor(
                    "StateWarningBrush", Colors.DarkGoldenrod);
            }
            else
            {
                warnMode = LatencyWarnMode.None;
                inpuDelayBackBrush.Color = Colors.Transparent;
                inpuDelayForeBrush.Color = ThemeColor(
                    "TextPrimaryBrush", SystemColors.WindowTextColor);
            }

            prevWarnMode = warnMode;

            batteryLvlLb.Content = $"{Translations.Strings.Battery}: {baseState.Battery}%";
            gyroCalEllipse.Visibility = cntCalibrating > 0 && ((cntCalibrating / 250) % 2 == 1) ? Visibility.Visible : Visibility.Hidden;
            UpdateCoordLabels(baseState, interState, exposeState);
        }

        /// <summary>One calm empty state instead of N/A, 0% and blank values.</summary>
        internal void ShowNoController()
        {
            showingReadings = false;
            readingsContent.Visibility = Visibility.Collapsed;
            readingsEmptyState.Visibility = Visibility.Visible;
        }

        internal void SetPreviewState(DS4State input, DS4State output)
        {
            input.CopyTo(baseState);
            output.CopyTo(interState);
        }

        private Color ThemeColor(string resourceKey, Color fallback)
        {
            return TryFindResource(resourceKey) is SolidColorBrush brush
                ? brush.Color
                : fallback;
        }

        private void UpdateCoordLabels(DS4State inState, DS4State mapState,
            DS4StateExposed exposeState)
        {
            lxInValLb.Content = inState.LX;
            lxOutValLb.Content = mapState.LX;
            lyInValLb.Content = inState.LY;
            lyOutValLb.Content = mapState.LY;

            rxInValLb.Content = inState.RX;
            rxOutValLb.Content = mapState.RX;
            ryInValLb.Content = inState.RY;
            ryOutValLb.Content = mapState.RY;

            sixAxisXInValLb.Content = exposeState.AccelX;
            sixAxisXOutValLb.Content = mapState.Motion.outputAccelX;
            sixAxisZInValLb.Content = exposeState.AccelZ;
            sixAxisZOutValLb.Content = mapState.Motion.outputAccelZ;

            l2InValLb.Content = inState.L2;
            l2OutValLb.Content = mapState.L2;
            r2InValLb.Content = inState.R2;
            r2OutValLb.Content = mapState.R2;
        }
    }
}
