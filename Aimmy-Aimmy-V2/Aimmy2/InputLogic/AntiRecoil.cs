using Aimmy2.Class;
using Other;
using System.Runtime.InteropServices;

namespace InputLogic
{
    /// <summary>
    /// Anti-recoil system that compensates for weapon recoil during automatic fire.
    /// Works by applying counter-movements to offset the weapon's upward climb.
    /// </summary>
    internal static class AntiRecoil
    {
        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

        private const uint MOUSEEVENTF_MOVE = 0x0001;

        private static bool _isActive = false;
        private static DateTime _lastShotTime = DateTime.MinValue;
        private static int _shotsFired = 0;
        private static double _currentRecoilCompensation = 0;

        // Configuration
        private static double BaseRecoil => Dictionary.sliderSettings.TryGetValue("Anti-Recoil Strength", out var val) ? val : 10.0;
        private static double RecoilScale => Dictionary.sliderSettings.TryGetValue("Anti-Recoil Scale", out var val) ? val : 1.0;
        private static int MaxShots => Dictionary.sliderSettings.TryGetValue("Anti-Recoil Max Shots", out var val) ? (int)val : 30;
        public static bool IsEnabled => Dictionary.toggleState.TryGetValue("Anti-Recoil", out var enabled) && enabled;
        public static bool UseSmooth => Dictionary.toggleState.TryGetValue("Anti-Recoil Smooth", out var smooth) && smooth;

        /// <summary>
        /// Activate anti-recoil compensation
        /// </summary>
        public static async Task Activate()
        {
            if (!_isActive && IsEnabled)
            {
                _isActive = true;
                _shotsFired = 0;
                _currentRecoilCompensation = 0;

                while (_isActive)
                {
                    await Task.Delay(1); // Check frequently
                }
            }
        }

        /// <summary>
        /// Apply recoil compensation when a shot is detected
        /// Called from the trigger bot or auto-shoot system
        /// </summary>
        public static void ApplyRecoilCompensation()
        {
            if (!IsEnabled) return;

            _shotsFired++;
            if (_shotsFired > MaxShots) return;

            // Calculate recoil compensation
            // Recoil typically increases with each shot in a burst
            double scaleFactor = 1.0 + (_shotsFired * 0.05); // 5% increase per shot
            double compensation = -(BaseRecoil * RecoilScale * scaleFactor);

            // Smooth the compensation value
            if (UseSmooth)
            {
                _currentRecoilCompensation = (_currentRecoilCompensation * 0.7) + (compensation * 0.3);
            }
            else
            {
                _currentRecoilCompensation = compensation;
            }

            // Apply the counter-movement (negative Y pulls down)
            int moveY = (int)Math.Round(_currentRecoilCompensation);

            try
            {
                mouse_event(MOUSEEVENTF_MOVE, 0, (uint)moveY, 0, 0);
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"Anti-recoil apply error: {ex.Message}", false);
            }
        }

        /// <summary>
        /// Reset anti-recoil state (called when stopping fire)
        /// </summary>
        public static void Reset()
        {
            _isActive = false;
            _shotsFired = 0;
            _currentRecoilCompensation = 0;
        }

        /// <summary>
        /// Get current compensation for display
        /// </summary>
        public static double GetCurrentCompensation()
        {
            return _currentRecoilCompensation;
        }

        /// <summary>
        /// Get shots fired count for display
        /// </summary>
        public static int GetShotsFired()
        {
            return _shotsFired;
        }
    }
}