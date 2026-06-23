using Aimmy2.Class;
using Aimmy2.Controls;
using Aimmy2.MouseMovementLibraries.GHubSupport;
using Aimmy2.UISections;
using MouseMovementLibraries.ViGEmSupport;
using MouseMovementLibraries.XInputSupport;
using MouseMovementLibraries.DirectInputSupport;
using Aimmy2.Other;
using Aimmy2.Theme;
using Aimmy2.UILibrary;
using AimmyWPF.Class;
using Class;
using InputLogic;
using Other;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using UILibrary;
using Visuality;

namespace Aimmy2
{
    public partial class MainWindow : Window
    {
        #region Managers and Windows

        // Core managers (lazy-loaded)
        private readonly Lazy<InputBindingManager> _bindingManager = new(() => new InputBindingManager());
        private static readonly Lazy<GithubManager> _githubManager = new(() => new GithubManager());
        private readonly Lazy<UI> _uiManager = new(() => new UI());
        private Lazy<FileManager>? _fileManager;

        // Windows
        private static readonly Lazy<FOV> _fovWindow = new(() =>
        {
            var window = new FOV();
            // Force immediate reposition to current display
            window.ForceReposition();
            return window;
        });

        private static readonly Lazy<DetectedPlayerWindow> _dpWindow = new(() =>
        {
            var window = new DetectedPlayerWindow();
            // Force immediate reposition to current display
            window.ForceReposition();
            return window;
        });

        // Public accessors
        internal InputBindingManager bindingManager => _bindingManager.Value;
        internal FileManager fileManager => _fileManager?.Value ?? throw new InvalidOperationException("FileManager not initialized");
        public static FOV FOVWindow => _fovWindow.Value;
        public static DetectedPlayerWindow DPWindow => _dpWindow.Value;
        public static GithubManager githubManager => _githubManager.Value;
        public UI uiManager => _uiManager.Value;
        
        private readonly Lazy<Aimmy2.AILogic.AntiRecoilManager> _arManager = new(() => new Aimmy2.AILogic.AntiRecoilManager());
        public Aimmy2.AILogic.AntiRecoilManager arManager => _arManager.Value;

        #endregion

        #region UI State
        public SettingsMenuControl? SettingsMenuControlInstance { get; set; }
        internal Dictionary<string, AToggle> toggleInstances = new();
        private readonly Dictionary<string, UserControl?> _menuControls = new();
        private readonly Dictionary<string, bool> _menuInitialized = new();
        private UserControl? _currentControl;
        private string _currentMenu = "AimMenu";
        private bool _currentlySwitching;
        private ScrollViewer? CurrentScrollViewer;
        public double ActualFOV { get; set; } = 640;
        private double _currentGradientAngle;

        // Menu names constant
        private static readonly string[] MenuNames = { "AimMenu", "ControllerMenu", "ModelMenu", "SettingsMenu", "AboutMenu" };

        #endregion

        #region Initialization

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveDictionary.EnsureDirectoriesExist();

                InitializeMenus();
                InitializeFileManagerEarly();

                // Load configurations BEFORE loading any menus
                // This ensures minimize states are loaded from file before menu initialization
                await LoadConfigurationsAsync();

                // Apply UI font and admin font rendering fix before loading menus
                ApplyUIFont();
                if (Other.HidHideManager.IsAdmin())
                {
                    TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
                    TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
                }

                // Now load the initial menu - it will use the loaded minimize states
                LoadInitialMenu();

                // Continue with the rest of initialization
                await InitializeApplicationAsync();
                UpdateAboutSpecs();
                UpdateHeaderStatuses();
                ApplyThemeGradients();
                ThemeManager.LoadMediaSettings();

                // Apply Light Theme if saved
                if (Dictionary.toggleState.TryGetValue("Light Theme", out var lightVal) && lightVal is bool lightEnabled && lightEnabled)
                {
                    ThemeManager.ApplyLightMode(true);
                }

                // Beta UI is temporarily disabled to prevent performance issues.
                // If Beta UI was saved as enabled in the config, force it off.
                if (Dictionary.toggleState.TryGetValue("Beta UI", out var betaUiVal) && betaUiVal is bool betaEnabled && betaEnabled)
                {
                    Dictionary.toggleState["Beta UI"] = false;
                }
                // ApplyBetaUITheme(true); // Disabled - causes lag

                // Start the animated glow border effect
                StartGlowAnimation();

                // Add premium window entrance animation
                AnimateWindowEntrance();

                // Add sidebar button hover animations
                AddHoverAnimationsToNavButtons();
            }
            catch (Exception ex)
            {
                ShowError($"Error during startup: {ex.Message}", ex);
            }
        }
        
        private void AnimateWindowEntrance()
        {
            this.Opacity = 0;
            var fadeAnim = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            this.BeginAnimation(Window.OpacityProperty, fadeAnim);

            if (this.Content is System.Windows.FrameworkElement rootElement)
            {
                rootElement.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
                var scaleTransform = new System.Windows.Media.ScaleTransform(0.95, 0.95);
                rootElement.RenderTransform = scaleTransform;

                var scaleAnimX = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = 0.95,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(500),
                    EasingFunction = new System.Windows.Media.Animation.BackEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut, Amplitude = 0.3 }
                };

                var scaleAnimY = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = 0.95,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(500),
                    EasingFunction = new System.Windows.Media.Animation.BackEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut, Amplitude = 0.3 }
                };

                scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleAnimX);
                scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnimY);
            }
        }

        private void AddHoverAnimationsToNavButtons()
        {
            var buttons = new System.Windows.Controls.Button[] { Menu1B, Menu2B, Menu3B, Menu4B, Menu5B };
            foreach (var btn in buttons)
            {
                if (btn == null) continue;
                
                btn.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
                var scaleTransform = new System.Windows.Media.ScaleTransform(1, 1);
                btn.RenderTransform = scaleTransform;

                btn.MouseEnter += (s, e) =>
                {
                    var scaleAnim = new System.Windows.Media.Animation.DoubleAnimation
                    {
                        To = 1.15, // Scale up more visibly
                        Duration = TimeSpan.FromMilliseconds(150),
                        EasingFunction = new System.Windows.Media.Animation.QuarticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                    };
                    scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleAnim);
                    scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnim);
                };

                btn.MouseLeave += (s, e) =>
                {
                    var scaleAnim = new System.Windows.Media.Animation.DoubleAnimation
                    {
                        To = 1.0,
                        Duration = TimeSpan.FromMilliseconds(250),
                        EasingFunction = new System.Windows.Media.Animation.QuarticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                    };
                    scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleAnim);
                    scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnim);
                };
            }
        }

        /// <summary>
        /// Start the animated gradient glow effect on the window border
        /// </summary>
        private void StartGlowAnimation()
        {
            if (GlowBorder == null) return;

            try
            {
                // Create a smooth pulsing glow effect with easing
                var glowAnimation = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = 0.3,
                    To = 0.7,
                    Duration = TimeSpan.FromSeconds(2.5),
                    RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                    AutoReverse = true,
                    EasingFunction = new System.Windows.Media.Animation.SineEase
                    {
                        EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut
                    }
                };

                GlowBorder.BeginAnimation(Border.OpacityProperty, glowAnimation);

                // Animate the drop shadow blur for a softer glow effect
                if (GlowBorder.Effect is System.Windows.Media.Effects.DropShadowEffect shadowEffect)
                {
                    var blurAnimation = new System.Windows.Media.Animation.DoubleAnimation
                    {
                        From = 10,
                        To = 25,
                        Duration = TimeSpan.FromSeconds(2.5),
                        RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                        AutoReverse = true,
                        EasingFunction = new System.Windows.Media.Animation.SineEase
                        {
                            EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut
                        }
                    };

                    shadowEffect.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.BlurRadiusProperty, blurAnimation);

                    // Also animate shadow opacity for a breathing effect
                    var shadowOpacityAnimation = new System.Windows.Media.Animation.DoubleAnimation
                    {
                        From = 0.5,
                        To = 1.0,
                        Duration = TimeSpan.FromSeconds(2.5),
                        RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                        AutoReverse = true,
                        EasingFunction = new System.Windows.Media.Animation.SineEase
                        {
                            EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut
                        }
                    };

                    shadowEffect.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, shadowOpacityAnimation);
                }

                // Animate gradient stops for a subtle color shift effect (only if GlowGradient exists)
                if (GlowGradient is LinearGradientBrush gradientBrush && gradientBrush.GradientStops.Count >= 2)
                {
                    var baseColor = System.Windows.Media.Color.FromRgb(0x72, 0x2E, 0xD1);
                    var lightColor = System.Windows.Media.Color.FromRgb(0x9B, 0x5A, 0xF0);
                    var darkColor = System.Windows.Media.Color.FromRgb(0x53, 0x1D, 0xAB);

                    // Animate the first gradient stop color (main accent)
                    var colorAnimation1 = new System.Windows.Media.Animation.ColorAnimation
                    {
                        From = baseColor,
                        To = lightColor,
                        Duration = TimeSpan.FromSeconds(4),
                        RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                        AutoReverse = true,
                        EasingFunction = new System.Windows.Media.Animation.SineEase
                        {
                            EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut
                        }
                    };

                    if (gradientBrush.GradientStops[0] != null)
                        gradientBrush.GradientStops[0].BeginAnimation(GradientStop.ColorProperty, colorAnimation1);

                    // Animate the second gradient stop color (darker accent)
                    if (gradientBrush.GradientStops.Count > 1)
                    {
                        var colorAnimation2 = new System.Windows.Media.Animation.ColorAnimation
                        {
                            From = darkColor,
                            To = baseColor,
                            Duration = TimeSpan.FromSeconds(3.5),
                            RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                            AutoReverse = true,
                            EasingFunction = new System.Windows.Media.Animation.SineEase
                            {
                                EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut
                            }
                        };

                        gradientBrush.GradientStops[1].BeginAnimation(GradientStop.ColorProperty, colorAnimation2);
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Warning, $"Glow animation error: {ex.Message}", false);
            }
        }

        private void InitializeMenus()
        {
            foreach (var menu in MenuNames)
            {
                _menuControls[menu] = null;
                _menuInitialized[menu] = false;
            }
        }

        private void InitializeFileManagerEarly()
        {
            var modelMenu = new ModelMenuControl();
            modelMenu.Initialize(this);
            _menuControls["ModelMenu"] = modelMenu;
            InitializeFileManager(modelMenu);
        }

        private void LoadInitialMenu()
        {
            LoadMenu("AimMenu");
            // Don't call UpdateSliderVisibility here - it would override collapsed menu states
            // Visibility is handled by the toggle click actions when user interacts with toggles
            _currentMenu = "AimMenu";
        }

        private async Task InitializeApplicationAsync()
        {
            CheckRunningFromTemp();

            // Initialize DisplayManager FIRST before anything else that depends on display info
            DisplayManager.Initialize();

            // Now that DisplayManager is initialized, we can create windows
            InitializeWindows();

            EnsureRequiredFiles();

            SetupKeybindings();
            ConfigurePropertyChangers();
            ApplyInitialSettings();
            ListenForKeybinds();

            // Subscribe to display changes after everything is initialized
            DisplayManager.DisplayChanged += OnDisplayChanged;
        }

        private void OnDisplayChanged(object? sender, DisplayChangedEventArgs e)
        {

            // Force update all windows to new display
            DisplayManager.ForceUpdateWindows();
        }

        private void CheckRunningFromTemp()
        {
            if (Directory.GetCurrentDirectory().Contains("Temp"))
            {
                MessageBox.Show(
                    "Hi, it is made aware that you are running Aimmy without extracting it from the zip file. " +
                    "Please extract Aimmy from the zip file or Aimmy will not be able to run properly.\n\nThank you.",
                    "Aimmy V2");
            }
        }

        private void InitializeWindows()
        {
            // Create windows but don't show them yet
            var fov = FOVWindow;  // This triggers lazy initialization
            var dpw = DPWindow;   // This triggers lazy initialization

            // Ensure they're positioned on the current display
            fov.ForceReposition();
            dpw.ForceReposition();

            // Set references in Dictionary
            Dictionary.DetectedPlayerOverlay = dpw;
            Dictionary.FOVWindow = fov;
        }

        private void EnsureRequiredFiles()
        {
            var labelsPath = "bin\\labels\\labels.txt";
            var labelsDir = Path.GetDirectoryName(labelsPath);

            // Ensure the directory exists
            if (!string.IsNullOrEmpty(labelsDir) && !Directory.Exists(labelsDir))
            {
                Directory.CreateDirectory(labelsDir);
            }

            // Create the file if it doesn't exist
            if (!File.Exists(labelsPath))
            {
                File.WriteAllText(labelsPath, "Enemy");
            }
        }

        private async Task LoadConfigurationsAsync()
        {
            // Run non-UI operations in background
            await Task.Run(() =>
            {
                // Load configurations that don't create UI
                var configs = new[]
                {
                    (Dictionary.minimizeState, "bin\\minimize.cfg"),
                    (Dictionary.bindingSettings, "bin\\binding.cfg"),
                    (Dictionary.colorState, "bin\\colors.cfg"),
                    (Dictionary.filelocationState, "bin\\filelocations.cfg"),
                    (Dictionary.dropdownState, "bin\\dropdown.cfg"),
                    (Dictionary.toggleState, "bin\\toggles.cfg")
                };

                foreach (var (dict, path) in configs)
                {
                    SaveDictionary.LoadJSON(dict, path);
                }
                
                // Ensure Hide Real Controller is always false on startup
                if (Dictionary.toggleState.ContainsKey("Hide Real Controller"))
                {
                    Dictionary.toggleState["Hide Real Controller"] = false;
                }
            });

            // Load these on UI thread since they might show notifications
            LoadConfig();
            ApplyThemeColorFromConfig();
        }


        private void ApplyThemeColorFromConfig()
        {
            if (Dictionary.colorState.TryGetValue("Theme Color", out var themeColor))
            {
                var colorString = themeColor?.ToString();
                if (!string.IsNullOrEmpty(colorString))
                {
                    try
                    {
                        ThemeManager.SetThemeColor(colorString);
                    }
                    catch (Exception ex)
                    {
                    }
                }
            }
        }

        private void ApplyUIFont()
        {
            if (Dictionary.dropdownState.TryGetValue("UI Font", out var fontObj) && fontObj != null)
            {
                var fontName = fontObj.ToString() ?? "Atkinson Hyperlegible";
                ApplyFontToUI(fontName);
            }
        }

        private void SetupKeybindings()
        {
            var keybinds = new[]
            {
                "Aim Keybind", "Second Aim Keybind", "Dynamic FOV Keybind",
                "Emergency Stop Keybind", "Model Switch Keybind",
                "Anti Recoil Keybind", "Disable Anti Recoil Keybind"
            };

            foreach (var keybind in keybinds)
            {
                if (!Dictionary.bindingSettings.ContainsKey(keybind))
                {
                    Dictionary.bindingSettings[keybind] = "None"; // Default fallback
                }
                bindingManager.SetupDefault(keybind, Dictionary.bindingSettings[keybind].ToString());
            }
        }

        private void ConfigurePropertyChangers()
        {
            PropertyChanger.ReceiveNewConfig = LoadConfig;
        }

        private void ApplyInitialSettings()
        {
            // FOV settings
            ActualFOV = Convert.ToDouble(Dictionary.sliderSettings["FOV Size"]);
            PropertyChanger.PostNewFOVSize(ActualFOV);
            PropertyChanger.PostColor((Color)ColorConverter.ConvertFromString(Dictionary.colorState["FOV Color"].ToString()));

            // Detected player window settings
            var dpSettings = new[]
            {
                ("Detected Player Color", (Action<object>)(c => PropertyChanger.PostDPColor((Color)c))),
                ("AI Confidence Font Size", (Action<object>)(s => PropertyChanger.PostDPFontSize((int)(double)s))),
                ("Corner Radius", (Action<object>)(r => PropertyChanger.PostDPWCornerRadius((int)(double)r))),
                ("Border Thickness", (Action<object>)(t => PropertyChanger.PostDPWBorderThickness((double)t))),
                ("Opacity", (Action<object>)(o => PropertyChanger.PostDPWOpacity((double)o)))
            };

            foreach (var (key, action) in dpSettings)
            {
                if (key.Contains("Color"))
                {
                    action(ColorConverter.ConvertFromString(Dictionary.colorState[key].ToString()));
                }
                else
                {
                    action(Convert.ToDouble(Dictionary.sliderSettings[key]));
                }
            }
        }

        private void UpdateAboutSpecs()
        {
            if (_menuControls["AboutMenu"] is AboutMenuControl aboutMenu)
            {
                aboutMenu.AboutSpecsControl.Content = "Loading system specs...";

                Task.Run(() =>
                {
                    var specs = $"{GetProcessorName()} • {GetVideoControllerName()} • {GetFormattedMemorySize()}GB RAM";
                    Dispatcher.Invoke(() => aboutMenu.AboutSpecsControl.Content = specs);
                });
            }
        }

        public void UpdateHeaderStatuses()
        {
            Dispatcher.Invoke(() =>
            {
                // Update model status
                if (FindName("HeaderModelStatus") is System.Windows.Controls.TextBlock modelText)
                {
                    bool modelLoaded = Dictionary.lastLoadedModel != "N/A";
                    modelText.Text = modelLoaded ? $"Model: {Dictionary.lastLoadedModel}" : "Model: None";
                    
                    // Show/hide animated status dot
                    if (FindName("ModelStatusDot") is System.Windows.Shapes.Ellipse modelDot)
                    {
                        modelDot.Visibility = modelLoaded 
                            ? System.Windows.Visibility.Visible 
                            : System.Windows.Visibility.Collapsed;
                        
                        // Start animation when visible
                        if (modelLoaded)
                        {
                            StartStatusDotAnimation(modelDot, 0.8);
                        }
                    }
                }

                // Update trigger status
                if (FindName("HeaderTriggerStatus") is System.Windows.Controls.TextBlock triggerText)
                {
                    bool triggerOn = Dictionary.toggleState.TryGetValue("Auto Trigger", out var val) && val is bool b && b;
                    triggerText.Text = triggerOn ? "Trigger: On" : "Trigger: Off";
                    
                    // Show/hide animated status dot
                    if (FindName("TriggerStatusDot") is System.Windows.Shapes.Ellipse triggerDot)
                    {
                        triggerDot.Visibility = triggerOn 
                            ? System.Windows.Visibility.Visible 
                            : System.Windows.Visibility.Collapsed;
                        
                        // Start animation when visible
                        if (triggerOn)
                        {
                            StartStatusDotAnimation(triggerDot, 0.6);
                        }
                    }
                }

                // Update display status
                if (FindName("HeaderDisplayStatus") is System.Windows.Controls.TextBlock displayText)
                {
                    displayText.Text = $"Display: {DisplayManager.CurrentDisplayIndex + 1}";
                }
            });
        }
        
        /// <summary>
        /// Start pulsing animation for a status dot using scale transform
        /// </summary>
        private void StartStatusDotAnimation(System.Windows.Shapes.Ellipse dot, double durationSeconds)
        {
            if (dot == null) return;

            try
            {
                // Stop any existing animations on this element
                dot.BeginAnimation(System.Windows.Shapes.Ellipse.OpacityProperty, null);
                dot.RenderTransform?.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, null);
                dot.RenderTransform?.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, null);

                // Get or create the scale transform
                var scaleTransform = dot.RenderTransform as System.Windows.Media.ScaleTransform;
                if (scaleTransform == null)
                {
                    scaleTransform = new System.Windows.Media.ScaleTransform(1, 1);
                    dot.RenderTransform = scaleTransform;
                    dot.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
                }

                // Create smooth scale animation for a pulsing "heartbeat" effect
                var scaleXAnimation = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = 1.0,
                    To = 1.3,
                    Duration = TimeSpan.FromSeconds(durationSeconds / 2),
                    AutoReverse = true,
                    RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                    EasingFunction = new System.Windows.Media.Animation.SineEase
                    {
                        EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut
                    }
                };

                var scaleYAnimation = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = 1.0,
                    To = 1.3,
                    Duration = TimeSpan.FromSeconds(durationSeconds / 2),
                    AutoReverse = true,
                    RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                    EasingFunction = new System.Windows.Media.Animation.SineEase
                    {
                        EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut
                    }
                };

                // Also add a subtle opacity animation for depth
                var opacityAnimation = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = 1.0,
                    To = 0.6,
                    Duration = TimeSpan.FromSeconds(durationSeconds),
                    AutoReverse = true,
                    RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                    EasingFunction = new System.Windows.Media.Animation.SineEase
                    {
                        EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut
                    }
                };

                scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleXAnimation);
                scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleYAnimation);
                dot.BeginAnimation(System.Windows.Shapes.Ellipse.OpacityProperty, opacityAnimation);
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Warning, $"Status dot animation error: {ex.Message}", false);
            }
        }

        private void ShowError(string message, Exception ex)
        {
            MessageBox.Show($"{message}\n\nStack trace: {ex.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void ApplyThemeGradients()
        {
            if (!Dictionary.colorState.TryGetValue("Theme Color", out var themeColor)) return;

            var colorString = themeColor?.ToString();
            if (string.IsNullOrEmpty(colorString)) return;

            try
            {
                var color = (Color)ColorConverter.ConvertFromString(colorString);

                var gradientMappings = new Dictionary<string, Func<Color, Color>>
                {
                    ["GradientThemeStop"] = c => Color.FromRgb((byte)(c.R * 0.3), (byte)(c.G * 0.3), (byte)(c.B * 0.3)),
                    ["HighlighterGradient1"] = c => c,
                    ["HighlighterGradient2"] = c => Color.FromArgb(102, c.R, c.G, c.B)
                };

                foreach (var (elementName, colorTransform) in gradientMappings)
                {
                    if (FindName(elementName) is GradientStop gradientStop)
                    {
                        gradientStop.Color = colorTransform(color);
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }

        private void InitializeFileManager(ModelMenuControl modelMenu)
        {
            if (_fileManager == null)
            {
                _fileManager = new Lazy<FileManager>(() => new FileManager(
                    modelMenu.ModelListBoxControl,
                    modelMenu.SelectedModelNotifierControl,
                    modelMenu.ConfigsListBoxControl,
                    modelMenu.SelectedConfigNotifierControl));

                try
                {
                    var fm = _fileManager.Value;
                }
                catch (Exception ex)
                {
                }
            }
        }

        #endregion

        #region Window Events

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                Maximize.Content = "\xE922"; // Maximize icon
            }
            else
            {
                WindowState = WindowState.Maximized;
                Maximize.Content = "\xE923"; // Restore down icon
            }
        }
        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                Maximize.Content = "\xE923"; // Restore down icon
            }
            else
            {
                Maximize.Content = "\xE922"; // Maximize icon
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_fileManager?.IsValueCreated == true)
            {
                fileManager.InQuittingState = true;
            }

            DisableAllFeatures();
            CloseWindows();
            CleanupDrivers();

            // Dispose menu controls to save their states
            if (_menuControls["AimMenu"] is AimMenuControl aimMenu)
                aimMenu.Dispose();

            if (_menuControls["SettingsMenu"] is SettingsMenuControl settingsMenu)
                settingsMenu.Dispose();

            SaveAllConfigurations();
            FileManager.AIManager?.Dispose();

            // Clean up display manager
            DisplayManager.DisplayChanged -= OnDisplayChanged;
            DisplayManager.Dispose();

            Application.Current.Shutdown();
        }

        private void HandleAntiRecoil(bool start)
        {
            if (!Dictionary.toggleState["Anti-Recoil"]) return;

            if (start)
            {
                arManager.IndependentMousePress = 0;
                arManager.HoldDownTimer.Start();
            }
            else
            {
                arManager.HoldDownTimer.Stop();
                arManager.IndependentMousePress = 0;
            }
        }

        private void DisableAntiRecoil()
        {
            if (!Dictionary.toggleState["Anti-Recoil"]) return;

            Dictionary.toggleState["Anti-Recoil"] = false;
            
            // Check if T_AntiRecoil is in the toggleInstances list, as it might not be initialized yet
            if (toggleInstances.TryGetValue("Anti-Recoil", out AToggle toggle))
            {
                UpdateToggleUI(toggle, false);
            }
            
            new NoticeBar("[Disable Anti Recoil Keybind] Disabled Anti-Recoil.", 4000).Show();
        }

        private void DisableAllFeatures()
        {
            var features = new[] { "Aim Assist", "FOV", "Show Detected Player" };
            foreach (var feature in features)
            {
                Dictionary.toggleState[feature] = false;
            }
        }

        private void CloseWindows()
        {
            FOVWindow.Close();
            DPWindow.Close();
        }

        private void CleanupDrivers()
        {
            if (Dictionary.dropdownState.TryGetValue("Mouse Movement Method", out var method) &&
                method?.ToString() == "LG HUB")
            {
                LGMouse.Close();
            }
            // Cleanup ViGEm virtual controller
            VirtualControllerOutput.Disconnect();
            
            // Clean up HidHide cloaking
            Other.HidHideManager.DisableAndCleanup();
        }

        private void SaveAllConfigurations()
        {
            Dictionary.colorState["Theme Color"] = ThemeManager.GetThemeColorHex();

            SaveDictionary.WriteJSON(Dictionary.sliderSettings
                .Concat(Dictionary.dropdownState)
                //.Where(kvp => kvp.Key != "Screen Capture Method")
                .GroupBy(kvp => kvp.Key)
                .ToDictionary(g => g.Key, g => g
                .First().Value));
            SaveDictionary.WriteJSON(Dictionary.minimizeState, "bin\\minimize.cfg");
            SaveDictionary.WriteJSON(Dictionary.bindingSettings, "bin\\binding.cfg");
            SaveDictionary.WriteJSON(Dictionary.dropdownState, "bin\\dropdown.cfg");
            SaveDictionary.WriteJSON(Dictionary.colorState, "bin\\colors.cfg");
            SaveDictionary.WriteJSON(Dictionary.filelocationState, "bin\\filelocations.cfg");
            SaveDictionary.WriteJSON(Dictionary.toggleState, "bin\\toggles.cfg");
        }

        #endregion

        #region Menu Management

        private UserControl GetOrCreateMenuControl(string menuName)
        {
            if (_menuControls[menuName] != null)
                return _menuControls[menuName]!;

            var newControl = menuName == "ModelMenu" && _menuControls["ModelMenu"] != null
                ? _menuControls["ModelMenu"]!
                : CreateMenuControl(menuName);

            _menuControls[menuName] = newControl;

            if (!_menuInitialized[menuName])
            {
                InitializeMenuControl(menuName, newControl);
                _menuInitialized[menuName] = true;
            }

            return newControl;
        }

        private UserControl CreateMenuControl(string menuName) => menuName switch
        {
            "AimMenu" => new AimMenuControl(),
            "ModelMenu" => new ModelMenuControl(),
            "SettingsMenu" => new SettingsMenuControl(),
            "AboutMenu" => new AboutMenuControl(),
            "ControllerMenu" => new ControllerMenuControl(),
            _ => throw new ArgumentException($"Unknown menu: {menuName}")
        };

        private void InitializeMenuControl(string menuName, UserControl control)
        {
            try
            {
                switch (control)
                {
                    case AimMenuControl aimMenu:
                        aimMenu.Initialize(this);
                        CurrentScrollViewer = aimMenu.AimMenuScrollViewer;
                        LoadDropdownStates();
                        break;

                    case ModelMenuControl modelMenu:
                        if (!_menuInitialized["ModelMenu"])
                            modelMenu.Initialize(this);
                        break;

                    case SettingsMenuControl settingsMenu:
                        settingsMenu.Initialize(this);
                        LoadDropdownStates();
                        SettingsMenuControlInstance = settingsMenu;
                        break;

                    case AboutMenuControl aboutMenu:
                        aboutMenu.Initialize(this);
                        UpdateAboutSpecs();
                        break;

                    case ControllerMenuControl controllerMenu:
                        controllerMenu.Initialize(this);
                        break;
                }
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText("error.log", $"[{DateTime.Now}] InitializeMenuControl error ({menuName}): {ex.Message}\n{ex.StackTrace}\n\n");
            }
        }

        public void ReloadCurrentConfig()
        {
            var path = Dictionary.lastLoadedConfig != "N/A"
                ? Path.Combine("bin\\configs", Dictionary.lastLoadedConfig)
                : "bin\\configs\\Default.cfg";
            LoadConfig(path, true);
        }

        public string GetActiveSetupSummary()
        {
            var model = Dictionary.lastLoadedModel == "N/A" ? "No model loaded" : Dictionary.lastLoadedModel;
            var config = Dictionary.lastLoadedConfig == "N/A" ? "No config loaded" : Dictionary.lastLoadedConfig;
            var display = $"Display {DisplayManager.CurrentDisplayIndex + 1}";
            return $"Model: {model}\nConfig: {config}\n{display}";
        }

        private void LoadMenu(string menuName)
        {
            var control = GetOrCreateMenuControl(menuName);
            ContentArea.Children.Clear();
            ContentArea.Children.Add(control);
            _currentControl = control;
            UpdateCurrentScrollViewer(menuName, control);

            if (IsBetaUIEnabled)
            {
                var scheme = ThemeManager.CurrentScheme ?? ThemeManager.GenerateMaterial3Scheme(ThemeManager.ThemeColor);
                ApplyM3ColorsToActiveMenu(scheme);
            }
        }

        private void UpdateCurrentScrollViewer(string menuName, UserControl control)
        {
            CurrentScrollViewer = control switch
            {
                AimMenuControl aim => aim.AimMenuScrollViewer,
                ModelMenuControl model => model.ModelMenuScrollViewer,
                SettingsMenuControl settings => settings.SettingsMenuScrollViewer,
                AboutMenuControl about => about.AboutMenuScrollViewer,
                _ => CurrentScrollViewer
            };
        }

        private async void MenuSwitch(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string newMenuName } ||
                !IsValidMenu(newMenuName) ||
                _currentlySwitching ||
                _currentMenu == newMenuName) return;

            _currentlySwitching = true;

            try
            {
                var targetButton = (Button)sender;
                Thickness targetMargin;
                if (IsBetaUIEnabled)
                {
                    targetMargin = new Thickness(8, targetButton.Margin.Top + 9, 0, 0);
                    var scheme = ThemeManager.CurrentScheme ?? ThemeManager.GenerateMaterial3Scheme(ThemeManager.ThemeColor);
                    _currentMenu = newMenuName;
                    UpdateNavButtonColors(scheme);
                }
                else
                {
                    targetMargin = targetButton.Margin;
                }

                Animator.ObjectShift(
                    TimeSpan.FromMilliseconds(150), // Fade between menu buttons
                    MenuHighlighter,
                    MenuHighlighter.Margin,
                    targetMargin);

                await SwitchToMenu(newMenuName);
                _currentMenu = newMenuName;
            }
            catch (Exception ex)
            {
            }
            finally
            {
                _currentlySwitching = false;
            }
        }

        private bool IsValidMenu(string? menuName) =>
            !string.IsNullOrEmpty(menuName) && _menuControls.ContainsKey(menuName!);

        private async Task SwitchToMenu(string menuName)
        {
            if (_currentControl != null)
            {
                Animator.SlideAndFadeOut(_currentControl, -15);
                await Task.Delay(200); // Wait for slide out animation
            }

            LoadMenu(menuName);
            Animator.SlideAndFadeIn(_currentControl!, 15);
        }

        #endregion

        #region Toggle Actions

        internal void Toggle_Action(string title)
        {
            var actions = new Dictionary<string, Action>
            {
                ["FOV"] = () =>
                {
                    FOVWindow.Visibility = GetToggleVisibility(title);
                    // Force reposition when showing the window
                    if (Dictionary.toggleState[title])
                    {
                        FOVWindow.ForceReposition();
                    }
                },
                ["Sticky Aim"] = () => UpdateSliderVisibility(uiManager),
                ["Show Detected Player"] = () =>
                {
                    ShowHideDPWindow();
                    DPWindow.DetectedPlayerFocus.Visibility = GetToggleVisibility(title, true);
                    // Force reposition when showing the window
                    if (Dictionary.toggleState[title])
                    {
                        DPWindow.ForceReposition();
                    }
                },
                ["Show AI Confidence"] = () => DPWindow.DetectedPlayerConfidence.Visibility = GetToggleVisibility(title, true),
                ["Mouse Background Effect"] = () => { if (!Dictionary.toggleState[title]) RotaryGradient.Angle = 0; },
                ["UI TopMost"] = () => Topmost = Dictionary.toggleState[title],
                ["StreamGuard"] = () =>
                {
                    StreamGuardManager.ApplyStreamGuardToAllWindows(Dictionary.toggleState[title]);
                },
                ["EMA Smoothening"] = () =>
                {
                    MouseManager.IsEMASmoothingEnabled = Dictionary.toggleState[title];
                },
                ["X Axis Percentage Adjustment"] = () => UpdateSliderVisibility(uiManager),
                ["Y Axis Percentage Adjustment"] = () => UpdateSliderVisibility(uiManager),
                ["Auto Trigger"] = () => UpdateHeaderStatuses(),
                ["Beta UI"] = () => ApplyBetaUITheme(Dictionary.toggleState["Beta UI"])
            };

            if (actions.TryGetValue(title, out var action))
            {
                action();
            }
        }
        private static void UpdateSliderVisibility(UI uiManager)
        {
            bool useYPercent = Dictionary.toggleState["Y Axis Percentage Adjustment"];
            bool useXPercent = Dictionary.toggleState["X Axis Percentage Adjustment"];
            bool thresholdEnabled = Dictionary.toggleState["Sticky Aim"];

            // Null checks in case AimMenu hasn't been loaded yet
            if (uiManager.S_StickyAimThreshold != null)
                uiManager.S_StickyAimThreshold.Visibility = thresholdEnabled ? Visibility.Visible : Visibility.Collapsed;

            if (uiManager.S_YOffset != null)
                uiManager.S_YOffset.Visibility = useYPercent ? Visibility.Collapsed : Visibility.Visible;
            if (uiManager.S_YOffsetPercent != null)
                uiManager.S_YOffsetPercent.Visibility = useYPercent ? Visibility.Visible : Visibility.Collapsed;

            if (uiManager.S_XOffset != null)
                uiManager.S_XOffset.Visibility = useXPercent ? Visibility.Collapsed : Visibility.Visible;
            if (uiManager.S_XOffsetPercent != null)
                uiManager.S_XOffsetPercent.Visibility = useXPercent ? Visibility.Visible : Visibility.Collapsed;
        }

        private Visibility GetToggleVisibility(string title, bool collapsed = false) =>
            Dictionary.toggleState[title]
                ? Visibility.Visible
                : (collapsed ? Visibility.Collapsed : Visibility.Hidden);

        private static void ShowHideDPWindow()
        {
            if (Dictionary.toggleState["Show Detected Player"])
            {
                DPWindow.Show();
                // Force reposition when showing
                DPWindow.ForceReposition();
            }
            else
            {
                DPWindow.Hide();
            }
        }

        private void ApplyBetaUITheme(bool enabled)
        {
            // Beta UI is temporarily disabled to prevent performance issues.
            return;
        }

        private void DISABLED_ApplyBetaUITheme(bool enabled)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (enabled)
                {
                    var scheme = ThemeManager.GenerateMaterial3Scheme(ThemeManager.ThemeColor);

                    // ── Window shape ──
                    MainBorder.CornerRadius = new CornerRadius(28);
                    GlowBorder.CornerRadius = new CornerRadius(28);

                    // ── Surface background: solid surface color ──
                    MainBorder.Background = new SolidColorBrush(scheme.Surface);
                    MainBorder.BorderBrush = new SolidColorBrush(scheme.OutlineVariant);
                    MainBorder.BorderThickness = new Thickness(1);

                    // ── Elevation shadow: M3 Level 2 ──
                    MainBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        BlurRadius = 24,
                        Opacity = 0.28,
                        Color = Colors.Black,
                        ShadowDepth = 6
                    };

                    // ── Navigation Rail: surface-container and width 72 ──
                    Sidebar.Width = 72;
                    Sidebar.Background = new SolidColorBrush(scheme.SurfaceContainer);
                    // Round the rail's inner left corners to match window rounding
                    if (Sidebar is Grid sidebarGrid)
                    {
                        var railBorder = new Border
                        {
                            CornerRadius = new CornerRadius(28, 0, 0, 28),
                            Background = new SolidColorBrush(scheme.SurfaceContainer),
                            ClipToBounds = true
                        };
                    }

                    // ── Active indicator pill on nav rail ──
                    if (MenuHighlighter != null)
                    {
                        MenuHighlighter.Width = 56;
                        MenuHighlighter.Height = 32;
                        MenuHighlighter.Background = new SolidColorBrush(scheme.SecondaryContainer);
                        MenuHighlighter.Effect = null;
                        MenuHighlighter.ClipToBounds = true;
                        MenuHighlighter.CornerRadius = new CornerRadius(16);
                        MenuHighlighter.Opacity = 1.0;
                    }

                    // ── Nav rail button width ──
                    foreach (var menuButton in new[] { Menu1B, Menu2B, Menu3B, Menu4B, Menu5B })
                    {
                        if (menuButton != null)
                        {
                            menuButton.Width = 72;
                        }
                    }

                    // ── Update highlighter position & button colors immediately ──
                    UpdateHighlighterPosition(true);
                    UpdateNavButtonColors(scheme);

                    // ── Content area: surface ──
                    ContentArea.Background = new SolidColorBrush(scheme.Surface);
                    ContentArea.Margin = new Thickness(72, 56, 0, 0);

                    // ── Top app bar: surface-container ──
                    Topbar.Background = new SolidColorBrush(scheme.SurfaceContainer);

                    // ── Top bar text colors ──
                    if (HeaderModelStatus != null)
                        HeaderModelStatus.Foreground = new SolidColorBrush(scheme.OnSurfaceVariant);
                    if (HeaderTriggerStatus != null)
                        HeaderTriggerStatus.Foreground = new SolidColorBrush(scheme.OnSurfaceVariant);
                    if (HeaderDisplayStatus != null)
                        HeaderDisplayStatus.Foreground = new SolidColorBrush(scheme.OnSurfaceVariant);

                    // ── Top status badges: pill shape & surface container background ──
                    if (ModelStatusBadge != null)
                    {
                        ModelStatusBadge.CornerRadius = new CornerRadius(28);
                        ModelStatusBadge.Background = new SolidColorBrush(scheme.SurfaceContainerHighest);
                        ModelStatusBadge.BorderBrush = new SolidColorBrush(scheme.OutlineVariant);
                    }
                    if (TriggerStatusBadge != null)
                    {
                        TriggerStatusBadge.CornerRadius = new CornerRadius(28);
                        TriggerStatusBadge.Background = new SolidColorBrush(scheme.SurfaceContainerHighest);
                        TriggerStatusBadge.BorderBrush = new SolidColorBrush(scheme.OutlineVariant);
                    }
                    if (DisplayStatusBadge != null)
                    {
                        DisplayStatusBadge.CornerRadius = new CornerRadius(28);
                        DisplayStatusBadge.Background = new SolidColorBrush(scheme.SurfaceContainerHighest);
                        DisplayStatusBadge.BorderBrush = new SolidColorBrush(scheme.OutlineVariant);
                    }

                    // ── Glow border: subtle primary tint ──
                    GlowBorder.Opacity = 0.3;
                    if (GlowBorder.BorderBrush is LinearGradientBrush glowGrad)
                    {
                        foreach (var stop in glowGrad.GradientStops)
                        {
                            stop.Color = scheme.Primary;
                        }
                    }

                    // ── Dynamic Resources for Tooltips/Tabs ──
                    this.Resources["M3TabUnderlineBrush"] = new SolidColorBrush(scheme.Primary);
                    this.Resources["M3TooltipBackground"] = new SolidColorBrush(scheme.SurfaceContainerHighest);
                    this.Resources["M3TooltipForeground"] = new SolidColorBrush(scheme.OnSurface);
                    this.Resources["M3TooltipAccentBrush"] = new SolidColorBrush(scheme.Primary);
                    this.Resources["M3TooltipBorderBrush"] = Brushes.Transparent;
                    this.Resources["M3TooltipCornerRadius"] = new CornerRadius(8);

                    // Notify all listeners that Beta UI is now active (so internal controls update themselves)
                    ThemeManager.SetBetaUIState(true);

                    // Apply M3 colors & dynamic card groupings to active menu
                    ApplyM3ColorsToActiveMenu(scheme);

                    // Apply Font Awesome 6 icons to navigation rail
                    IconManager.UpdateNavRailIcons(true);
                }
                else
                {
                    // ── Restore original styling ──
                    MainBorder.CornerRadius = new CornerRadius(5);
                    GlowBorder.CornerRadius = new CornerRadius(5);

                    var brush = new LinearGradientBrush
                    {
                        EndPoint = new Point(0.5, 1),
                        StartPoint = new Point(0, 0)
                    };
                    brush.GradientStops.Add(new GradientStop(Colors.Black, 0.27));
                    brush.GradientStops.Add(new GradientStop(Color.FromArgb(0xFF, 0x12, 0x03, 0x38), 1.0));
                    MainBorder.Background = brush;

                    Sidebar.Width = 50;
                    Sidebar.Background = null;
                    ContentArea.Background = null;
                    ContentArea.Margin = new Thickness(50, 56, 0, 0);
                    Topbar.Background = new SolidColorBrush(Color.FromArgb(0x12, 0, 0, 0));

                    MainBorder.Effect = null;
                    MainBorder.BorderBrush = new SolidColorBrush(Colors.Black);
                    MainBorder.BorderThickness = new Thickness(1, 1, 1, 1);

                    // Restore nav rail highlighter
                    if (MenuHighlighter != null)
                    {
                        MenuHighlighter.Width = 50;
                        MenuHighlighter.Height = 50;
                        MenuHighlighter.CornerRadius = new CornerRadius(0);
                        MenuHighlighter.Opacity = 1.0;
                        MenuHighlighter.Background = new RadialGradientBrush(
                            new GradientStopCollection
                            {
                                new GradientStop(ThemeManager.ThemeColor, 0),
                                new GradientStop(Color.FromArgb(0x33, ThemeManager.ThemeColor.R, ThemeManager.ThemeColor.G, ThemeManager.ThemeColor.B), 0.3),
                                new GradientStop(Colors.Transparent, 0.9)
                            });
                        MenuHighlighter.Effect = new System.Windows.Media.Effects.BlurEffect { Radius = 5 };
                    }

                    // Restore nav buttons width & foreground
                    foreach (var menuButton in new[] { Menu1B, Menu2B, Menu3B, Menu4B, Menu5B })
                    {
                        if (menuButton != null)
                        {
                            menuButton.Width = 50;
                            menuButton.Foreground = new SolidColorBrush(Colors.White);
                        }
                    }

                    // Restore position
                    UpdateHighlighterPosition(false);

                    // Restore header text colors
                    var headerWhite = new SolidColorBrush(Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF));
                    if (HeaderModelStatus != null)
                        HeaderModelStatus.Foreground = headerWhite;
                    if (HeaderTriggerStatus != null)
                        HeaderTriggerStatus.Foreground = headerWhite;
                    if (HeaderDisplayStatus != null)
                        HeaderDisplayStatus.Foreground = headerWhite;

                    // ── Restore status badge styling ──
                    if (ModelStatusBadge != null)
                    {
                        ModelStatusBadge.CornerRadius = new CornerRadius(8);
                        ModelStatusBadge.Background = new SolidColorBrush(Color.FromArgb(0x1E, 0xFF, 0xFF, 0xFF));
                        ModelStatusBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF));
                    }
                    if (TriggerStatusBadge != null)
                    {
                        TriggerStatusBadge.CornerRadius = new CornerRadius(8);
                        TriggerStatusBadge.Background = new SolidColorBrush(Color.FromArgb(0x1E, 0xFF, 0xFF, 0xFF));
                        TriggerStatusBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF));
                    }
                    if (DisplayStatusBadge != null)
                    {
                        DisplayStatusBadge.CornerRadius = new CornerRadius(8);
                        DisplayStatusBadge.Background = new SolidColorBrush(Color.FromArgb(0x1E, 0xFF, 0xFF, 0xFF));
                        DisplayStatusBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF));
                    }

                    // Restore glow
                    GlowBorder.Opacity = 0.6;

                    // Revert navigation rail icons to Segoe MDL2
                    IconManager.UpdateNavRailIcons(false);

                    // ── Revert Dynamic Resources ──
                    this.Resources["M3TabUnderlineBrush"] = new SolidColorBrush(Color.FromArgb(0xAA, 0xFF, 0xFF, 0xFF));
                    this.Resources["M3TooltipBackground"] = new SolidColorBrush(Color.FromRgb(0x19, 0x19, 0x19));
                    this.Resources["M3TooltipForeground"] = Brushes.White;
                    this.Resources["M3TooltipAccentBrush"] = new SolidColorBrush(ThemeManager.ThemeColor);
                    this.Resources["M3TooltipBorderBrush"] = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
                    this.Resources["M3TooltipCornerRadius"] = new CornerRadius(6);

                    // Notify all listeners that Beta UI is now inactive
                    ThemeManager.SetBetaUIState(false);
                }
            });
        }

        private Button? GetButtonForMenu(string menuName)
        {
            return menuName switch
            {
                "AimMenu" => Menu1B,
                "ControllerMenu" => Menu2B,
                "ModelMenu" => Menu3B,
                "SettingsMenu" => Menu4B,
                "AboutMenu" => Menu5B,
                _ => null
            };
        }

        private void UpdateHighlighterPosition(bool isBetaActive)
        {
            if (MenuHighlighter == null) return;
            var activeBtn = GetButtonForMenu(_currentMenu);
            if (activeBtn == null) return;

            Thickness targetMargin;
            if (isBetaActive)
            {
                double buttonHeight = activeBtn.Height;
                if (double.IsNaN(buttonHeight) || buttonHeight <= 0) buttonHeight = 50;

                double leftOffset = (72 - 56) / 2;
                double topOffset = (buttonHeight - 32) / 2;
                targetMargin = new Thickness(leftOffset, activeBtn.Margin.Top + topOffset, 0, 0);
            }
            else
            {
                targetMargin = activeBtn.Margin;
            }
            MenuHighlighter.Margin = targetMargin;
        }

        private void UpdateNavButtonColors(ThemeManager.Material3Scheme scheme)
        {
            var activeBtn = GetButtonForMenu(_currentMenu);
            foreach (var menuButton in new[] { Menu1B, Menu2B, Menu3B, Menu4B, Menu5B })
            {
                if (menuButton != null)
                {
                    if (menuButton == activeBtn)
                    {
                        menuButton.Foreground = new SolidColorBrush(scheme.OnSecondaryContainer);
                    }
                    else
                    {
                        menuButton.Foreground = new SolidColorBrush(scheme.OnSurfaceVariant);
                    }
                }
            }
        }

        private bool IsBetaUIEnabled => Dictionary.toggleState.TryGetValue("Beta UI", out var val) && val is bool b && b;

        private Border? FindMainOuterBorder(DependencyObject parent, bool allowDescend = true)
        {
            if (parent is Border b && 
                b.Name != "SwitchBorder" && 
                b.Name != "SwitchMoving" && 
                b.Name != "KeyNotifierBorder" && 
                b.Name != "ColorChangingBorder")
                return b;

            if (!allowDescend) return null;
            if (parent is StackPanel || parent is ScrollViewer || parent is ListBox || parent is TabControl)
                return null;

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                bool canDescend = !(child is UserControl) && !(child is StackPanel) && !(child is ScrollViewer) && !(child is ListBox);
                var border = FindMainOuterBorder(child, canDescend);
                if (border != null)
                    return border;
            }
            return null;
        }

        /// <summary>
        /// Walk the current active menu and apply Material 3 surface/border colors to inner controls
        /// </summary>
        private void ApplyM3ColorsToActiveMenu(ThemeManager.Material3Scheme scheme)
        {
            if (ContentArea.Children.Count == 0) return;

            var currentMenu = ContentArea.Children[0];
            if (currentMenu == null) return;

            WalkAndApplyM3(currentMenu, scheme, 0);
        }

        private void WalkAndApplyM3(DependencyObject? element, ThemeManager.Material3Scheme scheme, int depth)
        {
            if (element == null) return;

            // ── ScrollViewer: transparent background ──
            if (element is ScrollViewer sv)
            {
                sv.Background = Brushes.Transparent;
            }

            // ── StackPanel: dynamically group child component borders into unified outlined cards ──
            if (element is StackPanel stackPanel && stackPanel.Name != "ASP1" && stackPanel.Name != "ASP2")
            {
                // Find all child items that have outer borders
                var cardItems = new System.Collections.Generic.List<(FrameworkElement element, Border border)>();
                int childCount = stackPanel.Children.Count;
                for (int i = 0; i < childCount; i++)
                {
                    var child = stackPanel.Children[i] as FrameworkElement;
                    if (child == null) continue;
                    if (child.Visibility == Visibility.Collapsed) continue;

                    // Skip if the child itself is a spacer (spacers don't have borders)
                    if (child.GetType().Name == "ASpacer") continue;

                    var border = FindMainOuterBorder(child);
                    if (border != null)
                    {
                        cardItems.Add((child, border));
                    }
                }

                // Apply dynamic M3 outline card geometry
                int cardCount = cardItems.Count;
                for (int i = 0; i < cardCount; i++)
                {
                    var item = cardItems[i];
                    var childTypeName = item.element.GetType().Name;

                    bool isStart = (i == 0) || (childTypeName == "ATitle");
                    bool isEnd = (i == cardCount - 1) || 
                                 (childTypeName == "ARectangleBottom") || 
                                 (i + 1 < cardCount && cardItems[i + 1].element.GetType().Name == "ATitle");

                    // ATitle is always a start of a card
                    if (childTypeName == "ATitle")
                    {
                        isStart = true;
                    }

                    // If the next item is ARectangleBottom, the current item is the end (since ARectangleBottom is collapsed/height 0)
                    if (i + 1 < cardCount && cardItems[i + 1].element.GetType().Name == "ARectangleBottom")
                    {
                        isEnd = true;
                    }

                    // Apply outlined card geometry (28px rounded corners for Beta UI)
                    if (isStart && isEnd)
                    {
                        item.border.CornerRadius = new CornerRadius(28);
                        item.border.BorderThickness = new Thickness(1);
                    }
                    else if (isStart)
                    {
                        item.border.CornerRadius = new CornerRadius(28, 28, 0, 0);
                        item.border.BorderThickness = new Thickness(1, 1, 1, 1);
                    }
                    else if (isEnd)
                    {
                        item.border.CornerRadius = new CornerRadius(0, 0, 28, 28);
                        item.border.BorderThickness = new Thickness(1, 0, 1, 1);
                    }
                    else
                    {
                        item.border.CornerRadius = new CornerRadius(0);
                        item.border.BorderThickness = new Thickness(1, 0, 1, 1);
                    }

                    item.border.Background = new SolidColorBrush(scheme.SurfaceContainerHigh);
                    item.border.BorderBrush = new SolidColorBrush(scheme.OutlineVariant);
                }
            }

            // ── Border: individual components styling (fallback / skip system items) ──
            if (element is Border borderElement)
            {
                if (borderElement.Name == "SwitchBorder" || 
                    borderElement.Name == "SwitchMoving" || 
                    borderElement.Name == "KeyNotifierBorder" || 
                    borderElement.Name == "ColorChangingBorder")
                {
                    // Handled by component's own Beta UI logic
                }
                else if (borderElement.Name == "MenuHighlighter" || 
                         borderElement.Name == "GlowBorder" || 
                         borderElement.Name == "MainBorder")
                {
                    // Styled by MainWindow top-level
                }
                else if (borderElement.Parent is StackPanel)
                {
                    // Already styled by parent StackPanel pass
                }
                else
                {
                    // Fallback for standalone borders: round 16px, SurfaceContainerHigh
                    var bgHex = borderElement.Background?.ToString() ?? "";
                    if (bgHex.Contains("3C3C3C") || bgHex.Contains("3A3A3A") || bgHex.Contains("2F3A3A3A") || bgHex.Contains("3F3C3C3C"))
                    {
                        borderElement.Background = new SolidColorBrush(scheme.SurfaceContainerHigh);
                        borderElement.BorderBrush = new SolidColorBrush(scheme.OutlineVariant);
                        borderElement.BorderThickness = new Thickness(1);
                        borderElement.CornerRadius = new CornerRadius(16);
                    }
                    else if (bgHex.Contains("1EFFFFFF") || bgHex.Contains("2AFFFFFF")) // topbar status pills
                    {
                        borderElement.Background = new SolidColorBrush(scheme.SurfaceContainerHigh);
                        borderElement.BorderBrush = new SolidColorBrush(scheme.OutlineVariant);
                        borderElement.BorderThickness = new Thickness(1);
                        borderElement.CornerRadius = new CornerRadius(12);
                    }
                }
            }

            // ── Label: M3 text colors ──
            if (element is Label label)
            {
                if (label.FontWeight == FontWeights.Bold)
                    label.Foreground = new SolidColorBrush(scheme.OnSurface);
                else
                    label.Foreground = new SolidColorBrush(scheme.OnSurfaceVariant);
            }

            // ── TextBlock: M3 text hierarchy ──
            if (element is TextBlock tb)
            {
                if (tb.FontWeight == FontWeights.SemiBold || tb.FontWeight == FontWeights.Bold)
                    tb.Foreground = new SolidColorBrush(scheme.OnSurface);
                else
                    tb.Foreground = new SolidColorBrush(scheme.OnSurfaceVariant);
            }

            // ── Recurse into children ──
            int count = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < count; i++)
            {
                WalkAndApplyM3(VisualTreeHelper.GetChild(element, i), scheme, depth + 1);
            }
        }

        #endregion

        #region UI Helper Methods

        public void UpdateToggleUI(AToggle toggle, bool isEnabled)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (isEnabled)
                    toggle.EnableSwitch();
                else
                    toggle.DisableSwitch();
            });
        }

        public ComboBoxItem AddDropdownItem(ADropdown dropdown, string title)
        {
            var dropdownitem = new ComboBoxItem
            {
                Content = title,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0)),
                FontFamily = TryFindResource("Atkinson Hyperlegible") as FontFamily
            };

            dropdownitem.Selected += (s, e) =>
            {
                var key = dropdown.DropdownTitle.Content?.ToString()
                        ?? throw new NullReferenceException("dropdown.DropdownTitle.Content.ToString() is null");
                Dictionary.dropdownState[key] = title;
            };

            dropdown.DropdownBox.Items.Add(dropdownitem);
            return dropdownitem;
        }

        /// <summary>
        /// Apply a font across the entire application by updating the Window and resource dictionary.
        /// Controls that inherit font or reference a dynamic resource will pick up the change.
        /// </summary>
        public void ApplyFontToUI(string fontName)
        {
            try
            {
                FontFamily font;
                if (fontName == "Atkinson Hyperlegible")
                {
                    font = new FontFamily("pack://application:,,,/Graphics/Fonts/#Atkinson Hyperlegible");
                }
                else
                {
                    font = new FontFamily(fontName);
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Update the resource so new controls pick up the new font
                    Application.Current.Resources["Atkinson Hyperlegible"] = font;
                    Application.Current.Resources["MaterialDesignFont"] = font;

                    this.FontFamily = font;
                    foreach (Window window in Application.Current.Windows)
                    {
                        window.FontFamily = font;
                    }

                    // Force layout refresh on visual tree so inherited changes apply
                    this.UpdateLayout();
                });
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Warning, $"Failed to apply font: {ex.Message}", false);
            }
        }

        #endregion

        #region Keybind Handling

        private void ListenForKeybinds()
        {
            bindingManager.OnBindingPressed += HandleKeybindPressed;
            bindingManager.OnBindingReleased += HandleKeybindReleased;
        }

        private void HandleKeybindPressed(string bindingId)
        {
            var handlers = new Dictionary<string, Action>
            {
                ["Model Switch Keybind"] = HandleModelSwitch,
                ["Dynamic FOV Keybind"] = () => ApplyDynamicFOV(true),
                ["Emergency Stop Keybind"] = HandleEmergencyStop,
                ["Anti Recoil Keybind"] = () => HandleAntiRecoil(true),
                ["Disable Anti Recoil Keybind"] = DisableAntiRecoil
            };

            handlers.GetValueOrDefault(bindingId)?.Invoke();
        }

        private void HandleKeybindReleased(string bindingId)
        {
            var handlers = new Dictionary<string, Action>
            {
                ["Dynamic FOV Keybind"] = () => ApplyDynamicFOV(false),
                ["Anti Recoil Keybind"] = () => HandleAntiRecoil(false)
            };

            handlers.GetValueOrDefault(bindingId)?.Invoke();
        }

        private void HandleModelSwitch()
        {
            if (!Dictionary.toggleState["Enable Model Switch Keybind"] || FileManager.CurrentlyLoadingModel)
                return;

            if (_menuControls["ModelMenu"] is ModelMenuControl modelMenu)
            {
                var modelListBox = modelMenu.ModelListBoxControl;
                modelListBox.SelectedIndex = (modelListBox.SelectedIndex >= 0 &&
                    modelListBox.SelectedIndex < modelListBox.Items.Count - 1)
                    ? modelListBox.SelectedIndex + 1
                    : 0;
            }
        }

        private void ApplyDynamicFOV(bool apply)
        {
            if (!Dictionary.toggleState["Dynamic FOV"])
            {
                FOVWindow.Circle.BeginAnimation(FrameworkElement.WidthProperty, null);
                FOVWindow.Circle.BeginAnimation(FrameworkElement.HeightProperty, null);
                FOVWindow.RectangleShape.BeginAnimation(FrameworkElement.WidthProperty, null);
                FOVWindow.RectangleShape.BeginAnimation(FrameworkElement.HeightProperty, null);

                FOVWindow.UpdateFOVSize(ActualFOV);
                return;
            }
            var targetSize = apply ? Convert.ToDouble(Dictionary.sliderSettings["Dynamic FOV Size"]) : ActualFOV;
            Dictionary.sliderSettings["FOV Size"] = targetSize;
            AnimateFOVSize(targetSize);
        }
        /* Old
        private void ApplyDynamicFOV(bool apply)
        {
            if (!Dictionary.toggleState["Dynamic FOV"]) return;

            var targetSize = apply ? Convert.ToDouble(Dictionary.sliderSettings["Dynamic FOV Size"]) : ActualFOV;
            Dictionary.sliderSettings["FOV Size"] = targetSize;

            AnimateFOVSize(targetSize);
        }
        */
        private void AnimateFOVSize(double targetSize)
        {
            var duration = TimeSpan.FromMilliseconds(500);
            Animator.WidthShift(duration, FOVWindow.Circle, FOVWindow.Circle.ActualWidth, targetSize);
            Animator.HeightShift(duration, FOVWindow.Circle, FOVWindow.Circle.ActualHeight, targetSize);
            Animator.WidthShift(duration, FOVWindow.RectangleShape, FOVWindow.RectangleShape.ActualWidth, targetSize);
            Animator.HeightShift(duration, FOVWindow.RectangleShape, FOVWindow.RectangleShape.ActualHeight, targetSize);
        }
        /* Old
        private void AnimateFOVSize(double targetSize)
        {
            var duration = TimeSpan.FromMilliseconds(500);
            Animator.WidthShift(duration, FOVWindow.Circle, FOVWindow.Circle.ActualWidth, targetSize);
            Animator.HeightShift(duration, FOVWindow.Circle, FOVWindow.Circle.ActualHeight, targetSize);
        }
        */
        private void HandleEmergencyStop()
        {
            var features = new[] { "Aim Assist", "Constant AI Tracking", "Auto Trigger" };
            var toggles = new[] { uiManager.T_AimAligner, uiManager.T_ConstantAITracking, uiManager.T_AutoTrigger };

            for (int i = 0; i < features.Length; i++)
            {
                Dictionary.toggleState[features[i]] = false;
                if (toggles[i] != null)
                    UpdateToggleUI(toggles[i], false);
            }
            LogManager.Log(LogManager.LogLevel.Info, "[Emergency Stop Keybind] Disabled all AI features.", true);
        }

        #endregion

        #region UI Effects

        private void Main_Background_Gradient(object sender, MouseEventArgs e)
        {
            if (!Dictionary.toggleState["Mouse Background Effect"]) return;

            var mousePosition = WinAPICaller.GetCursorPosition();
            var translatedMousePos = PointFromScreen(new Point(mousePosition.X, mousePosition.Y));

            var targetAngle = Math.Atan2(
                translatedMousePos.Y - (MainBorder.ActualHeight * 0.5),
                translatedMousePos.X - (MainBorder.ActualWidth * 0.5)) * (180 / Math.PI);

            _currentGradientAngle = CalculateSmoothedAngle(targetAngle);
            RotaryGradient.Angle = _currentGradientAngle;
        }

        private double CalculateSmoothedAngle(double targetAngle)
        {
            const double fullCircle = 360;
            const double halfCircle = 180;
            const double clamp = 1;

            var angleDifference = (targetAngle - _currentGradientAngle + fullCircle) % fullCircle;
            if (angleDifference > halfCircle)
                angleDifference -= fullCircle;

            var clampedDifference = Math.Max(Math.Min(angleDifference, clamp), -clamp);
            return (_currentGradientAngle + clampedDifference + fullCircle) % fullCircle;
        }

        #endregion

        #region Configuration Management

        private void LoadDropdownStates()
        {

            var dropdownConfigs = new[]
            {
                // AimMenu dropdowns
                (uiManager.D_PredictionMethod, "Prediction Method", new Dictionary<string, int>
                {
                    ["Kalman Filter"] = 0,
                    ["Shall0e's Prediction"] = 1,
                    ["wisethef0x's EMA Prediction"] = 2
                }),
                (uiManager.D_DetectionAreaType, "Detection Area Type", new Dictionary<string, int>
                {
                    ["Closest to Center Screen"] = 0,
                    ["Closest to Mouse"] = 1,
                    ["Highest Confidence"] = 2
                }),
                (uiManager.D_AimingBoundariesAlignment, "Aiming Boundaries Alignment", new Dictionary<string, int>
                {
                    ["Center"] = 0,
                    ["Top"] = 1,
                    ["Bottom"] = 2
                }),
                // SettingsMenu dropdowns
                (uiManager.D_MouseMovementMethod, "Mouse Movement Method", new Dictionary<string, int>
                {
                    ["Mouse Event"] = 0,
                    ["SendInput"] = 1,
                    ["LG HUB"] = 2,
                    ["Razer Synapse (Require Razer Peripheral)"] = 3,
                    ["ddxoft Virtual Input Driver"] = 4,
                    ["XInput (Controller Input)"] = 5,
                    ["XInput (Normal)"] = 6,
                    ["DirectInput (PS4/PS5 Controller)"] = 7,
                    ["Virtual Controller (ViGEm)"] = 8
                }),
                (uiManager.D_ScreenCaptureMethod, "Screen Capture Method", new Dictionary<string, int>
                {
                    ["DirectX"] = 0,
                    ["GDI+"] = 1
                }),
                (uiManager.D_ImageSize, "Image Size", new Dictionary<string, int>
                {
                    ["640"] = 0,
                    ["512"] = 1,
                    ["416"] = 2,
                    ["320"] = 3,
                    ["256"] = 4,
                    ["160"] = 5
                }),
            };

            foreach (var (dropdown, key, mappings) in dropdownConfigs)
            {
                if (dropdown == null)
                {
                    continue;
                }

                if (Dictionary.dropdownState.TryGetValue(key, out var value))
                {
                    var stringValue = value?.ToString() ?? "";

                    if (mappings.TryGetValue(stringValue, out int index))
                    {
                        dropdown.DropdownBox.SelectedIndex = index;
                    }
                    else
                    {
                        LogManager.Log(LogManager.LogLevel.Warning, $"No mapping found for '{stringValue}' in '{key}' dropdown.");
                    }
                }
            }

            // Update slider visibility based on loaded states
            UpdatePredictionSliderVisibility();
            UpdateAimAssistSliderVisibility();
            UpdateAimConfigSliderVisibility();
        }

        private void LoadConfig(string path = "bin\\configs\\Default.cfg", bool loading_from_configlist = false)
        {
            SaveDictionary.LoadJSON(Dictionary.sliderSettings, path);
            SaveDictionary.LoadJSON(Dictionary.dropdownState, path);

            try
            {
                if (File.Exists(path))
                {
                    string text = File.ReadAllText(path);
                    if (!text.Contains("AR Hold Time") && Dictionary.toggleState.ContainsKey("Anti-Recoil") && Dictionary.toggleState["Anti-Recoil"])
                    {
                        Dictionary.toggleState["Anti-Recoil"] = false;
                        if (uiManager.T_AntiRecoil != null)
                        {
                            Dispatcher.Invoke(() => UpdateToggleUI(uiManager.T_AntiRecoil, false));
                        }
                    }
                }
            }
            catch { }

            if (!loading_from_configlist || _menuControls["AimMenu"] == null || !_menuInitialized["AimMenu"])
                return;

            try
            {
                ShowSuggestedModelIfSpecified();
                ApplyConfigToSliders();
                ApplyConfigToDropdowns();
            }
            catch (Exception e)
            {
                MessageBox.Show($"Error loading config, possibly outdated\n{e}");
            }
        }

        private void ShowSuggestedModelIfSpecified()
        {
            if (Dictionary.sliderSettings.TryGetValue("Suggested Model", out var model))
            {
                var suggestedModel = model?.ToString() ?? "N/A";
                if (suggestedModel != "N/A" && !string.IsNullOrEmpty(suggestedModel))
                {
                    MessageBox.Show(
                        $"The creator of this model suggests you use this model:\n{suggestedModel}",
                        "Suggested Model - Aimmy");
                }
            }
        }

        private void ApplyConfigToSliders()
        {
            var sliderConfigs = new[]
            {
                ("FOV Size", uiManager.S_FOVSize, 640.0),
                ("Mouse Sensitivity (+/-)", uiManager.S_MouseSensitivity, 0.8),
                ("Mouse Jitter", uiManager.S_MouseJitter, 0.0),
                ("Sticky Aim Threshold", uiManager.S_StickyAimThreshold, 50),
                ("EMA Smoothening", uiManager.S_EMASmoothing, 0.5),
                ("Y Offset (Up/Down)", uiManager.S_YOffset, 0.0),
                ("X Offset (Left/Right)", uiManager.S_XOffset, 0.0),
                ("Y Offset (%)", uiManager.S_YOffsetPercent, 0.0),
                ("X Offset (%)", uiManager.S_XOffsetPercent, 0.0),
                ("Auto Trigger Delay", uiManager.S_AutoTriggerDelay, 0.25),
                ("AI Minimum Confidence", uiManager.S_AIMinimumConfidence, 50.0),
                ("Kalman Lead Time", uiManager.S_KalmanLeadTime, 0.10),
                ("WiseTheFox Lead Time", uiManager.S_WiseTheFoxLeadTime, 0.15),
                ("Shalloe Lead Multiplier", uiManager.S_ShalloeLeadMultiplier, 3.0),
                ("AR Hold Time", uiManager.S_ARHoldTime, 0.0),
                ("AR Fire Rate", uiManager.S_ARFireRate, 100.0),
                ("AR Y Recoil", uiManager.S_YAntiRecoilAdjustment, 0.0),
                ("AR X Recoil", uiManager.S_XAntiRecoilAdjustment, 0.0)
            };

            ApplySliderValues(sliderConfigs, Dictionary.sliderSettings);
        }


        private void ApplyConfigToDropdowns()
        {
            var dropdownConfigs = new[]
            {

                ("Prediction Method", uiManager.D_PredictionMethod, new Dictionary<string, int>
                {
                    ["Kalman Filter"] = 0,
                    ["Shall0e's Prediction"] = 1,
                    ["wisethef0x's EMA Prediction"] = 2
                }),

                ("Detection Area Type", uiManager.D_DetectionAreaType, new Dictionary<string, int>
                {
                    ["Closest to Center Screen"] = 0,
                    ["Closest to Mouse"] = 1,
                    ["Highest Confidence"] = 2
                }),

                ("Aiming Boundaries Alignment", uiManager.D_AimingBoundariesAlignment, new Dictionary<string, int>
                {
                    ["Center"] = 0,
                    ["Top"] = 1,
                    ["Bottom"] = 2
                }),

                ("Mouse Movement Method", uiManager.D_MouseMovementMethod, new Dictionary<string, int>
                {
                    ["Mouse Event"] = 0,
                    ["SendInput"] = 1,
                    ["LG HUB"] = 2,
                    ["Razer Synapse (Require Razer Peripheral)"] = 3,
                    ["ddxoft Virtual Input Driver"] = 4,
                    ["Virtual Controller (ViGEm)"] = 5
                }),

                ("Movement Path", uiManager.D_MovementPath, new Dictionary<string, int>
                {
                    ["Cubic Bezier"] = 0,
                    ["Exponential"] = 1,
                    ["Linear"] = 2,
                    ["Adaptive"] = 3,
                    ["Perlin Noise"] = 4
                }),

                ("Tracer Position", uiManager.D_TracerPosition, new Dictionary<string, int>
                {
                    ["Bottom"] = 0,
                    ["Middle"] = 1,
                    ["Top"] = 2,
                }),

                ("Target Class", uiManager.D_TargetClass, new Dictionary<string, int>
                {
                    ["Best Confidence"] = 0,
                })
            };

            ApplyDropdownValues(dropdownConfigs, Dictionary.dropdownState);

            // Update prediction slider visibility based on selected method
            UpdatePredictionSliderVisibility();
        }

        public void UpdatePredictionSliderVisibility()
        {
            // Hide all prediction sliders first
            if (uiManager.S_KalmanLeadTime != null)
                uiManager.S_KalmanLeadTime.Visibility = Visibility.Collapsed;
            if (uiManager.S_WiseTheFoxLeadTime != null)
                uiManager.S_WiseTheFoxLeadTime.Visibility = Visibility.Collapsed;
            if (uiManager.S_ShalloeLeadMultiplier != null)
                uiManager.S_ShalloeLeadMultiplier.Visibility = Visibility.Collapsed;

            // Don't show sliders if Predictions section is collapsed
            if (Dictionary.minimizeState.TryGetValue("Predictions", out var collapsed) && collapsed == true)
                return;

            // Get selected method from actual dropdown selection
            var selectedItem = uiManager.D_PredictionMethod?.DropdownBox?.SelectedItem as ComboBoxItem;
            string selectedMethod = selectedItem?.Content?.ToString() ?? "";

            // Show only the relevant slider based on selected method
            switch (selectedMethod)
            {
                case "Kalman Filter":
                    if (uiManager.S_KalmanLeadTime != null)
                        uiManager.S_KalmanLeadTime.Visibility = Visibility.Visible;
                    break;
                case "Shall0e's Prediction":
                    if (uiManager.S_ShalloeLeadMultiplier != null)
                        uiManager.S_ShalloeLeadMultiplier.Visibility = Visibility.Visible;
                    break;
                case "wisethef0x's EMA Prediction":
                    if (uiManager.S_WiseTheFoxLeadTime != null)
                        uiManager.S_WiseTheFoxLeadTime.Visibility = Visibility.Visible;
                    break;
            }
        }

        public void UpdateAimAssistSliderVisibility()
        {
            // Don't show sliders if Aim Assist section is collapsed
            if (Dictionary.minimizeState.TryGetValue("Aim Assist", out var collapsed) && collapsed == true)
            {
                if (uiManager.S_StickyAimThreshold != null)
                    uiManager.S_StickyAimThreshold.Visibility = Visibility.Collapsed;
                return;
            }

            // Show Sticky Aim Threshold only if Sticky Aim is enabled
            if (uiManager.S_StickyAimThreshold != null)
            {
                uiManager.S_StickyAimThreshold.Visibility = Dictionary.toggleState["Sticky Aim"]
                    ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public void UpdateAimConfigSliderVisibility()
        {
            // Don't show sliders if Aim Config section is collapsed
            if (Dictionary.minimizeState.TryGetValue("Aim Config", out var collapsed) && collapsed == true)
            {
                if (uiManager.S_YOffset != null) uiManager.S_YOffset.Visibility = Visibility.Collapsed;
                if (uiManager.S_YOffsetPercent != null) uiManager.S_YOffsetPercent.Visibility = Visibility.Collapsed;
                if (uiManager.S_XOffset != null) uiManager.S_XOffset.Visibility = Visibility.Collapsed;
                if (uiManager.S_XOffsetPercent != null) uiManager.S_XOffsetPercent.Visibility = Visibility.Collapsed;
                return;
            }

            // Y Axis: Show pixel offset when toggle is OFF, percentage offset when toggle is ON
            bool yPercentEnabled = Dictionary.toggleState["Y Axis Percentage Adjustment"];
            if (uiManager.S_YOffset != null)
                uiManager.S_YOffset.Visibility = yPercentEnabled ? Visibility.Collapsed : Visibility.Visible;
            if (uiManager.S_YOffsetPercent != null)
                uiManager.S_YOffsetPercent.Visibility = yPercentEnabled ? Visibility.Visible : Visibility.Collapsed;

            // X Axis: Show pixel offset when toggle is OFF, percentage offset when toggle is ON
            bool xPercentEnabled = Dictionary.toggleState["X Axis Percentage Adjustment"];
            if (uiManager.S_XOffset != null)
                uiManager.S_XOffset.Visibility = xPercentEnabled ? Visibility.Collapsed : Visibility.Visible;
            if (uiManager.S_XOffsetPercent != null)
                uiManager.S_XOffsetPercent.Visibility = xPercentEnabled ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ApplySliderValues((string key, ASlider? slider, double defaultValue)[] configs, Dictionary<string, dynamic> source)
        {
            foreach (var (key, slider, defaultValue) in configs)
            {
                if (slider != null && source.TryGetValue(key, out var value))
                {
                    slider.Slider.Value = Convert.ToDouble(value);
                }
                else if (slider != null)
                {
                    slider.Slider.Value = defaultValue;
                }
            }
        }

        private void ApplyDropdownValues((string key, ADropdown? dropdown, Dictionary<string, int> mappings)[] configs, Dictionary<string, dynamic> source)
        {
            foreach (var (key, dropdown, mappings) in configs)
            {
                if (dropdown != null && source.TryGetValue(key, out var value))
                {
                    var stringValue = value?.ToString() ?? "";
                    if (mappings.TryGetValue(stringValue, out int index))
                    {
                        dropdown.DropdownBox.SelectedIndex = index;
                    }
                    else
                    {
                        LogManager.Log(LogManager.LogLevel.Warning, $"No mapping found for '{stringValue}' in '{key}' dropdown.");
                    }
                }
            }
        }

        #endregion

        #region System Information

        private static string? GetProcessorName() => GetSpecs.GetSpecification("Win32_Processor", "Name");
        private static string? GetVideoControllerName() => GetSpecs.GetSpecification("Win32_VideoController", "Name");
        private static string? GetFormattedMemorySize()
        {
            var totalMemorySize = long.Parse(GetSpecs.GetSpecification("CIM_OperatingSystem", "TotalVisibleMemorySize")!);
            return Math.Round(totalMemorySize / (1024.0 * 1024.0), 0).ToString();
        }

        #endregion

        #region Unimplemented Methods (For Controls)

        public AToggle AddToggle(StackPanel panel, string title) =>
            throw new NotImplementedException("Use control's internal implementation");

        public AKeyChanger AddKeyChanger(StackPanel panel, string title, string keybind) =>
            throw new NotImplementedException("Use control's internal implementation");

        public AColorChanger AddColorChanger(StackPanel panel, string title) =>
            throw new NotImplementedException("Use control's internal implementation");

        public ASlider AddSlider(StackPanel panel, string title, string label, double frequency, double buttonsteps, double min, double max) =>
            throw new NotImplementedException("Use control's internal implementation");

        public ADropdown AddDropdown(StackPanel panel, string title) =>
            throw new NotImplementedException("Use control's internal implementation");

        public AFileLocator AddFileLocator(StackPanel panel, string title, string filter = "All files (*.*)|*.*", string DLExtension = "") =>
            throw new NotImplementedException("Use control's internal implementation");

        public void ShowDirectInputWarning()
        {
            Dispatcher.Invoke(() =>
            {
                GTAWarningOverlay.Visibility = Visibility.Visible;
            });
        }

        private void DismissGTAWarning_Click(object sender, RoutedEventArgs e)
        {
            GTAWarningOverlay.Visibility = Visibility.Collapsed;
        }

        #endregion
    }

    #region Extension Methods

    internal static class DictionaryExtensions
    {
        public static T GetValueOrDefault<T>(this Dictionary<string, T> dictionary, string key, T defaultValue) =>
            dictionary.TryGetValue(key, out var value) ? value : defaultValue;

        public static T GetValueOrDefault<T>(this Dictionary<string, dynamic> dictionary, string key, T defaultValue)
        {
            if (dictionary.TryGetValue(key, out var value))
            {
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }
    }

    #endregion
}