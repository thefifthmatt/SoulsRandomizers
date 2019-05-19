using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DS3StaticItemRandomizer
{
    static class Program
    {
        // https://stackoverflow.com/questions/7198639/c-sharp-application-both-gui-and-commandline
        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int dwProcessId);
        private const int ATTACH_PARENT_PROCESS = -1;

        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length > 0 && !args.Contains("/gui"))
            {
                // If given command line args, go into command line mode.
                RandomizerOptions options = new RandomizerOptions();
                uint seed = 0;
                int difficulty = -1;
                foreach (string arg in args)
                {
                    if (uint.TryParse(arg, out uint num))
                    {
                        if (difficulty == -1) difficulty = (int)num;
                        else seed = num;
                    }
                    else
                    {
                        options[arg] = true;
                    }
                }
                if (seed == 0)
                {
                    seed = (uint)new Random().Next();
                }
                options.Difficulty = difficulty;
                options.Seed = seed;
                string outPath = @"C:\Program Files (x86)\Steam\steamapps\common\DARK SOULS III\Game\randomizer";
                new Randomizer().Randomize(options, status => Console.WriteLine("## " + status), outPath);
                Application.Exit();
            }
            else
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }
    }
}
