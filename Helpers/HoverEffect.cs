using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;

namespace AnimeStreamer.Helpers
{
    public static class HoverEffect
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(HoverEffect), new PropertyMetadata(false, OnIsEnabledChanged));

        public static void SetIsEnabled(UIElement element, bool value) => element.SetValue(IsEnabledProperty, value);
        public static bool GetIsEnabled(UIElement element) => (bool)element.GetValue(IsEnabledProperty);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element)
            {
                if ((bool)e.NewValue)
                {
                    // Ensure the element scales from its center
                    element.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
                    if (!(element.RenderTransform is ScaleTransform))
                    {
                        element.RenderTransform = new ScaleTransform { ScaleX = 1, ScaleY = 1 };
                    }

                    element.PointerEntered += Element_PointerEntered;
                    element.PointerExited += Element_PointerExited;
                }
                else
                {
                    element.PointerEntered -= Element_PointerEntered;
                    element.PointerExited -= Element_PointerExited;
                }
            }
        }

        private static void Element_PointerEntered(object sender, PointerRoutedEventArgs e) => AnimateScale((UIElement)sender, 1.05);
        private static void Element_PointerExited(object sender, PointerRoutedEventArgs e) => AnimateScale((UIElement)sender, 1.0);

        private static void AnimateScale(UIElement element, double targetScale)
        {
            if (element.RenderTransform is ScaleTransform scaleTransform)
            {
                var storyboard = new Storyboard();
                var duration = new Duration(TimeSpan.FromMilliseconds(200));
                var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut }; // Smooth CSS-like curve

                var animX = new DoubleAnimation { To = targetScale, Duration = duration, EasingFunction = easing };
                Storyboard.SetTarget(animX, scaleTransform);
                Storyboard.SetTargetProperty(animX, "ScaleX");

                var animY = new DoubleAnimation { To = targetScale, Duration = duration, EasingFunction = easing };
                Storyboard.SetTarget(animY, scaleTransform);
                Storyboard.SetTargetProperty(animY, "ScaleY");

                storyboard.Children.Add(animX);
                storyboard.Children.Add(animY);
                storyboard.Begin();
            }
        }
    }
}