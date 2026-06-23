using Aimmy2.Class;
using Aimmy2.Theme;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UserControl = System.Windows.Controls.UserControl;

namespace UILibrary
{
    /// <summary>
    /// Interaction logic for ADropdown.xaml
    /// </summary>
    public partial class ADropdown : UserControl
    {
        private string main_dictionary_path { get; set; }

        // Store original XAML values for reverting
        private Brush? _origBackground;
        private Brush? _origBorderBrush;
        private Thickness _origBorderThickness;
        private CornerRadius _origCornerRadius;

        private Brush? _origComboBackground;
        private Brush? _origComboBorderBrush;
        private Thickness _origComboBorderThickness;
        private Brush? _origComboForeground;

        public ADropdown(string title, string dictionary_path, string? tooltip = null)
        {
            InitializeComponent();
            DropdownTitle.Content = title;
            main_dictionary_path = dictionary_path;

            if (!string.IsNullOrEmpty(tooltip))
            {
                var tt = new System.Windows.Controls.ToolTip { Content = tooltip };
                if (TryFindResource("Tooltip") is System.Windows.Style style)
                    tt.Style = style;
                ToolTip = tt;
            }

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
            _origBackground = DropdownBorder.Background;
            _origBorderBrush = DropdownBorder.BorderBrush;
            _origBorderThickness = DropdownBorder.BorderThickness;
            _origCornerRadius = DropdownBorder.CornerRadius;

            _origComboBackground = DropdownBox.Background;
            _origComboBorderBrush = DropdownBox.BorderBrush;
            _origComboBorderThickness = DropdownBox.BorderThickness;
            _origComboForeground = DropdownBox.Foreground;
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

        private bool IsBetaUIEnabled => Dictionary.toggleState.TryGetValue("Beta UI", out var val) && val is bool b && b;

        private void ApplyBetaUIIfActive()
        {
            if (IsBetaUIEnabled) ApplyM3Style();
        }

        private void ApplyM3Style()
        {
            var scheme = ThemeManager.CurrentScheme ?? ThemeManager.GenerateMaterial3Scheme(ThemeManager.ThemeColor);

            // Combobox: M3 exposed dropdown look
            DropdownBox.Background = new SolidColorBrush(scheme.SurfaceContainer);
            DropdownBox.BorderBrush = new SolidColorBrush(scheme.Outline);
            DropdownBox.BorderThickness = new Thickness(1);
            DropdownBox.Foreground = new SolidColorBrush(scheme.OnSurface);

            // Title
            DropdownTitle.Foreground = new SolidColorBrush(scheme.OnSurface);
        }

        private void RevertM3Style()
        {
            DropdownBorder.Background = _origBackground;
            DropdownBorder.BorderBrush = _origBorderBrush;
            DropdownBorder.BorderThickness = _origBorderThickness;
            DropdownBorder.CornerRadius = _origCornerRadius;

            DropdownBox.Background = _origComboBackground;
            DropdownBox.BorderBrush = _origComboBorderBrush;
            DropdownBox.BorderThickness = _origComboBorderThickness;
            DropdownBox.Foreground = _origComboForeground;

            DropdownTitle.Foreground = new SolidColorBrush(Color.FromArgb(0xDD, 0xFF, 0xFF, 0xFF));
        }

        private void DropdownBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItemContent = ((ComboBoxItem)DropdownBox.SelectedItem)?.Content?.ToString();
            if (selectedItemContent != null)
            {
                Dictionary.dropdownState[main_dictionary_path] = selectedItemContent;
            }
        }
    }
}