using Aimmy2.Theme;
using System.Windows;
using System.Windows.Media;

namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for APButton.xaml
    /// </summary>
    public partial class APButton : System.Windows.Controls.UserControl
    {
        private Brush? _origBg;
        private Brush? _origBorderBrush;
        private Thickness _origBorderThickness;
        private CornerRadius _origCornerRadius;
        private bool _capturedOriginals = false;

        public APButton(string Text, string? tooltip = null)
        {
            InitializeComponent();
            ButtonTitle.Content = Text;

            if (!string.IsNullOrEmpty(tooltip))
            {
                var tt = new System.Windows.Controls.ToolTip { Content = tooltip };
                if (TryFindResource("Tooltip") is System.Windows.Style style)
                    tt.Style = style;
                ToolTip = tt;
            }

            ThemeManager.BetaUIStateChanged += OnBetaUIStateChanged;

            Loaded += (s, e) =>
            {
                CaptureOriginals();
                ApplyBetaUIIfActive();
            };
            Unloaded += (s, e) => ThemeManager.BetaUIStateChanged -= OnBetaUIStateChanged;
        }

        private void CaptureOriginals()
        {
            if (_capturedOriginals) return;
            _capturedOriginals = true;
            _origBg = ButtonBorder.Background;
            _origBorderBrush = ButtonBorder.BorderBrush;
            _origBorderThickness = ButtonBorder.BorderThickness;
            _origCornerRadius = ButtonBorder.CornerRadius;
        }

        private void OnBetaUIStateChanged(object? sender, bool isBetaUI)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (isBetaUI) ApplyM3Style();
                else RevertM3Style();
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

            // M3 filled-tonal button
            ButtonBorder.Background = new SolidColorBrush(scheme.SecondaryContainer);
            ButtonBorder.BorderBrush = Brushes.Transparent;
            ButtonBorder.BorderThickness = new Thickness(0);
            ButtonBorder.CornerRadius = new CornerRadius(20);

            ButtonTitle.Foreground = new SolidColorBrush(scheme.OnSecondaryContainer);
        }

        private void RevertM3Style()
        {
            ButtonBorder.Background = _origBg;
            ButtonBorder.BorderBrush = _origBorderBrush;
            ButtonBorder.BorderThickness = _origBorderThickness;
            ButtonBorder.CornerRadius = _origCornerRadius;

            ButtonTitle.Foreground = new SolidColorBrush(Colors.White);
        }

        public void ApplyLastButtonStyle()
        {
            if (IsBetaUIEnabled)
                ButtonBorder.CornerRadius = new CornerRadius(0, 0, 16, 16);
            else
                ButtonBorder.CornerRadius = new System.Windows.CornerRadius(0, 0, 6, 6);
        }
    }
}