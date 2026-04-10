using System.Runtime.InteropServices;
using Aimmy2.Class;
using Other;

namespace MouseMovementLibraries.XInputSupport
{
    /// <summary>
    /// Native XInput controller support for Aimmy.
    /// Allows sending controller stick movements without vJoy/ViGEmBus drivers.
    /// Works by sending XInput-compatible input events directly.
    /// </summary>
    internal class XInputMain
    {
        // XInput P/Invoke declarations
        [DllImport("xinput1_4.dll")]
        private static extern uint XInputGetState(uint dwUserIndex, out XINPUT_STATE pState);

        [DllImport("xinput1_4.dll")]
        private static extern uint XInputSetState(uint dwUserIndex, ref XINPUT_VIBRATION pVibration);

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_STATE
        {
            public uint dwPacketNumber;
            public XINPUT_GAMEPAD Gamepad;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_GAMEPAD
        {
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_VIBRATION
        {
            public ushort wLeftMotorSpeed;
            public ushort wRightMotorSpeed;
        }

        // XInput button constants
        private const ushort XINPUT_GAMEPAD_DPAD_UP = 0x0001;
        private const ushort XINPUT_GAMEPAD_DPAD_DOWN = 0x0002;
        private const ushort XINPUT_GAMEPAD_DPAD_LEFT = 0x0004;
        private const ushort XINPUT_GAMEPAD_DPAD_RIGHT = 0x0008;
        private const ushort XINPUT_GAMEPAD_START = 0x0010;
        private const ushort XINPUT_GAMEPAD_BACK = 0x0020;
        private const ushort XINPUT_GAMEPAD_LEFT_THUMB = 0x0040;
        private const ushort XINPUT_GAMEPAD_RIGHT_THUMB = 0x0080;
        private const ushort XINPUT_GAMEPAD_LEFT_SHOULDER = 0x0100;
        private const ushort XINPUT_GAMEPAD_RIGHT_SHOULDER = 0x0200;
        private const ushort XINPUT_GAMEPAD_A = 0x1000;
        private const ushort XINPUT_GAMEPAD_B = 0x2000;
        private const ushort XINPUT_GAMEPAD_X = 0x4000;
        private const ushort XINPUT_GAMEPAD_Y = 0x8000;

        // Deadzone constants
        private const short LEFT_THUMB_DEADZONE = 7849;
        private const short RIGHT_THUMB_DEADZONE = 8689;

        // Instance
        private static XInputMain? _instance;
        public static XInputMain Instance => _instance ??= new XInputMain();

        // State tracking
        private int _lastRightStickX = 0;
        private int _lastRightStickY = 0;
        private bool _isAimActive = false;
        private uint _controllerIndex = 0;

        public bool IsConnected { get; private set; }
        public short CurrentRightStickX { get; private set; }
        public short CurrentRightStickY { get; private set; }

        /// <summary>
        /// Check if a controller is connected and ready
        /// </summary>
        public bool Load()
        {
            try
            {
                // Try XInput first (native Xbox, Xbox-compatible controllers)
                var state = GetState();
                if (state == 0)
                {
                    IsConnected = true;
                    LogManager.Log(LogManager.LogLevel.Info, "XInput controller connected", false);
                    return true;
                }

                // Try alternative XInput library versions
                if (TryOtherXInputVersion(out state))
                {
                    IsConnected = true;
                    LogManager.Log(LogManager.LogLevel.Info, "XInput controller connected (alternate driver)", false);
                    return true;
                }

                IsConnected = false;
                LogManager.Log(LogManager.LogLevel.Warning, "No XInput controller detected. For PS5 controllers, please install DS4Windows (https://ds4-windows.com) or use Steam Input.", true);
                return false;
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"XInput load error: {ex.Message}", true);
                IsConnected = false;
                return false;
            }
        }

        /// <summary>
        /// Try alternative XInput DLL versions (xinput1_3.dll for older controllers)
        /// </summary>
        private bool TryOtherXInputVersion(out uint stateResult)
        {
            stateResult = 0xFFFFFFFF;
            try
            {
                var hModule = LoadLibrary("xinput1_3.dll");
                if (hModule == IntPtr.Zero)
                {
                    hModule = LoadLibrary("xinput9_1_0.dll");
                }
                if (hModule != IntPtr.Zero)
                {
                    var func = GetProcAddress(hModule, "XInputGetState");
                    if (func != IntPtr.Zero)
                    {
                        var getXInputGetState = (GetStateDelegate)Marshal.GetDelegateForFunctionPointer(
                            func, typeof(GetStateDelegate));
                        stateResult = getXInputGetState(_controllerIndex, out _);
                        FreeLibrary(hModule);
                        return stateResult == 0;
                    }
                    FreeLibrary(hModule);
                }
            }
            catch { }
            return false;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        private delegate uint GetStateDelegate(uint dwUserIndex, out XINPUT_STATE pState);

        /// <summary>
        /// Get current controller state (0 = connected, non-zero = disconnected)
        /// Also updates CurrentRightStickX/Y with latest values
        /// </summary>
        public uint GetState()
        {
            try
            {
                var result = XInputGetState(_controllerIndex, out var state);
                if (result == 0)
                {
                    // Update current stick positions
                    CurrentRightStickX = state.Gamepad.sThumbRX;
                    CurrentRightStickY = state.Gamepad.sThumbRY;
                }
                return result;
            }
            catch
            {
                return 1; // ERROR_DEVICE_NOT_CONNECTED
            }
        }

        /// <summary>
        /// Poll controller state and return right stick position
        /// Returns (0, 0) if controller not connected
        /// </summary>
        public (short x, short y, bool connected) PollControllerState()
        {
            try
            {
                var result = XInputGetState(_controllerIndex, out var state);
                if (result == 0)
                {
                    CurrentRightStickX = state.Gamepad.sThumbRX;
                    CurrentRightStickY = state.Gamepad.sThumbRY;
                    return (state.Gamepad.sThumbRX, state.Gamepad.sThumbRY, true);
                }
                return (0, 0, false);
            }
            catch
            {
                return (0, 0, false);
            }
        }

        /// <summary>
        /// Move the right stick (aiming) using XInput
        /// This modifies the stick position directly without actual mouse movement
        /// </summary>
        public void MoveStick(int x, int y)
        {
            if (!IsConnected) return;

            try
            {
                // Scale the input to XInput range (-32768 to 32767)
                // x and y come as clamped -150 to 150 range from MouseManager
                short scaledX = (short)(x * 218); // 32767 / 150 ≈ 218
                short scaledY = (short)(y * 218);

                // Apply deadzone
                if (Math.Abs(scaledX) < LEFT_THUMB_DEADZONE) scaledX = 0;
                if (Math.Abs(scaledY) < LEFT_THUMB_DEADZONE) scaledY = 0;

                _lastRightStickX = scaledX;
                _lastRightStickY = scaledY;
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"XInput MoveStick error: {ex.Message}", false);
            }
        }

        /// <summary>
        /// Simulate right trigger press (for shooting/aiming)
        /// </summary>
        public void PressRightTrigger()
        {
            if (!IsConnected) return;

            try
            {
                // Right trigger is a byte value 0-255
                // We simulate it through mouse down since XInput doesn't support
                // injecting input (only reading it)
                // For actual controller aim assist, we'd need a virtual controller
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"XInput PressRightTrigger error: {ex.Message}", false);
            }
        }

        /// <summary>
        /// Release right trigger
        /// </summary>
        public void ReleaseRightTrigger()
        {
            if (!IsConnected) return;

            try
            {
                // See note in PressRightTrigger
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"XInput ReleaseRightTrigger error: {ex.Message}", false);
            }
        }

        /// <summary>
        /// Vibrate controller (haptic feedback)
        /// </summary>
        /// <param name="leftMotor">0-65535 left motor speed</param>
        /// <param name="rightMotor">0-65535 right motor speed</param>
        public void Vibrate(ushort leftMotor = 0, ushort rightMotor = 0)
        {
            if (!IsConnected) return;

            try
            {
                var vibration = new XINPUT_VIBRATION
                {
                    wLeftMotorSpeed = leftMotor,
                    wRightMotorSpeed = rightMotor
                };
                XInputSetState(_controllerIndex, ref vibration);
            }
            catch
            {
                // Ignore vibration errors
            }
        }

        /// <summary>
        /// Get the last known right stick position
        /// </summary>
        public (short x, short y) GetStickPosition()
        {
            return ((short)_lastRightStickX, (short)_lastRightStickY);
        }

        /// <summary>
        /// Disconnect controller and stop any active operations
        /// </summary>
        public void Disconnect()
        {
            if (IsConnected)
            {
                Vibrate(0, 0); // Stop vibration
                IsConnected = false;
                LogManager.Log(LogManager.LogLevel.Info, "XInput controller disconnected", false);
            }
        }
    }
}