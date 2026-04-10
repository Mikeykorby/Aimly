using SharpDX.DirectInput;
using Aimmy2.Class;
using Other;
using System;
using System.Linq;

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
        private static DirectInputMain? _instance;
        public static DirectInputMain Instance => _instance ??= new DirectInputMain();

        private DirectInput? _directInput;
        private Joystick? _joystick;
        private JoystickState? _state;

        public bool IsConnected { get; private set; }
        public int CurrentRightStickX { get; private set; }
        public int CurrentRightStickY { get; private set; }
        public int CurrentLeftStickX { get; private set; }
        public int CurrentLeftStickY { get; private set; }
        public int CurrentRightTrigger { get; private set; }
        public int CurrentLeftTrigger { get; private set; }

        // Deadzone constants (same as XInput)
        private const int LEFT_THUMB_DEADZONE = 7849;
        private const int RIGHT_THUMB_DEADZONE = 8689;

        // Calibration: sample initial position to detect axis direction
        private int _calibrationRightStickXZero = 0;
        private int _calibrationRightStickYZero = 0;
        private bool _isCalibrated = false;

        /// <summary>
        /// Initialize DirectInput and find a connected controller
        /// </summary>
        public bool Load()
        {
            try
            {
                _directInput = new DirectInput();

                // Find all connected joysticks/gamepads
                var devices = _directInput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly);

                if (devices.Count == 0)
                {
                    // Try all game devices including joysticks
                    devices = _directInput.GetDevices(DeviceType.Joystick, DeviceEnumerationFlags.AttachedOnly);
                }

                if (devices.Count == 0)
                {
                    LogManager.Log(LogManager.LogLevel.Warning, "No DirectInput controller detected. Make sure your PS4/PS5 controller is connected via USB or Bluetooth.", true);
                    return false;
                }

                // Create joystick from first device
                var deviceInstance = devices.First();
                _joystick = new Joystick(_directInput, deviceInstance.InstanceGuid);
                _joystick.Acquire();

                IsConnected = true;

                // Calibrate: sample initial position for ~100ms to get center values
                CalibrateController();

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

        /// <summary>
        /// Calibrate controller by sampling center position
        /// </summary>
        private void CalibrateController()
        {
            if (!IsConnected || _joystick == null) return;

            try
            {
                // Sample a few times and average to get stable center
                int sumX = 0, sumY = 0, samples = 0;

                for (int i = 0; i < 5; i++)
                {
                    System.Threading.Thread.Sleep(20);
                    _joystick.Poll();
                    var state = _joystick.GetCurrentState();
                    if (state != null)
                    {
                        // PS4/PS5 native: Right stick is typically RotationZ (X) and Z (Y)
                        sumX += state.RotationZ;
                        sumY += state.Z;
                        samples++;
                    }
                }

                if (samples > 0)
                {
                    _calibrationRightStickXZero = sumX / samples;
                    _calibrationRightStickYZero = sumY / samples;
                    _isCalibrated = true;

                    LogManager.Log(LogManager.LogLevel.Info, 
                        $"Controller calibrated: center=({_calibrationRightStickXZero}, {_calibrationRightStickYZero})", false);
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
                    CurrentLeftStickX = _state.X;
                    CurrentLeftStickY = _state.Y;

                    // Right Stick: RotationZ (X axis), Z (Y axis) for PS4/PS5 native
                    int rawX = _state.RotationZ;
                    int rawY = _state.Z;

                    // Apply calibration offset
                    if (_isCalibrated)
                    {
                        CurrentRightStickX = rawX - _calibrationRightStickXZero;
                        CurrentRightStickY = rawY - _calibrationRightStickYZero;
                    }
                    else
                    {
                        CurrentRightStickX = rawX;
                        CurrentRightStickY = rawY;
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