using System;
using System.IO;
using System.Threading.Tasks;
using Aimmy2.Class;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using Other;

namespace MouseMovementLibraries.ViGEmSupport
{
    /// <summary>
    /// Virtual Xbox 360 controller output using ViGEmBus managed API.
    /// Creates a virtual controller that games/gamepad testers can detect.
    /// The AI aim is sent as virtual right stick movements.
    /// Requires ViGEmBus driver: https://github.com/ViGEm/ViGEmBus/releases
    /// </summary>
    internal class VirtualControllerOutput
    {
        private static ViGEmClient? _client;
        private static IXbox360Controller? _gamepad;
        private static Nefarius.ViGEm.Client.Targets.IDualShock4Controller? _ds4Gamepad;
        private static bool _isConnected = false;
        private static bool _initializationAttempted = false;

        // Second controller for "Show as 2 Controllers" mode
        private static ViGEmClient? _client2;
        private static IXbox360Controller? _gamepad2;
        private static Nefarius.ViGEm.Client.Targets.IDualShock4Controller? _ds4Gamepad2;
        private static bool _isConnected2 = false;

        public static bool InitializationAttempted => _initializationAttempted;
        public static bool IsConnected => _isConnected;
        public static bool IsConnected2 => _isConnected2;
        public static string ActiveTypeName { get; private set; } = "Xbox 360";

        /// <summary>
        /// Initialize ViGEm virtual controller using managed API
        /// Reads current HID options from Dictionary.toggleState to determine configuration.
        /// </summary>
        public static bool Initialize()
        {
            try
            {
                _initializationAttempted = true;
                if (_isConnected) return true;

                // Read HID options from saved state
                Dictionary.toggleState.TryGetValue("Show as 2 Controllers", out var showAs2Obj);
                bool showAs2 = showAs2Obj is bool b2 && b2;
                bool uncoverAsPS4 = Dictionary.toggleState.TryGetValue("Uncover as PS4", out var ps4Obj) && ps4Obj is bool ps4b && ps4b;

                LogManager.Log(LogManager.LogLevel.Info,
                    uncoverAsPS4
                        ? "Initializing ViGEm virtual controller as DualShock 4 (PS4)..."
                        : "Initializing ViGEm virtual controller (Xbox 360)...", false);

                _client = new ViGEmClient();

                if (uncoverAsPS4)
                {
                    _ds4Gamepad = _client.CreateDualShock4Controller();
                    _ds4Gamepad.Connect();
                    ActiveTypeName = "DualShock 4 (PS4)";
                    LogManager.Log(LogManager.LogLevel.Info, "Virtual DualShock 4 (PS4) controller connected via ViGEmBus", false);
                }
                else
                {
                    _gamepad = _client.CreateXbox360Controller();
                    _gamepad.Connect();
                    
                    try {
                        var ixbox = _gamepad as Nefarius.ViGEm.Client.Targets.IXbox360Controller;
                        if (ixbox != null) {
                            MouseMovementLibraries.XInputSupport.XInputMain.IgnoredIndex = (uint)ixbox.UserIndex;
                        }
                    } catch { }

                    ActiveTypeName = "Xbox 360";
                    LogManager.Log(LogManager.LogLevel.Info, "Virtual Xbox 360 controller connected via ViGEmBus", false);
                }

                _isConnected = true;

                // If dual-controller mode is enabled, create a second controller
                if (showAs2)
                {
                    try
                    {
                        _client2 = new ViGEmClient();
                        if (uncoverAsPS4)
                        {
                            _ds4Gamepad2 = _client2.CreateDualShock4Controller();
                            _ds4Gamepad2.Connect();
                            LogManager.Log(LogManager.LogLevel.Info, "Second virtual DualShock 4 (PS4) controller connected (dual-controller mode)", false);
                        }
                        else
                        {
                            _gamepad2 = _client2.CreateXbox360Controller();
                            _gamepad2.Connect();
                            LogManager.Log(LogManager.LogLevel.Info, "Second virtual Xbox 360 controller connected (dual-controller mode)", false);
                        }
                        _isConnected2 = true;
                    }
                    catch (Exception ex2)
                    {
                        LogManager.Log(LogManager.LogLevel.Warning, $"Could not create second virtual controller: {ex2.Message}", false);
                        _isConnected2 = false;
                    }
                }

                // Start passthrough task
                _cancellationTokenSource = new System.Threading.CancellationTokenSource();
                Task.Run(() => PassthroughLoop(_cancellationTokenSource.Token));

                return true;
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"ViGEm is not installed or initialization failed: {ex.Message}\nOpening ViGEmBus download page.", true);
                
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "https://github.com/ViGEm/ViGEmBus/releases/latest",
                        UseShellExecute = true
                    });
                }
                catch { }

                Disconnect();
                return false;
            }
        }

        private static System.Threading.CancellationTokenSource? _cancellationTokenSource;
        private static int _aimLeftOffsetX = 0;
        private static int _aimLeftOffsetY = 0;
        private static int _aimRightOffsetX = 0;
        private static int _aimRightOffsetY = 0;
        private static long _lastAimTick = 0;
        private static long _lastDiLoadAttempt = 0;

        private static async Task PassthroughLoop(System.Threading.CancellationToken token)
        {
            while (!token.IsCancellationRequested && _isConnected)
            {
                try
                {
                    ushort wButtons = 0;
                    byte bLeftTrigger = 0, bRightTrigger = 0;
                    short sThumbLX = 0, sThumbLY = 0, sThumbRX = 0, sThumbRY = 0;
                    bool[] diButtons = null;
                    bool hasInput = false;

                    bool useDirectInput = MouseMovementLibraries.DirectInputSupport.DirectInputMain.TargetGuid.HasValue;

                    if (!useDirectInput && (MouseMovementLibraries.XInputSupport.XInputMain.Instance.IsConnected || MouseMovementLibraries.XInputSupport.XInputMain.Instance.Load(false)))
                    {
                        var state = MouseMovementLibraries.XInputSupport.XInputMain.Instance.GetFullState();
                        if (state.HasValue)
                        {
                            var s = state.Value;
                            wButtons = s.wButtons;
                            bLeftTrigger = s.bLeftTrigger;
                            bRightTrigger = s.bRightTrigger;
                            sThumbLX = s.sThumbLX;
                            sThumbLY = s.sThumbLY;
                            sThumbRX = s.sThumbRX;
                            sThumbRY = s.sThumbRY;
                            hasInput = true;
                        }
                    }
                    else
                    {
                        bool diConnected = MouseMovementLibraries.DirectInputSupport.DirectInputMain.Instance.IsConnected;
                        if (!diConnected)
                        {
                            long now = System.Diagnostics.Stopwatch.GetTimestamp();
                            if (now - _lastDiLoadAttempt > System.Diagnostics.Stopwatch.Frequency * 2)
                            {
                                _lastDiLoadAttempt = now;
                                diConnected = MouseMovementLibraries.DirectInputSupport.DirectInputMain.Instance.Load(false);
                            }
                        }

                        if (diConnected)
                        {
                            var di = MouseMovementLibraries.DirectInputSupport.DirectInputMain.Instance;
                            di.PollState();
                            
                            sThumbLX = (short)Math.Clamp(di.CurrentLeftStickX, -32768, 32767);
                            sThumbLY = (short)Math.Clamp(-di.CurrentLeftStickY, -32768, 32767); // Invert Y
                            sThumbRX = (short)Math.Clamp(di.CurrentRightStickX, -32768, 32767);
                            sThumbRY = (short)Math.Clamp(-di.CurrentRightStickY, -32768, 32767); // Invert Y
                            bLeftTrigger = (byte)(Math.Clamp(di.CurrentLeftTrigger, 0, 65535) / 257);
                            bRightTrigger = (byte)(Math.Clamp(di.CurrentRightTrigger, 0, 65535) / 257);
                            
                            diButtons = di.GetButtons();
                            var povs = di.GetPOV();

                            if (diButtons != null && diButtons.Length >= 14)
                            {
                                // DS4 Mapping (0: Square/X, 1: Cross/A, 2: Circle/B, 3: Triangle/Y, 4: L1/LB, 5: R1/RB, 6: L2/LT, 7: R2/RT, 8: Share/Back, 9: Options/Start, 10: L3/LS, 11: R3/RS, 12: PS, 13: Touchpad)
                                if (diButtons[0]) wButtons |= 0x4000; // X
                                if (diButtons[1]) wButtons |= 0x1000; // A
                                if (diButtons[2]) wButtons |= 0x2000; // B
                                if (diButtons[3]) wButtons |= 0x8000; // Y
                                if (diButtons[4]) wButtons |= 0x0100; // LB
                                if (diButtons[5]) wButtons |= 0x0200; // RB
                                if (diButtons[8]) wButtons |= 0x0020; // Back
                                if (diButtons[9]) wButtons |= 0x0010; // Start
                                if (diButtons[10]) wButtons |= 0x0040; // LS
                                if (diButtons[11]) wButtons |= 0x0080; // RS
                            }

                            if (povs != null && povs.Length > 0 && povs[0] != -1)
                            {
                                int pov = povs[0];
                                if (pov == 0) wButtons |= 0x0001; // Up
                                else if (pov == 4500) { wButtons |= 0x0001; wButtons |= 0x0008; } // Up-Right
                                else if (pov == 9000) wButtons |= 0x0008; // Right
                                else if (pov == 13500) { wButtons |= 0x0002; wButtons |= 0x0008; } // Down-Right
                                else if (pov == 18000) wButtons |= 0x0002; // Down
                                else if (pov == 22500) { wButtons |= 0x0002; wButtons |= 0x0004; } // Down-Left
                                else if (pov == 27000) wButtons |= 0x0004; // Left
                                else if (pov == 31500) { wButtons |= 0x0001; wButtons |= 0x0004; } // Up-Left
                            }

                            hasInput = true;
                        }
                    }

                    if (hasInput || _aimLeftOffsetX != 0 || _aimLeftOffsetY != 0 || _aimRightOffsetX != 0 || _aimRightOffsetY != 0)
                    {
                        // Check if user wants buttons passed through (default to true)
                        bool passButtons = !Dictionary.toggleState.TryGetValue("Pass Buttons/Triggers", out var btnVal) || (btnVal is bool bPass && bPass);
                        if (!passButtons)
                        {
                            wButtons = 0;
                            bLeftTrigger = 0;
                            bRightTrigger = 0;
                        }

                        // Apply aim offset (decays after a short time if AI stops sending updates)
                        int aimLeftX = 0, aimLeftY = 0;
                        int aimRightX = 0, aimRightY = 0;
                        if (System.Diagnostics.Stopwatch.GetTimestamp() - _lastAimTick < TimeSpan.TicksPerMillisecond * 50)
                        {
                            aimLeftX = _aimLeftOffsetX;
                            aimLeftY = _aimLeftOffsetY;
                            aimRightX = _aimRightOffsetX;
                            aimRightY = _aimRightOffsetY;
                        }
                        
                        short finalLX = (short)Math.Clamp(sThumbLX + aimLeftX, -32767, 32767);
                        short finalLY = (short)Math.Clamp(sThumbLY + aimLeftY, -32767, 32767);
                        short finalRX = (short)Math.Clamp(sThumbRX + aimRightX, -32767, 32767);
                        short finalRY = (short)Math.Clamp(sThumbRY + aimRightY, -32767, 32767);

                        if (_ds4Gamepad != null)
                        {
                            // DS4 requires 0-255 mapping for sticks and triggers
                            _ds4Gamepad.SetAxisValue(Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4Axis.LeftThumbX, (byte)((finalLX + 32768) / 257));
                            _ds4Gamepad.SetAxisValue(Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4Axis.LeftThumbY, (byte)(255 - ((finalLY + 32768) / 257))); // Inverted Y for DS4
                            _ds4Gamepad.SetAxisValue(Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4Axis.RightThumbX, (byte)((finalRX + 32768) / 257));
                            _ds4Gamepad.SetAxisValue(Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4Axis.RightThumbY, (byte)(255 - ((finalRY + 32768) / 257))); // Inverted Y for DS4
                            _ds4Gamepad.SetSliderValue(Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4Slider.LeftTrigger, bLeftTrigger);
                            _ds4Gamepad.SetSliderValue(Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4Slider.RightTrigger, bRightTrigger);
                            
                            // Map buttons (approximate XInput to DS4)
                            _ds4Gamepad.SetButtonState(Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4Button.Cross, (wButtons & 0x1000) != 0); // A
                            _ds4Gamepad.SetButtonState(Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4Button.Circle, (wButtons & 0x2000) != 0); // B
                            _ds4Gamepad.SetButtonState(Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4Button.Square, (wButtons & 0x4000) != 0); // X
                            _ds4Gamepad.SetButtonState(Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4Button.Triangle, (wButtons & 0x8000) != 0); // Y
                            _ds4Gamepad.SetButtonState(Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4Button.ShoulderLeft, (wButtons & 0x0100) != 0); // LB
                            _ds4Gamepad.SetButtonState(Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4Button.ShoulderRight, (wButtons & 0x0200) != 0); // RB
                            _ds4Gamepad.SetButtonState(Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4Button.ThumbLeft, (wButtons & 0x0040) != 0); // LS
                            _ds4Gamepad.SetButtonState(Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4Button.ThumbRight, (wButtons & 0x0080) != 0); // RS
                            _ds4Gamepad.SetButtonState(Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4Button.Share, (wButtons & 0x0020) != 0); // Back
                            _ds4Gamepad.SetButtonState(Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4Button.Options, (wButtons & 0x0010) != 0); // Start
                            
                            bool fullPass = Dictionary.toggleState.TryGetValue("Full Input Passthrough (Beta)", out var valPass) && valPass is bool bPass2 && bPass2;
                            if (fullPass && diButtons != null && diButtons.Length >= 14 && passButtons)
                            {
                                _ds4Gamepad.SetButtonState(Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4SpecialButton.Ps, diButtons[12]);
                                _ds4Gamepad.SetButtonState(Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4SpecialButton.Touchpad, diButtons[13]);
                            }
                            else
                            {
                                _ds4Gamepad.SetButtonState(Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4SpecialButton.Ps, false);
                                _ds4Gamepad.SetButtonState(Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4SpecialButton.Touchpad, false);
                            }

                            // D-Pad
                            Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4DPadDirection dpad = Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4DPadDirection.None;
                            bool up = (wButtons & 0x0001) != 0;
                            bool down = (wButtons & 0x0002) != 0;
                            bool left = (wButtons & 0x0004) != 0;
                            bool right = (wButtons & 0x0008) != 0;
                            
                            if (up && right) dpad = Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4DPadDirection.Northeast;
                            else if (up && left) dpad = Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4DPadDirection.Northwest;
                            else if (down && right) dpad = Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4DPadDirection.Southeast;
                            else if (down && left) dpad = Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4DPadDirection.Southwest;
                            else if (up) dpad = Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4DPadDirection.North;
                            else if (down) dpad = Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4DPadDirection.South;
                            else if (left) dpad = Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4DPadDirection.West;
                            else if (right) dpad = Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4DPadDirection.East;
                            
                            _ds4Gamepad.SetDPadDirection(dpad);
                            _ds4Gamepad.SubmitReport();
                        }
                        else if (_gamepad != null)
                        {
                            _gamepad.LeftThumbX = finalLX;
                            _gamepad.LeftThumbY = finalLY;
                            _gamepad.RightThumbX = finalRX;
                            _gamepad.RightThumbY = finalRY;
                            _gamepad.LeftTrigger = bLeftTrigger;
                            _gamepad.RightTrigger = bRightTrigger;
                            _gamepad.SetButtonState(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.A, (wButtons & 0x1000) != 0);
                            _gamepad.SetButtonState(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.B, (wButtons & 0x2000) != 0);
                            _gamepad.SetButtonState(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.X, (wButtons & 0x4000) != 0);
                            _gamepad.SetButtonState(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Y, (wButtons & 0x8000) != 0);
                            _gamepad.SetButtonState(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.LeftShoulder, (wButtons & 0x0100) != 0);
                            _gamepad.SetButtonState(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.RightShoulder, (wButtons & 0x0200) != 0);
                            _gamepad.SetButtonState(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.LeftThumb, (wButtons & 0x0040) != 0);
                            _gamepad.SetButtonState(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.RightThumb, (wButtons & 0x0080) != 0);
                            _gamepad.SetButtonState(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Start, (wButtons & 0x0010) != 0);
                            _gamepad.SetButtonState(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Back, (wButtons & 0x0020) != 0);
                            _gamepad.SetButtonState(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Up, (wButtons & 0x0001) != 0);
                            _gamepad.SetButtonState(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Down, (wButtons & 0x0002) != 0);
                            _gamepad.SetButtonState(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Left, (wButtons & 0x0004) != 0);
                            _gamepad.SetButtonState(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Right, (wButtons & 0x0008) != 0);
                            _gamepad.SubmitReport();
                        }
                    }
                }
                catch { }

                await Task.Delay(4, token); // ~250 Hz poll rate for smooth passthrough
            }
        }

        private static long _lastUpdateTimestamp = 0;
        private static readonly object _updateLock = new object();

        /// <summary>
        /// Dynamic update interval from settings slider (10Hz - 125Hz)
        /// </summary>
        private static long UpdateIntervalTicks
        {
            get
            {
                if (Dictionary.sliderSettings != null && Dictionary.sliderSettings.ContainsKey("AI Output FPS"))
                {
                    double fps = Dictionary.sliderSettings["AI Output FPS"];
                    if (fps >= 10 && fps <= 125)
                    {
                        return (long)Math.Round((1000.0 / fps) * TimeSpan.TicksPerMillisecond);
                    }
                }
                return (long)Math.Round((1000.0 / 60.0) * TimeSpan.TicksPerMillisecond);
            }
        }

        /// <summary>
        /// Set virtual aim stick position
        /// Input: x, y in -150 to 150 range (from MouseManager)
        /// Output: XInput range -32767 to 32767
        /// </summary>
        public static void SetAimStick(int x, int y)
        {
            if (!_isConnected) return;

            lock (_updateLock)
            {
                try
                {
                    long now = System.Diagnostics.Stopwatch.GetTimestamp();
                    long elapsed = now - _lastUpdateTimestamp;
                    long elapsedTicks = (long)(elapsed * 10000000.0 / System.Diagnostics.Stopwatch.Frequency);

                    if (elapsedTicks < UpdateIntervalTicks)
                    {
                        return;
                    }

                    short thumbX = (short)Math.Clamp((int)Math.Round(x * (32767.0 / 150.0)), -32767, 32767);
                    short thumbY = (short)Math.Clamp((int)Math.Round(-y * (32767.0 / 150.0)), -32767, 32767); // Invert Y-axis for controller

                    bool useLeftStick = Dictionary.toggleState.TryGetValue("Virtual Controller Left Stick Aim", out var val) && val is bool b && b;

                    if (useLeftStick)
                    {
                        _aimLeftOffsetX = thumbX;
                        _aimLeftOffsetY = thumbY;
                        _aimRightOffsetX = 0;
                        _aimRightOffsetY = 0;
                    }
                    else
                    {
                        _aimRightOffsetX = thumbX;
                        _aimRightOffsetY = thumbY;
                        _aimLeftOffsetX = 0;
                        _aimLeftOffsetY = 0;
                    }
                    _lastAimTick = System.Diagnostics.Stopwatch.GetTimestamp();

                    _lastUpdateTimestamp = now;
                }
                catch (Exception ex)
                {
                    LogManager.Log(LogManager.LogLevel.Error, $"SetAimStick error: {ex.Message}", false);
                    Disconnect();
                }
            }
        }

        /// <summary>
        /// Set virtual left stick position
        /// </summary>
        public static void SetLeftStick(int x, int y)
        {
            if (!_isConnected || _gamepad == null) return;

            try
            {
                _gamepad.LeftThumbX = (short)Math.Clamp((int)Math.Round(x * (32767.0 / 150.0)), -32767, 32767);
                _gamepad.LeftThumbY = (short)Math.Clamp((int)Math.Round(y * (32767.0 / 150.0)), -32767, 32767);
                _gamepad.SubmitReport();
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"SetLeftStick error: {ex.Message}", false);
            }
        }

        /// <summary>
        /// Set virtual right trigger (0-255)
        /// </summary>
        public static void SetRightTrigger(byte value)
        {
            if (!_isConnected) return;

            try
            {
                if (_gamepad != null)
                {
                    _gamepad.RightTrigger = value;
                    _gamepad.SubmitReport();
                }
                if (_ds4Gamepad != null)
                {
                    _ds4Gamepad.SetSliderValue(Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4Slider.RightTrigger, value);
                    // Also set R2 button state for DS4 if fully pressed
                    _ds4Gamepad.SetButtonState(Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4Button.TriggerRight, value > 0);
                    _ds4Gamepad.SubmitReport();
                }
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"SetRightTrigger error: {ex.Message}", false);
            }
        }

        /// <summary>
        /// Press a button on the virtual controller
        /// </summary>
        public static void PressButton(Xbox360Button button)
        {
            if (!_isConnected || _gamepad == null) return;

            try
            {
                _gamepad.SetButtonState(button, true);
                _gamepad.SubmitReport();
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"PressButton error: {ex.Message}", false);
            }
        }

        /// <summary>
        /// Release a button on the virtual controller
        /// </summary>
        public static void ReleaseButton(Xbox360Button button)
        {
            if (!_isConnected || _gamepad == null) return;

            try
            {
                _gamepad.SetButtonState(button, false);
                _gamepad.SubmitReport();
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"ReleaseButton error: {ex.Message}", false);
            }
        }

        /// <summary>
        /// Reset all sticks and buttons to neutral
        /// </summary>
        public static void ResetAll()
        {
            if (!_isConnected) return;
            try
            {
                if (_gamepad != null)
                {
                    _gamepad.LeftThumbX = 0;
                    _gamepad.LeftThumbY = 0;
                    _gamepad.RightThumbX = 0;
                    _gamepad.RightThumbY = 0;
                    _gamepad.LeftTrigger = 0;
                    _gamepad.RightTrigger = 0;
                    // Reset all buttons
                    foreach (Xbox360Button btn in Enum.GetValues(typeof(Xbox360Button)))
                    {
                        _gamepad.SetButtonState(btn, false);
                    }
                    _gamepad.SubmitReport();
                }
                
                if (_ds4Gamepad != null)
                {
                    _ds4Gamepad.SetAxisValue(Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4Axis.LeftThumbX, 128);
                    _ds4Gamepad.SetAxisValue(Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4Axis.LeftThumbY, 128);
                    _ds4Gamepad.SetAxisValue(Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4Axis.RightThumbX, 128);
                    _ds4Gamepad.SetAxisValue(Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4Axis.RightThumbY, 128);
                    _ds4Gamepad.SetSliderValue(Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4Slider.LeftTrigger, 0);
                    _ds4Gamepad.SetSliderValue(Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4Slider.RightTrigger, 0);
                    // Reset all buttons
                    foreach (Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4Button btn in Enum.GetValues(typeof(Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4Button)))
                    {
                        _ds4Gamepad.SetButtonState(btn, false);
                    }
                    _ds4Gamepad.SetDPadDirection(Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4DPadDirection.None);
                    _ds4Gamepad.SubmitReport();
                }
            }
            catch { }
        }

        public static void Disconnect()
        {
            _isConnected = false;
            
            if (_cancellationTokenSource != null)
            {
                try { _cancellationTokenSource.Cancel(); } catch { }
            }

            try { ResetAll(); } catch { }

            try
            {
                if (_gamepad != null) { _gamepad.Disconnect(); _gamepad = null; }
                if (_ds4Gamepad != null) { _ds4Gamepad.Disconnect(); _ds4Gamepad = null; }
                if (_gamepad2 != null) { _gamepad2.Disconnect(); _gamepad2 = null; }
                if (_ds4Gamepad2 != null) { _ds4Gamepad2.Disconnect(); _ds4Gamepad2 = null; }

                if (_client != null) { _client.Dispose(); _client = null; }
                if (_client2 != null) { _client2.Dispose(); _client2 = null; }
            }
            catch { }
        }
    }
}