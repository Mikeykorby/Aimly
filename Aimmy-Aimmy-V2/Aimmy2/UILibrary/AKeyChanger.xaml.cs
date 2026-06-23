using Aimmy2.Theme;
using System.Windows;
using System.Windows.Media;
using Other;

namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for AKeyChanger.xaml
    /// </summary>
    public partial class AKeyChanger : System.Windows.Controls.UserControl
    {
        // Store original XAML values for reverting
        private Brush? _origBackground;
        private Brush? _origBorderBrush;
        private Thickness _origBorderThickness;
        private CornerRadius _origCornerRadius;

        private Brush? _origKeyNotifierBorderBrush;
        private Thickness _origKeyNotifierBorderThickness;
        private CornerRadius _origKeyNotifierCornerRadius;

        public AKeyChanger(string Text, string Keybind, string? tooltip = null)
        {
            InitializeComponent();
            KeyChangerTitle.Content = Text;

            if (!string.IsNullOrEmpty(tooltip))
            {
                var tt = new System.Windows.Controls.ToolTip { Content = tooltip };
                if (TryFindResource("Tooltip") is System.Windows.Style style)
                    tt.Style = style;
                ToolTip = tt;
            }

            KeyNotifier.Content = KeybindNameManager.ConvertToRegularKey(Keybind);

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
            _origBackground = KeyChangerBorder.Background;
            _origBorderBrush = KeyChangerBorder.BorderBrush;
            _origBorderThickness = KeyChangerBorder.BorderThickness;
            _origCornerRadius = KeyChangerBorder.CornerRadius;

            _origKeyNotifierBorderBrush = KeyNotifierBorder.BorderBrush;
            _origKeyNotifierBorderThickness = KeyNotifierBorder.BorderThickness;
            _origKeyNotifierCornerRadius = KeyNotifierBorder.CornerRadius;
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

            // Key notifier box: M3 Outlined input chip style
            KeyNotifierBorder.BorderBrush = new SolidColorBrush(scheme.Outline);
            KeyNotifierBorder.BorderThickness = new Thickness(1);
            KeyNotifierBorder.CornerRadius = new CornerRadius(8);
            KeyNotifierBorder.Background = new SolidColorBrush(scheme.SurfaceContainer);

            // Text colors
            KeyChangerTitle.Foreground = new SolidColorBrush(scheme.OnSurface);
            KeyNotifier.Foreground = new SolidColorBrush(scheme.Primary);
        }

        private void RevertM3Style()
        {
            KeyChangerBorder.Background = _origBackground;
            KeyChangerBorder.BorderBrush = _origBorderBrush;
            KeyChangerBorder.BorderThickness = _origBorderThickness;
            KeyChangerBorder.CornerRadius = _origCornerRadius;

            KeyNotifierBorder.BorderBrush = _origKeyNotifierBorderBrush;
            KeyNotifierBorder.BorderThickness = _origKeyNotifierBorderThickness;
            KeyNotifierBorder.CornerRadius = _origKeyNotifierCornerRadius;
            KeyNotifierBorder.Background = Brushes.Transparent;

            KeyChangerTitle.Foreground = new SolidColorBrush(Color.FromArgb(0xDD, 0xFF, 0xFF, 0xFF));
            KeyNotifier.Foreground = new SolidColorBrush(Color.FromArgb(0xDD, 0xFF, 0xFF, 0xFF));
        }
    }
}