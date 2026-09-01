using System;
using System.Linq;
using System.Windows.Forms;
using RandomizerCommon;
using static SoulsIds.GameSpec;

namespace EldenRingRandomizer
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            CharacterWriter.MeasureText = (s, f) => TextRenderer.MeasureText(s, f).Width;
            if (CommandRunner.Run(args, FromGame.ER))
            {
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new EldenForm(args.Contains("dumpmessages"), args.Contains("loadempty")));
        }
    }
}
