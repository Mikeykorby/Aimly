using Aimmy2.Class;
using Aimmy2.Theme;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for ADisplaySelector.xaml
    /// </summary>
    public partial class ADisplaySelector : UserControl
    {
        private List<DisplayInfo> _displays = new List<DisplayInfo>();
        private int _selectedDisplayIndex = 0;

        // Store original XAML values for reverting
        private Brush? _origBackground;
        private Brush? _origBorderBrush;
        private Thickness _origBorderThickness;
        private CornerRadius _origCornerRadius;

        public ADisplaySelector()
        {
            InitializeComponent();
            Loaded += ADisplaySelector_Loaded;

            // Subscribe to theme and display changes
            ThemeManager.ThemeChanged += OnThemeChanged;
            DisplayManager.DisplayChanged += OnDisplayManagerChanged;
            ThemeManager.BetaUIStateChanged += OnBetaUIStateChanged;
        }

        private void ADisplaySelector_Loaded(object sender, RoutedEventArgs e)
        {
            CaptureOriginalValues();
            RefreshDisplays();
        }

        private void CaptureOriginalValues()
        {
            _origBackground = DisplaySelectorBorder.Background;
            _origBorderBrush = DisplaySelectorBorder.BorderBrush;
            _origBorderThickness = DisplaySelectorBorder.BorderThickness;
            _origCornerRadius = DisplaySelectorBorder.CornerRadius;
        }

        private void OnThemeChanged(object? sender, Color newThemeColor)
        {
            // Update all display visuals when theme changes
            UpdateUI();
        }

        private void OnBetaUIStateChanged(object? sender, bool isBetaUI)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                ApplyContainerStyle();
                RefreshDisplays(); // Redraw displays to pick up changes in border/monitor visuals
            });
        }

        private void OnDisplayManagerChanged(object? sender, DisplayChangedEventArgs e)
        {
            // Update selection when DisplayManager changes (external change)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (e.DisplayIndex != _selectedDisplayIndex)
                {
                    _selectedDisplayIndex = e.DisplayIndex;
                    RefreshDisplays(); // This will update the UI
                }
            });
        }

        private bool IsBetaUIEnabled => Dictionary.toggleState.TryGetValue("Beta UI", out var val) && val is bool b && b;

         private void ApplyContainerStyle()
        {
            if (IsBetaUIEnabled)
            {
                var scheme = ThemeManager.CurrentScheme ?? ThemeManager.GenerateMaterial3Scheme(ThemeManager.ThemeColor);
                DisplaySelectorTitle.Foreground = new SolidColorBrush(scheme.OnSurface);
                CurrentDisplayInfo.Foreground = new SolidColorBrush(scheme.OnSurfaceVariant);
            }
            else
            {
                DisplaySelectorBorder.Background = _origBackground;
                DisplaySelectorBorder.BorderBrush = _origBorderBrush;
                DisplaySelectorBorder.BorderThickness = _origBorderThickness;
                DisplaySelectorBorder.CornerRadius = _origCornerRadius;

                DisplaySelectorTitle.Foreground = new SolidColorBrush(Color.FromArgb(0xDD, 0xFF, 0xFF, 0xFF));
                CurrentDisplayInfo.Foreground = new SolidColorBrush(Color.FromArgb(0xBB, 0xFF, 0xFF, 0xFF));
            }
        }

        public void RefreshDisplays()
        {
            _displays = DisplayManager.GetAllDisplays();
            _selectedDisplayIndex = DisplayManager.CurrentDisplayIndex;

            DisplayGrid.Children.Clear();
            CreateDisplayVisuals();
            UpdateGridLayout();
            UpdateUI();
        }

        private void CreateDisplayVisuals()
        {
            for (int i = 0; i < _displays.Count; i++)
            {
                var display = _displays[i];
                CreateDisplayVisual(display);
            }
        }

        private void CreateDisplayVisual(DisplayInfo display)
        {
            bool isBeta = IsBetaUIEnabled;
            var scheme = ThemeManager.CurrentScheme ?? ThemeManager.GenerateMaterial3Scheme(ThemeManager.ThemeColor);

            // Create container
            var container = new Border
            {
                Margin = new Thickness(5),
                Cursor = Cursors.Hand,
                Tag = display.Index
            };

            if (isBeta)
            {
                container.Background = new SolidColorBrush(scheme.SurfaceContainer);
                container.BorderBrush = new SolidColorBrush(scheme.OutlineVariant);
                container.BorderThickness = new Thickness(1);
                container.CornerRadius = new CornerRadius(12);
            }
            else
            {
                container.Background = new SolidColorBrush(Color.FromArgb(51, 60, 60, 60));
                container.BorderBrush = new SolidColorBrush(Color.FromArgb(63, 255, 255, 255));
                container.BorderThickness = new Thickness(1);
                container.CornerRadius = new CornerRadius(5);
            }

            // Create inner grid
            var grid = new Grid();

            // Monitor visual
            var monitorBorder = new Border
            {
                Width = 50,
                Height = 35,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 25)
            };

            if (isBeta)
            {
                monitorBorder.Background = new SolidColorBrush(Color.FromArgb(15, scheme.OnSurface.R, scheme.OnSurface.G, scheme.OnSurface.B));
                monitorBorder.BorderBrush = new SolidColorBrush(scheme.Outline);
                monitorBorder.BorderThickness = new Thickness(2);
                monitorBorder.CornerRadius = new CornerRadius(4);
            }
            else
            {
                monitorBorder.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
                monitorBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));
                monitorBorder.BorderThickness = new Thickness(2);
                monitorBorder.CornerRadius = new CornerRadius(3);
            }

            // Add a stand for the monitor
            var stand = new Rectangle
            {
                Width = 20,
                Height = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 35, 0, 0)
            };

            if (isBeta)
            {
                stand.Fill = new SolidColorBrush(scheme.Outline);
            }
            else
            {
                stand.Fill = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));
            }

            // Display number
            var displayNumber = new TextBlock
            {
                Text = (display.Index + 1).ToString(),
                FontFamily = (FontFamily)FindResource("Atkinson Hyperlegible"),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15),
                Tag = "DisplayNumber"
            };

            if (isBeta)
            {
                displayNumber.Foreground = new SolidColorBrush(scheme.OnSurface);
            }
            else
            {
                displayNumber.Foreground = new SolidColorBrush(Color.FromArgb(221, 255, 255, 255));
            }

            // Primary indicator
            if (display.IsPrimary)
            {
                var primaryBadge = new Border
                {
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(4, 2, 4, 2),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 0, 5),
                    Tag = "PrimaryBadge"
                };

                if (isBeta)
                {
                    primaryBadge.Background = new SolidColorBrush(scheme.Primary);
                }
                else
                {
                    primaryBadge.Background = new SolidColorBrush(ThemeManager.ThemeColor);
                }

                var primaryText = new TextBlock
                {
                    Text = "Primary",
                    FontFamily = (FontFamily)FindResource("Atkinson Hyperlegible"),
                    FontSize = 9
                };

                if (isBeta)
                {
                    primaryText.Foreground = new SolidColorBrush(scheme.OnPrimary);
                }
                else
                {
                    primaryText.Foreground = Brushes.White;
                }

                primaryBadge.Child = primaryText;
                grid.Children.Add(primaryBadge);
            }

            // Add elements to grid
            grid.Children.Add(monitorBorder);
            grid.Children.Add(stand);
            grid.Children.Add(displayNumber);

            container.Child = grid;

            // Event handlers
            container.MouseEnter += DisplayVisual_MouseEnter;
            container.MouseLeave += DisplayVisual_MouseLeave;
            container.MouseLeftButtonDown += DisplayVisual_MouseLeftButtonDown;

            DisplayGrid.Children.Add(container);
        }

        private void UpdateGridLayout()
        {
            switch (_displays.Count)
            {
                case 0:
                case 1:
                    DisplayGrid.Rows = 1;
                    DisplayGrid.Columns = 1;
                    break;
                case 2:
                    DisplayGrid.Rows = 1;
                    DisplayGrid.Columns = 2;
                    break;
                case 3:
                case 4:
                    DisplayGrid.Rows = 2;
                    DisplayGrid.Columns = 2;
                    break;
                default:
                    DisplayGrid.Rows = 2;
                    DisplayGrid.Columns = Math.Min(4, (_displays.Count + 1) / 2);
                    break;
            }
        }

        private void DisplayVisual_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Border border && (int)border.Tag != _selectedDisplayIndex)
            {
                if (IsBetaUIEnabled)
                {
                    var scheme = ThemeManager.CurrentScheme ?? ThemeManager.GenerateMaterial3Scheme(ThemeManager.ThemeColor);
                    border.Background = new SolidColorBrush(scheme.SurfaceContainerHighest);
                }
                else
                {
                    border.Background = new SolidColorBrush(Color.FromArgb(77, 60, 60, 60));
                }
            }
        }

        private void DisplayVisual_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Border border && (int)border.Tag != _selectedDisplayIndex)
            {
                if (IsBetaUIEnabled)
                {
                    var scheme = ThemeManager.CurrentScheme ?? ThemeManager.GenerateMaterial3Scheme(ThemeManager.ThemeColor);
                    border.Background = new SolidColorBrush(scheme.SurfaceContainer);
                }
                else
                {
                    border.Background = new SolidColorBrush(Color.FromArgb(51, 60, 60, 60));
                }
            }
        }

        private void DisplayVisual_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border)
            {
                int newIndex = (int)border.Tag;
                SelectDisplay(newIndex);
            }
        }

        private void SelectDisplay(int index)
        {
            if (index == _selectedDisplayIndex || index >= _displays.Count) return;

            if (DisplayManager.SetDisplay(index))
            {
                _selectedDisplayIndex = index;
                UpdateUI();
            }
        }

        private void UpdateUI()
        {
            ApplyContainerStyle();

            if (_displays.Count == 0)
            {
                CurrentDisplayInfo.Content = "No displays detected";
                return;
            }

            bool isBeta = IsBetaUIEnabled;
            var scheme = ThemeManager.CurrentScheme ?? ThemeManager.GenerateMaterial3Scheme(ThemeManager.ThemeColor);

            // Update visual states
            for (int i = 0; i < DisplayGrid.Children.Count; i++)
            {
                if (DisplayGrid.Children[i] is Border border)
                {
                    bool isSelected = (int)border.Tag == _selectedDisplayIndex;

                    if (isSelected)
                    {
                        if (isBeta)
                        {
                            border.Background = new SolidColorBrush(scheme.PrimaryContainer);
                            border.BorderBrush = new SolidColorBrush(scheme.Primary);
                            border.BorderThickness = new Thickness(2);
                        }
                        else
                        {
                            border.Background = new SolidColorBrush(ThemeManager.ThemeColor);
                            border.BorderBrush = new SolidColorBrush(Colors.White);
                            border.BorderThickness = new Thickness(2);
                        }
                    }
                    else
                    {
                        if (isBeta)
                        {
                            border.Background = new SolidColorBrush(scheme.SurfaceContainer);
                            border.BorderBrush = new SolidColorBrush(scheme.OutlineVariant);
                            border.BorderThickness = new Thickness(1);
                        }
                        else
                        {
                            border.Background = new SolidColorBrush(Color.FromArgb(51, 60, 60, 60));
                            border.BorderBrush = new SolidColorBrush(Color.FromArgb(63, 255, 255, 255));
                            border.BorderThickness = new Thickness(1);
                        }
                    }

                    // Update child elements
                    if (border.Child is Grid grid)
                    {
                        foreach (var child in grid.Children)
                        {
                            if (child is Border badge && badge.Tag as string == "PrimaryBadge")
                            {
                                badge.Background = new SolidColorBrush(isBeta ? scheme.Primary : ThemeManager.ThemeColor);
                                if (badge.Child is TextBlock badgeText)
                                {
                                    badgeText.Foreground = new SolidColorBrush(isBeta ? scheme.OnPrimary : Colors.White);
                                }
                            }
                            else if (child is TextBlock numText && numText.Tag as string == "DisplayNumber")
                            {
                                if (isBeta)
                                {
                                    numText.Foreground = new SolidColorBrush(isSelected ? scheme.OnPrimaryContainer : scheme.OnSurface);
                                }
                                else
                                {
                                    numText.Foreground = new SolidColorBrush(Color.FromArgb(221, 255, 255, 255));
                                }
                            }
                        }
                    }
                }
            }

            // Update info label
            if (_selectedDisplayIndex < _displays.Count)
            {
                var display = _displays[_selectedDisplayIndex];
                string info = $"Display {display.Index + 1} Selected";
                if (display.IsPrimary) info += " (Primary)";
                info += $" - {display.Bounds.Width}x{display.Bounds.Height}";
                CurrentDisplayInfo.Content = info;
            }
        }

        public int GetSelectedDisplayIndex() => _selectedDisplayIndex;

        public DisplayInfo? GetSelectedDisplay() => _selectedDisplayIndex < _displays.Count ? _displays[_selectedDisplayIndex] : null;

        public Rect GetSelectedDisplayBounds() => GetSelectedDisplay()?.Bounds ?? new Rect();

        public void Dispose()
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
            DisplayManager.DisplayChanged -= OnDisplayManagerChanged;
            ThemeManager.BetaUIStateChanged -= OnBetaUIStateChanged;
        }
    }
}