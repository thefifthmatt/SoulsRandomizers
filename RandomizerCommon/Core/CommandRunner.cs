using System.Collections.Generic;
using System.IO;
using static RandomizerCommon.LocationData;
using static SoulsIds.GameSpec;

namespace RandomizerCommon
{
    public static class CommandRunner
    {
        public static bool Run(IList<string> args, FromGame game)
        {
            // Common routines on startup
#if DEBUG
            MiscSetup.SetWorkingDirectoryForDevelopment();
#endif

            if (args == null || args.Count == 0) return false;

#if DEV
            if (DevRunner.Run(args, game)) return true;
#endif

            // In the future this could look at randomizeopt files.
            // In that case it won't return a boolean, but data needed to initialize the randomizer.
            return false;
        }
    }
}
