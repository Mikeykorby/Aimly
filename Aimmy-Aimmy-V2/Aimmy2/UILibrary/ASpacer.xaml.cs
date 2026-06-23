using Aimmy2.Theme;
using System.Windows;

namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for ASpacer.xaml
    /// </summary>
    public partial class ASpacer : System.Windows.Controls.UserControl
    {
        public ASpacer()
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
                    this.Height = 4; // Reduce spacing
                else
                    this.Height = 10; // Restore spacing
            });
        }

        private bool IsBetaUIEnabled => Class.Dictionary.toggleState.TryGetValue("Beta UI", out var val) && val is bool b && b;

        private void ApplyBetaUIIfActive()
        {
            if (IsBetaUIEnabled)
            {
                this.Height = 4;
            }
        }
    }
}