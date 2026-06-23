using Aimmy2.Class;
using Aimmy2.Theme;
using Class;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace Visuality
{
    public partial class DetectedPlayerWindow : Window
    {
        // Windows API for forcing window position
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        private bool _isInitialized = false;

        private double _targetConfidence = 0;
        private double _currentConfidence = 0;
        private long _lastUpdate = 0;

        public DetectedPlayerWindow()
        {
            InitializeComponent();

            //Subscribe to my Onlyfans to exclude bad Behavior!
            ThemeManager.ExcludeWindowFromBackground(this);

            Title = "";

            // Subscribe to display changes early
            DisplayManager.DisplayChanged += OnDisplayChanged;

            // Subscribe to property changes
            PropertyChanger.ReceiveDPColor = UpdateDPColor;
            PropertyChanger.ReceiveDPWBorderThickness = ChangeBorderThickness;
            PropertyChanger.ReceiveDPWOpacity = ChangeOpacity;

            // Hardcode to beta look
            DetectedPlayerFocus.CornerRadius = new CornerRadius(0);
            DetectedPlayerConfidence.FontSize = 11;

            CompositionTarget.Rendering += RenderLoop;
        }

        public void SetTargetConfidence(double conf, bool instant)
        {
            _targetConfidence = conf;
            if (instant)
            {
                _currentConfidence = conf;
                if (conf <= 0)
                {
                    DetectedPlayerConfidence.Opacity = 0;
                    ConfidenceBarContainer.Opacity = 0;
                }
            }
            else
            {
                DetectedPlayerConfidence.Opacity = 1;
                ConfidenceBarContainer.Opacity = 1;
            }
        }

        private Thickness _targetBoxMargin;
        private double _targetBoxWidth;
        private double _targetBoxHeight;
        private Thickness _targetAimPointMargin;
        private bool _isBoxTargetSet = false;

        public void SetTargetBox(Thickness margin, double width, double height, Thickness aimPointMargin)
        {
            _targetBoxMargin = margin;
            _targetBoxWidth = width;
            _targetBoxHeight = height;
            _targetAimPointMargin = aimPointMargin;
            _isBoxTargetSet = true;
        }

        private void RenderLoop(object sender, EventArgs e)
        {
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (_lastUpdate == 0) { _lastUpdate = now; return; }
            double dt = (now - _lastUpdate) / (double)System.Diagnostics.Stopwatch.Frequency;
            _lastUpdate = now;

            if (dt > 0.1) dt = 0.016;

            _currentConfidence += (_targetConfidence - _currentConfidence) * dt * 10.0;

            if (_isBoxTargetSet)
            {
                bool useSmoothTracking = Aimmy2.Class.Dictionary.toggleState.TryGetValue("Smooth Tracking", out var smoothVal) && smoothVal is bool s && s;
                
                double lerpSpeed = 15.0;
                if (Aimmy2.Class.Dictionary.sliderSettings.TryGetValue("Tracking Smooth Speed", out var speedVal) && speedVal is double sspd)
                {
                    lerpSpeed = sspd;
                }

                if (useSmoothTracking)
                {
                    // Lerp Width and Height
                    double curW = double.IsNaN(DetectedPlayerFocus.Width) ? _targetBoxWidth : DetectedPlayerFocus.Width;
                    double curH = double.IsNaN(DetectedPlayerFocus.Height) ? _targetBoxHeight : DetectedPlayerFocus.Height;
                    
                    DetectedPlayerFocus.Width = curW + (_targetBoxWidth - curW) * dt * lerpSpeed;
                    DetectedPlayerFocus.Height = curH + (_targetBoxHeight - curH) * dt * lerpSpeed;

                    // Lerp Margins
                    var curM = DetectedPlayerFocus.Margin;
                    double newLeft = curM.Left + (_targetBoxMargin.Left - curM.Left) * dt * lerpSpeed;
                    double newTop = curM.Top + (_targetBoxMargin.Top - curM.Top) * dt * lerpSpeed;
                    DetectedPlayerFocus.Margin = new Thickness(newLeft, newTop, 0, 0);

                    // Lerp AimPoint
                    var curAimM = AimPoint.Margin;
                    double newAimLeft = curAimM.Left + (_targetAimPointMargin.Left - curAimM.Left) * dt * lerpSpeed;
                    double newAimTop = curAimM.Top + (_targetAimPointMargin.Top - curAimM.Top) * dt * lerpSpeed;
                    AimPoint.Margin = new Thickness(newAimLeft, newAimTop, 0, 0);
                }
                else
                {
                    DetectedPlayerFocus.Width = _targetBoxWidth;
                    DetectedPlayerFocus.Height = _targetBoxHeight;
                    DetectedPlayerFocus.Margin = _targetBoxMargin;
                    AimPoint.Margin = _targetAimPointMargin;
                }
            }

            if (double.IsNaN(_currentConfidence)) return;

            double h = DetectedPlayerFocus.Height;
            if (double.IsNaN(h) || h <= 0) return;

            double fillH = h * _currentConfidence;
            if (fillH < 0) fillH = 0;
            
            double fillTop = h - fillH;

            ConfidenceFill.Height = fillH;
            Canvas.SetTop(ConfidenceFill, fillTop);
            
            // Set thumb position vertically centered on the fill's top
            Canvas.SetTop(ConfidenceThumb, fillTop - (ConfidenceThumb.Height / 2));
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Make window click-through
            ClickThroughOverlay.MakeClickThrough(new WindowInteropHelper(this).Handle);

            // Now that we have a window handle, position the window
            if (!_isInitialized)
            {
                _isInitialized = true;
                ForceReposition();
            }
        }

        private void OnDisplayChanged(object? sender, DisplayChangedEventArgs e)
        {

            // Update position when display changes
            Application.Current.Dispatcher.Invoke(() =>
            {
                ForceReposition();
            });
        }

        public void ForceReposition()
        {
            try
            {

                // Get window handle
                var hwnd = _isInitialized ? new WindowInteropHelper(this).Handle : IntPtr.Zero;

                // Set window state to normal first
                this.WindowState = WindowState.Normal;

                // Position window to cover the current display (accounting for DPI scaling)
                this.Left = DisplayManager.ScreenLeft / WinAPICaller.scalingFactorX;
                this.Top = DisplayManager.ScreenTop / WinAPICaller.scalingFactorY;
                this.Width = DisplayManager.ScreenWidth / WinAPICaller.scalingFactorX;
                this.Height = DisplayManager.ScreenHeight / WinAPICaller.scalingFactorY;

                // Force position with Windows API if we have a handle
                if (hwnd != IntPtr.Zero)
                {
                    SetWindowPos(hwnd, IntPtr.Zero,
                        DisplayManager.ScreenLeft,
                        DisplayManager.ScreenTop,
                        DisplayManager.ScreenWidth,
                        DisplayManager.ScreenHeight,
                        SWP_NOZORDER | SWP_NOACTIVATE);
                }

                // Maximize to cover entire display
                this.WindowState = WindowState.Maximized;

                // Update tracer start position (changed to be dynamic)
                DetectedTracers.X1 = (DisplayManager.ScreenWidth / 2.0) / WinAPICaller.scalingFactorX;

                string tracerPosition = "Bottom"; // default value
                if (Dictionary.dropdownState.TryGetValue("Tracer Position", out var position))
                {
                    tracerPosition = position.ToString();
                }

                switch (tracerPosition)
                {
                    case "Bottom":
                        DetectedTracers.Y1 = DisplayManager.ScreenHeight / WinAPICaller.scalingFactorY;
                        break;
                    case "Middle":
                        DetectedTracers.Y1 = (DisplayManager.ScreenHeight / 2.0) / WinAPICaller.scalingFactorY;
                        break;
                    case "Top":
                        DetectedTracers.Y1 = 0;
                        break;
                }

                // Force layout update
                this.UpdateLayout();

            }
            catch (Exception ex)
            {
            }
        }

        private void UpdateDPColor(Color NewColor)
        {
            DetectedPlayerFocus.BorderBrush = new SolidColorBrush(NewColor);
            DetectedPlayerConfidence.Foreground = new SolidColorBrush(NewColor);
            DetectedTracers.Stroke = new SolidColorBrush(NewColor);
            
            ConfidenceFill.Background = new SolidColorBrush(NewColor);
            AimPoint.Fill = new SolidColorBrush(NewColor);
        }

        private void ChangeBorderThickness(double newdouble)
        {
            DetectedPlayerFocus.BorderThickness = new Thickness(newdouble);
            DetectedTracers.StrokeThickness = newdouble;
        }

        private void ChangeOpacity(double newdouble) => DetectedPlayerFocus.Opacity = newdouble;

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        // Clean up event subscription
        protected override void OnClosed(EventArgs e)
        {
            DisplayManager.DisplayChanged -= OnDisplayChanged;
            base.OnClosed(e);
        }
    }
}