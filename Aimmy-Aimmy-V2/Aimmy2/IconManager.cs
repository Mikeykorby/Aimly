using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Aimmy2
{
    /// <summary>
    /// Maps navigation and section icons between Segoe MDL2 Assets (default) and Font Awesome 6 (Beta UI).
    /// </summary>
    public static class IconManager
    {
        // Font family references
        public static readonly FontFamily SegoeMDL2Font = new FontFamily("Segoe MDL2 Assets");
        public static readonly FontFamily FontAwesomeSolid = (FontFamily)Application.Current?.Resources["Font Awesome 6 Solid"] ?? new FontFamily("Segoe MDL2 Assets");
        public static readonly FontFamily FontAwesomeRegular = (FontFamily)Application.Current?.Resources["Font Awesome 6 Regular"] ?? new FontFamily("Segoe MDL2 Assets");

        // NavRail button icon mapping: ButtonName -> (MDL2_Unicode, FA_Solid_Unicode)
        private static readonly Dictionary<string, (string MdlUnicode, string FaUnicode)> NavRailIconMap = new()
        {
            { "Menu1B", ("\uE1D2", "\uf05b") },   // Aim        -> Crosshairs
            { "Menu2B", ("\uE99A", "\uf11b") },   // Controller -> Gamepad
            { "Menu3B", ("\uE8E5", "\uf07c") },   // Models     -> FolderOpen
            { "Menu4B", ("\uE713", "\uf013") },   // Settings   -> Cog
            { "Menu5B", ("\uE77B", "\uf007") },   // About      -> User
        };

        /// <summary>
        /// Apply Font Awesome 6 icons to the navigation rail buttons when Beta UI is enabled.
        /// Revert to Segoe MDL2 when disabled.
        /// </summary>
        public static void UpdateNavRailIcons(bool useFontAwesome)
        {
            if (Application.Current?.MainWindow is not MainWindow mainWindow) return;

            foreach (var (buttonName, (mdl, fa)) in NavRailIconMap)
            {
                var button = mainWindow.FindName(buttonName) as Button;
                if (button == null) continue;

                if (useFontAwesome)
                {
                    button.FontFamily = FontAwesomeSolid;
                    button.Content = fa;
                }
                else
                {
                    button.FontFamily = SegoeMDL2Font;
                    button.Content = mdl;
                }
            }
        }
    }
}
