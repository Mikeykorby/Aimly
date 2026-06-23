using Aimmy2.Theme;
using Class;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Aimmy2
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Global Exception Handlers
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    global::Other.LogManager.Log(global::Other.LogManager.LogLevel.Error, $"Fatal Unhandled Exception: {ex.Message}\n{ex.StackTrace}", false);
                }
            };

            DispatcherUnhandledException += (s, args) =>
            {
                global::Other.LogManager.Log(global::Other.LogManager.LogLevel.Error, $"UI Dispatcher Exception: {args.Exception.Message}\n{args.Exception.StackTrace}", false);
            };

            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                global::Other.LogManager.Log(global::Other.LogManager.LogLevel.Error, $"Unobserved Task Exception: {args.Exception.Message}\n{args.Exception.StackTrace}", false);
                args.SetObserved();
            };

            // Initialize the application theme from saved settings
            InitializeTheme();

            // Set shutdown mode to prevent app from closing when startup window closes
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

#if DEBUG
            var _mainWindow = new MainWindow();
            MainWindow = _mainWindow;
            _mainWindow.Show();
            return;
#endif
            // code IS reachable, only in release though
            try
            {
                // Create and show startup window
                var startupWindow = new StartupWindow();
                startupWindow.Show();

                // Reset shutdown mode after startup window is shown
                ShutdownMode = ShutdownMode.OnMainWindowClose;
            }
            catch (Exception ex)
            {
                // If startup window fails, launch main window directly
                MessageBox.Show($"Startup animation failed: {ex.Message}\nLaunching main application...",
                              "Aimmy AI", MessageBoxButton.OK, MessageBoxImage.Information);

                var mainWindow = new MainWindow();
                MainWindow = mainWindow;
                mainWindow.Show();

                ShutdownMode = ShutdownMode.OnMainWindowClose;
            }
        }

        private void InitializeTheme()
        {
            try
            {
                // Load the color state configuration
                var colorState = new Dictionary<string, dynamic>
                {
                    { "Theme Color", "#FFFFFFFF" }
                };

                // Load saved colors
                SaveDictionary.LoadJSON(colorState, "bin\\colors.cfg");

                // Apply theme color if found
                if (colorState.TryGetValue("Theme Color", out var themeColor) && themeColor is string colorString)
                {
                    ThemeManager.SetThemeColor(colorString);
                }
                else
                {
                    // Use default purple if no saved color
                    ThemeManager.SetThemeColor("#FFFFFFFF");
                }

                // Load toggles state configuration early to detect Beta UI
                var toggleState = new Dictionary<string, dynamic>
                {
                    { "Beta UI", false }
                };
                SaveDictionary.LoadJSON(toggleState, "bin\\toggles.cfg");

                if (toggleState.TryGetValue("Beta UI", out var betaUiVal) && betaUiVal is bool betaEnabled)
                {
                    Aimmy2.Class.Dictionary.toggleState["Beta UI"] = betaEnabled;
                    if (betaEnabled)
                    {
                        // Regenerate scheme
                        ThemeManager.RegenerateMaterial3Scheme();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error and use default color
                ThemeManager.SetThemeColor("#FFFFFFFF");
            }
        }
    }
}
