using System;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using DS4Windows;
using DS4WinWPF.DS4Forms.ViewModels;

namespace DS4WinWPF.DS4Forms;

/// <summary>
/// Measures where a stick actually rests. The previous version stored whatever
/// the stick reported at the instant Save was pressed, so a touch or one noisy
/// report became the correction; this one watches it settle and refuses a
/// measurement taken while the stick was moving.
/// </summary>
public partial class StickCalibrationWindow : Window
{
    private readonly Stick _stick;
    private readonly int _device;
    private readonly ProfileSettingsViewModel _profileSettingsVM;
    private readonly StickCalibration.CentreMeasurement measurement =
        new StickCalibration.CentreMeasurement();

    private readonly DispatcherTimer sampleTimer = new DispatcherTimer
    {
        // Faster than the eye needs, slower than the controller reports: a
        // second of watching is about 60 readings.
        Interval = TimeSpan.FromMilliseconds(16),
    };

    public StickCalibrationWindow(Stick stick, int device, ProfileSettingsViewModel profileSettingsVm)
    {
        _stick = stick;
        _device = device;
        _profileSettingsVM = profileSettingsVm;
        InitializeComponent();

        headingText.Text = stick == Stick.Left
            ? "Measure where the left stick rests"
            : "Measure where the right stick rests";
        sampleTimer.Tick += SampleTimer_Tick;
        Loaded += (_, _) => sampleTimer.Start();
        Closed += (_, _) => sampleTimer.Stop();
    }

    private void SampleTimer_Tick(object sender, EventArgs e)
    {
        DS4State state = App.rootHub.getDS4State(_device);
        if (state == null)
        {
            statusText.Text = "The controller is no longer connected.";
            saveButton.IsEnabled = false;
            sampleTimer.Stop();
            return;
        }

        if (_stick == Stick.Left)
        {
            measurement.Add(state.LX, state.LY);
        }
        else
        {
            measurement.Add(state.RX, state.RY);
        }

        restingText.Text = string.Format(CultureInfo.CurrentCulture, "{0:N0}, {1:N0}",
            measurement.MeanX, measurement.MeanY);
        steadinessText.Text = measurement.Count == 0
            ? "—"
            : string.Format(CultureInfo.CurrentCulture, "±{0}, ±{1}",
                measurement.SpreadX, measurement.SpreadY);
        readingsText.Text = measurement.Count.ToString(CultureInfo.CurrentCulture);
        statusText.Text = measurement.Describe();

        // A stick already at centre needs no correction, and saving one would
        // only add a rounding error; keep Save for measurements that change
        // something, and let "Measure again" recover from a disturbed reading.
        saveButton.IsEnabled = measurement.NeedsCorrection;

        if (measurement.Count >= StickCalibration.CentreMeasurement.RequiredSamples * 3)
        {
            sampleTimer.Stop();
        }
    }

    private void RestartButton_OnClick(object sender, RoutedEventArgs e)
    {
        measurement.Reset();
        saveButton.IsEnabled = false;
        statusText.Text = "Measuring. Let go of the stick and keep the controller still.";
        sampleTimer.Start();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!measurement.NeedsCorrection)
        {
            return;
        }

        if (_stick == Stick.Left)
        {
            _profileSettingsVM.LeftStickDriftXAxis = measurement.DriftX;
            _profileSettingsVM.LeftStickDriftYAxis = measurement.DriftY;
        }
        else
        {
            _profileSettingsVM.RightStickDriftXAxis = measurement.DriftX;
            _profileSettingsVM.RightStickDriftYAxis = measurement.DriftY;
        }

        Close();
    }
}

public enum Stick
{
    Left,
    Right,
}
