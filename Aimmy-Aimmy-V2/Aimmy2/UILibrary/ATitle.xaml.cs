using Aimmy2.Class;
using Aimmy2.Theme;
using System.Windows;
using System.Windows.Media;

namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for ATitle.xaml
    /// </summary>
    public partial class ATitle : System.Windows.Controls.UserControl
    {
        public ATitle(string Text, bool MinimizableMenu = false)
        {
            InitializeComponent();

            LabelTitle.Content = Text;

            if (MinimizableMenu)
            {
                Minimize.Visibility = System.Windows.Visibility.Visible;
                
                // Ensure the key exists in the dictionary (fixes crash when loading configs)
                if (!Dictionary.minimizeState.ContainsKey(Text))
                {
                    Dictionary.minimizeState[Text] = false;
                }
                
                // Initialize minimize button icon based on current state
                Minimize.Content = Dictionary.minimizeState[Text] ? "\xE710" : "\xE921";
            }

            Minimize.Click += (s, e) =>
            {
                // Ensure the key exists before toggling
                if (!Dictionary.minimizeState.ContainsKey(Text))
                {
                    Dictionary.minimizeState[Text] = false;
                }
                
                // Toggle the state
                bool currentState = Dictionary.minimizeState[Text];
                Dictionary.minimizeState[Text] = !currentState;
                
                // Update the button icon
                Minimize.Content = !currentState ? "\xE710" : "\xE921";
            };

            // Subscribe to Beta UI changes
            ThemeManager.BetaUIStateChanged += OnBetaUIStateChanged;

            Loaded += (s, e) => ApplyBetaUIIfActive();
            Unloaded += (s, e) => ThemeManager.BetaUIStateChanged -= OnBetaUIStateChanged;
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

            // Unregister minimize button from dynamic theme background overrides
            ThemeManager.UnregisterElement(Minimize);

            // Section header: M3 title-medium feel
            LabelTitle.Foreground = new SolidColorBrush(scheme.OnSurface);
            LabelTitle.FontWeight = FontWeights.Medium;
            LabelTitle.FontSize = 14;

            // Minimize button color
            Minimize.Foreground = new SolidColorBrush(scheme.OnSurfaceVariant);
        }

        private void RevertM3Style()
        {
            // Register minimize button back to dynamic theme background overrides
            ThemeManager.RegisterElement(Minimize);

            LabelTitle.Foreground = new SolidColorBrush(Colors.White);
            LabelTitle.FontWeight = FontWeights.Normal;
            LabelTitle.FontSize = 12;

            if (this.Content is System.Windows.Controls.Grid rootGrid &&
                rootGrid.Children.Count > 0 &&
                rootGrid.Children[0] is System.Windows.Controls.Border container)
            {
                container.Background = new SolidColorBrush(Color.FromArgb(0x3F, 0x3C, 0x3C, 0x3C));
                container.BorderBrush = new SolidColorBrush(Color.FromArgb(0x3F, 0xFF, 0xFF, 0xFF));
                container.BorderThickness = new Thickness(1, 1, 1, 0);
                container.CornerRadius = new CornerRadius(5, 5, 0, 0);
            }

            Minimize.Foreground = new SolidColorBrush(Colors.White);
        }
    }
}
