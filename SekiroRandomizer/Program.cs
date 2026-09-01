using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using RandomizerCommon;
using static SoulsIds.GameSpec;

namespace SekiroRandomizer
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (CommandRunner.Run(args, FromGame.SDT))
            {
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SekiroForm());
        }
    }
}
