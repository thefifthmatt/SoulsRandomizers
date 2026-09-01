using Avalonia;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using RandomizerCommon;
using RandomizerCommon.Localization;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace EldenRingRandomizer
{
    internal sealed class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            CharacterWriter.MeasureText = (s, f) =>
            {
                // System.Drawing.Font is still not cross-platform, but it doesn't require WinForms at least
                // This requires the app to be initialized
                FormattedText text = new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(f.Name), f.SizeInPoints, null);
                return (int)text.Width;
            };
#if DEBUG
            Trace.Listeners.Add(new ConsoleTraceListener());
            Trace.AutoFlush = true;
#endif
            // Must be done before CJK fallback
            Messages.LoadCulture();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .WithCJKFallback()
                .LogToTrace(LogEventLevel.Warning)
                // .UseReactiveUI(rxAppBuilder => rxAppBuilder.WithExceptionHandler(Observer.Create<Exception>(App.HandleException)))
                .UseReactiveUI();
    }
}
