using Aimmy2.Class;
using Aimmy2.MouseMovementLibraries.GHubSupport;
using MouseMovementLibraries.XInputSupport;
using MouseMovementLibraries.DirectInputSupport;
using MouseMovementLibraries.ViGEmSupport;
using Class;
using MouseMovementLibraries.ddxoftSupport;
using MouseMovementLibraries.RazerSupport;
using MouseMovementLibraries.SendInputSupport;
using Other;
using System.Drawing;
using System.Runtime.InteropServices;

namespace InputLogic
{
    internal class MouseManager
    {
        private static readonly double ScreenWidth = WinAPICaller.ScreenWidth;
        private static readonly double ScreenHeight = WinAPICaller.ScreenHeight;

        private static DateTime LastClickTime = DateTime.MinValue;
        private static bool isSpraying = false;

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private static double previousX = 0;
        private static double previousY = 0;
        private const short RIGHT_THUMB_DEADZONE = 8689;
        public static double smoothingFactor = 0.5;
        public static bool IsEMASmoothingEnabled = false;
public static bool IsAntiAimJitterEnabled = false;

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

        private static Random MouseRandom = new();

        private static double EmaSmoothing(double previousValue, double currentValue, double smoothingFactor) => (currentValue * smoothingFactor) + (previousValue * (1 - smoothingFactor));

        // Cleanup
        private static (Action down, Action up) GetMouseActions()
        {
            string mouseMovementMethod = Dictionary.dropdownState["Mouse Movement Method"];
            Action mouseDownAction;
            Action mouseUpAction;

            switch (mouseMovementMethod)
            {
                case "SendInput":
                    mouseDownAction = () => SendInputMouse.SendMouseCommand(MOUSEEVENTF_LEFTDOWN);
                    mouseUpAction = () => SendInputMouse.SendMouseCommand(MOUSEEVENTF_LEFTUP);
                    break;
                case "LG HUB":
                    mouseDownAction = () => LGMouse.Move(1, 0, 0, 0);
                    mouseUpAction = () => LGMouse.Move(0, 0, 0, 0);
                    break;
                case "Razer Synapse (Require Razer Peripheral)":
                    mouseDownAction = () => RZMouse.mouse_click(1);
                    mouseUpAction = () => RZMouse.mouse_click(0);
                    break;
                case "ddxoft Virtual Input Driver":
                    mouseDownAction = () => DdxoftMain.ddxoftInstance.btn!(1);
                    mouseUpAction = () => DdxoftMain.ddxoftInstance.btn(2);
                    break;
                default:
                    mouseDownAction = () => mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                    mouseUpAction = () => mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                    break;
            }

            return (mouseDownAction, mouseUpAction);
        }

        public static async Task DoTriggerClick(RectangleF? detectionBox = null)
        {
            var triggerArmed = Dictionary.toggleState["Constant AI Tracking"] ||
                               InputBindingManager.IsHoldingBinding("Aim Keybind") ||
                               InputBindingManager.IsHoldingBinding("Second Aim Keybind");

            if (!triggerArmed)
            {
                ResetSprayState();
                return;
            }


            if (Dictionary.toggleState["Spray Mode"])
            {
                if (Dictionary.toggleState["Cursor Check"])
                {
                    Point mousePos = WinAPICaller.GetCursorPosition();

                    if (detectionBox.HasValue && !detectionBox.Value.Contains(mousePos.X, mousePos.Y))
                    {
                        if (isSpraying) ReleaseMouseButton();
                        return;
                    }
                }

                if (!isSpraying) HoldMouseButton();
                return;
            }

            // Single click logic if spray mode off
            int timeSinceLastClick = (int)(DateTime.UtcNow - LastClickTime).TotalMilliseconds;
            int triggerDelayMilliseconds = (int)(Dictionary.sliderSettings["Auto Trigger Delay"] * 1000);
            const int clickDelayMilliseconds = 20;

            if (timeSinceLastClick < triggerDelayMilliseconds && LastClickTime != DateTime.MinValue)
            {
                return;
            }

            var (mouseDown, mouseUp) = GetMouseActions();

            mouseDown.Invoke();
            await Task.Delay(clickDelayMilliseconds);
            mouseUp.Invoke();

            LastClickTime = DateTime.UtcNow;
        }

        public static int LastAntiRecoilClickTime = 0;

        public static void DoAntiRecoil()
        {
            int timeSinceLastClick = Math.Abs(DateTime.UtcNow.Millisecond - LastAntiRecoilClickTime);

            if (timeSinceLastClick < Convert.ToInt32(Dictionary.sliderSettings["AR Fire Rate"]))
            {
                return;
            }

            int xRecoil = Convert.ToInt32(Dictionary.sliderSettings["AR X Recoil"]);
            int yRecoil = Convert.ToInt32(Dictionary.sliderSettings["AR Y Recoil"]);

            switch (Dictionary.dropdownState["Mouse Movement Method"])
            {
                case "SendInput":
                    SendInputMouse.SendMouseCommand(MOUSEEVENTF_MOVE, xRecoil, yRecoil);
                    break;

                case "LG HUB":
                    LGMouse.Move(0, xRecoil, yRecoil, 0);
                    break;

                case "Razer Synapse (Require Razer Peripheral)":
                    RZMouse.mouse_move(xRecoil, yRecoil, true);
                    break;

                case "ddxoft Virtual Input Driver":
                    DdxoftMain.ddxoftInstance.movR!(xRecoil, yRecoil);
                    break;

                case "XInput (Controller Input)":
                    // AI aim + user controller right stick both contribute to movement
                    // This is like mouse: AI aims but you can guide it with the stick
                    var xinputCtrl = XInputMain.Instance;
                    if (xinputCtrl.IsConnected)
                    {
                        var state = xinputCtrl.GetState();
                        if (state == 0)
                        {
                            // Get raw stick values (-32767 to 32767) and scale to match AI aim range (-150 to 150)
                            short stickX = xinputCtrl.CurrentRightStickX;
                            short stickY = xinputCtrl.CurrentRightStickY;
                            
                            // Apply XInput deadzone
                            const short deadzone = RIGHT_THUMB_DEADZONE;
                            if (Math.Abs(stickX) < deadzone) stickX = 0;
                            if (Math.Abs(stickY) < deadzone) stickY = 0;
                            
                            // Scale stick to AI aim range: stick value * (150 / 32767)
                            int stickAimX = (int)Math.Round(stickX * (150.0 / 32767.0));
                            int stickAimY = (int)Math.Round(stickY * (150.0 / 32767.0));
                            
                            // IMPORTANT: Apply deadzone AFTER scaling, and clamp stick input separately
                            // This means small stick movements don't affect the aim, but big movements add to it
                            if (stickAimX != 0 || stickAimY != 0)
                            {
                                // Combine AI aim + user stick input
                                // Both inputs work together like two hands guiding a mouse
                                xRecoil += stickAimX;
                                yRecoil += stickAimY;
                                
                                // Clamp the combined output to stay within mouse_event limits
                                xRecoil = Math.Clamp(xRecoil, -150, 150);
                                yRecoil = Math.Clamp(yRecoil, -150, 150);
                            }
                        }
                    }
                    // Send combined AI + stick input as mouse movement
                    mouse_event(MOUSEEVENTF_MOVE, (uint)xRecoil, (uint)yRecoil, 0, 0);
                    break;

                default:
                    mouse_event(MOUSEEVENTF_MOVE, (uint)xRecoil, (uint)yRecoil, 0, 0);
                    break;
            }

            LastAntiRecoilClickTime = DateTime.UtcNow.Millisecond;
        }

        #region Spray Mode Methods
        public static void HoldMouseButton()
        {
            if (isSpraying) return;

            var (mouseDown, _) = GetMouseActions();
            mouseDown.Invoke();
            isSpraying = true;
        }

        public static void ReleaseMouseButton()
        {
            if (!isSpraying) return;

            var (_, mouseUp) = GetMouseActions();
            mouseUp.Invoke();
            isSpraying = false;
        }

        public static void ResetSprayState()
        {
            if (isSpraying)
            {
                ReleaseMouseButton();
            }
        }
        #endregion

        public static void MoveCrosshair(int detectedX, int detectedY)
        {
            int halfScreenWidth = (int)ScreenWidth / 2;
            int halfScreenHeight = (int)ScreenHeight / 2;

            int targetX = detectedX - halfScreenWidth;
            int targetY = detectedY - halfScreenHeight;

            double aspectRatioCorrection = ScreenWidth / ScreenHeight;

            int MouseJitter = (int)Dictionary.sliderSettings["Mouse Jitter"];
            int jitterX = MouseRandom.Next(-MouseJitter, MouseJitter);
            int jitterY = MouseRandom.Next(-MouseJitter, MouseJitter);

            Point start = new(0, 0);
            Point end = new(targetX, targetY);
            Point newPosition = new Point(0, 0);

            switch (Dictionary.dropdownState["Movement Path"])
            {
                case "Cubic Bezier":
                    Point control1 = new Point(start.X + (end.X - start.X) / 3, start.Y + (end.Y - start.Y) / 3);
                    Point control2 = new Point(start.X + 2 * (end.X - start.X) / 3, start.Y + 2 * (end.Y - start.Y) / 3);
                    newPosition = MovementPaths.CubicBezier(start, end, control1, control2, 1 - Dictionary.sliderSettings["Mouse Sensitivity (+/-)"]);
                    break;
                case "Linear":
                    newPosition = MovementPaths.Lerp(start, end, 1 - Dictionary.sliderSettings["Mouse Sensitivity (+/-)"]);
                    break;
                case "Exponential":
                    newPosition = MovementPaths.Exponential(start, end, 1 - (Dictionary.sliderSettings["Mouse Sensitivity (+/-)"] - 0.2), 3.0);
                    break;
                case "Adaptive":
                    newPosition = MovementPaths.Adaptive(start, end, 1 - Dictionary.sliderSettings["Mouse Sensitivity (+/-)"]);
                    break;
                case "Perlin Noise":
                    newPosition = MovementPaths.PerlinNoise(start, end, 1 - Dictionary.sliderSettings["Mouse Sensitivity (+/-)"], 20, 0.5);
                    break;
                default:
                    newPosition = MovementPaths.Lerp(start, end, 1 - Dictionary.sliderSettings["Mouse Sensitivity (+/-)"]);
                    break;
            }

            if (IsEMASmoothingEnabled)
            {
                newPosition.X = (int)EmaSmoothing(previousX, newPosition.X, smoothingFactor);
                newPosition.Y = (int)EmaSmoothing(previousY, newPosition.Y, smoothingFactor);
            }

            newPosition.X = Math.Clamp(newPosition.X, -150, 150);
            newPosition.Y = Math.Clamp(newPosition.Y, -150, 150);

            newPosition.Y = (int)(newPosition.Y / aspectRatioCorrection);

            newPosition.X += jitterX;
            newPosition.Y += jitterY;

            switch (Dictionary.dropdownState["Mouse Movement Method"])
            {
                case "SendInput":
                    SendInputMouse.SendMouseCommand(MOUSEEVENTF_MOVE, newPosition.X, newPosition.Y);
                    break;

                case "LG HUB":
                    LGMouse.Move(0, newPosition.X, newPosition.Y, 0);
                    break;

                case "Razer Synapse (Require Razer Peripheral)":
                    RZMouse.mouse_move(newPosition.X, newPosition.Y, true);
                    break;

                case "ddxoft Virtual Input Driver":
                    DdxoftMain.ddxoftInstance.movR!(newPosition.X, newPosition.Y);
                    break;

                case "XInput (Controller Input)":
                    // AI aim + user controller right stick both contribute to movement
                    // This is like mouse: AI aims but you can guide it with the stick
                    var xinputCtrl = XInputMain.Instance;
                    if (xinputCtrl.IsConnected)
                    {
                        var state = xinputCtrl.GetState();
                        if (state == 0)
                        {
                            // Get raw stick values (-32767 to 32767) and scale to match AI aim range (-150 to 150)
                            short stickX = xinputCtrl.CurrentRightStickX;
                            short stickY = xinputCtrl.CurrentRightStickY;
                            
                            // Apply XInput deadzone
                            const short deadzone = RIGHT_THUMB_DEADZONE;
                            if (Math.Abs(stickX) < deadzone) stickX = 0;
                            if (Math.Abs(stickY) < deadzone) stickY = 0;
                            
                            // Scale stick to AI aim range: stick value * (150 / 32767)
                            int stickAimX = (int)Math.Round(stickX * (150.0 / 32767.0));
                            int stickAimY = (int)Math.Round(stickY * (150.0 / 32767.0));
                            
                            // IMPORTANT: Apply deadzone AFTER scaling, and clamp stick input separately
                            // This means small stick movements don't affect the aim, but big movements add to it
                            if (stickAimX != 0 || stickAimY != 0)
                            {
                                // Combine AI aim + user stick input
                                // Both inputs work together like two hands guiding a mouse
                                newPosition.X += stickAimX;
                                newPosition.Y += stickAimY;
                                
                                // Clamp the combined output to stay within mouse_event limits
                                newPosition.X = Math.Clamp(newPosition.X, -150, 150);
                                newPosition.Y = Math.Clamp(newPosition.Y, -150, 150);
                            }
                        }
                    }
                    // Send combined AI + stick input as mouse movement
                    mouse_event(MOUSEEVENTF_MOVE, (uint)newPosition.X, (uint)newPosition.Y, 0, 0);
                    break;

                case "XInput (Normal)":
                    // Standard mouse movement, no controller input
                    mouse_event(MOUSEEVENTF_MOVE, (uint)newPosition.X, (uint)newPosition.Y, 0, 0);
                    break;

                case "DirectInput (PS4/PS5 Controller)":
                    // AI aim + user PS controller right stick both contribute to movement
                    // Uses calibration to properly detect stick center position
                    var directInput = DirectInputMain.Instance;
                    
                    // Also check R2 trigger to activate aimbot (like pressing aim keybind)
                    if (directInput.IsConnected)
                    {
                        // Get normalized stick values already in -150 to 150 range
                        var (stickX, stickY) = directInput.GetRightStickNormalized();
                        
                        if (stickX != 0 || stickY != 0)
                        {
                            // Combine AI aim + user stick input
                            // Both inputs work together - AI aims but you can guide it
                            newPosition.X += stickX;
                            newPosition.Y += stickY;
                            
                            // Clamp the combined output
                            newPosition.X = Math.Clamp(newPosition.X, -150, 150);
                            newPosition.Y = Math.Clamp(newPosition.Y, -150, 150);
                        }
                        
                        // Check if R2 is pressed to activate aimbot (R2 = right trigger)
                        bool r2Pressed = directInput.IsRightTriggerPressed();
                        if (r2Pressed)
                        {
                            // Simulate aim keybind being held
                            // This activates AI tracking when R2 is pressed
                            Dictionary.toggleState["Aim Assist"] = true;
                        }
                        
                        // Debug logging to see what's happening
                        if (Dictionary.toggleState.TryGetValue("Debug Mode", out var debugMode) && (bool)debugMode)
                        {
                            LogManager.Log(LogManager.LogLevel.Info, 
                                $"DI Input: stick=({stickX},{stickY}) ai=({newPosition.X},{newPosition.Y}) R2={directInput.IsRightTriggerPressed()}", false);
                        }
                    }
                    // Send combined AI + stick input as mouse movement
                    mouse_event(MOUSEEVENTF_MOVE, (uint)newPosition.X, (uint)newPosition.Y, 0, 0);
                    break;

                case "ViGEm Virtual Controller (Xbox 360 Output)":
                    // Output AI aim as virtual Xbox 360 controller right stick
                    // Gamepad testers and games will see this as a real Xbox 360 controller
                    if (!VirtualControllerOutput.IsConnected)
                    {
                        VirtualControllerOutput.Initialize();
                    }
                    if (VirtualControllerOutput.IsConnected)
                    {
                        // Scale AI aim output (-150..150) to virtual controller range (-32767..32767)
                        VirtualControllerOutput.SetAimStick(newPosition.X, newPosition.Y);
                    }
                    // ALSO send mouse event so cursor still moves on screen
                    mouse_event(MOUSEEVENTF_MOVE, (uint)newPosition.X, (uint)newPosition.Y, 0, 0);
                    break;

                default:
                    mouse_event(MOUSEEVENTF_MOVE, (uint)newPosition.X, (uint)newPosition.Y, 0, 0);
                    break;
            }

            previousX = newPosition.X;
            previousY = newPosition.Y;

            if (!Dictionary.toggleState["Auto Trigger"])
            {
                ResetSprayState();
            }
        }
    }
}
