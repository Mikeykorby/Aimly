using Aimmy2.Class;
using Aimmy2.Theme;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Media;
using UserControl = System.Windows.Controls.UserControl;

namespace UILibrary
{
    /// <summary>
    /// Interaction logic for AFileLocator.xaml
    /// </summary>
    public partial class AFileLocator : UserControl
    {
        private OpenFileDialog openFileDialog = new OpenFileDialog();
        private string main_dictionary_path { get; set; }
        private string OFDFilter = "All files (*.*)|*.*";
        private string DefaultLocationExtension = "";

        // Store original XAML values for reverting
        private Brush? _origBackground;
        private Brush? _origBorderBrush;
        private Thickness _origBorderThickness;
        private CornerRadius _origCornerRadius;

        private Brush? _origTxtBackground;
        private Brush? _origTxtBorderBrush;
        private Thickness _origTxtBorderThickness;
        private Brush? _origTxtForeground;

        private Brush? _origBtnBackground;
        private Brush? _origBtnForeground;

        public AFileLocator(string title, string dictionary_path, string FileFilter = "All files (*.*)|*.*", string DLExtension = "")
        {
            InitializeComponent();
            DropdownTitle.Content = title;

            main_dictionary_path = dictionary_path;
            FileLocationTextbox.Text = Dictionary.filelocationState[main_dictionary_path];

            OFDFilter = FileFilter;
            DefaultLocationExtension = DLExtension;

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
            _origBackground = FileLocatorBorder.Background;
            _origBorderBrush = FileLocatorBorder.BorderBrush;
            _origBorderThickness = FileLocatorBorder.BorderThickness;
            _origCornerRadius = FileLocatorBorder.CornerRadius;

            _origTxtBackground = FileLocationTextbox.Background;
            _origTxtBorderBrush = FileLocationTextbox.BorderBrush;
            _origTxtBorderThickness = FileLocationTextbox.BorderThickness;
            _origTxtForeground = FileLocationTextbox.Foreground;

            _origBtnBackground = OpenFileB.Background;
            _origBtnForeground = OpenFileB.Foreground;
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

            // Unregister search button from dynamic theme background override
            ThemeManager.UnregisterElement(OpenFileB);

            // Text
            DropdownTitle.Foreground = new SolidColorBrush(scheme.OnSurface);

            // Textbox: M3 text field look
            FileLocationTextbox.Background = new SolidColorBrush(scheme.SurfaceContainer);
            FileLocationTextbox.BorderBrush = new SolidColorBrush(scheme.Outline);
            FileLocationTextbox.BorderThickness = new Thickness(1);
            FileLocationTextbox.Foreground = new SolidColorBrush(scheme.OnSurface);

            // Button: M3 filled tonal button
            OpenFileB.Background = new SolidColorBrush(scheme.SecondaryContainer);
            OpenFileB.Foreground = new SolidColorBrush(scheme.OnSecondaryContainer);

            // Dynamically set properties if supported by Ant:Button (like CornerRadius)
            var cornerRadiusProp = OpenFileB.GetType().GetProperty("CornerRadius");
            if (cornerRadiusProp != null)
            {
                cornerRadiusProp.SetValue(OpenFileB, new CornerRadius(8));
            }
        }

        private void RevertM3Style()
        {
            // Register search button back to dynamic theme background override
            ThemeManager.RegisterElement(OpenFileB);

            FileLocatorBorder.Background = _origBackground;
            FileLocatorBorder.BorderBrush = _origBorderBrush;
            FileLocatorBorder.BorderThickness = _origBorderThickness;
            FileLocatorBorder.CornerRadius = _origCornerRadius;

            DropdownTitle.Foreground = new SolidColorBrush(Color.FromArgb(0xDD, 0xFF, 0xFF, 0xFF));

            FileLocationTextbox.Background = _origTxtBackground;
            FileLocationTextbox.BorderBrush = _origTxtBorderBrush;
            FileLocationTextbox.BorderThickness = _origTxtBorderThickness;
            FileLocationTextbox.Foreground = _origTxtForeground;

            OpenFileB.Background = _origBtnBackground;
            OpenFileB.Foreground = _origBtnForeground;

            var cornerRadiusProp = OpenFileB.GetType().GetProperty("CornerRadius");
            if (cornerRadiusProp != null)
            {
                cornerRadiusProp.SetValue(OpenFileB, new CornerRadius(0));
            }
        }

        private void OpenFileB_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            openFileDialog.InitialDirectory = Directory.GetCurrentDirectory() + DefaultLocationExtension;
            openFileDialog.Filter = OFDFilter;

            if (openFileDialog.ShowDialog() == true)
            {
                FileLocationTextbox.Text = openFileDialog.FileName;
                Dictionary.filelocationState[main_dictionary_path] = openFileDialog.FileName;
            }
        }
    }
}