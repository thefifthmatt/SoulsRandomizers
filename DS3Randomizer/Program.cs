using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using RandomizerCommon;
using static SoulsIds.GameSpec;

namespace DS3Randomizer
{
    static class Program
    {
        // https://stackoverflow.com/questions/7198639/c-sharp-application-both-gui-and-commandline
        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int dwProcessId);
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
                RandomizerOptions options = RandomizerOptions.Parse(args, FromGame.DS3);
                if (options.Seed == 0)
                {
                    options.Seed = (uint)new Random().Next();
                }
                Preset preset = null;
                if (options.Preset != null)
                {
                    preset = Preset.LoadPreset(options.Preset, extractOopsAll: true, checkDir: "presets3");
                }
                if (preset == null && File.Exists("Dev3.txt"))
                {
                    options.Preset = "Dev3";
                    preset = Preset.LoadPreset("Dev3", checkDir: ".");
                }
                string outPath = @"C:\Program Files (x86)\Steam\steamapps\common\DARK SOULS III\Game\randomizer";
                new Randomizer().Randomize(
                    options, FromGame.DS3, status => Console.WriteLine("## " + status), outPath, preset);
                Application.Exit();
            }
            else
            {
#if DEBUG
                AttachConsole(-1);
#endif
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }
    }
}
