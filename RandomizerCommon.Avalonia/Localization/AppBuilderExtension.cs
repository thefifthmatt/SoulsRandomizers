using Avalonia;
using Avalonia.Media;
using System;
using System.Linq;
using System.Threading;

namespace RandomizerCommon.Localization
{
    public static class AppBuilderExtension
    {
        public static AppBuilder WithCJKFallback(this AppBuilder builder)
        {
            string lang = Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName;
            string[] fontOrder;
            if (lang == "ja")
            {
                fontOrder = ["Meiryo UI", "Yu Gothic UI"];
            }
            else if (lang == "zh")
            {
                fontOrder = ["Microsoft YaHei UI"];
            }
            else
            {
                return builder;
            }
            UnicodeRange mainCjk = new(0x4E00, 0x9FFF);
            // Fallbacks have to be set on startup before FontManager is available and cannot be changed after, so just try our best
            return builder.With(new FontManagerOptions
            {
                FontFallbacks = fontOrder.Select(font => new FontFallback()
                {
                    FontFamily = new FontFamily(font),
                    UnicodeRange = mainCjk,
                }).ToList(),
            });
        }
    }
}
