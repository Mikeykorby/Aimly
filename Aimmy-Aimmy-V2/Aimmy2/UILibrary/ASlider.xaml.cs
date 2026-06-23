using Aimmy2.Theme;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for ASlider.xaml
    /// </summary>
    public partial class ASlider : UserControl
    {
        // Store original container values for revert
        private Brush? _origContainerBg;
        private Brush? _origContainerBorder;
        private Thickness _origContainerBorderThickness;
        private CornerRadius _origContainerRadius;
        private bool _capturedOriginals = false;

        public ASlider(string Text, string NotifierText, double ButtonSteps, string? tooltip = null)
        {
            InitializeComponent();

            SliderTitle.Content = Text;

            if (!string.IsNullOrEmpty(tooltip))
            {
                var tt = new System.Windows.Controls.ToolTip { Content = tooltip };
                if (TryFindResource("Tooltip") is System.Windows.Style style)
                    tt.Style = style;
                ToolTip = tt;
            }

            Slider.ValueChanged += (s, e) =>
            {
                AdjustNotifier.Content = $"{Slider.Value:F2} {NotifierText}";
            };

            SubtractOne.Click += (s, e) => UpdateSliderValue(-ButtonSteps);
            AddOne.Click += (s, e) => UpdateSliderValue(ButtonSteps);

            // Subscribe to Beta UI changes
            ThemeManager.BetaUIStateChanged += OnBetaUIStateChanged;

            // Register buttons for theme updates when loaded
            Loaded += (s, e) =>
            {
                CaptureOriginals();
                ThemeManager.RegisterElement(SubtractOne);
                ThemeManager.RegisterElement(AddOne);
                ApplyBetaUIIfActive();
            };

            Unloaded += (s, e) =>
            {
                ThemeManager.BetaUIStateChanged -= OnBetaUIStateChanged;
            };
        }

        private void CaptureOriginals()
        {
            if (_capturedOriginals) return;
            _capturedOriginals = true;

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
            if (IsBetaUIEnabled)
                ApplyM3Style();
        }

        private void ApplyM3Style()
        {
            var scheme = ThemeManager.CurrentScheme ?? ThemeManager.GenerateMaterial3Scheme(ThemeManager.ThemeColor);

            // Unregister buttons from dynamic theme color overrides
            ThemeManager.UnregisterElement(SubtractOne);
            ThemeManager.UnregisterElement(AddOne);

            // Labels: M3 colors
            SliderTitle.Foreground = new SolidColorBrush(scheme.OnSurface);
            AdjustNotifier.Foreground = new SolidColorBrush(scheme.OnSurfaceVariant);

            // Slider track color via Foreground (used by default WPF slider template)
            Slider.Foreground = new SolidColorBrush(scheme.Primary);

            // Buttons: M3 tonal/transparent backgrounds
            SubtractOne.Background = Brushes.Transparent;
            AddOne.Background = Brushes.Transparent;
            SubtractOne.Foreground = new SolidColorBrush(scheme.OnSurfaceVariant);
            AddOne.Foreground = new SolidColorBrush(scheme.OnSurfaceVariant);
        }

        private void RevertM3Style()
        {
            // Register buttons back to dynamic theme color overrides
            ThemeManager.RegisterElement(SubtractOne);
            ThemeManager.RegisterElement(AddOne);

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

            // Restore labels
            var defaultFg = new SolidColorBrush(Color.FromArgb(0xDD, 0xFF, 0xFF, 0xFF));
            SliderTitle.Foreground = defaultFg;
            AdjustNotifier.Foreground = defaultFg;

            // Restore slider
            Slider.Foreground = new SolidColorBrush(Colors.White);

            // Restore buttons
            SubtractOne.Foreground = new SolidColorBrush(Colors.White);
            AddOne.Foreground = new SolidColorBrush(Colors.White);
        }

        private void UpdateSliderValue(double change)
        {
            Slider.Value = Math.Round(Slider.Value + change, 2);
        }

        private void Slider_MouseUp(object sender, MouseButtonEventArgs e)
        {
        }

        private void Slider_MouseUp_1(object sender, MouseButtonEventArgs e)
        {
            System.Windows.MessageBox.Show($"{Slider.Value:F2}");
        }
    }
}