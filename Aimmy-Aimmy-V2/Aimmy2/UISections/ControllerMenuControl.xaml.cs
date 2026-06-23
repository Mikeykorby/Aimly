using Aimmy2.Class;
using Aimmy2.UILibrary;
using MouseMovementLibraries.ViGEmSupport;
using MouseMovementLibraries.XInputSupport;
using MouseMovementLibraries.DirectInputSupport;
using Aimmy2.Other;
using Other;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UILibrary;

namespace Aimmy2.UISections
{
    public partial class ControllerMenuControl : UserControl
    {
        private MainWindow? _mainWindow;
        private bool _isInitialized;
        private System.Windows.Threading.DispatcherTimer? _statusTimer;
        private static bool _autoConfigRun = false;

        public ControllerMenuControl()
        {
            InitializeComponent();
        }

        public void Initialize(MainWindow mainWindow)
        {
            if (_isInitialized) return;

            _mainWindow = mainWindow;
            _isInitialized = true;

            LoadControllerStatus();
            LoadVirtualController();
            LoadHIDOptions();
            LoadTestVibration();
            LoadResetViGEm();

            // Initialize controller detection
            InitializeControllerDetection();

            // Start live status polling and set initial state immediately
            StartStatusTimer();
            UpdateControllerStatusToggles();
        }

        private void StartStatusTimer()
        {
            _statusTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2),
                IsEnabled = true
            };
            _statusTimer.Tick += (s, e) => UpdateControllerStatusToggles();
            _statusTimer.Start();
        }

        private void UpdateControllerStatusToggles()
        {
            if (_mainWindow == null) return;

            UpdateStatusToggle("XInput (Xbox/Compatible)", XInputMain.Instance.IsConnected);
            UpdateStatusToggle("DirectInput (PS4/PS5)", DirectInputMain.Instance.IsConnected);
        }

        private void UpdateStatusToggle(string title, bool isConnected)
        {
            if (_mainWindow.toggleInstances.TryGetValue(title, out var toggle))
            {
                if (isConnected)
                    toggle.EnableSwitch();
                else
                    toggle.DisableSwitch();
            }
        }

        private void InitializeControllerDetection()
        {
            // Auto-detect physical controllers on menu open
            Task.Run(() =>
            {
                try
                {
                    XInputMain.Instance.Load();
                    DirectInputMain.Instance.Load();

                    if (!_autoConfigRun && DirectInputMain.Instance.IsConnected)
                    {
                        _autoConfigRun = true;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            bool configChanged = false;
                            
                            // Auto-set Uncover as PS4
                            if (!Dictionary.toggleState.TryGetValue("Uncover as PS4", out var val) || !(val is bool b && b))
                            {
                                Dictionary.toggleState["Uncover as PS4"] = true;
                                if (_mainWindow?.toggleInstances.TryGetValue("Uncover as PS4", out var t1) == true)
                                    _mainWindow.UpdateToggleUI(t1, true);
                                configChanged = true;
                            }
                            
                            // Auto-disable Show as Xbox 360
                            if (Dictionary.toggleState.TryGetValue("Show as Xbox 360", out var xval) && (xval is bool xb && xb))
                            {
                                Dictionary.toggleState["Show as Xbox 360"] = false;
                                if (_mainWindow?.toggleInstances.TryGetValue("Show as Xbox 360", out var t2) == true)
                                    _mainWindow.UpdateToggleUI(t2, false);
                                configChanged = true;
                            }
                            
                            // Auto-set Full Input Passthrough
                            if (!Dictionary.toggleState.TryGetValue("Full Input Passthrough (Beta)", out var pval) || !(pval is bool pb && pb))
                            {
                                Dictionary.toggleState["Full Input Passthrough (Beta)"] = true;
                                if (_mainWindow?.toggleInstances.TryGetValue("Full Input Passthrough (Beta)", out var t3) == true)
                                    _mainWindow.UpdateToggleUI(t3, true);
                                configChanged = true;
                            }
                            
                            // Auto-set Pass Buttons/Triggers
                            if (!Dictionary.toggleState.TryGetValue("Pass Buttons/Triggers", out var pbval) || !(pbval is bool pbb && pbb))
                            {
                                Dictionary.toggleState["Pass Buttons/Triggers"] = true;
                                if (_mainWindow?.toggleInstances.TryGetValue("Pass Buttons/Triggers", out var t4) == true)
                                    _mainWindow.UpdateToggleUI(t4, true);
                                configChanged = true;
                            }

                            if (configChanged)
                            {
                                LogManager.Log(LogManager.LogLevel.Info, "Auto-enabled 'Uncover as PS4' since a DirectInput (PS4/PS5) controller was detected.", false);
                            }
                            
                            // Re-detect instantly if needed
                            DirectInputMain.Instance.Load(false);
                            if (Dictionary.toggleState.TryGetValue("Controller Output Mode", out var vcVal2) && vcVal2 is bool vcEnabled2 && vcEnabled2)
                            {
                                VirtualControllerOutput.Disconnect();
                                VirtualControllerOutput.Initialize();
                            }

                            // Trigger Warning if not Wireless Controller
                            if (!DirectInputMain.Instance.ProductName.Contains("Wireless Controller", StringComparison.OrdinalIgnoreCase))
                            {
                                _mainWindow?.ShowDirectInputWarning();
                            }
                        });
                    }
                }
                catch { }
            });
        }

        ~ControllerMenuControl()
        {
            _statusTimer?.Stop();
        }

        #region Menu Section Loaders

        private void LoadControllerStatus()
        {
            var builder = new SectionBuilder(this, ControllerStatus);
            builder
                .AddTitle("Controller Status", true, t =>
                {
                    t.Minimize.Click += (s, e) => TogglePanel("Controller Status", ControllerStatus);
                })
                .AddButton("Refresh Devices", b =>
                {
                    b.Reader.Click += (s, e) =>
                    {
                        XInputMain.Instance.Load(false);
                        DirectInputMain.Instance.Load(false);
                        
                        // Update toggles
                        if (_mainWindow != null)
                        {
                            if (_mainWindow.toggleInstances.TryGetValue("XInput (Xbox/Compatible)", out var tXbox))
                            {
                                _mainWindow.UpdateToggleUI(tXbox, XInputMain.Instance.IsConnected);
                            }
                            if (_mainWindow.toggleInstances.TryGetValue("DirectInput (PS4/PS5)", out var tPS))
                            {
                                _mainWindow.UpdateToggleUI(tPS, DirectInputMain.Instance.IsConnected);
                            }
                        }
                        
                        // Restart ViGEm if enabled
                        if (Dictionary.toggleState.TryGetValue("Controller Output Mode", out var vcVal) && vcVal is bool vcEnabled && vcEnabled)
                        {
                            VirtualControllerOutput.Disconnect();
                            VirtualControllerOutput.Initialize();
                        }
                    };
                }, tooltip: "Click to re-detect connected controllers.")
                .AddToggle("XInput (Xbox/Compatible)", t =>
                {
                    if (XInputMain.Instance.IsConnected)
                        t.EnableSwitch();
                    else
                        t.DisableSwitch();
                }, tooltip: "Physical Xbox or compatible controller status. Read-only indicator.",
                customClickHandler: (t, e) => { /* Status read-only */ })
                .AddToggle("DirectInput (PS4/PS5)", t =>
                {
                    if (DirectInputMain.Instance.IsConnected)
                        t.EnableSwitch();
                    else
                        t.DisableSwitch();
                }, tooltip: "Physical PS4/PS5 controller status. Read-only indicator.",
                customClickHandler: (t, e) => { /* Status read-only */ })
                .AddSeparator();
        }

        private void LoadVirtualController()
        {
            var builder = new SectionBuilder(this, VirtualController);
            builder
                .AddTitle("Virtual Controller Output", true, t =>
                {
                    t.Minimize.Click += (s, e) => TogglePanel("Virtual Controller Output", VirtualController);
                })
                .AddToggle("Controller Output Mode", null,
                    tooltip: "Routes AI aim output through the virtual controller (ViGEmBus). Required for controller-based aim assist.",
                    customClickHandler: (toggle, e) =>
                    {
                        bool newState = !Dictionary.toggleState.TryGetValue("Controller Output Mode", out var val) || !((val as bool?) ?? false);
                        Dictionary.toggleState["Controller Output Mode"] = newState;
                        _mainWindow?.UpdateToggleUI(toggle, newState);

                        if (newState)
                        {
                            VirtualControllerOutput.Initialize();
                            LogManager.Log(LogManager.LogLevel.Info, $"Controller Output Mode enabled ({VirtualControllerOutput.ActiveTypeName}).", false);
                        }
                        else
                        {
                            VirtualControllerOutput.Disconnect();
                            LogManager.Log(LogManager.LogLevel.Info, "Controller Output Mode disabled.", false);
                        }
                    })
                .AddToggle("Virtual Controller Left Stick Aim", null,
                    tooltip: "When enabled, AI aim is sent to the virtual Left Stick instead of the Right Stick.",
                    customClickHandler: (toggle, e) =>
                    {
                        bool newState = !Dictionary.toggleState.TryGetValue("Virtual Controller Left Stick Aim", out var val) || !((val as bool?) ?? false);
                        Dictionary.toggleState["Virtual Controller Left Stick Aim"] = newState;
                        _mainWindow?.UpdateToggleUI(toggle, newState);
                    })
                .AddToggle("Pass Buttons/Triggers", null,
                    tooltip: "When enabled, virtual controller passes through buttons and triggers. When disabled, only passes thumbsticks (prevents double input).",
                    customClickHandler: (toggle, e) =>
                    {
                        bool newState = !Dictionary.toggleState.TryGetValue("Pass Buttons/Triggers", out var val) || !((val as bool?) ?? false);
                        Dictionary.toggleState["Pass Buttons/Triggers"] = newState;
                        _mainWindow?.UpdateToggleUI(toggle, newState);
                    })
                .AddToggle("Full Input Passthrough (Beta)", null,
                    tooltip: "Captures and passes raw inputs (including Touchpad and PS Button) to the virtual PS4 controller instead of mapping them to Xbox generic standard. Best used with DS4/DualSense.",
                    customClickHandler: (toggle, e) =>
                    {
                        bool newState = !Dictionary.toggleState.TryGetValue("Full Input Passthrough (Beta)", out var val) || !((val as bool?) ?? false);
                        Dictionary.toggleState["Full Input Passthrough (Beta)"] = newState;
                        _mainWindow?.UpdateToggleUI(toggle, newState);
                    })
                .AddDropdown("Target Physical Controller", d =>
                {
                    d.DropdownBox.DropDownOpened += (s, e) =>
                    {
                        if (_mainWindow == null) return;
                        d.DropdownBox.Items.Clear();

                        var autoItem = _mainWindow.AddDropdownItem(d, "Auto (Any)");
                        autoItem.Selected += (s2, e2) => {
                            XInputMain.TargetIndex = null;
                            DirectInputMain.TargetGuid = null;
                        };

                        // Add XInput controllers
                        var xi = XInputMain.Instance.GetAvailableControllers();
                        foreach (var kvp in xi)
                        {
                            var item = _mainWindow.AddDropdownItem(d, kvp.Value);
                            item.Selected += (s2, e2) => {
                                XInputMain.TargetIndex = uint.Parse(kvp.Key.Split('_')[1]);
                                DirectInputMain.TargetGuid = null;
                                LogManager.Log(LogManager.LogLevel.Info, $"Target input locked to {kvp.Value}", false);
                            };
                        }

                        // Add DirectInput controllers
                        var di = DirectInputMain.Instance.GetAvailableControllers();
                        foreach (var kvp in di)
                        {
                            var item = _mainWindow.AddDropdownItem(d, kvp.Value);
                            item.Selected += (s2, e2) => {
                                XInputMain.TargetIndex = null;
                                DirectInputMain.TargetGuid = Guid.Parse(kvp.Key.Split('_')[1]);
                                LogManager.Log(LogManager.LogLevel.Info, $"Target input locked to {kvp.Value}", false);
                                
                                bool configChanged = false;

                                // Auto-set Uncover as PS4
                                if (!Dictionary.toggleState.TryGetValue("Uncover as PS4", out var val) || !(val is bool b && b))
                                {
                                    Dictionary.toggleState["Uncover as PS4"] = true;
                                    if (_mainWindow?.toggleInstances.TryGetValue("Uncover as PS4", out var t1) == true)
                                        _mainWindow.UpdateToggleUI(t1, true);
                                    configChanged = true;
                                }

                                // Auto-disable Show as Xbox 360
                                if (Dictionary.toggleState.TryGetValue("Show as Xbox 360", out var xval) && (xval is bool xb && xb))
                                {
                                    Dictionary.toggleState["Show as Xbox 360"] = false;
                                    if (_mainWindow?.toggleInstances.TryGetValue("Show as Xbox 360", out var t2) == true)
                                        _mainWindow.UpdateToggleUI(t2, false);
                                    configChanged = true;
                                }

                                // Auto-set Uncover as PS4
                                if (!Dictionary.toggleState.TryGetValue("Uncover as PS4", out var ps4val) || !(ps4val is bool ps4b && ps4b))
                                {
                                    Dictionary.toggleState["Uncover as PS4"] = true;
                                    if (_mainWindow?.toggleInstances.TryGetValue("Uncover as PS4", out var tPS4) == true)
                                        _mainWindow.UpdateToggleUI(tPS4, true);
                                    configChanged = true;
                                }

                                // Auto-set Full Input Passthrough
                                if (!Dictionary.toggleState.TryGetValue("Full Input Passthrough (Beta)", out var pval) || !(pval is bool pb && pb))
                                {
                                    Dictionary.toggleState["Full Input Passthrough (Beta)"] = true;
                                    if (_mainWindow?.toggleInstances.TryGetValue("Full Input Passthrough (Beta)", out var t3) == true)
                                        _mainWindow.UpdateToggleUI(t3, true);
                                    configChanged = true;
                                }

                                // Auto-set Pass Buttons/Triggers
                                if (!Dictionary.toggleState.TryGetValue("Pass Buttons/Triggers", out var pbval) || !(pbval is bool pbb && pbb))
                                {
                                    Dictionary.toggleState["Pass Buttons/Triggers"] = true;
                                    if (_mainWindow?.toggleInstances.TryGetValue("Pass Buttons/Triggers", out var t4) == true)
                                        _mainWindow.UpdateToggleUI(t4, true);
                                }
                                if (configChanged)
                                {
                                    LogManager.Log(LogManager.LogLevel.Info, "Auto-enabled 'Uncover as PS4' since a DirectInput (PS4/PS5) controller was selected.", false);
                                }
                                
                                // Re-detect instantly
                                DirectInputMain.Instance.Load(false);
                                if (Dictionary.toggleState.TryGetValue("Controller Output Mode", out var vcVal) && vcVal is bool vcEnabled && vcEnabled)
                                {
                                    VirtualControllerOutput.Disconnect();
                                    VirtualControllerOutput.Initialize();
                                }

                                // Trigger Warning if not Wireless Controller
                                if (!kvp.Value.Contains("Wireless Controller", StringComparison.OrdinalIgnoreCase))
                                {
                                    _mainWindow?.ShowDirectInputWarning();
                                }
                            };
                        }
                    };
                }, tooltip: "Explicitly select which physical controller to read from. 'Auto' selects the first detected one.")
                .AddSeparator();
        }

        private void LoadHIDOptions()
        {
            var builder = new SectionBuilder(this, HIDOptions);
            builder
                .AddTitle("HID Options", true, t =>
                {
                    t.Minimize.Click += (s, e) => TogglePanel("HID Options", HIDOptions);
                })
                .AddToggle("Uncover as PS4", null,
                    tooltip: "Emulate a DualShock 4 (PS4) controller instead of Xbox 360. Changes affect the virtual controller type.",
                    customClickHandler: (toggle, e) =>
                    {
                        bool newState = !Dictionary.toggleState.TryGetValue("Uncover as PS4", out var val) || !((val as bool?) ?? false);
                        Dictionary.toggleState["Uncover as PS4"] = newState;
                        _mainWindow?.UpdateToggleUI(toggle, newState);

                        // Mutually exclusive: turn off "Show as Xbox 360"
                        if (newState && Dictionary.toggleState.TryGetValue("Show as Xbox 360", out var xboxVal) && xboxVal is bool xboxOn && xboxOn)
                        {
                            Dictionary.toggleState["Show as Xbox 360"] = false;
                            if (_mainWindow?.toggleInstances.TryGetValue("Show as Xbox 360", out var xboxToggle) == true)
                            {
                                _mainWindow.UpdateToggleUI(xboxToggle, false);
                            }
                        }

                        if (VirtualControllerOutput.IsConnected)
                        {
                            VirtualControllerOutput.Disconnect();
                            Task.Run(() => VirtualControllerOutput.Initialize());
                        }
                    })
                .AddToggle("Hide Real Controller", null,
                    tooltip: "Hides your real controller from games using HidHide. Requires Aimmy to be run as Administrator.",
                    customClickHandler: (toggle, e) =>
                    {
                        bool isAdmin = false;
                        using (System.Security.Principal.WindowsIdentity identity = System.Security.Principal.WindowsIdentity.GetCurrent())
                        {
                            System.Security.Principal.WindowsPrincipal principal = new System.Security.Principal.WindowsPrincipal(identity);
                            isAdmin = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
                        }

                        if (!isAdmin)
                        {
                            System.Windows.MessageBox.Show("Administrator privileges are required to use this feature. Please restart Aimmy as Administrator.", "Admin Required", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                            Dictionary.toggleState["Hide Real Controller"] = false;
                            _mainWindow?.UpdateToggleUI(toggle, false);
                            return;
                        }

                        bool isChecked = !Dictionary.toggleState.TryGetValue("Hide Real Controller", out var val) || !((val as bool?) ?? false);
                        Dictionary.toggleState["Hide Real Controller"] = isChecked;
                        _mainWindow?.UpdateToggleUI(toggle, isChecked);

                        if (isChecked)
                        {
                            if (Other.HidHideManager.IsAvailable && Other.HidHideManager.IsAdmin())
                            {
                                Task.Run(async () =>
                                {
                                    bool wasConnected = VirtualControllerOutput.IsConnected;
                                    if (wasConnected)
                                    {
                                        VirtualControllerOutput.Disconnect();
                                        await Task.Delay(500); // Wait for the virtual controller to fully disconnect before hiding gaming devices
                                    }

                                    Other.HidHideManager.HideAllGamingDevices();
                                    Other.HidHideManager.EnableCloaking();
                                    LogManager.Log(LogManager.LogLevel.Info, "HidHide cloaking enabled for your controller.", false);

                                    if (wasConnected)
                                    {
                                        VirtualControllerOutput.Initialize();
                                    }
                                });
                            }
                            else
                            {
                                Other.HidHideManager.TrySetup();
                                // Revert toggle visually since we couldn't enable it
                                Dictionary.toggleState["Hide Real Controller"] = false;
                                _mainWindow?.UpdateToggleUI(toggle, false);
                            }
                        }
                        else
                        {
                            if (Other.HidHideManager.IsAvailable && Other.HidHideManager.IsAdmin())
                            {
                                Task.Run(() =>
                                {
                                    Other.HidHideManager.DisableAndCleanup();
                                    LogManager.Log(LogManager.LogLevel.Info, "HidHide cloaking disabled.", false);
                                });
                            }
                        }
                    })
                .AddSeparator();
        }

        private void LoadTestVibration()
        {
            var builder = new SectionBuilder(this, TestVibration);
            builder
                .AddTitle("Test Vibration", true, t =>
                {
                    t.Minimize.Click += (s, e) => TogglePanel("Test Vibration", TestVibration);
                })
                .AddButton("Test Physical Controller", b =>
                {
                    b.Reader.Click += (s, e) => TestPhysicalVibration();
                }, tooltip: "Test vibration on a physical Xbox/PS controller")
                .AddButton("Test Virtual Controller (Stick Move)", b =>
                {
                    b.Reader.Click += (s, e) => TestVirtualVibration();
                }, tooltip: "Moves the virtual right stick in a visible pattern. Requires a game or gamepad tester to observe.")
                .AddButton("Reconnect Controllers", b =>
                {
                    b.Reader.Click += (s, e) => ReconnectAllControllers();
                }, tooltip: "Force detection of all controller types")
                .AddSeparator();
        }

        private void LoadResetViGEm()
        {
            var builder = new SectionBuilder(this, ResetViGEm);
            builder
                .AddTitle("Reset ViGEm", true, t =>
                {
                    t.Minimize.Click += (s, e) => TogglePanel("Reset ViGEm", ResetViGEm);
                })
                .AddButton("Reset ViGEm Settings", b =>
                {
                    b.Reader.Click += (s, e) => ResetViGEmSettings();
                }, tooltip: "Reset all ViGEm/HID settings to defaults")
                .AddSeparator();
        }

        private void ResetViGEmSettings()
        {
            var togglesToReset = new[]
            {
                "Controller Output Mode",
                "Uncover as PS4",
                "Hide Real Controller",
                "Show as Xbox 360",

                "Controller Output Mode"
            };

            foreach (var key in togglesToReset)
            {
                Dictionary.toggleState[key] = false;
                if (_mainWindow?.toggleInstances.TryGetValue(key, out var toggle) == true)
                {
                    _mainWindow.UpdateToggleUI(toggle, false);
                }
            }

            VirtualControllerOutput.Disconnect();
            LogManager.Log(LogManager.LogLevel.Info, "ViGEm settings have been reset to defaults.", true);
        }

        #endregion

        #region Vibration Testing

        private async void TestPhysicalVibration()
        {
            // Ensure controller detection is fresh on a background thread to prevent UI freezing
            await Task.Run(() =>
            {
                if (!XInputMain.Instance.IsConnected)
                    XInputMain.Instance.Load();
                if (!DirectInputMain.Instance.IsConnected)
                    DirectInputMain.Instance.Load();
            });

            // 1) Try physical XInput first (Xbox controllers)
            if (XInputMain.Instance.IsConnected)
            {
                try
                {
                    XInputMain.Instance.Vibrate(30000, 30000);
                    await Task.Delay(200);
                    XInputMain.Instance.Vibrate(0, 0);
                    LogManager.Log(LogManager.LogLevel.Info, "XInput vibration test completed", false);
                    return;
                }
                catch (Exception ex)
                {
                    LogManager.Log(LogManager.LogLevel.Warning, $"XInput vibration test failed: {ex.Message}. Trying DirectInput...", false);
                }
            }

            // 2) Try physical DirectInput (PS4/PS5 via DS4Windows/DualSenseY)
            if (DirectInputMain.Instance.IsConnected)
            {
                try
                {
                    await DirectInputMain.Instance.VibrateUDPAsync(255, 255);
                    await Task.Delay(200);
                    await DirectInputMain.Instance.VibrateUDPAsync(0, 0);
                    LogManager.Log(LogManager.LogLevel.Info,
                        "DirectInput vibration test completed (via UDP to DS4Windows/DualSenseY).", false);
                    return;
                }
                catch (Exception ex)
                {
                    LogManager.Log(LogManager.LogLevel.Error, $"DirectInput vibration error: {ex.Message}", false);
                }
            }

            // 3) Virtual fallback: move the virtual right stick as a visual test
            if (VirtualControllerOutput.IsConnected)
            {
                try
                {
                    LogManager.Log(LogManager.LogLevel.Info,
                        "Physical controller not detected or hidden. Performing virtual stick test as fallback...", false);

                    for (int i = 0; i < 15; i++) { VirtualControllerOutput.SetAimStick(150, 150); await Task.Delay(20); }
                    for (int i = 0; i < 15; i++) { VirtualControllerOutput.SetAimStick(-150, -150); await Task.Delay(20); }
                    VirtualControllerOutput.SetAimStick(0, 0);

                    LogManager.Log(LogManager.LogLevel.Info,
                        "Virtual controller stick test completed (physical vibration unavailable, stick movement used as fallback).", false);
                    return;
                }
                catch (Exception ex)
                {
                    LogManager.Log(LogManager.LogLevel.Warning, $"Virtual fallback test failed: {ex.Message}", false);
                }
            }

            // 4) Nothing worked — show comprehensive help
            LogManager.Log(LogManager.LogLevel.Warning,
                "No physical controller detected for vibration test.\n" +
                "- If your controller is hidden by Exclusive Mode, some games might still see XInput.\n" +
                "- Xbox: make sure they're connected via USB or Xbox adapter.\n" +
                "- PS5/PS4: install DS4Windows (https://ds4-windows.com) or connect via USB.", true);
        }

        /// <summary>
        /// Tests the virtual controller by performing a short stick movement sequence
        /// that is visible to gamepad testers or in-game.
        /// </summary>
        private async void TestVirtualVibration()
        {
            if (!VirtualControllerOutput.IsConnected)
            {
                LogManager.Log(LogManager.LogLevel.Warning,
                    "ViGEm virtual controller is not connected.\n" +
                    "Enable 'Controller Output Mode' in the Controller page first.", true);
                return;
            }

            try
            {
                // Sequence: up-right -> down-left -> down-right -> back to center
                // This produces a visible diamond-shaped movement.
                LogManager.Log(LogManager.LogLevel.Info, "Virtual controller test: moving right stick in a diamond pattern...", false);

                // Use max values and loop to keep the stick pressed despite decay (decay is 50ms)
                for (int i=0; i<10; i++) { VirtualControllerOutput.SetAimStick(150, 150); await Task.Delay(20); }
                for (int i=0; i<10; i++) { VirtualControllerOutput.SetAimStick(-150, -150); await Task.Delay(20); }
                for (int i=0; i<10; i++) { VirtualControllerOutput.SetAimStick(-150, 150); await Task.Delay(20); }
                for (int i=0; i<10; i++) { VirtualControllerOutput.SetAimStick(150, -150); await Task.Delay(20); }
                VirtualControllerOutput.SetAimStick(0, 0);

                LogManager.Log(LogManager.LogLevel.Info,
                    "Test complete.", false);
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"Virtual test error: {ex.Message}", false);
            }
        }

        private void ReconnectAllControllers()
        {
            try
            {
                XInputMain.Instance.Load();
                DirectInputMain.Instance.Load();
                if (!VirtualControllerOutput.IsConnected && (Dictionary.toggleState.TryGetValue("Controller Output Mode", out var vcVal5) && vcVal5 is bool vcEnabled5 && vcEnabled5))
                    VirtualControllerOutput.Initialize();

                LogManager.Log(LogManager.LogLevel.Info,
                    "Controller reconnection attempted. Check status toggles for results.", false);
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"Reconnection error: {ex.Message}", false);
            }
        }

        #endregion

        #region Section Builder (same pattern as AimMenuControl)

        private class SectionBuilder
        {
            private readonly ControllerMenuControl _parent;
            private readonly StackPanel _panel;

            public SectionBuilder(ControllerMenuControl parent, StackPanel panel)
            {
                _parent = parent;
                _panel = panel;
            }

            public SectionBuilder AddTitle(string title, bool canMinimize, Action<ATitle>? configure = null)
            {
                var titleControl = new ATitle(title, canMinimize);
                configure?.Invoke(titleControl);
                _panel.Children.Add(titleControl);
                return this;
            }

            public SectionBuilder AddToggle(string title, Action<AToggle>? configure = null, string? tooltip = null, Func<string>? tooltipFunc = null, Action<AToggle, RoutedEventArgs>? customClickHandler = null)
            {
                string effectiveTooltip = tooltipFunc?.Invoke() ?? tooltip ?? "";
                var toggle = _parent.CreateToggle(title, effectiveTooltip, customClickHandler);
                configure?.Invoke(toggle);
                toggle.HorizontalAlignment = HorizontalAlignment.Stretch;
                _panel.Children.Add(toggle);
                return this;
            }

            public SectionBuilder AddDropdown(string title, Action<ADropdown>? configure = null, string? tooltip = null)
            {
                var dropdown = new ADropdown(title, title, tooltip);
                configure?.Invoke(dropdown);
                _panel.Children.Add(dropdown);
                return this;
            }

            public SectionBuilder AddButton(string title, Action<APButton>? configure = null, string? tooltip = null)
            {
                var button = new APButton(title, tooltip);
                configure?.Invoke(button);
                _panel.Children.Add(button);
                return this;
            }

            public SectionBuilder AddSeparator()
            {
                _panel.Children.Add(new ARectangleBottom());
                _panel.Children.Add(new ASpacer());
                return this;
            }
        }

        #endregion

        #region Control Creation Methods

        private AToggle CreateToggle(string title, string? tooltip = null, Action<AToggle, RoutedEventArgs>? customClickHandler = null)
        {
            var toggle = new AToggle(title, tooltip);
            _mainWindow?.toggleInstances.TryAdd(title, toggle);

            bool initialState = false;
            if (Dictionary.toggleState.TryGetValue(title, out var value) && value is bool b)
                initialState = b;
            else
                Dictionary.toggleState[title] = false;

            if (initialState)
                toggle.EnableSwitch();
            else
                toggle.DisableSwitch();

            if (customClickHandler != null)
            {
                toggle.Reader.Click += (sender, e) =>
                {
                    customClickHandler(toggle, e);
                };
            }
            else
            {
                toggle.Reader.Click += (sender, e) =>
                {
                    Dictionary.toggleState[title] = !Dictionary.toggleState[title];
                    _mainWindow?.UpdateToggleUI(toggle, Dictionary.toggleState[title]);
                    _mainWindow?.Toggle_Action(title);
                };
            }

            return toggle;
        }

        #endregion

        #region Helpers

        public void TogglePanel(string key, UIElement panel)
        {
            panel.Visibility = panel.Visibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion
    }
}
