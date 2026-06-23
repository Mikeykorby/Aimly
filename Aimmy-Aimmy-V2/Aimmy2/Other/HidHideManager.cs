using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Windows;
using Aimmy2.Class;

namespace Aimmy2.Other
{
    /// <summary>
    /// Manages HidHide integration for hiding real controllers devices
    /// from games while allowing Aimmy to see them.
    /// </summary>
    public static class HidHideManager
    {
        private static readonly string HidHideCliPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HidHideCLI.exe");
        private static readonly string HidHideDownloadUrl = "https://github.com/nefarius/HidHide/releases/latest";

        // Known default installation paths for HidHide
        private static readonly string[] KnownHidHidePaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Nefarius Software Solutions", "HidHide", "x64", "HidHideCLI.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Nefarius", "HidHide", "x64", "HidHideCLI.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Nefarius", "HidHide", "Client", "HidHideCLI.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Nefarius", "HidHide", "HidHideCLI.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Nefarius Software Solutions", "HidHide", "x64", "HidHideCLI.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Nefarius", "HidHide", "x86", "HidHideCLI.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Nefarius", "HidHide", "HidHideCLI.exe"),
            @"C:\Program Files\Nefarius Software Solutions\HidHide\x64\HidHideCLI.exe",
            @"C:\Program Files\Nefarius\HidHide\x64\HidHideCLI.exe",
            @"C:\Program Files\Nefarius\HidHide\Client\HidHideCLI.exe",
            @"C:\Program Files\Nefarius\HidHide\HidHideCLI.exe"
        };

        /// <summary>
        /// Checks if HidHideCLI is available in the application directory, known install paths, or system PATH
        /// </summary>
        public static bool IsAvailable => GetCliPath() != null;

        /// <summary>
        /// Gets the path to HidHideCLI.exe, or null if not found
        /// </summary>
        private static string? GetCliPath()
        {
            // 1) Check app directory first
            if (File.Exists(HidHideCliPath)) return HidHideCliPath;

            // 2) Check known default installation paths
            foreach (var path in KnownHidHidePaths)
            {
                if (File.Exists(path)) return path;
            }

            // 3) Fallback to system PATH
            return FindInPath("HidHideCLI.exe");
        }

        private static string? FindInPath(string fileName)
        {
            var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator);
            if (paths == null) return null;

            foreach (var path in paths)
            {
                var fullPath = Path.Combine(path, fileName);
                if (File.Exists(fullPath)) return fullPath;
            }
            return null;
        }

        /// <summary>
        /// Runs a HidHideCLI command
        /// </summary>
        private static bool RunCommand(string arguments, out string output, out string error, bool needOutput = true)
        {
            output = string.Empty;
            error = string.Empty;

            var cliPath = GetCliPath();
            if (cliPath == null) return false;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = cliPath,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };

                using var process = new Process { StartInfo = psi };
                
                global::Other.LogManager.Log(global::Other.LogManager.LogLevel.Info, $"Running: HidHideCLI {arguments}", false);
                
                process.Start();

                var outTask = process.StandardOutput.ReadToEndAsync();
                var errTask = process.StandardError.ReadToEndAsync();
                
                bool exited = process.WaitForExit(5000);
                
                try { output = outTask.Result; } catch { }
                try { error = errTask.Result; } catch { }

                if (!string.IsNullOrEmpty(output))
                {
                    global::Other.LogManager.Log(global::Other.LogManager.LogLevel.Info, $"HidHideCLI Output: {output}", false);
                }
                if (!string.IsNullOrEmpty(error))
                {
                    global::Other.LogManager.Log(global::Other.LogManager.LogLevel.Error, $"HidHideCLI Error: {error}", false);
                }

                return exited && process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                global::Other.LogManager.Log(global::Other.LogManager.LogLevel.Error, $"HidHideCLI Exception: {ex.Message}", false);
                return false;
            }
        }

        public static bool IsAdmin()
        {
            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Registers Aimly2.exe in HidHide's application whitelist
        /// so Aimmy can still see hidden controllers.
        /// </summary>
        public static bool RegisterApplication()
        {
            if (!IsAdmin()) return false;
            var appPath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(appPath)) return false;

            return RunCommand($"--app-reg \"{appPath}\"", out _, out _, false);
        }

        /// <summary>
        /// Unregisters Aimly2.exe from HidHide's whitelist
        /// </summary>
        public static bool UnregisterApplication()
        {
            if (!IsAdmin()) return false;
            var appPath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(appPath)) return false;

            return RunCommand($"--app-unreg \"{appPath}\"", out _, out _, false);
        }

        /// <summary>
        /// Globally enables device cloaking (hides all gaming controllers from unwhitelisted apps)
        /// </summary>
        public static bool EnableCloaking()
        {
            if (!IsAdmin()) return false;
            // First register ourselves so Aimmy can still see the controller
            RegisterApplication();
            return RunCommand("--cloak-on", out _, out _, false);
        }

        /// <summary>
        /// Globally disables device cloaking
        /// </summary>
        public static bool DisableCloaking()
        {
            if (!IsAdmin()) return false;
            return RunCommand("--cloak-off", out _, out _, false);
        }

        /// <summary>
        /// Hides all physical gaming devices
        /// </summary>
        public static bool HideAllGamingDevices()
        {
            if (!IsAdmin()) return false;
            
            if (!RunCommand("--dev-gaming", out var output, out _)) return false;

            try
            {
                int jsonStart = output.IndexOf('[');
                if (jsonStart >= 0)
                {
                    // HidHideCLI outputs JSON for --dev-gaming
                    var jsonArray = Newtonsoft.Json.Linq.JArray.Parse(output.Substring(jsonStart));
                    foreach (var container in jsonArray)
                    {
                        var devices = container["devices"] as Newtonsoft.Json.Linq.JArray;
                        if (devices != null)
                        {
                            foreach (var device in devices)
                            {
                                var deviceInstancePath = device["deviceInstancePath"]?.ToString();
                                var friendlyName = container["friendlyName"]?.ToString() ?? "";
                                var productName = device["product"]?.ToString() ?? "";
                                var baseContainer = device["baseContainerDeviceInstancePath"]?.ToString() ?? "";

                                if (!string.IsNullOrEmpty(deviceInstancePath))
                                {
                                    // Skip known virtual prefixes
                                    if (friendlyName.IndexOf("Virtual", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        productName.IndexOf("Virtual", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        friendlyName.IndexOf("ViGEm", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        productName.IndexOf("ViGEm", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        deviceInstancePath.IndexOf("ViGEm", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        baseContainer.IndexOf("VIGEMBUS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        baseContainer.IndexOf("ROOT\\SYSTEM", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        continue;
                                    }

                                    var targetName = global::MouseMovementLibraries.DirectInputSupport.DirectInputMain.Instance?.ProductName;
                                    if (!string.IsNullOrEmpty(targetName))
                                    {
                                        if (productName.IndexOf(targetName, StringComparison.OrdinalIgnoreCase) < 0 &&
                                            friendlyName.IndexOf(targetName, StringComparison.OrdinalIgnoreCase) < 0 &&
                                            targetName.IndexOf(productName, StringComparison.OrdinalIgnoreCase) < 0)
                                        {
                                            continue; // Not the target device
                                        }
                                    }

                                    RunCommand($"--dev-hide \"{deviceInstancePath}\"", out _, out _, false);
                                }
                            }
                        }
                    }
                }
                else
                {
                    throw new Exception("No JSON array start found in output.");
                }
            }
            catch (Exception ex)
            {
                global::Other.LogManager.Log(global::Other.LogManager.LogLevel.Info, $"HideAllGamingDevices JSON parse failed: {ex.Message}. Using fallback string splitting.", false);
                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.Contains("Virtual", StringComparison.OrdinalIgnoreCase) || 
                        line.Contains("ViGEm", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("Nefarius", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    RunCommand($"--dev-hide \"{line}\"", out _, out _, false);
                }
            }
            
            return true;
        }

        /// <summary>
        /// Unhides all devices currently in the hidden list
        /// </summary>
        public static bool UnhideAllGamingDevices()
        {
            if (!IsAdmin()) return false;
            
            if (!RunCommand("--dev-list", out var output, out _)) return false;
            
            try
            {
                int jsonStart = output.IndexOf('[');
                if (jsonStart >= 0)
                {
                    var jsonArray = Newtonsoft.Json.Linq.JArray.Parse(output.Substring(jsonStart));
                    foreach (var item in jsonArray)
                    {
                        if (item.Type == Newtonsoft.Json.Linq.JTokenType.String)
                        {
                            RunCommand($"--dev-unhide \"{item.ToString()}\"", out _, out _, false);
                        }
                        else
                        {
                            var path = item["deviceInstancePath"]?.ToString();
                            if (!string.IsNullOrEmpty(path))
                            {
                                RunCommand($"--dev-unhide \"{path}\"", out _, out _, false);
                            }
                        }
                    }
                }
                else
                {
                    throw new Exception("No JSON array start found in output.");
                }
            }
            catch (Exception ex)
            {
                global::Other.LogManager.Log(global::Other.LogManager.LogLevel.Info, $"UnhideAllGamingDevices JSON parse failed: {ex.Message}. Using fallback string splitting.", false);
                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var cleanLine = line.Trim();
                    if (cleanLine.StartsWith("--dev-hide "))
                    {
                        var unhideCmd = cleanLine.Replace("--dev-hide ", "--dev-unhide ");
                        RunCommand(unhideCmd, out _, out _, false);
                    }
                    else if (!cleanLine.StartsWith("[") && !cleanLine.StartsWith("{"))
                    {
                        RunCommand($"--dev-unhide \"{cleanLine}\"", out _, out _, false);
                    }
                }
            }
            
            return true;
        }

        /// <summary>
        /// Checks if cloaking is currently enabled
        /// </summary>
        public static bool IsCloakingEnabled()
        {
            if (!IsAdmin()) return false;
            RunCommand("--cloak-state", out var output, out _);
            return output?.Contains("enabled", StringComparison.OrdinalIgnoreCase) == true;
        }

        /// <summary>
        /// Tries to automatically set up HidHide for the first time.
        /// Returns true if setup succeeded or was already working.
        /// </summary>
        public static bool TrySetup()
        {
            if (IsAvailable)
            {
                if (!IsAdmin())
                {
                    global::Other.LogManager.Log(global::Other.LogManager.LogLevel.Warning, "You must run Aimmy as Administrator to use HidHide cloaking.", true);
                    return false;
                }
                return true;
            }

            // HidHideCLI not found - show download prompt without blocking
            global::Other.LogManager.Log(global::Other.LogManager.LogLevel.Warning, "HidHide is not installed. Opening download page. Please install it and restart Aimmy.", true);
            
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = HidHideDownloadUrl,
                    UseShellExecute = true
                });
            }
            catch { }

            return false;
        }

        /// <summary>
        /// Disables HidHide and cleans up.
        /// </summary>
        public static void DisableAndCleanup()
        {
            if (!IsAdmin()) return;
            if (!IsAvailable) return;

            DisableCloaking();
            UnhideAllGamingDevices();
            UnregisterApplication();
        }
    }
}