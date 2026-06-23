using SharpDX.DirectInput;
using Aimmy2.Class;
using Other;
using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace MouseMovementLibraries.DirectInputSupport
{
    /// <summary>
    /// DirectInput controller support for Aimmy.
    /// Supports PS4, PS5, and other DirectInput controllers without emulation.
    /// 
    /// Axis mapping note: Different PS4/PS5 drivers report axes differently:
    /// - Windows native HID driver: LeftStick=(X,Y), RightStick=(Z,Rz), Triggers=(Rx,Ry)
    /// - DS4Windows: May remap to emulate XInput
    /// 
    /// This class uses the native HID axis mapping and auto-detects axis direction.
    /// </summary>
    internal class DirectInputMain
    {
        public bool RequireExclusiveMode 
        { 
            get 
            {
                return Aimmy2.Class.Dictionary.toggleState.TryGetValue("Hide Real Controller", out var val) && val is bool b && b;
            }
        }

        private static DirectInputMain? _instance;
        public static DirectInputMain Instance => _instance ??= new DirectInputMain();

        private DirectInput? _directInput;
        private Joystick? _joystick;
        private JoystickState? _state;

        public static Guid? TargetGuid { get; set; } = null;

        public bool IsConnected { get; private set; }
        public int CurrentRightStickX { get; private set; }
        public int CurrentRightStickY { get; private set; }
        public int CurrentLeftStickX { get; private set; }
        public int CurrentLeftStickY { get; private set; }
        public int CurrentRightTrigger { get; private set; }
        public int CurrentLeftTrigger { get; private set; }
        public string ProductName { get; private set; } = string.Empty;

        // Deadzone constants (same as XInput)
        private const int LEFT_THUMB_DEADZONE = 7849;
        private const int RIGHT_THUMB_DEADZONE = 8689;

        // Calibration: sample initial position to detect axis direction
        private int _calibrationLeftStickXZero = 32768;
        private int _calibrationLeftStickYZero = 32768;
        private int _calibrationRightStickXZero = 32768;
        private int _calibrationRightStickYZero = 32768;
        private bool _isCalibrated = false;

        private DirectInputMain()
        {
            _directInput = new DirectInput();
        }

        public void Unload()
        {
            if (_joystick != null)
            {
                try
                {
                    _joystick.Unacquire();
                    _joystick.Dispose();
                }
                catch { }
                _joystick = null;
            }
            IsConnected = false;
        }

        /// <summary>
        /// Initialize DirectInput and find a connected controller
        /// For PS5/PS4 controllers, DS4Windows or DualSenseY must be running to emulate an Xbox/ViGEm controller
        /// </summary>
        public bool Load(bool showNotification = true)
        {
            try
            {
                Unload();

                // Find all connected joysticks/gamepads
                var devices = _directInput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly);

                if (devices.Count == 0)
                {
                    // Try all game devices including joysticks
                    devices = _directInput.GetDevices(DeviceType.Joystick, DeviceEnumerationFlags.AttachedOnly);
                }

                if (devices.Count == 0)
                {
                if (showNotification)
                {
                    LogManager.Log(LogManager.LogLevel.Warning,
                        "No DirectInput controller detected.\n\n" +
                        "For PS5/PS4 controllers:\n" +
                        "1. Install DS4Windows (https://ds4-windows.com) or DualSenseY (https://github.com/WujekFoliarz/DualSenseY)\n" +
                        "2. Start DS4Windows/DualSenseY and connect your controller\n" +
                        "3. Make sure DS4Windows/DualSenseY is running in the background\n" +
                        "4. Select 'DirectInput (PS4/PS5 Controller)' again", true);
                }
                return false;
                }

                // Filter by target guid if specified
                if (TargetGuid.HasValue)
                {
                    devices = devices.Where(d => d.InstanceGuid == TargetGuid.Value).ToList();
                    if (devices.Count == 0)
                    {
                        LogManager.Log(LogManager.LogLevel.Warning, $"DirectInput Target Controller (GUID {TargetGuid.Value}) is not connected.", true);
                        return false;
                    }
                }

                // Create joystick from first device
                var deviceInstance = devices.First();
                _joystick = new Joystick(_directInput, deviceInstance.InstanceGuid);

                IntPtr handle = IntPtr.Zero;
                if (System.Windows.Application.Current != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => {
                        var win = System.Windows.Application.Current.MainWindow;
                        if (win != null)
                            handle = new System.Windows.Interop.WindowInteropHelper(win).Handle;
                    });
                }

                if (handle != IntPtr.Zero)
                {
                    try
                    {
                        if (RequireExclusiveMode)
                            _joystick.SetCooperativeLevel(handle, CooperativeLevel.Exclusive | CooperativeLevel.Background);
                        else
                            _joystick.SetCooperativeLevel(handle, CooperativeLevel.NonExclusive | CooperativeLevel.Background);
                    }
                    catch { }
                }

                _joystick.Acquire();

                IsConnected = true;

                // Calibrate: sample initial position for ~100ms to get center values
                CalibrateController();

                ProductName = deviceInstance.ProductName;

                LogManager.Log(LogManager.LogLevel.Info, $"DirectInput controller connected: {deviceInstance.ProductName}", false);
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"DirectInput load error: {ex.Message}", true);
                IsConnected = false;
                return false;
            }
        }

        public System.Collections.Generic.Dictionary<string, string> GetAvailableControllers()
        {
            var list = new System.Collections.Generic.Dictionary<string, string>();
            try
            {
                var directInput = new DirectInput();
                var devices = directInput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly);
                if (devices.Count == 0)
                {
                    devices = directInput.GetDevices(DeviceType.Joystick, DeviceEnumerationFlags.AttachedOnly);
                }

                foreach (var device in devices)
                {
                    list.Add($"dinput_{device.InstanceGuid}", $"DirectInput: {device.ProductName}");
                }
            }
            catch { }
            return list;
        }

        /// <summary>
        /// Calibrate controller by sampling center position
        /// </summary>
        private void CalibrateController()
        {
            if (!IsConnected || _joystick == null) return;

            try
            {
                // Sample a few times and average to get stable center
                int sumLX = 0, sumLY = 0, sumRX = 0, sumRY = 0, samples = 0;

                for (int i = 0; i < 5; i++)
                {
                    System.Threading.Thread.Sleep(20);
                    _joystick.Poll();
                    var state = _joystick.GetCurrentState();
                    if (state != null)
                    {
                        sumLX += state.X;
                        sumLY += state.Y;
                        // PS4/PS5 native: Right stick is typically Z (X) and RotationZ (Y)
                        sumRX += state.Z;
                        sumRY += state.RotationZ;
                        samples++;
                    }
                }

                if (samples > 0)
                {
                    _calibrationLeftStickXZero = sumLX / samples;
                    _calibrationLeftStickYZero = sumLY / samples;
                    _calibrationRightStickXZero = sumRX / samples;
                    _calibrationRightStickYZero = sumRY / samples;
                    _isCalibrated = true;

                    LogManager.Log(LogManager.LogLevel.Info, 
                        $"Controller calibrated: L=({_calibrationLeftStickXZero}, {_calibrationLeftStickYZero}) R=({_calibrationRightStickXZero}, {_calibrationRightStickYZero})", false);
                }
            }
            catch
            {
                _isCalibrated = false;
            }
        }

        /// <summary>
        /// Poll controller state and update stick positions
        /// </summary>
        public void PollState()
        {
            if (!IsConnected || _joystick == null) return;

            try
            {
                _joystick.Poll();
                _state = _joystick.GetCurrentState();

                if (_state != null)
                {
                    // Map DirectInput axes to stick positions
                    // Left Stick: X, Y
                    int rawLX = _state.X;
                    int rawLY = _state.Y;

                    // Right Stick: Z (X axis), RotationZ (Y axis) for PS4/PS5 native
                    int rawRX = _state.Z;
                    int rawRY = _state.RotationZ;

                    // Apply calibration offset
                    if (_isCalibrated)
                    {
                        CurrentLeftStickX = rawLX - _calibrationLeftStickXZero;
                        CurrentLeftStickY = rawLY - _calibrationLeftStickYZero;
                        CurrentRightStickX = rawRX - _calibrationRightStickXZero;
                        CurrentRightStickY = rawRY - _calibrationRightStickYZero;
                    }
                    else
                    {
                        CurrentLeftStickX = rawLX - 32768;
                        CurrentLeftStickY = rawLY - 32768;
                        CurrentRightStickX = rawRX - 32768;
                        CurrentRightStickY = rawRY - 32768;
                    }

                    // Triggers: RotationX (Left), RotationY (Right)
                    CurrentLeftTrigger = Math.Max(0, _state.RotationX);
                    CurrentRightTrigger = Math.Max(0, _state.RotationY);
                }
            }
            catch
            {
                // Polling failed, controller may have been disconnected
                IsConnected = false;
            }
        }

        /// <summary>
        /// Get right stick position with deadzone applied
        /// Returns normalized value in range approximately -32767 to 32767
        /// </summary>
        public (int x, int y) GetRightStickWithDeadzone()
        {
            PollState();

            int x = CurrentRightStickX;
            int y = CurrentRightStickY;

            // Apply deadzone
            if (Math.Abs(x) < RIGHT_THUMB_DEADZONE) x = 0;
            if (Math.Abs(y) < RIGHT_THUMB_DEADZONE) y = 0;

            return (x, y);
        }

        /// <summary>
        /// Get right stick position normalized to match AI aim scale (-150 to 150)
        /// This allows combining user stick input with AI aim movement.
        /// </summary>
        public (int x, int y) GetRightStickNormalized()
        {
            var (rawX, rawY) = GetRightStickWithDeadzone();

            // Scale from -32767..32767 to -150..150 to match MouseManager's output range
            int normalizedX = (int)((rawX / 32767.0) * 150);
            int normalizedY = (int)((rawY / 32767.0) * 150);

            return (normalizedX, normalizedY);
        }

        /// <summary>
        /// Get left stick position with deadzone applied
        /// </summary>
        public (int x, int y) GetLeftStickWithDeadzone()
        {
            PollState();

            int x = CurrentLeftStickX;
            int y = CurrentLeftStickY;

            // Apply deadzone
            if (Math.Abs(x) < LEFT_THUMB_DEADZONE) x = 0;
            if (Math.Abs(y) < LEFT_THUMB_DEADZONE) y = 0;

            return (x, y);
        }

        /// <summary>
        /// Get left stick position normalized to match AI aim scale (-150 to 150)
        /// </summary>
        public (int x, int y) GetLeftStickNormalized()
        {
            var (rawX, rawY) = GetLeftStickWithDeadzone();

            int normalizedX = (int)((rawX / 32767.0) * 150);
            int normalizedY = (int)((rawY / 32767.0) * 150);

            return (normalizedX, normalizedY);
        }

        public bool[]? GetButtons()
        {
            return _state?.Buttons;
        }

        public int[]? GetPOV()
        {
            return _state?.PointOfViewControllers;
        }

        /// <summary>
        /// Check if right trigger is pressed (threshold > 50%)
        /// </summary>
        public bool IsRightTriggerPressed()
        {
            PollState();
            return CurrentRightTrigger > 128;
        }

        /// <summary>
        /// Get right trigger value (0-255)
        /// </summary>
        public int GetRightTriggerValue()
        {
            PollState();
            return CurrentRightTrigger;
        }

        /// <summary>
        /// Set force feedback (vibration) on the controller
        /// Uses DirectInput SendForceFeedbackCommand for simple vibration support
        /// Note: Most PS4/PS5 controllers don't support force feedback via standard DirectInput
        /// Use DS4Windows (ds4windows.com) or DualSenseY (github.com/WujekFoliarz/DualSenseY) for full vibration support
        /// </summary>
        /// <param name="leftMotor">Left motor intensity (0-65535)</param>
        /// <param name="rightMotor">Right motor intensity (0-65535)</param>
        public void Vibrate(int leftMotor = 0, int rightMotor = 0)
        {
            if (!IsConnected || _joystick == null) return;

            try
            {
                // Check if the device supports force feedback
                if (!_joystick.Capabilities.Flags.HasFlag(DeviceFlags.ForceFeedback))
                {
                    // Force feedback not supported on this device via DirectInput
                    // This is normal for PS5 controllers - they need DS4Windows/DualSenseY
                    LogManager.Log(LogManager.LogLevel.Info,
                        "Standard DirectInput force feedback not supported by this controller.\n" +
                        "For PS5/PS4 vibration, use DS4Windows or DualSenseY:\n" +
                        "- DS4Windows: https://ds4-windows.com\n" +
                        "- DualSenseY: https://github.com/WujekFoliarz/DualSenseY", false);
                    return;
                }

                // Stop any existing effects first
                try
                {
                    _joystick.SendForceFeedbackCommand(ForceFeedbackCommand.StopAll);
                }
                catch { }

                // Only vibrate if there's motor input
                if (leftMotor > 0 || rightMotor > 0)
                {
                    try
                    {
                        int intensity = (leftMotor + rightMotor) / 2;

                        // Create ramp force effect
                        var rampForce = new RampForce
                        {
                            Start = intensity,
                            End = intensity
                        };

                        // Set up effect parameters
                        var effectParams = new EffectParameters
                        {
                            Duration = int.MaxValue,
                            SamplePeriod = int.MaxValue
                        };

                        // Create and start the effect
                        var effect = new Effect(_joystick, EffectGuid.RampForce, effectParams);

                        // Set the ramp force data
                        effect.Download();
                        effect.Start();  // Start the effect so vibration actually works

                        LogManager.Log(LogManager.LogLevel.Info,
                            $"Controller vibration: Active (intensity: {intensity})", false);
                    }
                    catch (Exception ex)
                    {
                        LogManager.Log(LogManager.LogLevel.Warning, $"Could not create vibration effect: {ex.Message}", false);
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Warning, $"Vibration error: {ex.Message}", false);
            }
        }

        /// <summary>
        /// Check if controller supports force feedback
        /// </summary>
        public bool SupportsVibration()
        {
            if (!IsConnected || _joystick == null) return false;
            return _joystick.Capabilities.Flags.HasFlag(DeviceFlags.ForceFeedback);
        }

        private const int DS4WINDOWS_UDP_PORT = 26760;
        private const int DUALSENSEY_V2_UDP_PORT = 6969;

        private async Task SendDS4UDP_VibrationAsync(int smallMotor, int largeMotor, int port)
        {
            try
            {
                using var udpClient = new UdpClient();
                var endpoint = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, port);

                byte[] packet = new byte[78];
                packet[0] = 0x01;
                packet[1] = (byte)smallMotor;
                packet[2] = (byte)largeMotor;

                await udpClient.SendAsync(packet, packet.Length, endpoint);
            }
            catch
            {
            }
        }

        public async Task VibrateUDPAsync(int smallMotor = 0, int largeMotor = 0)
        {
            smallMotor = Math.Min(255, Math.Max(0, smallMotor));
            largeMotor = Math.Min(255, Math.Max(0, largeMotor));

            if (smallMotor <= 0 && largeMotor <= 0)
            {
                await SendDS4UDP_VibrationAsync(0, 0, DS4WINDOWS_UDP_PORT);
                await SendDS4UDP_VibrationAsync(0, 0, DUALSENSEY_V2_UDP_PORT);
                return;
            }

            await SendDS4UDP_VibrationAsync(smallMotor, largeMotor, DS4WINDOWS_UDP_PORT);
            await SendDS4UDP_VibrationAsync(smallMotor, largeMotor, DUALSENSEY_V2_UDP_PORT);

            await Task.Delay(200);

            await SendDS4UDP_VibrationAsync(0, 0, DS4WINDOWS_UDP_PORT);
            await SendDS4UDP_VibrationAsync(0, 0, DUALSENSEY_V2_UDP_PORT);
        }

        /// <summary>
        /// Disconnect and cleanup
        /// </summary>
        public void Disconnect()
        {
            if (IsConnected)
            {
                _joystick?.Unacquire();
                _joystick?.Dispose();
                _joystick = null;
                _directInput?.Dispose();
                _directInput = null;
                IsConnected = false;
                LogManager.Log(LogManager.LogLevel.Info, "DirectInput controller disconnected", false);
            }
        }
    }
}