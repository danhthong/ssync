using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using SandboxSync.Interop;

namespace SandboxSync.Views;

public partial class ClickOverlayWindow : Window
{
    public ClickOverlayWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var exStyle = NativeMethods.GetWindowLong(Handle, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(
            Handle,
            NativeMethods.GWL_EXSTYLE,
            exStyle | NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TRANSPARENT |
            NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOPMOST);
    }

    public void ShowRipple(int screenX, int screenY)
    {
        var local = PointFromScreen(new Point(screenX, screenY));

        var ellipse = new Ellipse
        {
            Width = 24,
            Height = 24,
            Stroke = new SolidColorBrush(Color.FromArgb(220, 0, 180, 255)),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(60, 0, 180, 255))
        };

        Canvas.SetLeft(ellipse, local.X - 12);
        Canvas.SetTop(ellipse, local.Y - 12);
        OverlayCanvas.Children.Add(ellipse);

        var scale = new DoubleAnimation(1, 3, TimeSpan.FromMilliseconds(350))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(350));

        scale.Completed += (_, _) => OverlayCanvas.Children.Remove(ellipse);

        ellipse.RenderTransform = new ScaleTransform(1, 1, 12, 12);
        ellipse.BeginAnimation(OpacityProperty, fade);
        ((ScaleTransform)ellipse.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, scale);
        ((ScaleTransform)ellipse.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, scale);
    }

    private IntPtr Handle =>
        new System.Windows.Interop.WindowInteropHelper(this).Handle;

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private static class NativeMethods
    {
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_LAYERED = 0x80000;
        public const int WS_EX_TRANSPARENT = 0x20;
        public const int WS_EX_TOOLWINDOW = 0x80;
        public const int WS_EX_NOACTIVATE = 0x8000000;
        public const int WS_EX_TOPMOST = 0x8;

        public static int GetWindowLong(IntPtr hwnd, int index) =>
            IntPtr.Size == 8
                ? (int)GetWindowLongPtr64(hwnd, index)
                : GetWindowLong32(hwnd, index);

        public static int SetWindowLong(IntPtr hwnd, int index, int value) =>
            IntPtr.Size == 8
                ? (int)SetWindowLongPtr64(hwnd, index, (nint)value)
                : SetWindowLong32(hwnd, index, value);
    }
}
