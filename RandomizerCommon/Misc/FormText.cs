using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RandomizerCommon.Messages;

namespace RandomizerCommon
{
    public class FormText
    {
        [Localize]
        public static readonly Text ConfirmTitleText = new Text("Confirm", "PresetEditForm_confirmTitle");
        [Localize]
        public static readonly Text ErrorTitleText = new Text("Error", "PresetEditForm_errorTitle");
        [Localize]
        public static readonly Text FileMissingText = new Text("File does not exist: {0}", "FormText_fileMissing");
        [Localize]
        public static readonly Text LoadFailureText = new Text("Failed to load preset {0}\n{1}", "PresetEditForm_loadFailure");
        [Localize]
        public static readonly Text SaveFailureText = new Text("Failed to save preset {0}\n{1}", "PresetEditForm_saveFailure");
        [Localize]
        public static readonly Text ConfirmOverwriteText = new Text("Overwrite existing file?", "PresetEditForm_confirmOverwrite");
        [Localize]
        public static readonly Text ConfirmLeaveText = new Text("Save changes before exiting?", "PresetEditForm_confirmLeave");
        [Localize]
        public static readonly Text ConfirmResetPresetText = new Text("Reset this preset to default?", "PresetEditForm_confirmReset");
        [Localize]
        public static readonly Text ConfirmResetText = new Text("Reset to default?", "FormText_confirmReset");
    }
}
