using Aimmy2.Theme;
using Other;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for ADownloadGateway.xaml
    /// </summary>
    public partial class ADownloadGateway : UserControl
    {
        private static readonly HttpClient httpClient = new();

        // Store original XAML values for reverting
        private Brush? _origBackground;
        private Brush? _origBorderBrush;
        private Thickness _origBorderThickness;
        private CornerRadius _origCornerRadius;

        private Brush? _origTitleForeground;
        private Brush? _origBtnBackground;
        private Brush? _origBtnForeground;
        private Brush? _origProgressForeground;

        public ADownloadGateway(string Name, string Path)
        {
            InitializeComponent();
            Title.Content = Name;

            DownloadButton.Click += async (s, e) =>
            {
                if ((string)DownloadButton.Content == "\xE895") return;

                DownloadButton.Content = "\xE895";
                SetupHttpClientHeaders();

                var encodedName = Uri.EscapeDataString(Name);
                var downloadUri = new Uri($"https://github.com/BabyHamsta/Aimmy/raw/Aimmy-V2/{Path}/{encodedName}");
                var downloadResult = await DownloadFileAsync(downloadUri, Path, Name);

                if (downloadResult)
                {
                    LogManager.Log(LogManager.LogLevel.Info, $"Downloaded {Name} to bin/{Path}/{Name}", true);
                    RemoveFromParent();
                }
                else
                {
                    DownloadButton.Content = "\xE896";
                }
            };

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
            _origBackground = DownloadGatewayBorder.Background;
            _origBorderBrush = DownloadGatewayBorder.BorderBrush;
            _origBorderThickness = DownloadGatewayBorder.BorderThickness;
            _origCornerRadius = DownloadGatewayBorder.CornerRadius;

            _origTitleForeground = Title.Foreground;
            _origBtnBackground = DownloadButton.Background;
            _origBtnForeground = DownloadButton.Foreground;
            _origProgressForeground = DownloadProgress.Foreground;
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

            // Unregister download button from dynamic theme background override
            ThemeManager.UnregisterElement(DownloadButton);

            // Labels & components
            Title.Foreground = new SolidColorBrush(scheme.OnSurface);

            // Download button
            DownloadButton.Background = new SolidColorBrush(scheme.Primary);
            DownloadButton.Foreground = new SolidColorBrush(scheme.OnPrimary);

            // Progress bar
            DownloadProgress.Foreground = new SolidColorBrush(scheme.Primary);
        }

        private void RevertM3Style()
        {
            // Register download button back to dynamic theme background override
            ThemeManager.RegisterElement(DownloadButton);

            DownloadGatewayBorder.Background = _origBackground;
            DownloadGatewayBorder.BorderBrush = _origBorderBrush;
            DownloadGatewayBorder.BorderThickness = _origBorderThickness;
            DownloadGatewayBorder.CornerRadius = _origCornerRadius;

            Title.Foreground = _origTitleForeground;
            DownloadButton.Background = _origBtnBackground;
            DownloadButton.Foreground = _origBtnForeground;
            DownloadProgress.Foreground = _origProgressForeground;
        }

        private static void SetupHttpClientHeaders()
        {
            if (!httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Aimmy2");
            }
            if (!httpClient.DefaultRequestHeaders.Contains("Accept"))
            {
                httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            }
        }

        private static async Task<bool> DownloadFileAsync(Uri uri, string path, string name)
        {
            var response = await httpClient.GetAsync(uri);

            if (!response.IsSuccessStatusCode)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"Failed to download {name} from {uri}. Status: {response.StatusCode} - {response.ReasonPhrase}", true);
                return false;
            }

            var content = await response.Content.ReadAsByteArrayAsync();
            var filePath = Path.Combine("bin", path, name);

            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            await File.WriteAllBytesAsync(filePath, content);
            return true;
        }

        private void RemoveFromParent()
        {
            if (Parent is StackPanel stackPanel)
            {
                Application.Current.Dispatcher.Invoke(() => stackPanel.Children.Remove(this));
            }
        }
    }
}