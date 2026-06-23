using Aimmy2.Theme;
using AimmyWPF.Class;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for AToggle.xaml
    /// </summary>
    public partial class AToggle : System.Windows.Controls.UserControl
    {
        private static readonly Color DisableColor = Colors.White;
        private static readonly TimeSpan AnimationDuration = TimeSpan.FromMilliseconds(500);
        private bool _isEnabled = false;

        // Store original XAML values for revert
        private Thickness _origBorderThickness;
        private CornerRadius _origBorderRadius;
        private CornerRadius _origThumbRadius;
        private Thickness _origThumbBorderThickness;
        private double _origBorderWidth;
        private double _origBorderHeight;
        private double _origThumbWidth;
        private double _origThumbHeight;
        private Brush? _origBorderBg;
        private Brush? _origContainerBg;
        private Brush? _origContainerBorder;
        private Thickness _origContainerBorderThickness;
        private CornerRadius _origContainerRadius;

        public AToggle(string Text, string? tooltip = null)
        {
            InitializeComponent();
            ToggleTitle.Content = Text;

            if (!string.IsNullOrEmpty(tooltip))
            {
                var tt = new System.Windows.Controls.ToolTip { Content = tooltip };
                if (TryFindResource("Tooltip") is Style style)
                    tt.Style = style;
                ToolTip = tt;
            }

            // Subscribe to theme change events
            ThemeManager.ThemeChanged += OnThemeChanged;
            ThemeManager.BetaUIStateChanged += OnBetaUIStateChanged;

            // Update theme on load
            this.Loaded += (s, e) =>
            {
                CaptureOriginalValues();
                RefreshThemeColors();
                UpdateToggleVisualStyle();
            };

            // Cleanup on unload
            this.Unloaded += (s, e) =>
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                ThemeManager.BetaUIStateChanged -= OnBetaUIStateChanged;
            };
        }

        private void CaptureOriginalValues()
        {
            _origBorderThickness = SwitchBorder.BorderThickness;
            _origBorderRadius = SwitchBorder.CornerRadius;
            _origThumbRadius = SwitchMoving.CornerRadius;
            _origThumbBorderThickness = SwitchMoving.BorderThickness;
            _origBorderWidth = SwitchBorder.Width;
            _origBorderHeight = SwitchBorder.Height;
            _origThumbWidth = SwitchMoving.Width;
            _origThumbHeight = SwitchMoving.Height;
            _origBorderBg = SwitchBorder.Background;

            // Capture the outer container border (parent of the toggle)
            if (this.Content is System.Windows.Controls.Grid rootGrid &&
                rootGrid.Children.Count > 0 &&
                rootGrid.Children[0] is System.Windows.Controls.Border container)
            {
                _origContainerBg = container.Background;
                _origContainerBorder = container.BorderBrush;
                _origContainerBorderThickness = container.BorderThickness;
                _origContainerRadius = container.CornerRadius;
            }
        }

        private void OnThemeChanged(object sender, Color newThemeColor)
        {
            Application.Current.Dispatcher.BeginInvoke(() => RefreshThemeColors());
        }

        private void OnBetaUIStateChanged(object? sender, bool isBetaUI)
        {
            // Beta UI removed
        }

        private void RefreshThemeColors()
        {
            SwitchBorder.BorderBrush = new SolidColorBrush(ThemeManager.ThemeColor);
            if (_isEnabled)
                SwitchMoving.Background = new SolidColorBrush(ThemeManager.ThemeColor);
        }

        private Color GetCurrentColor()
        {
            return SwitchMoving.Background is SolidColorBrush brush ? brush.Color : DisableColor;
        }

        public void UpdateToggleVisualStyle()
        {
            RevertBetaUIStyle();
        }



        private void RevertBetaUIStyle()
        {
            // Restore theme behavior on track border
            ThemeBehavior.SetTheme(SwitchBorder, "Theme:BorderBrush");

            // Restore original dimensions and styling
            SwitchBorder.Width = _origBorderWidth > 0 ? _origBorderWidth : 36;
            SwitchBorder.Height = _origBorderHeight > 0 ? _origBorderHeight : 18;
            SwitchBorder.CornerRadius = _origBorderRadius;
            SwitchBorder.Background = _origBorderBg ?? Brushes.Transparent;
            SwitchBorder.BorderThickness = _origBorderThickness;

            SwitchMoving.Width = _origThumbWidth > 0 ? _origThumbWidth : 18;
            SwitchMoving.Height = _origThumbHeight > 0 ? _origThumbHeight : 18;
            SwitchMoving.CornerRadius = _origThumbRadius;
            SwitchMoving.BorderThickness = _origThumbBorderThickness;

            // Restore container
            if (this.Content is System.Windows.Controls.Grid rootGrid &&
                rootGrid.Children.Count > 0 &&
                rootGrid.Children[0] is System.Windows.Controls.Border container)
            {
                container.Background = _origContainerBg;
                container.BorderBrush = _origContainerBorder;
                container.BorderThickness = _origContainerBorderThickness;
                container.CornerRadius = _origContainerRadius;
            }

            // Restore label
            ToggleTitle.Foreground = new SolidColorBrush(Color.FromArgb(0xDD, 0xFF, 0xFF, 0xFF));

            // Restore toggle visual state
            if (_isEnabled)
            {
                SwitchMoving.Background = new SolidColorBrush(ThemeManager.ThemeColor);
                SwitchMoving.Margin = new Thickness(0, 0, -1, 0);
            }
            else
            {
                SwitchMoving.Background = new SolidColorBrush(DisableColor);
                SwitchMoving.Margin = new Thickness(0, 0, 16, 0);
            }
        }

        public void EnableSwitch()
        {
            _isEnabled = true;

            SwitchMoving.Background = new SolidColorBrush(ThemeManager.ThemeColor);
            SwitchMoving.Margin = new Thickness(0, 0, -1, 0);
        }

        public void DisableSwitch()
        {
            _isEnabled = false;

            SwitchMoving.Background = new SolidColorBrush(DisableColor);
            SwitchMoving.Margin = new Thickness(0, 0, 16, 0);
        }

        private void AnimateThumbSize(double targetSize)
        {
            var widthAnim = new DoubleAnimation(SwitchMoving.Width, targetSize, AnimationDuration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            var heightAnim = new DoubleAnimation(SwitchMoving.Height, targetSize, AnimationDuration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            SwitchMoving.BeginAnimation(WidthProperty, widthAnim);
            SwitchMoving.BeginAnimation(HeightProperty, heightAnim);
        }

        private void SetColorAnimation(Color fromColor, Color toColor, TimeSpan duration)
        {
            ColorAnimation animation = new ColorAnimation(fromColor, toColor, duration);
            SwitchMoving.Background = new SolidColorBrush(fromColor);
            SwitchMoving.Background.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }
    }
}
