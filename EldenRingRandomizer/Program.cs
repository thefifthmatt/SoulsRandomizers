using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using RandomizerCommon;
using static SoulsIds.GameSpec;

namespace EldenRingRandomizer
{
    static class Program
    {
        // https://stackoverflow.com/questions/7198639/c-sharp-application-both-gui-and-commandline
        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int dwProcessId);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();
        [DllImport("kernel32")]
        static extern bool FreeConsole();

        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length > 0 && !args.Contains("/gui"))
            {
                // If given command line args, go into command line mode.
                AttachConsole(-1);
                // AllocConsole();
                if (args.Contains("restore"))
                {
                    string restorePath = @"C:\Program Files (x86)\Steam\steamapps\common\ELDEN RING\Game";
                    GameData.RestoreBackupsInternal(restorePath);
                    return;
                }
                if (args.Contains("updatemessages"))
                {
                    Messages.CopyExplanations(args);
                    return;
                }
                RandomizerOptions options = RandomizerOptions.Parse(args, FromGame.ER);
                if (options.Seed == 0)
                {
                    options.Seed = (uint)new Random().Next();
                }
                Messages messages = new Messages("diste");
                Preset preset = null;
                if (options.Preset != null)
                {
                    preset = Preset.LoadPreset(options.Preset);
                }
                if (preset == null && File.Exists("DevER.txt"))
                {
                    options.Preset = "DevER";
                    preset = Preset.LoadPreset("DevER", checkDir: ".");
                }
                string gameExe = @"C:\Program Files (x86)\Steam\steamapps\common\ELDEN RING\Game\eldenring.exe";
                string outPath;
                if (options["uxm"])
                {
                    outPath = Path.GetDirectoryName(gameExe);
                }
                else
                {
                    outPath = @"C:\Users\matt\Downloads\Mods\ModEngine-2.0.0-preview3-win64\randomizer";
                }
                new Randomizer().Randomize(
                    options, FromGame.ER, status => Console.WriteLine("## " + status), outPath: outPath, preset: preset, messages: messages, gameExe: gameExe);
                Application.Exit();
            }
            else
            {
#if DEBUG
                AttachConsole(-1);
#endif
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new EldenForm(args.Contains("dumpmessages"), args.Contains("loadempty")));
            }
        }
    }
}
