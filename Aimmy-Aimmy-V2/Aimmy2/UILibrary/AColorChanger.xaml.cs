using Aimmy2.Theme;
using System.Windows;
using System.Windows.Media;

namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for AColorChanger.xaml
    /// </summary>
    public partial class AColorChanger : System.Windows.Controls.UserControl
    {
        // Store original XAML values for reverting
        private Brush? _origBackground;
        private Brush? _origBorderBrush;
        private Thickness _origBorderThickness;
        private CornerRadius _origCornerRadius;

        private CornerRadius _origColorChangingRadius;

        public AColorChanger(string title)
        {
            InitializeComponent();
            ColorChangerTitle.Content = title;

            // Subscribe to Beta UI changes
            ThemeManager.BetaUIStateChanged += OnBetaUIStateChanged;

            Loaded += (s, e) =>
            {
                CaptureOriginalValues();
                ApplyBetaUIIfActive();
            };
            Unloaded += (s, e) => ThemeManager.BetaUIStateChanged -= OnBetaUIStateChanged;
        }

        private void CaptureOriginalValues()
        {
            _origBackground = ColorChangerBorder.Background;
            _origBorderBrush = ColorChangerBorder.BorderBrush;
            _origBorderThickness = ColorChangerBorder.BorderThickness;
            _origCornerRadius = ColorChangerBorder.CornerRadius;

            _origColorChangingRadius = ColorChangingBorder.CornerRadius;
        }

        private void OnBetaUIStateChanged(object? sender, bool isBetaUI)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (isBetaUI)
                    ApplyM3Style();
                else
                    RevertM3Style();
            });
        }

        private bool IsBetaUIEnabled => Class.Dictionary.toggleState.TryGetValue("Beta UI", out var val) && val is bool b && b;

        private void ApplyBetaUIIfActive()
        {
            if (IsBetaUIEnabled) ApplyM3Style();
        }

        private void ApplyM3Style()
        {
            var scheme = ThemeManager.CurrentScheme ?? ThemeManager.GenerateMaterial3Scheme(ThemeManager.ThemeColor);

            // Text
            ColorChangerTitle.Foreground = new SolidColorBrush(scheme.OnSurface);

            // Color indicator chip: round slightly more to look modern (pill/circle style)
            ColorChangingBorder.CornerRadius = new CornerRadius(8);
        }

        private void RevertM3Style()
        {
            ColorChangerBorder.Background = _origBackground;
            ColorChangerBorder.BorderBrush = _origBorderBrush;
            ColorChangerBorder.BorderThickness = _origBorderThickness;
            ColorChangerBorder.CornerRadius = _origCornerRadius;

            ColorChangerTitle.Foreground = new SolidColorBrush(Color.FromArgb(0xDD, 0xFF, 0xFF, 0xFF));

            ColorChangingBorder.CornerRadius = _origColorChangingRadius;
        }
    }
}