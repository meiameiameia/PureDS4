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

using DS4Windows.DS4Control;
using DS4WinWPF.DS4Control;
using Microsoft.Win32;
using NLog;
using Sensorit.Base;
using SharpOSC;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using DS4WinWPF.DS4Forms;
using static DS4Windows.Global;

namespace DS4Windows
{
    public partial class ControlService
    {
        private readonly DualShock4AudioPassthrough dualShock4AudioPassthrough = new DualShock4AudioPassthrough();
        private readonly ViiperOutDevice[] playStationFeatureOutputDevices =
            new ViiperOutDevice[MAX_DS4_CONTROLLER_COUNT];
        private readonly object playStationFeatureOutputLock = new object();
        private readonly GameBarIntegration gameBarIntegration = new GameBarIntegration();
        private readonly object hidHideSessionLock = new object();
        private readonly HashSet<string> hidHideSessionManagedInstanceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> hidHidePersistentManagedInstanceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> hidHideBaselineBlacklist;
        private HidHideOwnershipJournal hidHideOwnershipJournal;
        private bool hidHideExternalRecoveryAttempted;
        private bool hidHideTransientRunStarted;
        private bool hidHideRecoveryReported;
        private readonly object steamInputReclaimLock = new object();
        private readonly Dictionary<string, DateTime> steamInputReclaimAttempts =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private bool? hidHideActiveStateBeforeManagedSession;
        // Might be useful for ScpVBus build
        public const int EXPANDED_CONTROLLER_COUNT = 8;
        public const int MAX_DS4_CONTROLLER_COUNT = Global.MAX_DS4_CONTROLLER_COUNT;
#if FORCE_4_INPUT
        public static int CURRENT_DS4_CONTROLLER_LIMIT = Global.OLD_XINPUT_CONTROLLER_COUNT;
#else
        public static int CURRENT_DS4_CONTROLLER_LIMIT = Global.IsWin8OrGreater() ? MAX_DS4_CONTROLLER_COUNT : Global.OLD_XINPUT_CONTROLLER_COUNT;
#endif
        public static bool USING_MAX_CONTROLLERS = CURRENT_DS4_CONTROLLER_LIMIT == EXPANDED_CONTROLLER_COUNT;
        public DS4Device[] DS4Controllers = new DS4Device[MAX_DS4_CONTROLLER_COUNT];
        public int activeControllers = 0;
        public Mouse[] touchPad = new Mouse[MAX_DS4_CONTROLLER_COUNT];
        public bool running = false;
        public bool loopControllers = true;
        public bool inServiceTask = false;
        private DS4State[] MappedState = new DS4State[MAX_DS4_CONTROLLER_COUNT];
        private DS4State[] CurrentState = new DS4State[MAX_DS4_CONTROLLER_COUNT];
        private DS4State[] PreviousState = new DS4State[MAX_DS4_CONTROLLER_COUNT];
        private DS4State[] TempState = new DS4State[MAX_DS4_CONTROLLER_COUNT];
        public DS4StateExposed[] ExposedState = new DS4StateExposed[MAX_DS4_CONTROLLER_COUNT];
        public ControllerSlotManager slotManager = new ControllerSlotManager();
        public bool recordingMacro = false;
        public event EventHandler<DebugEventArgs> Debug = null;
        bool[] buttonsdown = new bool[MAX_DS4_CONTROLLER_COUNT] { false, false, false, false, false, false, false, false };
        bool[] held = new bool[MAX_DS4_CONTROLLER_COUNT];
        int[] oldmouse = new int[MAX_DS4_CONTROLLER_COUNT] { -1, -1, -1, -1, -1, -1, -1, -1 };
        private int[] startupReportDiagCounts = new int[MAX_DS4_CONTROLLER_COUNT];
        private System.Threading.Timer gameBarStateTimer;
        private int gameBarStateUpdateGate = 0;
        public OutputDevice[] outputDevices = new OutputDevice[MAX_DS4_CONTROLLER_COUNT] { null, null, null, null, null, null, null, null };
        private readonly VirtualOutputBlockReason[] virtualOutputBlockReasons =
            new VirtualOutputBlockReason[MAX_DS4_CONTROLLER_COUNT];
        private OneEuroFilter3D[] udpEuroPairAccel = new OneEuroFilter3D[UdpServer.NUMBER_SLOTS]
        {
            new OneEuroFilter3D(), new OneEuroFilter3D(),
            new OneEuroFilter3D(), new OneEuroFilter3D(),
        };
        private OneEuroFilter3D[] udpEuroPairGyro = new OneEuroFilter3D[UdpServer.NUMBER_SLOTS]
        {
            new OneEuroFilter3D(), new OneEuroFilter3D(),
            new OneEuroFilter3D(), new OneEuroFilter3D(),
        };
        Thread eventDispatchThread;
        Dispatcher eventDispatcher;
        public bool suspending;

        private UdpServer _udpServer;
        private OutputSlotManager outputslotMan;

        private HashSet<string> hidDeviceHidingAffectedDevs = new HashSet<string>();
        private HashSet<string> hidDeviceHidingExemptedDevs = new HashSet<string>();
        private bool hidDeviceHidingForced = false;
        private bool hidDeviceHidingEnabled = false;
        private readonly object outputKbmHandlerLock = new object();
        private readonly object serviceLifecycleLock = new object();
        private readonly ControllerExposureTransitionCoordinator[]
            controllerExposureTransitions =
                new ControllerExposureTransitionCoordinator[
                    MAX_DS4_CONTROLLER_COUNT];

        private ControlServiceDeviceOptions deviceOptions;
        public ControlServiceDeviceOptions DeviceOptions { get => deviceOptions; }

        private DS4WinWPF.ArgumentParser cmdParser;
        private static readonly Logger startupDiagLogger = LogManager.GetCurrentClassLogger();

        public event EventHandler ServiceStarted;
        public event EventHandler PreServiceStop;
        public event EventHandler ServiceStopped;
        public event EventHandler RunningChanged;
        //public event EventHandler HotplugFinished;
        public delegate void HotplugControllerHandler(ControlService sender, DS4Device device, int index);
        public event HotplugControllerHandler HotplugController;

        private byte[][] udpOutBuffers = new byte[UdpServer.NUMBER_SLOTS][]
        {
            new byte[UdpServer.DATA_RSP_PACKET_LEN], new byte[UdpServer.DATA_RSP_PACKET_LEN],
            new byte[UdpServer.DATA_RSP_PACKET_LEN], new byte[UdpServer.DATA_RSP_PACKET_LEN],
        };

        private DS4State[] oscState = new DS4State[MAX_DS4_CONTROLLER_COUNT];
        public HandleOscPacket oscCallback;

        public UDPListener oscListener;
        public UDPSender oscSender;

        void GetPadDetailForIdx(int padIdx, ref DualShockPadMeta meta)
        {
            //meta = new DualShockPadMeta();
            meta.PadId = (byte)padIdx;
            meta.Model = DsModel.DS4;

            var d = DS4Controllers[padIdx];
            if (d == null)
            {
                meta.PadMacAddress = null;
                meta.PadState = DsState.Disconnected;
                meta.ConnectionType = DsConnection.None;
                meta.Model = DsModel.None;
                meta.BatteryStatus = 0;
                meta.IsActive = false;
                return;
                //return meta;
            }

            bool isValidSerial = false;
            string stringMac = d.getMacAddress();
            if (!string.IsNullOrEmpty(stringMac))
            {
                stringMac = string.Join("", stringMac.Split(':'));
                //stringMac = stringMac.Replace(":", "").Trim();
                meta.PadMacAddress = System.Net.NetworkInformation.PhysicalAddress.Parse(stringMac);
                isValidSerial = d.isValidSerial();
            }

            if (!isValidSerial)
            {
                //meta.PadMacAddress = null;
                meta.PadState = DsState.Disconnected;
            }
            else
            {
                if (d.isSynced() || d.IsAlive())
                    meta.PadState = DsState.Connected;
                else
                    meta.PadState = DsState.Reserved;
            }

            meta.ConnectionType = (d.getConnectionType() == ConnectionType.USB) ? DsConnection.Usb : DsConnection.Bluetooth;
            meta.IsActive = !d.isDS4Idle();

            int batteryLevel = d.getBattery();
            if (d.isCharging() && batteryLevel >= 100)
                meta.BatteryStatus = DsBattery.Charged;
            else
            {
                if (batteryLevel >= 95)
                    meta.BatteryStatus = DsBattery.Full;
                else if (batteryLevel >= 70)
                    meta.BatteryStatus = DsBattery.High;
                else if (batteryLevel >= 50)
                    meta.BatteryStatus = DsBattery.Medium;
                else if (batteryLevel >= 20)
                    meta.BatteryStatus = DsBattery.Low;
                else if (batteryLevel >= 5)
                    meta.BatteryStatus = DsBattery.Dying;
                else
                    meta.BatteryStatus = DsBattery.None;
            }

            //return meta;
        }

        public ControlService(DS4WinWPF.ArgumentParser cmdParser)
        {
            this.cmdParser = cmdParser;

            Crc32Algorithm.InitializeTable(DS4Device.DefaultPolynomial);

            eventDispatchThread = new Thread(() =>
            {
                Dispatcher currentDis = Dispatcher.CurrentDispatcher;
                eventDispatcher = currentDis;
                Dispatcher.Run();
            });
            eventDispatchThread.IsBackground = true;
            eventDispatchThread.Priority = ThreadPriority.BelowNormal;
            eventDispatchThread.Name = "ControlService Events";
            eventDispatchThread.Start();

            for (int i = 0, arlength = DS4Controllers.Length; i < arlength; i++)
            {
                MappedState[i] = new DS4State();
                CurrentState[i] = new DS4State();
                TempState[i] = new DS4State();
                PreviousState[i] = new DS4State();
                ExposedState[i] = new DS4StateExposed(CurrentState[i]);
                oscState[i] = new DS4State();
                controllerExposureTransitions[i] =
                    new ControllerExposureTransitionCoordinator();

                int tempDev = i;
                Global.L2OutputSettings[i].TwoStageModeChanged += (sender, e) =>
                {
                    Mapping.l2TwoStageMappingData[tempDev].Reset();
                };

                Global.R2OutputSettings[i].TwoStageModeChanged += (sender, e) =>
                {
                    Mapping.r2TwoStageMappingData[tempDev].Reset();
                };
            }

            outputslotMan = new OutputSlotManager(
                EnsureHidHideDoesNotCloakVirtualSonyOutputs);
            //outputslotMan.SlotAssigned += OutputslotMan_SlotAssigned;
            deviceOptions = Global.DeviceOptions;

            DS4Devices.RequestElevation += DS4Devices_RequestElevation;
            DS4Devices.PrepareDS4Init = PrepareDS4DeviceInit;
            DS4Devices.PostDS4Init = PostDS4DeviceInit;
            DS4Devices.PreparePendingDevice = CheckForSupportedDevice;

            Global.UDPServerSmoothingMincutoffChanged += ChangeUdpSmoothingAttrs;
            Global.UDPServerSmoothingBetaChanged += ChangeUdpSmoothingAttrs;

            CreateOSCCallback();

            SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
            //oscListener = new UDPListener(Global.getOSCServerPortNum(), callback: oscCallback);
            //AppLogger.LogToGui("OSC LISTENER STARTED", false);
        }

        private void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
        {
            Global.PrepareAbsMonitorBounds(string.Empty);
        }

        //private void OutputslotMan_SlotAssigned(OutputSlotManager sender, int slotNum, OutSlotDevice outSlotDev)
        //{
        //    LogDebug($"Associated input controller #{outSlotDev.InputIndex + 1} ({outSlotDev.InputDisplayString}) to virtual {outSlotDev.OutputDevice.GetDeviceType()} Controller in{(outSlotDev.PermanentType != OutContType.None ? " permanent" : "")} output slot #{outSlotDev.Index + 1}");
        //}

        private long nextOscWarningTick;

        private void LogOscWarning(string message)
        {
            if (OscInputPacketPolicy.ShouldLogMalformed(ref nextOscWarningTick,
                Stopwatch.GetTimestamp(), Stopwatch.Frequency * 10))
            {
                AppLogger.LogToGui(message, false);
            }
        }

        private void CreateOSCCallback()
        {
            oscCallback = packet =>
            {
                if (!OscInputPacketPolicy.TryParse(packet,
                    Global.isInterpretingOscMonitoring(), oscState.Length,
                    out OscInputCommand command))
                {
                    LogOscWarning("Ignored invalid OSC input packet.");
                    return;
                }

                if (command.Kind == OscInputKind.BatteryRequest)
                {
                    UDPSender sender = oscSender;
                    if (sender == null || !isUsingOSCSender())
                    {
                        LogOscWarning("OSC battery request ignored because the sender is off.");
                        return;
                    }

                    try
                    {
                        sender.Send(new OscMessage(
                            "/ds4windows/monitor/" + command.ControllerIndex + "/battery",
                            oscState[command.ControllerIndex].Battery));
                    }
                    catch (Exception e)
                    {
                        LogOscWarning("OSC battery response failed: " +
                            e.GetType().Name);
                    }
                    return;
                }

                command.ApplyTo(oscState[command.ControllerIndex]);
            };
        }

        public void RefreshOutputKBMHandler()
        {
            lock (outputKbmHandlerLock)
            {
                if (Global.outputKBMHandler != null)
                {
                    Global.outputKBMHandler.Disconnect();
                    Global.outputKBMHandler = null;
                }

                if (Global.outputKBMMapping != null)
                {
                    Global.outputKBMMapping = null;
                }

                InitOutputKBMHandler();
            }
        }

        private void InitOutputKBMHandler()
        {
            string attemptVirtualkbmHandler = cmdParser.VirtualkbmHandler;
            InitOutputKBMHandler(attemptVirtualkbmHandler);
        }

        private void InitOutputKBMHandler(string attemptVirtualkbmHandler)
        {
            StartupDiag($"InitOutputKBMHandler begin requested={attemptVirtualkbmHandler}");
            Global.InitOutputKBMHandler(attemptVirtualkbmHandler);
            StartupDiag($"InitOutputKBMHandler created handler={Global.outputKBMHandler?.GetIdentifier()}");

            bool handlerConnected = false;
            try
            {
                StartupDiag($"OutputKBM.Connect begin handler={Global.outputKBMHandler?.GetIdentifier()}");
                handlerConnected = Global.outputKBMHandler.Connect();
                StartupDiag($"OutputKBM.Connect end handler={Global.outputKBMHandler?.GetIdentifier()} connected={handlerConnected}");
            }
            catch (Exception ex)
            {
                StartupDiag($"OutputKBM.Connect exception handler={Global.outputKBMHandler?.GetIdentifier()} {ex.GetType().Name}: {ex.Message}");
            }

            if (!handlerConnected &&
                attemptVirtualkbmHandler != VirtualKBMFactory.GetFallbackHandlerIdentifier())
            {
                StartupDiag($"OutputKBM falling back to {VirtualKBMFactory.GetFallbackHandlerIdentifier()}");
                Global.outputKBMHandler = VirtualKBMFactory.GetFallbackHandler();
            }

            Global.InitOutputKBMMapping(Global.outputKBMHandler.GetIdentifier());
            Global.outputKBMMapping.PopulateConstants();
            Global.outputKBMMapping.PopulateMappings();
            StartupDiag($"InitOutputKBMHandler end active={Global.outputKBMHandler?.GetFullDisplayName()} mapping={Global.outputKBMMapping?.GetType().Name}");
        }

        public void PostDS4DeviceInit(DS4Device device)
        {
        }

        private void PrepareDS4DeviceSettingHooks(DS4Device device)
        {
        }

        public bool CheckForSupportedDevice(HidDevice device, VidPidInfo metaInfo)
        {
            bool result = false;
            switch (metaInfo.inputDevType)
            {
                case InputDevices.InputDeviceType.DS4:
                    result = deviceOptions.DS4DeviceOpts.Enabled;
                    break;
                case InputDevices.InputDeviceType.DS3:
                    // DualShock 3 is a planned milestone, not a supported one.
                    // The implementation and its stored option survive for
                    // that future gate, but this release must not act on a
                    // stored or imported setting that would silently turn on a
                    // path no genuine hardware has validated.
                    result = ProductScope.DualShock3Offered &&
                        deviceOptions.DS3DeviceOpts.Enabled;
                    break;
                default:
                    break;
            }

            return result;
        }

        public void PrepareDS4DeviceInit(DS4Device device)
        {
            // Does nothing now
        }

        public void ShutDown()
        {
            lock (serviceLifecycleLock)
            {
                ShutDownCore();
            }
        }

        public void StopAndShutDown(bool immediateUnplug)
        {
            lock (serviceLifecycleLock)
            {
                if (running)
                {
                    StopCore(showlog: true,
                        immediateUnplug: immediateUnplug);
                }

                ShutDownCore();
            }
        }

        private void ShutDownCore()
        {
            ReleaseHidHideManagedDevices();
            if (hidHideOwnershipJournal?.IsTransientRunInProgress == true &&
                !hidHideOwnershipJournal.MarkStoppedRunRecoveryRequired())
            {
                StartupDiag("HidHide recovery state could not be saved " +
                    "during shutdown");
            }
            outputslotMan.ShutDown();
            OutputSlotPersist.WriteConfig(outputslotMan);

            eventDispatcher.InvokeShutdown();
            eventDispatcher = null;

            eventDispatchThread.Join();
            eventDispatchThread = null;
        }

        private void DS4Devices_RequestElevation(RequestElevationArgs args)
        {
            // Launches an elevated child process to re-enable device
            ProcessStartInfo startInfo =
                new ProcessStartInfo(Global.exelocation);
            startInfo.Verb = "runas";
            startInfo.Arguments = "re-enabledevice " + args.InstanceId;
            startInfo.UseShellExecute = true;

            try
            {
                Process child = Process.Start(startInfo);
                if (child == null)
                {
                    return;
                }

                if (!child.WaitForExit(30000))
                {
                    // Never terminate this helper while it may be between the
                    // SetupAPI disable and enable calls.  The helper owns the
                    // recovery path and must be allowed to finish even when a
                    // slow driver stack exceeds the normal wait window.
                    LogDebug("The elevated controller recovery helper is still running; leaving it active so it can safely re-enable the HID device.", true);
                }
                else
                {
                    args.StatusCode = child.ExitCode;
                }
                child.Dispose();
            }
            catch { }
        }

        public void CheckHidHidePresence(string ExePath = "", string ExeName = "Autoprofile Exe", bool AddExe = true) // Default value for D4W Startup
        {
            if (Global.hidHideInstalled)
            {
                LogDebug("HidHide control device found");
                using (HidHideAPIDevice hidHideDevice = new HidHideAPIDevice())
                {
                    if (!hidHideDevice.IsOpen())
                    {
                        return;
                    }
                    // Catch Blank Values and initialize for Startup. Also catches empty Values.
                    // Also Catches Empty values in auto-profiler, and defaults to trying to re-add D4W. Will fail harmlessly later.
                    if (ExePath == "") { ExePath = Global.exelocation; ExeName = ProductIdentity.Name; AddExe = true; }

                    // Check for inverse application cloak. If setting is being used in HidHide,
                    // skip checking HidHide whitelist for this application.
                    bool inverseAppCloak = hidHideDevice.GetWhiteListInverseState();
                    if (inverseAppCloak)
                    {
                        return;
                    }


                    HidHideOwnershipJournal ownershipJournal =
                        GetHidHideOwnershipJournal();
                    List<string> dosPaths = hidHideDevice.GetWhitelist();
                    dosPaths = ReconcileOwnedHidHideWhitelistEntries(
                        hidHideDevice, ownershipJournal, dosPaths);

                    int maxPathCheckLength = 512;
                    StringBuilder sb = new StringBuilder(maxPathCheckLength);

                    DirectoryInfo dirInfo = new DirectoryInfo(Path.GetDirectoryName(ExePath));
                    // Check if exe is placed in a junction symlink directory (done with Scoop).
                    // Good enough
                    if (dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint) &&
                        dirInfo.LinkTarget != null)
                    {
                        // App directory is a junction. Find real directory and get proper path
                        // for inserting into HidHide
                        ExePath = Path.Combine(dirInfo.LinkTarget, Path.GetFileName(ExePath));
                    }

                    string driveLetter = Path.GetPathRoot(ExePath).Replace("\\", "");
                    uint _ = NativeMethods.QueryDosDevice(driveLetter, sb, maxPathCheckLength);
                    //int error = Marshal.GetLastWin32Error();

                    string dosDrivePath = sb.ToString();
                    // Strip a possible \??\ prefix.
                    if (dosDrivePath.StartsWith(@"\??\"))
                    {
                        dosDrivePath = dosDrivePath.Remove(0, 4);
                    }

                    string partial = ExePath.Replace(driveLetter, "");
                    // Need to trim starting '\\' from path2 or Path.Combine will
                    // treat it as an absolute path and only return path2
                    string realPath = Path.Combine(dosDrivePath, partial.TrimStart('\\'));
                    bool exists = HidHideOwnershipPolicy.Contains(dosPaths,
                        realPath);
                    if (!exists && AddExe)
                    {
                        if (!ownershipJournal.IsReliable)
                        {
                            StartupDiag($"HidHide did not add {ExeName} to the whitelist because ownership cannot be recorded safely");
                            return;
                        }

                        LogDebug($"{ExeName} not found in HidHide whitelist. Adding to list");
                        dosPaths.Add(realPath);
                        if (hidHideDevice.SetWhitelist(dosPaths) &&
                            !ownershipJournal.RecordWhitelistEntry(realPath))
                        {
                            dosPaths.RemoveAll(path => string.Equals(path,
                                realPath, StringComparison.OrdinalIgnoreCase));
                            hidHideDevice.SetWhitelist(dosPaths);
                            StartupDiag($"HidHide rolled back the {ExeName} whitelist entry because ownership could not be recorded");
                        }
                    }
                    if (exists && !AddExe)
                    {
                        if (!ownershipJournal.OwnsWhitelistEntry(realPath))
                        {
                            StartupDiag($"HidHide preserved the pre-existing {ExeName} whitelist entry because {ProductIdentity.Name} did not create it");
                            return;
                        }

                        LogDebug($"{ExeName} found in HidHide whitelist. Removing from list");
                        dosPaths.RemoveAll(path => string.Equals(path,
                            realPath, StringComparison.OrdinalIgnoreCase));
                        if (hidHideDevice.SetWhitelist(dosPaths))
                        {
                            ownershipJournal.ForgetWhitelistEntry(realPath);
                        }
                    }
                }
            }
        }

        private List<string> ReconcileOwnedHidHideWhitelistEntries(
            IHidHideWhitelistDevice hidHideDevice,
            HidHideOwnershipJournal ownershipJournal,
            IReadOnlyCollection<string> currentWhitelist)
        {
            List<string> current = (currentWhitelist ?? Array.Empty<string>())
                .ToList();
            if (ownershipJournal == null || !ownershipJournal.IsReliable)
            {
                return current;
            }

            HidHideWhitelistReconciliationPlan plan =
                HidHideWhitelistReconciliationPolicy.Create(current,
                    ownershipJournal.PersistentWhitelistEntries,
                    HidHideOwnedApplicationPathInspector.Inspect);
            string[] journalOnly = plan.JournalEntriesToForget
                .Except(plan.EntriesToRemove,
                    StringComparer.OrdinalIgnoreCase).ToArray();
            if (journalOnly.Length > 0 &&
                !ownershipJournal.ForgetWhitelistEntries(journalOnly))
            {
                StartupDiag("HidHide preserved stale whitelist ownership " +
                    "records because the ownership journal could not be " +
                    "updated safely");
                return current;
            }

            if (plan.EntriesToRemove.Count == 0)
            {
                return current;
            }

            HidHideWhitelistMutationResult result =
                HidHideWhitelistMutationGateway.RemoveExact(hidHideDevice,
                    plan.EntriesToRemove);
            if (!result.Succeeded)
            {
                StartupDiag("HidHide preserved owned whitelist entries: " +
                    result.Error);
                return result.After.Count > 0
                    ? result.After.ToList()
                    : hidHideDevice.GetWhitelist();
            }

            if (!ownershipJournal.ForgetWhitelistEntries(
                    plan.EntriesToRemove))
            {
                StartupDiag("HidHide removed obsolete PureDS4 whitelist " +
                    "entries, but their ownership records could not be " +
                    "cleared; recovery remains fail-closed");
            }
            else
            {
                StartupDiag("HidHide removed obsolete PureDS4 whitelist " +
                    $"entries: {string.Join(", ", plan.EntriesToRemove)}");
            }

            return result.After.ToList();
        }

        public void LoadPermanentSlotsConfig()
        {
            OutputSlotPersist.ReadConfig(outputslotMan);
        }

        public void UpdateHidHideAttributes()
        {
            if (Global.hidHideInstalled)
            {
                hidDeviceHidingAffectedDevs.Clear();
                hidDeviceHidingExemptedDevs.Clear(); // No known equivalent in HidHide
                hidDeviceHidingForced = false; // No known equivalent in HidHide
                hidDeviceHidingEnabled = false;

                using (HidHideAPIDevice hidHideDevice = new HidHideAPIDevice(writeAccess: false))
                {
                    if (!hidHideDevice.IsOpen())
                    {
                        return;
                    }

                    bool active = hidHideDevice.GetActiveState();
                    List<string> instances = hidHideDevice.GetBlacklist();

                    hidDeviceHidingEnabled = active;
                    foreach (string instance in instances)
                    {
                        hidDeviceHidingAffectedDevs.Add(instance.ToUpper());
                    }
                }
            }
        }

        public void UpdateHidHiddenAttributes()
        {
            if (Global.hidHideInstalled)
            {
                UpdateHidHideAttributes();
            }
        }

        private bool CheckAffected(DS4Device dev)
        {
            bool result = false;
            if (dev != null && hidDeviceHidingEnabled)
            {
                string deviceInstanceId = Global.GetInstanceIdFromDevicePath(dev.HidDevice.DevicePath);
                if (Global.hidHideInstalled)
                {
                    result = Global.CheckHidHideAffectedStatus(deviceInstanceId,
                        hidDeviceHidingAffectedDevs, hidDeviceHidingExemptedDevs, hidDeviceHidingForced);
                }
            }

            return result;
        }

        /// <summary>
        /// Obtain extra mappable controls not on a DS4 that should be added
        /// to the checked inputs list. Keeps Mapping class from having to check
        /// extra non-DS4 buttons for DS4 controllers
        /// </summary>
        /// <param name="dev">Instance of input device</param>
        /// <returns>List of extra controls to check in Mapping class</returns>
        private List<DS4Controls> GetKnownExtraButtons(DS4Device dev)
        {
            List<DS4Controls> result = new List<DS4Controls>();
            switch (dev.DeviceType)
            {
                default:
                    break;
            }

            return result;
        }

        private void ChangeExclusiveStatus(DS4Device dev)
        {
            if (Global.hidHideInstalled)
            {
                dev.CurrentExclusiveStatus = DS4Device.ExclusiveStatus.HidHideAffected;
            }
        }

        internal bool HidHideRecoveryRequired =>
            hidHideOwnershipJournal != null &&
            (!hidHideOwnershipJournal.IsReliable ||
             hidHideOwnershipJournal.RecoveryRequired ||
             (!running && hidHideOwnershipJournal.IsTransientRunInProgress));

        private HidHideOwnershipJournal GetHidHideOwnershipJournal(
            bool restoreExternalContainment = true)
        {
            if (hidHideOwnershipJournal == null)
            {
                string appDataRoot = Global.appdatapath;
                if (string.IsNullOrWhiteSpace(appDataRoot))
                {
                    appDataRoot = Path.Combine(Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData),
                        ProductIdentity.DataFolderName);
                }

                hidHideOwnershipJournal = new HidHideOwnershipJournal(
                    Path.Combine(appDataRoot,
                        HidHideOwnershipJournal.FileName));
                hidHideOwnershipJournal.Load();
            }
            if (restoreExternalContainment &&
                !hidHideExternalRecoveryAttempted)
            {
                hidHideExternalRecoveryAttempted = true;
                if (hidHideOwnershipJournal.IsReliable &&
                    hidHideOwnershipJournal.ExternalContainmentSuspensions.Count > 0)
                {
                    TryRestoreExternalContainmentSuspensions(
                        hidHideOwnershipJournal, startupRecovery: true);
                }
            }

            if (!hidHideRecoveryReported &&
                (!hidHideOwnershipJournal.IsReliable ||
                 hidHideOwnershipJournal.RecoveryRequired))
            {
                hidHideRecoveryReported = true;
                string detail = hidHideOwnershipJournal.IsReliable
                    ? string.Join(", ", hidHideOwnershipJournal.
                        UnresolvedPersistentBlacklistEntries.Concat(
                            hidHideOwnershipJournal.
                                ExternalContainmentSuspensions.Select(
                                    suspension => suspension.InstanceId)))
                    : "the ownership journal could not be read";
                string message =
                    $"HidHide recovery is required. {ProductIdentity.Name} preserved " +
                    "uncertain global configuration instead of removing it. " +
                    "Stop controller handling and open Tools > HidHide recovery. " +
                    $"If inspection fails, review HidHide Configuration Client and {hidHideOwnershipJournal.Path}. " +
                    $"Details: {detail}.";
                StartupDiag(message);
                AppLogger.LogToGui(message, true);
            }

            return hidHideOwnershipJournal;
        }

        /// <summary>
        /// Adds the device to HidHide while the DS4Windows service is running.
        /// Stop releases managed entries and Start acquires them again.
        /// </summary>
        private bool EnsureHidHideSessionForDevice(DS4Device dev)
        {
            if (!Global.hidHideInstalled || dev == null) return false;

            string instanceId = Global.GetInstanceIdFromDevicePath(dev.HidDevice.DevicePath);
            if (string.IsNullOrEmpty(instanceId)) return false;

            return EnsureHidHideForInstance(instanceId, dev.DisplayName,
                preferPersistent: false);
        }

        private bool EnsureHidHideForInstance(string instanceId,
            string displayName, bool preferPersistent)
        {
            if (!Global.hidHideInstalled ||
                string.IsNullOrWhiteSpace(instanceId))
            {
                return false;
            }

            displayName = string.IsNullOrWhiteSpace(displayName)
                ? "controller" : displayName;

            bool alreadyManaged;
            lock (hidHideSessionLock)
            {
                alreadyManaged = hidHideSessionManagedInstanceIds.Contains(instanceId) ||
                    hidHidePersistentManagedInstanceIds.Contains(instanceId);
            }

            try
            {
                using (HidHideAPIDevice hidHideDevice = new HidHideAPIDevice())
                {
                    if (!hidHideDevice.IsOpen()) return false;

                    HidHideOwnershipJournal ownershipJournal =
                        GetHidHideOwnershipJournal();
                    if (!ownershipJournal.IsReliable ||
                        ownershipJournal.RecoveryRequired)
                    {
                        StartupDiag("HidHide containment was not changed because recovery is required");
                        return false;
                    }
                    List<string> currentBlacklist = hidHideDevice.GetBlacklist()
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .ToList();

                    lock (hidHideSessionLock)
                    {
                        if (!hidHideTransientRunStarted ||
                            !ownershipJournal.IsTransientRunInProgress)
                        {
                            if (!ownershipJournal.BeginTransientRun())
                            {
                                StartupDiag("HidHide session was not changed because its ownership journal is unavailable");
                                return false;
                            }

                            hidHideTransientRunStarted = true;
                            hidHideBaselineBlacklist = new HashSet<string>(
                                currentBlacklist,
                                StringComparer.OrdinalIgnoreCase);
                        }
                    }

                    bool active = hidHideDevice.GetActiveState();
                    lock (hidHideSessionLock)
                    {
                        hidHideActiveStateBeforeManagedSession ??= active;
                    }

                    if (!active)
                    {
                        if (!hidHideDevice.SetActiveState(true))
                        {
                            StartupDiag($"HidHide failed to enable cloaking for {displayName} ({instanceId})");
                            return false;
                        }

                        if (!ownershipJournal.RecordActiveStateEnabled())
                        {
                            hidHideDevice.SetActiveState(false);
                            StartupDiag($"HidHide rolled back cloaking for {displayName} because active-state ownership could not be recorded");
                            return false;
                        }
                    }

                    if (!alreadyManaged &&
                        !HidHideOwnershipPolicy.Contains(currentBlacklist,
                            instanceId))
                    {
                        if (!preferPersistent && hidHideDevice.AddSessionBlacklist(
                                new List<string> { instanceId }))
                        {
                            lock (hidHideSessionLock)
                            {
                                hidHideSessionManagedInstanceIds.Add(instanceId);
                            }

                            LogDebug($"HidHide session hiding enabled for {displayName} ({instanceId})", false);
                        }
                        else if (!EnsurePersistentHidHideBlacklist(
                            hidHideDevice, instanceId, displayName,
                            ownershipJournal))
                        {
                            return false;
                        }
                    }
                    else if (!alreadyManaged)
                    {
                        StartupDiag($"HidHide preserved pre-existing blacklist ownership for {displayName} ({instanceId})");
                    }

                    UpdateHidHideAttributes();
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogDebug($"HidHide session setup failed for {displayName}: {ex.Message}", true);
                return false;
            }
        }

        private bool EnsurePersistentHidHideBlacklist(
            HidHideAPIDevice hidHideDevice, string instanceId,
            string displayName,
            HidHideOwnershipJournal ownershipJournal)
        {
            bool ownershipRecorded = false;
            HidHideBlacklistMutationResult mutation =
                HidHideBlacklistMutationGateway.Mutate(hidHideDevice,
                    current => HidHideBlacklistMutationGateway.AddExact(
                        current, instanceId),
                    () =>
                    {
                        ownershipRecorded = ownershipJournal.
                            RecordPersistentBlacklistEntry(instanceId);
                        return ownershipRecorded;
                    });
            if (mutation.Succeeded && !mutation.Changed)
            {
                StartupDiag($"HidHide preserved pre-existing persistent blacklist entry {instanceId}");
                return true;
            }

            if (!mutation.Succeeded)
            {
                if (ownershipRecorded && !mutation.WriteAttempted)
                {
                    ownershipJournal.CompleteTransientRun(
                        new[] { instanceId }, activeStateRestored: false);
                }
                StartupDiag($"HidHide persistent blacklist fallback failed " +
                    $"for {displayName} ({instanceId}): {mutation.Error}");
                return false;
            }

            lock (hidHideSessionLock)
            {
                hidHidePersistentManagedInstanceIds.Add(instanceId);
            }

            LogDebug($"HidHide persistent hiding enabled for {displayName} ({instanceId})", false);
            return true;
        }

        private void QueueSteamInputReclaim(DS4Device device)
        {
            if (!Global.ReclaimSteamInput || !Global.hidHideInstalled ||
                device?.CurrentExclusiveStatus !=
                    DS4Device.ExclusiveStatus.HidHideAffected ||
                !IsSteamClientRunning())
            {
                return;
            }

            string instanceId = Global.GetInstanceIdFromDevicePath(
                device.HidDevice.DevicePath);
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                return;
            }

            DateTime now = DateTime.UtcNow;
            lock (steamInputReclaimLock)
            {
                if (steamInputReclaimAttempts.TryGetValue(instanceId,
                        out DateTime previousAttempt) &&
                    now - previousAttempt < TimeSpan.FromSeconds(10))
                {
                    return;
                }

                steamInputReclaimAttempts[instanceId] = now;
            }

            string displayName = device.DisplayName;
            Task task = Task.Run(async () =>
            {
                bool success = false;
                try
                {
                    if (!Global.IsAdministrator())
                    {
                        LogDebug($"Steam Input reclaim requires {ProductIdentity.Name} to " +
                            "run as administrator.", true);
                        return;
                    }

                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = Path.Combine(Environment.SystemDirectory,
                            "pnputil.exe"),
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    };
                    startInfo.ArgumentList.Add("/restart-device");
                    startInfo.ArgumentList.Add(instanceId);

                    using Process process = Process.Start(startInfo);
                    if (process == null)
                    {
                        return;
                    }

                    Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                    Task<string> errorTask = process.StandardError.ReadToEndAsync();
                    using CancellationTokenSource timeout =
                        new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    try
                    {
                        await process.WaitForExitAsync(timeout.Token)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        try { process.Kill(entireProcessTree: true); }
                        catch { }
                        LogDebug($"Steam Input reclaim timed out for " +
                            $"{displayName}.", true);
                        return;
                    }

                    string output = await outputTask.ConfigureAwait(false);
                    string error = await errorTask.ConfigureAwait(false);
                    success = process.ExitCode == 0;
                    if (success)
                    {
                        LogDebug($"Reclaimed {displayName} from Steam " +
                            "Input; reconnecting its hidden HID collection.",
                            false);
                    }
                    else
                    {
                        string detail = string.IsNullOrWhiteSpace(error)
                            ? output : error;
                        LogDebug($"Steam Input reclaim failed for " +
                            $"{displayName}: {detail.Trim()}", true);
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"Steam Input reclaim failed for " +
                        $"{displayName}: {ex.Message}", true);
                }
                finally
                {
                    if (!success)
                    {
                        lock (steamInputReclaimLock)
                        {
                            steamInputReclaimAttempts.Remove(instanceId);
                        }
                    }
                }
            });
            Util.LogAssistBackgroundTask(task);
        }

        private static bool IsSteamClientRunning()
        {
            Process[] processes = null;
            try
            {
                processes = Process.GetProcessesByName("steam");
                return processes.Length > 0;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (processes != null)
                {
                    foreach (Process process in processes)
                    {
                        process.Dispose();
                    }
                }
            }
        }

        private void ReleaseHidHideManagedDevices()
        {
            if (!Global.hidHideInstalled) return;

            if (hidHideOwnershipJournal != null)
            {
                TryRestoreExternalContainmentSuspensions(
                    hidHideOwnershipJournal, startupRecovery: false);
            }

            List<string> sessionIds;
            List<string> persistentIds;
            bool? restoreActiveState;
            lock (hidHideSessionLock)
            {
                sessionIds = hidHideSessionManagedInstanceIds.ToList();
                persistentIds = hidHidePersistentManagedInstanceIds.ToList();
                restoreActiveState = hidHideActiveStateBeforeManagedSession;
            }

            if (sessionIds.Count == 0 && persistentIds.Count == 0 && restoreActiveState is null) return;

            try
            {
                using (HidHideAPIDevice hidHideDevice = new HidHideAPIDevice())
                {
                    if (!hidHideDevice.IsOpen())
                    {
                        StartupDiag("Could not open HidHide while releasing managed controllers; cleanup will be retried");
                        return;
                    }

                    bool sessionReleased = sessionIds.Count == 0;
                    if (sessionIds.Count > 0)
                    {
                        sessionReleased = hidHideDevice.ClearSessionBlacklist();
                        StartupDiag(sessionReleased
                            ? $"Released {sessionIds.Count} {ProductIdentity.Name}-managed HidHide session entries"
                            : "HidHide session release failed; cleanup will be retried");
                    }

                    bool persistentReleased = persistentIds.Count == 0;
                    List<string> releasedPersistentIds = new List<string>();
                    List<string> blacklistAfterCleanup = null;
                    if (persistentIds.Count > 0)
                    {
                        HidHideBlacklistMutationResult mutation =
                            HidHideBlacklistMutationGateway.Mutate(
                                hidHideDevice,
                                current => HidHideBlacklistMutationGateway.
                                    RemoveExact(current, persistentIds));
                        persistentReleased = mutation.Succeeded;
                        blacklistAfterCleanup = mutation.After.ToList();
                        if (persistentReleased)
                        {
                            releasedPersistentIds = persistentIds.Where(id =>
                                HidHideBlacklistMutationGateway.Contains(
                                    mutation.Before, id) &&
                                !HidHideBlacklistMutationGateway.Contains(
                                    mutation.After, id)).ToList();
                        }
                        if (releasedPersistentIds.Count > 0 &&
                            persistentReleased)
                        {
                            StartupDiag($"Released {releasedPersistentIds.Count} {ProductIdentity.Name}-created HidHide blacklist entries");
                        }
                        else if (!persistentReleased)
                        {
                            StartupDiag("HidHide persistent blacklist release failed; cleanup will be retried");
                        }
                    }

                    bool activeStateRestored = restoreActiveState != false;
                    if (restoreActiveState == false)
                    {
                        blacklistAfterCleanup ??= hidHideDevice.GetBlacklist()
                            .Where(item => !string.IsNullOrWhiteSpace(item))
                            .ToList();
                        bool safeToRestore = sessionReleased &&
                            persistentReleased &&
                            HidHideOwnershipPolicy.CanRestoreInactiveState(
                                hidHideBaselineBlacklist,
                                blacklistAfterCleanup);
                        activeStateRestored = safeToRestore &&
                            hidHideDevice.SetActiveState(false);
                        if (!activeStateRestored)
                        {
                            StartupDiag(safeToRestore
                                ? "HidHide cloaking state restore failed; cleanup will be retried"
                                : "HidHide cloaking remained enabled because global state changed during this run; recovery is required");
                        }
                    }

                    if (hidHideOwnershipJournal != null)
                    {
                        hidHideOwnershipJournal.CompleteTransientRun(
                            persistentReleased ? persistentIds :
                                Array.Empty<string>(),
                            activeStateRestored);
                    }

                    lock (hidHideSessionLock)
                    {
                        if (sessionReleased)
                        {
                            hidHideSessionManagedInstanceIds.ExceptWith(sessionIds);
                        }

                        if (persistentReleased)
                        {
                            hidHidePersistentManagedInstanceIds.ExceptWith(persistentIds);
                        }

                        if (activeStateRestored &&
                            hidHideSessionManagedInstanceIds.Count == 0 &&
                            hidHidePersistentManagedInstanceIds.Count == 0)
                        {
                            hidHideActiveStateBeforeManagedSession = null;
                            hidHideBaselineBlacklist = null;
                            hidHideTransientRunStarted = false;
                        }
                    }

                    UpdateHidHideAttributes();
                }
            }
            catch (Exception ex)
            {
                StartupDiag($"ReleaseHidHideManagedDevices exception {ex.GetType().Name}: {ex.Message}");
            }
        }

        private bool EnsureHidHideForVirtualOutput(int index,
            DS4Device device, OutContType contType)
        {
            contType = contType.Normalize();
            if (device == null)
            {
                return false;
            }

            if (!ViiperOutDevice.IsViiperType(contType))
            {
                return true;
            }

            if (EnsureHidHideSessionForDevice(device))
            {
                ChangeExclusiveStatus(device);
                StartupDiag($"HidHide virtual-output containment ready index={index} type={contType}");
                return true;
            }

            LogDebug($"VIIPER {contType} output was not created because the physical {device.DisplayName} could not be contained with HidHide.", true);
            return false;
        }

        /// <summary>
        /// A VIIPER Sony output is a complete USB/IP HID, so an instance path
        /// accidentally retained in HidHide's persistent blacklist makes the
        /// virtual controller healthy and writable inside DS4Windows while it
        /// is invisible to games. Remove only the exact before/after paths that
        /// this process just created; physical Sony controllers stay cloaked.
        /// </summary>
        private void EnsureHidHideDoesNotCloakVirtualSonyOutputs(
            IReadOnlyCollection<string> devicePaths)
        {
            if (!Global.hidHideInstalled || devicePaths == null ||
                devicePaths.Count == 0)
            {
                return;
            }

            HashSet<string> instanceIds = new HashSet<string>(
                devicePaths.Select(Global.GetInstanceIdFromDevicePath)
                    .Where(instanceId => !string.IsNullOrWhiteSpace(instanceId)),
                StringComparer.OrdinalIgnoreCase);
            if (instanceIds.Count == 0)
            {
                return;
            }

            try
            {
                using (HidHideAPIDevice hidHideDevice = new HidHideAPIDevice())
                {
                    if (!hidHideDevice.IsOpen())
                    {
                        StartupDiag(
                            "Could not open HidHide while exempting a VIIPER virtual Sony output");
                        return;
                    }

                    HidHideBlacklistMutationResult mutation =
                        HidHideBlacklistMutationGateway.Mutate(hidHideDevice,
                            current => HidHideBlacklistMutationGateway.
                                RemoveExact(current, instanceIds));
                    int removed = mutation.Before.Count(entry =>
                        instanceIds.Contains(entry)) -
                        mutation.After.Count(entry =>
                            instanceIds.Contains(entry));
                    if (mutation.Succeeded && removed == 0)
                    {
                        return;
                    }

                    if (!mutation.Succeeded)
                    {
                        StartupDiag(
                            $"HidHide failed to exempt VIIPER virtual Sony output entries: {mutation.Error}");
                        return;
                    }

                    lock (hidHideSessionLock)
                    {
                        hidHidePersistentManagedInstanceIds.ExceptWith(instanceIds);
                    }

                    StartupDiag(
                        $"HidHide exempted {removed} VIIPER virtual Sony output entr{(removed == 1 ? "y" : "ies")}: {string.Join(", ", instanceIds)}");
                    UpdateHidHideAttributes();
                }
            }
            catch (Exception ex)
            {
                StartupDiag(
                    $"HidHide VIIPER virtual-output exemption failed {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void TestQueueBus(Action temp)
        {
            eventDispatcher.BeginInvoke(() =>
            {
                temp?.Invoke();
            });
        }

        public void ChangeUDPStatus(bool state, bool openPort = true)
        {

            if (state && _udpServer == null)
            {
                udpChangeStatus = true;
                TestQueueBus(() =>
                {
                    _udpServer = new UdpServer(GetPadDetailForIdx);
                    if (openPort)
                    {
                        // Change thread affinity of object to have normal priority
                        Task.Run(() =>
                        {
                            var UDP_SERVER_PORT = Global.getUDPServerPortNum();
                            var UDP_SERVER_LISTEN_ADDRESS = Global.getUDPServerListenAddress();

                            try
                            {
                                _udpServer.Start(UDP_SERVER_PORT, UDP_SERVER_LISTEN_ADDRESS);
                                LogDebug($"UDP server listening on address {UDP_SERVER_LISTEN_ADDRESS} port {UDP_SERVER_PORT}");
                            }
                            catch (System.Net.Sockets.SocketException ex)
                            {
                                var errMsg = String.Format("Couldn't start UDP server on address {0}:{1}, outside applications won't be able to access pad data ({2})", UDP_SERVER_LISTEN_ADDRESS, UDP_SERVER_PORT, ex.SocketErrorCode);

                                LogDebug(errMsg, true);
                                AppLogger.LogToTray(errMsg, true, true);
                            }
                        }).Wait();
                    }

                    udpChangeStatus = false;
                });
            }
            else if (!state && _udpServer != null)
            {
                TestQueueBus(() =>
                {
                    udpChangeStatus = true;
                    _udpServer.Stop();
                    _udpServer = null;
                    AppLogger.LogToGui("Closed UDP server", false);
                    udpChangeStatus = false;

                    for (int i = 0; i < UdpServer.NUMBER_SLOTS; i++)
                    {
                        ResetUdpSmoothingFilters(i);
                    }
                });
            }
        }

        public void ChangeOSCListenerStatus(bool state)
        {
            if (state)
            {
                oscListener = new UDPListener(Global.getOSCServerPortNum(), callback: oscCallback);

                AppLogger.LogToGui("OSC LISTENER STARTED AT PORT: " + Global.getOSCServerPortNum(), false);
            }
            else
            {
                oscListener.Close();
                oscListener = null;
                AppLogger.LogToGui("OSC LISTENER STOPPED", false);
            }
        }

        public void ChangeOSCSenderStatus(bool state)
        {
            if (state)
            {
                AppLogger.LogToGui("OSC SENDER STARTED AT IP: " + Global.getOSCSenderAddress() + " PORT: " + Global.getOSCSenderPortNum(), false);
                oscSender = new UDPSender(Global.getOSCSenderAddress(), Global.getOSCSenderPortNum());
            }
            else
            {
                AppLogger.LogToGui("OSC SENDER STOPPED", false);
                if (oscSender == null) { return; }
                oscSender.Close();
                oscSender = null;
            }
        }

        public void ChangeMotionEventStatus(bool state)
        {
            IEnumerable<DS4Device> devices = DS4Devices.getDS4Controllers();
            if (state)
            {
                int i = 0;
                foreach (DS4Device dev in devices)
                {
                    int tempIdx = i;
                    dev.queueEvent(() =>
                    {
                        if (i < UdpServer.NUMBER_SLOTS)
                        {
                            PrepareDevUDPMotion(dev, tempIdx);
                        }
                    });

                    i++;
                }
            }
            else
            {
                foreach (DS4Device dev in devices)
                {
                    dev.queueEvent(() =>
                    {
                        if (dev.MotionEvent != null)
                        {
                            dev.Report -= dev.MotionEvent;
                            dev.MotionEvent = null;
                        }
                    });
                }
            }
        }

        private bool udpChangeStatus = false;
        public bool changingUDPPort = false;
        public async void UseUDPPort()
        {
            changingUDPPort = true;
            IEnumerable<DS4Device> devices = DS4Devices.getDS4Controllers();
            foreach (DS4Device dev in devices)
            {
                dev.queueEvent(() =>
                {
                    if (dev.MotionEvent != null)
                    {
                        dev.Report -= dev.MotionEvent;
                    }
                });
            }

            await Task.Delay(100);

            var UDP_SERVER_PORT = Global.getUDPServerPortNum();
            var UDP_SERVER_LISTEN_ADDRESS = Global.getUDPServerListenAddress();

            try
            {
                _udpServer.Start(UDP_SERVER_PORT, UDP_SERVER_LISTEN_ADDRESS);
                foreach (DS4Device dev in devices)
                {
                    dev.queueEvent(() =>
                    {
                        if (dev.MotionEvent != null)
                        {
                            dev.Report += dev.MotionEvent;
                        }
                    });
                }
                LogDebug($"UDP server listening on address {UDP_SERVER_LISTEN_ADDRESS} port {UDP_SERVER_PORT}");
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                var errMsg = String.Format("Couldn't start UDP server on address {0}:{1}, outside applications won't be able to access pad data ({2})", UDP_SERVER_LISTEN_ADDRESS, UDP_SERVER_PORT, ex.SocketErrorCode);

                LogDebug(errMsg, true);
                AppLogger.LogToTray(errMsg, true, true);
            }

            changingUDPPort = false;
        }

        private void WarnExclusiveModeFailure(DS4Device device)
        {
            if (DS4Devices.isExclusiveMode && !device.isExclusive())
            {
                string message = DS4WinWPF.Properties.Resources.CouldNotOpenDS4.Replace("*Mac address*", device.getMacAddress()) + " " +
                    DS4WinWPF.Properties.Resources.QuitOtherPrograms;
                LogDebug(message, true);
                AppLogger.LogToTray(message, true);
            }
        }

        public void AssignInitialDevices()
        {
            foreach (OutSlotDevice slotDevice in outputslotMan.OutputSlots)
            {
                if (slotDevice.CurrentReserveStatus ==
                    OutSlotDevice.ReserveStatus.Permanent)
                {
                    OutputDevice outDevice = EstablishOutDevice(0, slotDevice.PermanentType);
                    outputslotMan.DeferredPlugin(outDevice, -1, "", outputDevices, slotDevice.PermanentType);
                }
            }
            /*OutSlotDevice slotDevice =
                outputslotMan.FindExistUnboundSlotType(OutContType.X360);

            if (slotDevice == null)
            {
                slotDevice = outputslotMan.FindOpenSlot();
                slotDevice.CurrentReserveStatus = OutSlotDevice.ReserveStatus.Permanent;
                slotDevice.PermanentType = OutContType.X360;
                OutputDevice outDevice = EstablishOutDevice(0, OutContType.X360);
                Xbox360OutDevice tempXbox = outDevice as Xbox360OutDevice;
                outputslotMan.DeferredPlugin(tempXbox, -1, outputDevices, OutContType.X360);
            }
            */

            /*slotDevice = outputslotMan.FindExistUnboundSlotType(OutContType.X360);
            if (slotDevice == null)
            {
                slotDevice = outputslotMan.FindOpenSlot();
                slotDevice.CurrentReserveStatus = OutSlotDevice.ReserveStatus.Permanent;
                slotDevice.DesiredType = OutContType.X360;
                OutputDevice outDevice = EstablishOutDevice(1, OutContType.X360);
                Xbox360OutDevice tempXbox = outDevice as Xbox360OutDevice;
                outputslotMan.DeferredPlugin(tempXbox, 1, outputDevices);
            }*/
        }

        private OutputDevice EstablishOutDevice(int index, OutContType contType)
        {
            contType = contType.Normalize();
            StartupDiag($"EstablishOutDevice begin index={index} contType={contType}");
            OutputDevice temp = outputslotMan.AllocateController(contType);
            StartupDiag($"EstablishOutDevice end index={index} contType={contType} result={temp?.GetType().Name ?? "null"}");
            return temp;
        }

        public void AttachNewUnboundOutDev(OutContType contType)
        {
            contType = contType.Normalize();
            OutSlotDevice slotDevice = outputslotMan.FindOpenSlot();
            if (slotDevice != null &&
                slotDevice.CurrentAttachedStatus == OutSlotDevice.AttachedStatus.UnAttached)
            {
                OutputDevice outDevice = EstablishOutDevice(-1, contType);
                outputslotMan.DeferredPlugin(outDevice, -1, "", outputDevices, contType);
            }
        }

        public void AttachUnboundOutDev(OutSlotDevice slotDevice, OutContType contType)
        {
            contType = contType.Normalize();
            if (slotDevice.CurrentAttachedStatus == OutSlotDevice.AttachedStatus.UnAttached &&
                slotDevice.CurrentInputBound == OutSlotDevice.InputBound.Unbound)
            {
                OutputDevice outDevice = EstablishOutDevice(-1, contType);
                outputslotMan.DeferredPlugin(outDevice, -1, "", outputDevices, contType);
            }
        }

        public void DetachUnboundOutDev(OutSlotDevice slotDevice)
        {
            if (slotDevice.CurrentInputBound == OutSlotDevice.InputBound.Unbound)
            {
                OutputDevice dev = slotDevice.OutputDevice;
                string tempType = dev.GetDeviceType();
                slotDevice.CurrentInputBound = OutSlotDevice.InputBound.Unbound;
                outputslotMan.DeferredRemoval(dev, -1, outputDevices, false);
            }
        }

        public void PluginOutDev(int index, DS4Device device,
            OutContType requestedContType = OutContType.None)
        {
            virtualOutputBlockReasons[index] = VirtualOutputBlockReason.None;
            OutContType contType = requestedContType == OutContType.None ?
                Global.OutContType[index].Normalize() :
                requestedContType.Normalize();
            if (requestedContType == OutContType.None)
            {
                Global.OutContType[index] = contType;
            }
            Global.outDevTypeTemp[index] = Global.outDevTypeTemp[index].Normalize();
            StartupDiag($"PluginOutDev enter index={index} contType={contType} useDInputOnly={useDInputOnly[index]} profileDInputOnly={getDInputOnly(index)}");

            bool profileDInputOnly = getDInputOnly(index);
            OutSlotDevice existingSlot = null;
            if (!profileDInputOnly)
            {
                existingSlot = outputslotMan.FindExistUnboundSlotType(contType);
                StartupDiag($"PluginOutDev existingSlot index={index} found={existingSlot != null} slot={(existingSlot != null ? existingSlot.Index + 1 : 0)}");
            }

            if (useDInputOnly[index])
            {
                if (!EnsureHidHideForVirtualOutput(index, device, contType))
                {
                    activeOutDevType[index] = OutContType.None;
                    virtualOutputBlockReasons[index] =
                        VirtualOutputBlockReason.PhysicalContainmentUnavailable;
                    StartupDiag($"PluginOutDev blocked index={index} reason=physical-containment-unavailable");
                    return;
                }

                bool success = false;
                OutSlotDevice slotDevice = null;
                OutSlotDevice candidateSlot = null;
                if (ViiperOutDevice.IsViiperType(contType))
                {
                    activeOutDevType[index] = contType;
                    candidateSlot = existingSlot ?? outputslotMan.FindOpenSlot();
                    bool bindingSucceeded = outputslotMan.TryBindInput(index,
                        $"{device.DisplayName} [{device.MacAddress}]", outputDevices,
                        contType, () => EstablishOutDevice(index, contType), out slotDevice,
                        allowPermanentSlotReuse: !profileDInputOnly);
                    if (!bindingSucceeded && candidateSlot != null)
                    {
                        slotDevice = candidateSlot;
                    }

                    success = bindingSucceeded || candidateSlot != null;
                    if (!success && candidateSlot == null)
                    {
                        LogDebug("Failed. No open output slot found");
                    }
                }

                if (success && slotDevice.OutputDevice != null)
                {
                    virtualOutputBlockReasons[index] =
                        VirtualOutputBlockReason.None;
                    LogDebug($"Associated input controller #{index + 1} ({device.DisplayName}) to virtual {slotDevice.CurrentType.ToDisplayName()} Controller in{(slotDevice.PermanentType != OutContType.None ? " permanent" : "")} output slot #{slotDevice.Index + 1}");
                    useDInputOnly[index] = false;
                    StartupDiag($"PluginOutDev success index={index} slot={slotDevice.Index + 1} output={slotDevice.OutputDevice.GetDeviceType()}");
                }
                else
                {
                    virtualOutputBlockReasons[index] = candidateSlot == null
                        ? VirtualOutputBlockReason.NoAvailableOutputSlot
                        : VirtualOutputBlockReason.OutputBindingFailed;
                    LogDebug("Failed. No output device was associated");
                    StartupDiag($"PluginOutDev failed index={index} success={success} slotNull={candidateSlot == null} slotOutputNull={candidateSlot?.OutputDevice == null}");
                }
            }
            else
            {
                StartupDiag($"PluginOutDev skipped index={index} useDInputOnly=false");
            }
        }

        public void UnplugOutDev(int index, DS4Device device, bool immediate = false, bool force = false)
        {
            if (!useDInputOnly[index])
            {
                try
                {
                    //OutContType contType = Global.OutContType[index];
                    OutputDevice dev = outputDevices[index];
                    OutSlotDevice slotDevice = outputslotMan.GetOutSlotDevice(dev);
                    if (dev != null && slotDevice != null)
                    {
                        string tempType = slotDevice.CurrentType.ToDisplayName();
                        LogDebug($"Disassociated virtual {tempType} Controller in{(slotDevice.CurrentReserveStatus == OutSlotDevice.ReserveStatus.Permanent ? " permanent" : "")} output slot #{slotDevice.Index + 1} from input controller #{index + 1} ({device.DisplayName})", false);

                        activeOutDevType[index] = OutContType.None;
                        outputslotMan.TryUnbindInput(dev, index, outputDevices, force);
                    }
                }
                finally
                {
                    outputDevices[index] = null;
                    activeOutDevType[index] = OutContType.None;
                    useDInputOnly[index] = true;
                    virtualOutputBlockReasons[index] =
                        VirtualOutputBlockReason.None;
                }
            }
        }

        public bool Start(bool showlog = true)
        {
            lock (serviceLifecycleLock)
            {
                if (running)
                {
                    StartupDiag("ControlService.Start ignored because the service is already running");
                    return true;
                }

                return StartCore(showlog);
            }
        }

        private bool StartCore(bool showlog)
        {
            StartupDiag($"ControlService.Start enter showlog={showlog} running={running} inServiceTask={inServiceTask} admin={Global.IsAdministrator()}");
            if (Global.hidHideInstalled)
            {
                // Restore externally owned containment before controller
                // discovery or any virtual-output work begins.
                GetHidHideOwnershipJournal();
            }
            inServiceTask = true;
            {
                // Initialize output KBM handler at start of ControlService
                StartupDiag("ControlService.Start before InitOutputKBMHandler");
                InitOutputKBMHandler();
                StartupDiag($"ControlService.Start after InitOutputKBMHandler handler={Global.outputKBMHandler?.GetFullDisplayName()}");

                if (showlog)
                    LogDebug(DS4WinWPF.Properties.Resources.Starting);

                Thread.Sleep(2000);

                bool runningAsAdmin = Global.IsAdministrator();
                if (!runningAsAdmin)
                {
                    LogDebug($"Keyboard and mouse output cannot reach a window that is running with administrator rights while {ProductIdentity.Name} is not. Restart as administrator if a game ignores mapped keys or mouse movement.");
                }

                LogDebug($"Using output KB+M handler: {Global.outputKBMHandler.GetFullDisplayName()}");
                LogDebug("VIIPER virtual-controller backend ready");

                DS4Devices.isExclusiveMode = getUseExclusiveMode(); //Re-enable Exclusive Mode

                StartupDiag($"UpdateHidHiddenAttributes begin exclusive={DS4Devices.isExclusiveMode}");
                UpdateHidHiddenAttributes();
                StartupDiag("UpdateHidHiddenAttributes end");

                if (Global.openRGBSyncEnabled)
                {
                    StartupDiag($"OpenRGB start begin port={Global.openRGBServerPort}");
                    bool openRGBStarted = OpenRGBServer.Instance.Start(Global.openRGBServerPort);
                    StartupDiag($"OpenRGB start end started={openRGBStarted}");
                    if (showlog)
                        LogDebug(openRGBStarted
                            ? $"OpenRGB server listening on port {Global.openRGBServerPort}"
                            : $"OpenRGB server could not bind to port {Global.openRGBServerPort} - lightbar will use profile colour");
                }

                if (showlog)
                {
                    LogDebug(DS4WinWPF.Properties.Resources.SearchingController);
                    LogDebug(DS4Devices.isExclusiveMode ? DS4WinWPF.Properties.Resources.UsingExclusive : DS4WinWPF.Properties.Resources.UsingShared);
                }

                if (isUsingOSCServer() && oscListener == null)
                {
                    StartupDiag("OSC listener start begin");
                    ChangeOSCListenerStatus(true);
                    StartupDiag("OSC listener start requested");
                }

                if (isUsingOSCSender() && oscSender == null)
                {
                    StartupDiag("OSC sender start begin");
                    ChangeOSCSenderStatus(true);
                    StartupDiag("OSC sender start requested");
                }

                if (isUsingUDPServer() && _udpServer == null)
                {
                    StartupDiag("UDP change-status start begin");
                    ChangeUDPStatus(true, false);
                    while (udpChangeStatus == true)
                    {
                        Thread.SpinWait(500);
                    }
                    StartupDiag("UDP change-status start end");
                }

                try
                {
                    loopControllers = true;
                    StartupDiag("AssignInitialDevices begin");
                    AssignInitialDevices();
                    StartupDiag("AssignInitialDevices end");

                    // A force-closed prior development build can leave its
                    // USB/IP output imported. Remove those ports before HID
                    // discovery or DS4Windows will ingest its own VIIPER DS4,
                    // create a second output/UAC endpoint, and recurse.
                    ViiperUsbipPortManager.DetachStaleLocalViiperPorts();
                    // Let usbccgp/HID finish publishing removal before the
                    // first input snapshot; otherwise a detached interface can
                    // remain enumerable for one final discovery pass.
                    Thread.Sleep(250);

                    StartupDiag("DS4Devices.findControllers dispatch begin");
                    eventDispatcher.Invoke(() =>
                    {
                        DS4Devices.findControllers();
                    });
                    StartupDiag("DS4Devices.findControllers dispatch end");

                    IEnumerable<DS4Device> devices = DS4Devices.getDS4Controllers();
                    int numControllers = devices.Count();
                    StartupDiag($"DS4Devices.getDS4Controllers count={numControllers}");
                    activeControllers = numControllers;
                    DS4LightBar.defaultLight = false;
                    int i = 0;
                    for (var devEnum = devices.GetEnumerator();
                        devEnum.MoveNext() && loopControllers; i++)
                    {
                        DS4Device device = devEnum.Current;
                        StartupDiag($"Prepare controller loop index={i} type={device.DeviceType} display={device.DisplayName} mac={device.MacAddress} conn={device.ConnectionType} synced={device.isSynced()} primary={device.PrimaryDevice}");

                        StartupDiag($"BeginPrepareConnectedInputController begin index={i}");
                        BeginPrepareConnectedInputController(device, showlog: true);
                        StartupDiag($"BeginPrepareConnectedInputController end index={i}");

                        DS4Controllers[i] = device;
                        device.DeviceSlotNumber = i;
                        StartupDiag($"PrepareConnectedInputControllerSettingEvents begin index={i}");
                        PrepareConnectedInputControllerSettingEvents(numControllers, device, index: i);
                        StartupDiag($"PrepareConnectedInputControllerSettingEvents end index={i}");

                        if (i >= CURRENT_DS4_CONTROLLER_LIMIT) // out of Xinput devices!
                            break;
                    }
                }
                catch (Exception e)
                {
                    StartupDiag($"ControlService.Start managed exception {e.GetType().Name}: {e.Message}");
                    LogDebug(e.Message, true);
                    AppLogger.LogToTray(e.Message, true);
                }

                StartupDiag("ControlService.Start setting running=true");
                running = true;
                StartGameBarStateTimer();

                if (_udpServer != null)
                {
                    //var UDP_SERVER_PORT = 26760;
                    var UDP_SERVER_PORT = Global.getUDPServerPortNum();
                    var UDP_SERVER_LISTEN_ADDRESS = Global.getUDPServerListenAddress();

                    try
                    {
                        StartupDiag($"UDP server Start begin address={UDP_SERVER_LISTEN_ADDRESS} port={UDP_SERVER_PORT}");
                        _udpServer.Start(UDP_SERVER_PORT, UDP_SERVER_LISTEN_ADDRESS);
                        LogDebug($"UDP server listening on address {UDP_SERVER_LISTEN_ADDRESS} port {UDP_SERVER_PORT}");
                        StartupDiag("UDP server Start end");
                    }
                    catch (System.Net.Sockets.SocketException ex)
                    {
                        StartupDiag($"UDP server Start exception {ex.SocketErrorCode}: {ex.Message}");
                        var errMsg = string.Format("Couldn't start UDP server on address {0}:{1}, outside applications won't be able to access pad data ({2})", UDP_SERVER_LISTEN_ADDRESS, UDP_SERVER_PORT, ex.SocketErrorCode);

                        LogDebug(errMsg, true);
                        AppLogger.LogToTray(errMsg, true, true);
                    }
                }
            }
            inServiceTask = false;
            runHotPlug = true;
            StartupDiag("ControlService.Start before ServiceStarted events");
            ServiceStarted?.Invoke(this, EventArgs.Empty);
            RunningChanged?.Invoke(this, EventArgs.Empty);
            StartupDiag("ControlService.Start after RunningChanged");
            ProcessPriorityClass appliedPriority =
                ManagedAudioLatencyLease.ApplyRequestedProcessPriority(
                    MainWindow.ProcessPriorityClasses[Global.ProcessPriority]);
            StartupDiag($"ControlService.Start exit priority={appliedPriority}");
            return true;
        }

        private void PrepareDevUDPMotion(DS4Device device, int index)
        {
            int tempIdx = index;
            DS4Device.ReportHandler<EventArgs> tempEvnt = (sender, args) =>
            {
                DualShockPadMeta padDetail = new DualShockPadMeta();
                GetPadDetailForIdx(tempIdx, ref padDetail);
                DS4State stateForUdp = TempState[tempIdx];

                CurrentState[tempIdx].CopyTo(stateForUdp);
                if (Global.IsUsingUDPServerSmoothing())
                {
                    if (stateForUdp.elapsedTime == 0)
                    {
                        // No timestamp was found. Exit out of routine
                        return;
                    }

                    double rate = 1.0 / stateForUdp.elapsedTime;
                    OneEuroFilter3D accelFilter = udpEuroPairAccel[tempIdx];
                    stateForUdp.Motion.accelXG = accelFilter.axis1Filter.Filter(stateForUdp.Motion.accelXG, rate);
                    stateForUdp.Motion.accelYG = accelFilter.axis2Filter.Filter(stateForUdp.Motion.accelYG, rate);
                    stateForUdp.Motion.accelZG = accelFilter.axis3Filter.Filter(stateForUdp.Motion.accelZG, rate);

                    OneEuroFilter3D gyroFilter = udpEuroPairGyro[tempIdx];
                    stateForUdp.Motion.angVelYaw = gyroFilter.axis1Filter.Filter(stateForUdp.Motion.angVelYaw, rate);
                    stateForUdp.Motion.angVelPitch = gyroFilter.axis2Filter.Filter(stateForUdp.Motion.angVelPitch, rate);
                    stateForUdp.Motion.angVelRoll = gyroFilter.axis3Filter.Filter(stateForUdp.Motion.angVelRoll, rate);
                }

                _udpServer?.NewReportIncoming(ref padDetail, stateForUdp, udpOutBuffers[tempIdx]);
            };

            device.MotionEvent = tempEvnt;
            device.Report += tempEvnt;
        }

        private void CheckQuickCharge(object sender, EventArgs e)
        {
            DS4Device device = sender as DS4Device;
            if (device.ConnectionType == ConnectionType.BT && getQuickCharge() &&
                device.Charging)
            {
                // Set disconnect flag here. Later Hotplug event will check
                // for presence of flag and remove the device then
                device.ReadyQuickChargeDisconnect = true;
            }
        }

        public void PrepareAbort()
        {
            for (int i = 0, arlength = DS4Controllers.Length; i < arlength; i++)
            {
                DS4Device tempDevice = DS4Controllers[i];
                if (tempDevice != null)
                {
                    tempDevice.PrepareAbort();
                }
            }
        }

        public bool Stop(bool showlog = true, bool immediateUnplug = false)
        {
            lock (serviceLifecycleLock)
            {
                return StopCore(showlog, immediateUnplug);
            }
        }

        private bool StopCore(bool showlog, bool immediateUnplug)
        {
            StartupDiag($"ControlService.Stop enter showlog={showlog} immediate={immediateUnplug} running={running}");
            if (running)
            {
                if (OpenRGBServer.Instance.IsRunning)
                {
                    StartupDiag("ControlService.Stop OpenRGB stop begin");
                    OpenRGBServer.Instance.Stop();
                    StartupDiag("ControlService.Stop OpenRGB stop end");
                }

                running = false;
                runHotPlug = false;
                inServiceTask = true;
                StopGameBarStateTimer();
                StopAllGameBarCompatibilityOutputs();
                StartupDiag("ControlService.Stop PreServiceStop begin");
                PreServiceStop?.Invoke(this, EventArgs.Empty);
                StartupDiag("ControlService.Stop PreServiceStop end");

                if (showlog)
                    LogDebug(DS4WinWPF.Properties.Resources.StoppingX360);

                LogDebug("Closing VIIPER virtual-controller connections");

                bool anyUnplugged = false;
                for (int i = 0, arlength = DS4Controllers.Length; i < arlength; i++)
                {
                    DS4Device tempDevice = DS4Controllers[i];
                    if (tempDevice != null)
                    {
                        StartupDiag($"ControlService.Stop controller loop index={i} display={tempDevice.DisplayName} mac={tempDevice.MacAddress} conn={tempDevice.ConnectionType} charging={tempDevice.isCharging()}");
                        if ((DCBTatStop && !tempDevice.isCharging()) || suspending)
                        {
                            if (tempDevice.getConnectionType() == ConnectionType.BT)
                            {
                                tempDevice.StopUpdate();
                                tempDevice.DisconnectBT(true);
                            }
                            else if (tempDevice.getConnectionType() == ConnectionType.SONYWA)
                            {
                                // Controller disconnect will complete on next attempted read.
                                // Do not use StopUpdate here
                                tempDevice.DisconnectDongle(true);
                            }
                            else
                            {
                                tempDevice.StopUpdate();
                            }
                        }
                        else
                        {
                            if (!immediateUnplug)
                            {
                                DS4LightBar.forcelight[i] = false;
                                DS4LightBar.forcedFlash[i] = 0;
                                DS4LightBar.defaultLight = true;
                                DS4LightBar.updateLightBar(DS4Controllers[i], i);
                            }

                            tempDevice.IsRemoved = true;
                            tempDevice.StopUpdate();
                            DS4Devices.RemoveDevice(tempDevice);
                            Thread.Sleep(50);
                        }

                        CurrentState[i].Battery = PreviousState[i].Battery = 0; // Reset for the next connection's initial status change.
                        OutputDevice tempout = outputDevices[i];
                        if (tempout != null)
                        {
                            StartupDiag($"ControlService.Stop UnplugOutDev begin index={i} type={tempout.GetDeviceType()}");
                            UnplugOutDev(i, tempDevice, immediate: immediateUnplug, force: true);
                            StartupDiag($"ControlService.Stop UnplugOutDev end index={i}");
                            anyUnplugged = true;
                        }

                        //outputDevices[i] = null;
                        //useDInputOnly[i] = true;
                        //Global.activeOutDevType[i] = OutContType.None;
                        useDInputOnly[i] = true;
                        DS4Controllers[i] = null;
                        oscState[i] = new DS4State();
                        touchPad[i] = null;
                        lag[i] = false;
                        inWarnMonitor[i] = false;
                    }
                }

                if (showlog)
                    LogDebug(DS4WinWPF.Properties.Resources.StoppingDS4);

                StartupDiag("ControlService.Stop DualShock4Audio reset begin");
                dualShock4AudioPassthrough.ResetForServiceStop();
                StartupDiag("ControlService.Stop DualShock4Audio reset end");
                StartupDiag("ControlService.Stop PlayStation feature outputs begin");
                StopAllPlayStationFeatureOutputs();
                StartupDiag("ControlService.Stop PlayStation feature outputs end");
                StartupDiag("ControlService.Stop DS4Devices.stopControllers begin");
                DS4Devices.stopControllers();
                StartupDiag("ControlService.Stop DS4Devices.stopControllers end");
                slotManager.ClearControllerList();

                if (oscListener != null)
                {
                    ChangeOSCListenerStatus(false);
                }

                if (oscSender != null)
                {
                    ChangeOSCSenderStatus(false);
                }

                if (_udpServer != null)
                {
                    StartupDiag("ControlService.Stop UDP stop begin");
                    ChangeUDPStatus(false);
                    StartupDiag("ControlService.Stop UDP stop requested");
                }

                if (showlog)
                    LogDebug(DS4WinWPF.Properties.Resources.StoppedDS4Windows);

                Stopwatch outputQueueWait = Stopwatch.StartNew();
                while (outputslotMan.RunningQueue && outputQueueWait.ElapsedMilliseconds < 2000)
                {
                    Thread.Sleep(1);
                }

                if (outputslotMan.RunningQueue)
                {
                    StartupDiag("ControlService.Stop timed out waiting for output slot queue");
                }

                StartupDiag("ControlService.Stop outputslotMan.Stop begin");
                outputslotMan.Stop(true);
                StartupDiag("ControlService.Stop outputslotMan.Stop end");

                if (anyUnplugged)
                {
                    Thread.Sleep(OutputSlotManager.DELAY_TIME);
                }

                // Disconnect from KBM system when stopping ControlService
                StartupDiag($"ControlService.Stop outputKBM Disconnect begin handler={outputKBMHandler?.GetFullDisplayName()}");
                LogDebug($"Closing connection to output handler {outputKBMHandler.GetDisplayName()}");
                outputKBMHandler.Disconnect();
                StartupDiag("ControlService.Stop outputKBM Disconnect end");
                inServiceTask = false;
                activeControllers = 0;
            }

            runHotPlug = false;
            // Release only entries for controllers managed by this service run after all
            // controller handles are closed. Unrelated HidHide entries remain untouched.
            // Start will reacquire hiding as each managed controller is discovered again.
            ReleaseHidHideManagedDevices();
            if (hidHideOwnershipJournal?.IsTransientRunInProgress == true &&
                !hidHideOwnershipJournal.MarkStoppedRunRecoveryRequired())
            {
                StartupDiag("HidHide recovery state could not be saved " +
                    "after controller handling stopped");
            }
            ResetControllerExposureSessionsForServiceStop();
            StartupDiag("ControlService.Stop before stopped events");
            ServiceStopped?.Invoke(this, EventArgs.Empty);
            RunningChanged?.Invoke(this, EventArgs.Empty);
            StartupDiag("ControlService.Stop exit");
            return !HidHideRecoveryRequired;
        }

        public bool HotPlug()
        {
            if (running)
            {
                inServiceTask = true;
                loopControllers = true;
                eventDispatcher.Invoke(() =>
                {
                    DS4Devices.findControllers();
                });

                IEnumerable<DS4Device> devices = DS4Devices.getDS4Controllers();
                int numControllers = devices.Count();
                activeControllers = numControllers;
                for (var devEnum = devices.GetEnumerator(); devEnum.MoveNext() && loopControllers;)
                {
                    DS4Device device = devEnum.Current;

                    if (device.isDisconnectingStatus())
                        continue;

                    // Use local method rather than Func
                    bool checkAlreadyExists()
                    {
                        for (int Index = 0, arlength = DS4Controllers.Length; Index < arlength; Index++)
                        {
                            if (DS4Controllers[Index] != null &&
                                DS4Controllers[Index].getMacAddress() == device.getMacAddress())
                            {
                                device.CheckControllerNumDeviceSettings(numControllers);
                                return true;
                            }
                        }

                        return false;
                    }

                    if (checkAlreadyExists())
                    {
                        continue;
                    }

                    int preferredSlot =
                        GetControllerExposurePreferredSlot(device);
                    for (int Index = 0, arlength = DS4Controllers.Length;
                        Index < arlength && Index < CURRENT_DS4_CONTROLLER_LIMIT; Index++)
                    {
                        if ((preferredSlot >= 0 && Index != preferredSlot) ||
                            IsControllerExposureSlotReserved(Index, device))
                        {
                            continue;
                        }

                        if (DS4Controllers[Index] == null)
                        {
                            BeginPrepareConnectedInputController(device);

                            DS4Controllers[Index] = device;
                            device.DeviceSlotNumber = Index;
                            PrepareConnectedInputControllerSettingEvents(numControllers, device, Index);

                            HotplugController?.Invoke(this, device, Index);
                            break;
                        }
                    }
                }

                inServiceTask = false;
            }

            return true;
        }

        private void PrepareConnectedInputControllerSettingEvents(int numControllers, DS4Device device, int index)
        {
            StartupDiag($"Controller prep begin index={index} numControllers={numControllers} display={device.DisplayName} mac={device.MacAddress} type={device.DeviceType}");
            StartupDiag($"RefreshExtrasButtons begin index={index}");
            Global.RefreshExtrasButtons(index, GetKnownExtraButtons(device));
            StartupDiag($"RefreshExtrasButtons end index={index}");
            StartupDiag($"LoadControllerConfigs begin index={index}");
            Global.LoadControllerConfigs(device);
            StartupDiag($"LoadControllerConfigs end index={index}");
            StartupDiag($"device.LoadStoreSettings begin index={index}");
            device.LoadStoreSettings();
            StartupDiag($"device.LoadStoreSettings end index={index}");
            StartupDiag($"CheckControllerNumDeviceSettings begin index={index}");
            device.CheckControllerNumDeviceSettings(numControllers);
            StartupDiag($"CheckControllerNumDeviceSettings end index={index}");

            slotManager.AddController(device, index);
            if (isUsingOSCSender())
            {
                oscSender.Send(new OscMessage("/ds4windows/monitor/" + index + "/plug", 1));
            }
            device.Removal += this.On_DS4Removal;
            device.Removal += DS4Devices.On_Removal;
            device.SyncChange += this.On_SyncChange;
            device.SyncChange += DS4Devices.UpdateSerial;
            device.SerialChange += this.On_SerialChange;
            device.ChargingChanged += CheckQuickCharge;

            StartupDiag($"TouchPad create begin index={index}");
            touchPad[index] = new Mouse(index, device);
            StartupDiag($"TouchPad create end index={index}");
            bool profileLoaded = false;
            bool useAutoProfile = useTempProfile[index];
            if (!useAutoProfile)
            {
                if (device.isValidSerial() && containsLinkedProfile(device.getMacAddress()))
                {
                    ProfilePath[index] = getLinkedProfile(device.getMacAddress());
                    Global.linkedProfileCheck[index] = true;
                }
                else
                {
                    ProfilePath[index] = OlderProfilePath[index];
                    Global.linkedProfileCheck[index] = false;
                }

                // Now attempt to load requested profile and settings
                StartupDiag($"LoadProfile begin index={index} profile=\"{ProfilePath[index]}\" linked={Global.linkedProfileCheck[index]}");
                profileLoaded = LoadProfile(index, false, this, false, false);
                StartupDiag($"LoadProfile end index={index} loaded={profileLoaded} profile=\"{ProfilePath[index]}\" dinputOnly={getDInputOnly(index)} outType={Global.OutContType[index]}");
            }
            else
            {
                StartupDiag($"LoadProfile skipped for auto/temp profile index={index} tempProfile=\"{tempprofilename[index]}\"");
            }

            if (profileLoaded || useAutoProfile)
            {
                device.LightBarColor = getMainColor(index);
                bool deferExposureProfileSetup =
                    ShouldDeferControllerExposureProfileSetup(index, device);
                RecordControllerExposureProfileReady(index, device,
                    profileLoaded || useAutoProfile);

                if (deferExposureProfileSetup)
                {
                    useDInputOnly[index] = true;
                    Global.activeOutDevType[index] = OutContType.None;
                }
                else if (!getDInputOnly(index) && device.isSynced())
                {
                    if (device.PrimaryDevice)
                    {
                        StartupDiag($"PluginOutDev begin index={index} outType={Global.OutContType[index]}");
                        PluginOutDev(index, device);
                        StartupDiag($"PluginOutDev end index={index} useDInputOnly={useDInputOnly[index]} activeOut={activeOutDevType[index]} outDev={outputDevices[index]?.GetDeviceType() ?? "null"}");
                    }
                    else if (device.JointDeviceSlotNumber != DS4Device.DEFAULT_JOINT_SLOT_NUMBER)
                    {
                        int otherIdx = device.JointDeviceSlotNumber;
                        OutputDevice tempOutDev = outputDevices[otherIdx];
                        if (tempOutDev != null)
                        {
                            OutContType tempConType = activeOutDevType[otherIdx];
                            outputDevices[index] = tempOutDev;
                            Global.activeOutDevType[index] = tempConType;
                        }
                    }
                }
                else
                {
                    useDInputOnly[index] = true;
                    Global.activeOutDevType[index] = OutContType.None;
                }

                if (!deferExposureProfileSetup && device.PrimaryDevice &&
                    device.OutputMapGyro)
                {
                    StartupDiag($"TouchPadOn begin index={index}");
                    TouchPadOn(index, device);
                    StartupDiag($"TouchPadOn end index={index}");
                }
                else if (!deferExposureProfileSetup &&
                    device.JointDeviceSlotNumber !=
                        DS4Device.DEFAULT_JOINT_SLOT_NUMBER)
                {
                    int otherIdx = device.JointDeviceSlotNumber;
                    DS4Device tempDev = DS4Controllers[otherIdx];
                    if (tempDev != null)
                    {
                        int mappedIdx = tempDev.PrimaryDevice ? otherIdx : index;
                        DS4Device gyroDev = device.OutputMapGyro ? device : (tempDev.OutputMapGyro ? tempDev : null);
                        if (gyroDev != null)
                        {
                            TouchPadOn(mappedIdx, gyroDev);
                        }
                    }
                }

                if (!deferExposureProfileSetup)
                {
                    StartupDiag($"CheckProfileOptions begin index={index}");
                    CheckProfileOptions(index, device);
                    StartupDiag($"CheckProfileOptions end index={index}");
                    StartupDiag($"SetupInitialHookEvents begin index={index}");
                    SetupInitialHookEvents(index, device);
                    StartupDiag($"SetupInitialHookEvents end index={index}");
                }
            }
            else
            {
                RecordControllerExposureProfileReady(index, device, false);
                StartupDiag($"Controller prep profile not loaded index={index} profile=\"{ProfilePath[index]}\"");
            }

            int tempIdx = index;
            device.Report += (sender, e) =>
            {
                this.On_Report(sender, e, tempIdx);
            };
            StartupDiag($"Report hook added index={index}");

            if (_udpServer != null && index < UdpServer.NUMBER_SLOTS)
            {
                StartupDiag($"PrepareDevUDPMotion begin index={index}");
                PrepareDevUDPMotion(device, tempIdx);
                StartupDiag($"PrepareDevUDPMotion end index={index}");
            }

            StartupDiag($"device.StartUpdate begin index={index}");
            device.StartUpdate();
            QueueSteamInputReclaim(device);
            StartupDiag($"device.StartUpdate end index={index}");
            StartupDiag($"Controller prep end index={index}");
        }

        private void BeginPrepareConnectedInputController(DS4Device device, bool showlog = false)
        {
            if (DS4Devices.isExclusiveMode && EnsureHidHideSessionForDevice(device))
            {
                ChangeExclusiveStatus(device);
            }
            else if (hidDeviceHidingEnabled && CheckAffected(device))
            {
                ChangeExclusiveStatus(device);
            }

            //Task task = new Task(() => { Thread.Sleep(5); WarnExclusiveModeFailure(device); });
            //task.Start();

            PrepareDS4DeviceSettingHooks(device);
        }

        public void ResetUdpSmoothingFilters(int idx)
        {
            if (idx < UdpServer.NUMBER_SLOTS)
            {
                OneEuroFilter3D temp = udpEuroPairAccel[idx] = new OneEuroFilter3D();
                temp.SetFilterAttrs(Global.UDPServerSmoothingMincutoff, Global.UDPServerSmoothingBeta);

                temp = udpEuroPairGyro[idx] = new OneEuroFilter3D();
                temp.SetFilterAttrs(Global.UDPServerSmoothingMincutoff, Global.UDPServerSmoothingBeta);
            }
        }

        private void ChangeUdpSmoothingAttrs(object sender, EventArgs e)
        {
            for (int i = 0; i < udpEuroPairAccel.Length; i++)
            {
                OneEuroFilter3D temp = udpEuroPairAccel[i];
                temp.SetFilterAttrs(Global.UDPServerSmoothingMincutoff, Global.UDPServerSmoothingBeta);
            }

            for (int i = 0; i < udpEuroPairGyro.Length; i++)
            {
                OneEuroFilter3D temp = udpEuroPairGyro[i];
                temp.SetFilterAttrs(Global.UDPServerSmoothingMincutoff, Global.UDPServerSmoothingBeta);
            }
        }

        /// <summary>
        /// Returns the VIIPER device that owns the Windows PlayStation audio
        /// endpoints for a physical controller. PlayStation personas use their
        /// game-visible composite device; Xbox and Switch personas use a
        /// persistent audio-only companion.
        /// </summary>
        internal ViiperOutDevice GetPlayStationFeatureOutput(int index)
        {
            if (index < 0 || index >= MAX_DS4_CONTROLLER_COUNT)
            {
                return null;
            }

            ViiperOutDevice primary = outputDevices[index] as ViiperOutDevice;
            if (primary != null &&
                PlayStationFeatureOutputPolicy.IsPlayStationAudioOutput(
                    primary.OutputType))
            {
                return primary;
            }

            lock (playStationFeatureOutputLock)
            {
                return playStationFeatureOutputDevices[index];
            }
        }

        internal OutContType GetPlayStationFeatureOutputType(int index)
        {
            return GetPlayStationFeatureOutput(index)?.OutputType ??
                OutContType.None;
        }

        private ViiperOutDevice EnsurePlayStationFeatureOutput(
            int index, DS4Device source)
        {
            ViiperOutDevice primary = outputDevices[index] as ViiperOutDevice;
            OutContType primaryType = primary?.OutputType ??
                Global.OutContType[index].Normalize();

            if (primary?.IsRuntimeConnected == true &&
                PlayStationFeatureOutputPolicy.IsPlayStationAudioOutput(
                    primaryType))
            {
                DisconnectPlayStationFeatureOutput(index);
                return primary;
            }

            OutContType desiredSidecar = primary?.IsRuntimeConnected == true
                ? PlayStationFeatureOutputPolicy.GetAudioOnlySidecarType(
                    source, primaryType, getDInputOnly(index))
                : OutContType.None;
            if (desiredSidecar == OutContType.None)
            {
                DisconnectPlayStationFeatureOutput(index);
                return null;
            }

            lock (playStationFeatureOutputLock)
            {
                ViiperOutDevice existing =
                    playStationFeatureOutputDevices[index];
                if (existing?.IsRuntimeConnected == true &&
                    existing.OutputType == desiredSidecar)
                {
                    existing.BindPhysicalController(index);
                    return existing;
                }

                if (existing != null)
                {
                    playStationFeatureOutputDevices[index] = null;
                    existing.Disconnect();
                }

                ViiperOutDevice sidecar = new ViiperOutDevice(
                    desiredSidecar,
                    PlayStationFeatureOutputPolicy.GetViiperType(
                        desiredSidecar),
                    audioOnlySidecar: true);
                try
                {
                    StartupDiag(
                        $"Persistent PlayStation audio owner connect begin index={index} type={desiredSidecar}");
                    sidecar.Connect();
                    sidecar.BindPhysicalController(index);
                    playStationFeatureOutputDevices[index] = sidecar;
                    StartupDiag(
                        $"Persistent PlayStation audio owner ready index={index} type={desiredSidecar} port={sidecar.DirectSpeakerUsbipPort}");
                    return sidecar;
                }
                catch (Exception ex)
                {
                    sidecar.Disconnect();
                    AppLogger.LogToGui(
                        $"Could not create the {desiredSidecar.ToDisplayName()} audio interface for controller #{index + 1}: {ex.Message}",
                        true);
                    StartupDiag(
                        $"PlayStation audio sidecar failed index={index} type={desiredSidecar} {ex.GetType().Name}: {ex.Message}");
                    return null;
                }
            }
        }

        private void DisconnectPlayStationFeatureOutput(int index)
        {
            ViiperOutDevice sidecar = null;
            lock (playStationFeatureOutputLock)
            {
                if (index >= 0 && index <
                    playStationFeatureOutputDevices.Length)
                {
                    sidecar = playStationFeatureOutputDevices[index];
                    playStationFeatureOutputDevices[index] = null;
                }
            }

            if (sidecar != null)
            {
                StartupDiag(
                    $"Persistent PlayStation audio owner disconnect index={index} type={sidecar.OutputType}");
                sidecar.Disconnect();
            }
        }

        private void StopAllPlayStationFeatureOutputs()
        {
            for (int index = 0; index <
                playStationFeatureOutputDevices.Length; index++)
            {
                DisconnectPlayStationFeatureOutput(index);
            }
        }

        public void CheckProfileOptions(int ind, DS4Device device, bool startUp = false)
        {
            ViiperOutDevice playStationFeatureOutput =
                EnsurePlayStationFeatureOutput(ind, device);
            OutContType playStationFeatureOutputType =
                playStationFeatureOutput?.OutputType ?? OutContType.None;

            device.ModifyFeatureSetFlag(VidPidFeatureSet.NoOutputData, !getEnableOutputDataToDS4(ind));
            if (!getEnableOutputDataToDS4(ind))
                LogDebug("Output data to DS4 disabled. Lightbar and rumble events are not written to DS4 gamepad. If the gamepad is connected over BT then IdleDisconnect option is recommended to let PureDS4 close the connection after long period of idling.");

            device.setIdleTimeout(getIdleDisconnectTimeout(ind));
            device.setBTPollRate(getBTPollRate(ind));

            touchPad[ind].ResetTrackAccel(getTrackballFriction(ind));
            touchPad[ind].ResetToggleGyroModes();

            //Global.TouchOutMode[ind] = TouchpadOutMode.MouseJoystick;
            touchPad[ind].PostSetup();

            if (Global.L2OutputSettings[ind].TrigEffectSettings.maxValue == 0)
            {
                Global.L2OutputSettings[ind].TrigEffectSettings.maxValue = (byte)(Math.Max(Global.L2ModInfo[ind].maxOutput, Global.L2ModInfo[ind].maxZone) / 100.0 * 255);
            }

            if (Global.R2OutputSettings[ind].TrigEffectSettings.maxValue == 0)
            {
                Global.R2OutputSettings[ind].TrigEffectSettings.maxValue = (byte)(Math.Max(Global.R2ModInfo[ind].maxOutput, Global.R2ModInfo[ind].maxZone) / 100.0 * 255);
            }

            device.PrepareTriggerEffect(InputDevices.TriggerId.LeftTrigger, Global.L2OutputSettings[ind].TriggerEffect,
                Global.L2OutputSettings[ind].TrigEffectSettings);
            device.PrepareTriggerEffect(InputDevices.TriggerId.RightTrigger, Global.R2OutputSettings[ind].TriggerEffect,
                Global.R2OutputSettings[ind].TrigEffectSettings);

            device.RumbleAutostopTime = getRumbleAutostopTime(ind);
            device.setRumble(0, 0);
            device.LightBarColor = Global.getMainColor(ind);

            bool speakerEnabled = IsControllerSpeakerEnabled(ind);
            string speakerCaptureEndpointId =
                GetControllerSpeakerCaptureEndpointId(ind);
            bool headsetOnlyAudio = IsControllerHeadsetOnlyAudio(ind);
            byte physicalSpeakerVolume = headsetOnlyAudio
                ? (byte)0
                : DualSenseSpeakerVolume[ind];
            bool useViiperControllerMicrophone =
                ControllerMicrophoneRoutePolicy.CanRouteDirectViiperMicrophone(
                    DualSenseEnableMicrophonePassthrough[ind], device,
                    playStationFeatureOutputType,
                    playStationFeatureOutput);
            // VIIPER opens the physical microphone only while a Windows
            // client is actively recording. Do not arm it during profile
            // load and consume Bluetooth bandwidth before that point.
            bool microphoneEnabled =
                ControllerMicrophoneRoutePolicy.ShouldArmPhysicalBluetoothMicrophone(
                    DualSenseEnableMicrophonePassthrough[ind], device,
                    playStationFeatureOutputType,
                    playStationFeatureOutput);
            bool audioConfigured = device.ConfigureBluetoothAudioForProfile(
                speakerEnabled,
                microphoneEnabled,
                physicalSpeakerVolume,
                DualSenseHeadphoneVolume[ind],
                useViiperControllerMicrophone ? byte.MaxValue :
                    DualSenseMicrophoneVolume[ind]);

            if (audioConfigured && speakerEnabled)
            {
                dualShock4AudioPassthrough.Start(ind, device,
                    physicalSpeakerVolume,
                    (DualSenseSpeakerCompression)Global.DualSenseSpeakerCompression[ind],
                    Global.DualSenseSpeakerBassBoost[ind],
                    speakerCaptureEndpointId,
                    playStationFeatureOutputType,
                    playStationFeatureOutput,
                    headsetOnlyAudio,
                    () => GetPlayStationFeatureOutput(ind));
            }
            else
            {
                dualShock4AudioPassthrough.Stop(ind);
            }

            if (!startUp)
            {
                CheckLauchProfileOption(ind, device);
            }
        }

        private static string GetControllerSpeakerCaptureEndpointId(int index) =>
            Global.DualSenseAudioCaptureEndpointId[index];

        private static bool IsControllerHeadsetOnlyAudio(int index) =>
            Global.DualSenseHeadsetOnlyAudio[index];

        private static bool IsControllerSpeakerEnabled(int index) =>
            Global.DualSenseEnableSpeakerOutput[index];

        internal static bool RequiresDualSenseBluetoothMediaCarrier(
            ConnectionType connectionType, bool speakerEnabled,
            OutContType outputType)
        {
            if (connectionType != ConnectionType.BT || speakerEnabled)
            {
                return false;
            }

            outputType = outputType.Normalize();
            return outputType == OutContType.ViiperDualSense ||
                outputType == OutContType.ViiperDualSenseEdge;
        }

        private void CheckLauchProfileOption(int ind, DS4Device device)
        {
            string programPath = LaunchProgram[ind];
            if (programPath != string.Empty)
            {
                Process[] localAll = Process.GetProcesses();
                bool procFound = false;
                for (int procInd = 0, procsLen = localAll.Length; !procFound && procInd < procsLen; procInd++)
                {
                    try
                    {
                        string temp = localAll[procInd].MainModule.FileName;
                        if (temp == programPath)
                        {
                            procFound = true;
                        }
                    }
                    // Ignore any process for which this information
                    // is not exposed
                    catch { }
                }

                if (!procFound)
                {
                    Task processTask = new Task(() =>
                    {
                        Thread.Sleep(5000);
                        Process tempProcess = new Process();
                        tempProcess.StartInfo.FileName = programPath;
                        tempProcess.StartInfo.WorkingDirectory = new FileInfo(programPath).Directory.ToString();
                        //tempProcess.StartInfo.UseShellExecute = false;
                        try { tempProcess.Start(); }
                        catch { }
                    });

                    processTask.Start();
                }
            }
        }

        private void SetupInitialHookEvents(int ind, DS4Device device)
        {
            ResetUdpSmoothingFilters(ind);

            // Set up filter for new input device
            OneEuroFilter tempFilter = new OneEuroFilter(OneEuroFilterPair.DEFAULT_WHEEL_CUTOFF,
                OneEuroFilterPair.DEFAULT_WHEEL_BETA);
            Mapping.wheelFilters[ind] = tempFilter;

            // Carry over initial profile wheel smoothing values to filter instances.
            // Set up event hooks to keep values in sync
            SteeringWheelSmoothingInfo wheelSmoothInfo = WheelSmoothInfo[ind];
            wheelSmoothInfo.SetFilterAttrs(tempFilter);
            wheelSmoothInfo.SetRefreshEvents(tempFilter);

            FlickStickSettings flickStickSettings = Global.LSOutputSettings[ind].outputSettings.flickSettings;
            flickStickSettings.RemoveRefreshEvents();
            flickStickSettings.SetRefreshEvents(Mapping.flickMappingData[ind].flickFilter);

            flickStickSettings = Global.RSOutputSettings[ind].outputSettings.flickSettings;
            flickStickSettings.RemoveRefreshEvents();
            flickStickSettings.SetRefreshEvents(Mapping.flickMappingData[ind].flickFilter);

            int tempIdx = ind;
            Global.L2OutputSettings[ind].ResetEvents();
            Global.L2ModInfo[ind].ResetEvents();
            Global.L2OutputSettings[ind].TriggerEffectChanged += (sender, e) =>
            {
                device.PrepareTriggerEffect(InputDevices.TriggerId.LeftTrigger, Global.L2OutputSettings[tempIdx].TriggerEffect,
                    Global.L2OutputSettings[tempIdx].TrigEffectSettings);
            };
            Global.L2ModInfo[ind].MaxOutputChanged += (sender, e) =>
            {
                TriggerDeadZoneZInfo tempInfo = sender as TriggerDeadZoneZInfo;
                L2OutputSettings[tempIdx].TrigEffectSettings.maxValue = (byte)(Math.Max(tempInfo.maxOutput, tempInfo.maxZone) / 100.0 * 255.0);

                // Refresh trigger effect
                device.PrepareTriggerEffect(InputDevices.TriggerId.LeftTrigger, Global.L2OutputSettings[tempIdx].TriggerEffect,
                    Global.L2OutputSettings[tempIdx].TrigEffectSettings);
            };
            Global.L2ModInfo[ind].MaxZoneChanged += (sender, e) =>
            {
                TriggerDeadZoneZInfo tempInfo = sender as TriggerDeadZoneZInfo;
                L2OutputSettings[tempIdx].TrigEffectSettings.maxValue = (byte)(Math.Max(tempInfo.maxOutput, tempInfo.maxZone) / 100.0 * 255.0);

                // Refresh trigger effect
                device.PrepareTriggerEffect(InputDevices.TriggerId.LeftTrigger, Global.L2OutputSettings[tempIdx].TriggerEffect,
                    Global.L2OutputSettings[tempIdx].TrigEffectSettings);
            };

            Global.R2OutputSettings[ind].ResetEvents();
            Global.R2OutputSettings[ind].TriggerEffectChanged += (sender, e) =>
            {
                device.PrepareTriggerEffect(InputDevices.TriggerId.RightTrigger, Global.R2OutputSettings[tempIdx].TriggerEffect,
                    Global.R2OutputSettings[tempIdx].TrigEffectSettings);
            };
            Global.R2ModInfo[ind].MaxOutputChanged += (sender, e) =>
            {
                TriggerDeadZoneZInfo tempInfo = sender as TriggerDeadZoneZInfo;
                R2OutputSettings[tempIdx].TrigEffectSettings.maxValue = (byte)(tempInfo.maxOutput / 100.0 * 255.0);

                // Refresh trigger effect
                device.PrepareTriggerEffect(InputDevices.TriggerId.RightTrigger, Global.R2OutputSettings[tempIdx].TriggerEffect,
                    Global.R2OutputSettings[tempIdx].TrigEffectSettings);
            };
            Global.R2ModInfo[ind].MaxZoneChanged += (sender, e) =>
            {
                TriggerDeadZoneZInfo tempInfo = sender as TriggerDeadZoneZInfo;
                R2OutputSettings[tempIdx].TrigEffectSettings.maxValue = (byte)(tempInfo.maxOutput / 100.0 * 255.0);

                // Refresh trigger effect
                device.PrepareTriggerEffect(InputDevices.TriggerId.RightTrigger, Global.R2OutputSettings[tempIdx].TriggerEffect,
                    Global.R2OutputSettings[tempIdx].TrigEffectSettings);
            };
        }

        /// <summary>
        /// Perform Mapping property resetting as needed before loading profile settings
        /// </summary>
        /// <param name="device">Input device instance</param>
        public void PreLoadReset(int ind)
        {
            //DS4Device inputDevice = DS4Controllers[ind];
            //if (inputDevice == null)
            //{
            //    return;
            //}
            // Skip running for test profile with no mapping data
            if (ind >= Global.TEST_PROFILE_INDEX)
            {
                return;
            }

            // Reset current flick stick progress from previous profile
            Mapping.flickMappingData[ind].Reset();

            // Reset delta accel processors for sticks
            Mapping.deltaAccelProcessors[ind].LSProcessor.Reset();
            Mapping.deltaAccelProcessors[ind].RSProcessor.Reset();

            // Reset absolute mouse state data
            Mapping.absMouseOutputState[ind].Reset();

            // Reset some elements of current Mouse instance
            touchPad[ind]?.Reset();
        }

        public void TouchPadOn(int ind, DS4Device device)
        {
            Mouse tPad = touchPad[ind];
            //ITouchpadBehaviour tPad = touchPad[ind];
            device.Touchpad.TouchButtonDown += tPad.touchButtonDown;
            device.Touchpad.TouchButtonUp += tPad.touchButtonUp;
            device.Touchpad.TouchesBegan += tPad.touchesBegan;
            device.Touchpad.TouchesBegan += tPad.TouchStartedOrEnded;
            device.Touchpad.TouchesMoved += tPad.touchesMoved;
            device.Touchpad.TouchesEnded += tPad.touchesEnded;
            device.Touchpad.TouchesEnded += tPad.TouchStartedOrEnded;
            device.Touchpad.TouchUnchanged += tPad.touchUnchanged;
            //device.Touchpad.PreTouchProcess += delegate { touchPad[ind].populatePriorButtonStates(); };
            device.Touchpad.PreTouchProcess += (sender, args) => { touchPad[ind].populatePriorButtonStates(); };
            device.SixAxis.SixAccelMoved += tPad.sixaxisMoved;
            //LogDebug("Touchpad mode for " + device.MacAddress + " is now " + tmode.ToString());
            //Log.LogToTray("Touchpad mode for " + device.MacAddress + " is now " + tmode.ToString());
        }

        public string GetDS4Battery(int index)
        {
            DS4Device d = DS4Controllers[index];
            if (d != null)
            {
                if (!d.IsAlive())
                    return "...";

                if (d.DeviceType == InputDevices.InputDeviceType.DS4)
                {
                    DS4BatteryPresentation presentation = d.BatteryPresentation;
                    return presentation.Status switch
                    {
                        DS4BatteryStatus.Charging => "Charging",
                        DS4BatteryStatus.Full => DS4WinWPF.Properties.Resources.Full,
                        DS4BatteryStatus.ChargingUnavailable => "Charging unavailable",
                        DS4BatteryStatus.ChargingError => "Charging error",
                        _ when presentation.IsSettling || !presentation.HasCapacity => "...",
                        _ => $"~{presentation.Capacity}%",
                    };
                }

                if (d.isCharging())
                {
                    if (d.getBattery() >= 100)
                        return DS4WinWPF.Properties.Resources.Full;

                    return d.getBattery() + "%+";
                }

                return d.getBattery() + "%";
            }
            else
                return DS4WinWPF.Properties.Resources.NA;
        }

        protected void On_SerialChange(object sender, EventArgs e)
        {
            DS4Device device = (DS4Device)sender;
            int ind = -1;
            for (int i = 0, arlength = MAX_DS4_CONTROLLER_COUNT; ind == -1 && i < arlength; i++)
            {
                DS4Device tempDev = DS4Controllers[i];
                if (tempDev != null && device == tempDev)
                    ind = i;
            }

            if (ind >= 0)
            {
                OnDeviceSerialChange(this, ind, device.getMacAddress());
            }
        }

        protected void On_SyncChange(object sender, EventArgs e)
        {
            DS4Device device = (DS4Device)sender;
            int ind = -1;
            for (int i = 0, arlength = CURRENT_DS4_CONTROLLER_LIMIT; ind == -1 && i < arlength; i++)
            {
                DS4Device tempDev = DS4Controllers[i];
                if (tempDev != null && device == tempDev)
                    ind = i;
            }

            if (ind >= 0)
            {
                bool synced = device.isSynced();

                if (!synced)
                {
                    if (!useDInputOnly[ind])
                    {
                        Global.activeOutDevType[ind] = OutContType.None;
                        UnplugOutDev(ind, device);
                    }
                }
                else
                {
                    if (!getDInputOnly(ind))
                    {
                        touchPad[ind].ReplaceOneEuroFilterPair();
                        //touchPad[ind].ReplaceOneEuroFilterPair();

                        touchPad[ind].Cursor.ReplaceOneEuroFilterPair();
                        touchPad[ind].Cursor.SetupLateOneEuroFilters();
                        PluginOutDev(ind, device);
                    }
                }
            }
        }

        // Called when DS4 is disconnected or timed out
        protected void On_DS4Removal(object sender, EventArgs e)
        {
            DS4Device device = (DS4Device)sender;
            int ind = -1;
            for (int i = 0, arlength = DS4Controllers.Length; ind == -1 && i < arlength; i++)
            {
                if (DS4Controllers[i] != null && device.getMacAddress() == DS4Controllers[i].getMacAddress())
                    ind = i;
            }

            if (ind != -1)
            {
                bool removingStatus = false;
                lock (device.removeLocker)
                {
                    if (!device.IsRemoving)
                    {
                        removingStatus = true;
                        device.IsRemoving = true;
                    }
                }

                if (removingStatus)
                {
                    bool exposureRelease =
                        controllerExposureTransitions[ind].Status.Stage ==
                        ControllerExposureStage.ReleasingPhysicalHandle;
                    if (exposureRelease)
                    {
                        StartupDiag($"Controller exposure removal state cleanup begin index={ind}");
                    }

                    CurrentState[ind].Battery = PreviousState[ind].Battery = 0; // Reset for the next connection's initial status change.
                    if (!exposureRelease)
                    {
                        DeactivateGameBarCompatibilityOutput(ind);
                        if (!useDInputOnly[ind])
                        {
                            UnplugOutDev(ind, device);
                        }
                        else if (!device.PrimaryDevice)
                        {
                            OutputDevice outDev = outputDevices[ind];
                            if (outDev != null)
                            {
                                outDev.RemoveFeedback(ind);
                                outputDevices[ind] = null;
                            }
                        }

                        // Use Task to reset device synth state and commit it
                        Task.Run(() =>
                        {
                            Mapping.Commit(ind);
                        }).Wait();
                    }

                    string removed = DS4WinWPF.Properties.Resources.ControllerWasRemoved.Replace("*Mac address*", (ind + 1).ToString());
                    if (!exposureRelease && device.getBattery() <= 20 &&
                        device.getConnectionType() == ConnectionType.BT && !device.isCharging())
                    {
                        removed += ". " + DS4WinWPF.Properties.Resources.ChargeController;
                    }

                    if (!exposureRelease)
                    {
                        LogDebug(removed);
                        AppLogger.LogToTray(removed);
                    }
                    if (!exposureRelease)
                    {
                        dualShock4AudioPassthrough.Stop(ind);
                        DisconnectPlayStationFeatureOutput(ind);
                    }
                    /*Stopwatch sw = new Stopwatch();
                    sw.Start();
                    while (sw.ElapsedMilliseconds < XINPUT_UNPLUG_SETTLE_TIME)
                    {
                        // Use SpinWait to keep control of current thread. Using Sleep could potentially
                        // cause other events to get run out of order
                        System.Threading.Thread.SpinWait(500);
                    }
                    sw.Stop();
                    */

                    device.IsRemoved = true;
                    device.Synced = false;
                    DS4Controllers[ind] = null;
                    oscState[ind] = new DS4State();
                    //eventDispatcher.Invoke(() =>
                    //{
                    slotManager.RemoveController(device, ind);
                    if (isUsingOSCSender())
                    {
                        oscSender.Send(new SharpOSC.OscMessage("/ds4windows/monitor/" + ind + "/plug", 0));
                    }
                    //});

                    touchPad[ind] = null;
                    lag[ind] = false;
                    inWarnMonitor[ind] = false;
                    useDInputOnly[ind] = true;
                    Global.activeOutDevType[ind] = OutContType.None;
                    if (exposureRelease)
                    {
                        StartupDiag($"Controller exposure removal state cleanup end index={ind}");
                    }
                    /* Leave up to Auto Profile system to change the following flags? */
                    //Global.useTempProfile[ind] = false;
                    //Global.tempprofilename[ind] = string.Empty;
                    //Global.tempprofileDistance[ind] = false;

                    //Thread.Sleep(XINPUT_UNPLUG_SETTLE_TIME);
                }
            }
        }

        public bool[] lag = new bool[MAX_DS4_CONTROLLER_COUNT] { false, false, false, false, false, false, false, false };
        public bool[] inWarnMonitor = new bool[MAX_DS4_CONTROLLER_COUNT] { false, false, false, false, false, false, false, false };
        private byte[] currentBattery = new byte[MAX_DS4_CONTROLLER_COUNT] { 0, 0, 0, 0, 0, 0, 0, 0 };
        private bool[] charging = new bool[MAX_DS4_CONTROLLER_COUNT] { false, false, false, false, false, false, false, false };
        private string[] tempStrings = new string[MAX_DS4_CONTROLLER_COUNT] { string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty };
        private DateTime[] gameBarHomeButtonIgnoreUntilUtc = new DateTime[MAX_DS4_CONTROLLER_COUNT];
        private readonly OutputDevice[] gameBarCompatibilityOutputDevices = new OutputDevice[MAX_DS4_CONTROLLER_COUNT];
        private readonly int[] gameBarCompatibilityRoutingActive = new int[MAX_DS4_CONTROLLER_COUNT];
        private readonly DateTime[] gameBarCompatibilityNextRetryUtc = new DateTime[MAX_DS4_CONTROLLER_COUNT];
        private readonly long[] gameBarCompatibilityPrewarmUntilTicks = new long[MAX_DS4_CONTROLLER_COUNT];
        private readonly object gameBarCompatibilityOutputLock = new object();

        private DateTime gameBarLastVisibleUtc = DateTime.MinValue;
        private DateTime gameBarLastVisibilityCheckUtc = DateTime.MinValue;
        private bool gameBarVerboseDetectionLogInitialized = false;
        private bool gameBarVerboseLastVisible = false;
        private DateTime gameBarVerboseLastDetectionLogUtc = DateTime.MinValue;
        private bool[] dualSenseMuteLedOn = new bool[MAX_DS4_CONTROLLER_COUNT] { false, false, false, false, false, false, false, false };
        private bool[] dualSenseMuteProfilePending = new bool[MAX_DS4_CONTROLLER_COUNT] { false, false, false, false, false, false, false, false };
        private string[] dualSenseMuteRequestedProfileName = new string[MAX_DS4_CONTROLLER_COUNT] { string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty };

        public ControllerRuntimeSignals GetControllerRuntimeSignals(int index)
        {
            if (index < 0 || index >= CURRENT_DS4_CONTROLLER_LIMIT)
            {
                return new ControllerRuntimeSignals(false, false, false,
                    false, false, false,
                    ControllerRuntimeLaneState.NotRequired,
                    ControllerRuntimeLaneState.NotRequired,
                    ControllerRuntimeLaneState.NotRequired,
                    "virtual controller");
            }

            DS4Device device = DS4Controllers[index];
            ControllerExposureStatus exposureStatus =
                controllerExposureTransitions[index].Status;
            bool physicalPresent = device != null && !device.IsRemoving;
            bool physicalSynced = physicalPresent && device.isSynced();
            bool physicalAlive = physicalSynced && device.IsAlive();
            bool virtualRequired = !Global.getDInputOnly(index);
            OutContType desiredType = Global.OutContType[index].Normalize();
            ViiperOutDevice viiperOutput = outputDevices[index] as ViiperOutDevice;
            ViiperOutDevice playStationFeatureOutput =
                GetPlayStationFeatureOutput(index);
            OutContType playStationFeatureOutputType =
                playStationFeatureOutput?.OutputType ?? OutContType.None;
            bool virtualConnected = !virtualRequired ||
                viiperOutput?.IsRuntimeConnected == true;
            bool virtualTypeMatches = !virtualRequired ||
                Global.activeOutDevType[index].Normalize() == desiredType;
            VirtualOutputBlockReason virtualOutputBlockReason =
                virtualRequired && !virtualConnected
                    ? virtualOutputBlockReasons[index]
                    : VirtualOutputBlockReason.None;
            string activeVirtualControllerName = virtualConnected
                ? Global.activeOutDevType[index].Normalize().ToDisplayName()
                : string.Empty;

            bool advancedHapticsRequired = virtualRequired &&
                (desiredType == OutContType.ViiperDualSense ||
                    desiredType == OutContType.ViiperDualSenseEdge);
            ViiperOutDevice advancedHapticsOutput =
                playStationFeatureOutput ?? viiperOutput;
            ControllerRuntimeLaneState advancedHaptics =
                !advancedHapticsRequired
                    ? ControllerRuntimeLaneState.NotRequired
                    : advancedHapticsOutput?.SupportsAtomicAudioHaptics == true
                        ? ControllerRuntimeLaneState.Ready
                        : virtualConnected
                            ? ControllerRuntimeLaneState.Unavailable
                            : ControllerRuntimeLaneState.Starting;

            bool speakerRequired = physicalPresent &&
                IsControllerSpeakerEnabled(index);
            ControllerRuntimeLaneState speaker =
                ControllerRuntimeLaneState.NotRequired;
            if (speakerRequired)
            {
                speaker = dualShock4AudioPassthrough.GetStatus(index);
            }

            bool microphoneRequired = physicalPresent &&
                Global.DualSenseEnableMicrophonePassthrough[index];
            ControllerRuntimeLaneState microphone =
                ControllerRuntimeLaneState.NotRequired;
            if (microphoneRequired)
            {
                bool directMicrophone =
                    ControllerMicrophoneRoutePolicy.CanRouteDirectViiperMicrophone(
                        true, device, playStationFeatureOutputType,
                        playStationFeatureOutput);
                if (directMicrophone)
                {
                    microphone = playStationFeatureOutput?
                        .SupportsActiveVirtualMicrophone == true
                        ? ControllerRuntimeLaneState.Ready
                        : playStationFeatureOutput?.IsRuntimeConnected == true
                            ? ControllerRuntimeLaneState.Unavailable
                            : ControllerRuntimeLaneState.Starting;
                }
                else
                {
                    microphone = ControllerRuntimeLaneState.Unavailable;
                }
            }

            return new ControllerRuntimeSignals(physicalPresent,
                physicalSynced, physicalAlive, virtualRequired,
                virtualConnected, virtualTypeMatches, advancedHaptics,
                speaker, microphone,
                desiredType.ToDisplayName(), exposureStatus.Mode,
                exposureStatus.Stage, virtualOutputBlockReason,
                activeVirtualControllerName);
        }

        internal static bool ShouldUseGameBarControllerCompatibility(bool enabled,
            OutContType outputType, bool dInputOnly)
        {
            return enabled && !dInputOnly &&
                (outputType == OutContType.ViiperDualSense ||
                outputType == OutContType.ViiperDualSenseEdge ||
                outputType == OutContType.ViiperDS4);
        }

        internal static bool ShouldRetireGameBarCompatibilityBeforeProfileChange(
            bool routeActive, bool enabled, OutContType requestedOutputType,
            bool requestedDInputOnly)
        {
            return routeActive &&
                !ShouldUseGameBarControllerCompatibility(enabled,
                    requestedOutputType.Normalize(), requestedDInputOnly);
        }

        /// <summary>
        /// Reconciles the temporary XInput route with a profile's requested
        /// output before that profile unplugs or creates its native device.
        /// Waiting for the periodic Game Bar visibility poll leaves a window
        /// where reports still target the old companion while a new native
        /// Xbox pad is already visible. Game Bar can bind that stale pad and
        /// then lose all input when the timer eventually removes it.
        /// </summary>
        internal void PrepareGameBarCompatibilityProfileTransition(int index,
            OutContType requestedOutputType, bool requestedDInputOnly)
        {
            if (index < 0 || index >= MAX_DS4_CONTROLLER_COUNT)
            {
                return;
            }

            lock (gameBarCompatibilityOutputLock)
            {
                bool routeActive = Volatile.Read(
                    ref gameBarCompatibilityRoutingActive[index]) == 1;
                if (!ShouldRetireGameBarCompatibilityBeforeProfileChange(
                        routeActive,
                        Global.GameBarControllerCompatibility[index],
                        requestedOutputType, requestedDInputOnly))
                {
                    return;
                }

                Interlocked.Exchange(
                    ref gameBarCompatibilityPrewarmUntilTicks[index], 0);
                DeactivateGameBarCompatibilityOutputCore(index);
                StartupDiag(
                    $"GameBar compatibility retired before profile output transition controller={index + 1} requested={requestedOutputType.Normalize()}");
            }
        }

        private bool HasAnyConfiguredGameBarCompatibility()
        {
            for (int i = 0; i < MAX_DS4_CONTROLLER_COUNT; i++)
            {
                if (DS4Controllers[i] != null &&
                    ShouldUseGameBarControllerCompatibility(
                        Global.GameBarControllerCompatibility[i],
                        Global.OutContType[i], getDInputOnly(i)))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsAnyGameBarCompatibilityActive()
        {
            for (int i = 0; i < MAX_DS4_CONTROLLER_COUNT; i++)
            {
                if (Volatile.Read(ref gameBarCompatibilityRoutingActive[i]) == 1)
                {
                    return true;
                }
            }

            return false;
        }

        private OutputDevice GetReportOutputDevice(int index)
        {
            // The companion pointer is published before routing becomes active
            // and routing is disabled before the pointer is withdrawn. This
            // keeps the report path valid throughout VIIPER's comparatively
            // slow USB/IP plug and unplug operations.
            if (Volatile.Read(ref gameBarCompatibilityRoutingActive[index]) == 1)
            {
                OutputDevice compatibilityOutput = Volatile.Read(
                    ref gameBarCompatibilityOutputDevices[index]);
                if (compatibilityOutput != null)
                {
                    return compatibilityOutput;
                }
            }

            return outputDevices[index];
        }

        private void CheckGameBarHomeButton(int ind, DS4State cState, DS4State tempControlState, DS4State pState)
        {
            if (!cState.PS)
            {
                return;
            }

            DateTime now = DateTime.UtcNow;
            if (now < gameBarHomeButtonIgnoreUntilUtc[ind])
            {
                cState.PS = false;
                tempControlState.PS = false;
                return;
            }

            if (pState.PS)
            {
                return;
            }

            if (ShouldUseGameBarControllerCompatibility(
                Global.GameBarControllerCompatibility[ind],
                Global.OutContType[ind], getDInputOnly(ind)))
            {
                cState.PS = false;
                tempControlState.PS = false;
                gameBarHomeButtonIgnoreUntilUtc[ind] = now + TimeSpan.FromSeconds(1);
                // A USB/IP attach is slow enough to make the first Game Bar
                // interaction visibly hitch. Prewarm off the controller report
                // thread, then open the overlay only after XInput is available.
                Interlocked.Exchange(
                    ref gameBarCompatibilityPrewarmUntilTicks[ind],
                    Environment.TickCount64 + 2000);
                _ = Task.Run(() =>
                {
                    ActivateGameBarCompatibilityOutput(ind);
                    string openResult = gameBarIntegration.OpenGameBar();
                    StartupDiag($"GameBar compatibility home button controller={ind + 1} {openResult}");
                });
                return;
            }

            // Profiles that do not request the modern compatibility route use
            // their normal Home mapping. There is deliberately no legacy
            // profile-switch fallback here.
        }

        private void UpdateGameBarCompatibilityOutputs(bool gameBarVisible)
        {
            long nowTicks = Environment.TickCount64;
            for (int i = 0; i < MAX_DS4_CONTROLLER_COUNT; i++)
            {
                if (gameBarVisible)
                {
                    // Once visibility is confirmed, the overlay owns the route.
                    // Closing it can then remove the companion immediately.
                    Interlocked.Exchange(
                        ref gameBarCompatibilityPrewarmUntilTicks[i], 0);
                }

                bool shouldRoute = ShouldKeepGameBarCompatibilityRoute(
                        gameBarVisible, nowTicks,
                        Interlocked.Read(
                            ref gameBarCompatibilityPrewarmUntilTicks[i])) &&
                    DS4Controllers[i] != null &&
                    ShouldUseGameBarControllerCompatibility(
                        Global.GameBarControllerCompatibility[i],
                        Global.OutContType[i], getDInputOnly(i));
                if (shouldRoute)
                {
                    ActivateGameBarCompatibilityOutput(i);
                }
                else
                {
                    DeactivateGameBarCompatibilityOutput(i);
                }
            }
        }

        internal static bool ShouldKeepGameBarCompatibilityRoute(
            bool gameBarVisible, long nowTicks, long prewarmUntilTicks)
        {
            return gameBarVisible || nowTicks < prewarmUntilTicks;
        }

        private void ActivateGameBarCompatibilityOutput(int index)
        {
            lock (gameBarCompatibilityOutputLock)
            {
                ActivateGameBarCompatibilityOutputCore(index);
            }
        }

        private void ActivateGameBarCompatibilityOutputCore(int index)
        {
            if (!running ||
                Volatile.Read(ref gameBarCompatibilityRoutingActive[index]) == 1 ||
                DateTime.UtcNow < gameBarCompatibilityNextRetryUtc[index])
            {
                return;
            }

            DS4Device source = DS4Controllers[index];
            OutputDevice nativeOutput = outputDevices[index];
            if (source == null || nativeOutput == null)
            {
                return;
            }

            if (outputslotMan.FindOpenSlot() == null)
            {
                gameBarCompatibilityNextRetryUtc[index] =
                    DateTime.UtcNow + TimeSpan.FromSeconds(2);
                StartupDiag($"GameBar compatibility activation delayed controller={index + 1} reason=no-output-slot");
                return;
            }

            OutputDevice compatibilityOutput = null;
            try
            {
                compatibilityOutput = EstablishOutDevice(index, OutContType.ViiperX360);
                if (compatibilityOutput == null)
                {
                    throw new InvalidOperationException(
                        "Could not create the temporary XInput output.");
                }

                outputslotMan.DeferredPlugin(compatibilityOutput, -1,
                    $"Game Bar compatibility for controller {index + 1}",
                    outputDevices, OutContType.ViiperX360);
                if (outputslotMan.GetOutSlotDevice(compatibilityOutput) == null)
                {
                    throw new InvalidOperationException(
                        "The temporary XInput output was not assigned to a slot.");
                }

                Interlocked.Exchange(
                    ref gameBarCompatibilityOutputDevices[index], compatibilityOutput);
                // Commit routing only after the companion is fully connected
                // and published. The native output continues receiving reports
                // during the whole USB/IP creation interval.
                Interlocked.Exchange(ref gameBarCompatibilityRoutingActive[index], 1);
                try
                {
                    nativeOutput.ResetState();
                }
                catch (Exception resetEx)
                {
                    // The companion is already live. A native neutral-report
                    // failure must not roll back or tear down the valid route.
                    StartupDiag($"GameBar compatibility native reset failed controller={index + 1} {resetEx.GetType().Name}: {resetEx.Message}");
                }
                gameBarCompatibilityNextRetryUtc[index] = DateTime.MinValue;
                StartupDiag($"GameBar compatibility activated controller={index + 1} native={Global.OutContType[index]} companion=X360");
            }
            catch (Exception ex)
            {
                if (compatibilityOutput != null &&
                    outputslotMan.GetOutSlotDevice(compatibilityOutput) != null)
                {
                    outputslotMan.DeferredRemoval(compatibilityOutput, -1,
                        outputDevices, true);
                }

                Interlocked.Exchange(
                    ref gameBarCompatibilityOutputDevices[index], null);
                Interlocked.Exchange(ref gameBarCompatibilityRoutingActive[index], 0);
                gameBarCompatibilityNextRetryUtc[index] =
                    DateTime.UtcNow + TimeSpan.FromSeconds(2);
                StartupDiag($"GameBar compatibility activation failed controller={index + 1} {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void DeactivateGameBarCompatibilityOutput(int index)
        {
            lock (gameBarCompatibilityOutputLock)
            {
                DeactivateGameBarCompatibilityOutputCore(index);
            }
        }

        private void DeactivateGameBarCompatibilityOutputCore(int index)
        {
            gameBarCompatibilityNextRetryUtc[index] = DateTime.MinValue;
            // Return the report path to the native output before withdrawing or
            // disconnecting the companion. Reports never observe a null route.
            Interlocked.Exchange(ref gameBarCompatibilityRoutingActive[index], 0);
            OutputDevice compatibilityOutput = Interlocked.Exchange(
                ref gameBarCompatibilityOutputDevices[index], null);
            if (compatibilityOutput == null)
            {
                return;
            }

            try
            {
                compatibilityOutput?.ResetState();
                if (compatibilityOutput != null &&
                    outputslotMan.GetOutSlotDevice(compatibilityOutput) != null)
                {
                    outputslotMan.DeferredRemoval(compatibilityOutput, -1,
                        outputDevices, true);
                }
            }
            catch (Exception ex)
            {
                StartupDiag($"GameBar compatibility removal failed controller={index + 1} {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref gameBarCompatibilityRoutingActive[index], 0);
            }

            StartupDiag($"GameBar compatibility deactivated controller={index + 1} native={Global.OutContType[index]}");
        }

        private void StopAllGameBarCompatibilityOutputs()
        {
            for (int i = 0; i < MAX_DS4_CONTROLLER_COUNT; i++)
            {
                DeactivateGameBarCompatibilityOutput(i);
            }
        }

        public void UpdateGameBarState()
        {
            if (!running)
            {
                return;
            }

            if (Interlocked.Exchange(ref gameBarStateUpdateGate, 1) == 1)
            {
                return;
            }

            try
            {
                bool anyMutePending = HasAnyPendingDualSenseMuteProfile();
                bool anyCompatibilityConfigured = HasAnyConfiguredGameBarCompatibility();
                bool anyCompatibilityActive = IsAnyGameBarCompatibilityActive();

                if (!anyMutePending && !anyCompatibilityConfigured &&
                    !anyCompatibilityActive)
                {
                    return;
                }

                DateTime now = DateTime.UtcNow;
                if (now - gameBarLastVisibilityCheckUtc < TimeSpan.FromMilliseconds(100))
                {
                    return;
                }

                gameBarLastVisibilityCheckUtc = now;
                bool gameBarVisible = gameBarIntegration.IsGameBarVisible();
                LogGameBarDetectionIfVerbose(now, gameBarVisible,
                    anyCompatibilityConfigured, anyCompatibilityActive);
                if (gameBarVisible)
                {
                    gameBarLastVisibleUtc = now;
                    UpdateGameBarCompatibilityOutputs(true);
                    return;
                }

                ProcessPendingDualSenseMuteProfiles();
                // Publish the native route before removing the companion so
                // the report path never observes a missing output device.
                UpdateGameBarCompatibilityOutputs(false);
            }
            catch (Exception ex)
            {
                StartupDiag($"UpdateGameBarState exception {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref gameBarStateUpdateGate, 0);
            }
        }

        private void LogGameBarDetectionIfVerbose(DateTime now, bool gameBarVisible,
            bool anyCompatibilityConfigured, bool anyCompatibilityActive)
        {
            if (!Global.VerboseStartupLogging)
            {
                return;
            }

            bool shouldLog = !gameBarVerboseDetectionLogInitialized ||
                gameBarVisible != gameBarVerboseLastVisible ||
                now - gameBarVerboseLastDetectionLogUtc > TimeSpan.FromSeconds(30);

            if (!shouldLog)
            {
                return;
            }

            gameBarVerboseDetectionLogInitialized = true;
            gameBarVerboseLastVisible = gameBarVisible;
            gameBarVerboseLastDetectionLogUtc = now;
            StartupDiag($"GameBar detection visible={gameBarVisible} compatibilityConfigured={anyCompatibilityConfigured} compatibilityActive={anyCompatibilityActive} " +
                $"{gameBarIntegration.CaptureLastDetectionSummary()} controllers={BuildGameBarPriorityStateSummary()}");
        }

        private string BuildGameBarPriorityStateSummary()
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < MAX_DS4_CONTROLLER_COUNT; i++)
            {
                if (DS4Controllers[i] == null &&
                    Volatile.Read(ref gameBarCompatibilityRoutingActive[i]) == 0)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(" ");
                }

                builder.Append("C");
                builder.Append(i + 1);
                builder.Append("[connected=");
                builder.Append(DS4Controllers[i] != null);
                builder.Append(",compatibility=");
                builder.Append(Global.GameBarControllerCompatibility[i]);
                builder.Append(",compatibilityActive=");
                builder.Append(Volatile.Read(ref gameBarCompatibilityRoutingActive[i]) == 1);
                builder.Append("]");
            }

            return builder.Length == 0 ? "none" : builder.ToString();
        }

        private bool HasAnyPendingDualSenseMuteProfile()
        {
            for (int i = 0; i < MAX_DS4_CONTROLLER_COUNT; i++)
            {
                if (dualSenseMuteProfilePending[i])
                {
                    return true;
                }
            }

            return false;
        }

        private void QueueDualSenseMuteProfile(int ind, string profileName)
        {
            if (string.IsNullOrEmpty(profileName))
            {
                return;
            }

            string profilePath = Path.Combine(appdatapath, "Profiles", $"{profileName}.xml");
            if (!File.Exists(profilePath))
            {
                LogDebug($"DualSense mute profile action skipped. Profile '{profileName}' was not found.", true);
                return;
            }

            dualSenseMuteRequestedProfileName[ind] = profileName;
            dualSenseMuteProfilePending[ind] = true;
        }

        private void ProcessPendingDualSenseMuteProfiles()
        {
            for (int i = 0; i < MAX_DS4_CONTROLLER_COUNT; i++)
            {
                if (!dualSenseMuteProfilePending[i])
                {
                    continue;
                }

                string profileName = dualSenseMuteRequestedProfileName[i];
                dualSenseMuteProfilePending[i] = false;
                dualSenseMuteRequestedProfileName[i] = string.Empty;
                int deviceIndex = i;
                Mapping.RequestTemporaryProfileLoad(deviceIndex, profileName,
                    false, this, loaded =>
                    {
                        if (!loaded)
                        {
                            LogDebug($"DualSense mute profile action failed to load " +
                                $"'{profileName}'.", true);
                        }
                    });
            }
        }

        private void StartGameBarStateTimer()
        {
            if (gameBarStateTimer != null)
            {
                return;
            }

            gameBarStateTimer = new System.Threading.Timer(_ => UpdateGameBarState(),
                null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
        }

        private void StopGameBarStateTimer()
        {
            System.Threading.Timer timer = Interlocked.Exchange(ref gameBarStateTimer, null);
            timer?.Dispose();
        }

        // Called every time a new input report has arrived
        protected void On_Report(DS4Device device, EventArgs e, int ind)
        {
            if (ind != -1)
            {
                int startupReportCount = 0;
                bool startupReportDiag = false;
                if (Global.VerboseStartupLogging)
                {
                    startupReportCount = ++startupReportDiagCounts[ind];
                    startupReportDiag = startupReportCount <= 5 || startupReportCount == 50;
                    if (startupReportDiag)
                    {
                        StartupDiag($"On_Report enter index={ind} count={startupReportCount} synced={device.isSynced()} latency={device.Latency} useDInputOnly={useDInputOnly[ind]} activeOut={activeOutDevType[ind]} outDev={outputDevices[ind]?.GetDeviceType() ?? "null"}");
                    }
                }

                string devError = tempStrings[ind] = device.error;
                if (!string.IsNullOrEmpty(devError))
                {
                    LogDebug(devError);
                }

                if (inWarnMonitor[ind])
                {
                    int flashWhenLateAt = getFlashWhenLateAt();
                    if (!lag[ind] && device.Latency >= flashWhenLateAt)
                    {
                        lag[ind] = true;
                        LagFlashWarning(device, ind, true);
                    }
                    else if (lag[ind] && device.Latency < flashWhenLateAt)
                    {
                        lag[ind] = false;
                        LagFlashWarning(device, ind, false);
                    }
                }
                else
                {
                    if (DateTime.UtcNow - device.firstActive > TimeSpan.FromSeconds(5))
                    {
                        inWarnMonitor[ind] = true;
                    }
                }

                DS4State cState, tempControlState;
                if (!device.PerformStateMerge)
                {
                    cState = CurrentState[ind];
                    device.getRawCurrentState(cState);
                    tempControlState = CurrentState[ind];
                }
                else
                {
                    cState = device.JointState;
                    device.MergeStateData(cState);
                    // Need to copy state object info for use in UDP server
                    cState.CopyTo(CurrentState[ind]);
                    tempControlState = CurrentState[ind];
                }

                DS4State pState = device.getPreviousStateRef();
                //device.getPreviousState(PreviousState[ind]);
                //DS4State pState = PreviousState[ind];

                if (device.firstReport && device.isSynced())
                {
                    // Only send Log message when device is considered a primary device
                    if (device.PrimaryDevice)
                    {
                        if (File.Exists(Path.Combine(appdatapath, "Profiles", $"{ProfilePath[ind]}.xml")))
                        {
                            string prolog = string.Format(DS4WinWPF.Properties.Resources.UsingProfile, (ind + 1).ToString(), ProfilePath[ind], $"{device.Battery}");
                            LogDebug(prolog);
                            AppLogger.LogToTray(prolog);
                        }
                        else
                        {
                            string prolog = string.Format(DS4WinWPF.Properties.Resources.NotUsingProfile, (ind + 1).ToString(), $"{device.Battery}");
                            LogDebug(prolog);
                            AppLogger.LogToTray(prolog);
                        }
                    }

                    device.firstReport = false;
                }

                if (device.PrimaryDevice && Global.UseIconChoice == TrayIconChoice.Battery)
                {
                    InvokeBatteryChanged(cState.Battery);
                }

                ControllerExposureStatus exposureStatus =
                    controllerExposureTransitions[ind].Status;
                if (exposureStatus.Stage !=
                    ControllerExposureStage.ManagedVirtualReady)
                {
                    if (startupReportDiag)
                    {
                        StartupDiag($"On_Report gated index={ind} stage={exposureStatus.Stage}");
                    }
                    return;
                }

                if (!device.PrimaryDevice)
                {
                    // Make sure a joined device is still linked
                    int jointInd = device.JointDeviceSlotNumber;
                    if (device.OutputMapGyro &&
                        jointInd != DS4Device.DEFAULT_JOINT_SLOT_NUMBER)
                    {
                        // Output changes from Gyro data early. Seems better to ME... REE
                        GyroOutMode imuOutMode = Global.GetGyroOutMode(device.JointDeviceSlotNumber);
                        if (imuOutMode != GyroOutMode.None)
                        {
                            if (imuOutMode == GyroOutMode.Mouse)
                            {
                                outputKBMHandler.Sync();
                            }
                            else if (imuOutMode == GyroOutMode.MouseJoystick)
                            {
                                // Add new Mapping method and add data to
                                // parent device state
                                DS4State tempMapState = MappedState[jointInd];
                                Mapping.TempMouseJoystick(jointInd, tempMapState);
                                if (!useDInputOnly[jointInd])
                                {
                                    GetReportOutputDevice(jointInd)?.ConvertandSendReport(tempMapState, jointInd);
                                }
                            }
                        }
                    }
                    else if (!device.OutputMapGyro)
                    {
                        // Copy for use in UDP
                        tempControlState.Motion = device.GetRawCurrentStateRef().Motion;
                    }

                    // Skip mapping routine if part of a joined device
                    return;
                }

                CheckGameBarHomeButton(ind, cState, tempControlState, pState);

                if (getEnableTouchToggle(ind))
                {
                    CheckForTouchToggle(ind, cState, pState);
                }

                cState = device.Debouncer.ProcessInput(cState);

                if (startupReportDiag)
                {
                    StartupDiag($"On_Report pre-map index={ind} count={startupReportCount} buttons Cross={cState.Cross} Circle={cState.Circle} PS={cState.PS} LX={cState.LX} LY={cState.LY} RX={cState.RX} RY={cState.RY} L2={cState.L2} R2={cState.R2}");
                }

                cState = Mapping.SetCurveAndDeadzone(ind, cState, TempState[ind]);

                if (!recordingMacro && (useTempProfile[ind] ||
                    containsCustomAction(ind) || containsCustomExtras(ind) ||
                    getProfileActionCount(ind) > 0))
                {
                    DS4State tempMapState = MappedState[ind];
                    DS4State oscMapState = oscState[ind];

                    if (isUsingOSCSender())
                    {
                        OSCPreMappingStep(ind, cState, tempMapState, oscMapState);
                    }

                    if (startupReportDiag)
                    {
                        StartupDiag($"On_Report MapCustom begin index={ind} count={startupReportCount}");
                    }
                    Mapping.MapCustom(ind, cState, tempMapState, ExposedState[ind], touchPad[ind], this);
                    if (startupReportDiag)
                    {
                        StartupDiag($"On_Report MapCustom end index={ind} count={startupReportCount}");
                    }

                    // Copy current Touchpad and Gyro data
                    // Might change to use new DS4State.CopyExtrasTo method
                    tempMapState.Motion = cState.Motion;
                    tempMapState.ds4Timestamp = cState.ds4Timestamp;
                    tempMapState.FrameCounter = cState.FrameCounter;
                    tempMapState.TouchPacketCounter = cState.TouchPacketCounter;
                    tempMapState.TrackPadTouch0 = cState.TrackPadTouch0;
                    tempMapState.TrackPadTouch1 = cState.TrackPadTouch1;

                    if (isUsingOSCServer())
                    {
                        OSCPostMappingStep(tempMapState, oscMapState);
                    }

                    cState = tempMapState;

                }

                if (!useDInputOnly[ind])
                {
                    // Perform this virtual trigger button check in post
                    if (activeOutDevType[ind].Normalize() == OutContType.ViiperDS4)
                    {
                        DS4TriggerOutputMode trigMode = Global.GetOutputDS4TriggerMode(ind);
                        if (trigMode == DS4TriggerOutputMode.Default)
                        {
                            cState.L2Btn = cState.L2 > 0;
                            cState.R2Btn = cState.R2 > 0;
                        }
                        else if (trigMode == DS4TriggerOutputMode.Buttons)
                        {
                            cState.L2Btn = cState.L2 > 0;
                            cState.R2Btn = cState.R2 > 0;
                            // Disable analog output
                            cState.L2 = 0;
                            cState.R2 = 0;
                        }
                    }

                    OutputDevice reportOutput = GetReportOutputDevice(ind);
                    if (startupReportDiag)
                    {
                        StartupDiag($"On_Report ConvertandSendReport begin index={ind} count={startupReportCount} outDev={reportOutput?.GetDeviceType() ?? "null"}");
                    }
                    reportOutput?.ConvertandSendReport(cState, ind);
                    if (startupReportDiag)
                    {
                        StartupDiag($"On_Report ConvertandSendReport end index={ind} count={startupReportCount}");
                    }
                    //testNewReport(ref x360reports[ind], cState, ind);
                    //x360controls[ind]?.SendReport(x360reports[ind]);

                    //x360Bus.Parse(cState, processingData[ind].Report, ind);
                    // We push the translated Xinput state, and simultaneously we
                    // pull back any possible rumble data coming from Xinput consumers.
                    /*if (x360Bus.Report(processingData[ind].Report, processingData[ind].Rumble))
                    {
                        byte Big = processingData[ind].Rumble[3];
                        byte Small = processingData[ind].Rumble[4];

                        if (processingData[ind].Rumble[1] == 0x08)
                        {
                            SetDevRumble(device, Big, Small, ind);
                        }
                    }
                    */
                }
                else
                {
                    // UseDInputOnly profile may re-map sixaxis gyro sensor values as a VJoy joystick axis (steering wheel emulation mode using VJoy output device). Handle this option because VJoy output works even in USeDInputOnly mode.
                    // If steering wheel emulation uses LS/RS/R2/L2 output axies then the profile should NOT use UseDInputOnly option at all because those require a virtual output device.
                    SASteeringWheelEmulationAxisType steeringWheelMappedAxis = Global.GetSASteeringWheelEmulationAxis(ind);
                    switch (steeringWheelMappedAxis)
                    {
                        case SASteeringWheelEmulationAxisType.None: break;

                        case SASteeringWheelEmulationAxisType.VJoy1X:
                        case SASteeringWheelEmulationAxisType.VJoy2X:
                            VJoyFeeder.vJoyFeeder.FeedAxisValue(cState.SASteeringWheelEmulationUnit, ((((uint)steeringWheelMappedAxis) - ((uint)SASteeringWheelEmulationAxisType.VJoy1X)) / 3) + 1, VJoyFeeder.HID_USAGES.HID_USAGE_X);
                            break;

                        case SASteeringWheelEmulationAxisType.VJoy1Y:
                        case SASteeringWheelEmulationAxisType.VJoy2Y:
                            VJoyFeeder.vJoyFeeder.FeedAxisValue(cState.SASteeringWheelEmulationUnit, ((((uint)steeringWheelMappedAxis) - ((uint)SASteeringWheelEmulationAxisType.VJoy1X)) / 3) + 1, VJoyFeeder.HID_USAGES.HID_USAGE_Y);
                            break;

                        case SASteeringWheelEmulationAxisType.VJoy1Z:
                        case SASteeringWheelEmulationAxisType.VJoy2Z:
                            VJoyFeeder.vJoyFeeder.FeedAxisValue(cState.SASteeringWheelEmulationUnit, ((((uint)steeringWheelMappedAxis) - ((uint)SASteeringWheelEmulationAxisType.VJoy1X)) / 3) + 1, VJoyFeeder.HID_USAGES.HID_USAGE_Z);
                            break;

                        default: break;
                    }
                }

                // Output any synthetic events.
                if (startupReportDiag)
                {
                    StartupDiag($"On_Report Mapping.Commit begin index={ind} count={startupReportCount}");
                }
                Mapping.Commit(ind);
                if (startupReportDiag)
                {
                    StartupDiag($"On_Report Mapping.Commit end index={ind} count={startupReportCount}");
                }

                // Update the Lightbar color
                if (startupReportDiag)
                {
                    StartupDiag($"On_Report updateLightBar begin index={ind} count={startupReportCount}");
                }
                DS4LightBar.updateLightBar(device, ind);
                if (startupReportDiag)
                {
                    StartupDiag($"On_Report updateLightBar end index={ind} count={startupReportCount}");
                }

                if (device.PerformStateMerge)
                {
                    device.PreserveMergedStateData();
                }

                if (device.PerformStateMerge && !device.OutputMapGyro)
                {
                    // Copy for use in UDP
                    tempControlState.Motion = device.GetRawCurrentStateRef().Motion;
                }

                if (startupReportDiag)
                {
                    StartupDiag($"On_Report exit index={ind} count={startupReportCount}");
                }
            }
        }

        private static void OSCPostMappingStep(DS4State tempMapState, DS4State oscMapState)
        {
            tempMapState.Cross |= oscMapState.Cross;
            tempMapState.Square |= oscMapState.Square;
            tempMapState.Circle |= oscMapState.Circle;
            tempMapState.Triangle |= oscMapState.Triangle;
            tempMapState.R1 |= oscMapState.R1;
            tempMapState.R3 |= oscMapState.R3;
            tempMapState.L1 |= oscMapState.L1;
            tempMapState.L3 |= oscMapState.L3;
            tempMapState.DpadUp |= oscMapState.DpadUp;
            tempMapState.DpadLeft |= oscMapState.DpadLeft;
            tempMapState.DpadRight |= oscMapState.DpadRight;
            tempMapState.DpadDown |= oscMapState.DpadDown;
            tempMapState.Options |= oscMapState.Options;
            tempMapState.Share |= oscMapState.Share;

            tempMapState.LX = oscMapState.LX != 128 ? oscMapState.LX : tempMapState.LX;
            tempMapState.LY = oscMapState.LY != 128 ? oscMapState.LY : tempMapState.LY;
            tempMapState.L2 = oscMapState.L2 != 0 ? oscMapState.L2 : tempMapState.L2;
            tempMapState.RX = oscMapState.RX != 128 ? oscMapState.RX : tempMapState.RX;
            tempMapState.RY = oscMapState.RY != 128 ? oscMapState.RY : tempMapState.RY;
            tempMapState.R2 = oscMapState.R2 != 0 ? oscMapState.R2 : tempMapState.R2;
        }

        private void OSCPreMappingStep(int ind, DS4State cState, DS4State tempMapState,
            DS4State oscMapState)
        {
            if (cState.Battery != oscMapState.Battery)
            {
                oscSender.Send(new OscMessage("/ds4windows/monitor/" + ind + "/battery", Convert.ToInt32(cState.Battery)));
                oscMapState.Battery = cState.Battery;
            }
            cState.Cross |= oscMapState.Cross;
            cState.Square |= oscMapState.Square;
            cState.Circle |= oscMapState.Circle;
            cState.Triangle |= oscMapState.Triangle;
            cState.R1 |= oscMapState.R1;
            cState.R3 |= oscMapState.R3;
            cState.L1 |= oscMapState.L1;
            cState.L3 |= oscMapState.L3;
            cState.DpadUp |= oscMapState.DpadUp;
            cState.DpadLeft |= oscMapState.DpadLeft;
            cState.DpadRight |= oscMapState.DpadRight;
            cState.DpadDown |= oscMapState.DpadDown;
            cState.Options |= oscMapState.Options;
            cState.Share |= oscMapState.Share;

            cState.LX = oscMapState.LX != 128 ? oscMapState.LX : cState.LX;
            cState.LY = oscMapState.LY != 128 ? oscMapState.LY : cState.LY;
            cState.L2 = oscMapState.L2 != 0 ? oscMapState.L2 : cState.L2;
            cState.RX = oscMapState.RX != 128 ? oscMapState.RX : cState.RX;
            cState.RY = oscMapState.RY != 128 ? oscMapState.RY : cState.RY;
            cState.R2 = oscMapState.R2 != 0 ? oscMapState.R2 : cState.R2;

            CompareAndSendChangesToOSC(ind, tempMapState, cState);
        }

        private void CompareAndSendChangesToOSC(int index, DS4State oldState, DS4State newState)
        {
            // Buttons 
            if (oldState.Square != newState.Square)
            {
                oscSender.Send(new OscMessage("/ds4windows/monitor/" + index + "/square", newState.Square == true ? 1 : 0));
            }

            if (oldState.Triangle != newState.Triangle)
            {
                oscSender.Send(new OscMessage("/ds4windows/monitor/" + index + "/triangle", newState.Triangle == true ? 1 : 0));
            }

            if (oldState.Circle != newState.Circle)
            {
                oscSender.Send(new OscMessage("/ds4windows/monitor/" + index + "/circle", newState.Circle == true ? 1 : 0));
            }

            if (oldState.Cross != newState.Cross)
            {
                oscSender.Send(new OscMessage("/ds4windows/monitor/" + index + "/cross", newState.Cross == true ? 1 : 0));
            }

            if (oldState.DpadUp != newState.DpadUp)
            {
                oscSender.Send(new OscMessage("/ds4windows/monitor/" + index + "/dpadup", newState.DpadUp == true ? 1 : 0));
            }

            if (oldState.DpadDown != newState.DpadDown)
            {
                oscSender.Send(new OscMessage("/ds4windows/monitor/" + index + "/dpaddown", newState.DpadDown == true ? 1 : 0));
            }

            if (oldState.DpadLeft != newState.DpadLeft)
            {
                oscSender.Send(new OscMessage("/ds4windows/monitor/" + index + "/dpadleft", newState.DpadLeft == true ? 1 : 0));
            }

            if (oldState.DpadRight != newState.DpadRight)
            {
                oscSender.Send(new OscMessage("/ds4windows/monitor/" + index + "/dpadright", newState.DpadRight == true ? 1 : 0));
            }

            if (oldState.L1 != newState.L1)
            {
                oscSender.Send(new OscMessage("/ds4windows/monitor/" + index + "/l1", newState.L1 == true ? 1 : 0));
            }

            if (oldState.L3 != newState.L3)
            {
                oscSender.Send(new OscMessage("/ds4windows/monitor/" + index + "/l3", newState.L3 == true ? 1 : 0));
            }

            if (oldState.R1 != newState.R1)
            {
                oscSender.Send(new OscMessage("/ds4windows/monitor/" + index + "/r1", newState.R1 == true ? 1 : 0));
            }

            if (oldState.R3 != newState.R3)
            {
                oscSender.Send(new OscMessage("/ds4windows/monitor/" + index + "/r3", newState.R3 == true ? 1 : 0));
            }

            if (oldState.Options != newState.Options)
            {
                oscSender.Send(new OscMessage("/ds4windows/monitor/" + index + "/options", newState.Options == true ? 1 : 0));
            }

            if (oldState.Share != newState.Share)
            {
                oscSender.Send(new OscMessage("/ds4windows/monitor/" + index + "/share", newState.Share == true ? 1 : 0));
            }

            if (oldState.PS != newState.PS)
            {
                oscSender.Send(new OscMessage("/ds4windows/monitor/" + index + "/ps", newState.PS == true ? 1 : 0));
            }

            // Sticks
            if (oldState.LX != newState.LX)
            {
                oscSender.Send(new OscMessage("/ds4windows/monitor/" + index + "/lx", Convert.ToInt32(newState.LX)));
            }

            if (oldState.LY != newState.LY)
            {
                oscSender.Send(new OscMessage("/ds4windows/monitor/" + index + "/ly", Convert.ToInt32(newState.LY)));
            }

            if (oldState.RX != newState.RX)
            {
                oscSender.Send(new OscMessage("/ds4windows/monitor/" + index + "/rx", Convert.ToInt32(newState.RX)));
            }

            if (oldState.RY != newState.RY)
            {
                oscSender.Send(new OscMessage("/ds4windows/monitor/" + index + "/ry", Convert.ToInt32(newState.RY)));
            }

            // Triggers
            if (oldState.L2 != newState.L2)
            {
                oscSender.Send(new OscMessage("/ds4windows/monitor/" + index + "/l2", Convert.ToInt32(newState.L2)));
            }

            if (oldState.R2 != newState.R2)
            {
                oscSender.Send(new OscMessage("/ds4windows/monitor/" + index + "/r2", Convert.ToInt32(newState.R2)));
            }

            // if (oldState.Battery != newState.Battery)
            // {
            //     AppLogger.LogToGui("BATTERY " + oldState.Battery + " : " + newState.Battery, false);
            //     oscSender.Send(new SharpOSC.OscMessage("/ds4windows/monitor/" + index + "/battery", Convert.ToInt32(newState.Battery)));
            // }
        }

        private void LagFlashWarning(DS4Device device, int ind, bool on)
        {
            if (on)
            {
                lag[ind] = true;
                LogDebug(string.Format(DS4WinWPF.Properties.Resources.LatencyOverTen, (ind + 1), device.Latency), true);
                if (getFlashWhenLate())
                {
                    DS4Color color = new DS4Color { red = 50, green = 0, blue = 0 };
                    DS4LightBar.forcedColor[ind] = color;
                    DS4LightBar.forcedFlash[ind] = 2;
                    DS4LightBar.forcelight[ind] = true;
                }
            }
            else
            {
                lag[ind] = false;
                LogDebug(DS4WinWPF.Properties.Resources.LatencyNotOverTen.Replace("*number*", (ind + 1).ToString()));
                DS4LightBar.forcelight[ind] = false;
                DS4LightBar.forcedFlash[ind] = 0;
                device.LightBarColor = getMainColor(ind);
            }
        }

        public DS4Controls GetActiveInputControl(int ind)
        {
            DS4State cState = CurrentState[ind];
            DS4StateExposed eState = ExposedState[ind];
            Mouse tp = touchPad[ind];
            DS4Controls result = DS4Controls.None;

            if (DS4Controllers[ind] != null)
            {
                if (Mapping.getBoolButtonMapping(cState.Cross))
                    result = DS4Controls.Cross;
                else if (Mapping.getBoolButtonMapping(cState.Circle))
                    result = DS4Controls.Circle;
                else if (Mapping.getBoolButtonMapping(cState.Triangle))
                    result = DS4Controls.Triangle;
                else if (Mapping.getBoolButtonMapping(cState.Square))
                    result = DS4Controls.Square;
                else if (Mapping.getBoolButtonMapping(cState.L1))
                    result = DS4Controls.L1;
                else if (Mapping.getBoolTriggerMapping(cState.L2))
                    result = DS4Controls.L2;
                else if (Mapping.getBoolButtonMapping(cState.L3))
                    result = DS4Controls.L3;
                else if (Mapping.getBoolButtonMapping(cState.R1))
                    result = DS4Controls.R1;
                else if (Mapping.getBoolTriggerMapping(cState.R2))
                    result = DS4Controls.R2;
                else if (Mapping.getBoolButtonMapping(cState.R3))
                    result = DS4Controls.R3;
                else if (Mapping.getBoolButtonMapping(cState.DpadUp))
                    result = DS4Controls.DpadUp;
                else if (Mapping.getBoolButtonMapping(cState.DpadDown))
                    result = DS4Controls.DpadDown;
                else if (Mapping.getBoolButtonMapping(cState.DpadLeft))
                    result = DS4Controls.DpadLeft;
                else if (Mapping.getBoolButtonMapping(cState.DpadRight))
                    result = DS4Controls.DpadRight;
                else if (Mapping.getBoolButtonMapping(cState.Share))
                    result = DS4Controls.Share;
                else if (Mapping.getBoolButtonMapping(cState.Options))
                    result = DS4Controls.Options;
                else if (Mapping.getBoolButtonMapping(cState.PS))
                    result = DS4Controls.PS;
                else if (Mapping.getBoolAxisDirMapping(cState.LX, true))
                    result = DS4Controls.LXPos;
                else if (Mapping.getBoolAxisDirMapping(cState.LX, false))
                    result = DS4Controls.LXNeg;
                else if (Mapping.getBoolAxisDirMapping(cState.LY, true))
                    result = DS4Controls.LYPos;
                else if (Mapping.getBoolAxisDirMapping(cState.LY, false))
                    result = DS4Controls.LYNeg;
                else if (Mapping.getBoolAxisDirMapping(cState.RX, true))
                    result = DS4Controls.RXPos;
                else if (Mapping.getBoolAxisDirMapping(cState.RX, false))
                    result = DS4Controls.RXNeg;
                else if (Mapping.getBoolAxisDirMapping(cState.RY, true))
                    result = DS4Controls.RYPos;
                else if (Mapping.getBoolAxisDirMapping(cState.RY, false))
                    result = DS4Controls.RYNeg;
                else if (Mapping.getBoolTouchMapping(tp.leftDown))
                    result = DS4Controls.TouchLeft;
                else if (Mapping.getBoolTouchMapping(tp.rightDown))
                    result = DS4Controls.TouchRight;
                else if (Mapping.getBoolTouchMapping(tp.multiDown))
                    result = DS4Controls.TouchMulti;
                else if (Mapping.getBoolTouchMapping(tp.upperDown))
                    result = DS4Controls.TouchUpper;
            }

            return result;
        }

        public bool[] touchreleased = new bool[MAX_DS4_CONTROLLER_COUNT] { true, true, true, true, true, true, true, true };

        public Dispatcher EventDispatcher { get => eventDispatcher; }
        public OutputSlotManager OutputslotMan { get => outputslotMan; }

        protected void CheckForTouchToggle(int deviceID, DS4State cState, DS4State pState)
        {
            if (!IsUsingTouchpadForControls(deviceID) && cState.Touch1 && pState.PS)
            {
                if (GetTouchActive(deviceID) && touchreleased[deviceID])
                {
                    TouchActive[deviceID] = false;
                    LogDebug(DS4WinWPF.Properties.Resources.TouchpadMovementOff);
                    AppLogger.LogToTray(DS4WinWPF.Properties.Resources.TouchpadMovementOff);
                    touchreleased[deviceID] = false;
                }
                else if (touchreleased[deviceID])
                {
                    TouchActive[deviceID] = true;
                    LogDebug(DS4WinWPF.Properties.Resources.TouchpadMovementOn);
                    AppLogger.LogToTray(DS4WinWPF.Properties.Resources.TouchpadMovementOn);
                    touchreleased[deviceID] = false;
                }
            }
            else
                touchreleased[deviceID] = true;
        }

        public void StartTPOff(int deviceID)
        {
            if (deviceID < CURRENT_DS4_CONTROLLER_LIMIT)
            {
                TouchActive[deviceID] = false;
            }
        }

        public void SetTouchpadMovementActive(int deviceID, bool active)
        {
            if (deviceID < CURRENT_DS4_CONTROLLER_LIMIT)
            {
                TouchActive[deviceID] = active;
                touchreleased[deviceID] = true;
            }
        }

        public string TouchpadSlide(int ind)
        {
            if (ind < 0 || ind >= touchPad.Length || touchPad[ind] == null ||
                DS4Controllers[ind] == null)
            {
                return "none";
            }

            int direction = touchPad[ind].ConsumeProfileSwipeDirection();
            return direction < 0 ? "left" : direction > 0 ? "right" : "none";
        }

        public void LogDebug(String Data, bool warning = false)
        {
            //Console.WriteLine(System.DateTime.Now.ToString("G") + "> " + Data);
            if (Debug != null)
            {
                DebugEventArgs args = new DebugEventArgs(Data, warning);
                OnDebug(this, args);
            }
        }

        public static void StartupDiag(string data)
        {
            if (!Global.VerboseStartupLogging)
            {
                return;
            }

            startupDiagLogger.Info($"[StartupDiag][T{Thread.CurrentThread.ManagedThreadId}] {data}");
        }

        public void OnDebug(object sender, DebugEventArgs args)
        {
            if (Debug != null)
                Debug(this, args);
        }

        // sets the rumble adjusted with rumble boost. General use method
        public void setRumble(byte heavyMotor, byte lightMotor, int deviceNum)
        {
            if (deviceNum < CURRENT_DS4_CONTROLLER_LIMIT)
            {
                DS4Device device = DS4Controllers[deviceNum];
                if (device != null)
                    SetDevRumble(device, heavyMotor, lightMotor, deviceNum);
                //device.setRumble((byte)lightBoosted, (byte)heavyBoosted);
            }
        }

        // sets the rumble adjusted with rumble boost. Method more used for
        // report handling. Avoid constant checking for a device.
        public void SetDevRumble(DS4Device device,
            byte heavyMotor, byte lightMotor, int deviceNum)
        {
            byte boost = getRumbleBoost(deviceNum);
            uint lightBoosted = ((uint)lightMotor * (uint)boost) / 100;
            if (lightBoosted > 255)
                lightBoosted = 255;
            uint heavyBoosted = ((uint)heavyMotor * (uint)boost) / 100;
            if (heavyBoosted > 255)
                heavyBoosted = 255;

            if (Global.InverseRumbleMotors[deviceNum])
                device.setRumble((byte)heavyBoosted, (byte)lightBoosted);
            else
                device.setRumble((byte)lightBoosted, (byte)heavyBoosted);
        }

        public DS4State getDS4State(int ind)
        {
            return CurrentState[ind];
        }

        public DS4State getDS4StateMapped(int ind)
        {
            return MappedState[ind];
        }

        public DS4State getDS4StateTemp(int ind)
        {
            return TempState[ind];
        }
    }
}
