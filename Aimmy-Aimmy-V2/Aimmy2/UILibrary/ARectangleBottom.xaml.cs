using Aimmy2.Theme;
using System.Windows;

namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for ARectangleBottom.xaml
    /// </summary>
    public partial class ARectangleBottom : System.Windows.Controls.UserControl
    {
        public ARectangleBottom()
        {
            InitializeComponent();

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
                    this.Height = 0; // Collapse height
                else
                    this.Height = 5; // Restore height
            });
        }

        private bool IsBetaUIEnabled => Class.Dictionary.toggleState.TryGetValue("Beta UI", out var val) && val is bool b && b;

        private void ApplyBetaUIIfActive()
        {
            if (IsBetaUIEnabled)
            {
                this.Height = 0;
            }
        }
    }
}