using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using Aimmy2.Class;
using Other;

namespace MouseMovementLibraries.ViGEmSupport
{
    /// <summary>
    /// Virtual Xbox 360 controller output using ViGEmBus.
    /// Creates a virtual controller that games/gamepad testers can detect.
    /// The AI aim is sent as virtual right stick movements.
    /// Requires ViGEmBus driver: https://github.com/ViGEm/ViGEmBus/releases
    /// </summary>
    internal class VirtualControllerOutput
    {
        #region Native ViGEm P/Invoke
        [DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32")]
        private static extern bool FreeLibrary(IntPtr hModule);

        private static IntPtr _vigemHandle = IntPtr.Zero;
        private static IntPtr _vigemClient = IntPtr.Zero;
        private static IntPtr _x360Target = IntPtr.Zero;
        private static bool _isConnected = false;

        private const string ViGEmClientDllName = "ViGEmClient.dll";
        private const string ViGEmDownloadUrl = "https://gitlab.com/marsqq/extra-files/-/raw/main/ViGEmClient.dll";
        private const string ViGEmInstallerPath = "runtimes\\vig.exe";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr ViGEmAllocDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ViGEmConnectDelegate(IntPtr client);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ViGEmDisconnectDelegate(IntPtr client);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ViGEmFreeDelegate(IntPtr client);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ViGEmTargetX360AllocDelegate(out IntPtr target);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ViGEmTargetAddDelegate(IntPtr client, IntPtr target);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ViGEmTargetRemoveDelegate(IntPtr client, IntPtr target);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ViGEmTargetFreeDelegate(IntPtr target);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ViGEmTargetX360UpdateDelegate(IntPtr client, IntPtr target, ref X360Report report);

        [StructLayout(LayoutKind.Sequential)]
        private struct X360Report
        {
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        private static ViGEmAllocDelegate? _vigem_alloc;
        private static ViGEmConnectDelegate? _vigem_connect;
        private static ViGEmDisconnectDelegate? _vigem_disconnect;
        private static ViGEmFreeDelegate? _vigem_free;
        private static ViGEmTargetX360AllocDelegate? _vigem_target_x360_alloc;
        private static ViGEmTargetAddDelegate? _vigem_target_add;
        private static ViGEmTargetRemoveDelegate? _vigem_target_remove;
        private static ViGEmTargetFreeDelegate? _vigem_target_free;
        private static ViGEmTargetX360UpdateDelegate? _vigem_target_x360_update;

        private static X360Report _currentReport;

        /// <summary>
        /// Search for ViGEmClient.dll in common locations and download if not found
        /// </summary>
        private static string? FindViGEmClientDll()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            
            // Search order: current directory, system directories, ViGEm install path, runtimes, ProgramData
            string[] searchPaths = new[]
            {
                ViGEmClientDllName,  // Current directory
                Path.Combine(baseDir, ViGEmClientDllName),
                Path.Combine(baseDir, "runtimes", "win-x64", "native", ViGEmClientDllName),
                Path.Combine(baseDir, "runtimes", "win", ViGEmClientDllName),
                Path.Combine(baseDir, "runtimes", ViGEmClientDllName),
                Path.Combine(Environment.SystemDirectory, ViGEmClientDllName),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Nefarius Software Solutions e.U", "ViGEm Bus Driver", ViGEmClientDllName),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Nefarius Software Solutions e.U", "ViGEm Bus Driver", ViGEmClientDllName),
                // Also check common ViGEm install locations
                @"C:\Program Files\Nefarius Software Solutions e.U\ViGEm Bus Driver\ViGEmClient.dll",
                @"C:\Program Files (x86)\Nefarius Software Solutions e.U\ViGEm Bus Driver\ViGEmClient.dll",
                // Check ProgramData
                @"C:\ProgramData\ViGEmBus\ViGEmClient.dll",
                // Check Windows directory
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), ViGEmClientDllName),
            };

            foreach (var path in searchPaths)
            {
                try
                {
                    string fullPath = Path.GetFullPath(path);
                    LogManager.Log(LogManager.LogLevel.Info, $"Searching for ViGEmClient.dll at: {fullPath}", false);
                    if (File.Exists(fullPath))
                    {
                        LogManager.Log(LogManager.LogLevel.Info, $"FOUND ViGEmClient.dll at: {fullPath}", false);
                        return fullPath;
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Log(LogManager.LogLevel.Info, $"Search error at {path}: {ex.Message}", false);
                }
            }

            // Also search all subdirectories of Program Files for ViGEmClient.dll
            try
            {
                LogManager.Log(LogManager.LogLevel.Info, "Deep searching Program Files for ViGEmClient.dll...", false);
                var progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                
                foreach (var baseDir2 in new[] { progFiles, progFilesX86 })
                {
                    if (Directory.Exists(baseDir2))
                    {
                        var files = Directory.GetFiles(baseDir2, ViGEmClientDllName, SearchOption.AllDirectories);
                        if (files.Length > 0)
                        {
                            LogManager.Log(LogManager.LogLevel.Info, $"FOUND ViGEmClient.dll at: {files[0]}", false);
                            return files[0];
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Info, $"Deep search error: {ex.Message}", false);
            }

            LogManager.Log(LogManager.LogLevel.Warning, "ViGEmClient.dll not found in any searched location.", false);
            return null;
        }

        /// <summary>
        /// Download ViGEmClient.dll if not found locally
        /// </summary>
        private static async Task<bool> DownloadViGEmClient()
        {
            try
            {
                LogManager.Log(LogManager.LogLevel.Info, $"{ViGEmClientDllName} not found, attempting to download...", true);

                using HttpClient httpClient = new();
                var response = await httpClient.GetAsync(new Uri(ViGEmDownloadUrl));

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsByteArrayAsync();
                    string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ViGEmClientDllName);
                    await File.WriteAllBytesAsync(localPath, content);
                    LogManager.Log(LogManager.LogLevel.Info, $"{ViGEmClientDllName} downloaded successfully. Please re-select ViGEm Virtual Controller to load it.", true);
                    return true;
                }
                else
                {
                    LogManager.Log(LogManager.LogLevel.Error, $"Download failed with status: {response.StatusCode}", true);
                }
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"{ViGEmClientDllName} download failed: {ex.Message}", true);
            }

            LogManager.Log(LogManager.LogLevel.Warning, "Failed to download ViGEmClient.dll. You can also install ViGEmBus from: https://github.com/ViGEm/ViGEmBus/releases", true);
            return false;
        }

        /// <summary>
        /// Load the ViGEmClient.dll
        /// </summary>
        private static IntPtr LoadViGEmDll(string path)
        {
            try
            {
                IntPtr handle = LoadLibrary(path);
                if (handle != IntPtr.Zero)
                {
                    LogManager.Log(LogManager.LogLevel.Info, $"Loaded ViGEmClient.dll from: {path}", false);
                }
                return handle;
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"Failed to load ViGEmClient.dll: {ex.Message}", true);
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// Try to load ViGEmClient.dll and download/install if not found
        /// Returns true if DLL was loaded successfully
        /// </summary>
        public static async Task<bool> LoadViGEmClient()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            
            // First, try to find existing DLL
            string? existingPath = FindViGEmClientDll();
            if (existingPath != null)
            {
                IntPtr handle = LoadViGEmDll(existingPath);
                if (handle != IntPtr.Zero)
                {
                    return true;
                }
            }

            // DLL not found locally, download it first
            LogManager.Log(LogManager.LogLevel.Info, "ViGEmClient.dll not found. Downloading required client library...", true);
            bool downloaded = await DownloadViGEmClient();
            
            if (downloaded)
            {
                // Try loading the downloaded DLL
                string downloadedPath = Path.Combine(baseDir, ViGEmClientDllName);
                if (File.Exists(downloadedPath))
                {
                    IntPtr handle = LoadViGEmDll(downloadedPath);
                    if (handle != IntPtr.Zero)
                    {
                        LogManager.Log(LogManager.LogLevel.Info, "ViGEmClient.dll loaded successfully. Now checking for driver...", true);
                        // DLL loaded, but we still need the driver installed
                        // Check if vig.exe installer exists and prompt user
                        string localInstaller = Path.Combine(baseDir, ViGEmInstallerPath);
                        if (File.Exists(localInstaller))
                        {
                            LogManager.Log(LogManager.LogLevel.Info, "ViGEmBus driver installer found. Running installer...", true);
                            try
                            {
                                var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = localInstaller,
                                    UseShellExecute = true,
                                    Verb = "runas"
                                });
                                LogManager.Log(LogManager.LogLevel.Info, "Please complete the ViGEmBus installation, then select 'ViGEm Virtual Controller' again.", true);
                            }
                            catch (Exception ex)
                            {
                                LogManager.Log(LogManager.LogLevel.Error, $"Failed to start ViGEm installer: {ex.Message}", true);
                            }
                        }
                        else
                        {
                            LogManager.Log(LogManager.LogLevel.Warning, "ViGEmBus driver not installed. Please install from: https://github.com/ViGEm/ViGEmBus/releases", true);
                        }
                        return false; // Need user to complete install first
                    }
                }
            }

            // Download failed, check for installer
            string installer = Path.Combine(baseDir, ViGEmInstallerPath);
            if (File.Exists(installer))
            {
                LogManager.Log(LogManager.LogLevel.Info, "Found ViGEmBus installer. Please run it to install the driver.", true);
                try
                {
                    var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = installer,
                        UseShellExecute = true,
                        Verb = "runas"
                    });
                    LogManager.Log(LogManager.LogLevel.Info, "After installation completes, select 'ViGEm Virtual Controller' again.", true);
                }
                catch (Exception ex)
                {
                    LogManager.Log(LogManager.LogLevel.Error, $"Failed to start installer: {ex.Message}", true);
                }
            }
            else
            {
                LogManager.Log(LogManager.LogLevel.Error, "ViGEmClient.dll not found and download failed. Please install ViGEmBus from: https://github.com/ViGEm/ViGEmBus/releases", true);
            }

            return false;
        }

        /// <summary>
        /// Try to load ViGEmClient.dll from system paths
        /// </summary>
        public static bool Initialize()
        {
            try
            {
                if (_isConnected) return true;

                // First try the deep search used by FindViGEmClientDll
                string? foundPath = FindViGEmClientDll();
                if (foundPath != null)
                {
                    _vigemHandle = LoadLibrary(foundPath);
                    if (_vigemHandle != IntPtr.Zero)
                    {
                        LogManager.Log(LogManager.LogLevel.Info, $"Loaded ViGEmClient.dll from: {foundPath}", false);
                    }
                    else
                    {
                        LogManager.Log(LogManager.LogLevel.Error, $"Found ViGEmClient.dll at {foundPath} but failed to load it", true);
                        return false;
                    }
                }

                if (_vigemHandle == IntPtr.Zero)
                {
                    LogManager.Log(LogManager.LogLevel.Error,
                        $"{ViGEmClientDllName} not found. Please install ViGEmBus driver from: https://github.com/ViGEm/ViGEmBus/releases", true);
                    return false;
                }

                // Get function pointers
                _vigem_alloc = Marshal.GetDelegateForFunctionPointer<ViGEmAllocDelegate>(GetProcAddress(_vigemHandle, "vigem_alloc"))!;
                _vigem_connect = Marshal.GetDelegateForFunctionPointer<ViGEmConnectDelegate>(GetProcAddress(_vigemHandle, "vigem_connect"))!;
                _vigem_disconnect = Marshal.GetDelegateForFunctionPointer<ViGEmDisconnectDelegate>(GetProcAddress(_vigemHandle, "vigem_disconnect"))!;
                _vigem_free = Marshal.GetDelegateForFunctionPointer<ViGEmFreeDelegate>(GetProcAddress(_vigemHandle, "vigem_free"))!;
                _vigem_target_x360_alloc = Marshal.GetDelegateForFunctionPointer<ViGEmTargetX360AllocDelegate>(GetProcAddress(_vigemHandle, "vigem_target_x360_alloc"))!;
                _vigem_target_add = Marshal.GetDelegateForFunctionPointer<ViGEmTargetAddDelegate>(GetProcAddress(_vigemHandle, "vigem_target_add"))!;
                _vigem_target_remove = Marshal.GetDelegateForFunctionPointer<ViGEmTargetRemoveDelegate>(GetProcAddress(_vigemHandle, "vigem_target_remove"))!;
                _vigem_target_free = Marshal.GetDelegateForFunctionPointer<ViGEmTargetFreeDelegate>(GetProcAddress(_vigemHandle, "vigem_target_free"))!;
                _vigem_target_x360_update = Marshal.GetDelegateForFunctionPointer<ViGEmTargetX360UpdateDelegate>(GetProcAddress(_vigemHandle, "vigem_target_x360_update"))!;

                // Create ViGEm client
                _vigemClient = _vigem_alloc();
                if (_vigemClient == IntPtr.Zero)
                {
                    LogManager.Log(LogManager.LogLevel.Error, "Failed to allocate ViGEm client", true);
                    return false;
                }

                // Connect to ViGEmBus
                int result = _vigem_connect(_vigemClient);
                if (result != 0)
                {
                    string errorDesc = result switch
                    {
                        -536870351 => "Device not found - ViGEmBus driver not installed",
                        -536870350 => "Invalid handle",
                        -536870349 => "Target not found",
                        1 => "ViGEM_BUS_ALREADY_CONNECTED - already connected",
                        _ => $"Unknown error code: {result} (0x{result:X8})"
                    };
                    LogManager.Log(LogManager.LogLevel.Error, 
                        $"Failed to connect to ViGEmBus.\n" +
                        $"Error code: {result} ({errorDesc})\n" +
                        $"The ViGEmBus driver may not be installed or not running.\n" +
                        $"Please run vig.exe installer as Administrator, then restart your PC and try again.", true);
                    _vigem_free(_vigemClient);
                    _vigemClient = IntPtr.Zero;
                    return false;
                }

                // Allocate X360 target
                result = _vigem_target_x360_alloc(out _x360Target);
                if (result != 0 || _x360Target == IntPtr.Zero)
                {
                    LogManager.Log(LogManager.LogLevel.Error, "Failed to allocate X360 target", true);
                    _vigem_disconnect(_vigemClient);
                    _vigem_free(_vigemClient);
                    _vigemClient = IntPtr.Zero;
                    return false;
                }

                // Add target to ViGEmBus
                result = _vigem_target_add(_vigemClient, _x360Target);
                if (result != 0)
                {
                    LogManager.Log(LogManager.LogLevel.Error, $"Failed to add X360 target (error code: {result})", true);
                    _vigem_target_free(_x360Target);
                    _vigem_disconnect(_vigemClient);
                    _vigem_free(_vigemClient);
                    _x360Target = IntPtr.Zero;
                    _vigemClient = IntPtr.Zero;
                    return false;
                }

                _currentReport = new X360Report();
                _isConnected = true;

                LogManager.Log(LogManager.LogLevel.Info, "Virtual Xbox 360 controller connected via ViGEmBus", false);
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"ViGEm initialization error: {ex.Message}", true);
                Disconnect();
                return false;
            }
        }

        /// <summary>
        /// Set virtual right stick position
        /// Input: x, y in -150 to 150 range (from MouseManager)
        /// Output: XInput range -32767 to 32767
        /// </summary>
        public static void SetRightStick(int x, int y)
        {
            if (!_isConnected) return;

            try
            {
                // Scale from -150..150 to -32767..32767
                _currentReport.sThumbRX = (short)Math.Clamp((int)Math.Round(x * (32767.0 / 150.0)), -32767, 32767);
                _currentReport.sThumbRY = (short)Math.Clamp((int)Math.Round(y * (32767.0 / 150.0)), -32767, 32767);

                // Update virtual controller
                _vigem_target_x360_update!(_vigemClient, _x360Target, ref _currentReport);
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"SetRightStick error: {ex.Message}", false);
            }
        }

        /// <summary>
        /// Set virtual left stick position
        /// </summary>
        public static void SetLeftStick(int x, int y)
        {
            if (!_isConnected) return;

            try
            {
                _currentReport.sThumbLX = (short)Math.Clamp((int)Math.Round(x * (32767.0 / 150.0)), -32767, 32767);
                _currentReport.sThumbLY = (short)Math.Clamp((int)Math.Round(y * (32767.0 / 150.0)), -32767, 32767);

                _vigem_target_x360_update!(_vigemClient, _x360Target, ref _currentReport);
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"SetLeftStick error: {ex.Message}", false);
            }
        }

        /// <summary>
        /// Set virtual right trigger (0-255)
        /// </summary>
        public static void SetRightTrigger(byte value)
        {
            if (!_isConnected) return;

            try
            {
                _currentReport.bRightTrigger = value;
                _vigem_target_x360_update!(_vigemClient, _x360Target, ref _currentReport);
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"SetRightTrigger error: {ex.Message}", false);
            }
        }

        /// <summary>
        /// Press a button on the virtual controller
        /// </summary>
        public static void PressButton(ushort button)
        {
            if (!_isConnected) return;

            try
            {
                _currentReport.wButtons |= button;
                _vigem_target_x360_update!(_vigemClient, _x360Target, ref _currentReport);
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"PressButton error: {ex.Message}", false);
            }
        }

        /// <summary>
        /// Release a button on the virtual controller
        /// </summary>
        public static void ReleaseButton(ushort button)
        {
            if (!_isConnected) return;

            try
            {
                _currentReport.wButtons = (ushort)(_currentReport.wButtons & ~button);
                _vigem_target_x360_update!(_vigemClient, _x360Target, ref _currentReport);
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"ReleaseButton error: {ex.Message}", false);
            }
        }

        /// <summary>
        /// Reset all sticks and buttons to neutral
        /// </summary>
        public static void ResetAll()
        {
            if (!_isConnected) return;

            try
            {
                _currentReport = new X360Report();
                _vigem_target_x360_update!(_vigemClient, _x360Target, ref _currentReport);
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"ResetAll error: {ex.Message}", false);
            }
        }

        #endregion

        public static bool IsConnected => _isConnected;

        /// <summary>
        /// Disconnect and cleanup all ViGEm resources
        /// </summary>
        public static void Disconnect()
        {
            try
            {
                if (_isConnected)
                {
                    ResetAll();
                }

                if (_x360Target != IntPtr.Zero && _vigemClient != IntPtr.Zero && _vigem_target_remove != null)
                {
                    _vigem_target_remove(_vigemClient, _x360Target);
                    _x360Target = IntPtr.Zero;
                }

                if (_x360Target != IntPtr.Zero && _vigem_target_free != null)
                {
                    _vigem_target_free(_x360Target);
                    _x360Target = IntPtr.Zero;
                }

                if (_vigemClient != IntPtr.Zero && _vigem_disconnect != null)
                {
                    _vigem_disconnect(_vigemClient);
                    _vigemClient = IntPtr.Zero;
                }

                if (_vigemClient != IntPtr.Zero && _vigem_free != null)
                {
                    _vigem_free(_vigemClient);
                    _vigemClient = IntPtr.Zero;
                }

                if (_vigemHandle != IntPtr.Zero)
                {
                    FreeLibrary(_vigemHandle);
                    _vigemHandle = IntPtr.Zero;
                }

                _isConnected = false;

                // Clear delegates
                _vigem_alloc = null;
                _vigem_connect = null;
                _vigem_disconnect = null;
                _vigem_free = null;
                _vigem_target_x360_alloc = null;
                _vigem_target_add = null;
                _vigem_target_remove = null;
                _vigem_target_free = null;
                _vigem_target_x360_update = null;

                LogManager.Log(LogManager.LogLevel.Info, "Virtual controller disconnected", false);
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"Disconnect error: {ex.Message}", false);
                _isConnected = false;
            }
        }
    }
}