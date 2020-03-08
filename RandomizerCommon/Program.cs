using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RandomizerCommon
{
    // TODO: Fork this into SekiroRandomizer and DS3Randomizer
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
                bool sekiro = true;
                RandomizerOptions options = RandomizerOptions.Parse(args, sekiro);
                if (options.Seed == 0)
                {
                    options.Seed = (uint)new Random().Next();
                }
                string outPath = sekiro
                    ? @"C:\Program Files (x86)\Steam\steamapps\common\Sekiro\randomizer"
                    : @"C:\Program Files (x86)\Steam\steamapps\common\DARK SOULS III\Game\randomizer";
                new Randomizer().Randomize(options, status => Console.WriteLine("## " + status), outPath, sekiro);
                Application.Exit();
            }
            else
            {
#if DEBUG
                AttachConsole(-1);
#endif
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                // Application.Run(new MainForm());
                Application.Run(new SekiroForm());
            }
        }
    }
}
