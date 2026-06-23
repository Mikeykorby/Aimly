using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AimmyWPF.Class
{
    public static class Animator
    {
        public static Storyboard StoryBoard = new();
        private static TimeSpan duration = TimeSpan.FromMilliseconds(500);
        //private static TimeSpan duration2 = TimeSpan.FromMilliseconds(1000);

        private static readonly IEasingFunction Smooth = new QuarticEase
        {
            EasingMode = EasingMode.EaseInOut
        };

        public static void Fade(DependencyObject Object)
        {
            DoubleAnimation FadeIn = new()
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(duration),
            };
            Storyboard.SetTarget(FadeIn, Object);
            Storyboard.SetTargetProperty(FadeIn, new PropertyPath("Opacity", 1));
            StoryBoard.Children.Add(FadeIn);
            StoryBoard.Begin();
            StoryBoard.Children.Remove(FadeIn);
        }

        public static void FadeOut(DependencyObject Object)
        {
            DoubleAnimation Fade = new()
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(duration),
            };
            Storyboard.SetTarget(Fade, Object);
            Storyboard.SetTargetProperty(Fade, new PropertyPath("Opacity", 1));
            StoryBoard.Children.Add(Fade);
            StoryBoard.Begin();
            StoryBoard.Children.Remove(Fade);
        }

        public static void SlideAndFadeIn(UIElement element, double fromYOffset = 20)
        {
            var translate = new TranslateTransform(0, fromYOffset);
            element.RenderTransform = translate;

            var slideAnim = new DoubleAnimation
            {
                From = fromYOffset,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
            };

            var fadeAnim = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
            };

            translate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            element.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
        }

        public static void SlideAndFadeOut(UIElement element, double toYOffset = -20)
        {
            var translate = new TranslateTransform(0, 0);
            element.RenderTransform = translate;

            var slideAnim = new DoubleAnimation
            {
                From = 0,
                To = toYOffset,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseIn }
            };

            var fadeAnim = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseIn }
            };

            translate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            element.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
        }

        public static void ObjectShift(Duration speed, DependencyObject Object, Thickness Get, Thickness Set)
        {
            ThicknessAnimation Animation = new()
            {
                From = Get,
                To = Set,
                Duration = speed,
                EasingFunction = Smooth,
            };
            Storyboard.SetTarget(Animation, Object);
            Storyboard.SetTargetProperty(Animation, new PropertyPath("(Panel.Margin)"));
            StoryBoard.Children.Add(Animation);
            StoryBoard.Begin();
            StoryBoard.Children.Remove(Animation);
        }

        public static void WidthShift(Duration speed, FrameworkElement element, double originalSize, double newSize)
        {
            var animation = new DoubleAnimation
            {
                From = originalSize,
                To = newSize,
                Duration = speed,
                EasingFunction = new QuarticEase()
            };

            element.BeginAnimation(FrameworkElement.WidthProperty, animation);
        }

        public static void HeightShift(Duration speed, FrameworkElement element, double originalSize, double newSize)
        {
            var animation = new DoubleAnimation
            {
                From = originalSize,
                To = newSize,
                Duration = speed,
                EasingFunction = new QuarticEase()
            };

            element.BeginAnimation(FrameworkElement.HeightProperty, animation);
        }



        // This is old, replaced with new -- Saving it here tho just incase i run into problems.
        /*
        public static void WidthShift(Duration speed, Ellipse Circle, double OriginalSize, double NewSize)
        {
            DoubleAnimation doubleanimation = new DoubleAnimation();
            doubleanimation.From = new double?(OriginalSize);
            doubleanimation.To = new double?(NewSize);
            doubleanimation.Duration = speed;
            doubleanimation.EasingFunction = new QuarticEase();
            Circle.BeginAnimation(FrameworkElement.WidthProperty, doubleanimation); ;
        }

        public static void HeightShift(Duration speed, Ellipse Circle, double OriginalSize, double NewSize)
        {
            DoubleAnimation doubleanimation = new DoubleAnimation();
            doubleanimation.From = new double?(OriginalSize);
            doubleanimation.To = new double?(NewSize);
            doubleanimation.Duration = speed;
            doubleanimation.EasingFunction = new QuarticEase();
            Circle.BeginAnimation(FrameworkElement.HeightProperty, doubleanimation); ;
        }
        */
    }
}