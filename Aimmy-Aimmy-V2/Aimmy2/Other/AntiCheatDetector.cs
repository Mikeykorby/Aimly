using System.Diagnostics;
using System.Management;
using Other;

namespace Aimmy2.Other
{
    /// <summary>
    /// Detects running anti-cheat services and processes.
    /// Helps users understand which anti-cheats are active while using Aimmy.
    /// </summary>
    public static class AntiCheatDetector
    {
        // Known anti-cheat process names and their display names
        private static readonly Dictionary<string, AntiCheatInfo> AntiCheatProcesses = new()
        {
            // Riot Vanguard (Valorant)
            { "vgc", new AntiCheatInfo("Valorant Vanguard", "Riot Vanguard (Kernel)", AntiCheatSeverity.Critical) },
            { "vgk", new AntiCheatInfo("Valorant Vanguard", "Riot Vanguard (Kernel Service)", AntiCheatSeverity.Critical) },
            { "vanguard", new AntiCheatInfo("Valorant Vanguard", "Riot Vanguard", AntiCheatSeverity.Critical) },

            // Easy Anti-Cheat (EAC)
            { "easyanticheat_setup", new AntiCheatInfo("Easy Anti-Cheat", "EAC", AntiCheatSeverity.High) },
            { "easyanticheat", new AntiCheatInfo("Easy Anti-Cheat", "EAC Service", AntiCheatSeverity.High) },

            // BattlEye (BE)
            { "beservice", new AntiCheatInfo("BattlEye", "BattlEye Service", AntiCheatSeverity.High) },
            { "beservice_x64", new AntiCheatInfo("BattlEye", "BattlEye Service (x64)", AntiCheatSeverity.High) },
            { "bedaisy", new AntiCheatInfo("BattlEye", "BattlEye Driver", AntiCheatSeverity.High) },

            // PunkBuster
            { "punksv", new AntiCheatInfo("PunkBuster", "PunkBuster Server", AntiCheatSeverity.Medium) },
            { "puncl", new AntiCheatInfo("PunkBuster", "PunkBuster Client", AntiCheatSeverity.Medium) },

            // FaceIT Anti-Cheat
            { "faceitclient", new AntiCheatInfo("FACEIT AC", "FACEIT Client", AntiCheatSeverity.High) },
            { "faceitservice", new AntiCheatInfo("FACEIT AC", "FACEIT Service", AntiCheatSeverity.High) },

            // Vanguard/Other
            { "rifus", new AntiCheatInfo("Ricochet AC", "Ricochet Anti-Cheat (Warzone)", AntiCheatSeverity.Critical) },
            { "antitamper", new AntiCheatInfo("Game Anti-Tamper", "Generic Anti-Tamper", AntiCheatSeverity.Medium) },

            // NvExpert (Fortnite-related detections)
            { "fortniteclient", new AntiCheatInfo("Fortnite", "Fortnite Client", AntiCheatSeverity.High) },
        };

        // Known service names for anti-cheats
        private static readonly Dictionary<string, AntiCheatInfo> AntiCheatServices = new()
        {
            { "vgc", new AntiCheatInfo("Valorant Vanguard", "Riot Vanguard Service", AntiCheatSeverity.Critical) },
            { "vgk", new AntiCheatInfo("Valorant Vanguard", "Riot Vanguard Driver", AntiCheatSeverity.Critical) },
            { "EasyAntiCheat", new AntiCheatInfo("Easy Anti-Cheat", "EAC Service", AntiCheatSeverity.High) },
            { "BEService", new AntiCheatInfo("BattlEye", "BattlEye Service", AntiCheatSeverity.High) },
            { "PnkBstrA", new AntiCheatInfo("PunkBuster", "PunkBuster Service A", AntiCheatSeverity.Medium) },
            { "PnkBstrB", new AntiCheatInfo("PunkBuster", "PunkBuster Service B", AntiCheatSeverity.Medium) },
        };

        /// <summary>
        /// Severity levels for anti-cheat detection
        /// </summary>
        public enum AntiCheatSeverity
        {
            Low,      // May not affect gameplay
            Medium,   // May trigger warnings
            High,     // Likely to detect aim assist
            Critical  // Almost certainly will detect and ban
        }

        /// <summary>
        /// Information about a detected anti-cheat
        /// </summary>
        public class AntiCheatInfo
        {
            public string Game { get; }
            public string DisplayName { get; }
            public AntiCheatSeverity Severity { get; }

            public AntiCheatInfo(string game, string displayName, AntiCheatSeverity severity)
            {
                Game = game;
                DisplayName = displayName;
                Severity = severity;
            }
        }

        /// <summary>
        /// Result of anti-cheat detection scan
        /// </summary>
        public class DetectionResult
        {
            public List<AntiCheatInfo> DetectedAntiCheats { get; } = new();
            public bool AnyDetected => DetectedAntiCheats.Count > 0;
            public bool HasCritical => DetectedAntiCheats.Any(a => a.Severity == AntiCheatSeverity.Critical);
            public bool HasHigh => DetectedAntiCheats.Any(a => a.Severity == AntiCheatSeverity.High);
            public string Summary => AnyDetected
                ? $"Detected {DetectedAntiCheats.Count} anti-cheat(s): {string.Join(", ", DetectedAntiCheats.Select(a => a.DisplayName))}"
                : "No anti-cheats detected";
        }

        /// <summary>
        /// Scan for running anti-cheat processes and services
        /// </summary>
        public static DetectionResult Scan()
        {
            var result = new DetectionResult();
            var detectedNames = new HashSet<string>();

            try
            {
                // Scan running processes
                using (var process = new Process())
                {
                    var processes = Process.GetProcesses();
                    foreach (var proc in processes)
                    {
                        try
                        {
                            var procName = proc.ProcessName.ToLowerInvariant();

                            foreach (var kvp in AntiCheatProcesses)
                            {
                                if (procName.Contains(kvp.Key.ToLowerInvariant()) &&
                                    !detectedNames.Contains(kvp.Value.DisplayName))
                                {
                                    detectedNames.Add(kvp.Value.DisplayName);
                                    result.DetectedAntiCheats.Add(kvp.Value);
                                }
                            }
                        }
                        catch
                        {
                            // Access denied for some processes, skip
                        }
                    }
                }

                // Scan running services
                try
                {
                    using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Service WHERE State='Running'");
                    using var services = searcher.Get();
                    foreach (ManagementObject service in services)
                    {
                        try
                        {
                            var serviceName = service["Name"]?.ToString();
                            if (!string.IsNullOrEmpty(serviceName))
                            {
                                foreach (var kvp in AntiCheatServices)
                                {
                                    if (serviceName.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase) &&
                                        !detectedNames.Contains(kvp.Value.DisplayName))
                                    {
                                        detectedNames.Add(kvp.Value.DisplayName);
                                        result.DetectedAntiCheats.Add(kvp.Value);
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Skip service errors
                        }
                    }
                }
                catch
                {
                    // WMI query might fail, continue
                }
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"Anti-cheat scan error: {ex.Message}", false);
            }

            return result;
        }

        /// <summary>
        /// Check if a specific anti-cheat is running
        /// </summary>
        public static bool IsAntiCheatRunning(string antiCheatKey)
        {
            try
            {
                var processes = Process.GetProcesses();
                foreach (var proc in processes)
                {
                    try
                    {
                        if (proc.ProcessName.IndexOf(antiCheatKey, StringComparison.OrdinalIgnoreCase) >= 0)
                            return true;
                    }
                    catch { }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get color based on severity for UI display
        /// </summary>
        public static string GetSeverityColor(AntiCheatSeverity severity)
        {
            return severity switch
            {
                AntiCheatSeverity.Low => "#4CAF50",     // Green
                AntiCheatSeverity.Medium => "#FF9800",  // Orange
                AntiCheatSeverity.High => "#F44336",   // Red
                AntiCheatSeverity.Critical => "#B71C1C", // Dark Red
                _ => "#FFFFFF"
            };
        }

        /// <summary>
        /// Get severity description for tooltips
        /// </summary>
        public static string GetSeverityDescription(AntiCheatSeverity severity)
        {
            return severity switch
            {
                AntiCheatSeverity.Low => "Low risk - May not affect gameplay",
                AntiCheatSeverity.Medium => "Medium risk - May trigger warnings",
                AntiCheatSeverity.High => "High risk - Likely to detect aim assist",
                AntiCheatSeverity.Critical => "Critical risk - Almost certainly will detect and ban",
                _ => "Unknown"
            };
        }
    }
}