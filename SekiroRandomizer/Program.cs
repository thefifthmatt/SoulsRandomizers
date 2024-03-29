﻿using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using RandomizerCommon;
using static SoulsIds.GameSpec;

namespace SekiroRandomizer
{
    static class Program
    {
        // https://stackoverflow.com/questions/7198639/c-sharp-application-both-gui-and-commandline
        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int dwProcessId);
        private const int ATTACH_PARENT_PROCESS = -1;
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length > 0 && !args.Contains("/gui"))
            {
                // If given command line args, go into command line mode.
                AttachConsole(-1);
                RandomizerOptions options = RandomizerOptions.Parse(args, FromGame.SDT);
                if (options.Seed == 0)
                {
                    options.Seed = (uint)new Random().Next();
                }
                Preset preset = null;
                if (options.Preset != null)
                {
                    preset = Preset.LoadPreset(options.Preset, extractOopsAll: true);
                }
                if (preset == null && File.Exists("Dev.txt"))
                {
                    options.Preset = "Dev";
                    preset = Preset.LoadPreset("Dev", checkDir: ".");
                }
                string outPath = @"C:\Program Files (x86)\Steam\steamapps\common\Sekiro\randomizer";
                new Randomizer().Randomize(
                    options, FromGame.SDT, status => Console.WriteLine("## " + status), outPath, preset);
                Application.Exit();
            }
            else
            {
#if DEBUG
                AttachConsole(-1);
#endif
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new SekiroForm());
            }
        }
    }
}
