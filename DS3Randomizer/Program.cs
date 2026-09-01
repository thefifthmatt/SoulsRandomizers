using System;
using System.Windows.Forms;
using RandomizerCommon;
using static SoulsIds.GameSpec;

namespace DS3Randomizer
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            CharacterWriter.MeasureText = (s, f) => TextRenderer.MeasureText(s, f).Width;
            if (CommandRunner.Run(args, FromGame.DS3))
            {
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
