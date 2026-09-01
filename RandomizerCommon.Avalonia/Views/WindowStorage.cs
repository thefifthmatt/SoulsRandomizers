using Avalonia;
using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RandomizerCommon.Views
{
    public static class WindowStorage
    {
        public static void Save(Window window)
        {
            if (window.WindowState == WindowState.Normal)
            {
                Rectangle rect = GetBounds(window);
                PixelRect pixelRect = new PixelRect(rect.X, rect.Y, rect.Width, rect.Height);
                if (window.Screens.All.Any(s => s.Bounds.Contains(pixelRect)))
                {
                    RandomizerOptions.SaveWindowPosition(rect);
                }
            }
        }

        public static void Restore(Window window)
        {
            Rectangle rect = RandomizerOptions.GetWindowPosition();
            if (rect.IsEmpty)
            {
                return;
            }
            PixelRect pixelRect = new(rect.X, rect.Y, Math.Max(rect.Width, (int)window.MinWidth), Math.Max(rect.Height, (int)window.MinHeight));
            if (window.Screens.All.Any(s => s.Bounds.Contains(pixelRect)))
            {
                window.Position = pixelRect.Position;
                window.Width = pixelRect.Width;
                window.Height = pixelRect.Height;
            }
        }

        private static Rectangle GetBounds(Window window)
        {
            return new(window.Position.X, window.Position.Y, (int)window.Width, (int)window.Height);
        }
    }
}
