using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace MiniT.UI.Helpers;

/// <summary>
/// Small, reusable motion primitives used across the shell (sidebar, tabs,
/// search overlay, tool cards). Centralizing these means every interactive
/// element eases in/out with the same feel instead of each place inventing
/// its own timing — that consistency is a big part of what makes an app feel
/// like one deliberate product instead of a pile of screens.
/// </summary>
public static class Motion
{
    private static QuadraticEase EaseOut => new() { EasingMode = EasingMode.EaseOut };

    public static void FadeTo(UIElement target, double to, int ms = 120, Action? completed = null)
    {
        var sb = new Storyboard();
        var anim = new DoubleAnimation
        {
            To = to,
            Duration = new Duration(TimeSpan.FromMilliseconds(ms)),
            EasingFunction = EaseOut
        };
        Storyboard.SetTarget(anim, target);
        Storyboard.SetTargetProperty(anim, "Opacity");
        sb.Children.Add(anim);
        if (completed != null) sb.Completed += (_, _) => completed();
        sb.Begin();
    }

    /// <summary>
    /// Cross-fades a ContentControl's (Frame's) content instead of swapping it
    /// instantly, so moving between a Space, a tool tab, and Settings reads as
    /// a deliberate transition rather than an abrupt cut.
    /// </summary>
    public static void SwapContent(ContentControl host, object? newContent)
    {
        FadeTo(host, 0, 90, () =>
        {
            host.Content = newContent;
            host.Opacity = 0;
            FadeTo(host, 1, 140);
        });
    }

    public static void AnimateScale(ScaleTransform scale, double to, int ms = 100)
    {
        var sb = new Storyboard();
        var ax = new DoubleAnimation { To = to, Duration = new Duration(TimeSpan.FromMilliseconds(ms)), EasingFunction = EaseOut };
        var ay = new DoubleAnimation { To = to, Duration = new Duration(TimeSpan.FromMilliseconds(ms)), EasingFunction = EaseOut };
        Storyboard.SetTarget(ax, scale); Storyboard.SetTargetProperty(ax, "ScaleX");
        Storyboard.SetTarget(ay, scale); Storyboard.SetTargetProperty(ay, "ScaleY");
        sb.Children.Add(ax);
        sb.Children.Add(ay);
        sb.Begin();
    }

    /// <summary>
    /// Wires up a "press down" scale on any element that already exposes a
    /// ScaleTransform as (part of) its RenderTransform — the same kind of
    /// tactile click feedback buttons get on iOS/macOS, applied consistently
    /// to our custom-built rows and pills.
    /// </summary>
    public static void AttachPressScale(UIElement element, ScaleTransform scale, double pressScale = 0.965, double restScale = 1.0)
    {
        element.PointerPressed += (_, _) => AnimateScale(scale, pressScale, 70);
        element.PointerReleased += (_, _) => AnimateScale(scale, restScale, 100);
        element.PointerCanceled += (_, _) => AnimateScale(scale, restScale, 100);
        element.PointerCaptureLost += (_, _) => AnimateScale(scale, restScale, 100);
    }

    /// <summary>
    /// Fades and gently rises a freshly-added element into place. Used to
    /// stagger a grid of tool cards in one after another (via <paramref name="delayMs"/>)
    /// instead of the whole grid popping in at once.
    /// </summary>
    public static void EntranceFadeRise(UIElement target, TranslateTransform translate, int delayMs = 0)
    {
        target.Opacity = 0;
        translate.Y = 8;
        var sb = new Storyboard { BeginTime = TimeSpan.FromMilliseconds(delayMs) };

        var fade = new DoubleAnimation { To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(220)), EasingFunction = EaseOut };
        Storyboard.SetTarget(fade, target);
        Storyboard.SetTargetProperty(fade, "Opacity");

        var rise = new DoubleAnimation { To = 0, Duration = new Duration(TimeSpan.FromMilliseconds(220)), EasingFunction = EaseOut };
        Storyboard.SetTarget(rise, translate);
        Storyboard.SetTargetProperty(rise, "Y");

        sb.Children.Add(fade);
        sb.Children.Add(rise);
        sb.Begin();
    }

    /// <summary>
    /// Grows an element in vertically from its center — used for the sidebar's
    /// active-space indicator bar, so switching spaces reads as the indicator
    /// "snapping" onto the new row instead of just appearing.
    /// </summary>
    public static void GrowVerticalIn(FrameworkElement element, int ms = 160)
    {
        element.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
        var scale = new ScaleTransform { ScaleX = 1, ScaleY = 0 };
        element.RenderTransform = scale;

        var sb = new Storyboard();
        var anim = new DoubleAnimation { To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(ms)), EasingFunction = EaseOut };
        Storyboard.SetTarget(anim, scale);
        Storyboard.SetTargetProperty(anim, "ScaleY");
        sb.Children.Add(anim);
        sb.Begin();
    }

    /// <summary>
    /// Slides and resizes an indicator (e.g. the segmented-filter highlight) to
    /// sit exactly under a newly-selected option, instead of the highlight just
    /// jumping there — the same feel as iOS segmented controls or a browser's
    /// tab-indicator underline.
    /// </summary>
    public static void AnimateSlide(TranslateTransform translate, FrameworkElement element, double toX, double toWidth, int ms = 200)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var sb = new Storyboard();

        var moveX = new DoubleAnimation { To = toX, Duration = new Duration(TimeSpan.FromMilliseconds(ms)), EasingFunction = ease };
        Storyboard.SetTarget(moveX, translate);
        Storyboard.SetTargetProperty(moveX, "X");

        var resize = new DoubleAnimation { To = toWidth, Duration = new Duration(TimeSpan.FromMilliseconds(ms)), EasingFunction = ease };
        Storyboard.SetTarget(resize, element);
        Storyboard.SetTargetProperty(resize, "Width");

        sb.Children.Add(moveX);
        sb.Children.Add(resize);
        sb.Begin();
    }

    public static void SetSlideImmediate(TranslateTransform translate, FrameworkElement element, double x, double width)
    {
        translate.X = x;
        element.Width = width;
    }
}
