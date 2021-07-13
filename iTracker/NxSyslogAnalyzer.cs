using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using iTracker.Command;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;

namespace iTracker
{
    public class NxSyslogAnalyzer
    {
        public static Logger Log = new Logger("NxSyslogAnalyzer");
        private List<string> _tempStorageOfLines = new List<string>(0);
        private bool _isCallbackClicked = false;
        private bool _isSketch = false;
        private bool _dlgBegin = false;
        private bool _dlgEnd = false;
        private string _timestamp = String.Empty;
        private bool _isAlertMessageCaptured = false;
        private List<string> _fullBlockStrings = new List<string>(0);
        private int _beginItemStartLineNo = 0;
        private int _beginItemEndLineNo = 0;
        private int _errorLineNo = 0;
        private int _intermediateLineNo = 0;
        private int _callBackLineNo = 0;
        FeatureCommand _iFeatureCommand = new FeatureCommand();

        public NxSyslogAnalyzer()
        {

        }

        public void AnalyzeSyslogAndWriteToDataBase(StreamReader iStreamReader)
        {
            try
            {
                Log.Info("Dlg Started....");
                GetFeatureCommandObject(iStreamReader);
                if (_dlgEnd)
                {
                    Log.Info("Dlg End....");
                    
                    WriteDataToDatabase();
                }
            }
            catch (Exception e)
            {
                Log.Error("Error while analyzing log",e);
            }
           
        }

        private void GetFeatureCommandObject(StreamReader iStreamReader)
        {
            string line;
            try
            {
                while ((line = iStreamReader.ReadLine()) != null)
                {
                    try
                    {
                        if (line.StartsWith("&MACRO ") ||
                            line.Contains("Used Dialog <Alert Dialog>") ||
                            line.Contains(Constant.SKETCH_START) ||
                            line.Contains(Constant.SKETCH_END) ||
                            line.Contains("TL_DEFAULT_ACTION") || //For edit Function
                            line.Contains("MODL_EDIT_PARAMETERS") || //For edit operation
                            line.Contains(Constant.SKETCH_END_1) ||
                            line.Contains(Constant.SKETCH_START_1))
                        {
                            if (line.Contains("TL_DEFAULT_ACTION") || line.Contains("MODL_EDIT_PARAMETERS"))
                            {
                                //It means it is in edit mode so we will add feature name and edit mode with the dialog begin
                                //&MACRO DIALOG_BEGIN "##25Block" 0 ! DA2  will become &MACRO DIALOG_BEGIN "##25Block" 0 ! DA2  ! [FEATURE NAME] [TIMESTAMP]
                                string after = line.After("!");
                                if (after != null)
                                {
                                    _timestamp = "      EDIT_MODE     [" + after.Between("(", ")").Trim() + "]";
                                }
                            }

                            if (line.Contains(Constant.DIALOG_BEGIN))
                            {
                                _dlgBegin = true;
                            }

                            if (line.Contains(Constant.DIALOG_PROFILE_START))
                            {
                                _dlgBegin = false;
                                continue;
                            }

                            if (line.Contains(Constant.DIALOG_PERSISTENT_BEGIN))
                            {
                                _dlgBegin = false;
                                continue;
                            }

                            if (line.Contains(Constant.DIALOG_PERSISTENT_END))
                            {
                                //We should not write this line
                                _dlgBegin = true;
                                continue;
                            }
                            //For Deactive recording of sketch

                            if (line.Contains(Constant.MENU) && line.Contains(Constant.SKETCH_START) ||
                                line.Contains(Constant.MENU) && line.Contains(Constant.SKETCH_START_1))
                            {
                                //_dlgBegin = false;
                                _isSketch = true;
                                continue;
                            }

                            if (line.Contains(Constant.SKETCH_END) || line.Contains(Constant.SKETCH_END_1))
                            {
                                //We should not write this line
                                _dlgBegin = false;
                                _isSketch = false;
                                continue;
                            }

                            if (!_dlgBegin || _isSketch)
                            {
                                continue;
                            }

                            //Check if alert window is triggered:

                            #region Alert Message Capture

                            if (!_isAlertMessageCaptured)
                            {
                                //_tempStorageOfLines.Add(line);
                                //Todo: Capture alert dlg messages by invoking windows functions
                                var findWindows = PInvokeMethods.FindWindows("NX_SURFACE_WND_DIALOG", "Alert Dialog");
                                if (findWindows != 0)
                                {
                                    IntPtr alertWindowHandle =
                                        PInvokeMethods.FindWindowHandle("NX_SURFACE_WND_DIALOG", "Alert Dialog");

                                    WindowHandleInfo alertDlg = new WindowHandleInfo(alertWindowHandle);
                                    List<IntPtr> allChildHandles = alertDlg.GetAllChildHandles();
                                    List<string> alertString = new List<string>(0);
                                    int alertCnt = 1;
                                    foreach (IntPtr allChildHandle in allChildHandles)
                                    {
                                        string text = PInvokeMethods.GetWindowText(allChildHandle);
                                        string className = PInvokeMethods.GetWindowClassName(allChildHandle);
                                        if (className.Equals("Static") && !String.IsNullOrEmpty(text) &&
                                            !text.Contains("IE Message Window"))
                                        {
                                            alertString.Add("[" + alertCnt + "] " + text + Environment.NewLine);
                                            alertCnt++;
                                        }
                                    }

                                    if (alertString.Any())
                                    {
                                        string @join = String.Join(",", alertString);
                                        _tempStorageOfLines.Add("ALERT_START" + Environment.NewLine + @join +
                                                                Environment.NewLine +
                                                                "ALERT_END");
                                        _isAlertMessageCaptured = true;
                                        alertString = new List<string>(0);
                                    }
                                }
                            }

                            #endregion

                            //End of alert
                            //On Alert
                            if (line.Contains("Used Dialog <Alert Dialog>"))
                            {
                                if (line.Contains("Bitmap: cancel_sc"))
                                {

                                }
                            }
                            else
                            {
                                if (line.Contains(Constant.OK_CALLBACK_CLICKED) ||
                                    line.Contains(Constant.APPLY_CALLBACK_CLICKED) ||
                                    line.Contains(Constant.CANCEL_CALLBACK_CLICKED))
                                {
                                    _isCallbackClicked = true;
                                    _tempStorageOfLines.Add(line);
                                }

                                if (line.Contains(Constant.DIALOG_BEGIN))
                                {
                                    _tempStorageOfLines.Add(line + _timestamp);
                                    //Once time stamp is appended to the dialog begin, we will reinitialize to empty
                                    _timestamp = String.Empty;
                                }

                                if (line.Contains(Constant.ON_ERROR_MSG_TEXT))
                                {
                                    _isAlertMessageCaptured = false;
                                }

                                if (line.Contains(Constant.BEG_ITEM) ||
                                    line.Contains(Constant.ASK_ITEM) ||
                                    line.Contains(Constant.END_ITEM) ||
                                    line.Contains(Constant.DIALOG_END) ||
                                    line.Contains(Constant.DIALOG_END_APPLY) ||
                                    line.Contains(Constant.ON_ERROR_MSG_TEXT) ||
                                    line.Contains(Constant.ON_ERROR_MSG_BOX))
                                {
                                    _tempStorageOfLines.Add(line);
                                    //FullBlockStrings.Add(line);
                                }

                                if (line.Contains(Constant.DIALOG_END_APPLY) ||
                                    line.Contains(Constant.DIALOG_END))
                                {
                                    //Add time Stamp at Dlgbegin
                                    if (_isCallbackClicked)
                                    {
                                        _fullBlockStrings.AddRange(_tempStorageOfLines);
                                        int count = _fullBlockStrings.Count - 1;
                                        //Add time Stamp at Dlgbegin
                                        string timeStampOfFeature = Nx.NxSession.GetTimeStampOfFeature();

                                        _fullBlockStrings[count] =
                                            _fullBlockStrings[count] + "   [" + timeStampOfFeature + "]";
                                        _tempStorageOfLines = new List<string>(0);
                                        _isCallbackClicked = false;
                                        _dlgBegin = false;
                                        _isSketch = false;
                                        _isAlertMessageCaptured = false;
                                    }
                                    else
                                    {
                                        _tempStorageOfLines = new List<string>(0);
                                        _isCallbackClicked = false;
                                        _dlgBegin = false;
                                        _isSketch = false;
                                        _isAlertMessageCaptured = false;
                                    }
                                }

                                //If dialog ends write to the database
                                if (line.Contains(Constant.DIALOG_END) &&
                                    !line.Contains(Constant.DIALOG_END_APPLY))
                                {
                                    _dlgEnd = true;

                                    //Set to default
                                    _dlgBegin = false;
                                    _isSketch = false;
                                    _tempStorageOfLines = new List<string>(0);
                                    _isCallbackClicked = false;
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error("Error while reading syslog file", e);
                        //Set to default
                        _dlgBegin = false;
                        _isSketch = false;
                        _tempStorageOfLines = new List<string>(0);
                        _isCallbackClicked = false;
                    }

                }

            }
            catch (Exception e)
            {
                Log.Error("Error while analysing the SysLog", e);
                //Set to default
                _dlgBegin = false;
                _isSketch = false;
                _tempStorageOfLines = new List<string>(0);
                _isCallbackClicked = false;
            }
        }

        private void WriteDataToDatabase()
        {

            //Testing: Comment at release 
            //System.IO.File.WriteAllLines(@"C:\Users\username\Desktop\log\collectedCommand.txt", FullBlockStrings);

            bool isOkCbClk = false;
            bool isApplyCbClk = false;
            bool isCancelCbClk = false;
            List<FeatureCommand> collectObjsForDb = new List<FeatureCommand>(0);
            try
            {
                Log.Info("Writing to database....");

                #region Start Writing to Database

                string featureCommandName = String.Empty;
                List<string> alertStrings = new List<string>(0);
                //List<FeatureCommand> collectObjsForDb = new List<FeatureCommand>(0);
                FeatureCommand i0FeatureCommand = new FeatureCommand();
                for (int i = 0; i < _fullBlockStrings.Count; i++)
                {
                    try
                    {
                        string blockString = _fullBlockStrings[i];
                        if (blockString.Contains(Constant.DIALOG_BEGIN) && blockString.Contains("DA2"))
                        {
                            var s = blockString.Split('"')[1];
                            var reqLine = s.Split('"')[0];
                            reqLine = reqLine.Replace('#', ' ');
                            reqLine =
                                Regex.Replace(reqLine, "[^a-zA-Z]", "");
                            //reqLine = blockString.Between("\"", "\"");
                            featureCommandName = reqLine.ToUpper();

                            //Time stamp related
                            //Get the time stamp edit mode from line
                            if (blockString.Contains("EDIT_MODE"))
                            {
                                i0FeatureCommand.IsEditMode = true;
                                i0FeatureCommand.TimeStamp = blockString.Between("[", "]").Trim();
                            }
                            else
                            {
                                i0FeatureCommand.IsEditMode = false;
                                i0FeatureCommand.TimeStamp =
                                    String.Empty; //DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss");
                            }

                            i0FeatureCommand.FeatureName = reqLine.ToUpper();
                            i0FeatureCommand.NxVersion = Nx.NxSession.GetNxVersion(); //"NX 12.0.0.1";
                            i0FeatureCommand.OsVersion = Nx.NxSession.GetWindowsOsVersion();
                            i0FeatureCommand.PartName = Nx.NxSession.GetDisplayPartNumber();
                            i0FeatureCommand.Department =
                                Nx.NxSession.GetStringAttributeValue("Department"); //Department
                            i0FeatureCommand.BusinessUnit =
                                Nx.NxSession.GetStringAttributeValue("Business-Unit"); //Business-Unit
                            i0FeatureCommand.ErrorStatus = 0;
                            i0FeatureCommand.ErrorMessage = String.Empty;

                            if (Constant.ListOfExtraAttributes.Any())
                            {
                                i0FeatureCommand.AttributesString =
                                    Nx.NxSession.GetStringAttributeValue(Constant.ListOfExtraAttributes);
                            }

                            i0FeatureCommand.TimeOfCreation = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss");
                            i0FeatureCommand.OsMachineType = TrackerMain.SysMachineType;
                            i0FeatureCommand.OsInstalledMemory = TrackerMain.SysInstalledMemory;

                            //Capture InitialSettings 
                            //After Dialog begin starts BEGIN_ITEM from i+1 line
                            _beginItemStartLineNo = i + 1;
                            i0FeatureCommand.InitialSettings =
                                CaptureInitialSettings(_fullBlockStrings, i + 1, out i);
                            _beginItemEndLineNo = i;
                            _intermediateLineNo = i;

                            i0FeatureCommand.ErrorSettings = String.Empty;
                            i0FeatureCommand.FinalSettings = String.Empty;
                        }
                        else if (blockString.Contains(Constant.OK_CALLBACK_CLICKED))
                        {
                            i0FeatureCommand.CallBackType = Constant.OK_CALLBACK_CLICKED;
                            isOkCbClk = true;
                            _callBackLineNo = i;
                            //i = FilterErrorMessages(i, i0FeatureCommand,out isOkCbClk);

                            #region FilterErrorMessages

                            //i++;
                            //Get all Error Msg box Text Between Two call backs
                            for (int j = i + 1; j < _fullBlockStrings.Count; j++)
                            {
                                string nextLineStr = _fullBlockStrings[j];
                                if (nextLineStr.Contains(Constant.DIALOG_END))
                                {
                                    i = j - 1;
                                    break;
                                }

                                if (nextLineStr.Contains(Constant.OK_CALLBACK_CLICKED) ||
                                    nextLineStr.Contains(Constant.APPLY_CALLBACK_CLICKED) ||
                                    nextLineStr.Contains(Constant.CANCEL_CALLBACK_CLICKED))
                                {
                                    i = j;
                                    break;
                                }

                                if (nextLineStr.Contains(Constant.ON_ERROR_MSG_BOX))
                                    //if (nextLineStr.Contains(Constant.ON_ERROR_MSG_BOX))
                                {
                                    //Capture Error Settings
                                    //Read The Settings between _IntermediateLineNo and _callBackLineNo
                                    _errorLineNo = j;
                                    i0FeatureCommand.CallBackType = "Error Occured";
                                    i0FeatureCommand.ErrorSettings =
                                        CaptureErrorSettings(_fullBlockStrings,
                                            _intermediateLineNo, _callBackLineNo, i0FeatureCommand.InitialSettings);
                                    _intermediateLineNo = j;


                                    var reqLine = nextLineStr.Replace("&MACRO MESSAGE_BOX  -2", " ");
                                    var reqLine2 = reqLine.Replace("&MACRO MESSAGE_BOX -2,", " ");
                                    i0FeatureCommand.ErrorMessage = i0FeatureCommand.ErrorMessage + reqLine2;
                                    i0FeatureCommand.ErrorStatus = 1;

                                    //Get all Error Msg box Text Between Two call backs
                                    //j++;
                                    for (int k = j + 1; k < _fullBlockStrings.Count; k++)
                                    {
                                        nextLineStr = _fullBlockStrings[k];
                                        if (nextLineStr.Contains(Constant.OK_CALLBACK_CLICKED) ||
                                            nextLineStr.Contains(Constant.APPLY_CALLBACK_CLICKED) ||
                                            nextLineStr.Contains(Constant.CANCEL_CALLBACK_CLICKED))
                                        {
                                            j = k;
                                            break;
                                        }
                                        else
                                        {
                                            if (nextLineStr.Contains(Constant.ON_ERROR_MSG_TEXT))
                                            {
                                                var reqMsgTxt = nextLineStr.Replace("&MACRO MESSAGE_TEXT", " ");
                                                i0FeatureCommand.ErrorMessage =
                                                    i0FeatureCommand.ErrorMessage + reqMsgTxt;
                                            }
                                        }
                                    }

                                    //Wanted the presnt line to be processed.
                                    //Hence made -1 so that if i is not incremented and present line is looped 
                                    i = j - 1;

                                    //On Error Immediately write to the database
                                    //TrackerMain.WriteToDatabase(i0FeatureCommand);
                                    //File.AppendAllText(@"C:/Users/Random 777/Desktop/log/data.txt", JsonConvert.SerializeObject(i0FeatureCommand) + Environment.NewLine);
                                    if (!String.IsNullOrEmpty(i0FeatureCommand.FeatureName))
                                    {
                                        i0FeatureCommand.CaptureSetting =
                                            new BsonArray
                                            {
                                                new BsonDocument
                                                {
                                                    {
                                                        "Initial Setting",
                                                        FormattedFinalSettings(i0FeatureCommand.InitialSettings)
                                                    },
                                                    {
                                                        "Error Setting",
                                                        FormattedFinalSettings(i0FeatureCommand.ErrorSettings)
                                                    }
                                                }
                                            };
                                        if (alertStrings.Any())
                                        {
                                            i0FeatureCommand.AlertMessage = String.Join(",", alertStrings);
                                        }

                                        collectObjsForDb.Add(i0FeatureCommand);
                                        alertStrings = new List<string>(0);
                                    }

                                    //Initiate The new Instance and fill the Feature data
                                    string featureName = i0FeatureCommand.FeatureName;
                                    string initialSettings = i0FeatureCommand.InitialSettings;
                                    bool isEditMode = i0FeatureCommand.IsEditMode;
                                    var timeStamp = isEditMode
                                        ? i0FeatureCommand.TimeStamp
                                        : String.Empty; //DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss");

                                    FeatureCommand newFeatureCommand = new FeatureCommand();
                                    i0FeatureCommand = newFeatureCommand;
                                    i0FeatureCommand.TimeStamp = timeStamp;
                                    i0FeatureCommand.FeatureName = featureName;
                                    i0FeatureCommand.NxVersion = Nx.NxSession.GetNxVersion(); //"NX 12.0.0.1";
                                    i0FeatureCommand.OsVersion = Nx.NxSession.GetWindowsOsVersion();
                                    i0FeatureCommand.PartName = Nx.NxSession.GetDisplayPartNumber();
                                    i0FeatureCommand.Department =
                                        Nx.NxSession.GetStringAttributeValue("Department"); //Department
                                    i0FeatureCommand.BusinessUnit =
                                        Nx.NxSession.GetStringAttributeValue("Business-Unit"); //Business-Unit
                                    i0FeatureCommand.ErrorStatus = 0;
                                    i0FeatureCommand.ErrorMessage = String.Empty;
                                    i0FeatureCommand.InitialSettings = initialSettings;
                                    i0FeatureCommand.ErrorSettings = String.Empty;
                                    i0FeatureCommand.FinalSettings = String.Empty;
                                    i0FeatureCommand.CallBackType = String.Empty;
                                    i0FeatureCommand.IsEditMode = isEditMode;
                                    if (Constant.ListOfExtraAttributes.Any())
                                    {
                                        i0FeatureCommand.AttributesString =
                                            Nx.NxSession.GetStringAttributeValue(Constant.ListOfExtraAttributes);
                                    }

                                    i0FeatureCommand.TimeOfCreation = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss");
                                    i0FeatureCommand.OsMachineType = TrackerMain.SysMachineType;
                                    i0FeatureCommand.OsInstalledMemory = TrackerMain.SysInstalledMemory;
                                    isOkCbClk = false;
                                    break;
                                }

                                if (nextLineStr.Contains(Constant.ALERT_START))
                                {
                                    string alertMessage = nextLineStr
                                        .Between(Constant.ALERT_START, Constant.ALERT_END).Trim();
                                    alertStrings.Add(alertMessage);
                                }
                            }

                            #endregion
                        }
                        else if (blockString.Contains(Constant.APPLY_CALLBACK_CLICKED))
                        {
                            i0FeatureCommand.CallBackType = Constant.APPLY_CALLBACK_CLICKED;
                            _callBackLineNo = i;
                            isApplyCbClk = true;
                            //i = FilterErrorMessages(i, i0FeatureCommand, out isApplyCbClk);

                            #region FilterErrorMessages

                            //i++;
                            //Get all Error Msg box Text Between Two call backs
                            for (int j = i + 1; j < _fullBlockStrings.Count; j++)
                            {
                                string nextLineStr = _fullBlockStrings[j];
                                if (nextLineStr.Contains(Constant.DIALOG_END))
                                {
                                    i = j - 1;
                                    break;
                                }

                                if (nextLineStr.Contains(Constant.OK_CALLBACK_CLICKED) ||
                                    nextLineStr.Contains(Constant.APPLY_CALLBACK_CLICKED) ||
                                    nextLineStr.Contains(Constant.CANCEL_CALLBACK_CLICKED))
                                {
                                    i = j;
                                    break;
                                }

                                //if (nextLineStr.IndexOf(Constant.ON_ERROR_MSG_BOX, StringComparison.CurrentCultureIgnoreCase) >= 0)
                                if (nextLineStr.Contains(Constant.ON_ERROR_MSG_BOX))
                                {
                                    _errorLineNo = j;
                                    i0FeatureCommand.CallBackType = "Error Occured";
                                    i0FeatureCommand.ErrorSettings =
                                        CaptureErrorSettings(_fullBlockStrings,
                                            _intermediateLineNo, _callBackLineNo, i0FeatureCommand.InitialSettings);
                                    _intermediateLineNo = j;

                                    var reqLine = nextLineStr.Replace("&MACRO MESSAGE_BOX  -2", " ");
                                    var reqLine2 = reqLine.Replace("&MACRO MESSAGE_BOX -2,", " ");
                                    i0FeatureCommand.ErrorMessage = i0FeatureCommand.ErrorMessage + reqLine2;
                                    i0FeatureCommand.ErrorStatus = 1;

                                    //Get all Error Msg box Text Between Two call backs
                                    //j++;
                                    for (int k = j + 1; k < _fullBlockStrings.Count; k++)
                                    {
                                        nextLineStr = _fullBlockStrings[k];
                                        if (nextLineStr.Contains(Constant.OK_CALLBACK_CLICKED) ||
                                            nextLineStr.Contains(Constant.APPLY_CALLBACK_CLICKED) ||
                                            nextLineStr.Contains(Constant.CANCEL_CALLBACK_CLICKED))
                                        {
                                            j = k;
                                            break;
                                        }
                                        else
                                        {
                                            if (nextLineStr.Contains(Constant.ON_ERROR_MSG_TEXT))
                                            {
                                                var reqMsgTxt = nextLineStr.Replace("&MACRO MESSAGE_TEXT", " ");
                                                i0FeatureCommand.ErrorMessage =
                                                    i0FeatureCommand.ErrorMessage + reqMsgTxt;
                                            }
                                        }
                                    }

                                    //Wanted the presnt line to be processed.
                                    //Hence made -1 so that if i is not incremented and present line is looped 
                                    i = j - 1;

                                    //On Error Immediately write to the database
                                    //TrackerMain.WriteToDatabase(i0FeatureCommand);
                                    //File.AppendAllText(@"C:/Users/Random 777/Desktop/log/data.txt", JsonConvert.SerializeObject(i0FeatureCommand) + Environment.NewLine);
                                    if (!String.IsNullOrEmpty(i0FeatureCommand.FeatureName))
                                    {

                                        i0FeatureCommand.CaptureSetting =
                                            new BsonArray
                                            {
                                                new BsonDocument
                                                {
                                                    {
                                                        "Initial Setting",
                                                        FormattedFinalSettings(i0FeatureCommand.InitialSettings)
                                                    },
                                                    {
                                                        "Error Setting",
                                                        FormattedFinalSettings(i0FeatureCommand.ErrorSettings)
                                                    }
                                                }
                                            };
                                        if (alertStrings.Any())
                                        {
                                            i0FeatureCommand.AlertMessage = String.Join(",", alertStrings);
                                        }

                                        collectObjsForDb.Add(i0FeatureCommand);
                                        alertStrings = new List<string>(0);
                                    }


                                    //Initiate The new Instance and fill the Feature data
                                    string featureName = i0FeatureCommand.FeatureName;
                                    string initialSettings = i0FeatureCommand.InitialSettings;
                                    bool isEditMode = i0FeatureCommand.IsEditMode;
                                    string timeStamp;
                                    timeStamp = isEditMode
                                        ? i0FeatureCommand.TimeStamp
                                        : String.Empty; //DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss");

                                    FeatureCommand newFeatureCommand = new FeatureCommand();
                                    i0FeatureCommand = newFeatureCommand;
                                    i0FeatureCommand.TimeStamp = timeStamp;
                                    i0FeatureCommand.FeatureName = featureName;
                                    i0FeatureCommand.NxVersion = Nx.NxSession.GetNxVersion(); //"NX 12.0.0.1";
                                    i0FeatureCommand.OsVersion = Nx.NxSession.GetWindowsOsVersion();
                                    i0FeatureCommand.PartName = Nx.NxSession.GetDisplayPartNumber();
                                    i0FeatureCommand.Department =
                                        Nx.NxSession.GetStringAttributeValue("Department"); //Department
                                    i0FeatureCommand.BusinessUnit =
                                        Nx.NxSession.GetStringAttributeValue("Business-Unit"); //Business-Unit
                                    i0FeatureCommand.ErrorStatus = 0;
                                    i0FeatureCommand.ErrorMessage = String.Empty;
                                    i0FeatureCommand.InitialSettings = initialSettings;
                                    i0FeatureCommand.ErrorSettings = String.Empty;
                                    i0FeatureCommand.FinalSettings = String.Empty;
                                    i0FeatureCommand.CallBackType = String.Empty;
                                    i0FeatureCommand.IsEditMode = isEditMode;
                                    if (Constant.ListOfExtraAttributes.Any())
                                    {
                                        i0FeatureCommand.AttributesString =
                                            Nx.NxSession.GetStringAttributeValue(Constant.ListOfExtraAttributes);
                                    }

                                    i0FeatureCommand.TimeOfCreation = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss");
                                    i0FeatureCommand.OsMachineType = TrackerMain.SysMachineType;
                                    i0FeatureCommand.OsInstalledMemory = TrackerMain.SysInstalledMemory;
                                    isApplyCbClk = false;
                                    break;
                                }

                                if (nextLineStr.Contains(Constant.ALERT_START))
                                {
                                    if (nextLineStr.Contains(Constant.ALERT_START))
                                    {
                                        string alertMessage = nextLineStr
                                            .Between(Constant.ALERT_START, Constant.ALERT_END).Trim();
                                        alertStrings.Add(alertMessage);
                                    }

                                }
                            }

                            #endregion
                        }
                        else if (blockString.Contains(Constant.CANCEL_CALLBACK_CLICKED))
                        {
                            isCancelCbClk = false;
                        }
                        else if (blockString.Contains(Constant.ALERT_START))
                        {
                            if (blockString.Contains(Constant.ALERT_START))
                            {
                                string alertMessage = blockString
                                    .Between(Constant.ALERT_START, Constant.ALERT_END).Trim();
                                alertStrings.Add(alertMessage);
                            }

                        }
                        else if (blockString.Contains(Constant.DIALOG_END))
                        {
                            _intermediateLineNo = i;
                            //On Error Immediately write to the database
                            if (isOkCbClk || isApplyCbClk || isCancelCbClk)
                            {
                                i0FeatureCommand.FinalSettings = CaptureFinalSettings(
                                    _fullBlockStrings, _callBackLineNo,
                                    _intermediateLineNo);
                                if (!i0FeatureCommand.IsEditMode)
                                {
                                    //i0FeatureCommand.TimeStamp = TrackerMain.GetTimeStampOfFeature();
                                    i0FeatureCommand.TimeStamp = blockString.Between("[", "]").Trim();
                                }

                                if (!String.IsNullOrEmpty(i0FeatureCommand.FeatureName))
                                {
                                    i0FeatureCommand.CaptureSetting =
                                        new BsonArray
                                        {
                                            new BsonDocument
                                            {
                                                {
                                                    "Initial Setting",
                                                    FormattedFinalSettings(i0FeatureCommand.InitialSettings)
                                                },
                                                {
                                                    "Final Setting",
                                                    FormattedFinalSettings(i0FeatureCommand.FinalSettings)
                                                }
                                            }
                                        };
                                    if (alertStrings.Any())
                                    {
                                        i0FeatureCommand.AlertMessage = String.Join(",", alertStrings);
                                    }

                                    collectObjsForDb.Add(i0FeatureCommand);
                                    alertStrings = new List<string>(0);
                                }

                                //File.AppendAllText(@"C:/Users/username/Desktop/log/data.txt", JsonConvert.SerializeObject(i0FeatureCommand) + Environment.NewLine);
                                //TrackerMain.WriteToDatabase(i0FeatureCommand);

                                FeatureCommand newFeatureCommand = new FeatureCommand();
                                i0FeatureCommand = newFeatureCommand;
                            }

                            isOkCbClk = false;
                            isApplyCbClk = false;
                            isCancelCbClk = false;
                            _beginItemStartLineNo = 0;
                            _beginItemEndLineNo = 0;
                            _callBackLineNo = 0;
                            _errorLineNo = 0;
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error("Error while writing to the database", e);
                    }

                }

                //Write collection of objects to MongoDb
                if (collectObjsForDb.Any())
                {
                    WriteToDatabase(collectObjsForDb);
                }

                Log.Info("Writing to database completed....");

                #endregion
            }
            catch (Exception e)
            {
                Log.Error("Error while writing to database...", e);
            }


            //Clear the block strings
            collectObjsForDb = new List<FeatureCommand>(0);
            _fullBlockStrings = new List<string>(0);
            _tempStorageOfLines = new List<string>(0);
            _isCallbackClicked = false;
            _dlgEnd = false;
        }

        private string FormattedFinalSettings(string iJsonSettingString)
        {
            Dictionary<string, string> settingDictinary = JsonConvert.DeserializeObject<Dictionary<string, string>>(iJsonSettingString);
            List<string> valueStringList = settingDictinary.Values.ToList();
            return JsonConvert.SerializeObject(valueStringList, Formatting.Indented);
        }

        private void WriteToDatabase(List<FeatureCommand> iFeatureCommandObjs)
        {
            try
            {
                TrackerMain.MongoDbCollection.InsertMany(iFeatureCommandObjs, new InsertManyOptions() { IsOrdered = true });
            }
            catch (Exception e)
            {
                Log.Error("Error in writing to database", e);
            }

        }

        public static string CaptureInitialSettings(List<string> iFullBlockStrings, int iBeginLineCount, out int oEndLineCount)
        {
            oEndLineCount = 0;
            Dictionary<string, string> settingValues = new Dictionary<string, string>(0);
            for (int i = iBeginLineCount; i < iFullBlockStrings.Count; i++)
            {
                string line = iFullBlockStrings[i];
                if (line.Contains(Constant.ASK_ITEM))
                {
                    oEndLineCount = i;
                    break;
                }

                if (line.Contains(Constant.BEG_ITEM))
                {
                    //&MACRO BEG_ITEM 262144 (1 BOOL 0) = 0  ! ##154Edge
                    //Here key = 262144
                    //Trim removes white spaces before and after the string
                    string key = line.Between(Constant.BEG_ITEM, "(").Trim();

                    line = line.After(Constant.BEG_ITEM).Trim();

                    settingValues = FillSettingValues(key, line, settingValues);
                }
            }
            var initialSettings = JsonConvert.SerializeObject(settingValues, Formatting.Indented);

            return initialSettings;
        }

        public static string CaptureErrorSettings(List<string> iFullBlockStrings, int iStartLineCount, int iEndLineCount,
            string initialSettingsJson)
        {
            Dictionary<string, string> settingValues = JsonConvert.DeserializeObject<Dictionary<string, string>>(initialSettingsJson);
            for (int i = iStartLineCount; i < iEndLineCount; i++)
            {
                string line = iFullBlockStrings[i];
                if (line.Contains(Constant.ASK_ITEM))
                {
                    //&MACRO BEG_ITEM 262144 (1 BOOL 0) = 0  ! ##154Edge
                    //Here key = 262144
                    //Trim removes white spaces before and after the string
                    string key = line.Between(Constant.ASK_ITEM, "(").Trim();

                    line = line.After(Constant.ASK_ITEM).Trim();

                    settingValues = FillSettingValues(key, line, settingValues);
                }
            }
            string errorSettings = JsonConvert.SerializeObject(settingValues, Formatting.Indented);
            return errorSettings;
        }

        public static string CaptureFinalSettings(List<string> iFullBlockStrings, int iStartLineCount, int iEndLineCount)
        {
            //Chamfer finalSettingsObj = new Chamfer();
            Dictionary<string, string> settingValues = new Dictionary<string, string>(0);
            for (int i = iStartLineCount; i < iEndLineCount; i++)
            {
                string line = iFullBlockStrings[i];
                if (line.Contains(Constant.END_ITEM))
                {
                    //&MACRO BEG_ITEM 262144 (1 BOOL 0) = 0  ! ##154Edge
                    //Here key = 262144
                    //Trim removes white spaces before and after the string
                    string key = line.Between(Constant.END_ITEM, "(").Trim();

                    line = line.After(Constant.END_ITEM).Trim();

                    settingValues = FillSettingValues(key, line, settingValues);
                }
            }
            string finalSettings = JsonConvert.SerializeObject(settingValues, Formatting.Indented);
            return finalSettings;
        }

        private static Dictionary<string, string> FillSettingValues(string iKey, string iLine,
            Dictionary<string, string> iSettingValues)
        {
            Dictionary<string, string> settingValues = iSettingValues;

            string iValue = String.Empty;
            if (settingValues.ContainsKey(iKey))
            {
                iValue = settingValues[iKey];
            }

            string iValuePart1 = iLine.Between("=", "!").Trim();
            string iValuePart2 = " [" + iLine.After("!").Trim() + "]";
            string iValuePart3 = "[" + iLine.ExtractOnlyStringsBetween("(", ")").Trim() + "]";

            iValue = iValuePart1 + iValuePart2 + iValuePart3;

            if (settingValues.ContainsKey(iKey))
            {
                settingValues[iKey] = iValue;
            }
            else
            {
                settingValues.Add(iKey, iValue);
            }

            return settingValues;
        }
    }
}
