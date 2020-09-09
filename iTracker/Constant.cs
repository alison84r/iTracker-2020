using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iTracker
{
    public static class Constant
    {
        public const string ALERT_MSG = "ALERT";
        public const string OK_CALLBACK_CLICKED = "OK Callback";
        public const string APPLY_CALLBACK_CLICKED = "Apply Callback";
        public const string CANCEL_CALLBACK_CLICKED = "Cancel Callback";
        public const string ON_ERROR_MSG_BOX = "MESSAGE_BOX";
        public const string ON_ERROR_MSG_TEXT = "MESSAGE_TEXT";
        public const string DIALOG_BEGIN = "DIALOG_BEGIN";
        public const string DIALOG_END = "DIALOG_END";
        public const string DIALOG_END_1 = "DIALOG_END"; //This is added for Sketch
        public const string DIALOG_END_OK = "OK";
        public const string DIALOG_END_APPLY = "mtEndEventLoop";
        public const string DIALOG_END_CANCEL = "CANCEL";
        public const string BEG_ITEM = "BEG_ITEM";
        public const string ASK_ITEM = "ASK_ITEM";
        public const string END_ITEM = "END_ITEM";
        public const string DIALOG_PERSISTENT_BEGIN = "Persistent Dialog";
        public const string DIALOG_PERSISTENT_END = "DIALOG_PERSISTENT_END";
        public const string DIALOG_PROFILE_START = "DIALOG_BEGIN \"Profile\"";

        public const string MENU = "MENU";
        public const string SKETCH_START = "SKETCH";
        public const string SKETCH_START_1 = "Entry: sketch task environment";
        public const string SKETCH_END = "Deactivate sketch";
        public const string SKETCH_END_1 = "Exit: sketch task environment";

        public const string ALERT_START = "ALERT_START";
        public const string ALERT_END = "ALERT_END";


        //Attributes Used To Collect data
        public const string EXTRA_ATTRIBUTE_FILE_NAME = "AttributeNames.config";
        public static List<string> ListOfExtraAttributes;

        static Constant()
        {
            ListOfExtraAttributes = new List<string>(0);
        }
    }
}
