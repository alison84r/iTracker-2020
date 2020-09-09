using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using iTracker.Command;
using iTracker.Properties;
using MongoDB.Driver;

namespace iTracker
{
    public class TrackerMain
    {
        // class members
        public static TrackerMain TheTrackerProgram;
        public static bool IsDisposeCalled;

        //File Tracker properties
        private readonly ObservableCollection<FileMonitorViewModel> _fileMonitors = new ObservableCollection<FileMonitorViewModel>();
        private Timer _refreshTimer;
        private string _font;
        private DateTime? _lastUpdateDateTime;
        private string _lastUpdated;
        private FileMonitorViewModel _selectedItem;
        private FileMonitorViewModel _lastUpdatedViewModel;

        //MongoDb
        private MongoClient _dbClient;
        private IMongoDatabase _featureCommandDb;
        public static IMongoCollection<FeatureCommand> MongoDbCollection;

        public static string SysMachineType = string.Empty;
        public static string SysInstalledMemory = string.Empty;

        //General
        public static string ExecutingAssemblyPath;
        public static string ConfigDirectoryPath;

        //Log
        public static Logger Log;

        //------------------------------------------------------------------------------
        // Constructor
        //------------------------------------------------------------------------------
        public TrackerMain()
        {
            try
            {
                IsDisposeCalled = false;
            }
            catch (NXOpen.NXException ex)
            {
                // ---- Enter your exception handling code here -----
                // UI.GetUI().NXMessageBox.Show("Message", NXMessageBox.DialogType.Error, ex.Message);
            }
        }

        //------------------------------------------------------------------------------
        //  NX Startup
        //      This entry point activates the application at NX startup

        //Will work when complete path of the dll is provided to Environment Variable 
        //USER_STARTUP or USER_DEFAULT

        //OR

        //Will also work if dll is at folder named "startup" under any folder listed in the 
        //text file pointed to by the environment variable UGII_CUSTOM_DIRECTORY_FILE.
        //------------------------------------------------------------------------------
        //public static int Main() //Do Not Delete this line
        public static int Startup()
        {
            int retValue = 0;
            //Initialize Log File Settings
            string logFilePath = InitialSettings();
            var targetLogFile = new FileInfo(logFilePath);
            Logger.Start(targetLogFile);
            Log = new Logger("iTrackerLog");
            try
            {
                Log.Info("iTracker Started");

                TheTrackerProgram = new TrackerMain();

                //Initialize MongoDb Settings
                bool isMongoDbConnection = InitialMongoDbSettings();
                if (!isMongoDbConnection)
                {
                    return -1;
                }

                //Initialize Extra attribute list
                bool initializeExtraAttributes = InitializeExtraAttributes();
                if (!initializeExtraAttributes)
                {
                    return - 2;
                }
                SysMachineType = Nx.NxSession.GetSystemMachineType();
                SysInstalledMemory = Nx.NxSession.GetSystemMemory();

                string sysLogFileName = Nx.NxSession.GetLogFileOfCurrentSession();
                TheTrackerProgram.AddFileMonitor(sysLogFileName);
            }
            catch (Exception e)
            {
                Log.Error("Exception in ITracker", e);
            }
            finally
            {
                Logger.ShutDown(); // Removing this line may result in lost log entries.
            }
            return retValue;
        }

        //------------------------------------------------------------------------------
        // Following method disposes all the class members
        //------------------------------------------------------------------------------
        public void Dispose()
        {
            try
            {
                if (IsDisposeCalled == false)
                {
                    //TODO: Add your application code here 
                }
                IsDisposeCalled = true;
            }
            catch (NXOpen.NXException ex)
            {
                // ---- Enter your exception handling code here -----

            }
        }

        public static int GetUnloadOption(string arg)
        {
            //return Nx.NxSession.UnloadExplicitly();
            return Nx.NxSession.UnloadAtTermination();
        }

        #region Initial Settings

        /// <summary>
        /// Initial Settings. Setting log file path
        /// </summary>
        /// <returns></returns>
        public static string InitialSettings()
        {
            ExecutingAssemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            ConfigDirectoryPath = Path.Combine(ExecutingAssemblyPath + @"/Config/");

            //1.Initialize the log file
            DateTime datetime = DateTime.Now;
            string uniqueId = String.Format("{0:0000}{1:00}{2:00}{3:00}{4:00}{5:00}{6:000}",
                datetime.Year, datetime.Month, datetime.Day,
                datetime.Hour, datetime.Minute, datetime.Second, datetime.Millisecond);
            string appLogDir = Path.GetTempPath();
            string logFilePath = Path.Combine(appLogDir, uniqueId + "_iTracker.log");
            return logFilePath;
        }

        /// <summary>
        /// Setting the attribute Names by reading from AttributeNames.config file
        /// </summary>
        /// <returns></returns>
        public static bool InitializeExtraAttributes()
        {
            string extraAttributeConfigFileName = "AttributeNames.config";
            string attributeFile = Path.Combine(ConfigDirectoryPath + extraAttributeConfigFileName);
            try
            {
                Constant.ListOfExtraAttributes = File.ReadAllLines(attributeFile).ToList();
            }
            catch (Exception e)
            {
                Log.Error("Error In Reading  Extra Attributes Config file", e);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Setting the Mongo Db names
        /// </summary>
        /// <returns></returns>
        public static bool InitialMongoDbSettings()
        {
            
            #region MongoDbClient

            string serverConfigFileName = "server.config";
            string serverConfigFile = Path.Combine(ConfigDirectoryPath + serverConfigFileName);

            var connectionString = File.ReadLines(serverConfigFile).First(); //Server address
            var dataBaseName = File.ReadLines(serverConfigFile).ElementAtOrDefault(1); //Database name
            var collectionName = File.ReadLines(serverConfigFile).ElementAtOrDefault(2); //Collection Name

            if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(dataBaseName) ||
                string.IsNullOrEmpty(collectionName))
            {
                Log.Error("Error In Server.Config");
                return false;
            }
            //Read the Server address from server.config folder
            try
            {
                TheTrackerProgram._dbClient = new MongoClient(connectionString);
                TheTrackerProgram._featureCommandDb = TheTrackerProgram._dbClient.GetDatabase(dataBaseName); //Feature Common Data
                MongoDbCollection = TheTrackerProgram._featureCommandDb.GetCollection<FeatureCommand>(collectionName);
                Log.Info("Connected to MongoDb Database successfully");
            }
            catch (Exception e)
            {
                Log.Error("Error In connecting to MongoDb database",e);
                return false;
            }

            return true;

            #endregion
        }
        #endregion

        #region Watching Syslog for Changes : Do not Edit
        public string LastUpdated
        {
            get { return _lastUpdated; }
            set
            {
                if (value == _lastUpdated) return;
                _lastUpdated = value;
                OnPropertyChanged();
            }
        }

        public FileMonitorViewModel SelectedItem
        {
            get { return _selectedItem; }
            set
            {
                if (Equals(value, _selectedItem)) return;
                _selectedItem = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<FileMonitorViewModel> FileMonitors
        {
            get { return _fileMonitors; }
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
        
        private void AddFileMonitor(string filepath)
        {
            var existingMonitor = FileMonitors.FirstOrDefault(m => string.Equals(m.FilePath, filepath, StringComparison.CurrentCultureIgnoreCase));

            if (existingMonitor != null)
            {
                // Already being monitored
                SelectedItem = existingMonitor;
                return;
            }
            var monitorViewModel = new FileMonitorViewModel(filepath, "Tracking", Settings.Default.DefaultEncoding, Settings.Default.BufferedRead);
            //var monitorViewModel = new FileMonitorViewModel(filepath, GetFileNameForPath(filepath), Settings.Default.DefaultEncoding, Settings.Default.BufferedRead);
            monitorViewModel.Renamed += MonitorViewModelOnRenamed;
            monitorViewModel.Updated += MonitorViewModelOnUpdated;

            FileMonitors.Add(monitorViewModel);
            SelectedItem = monitorViewModel;
        }

        private void MonitorViewModelOnUpdated(FileMonitorViewModel obj)
        {
            _lastUpdateDateTime = DateTime.Now;
            string objContents = obj.Contents;
            _lastUpdatedViewModel = obj;
            RefreshLastUpdatedText();
        }

        private void MonitorViewModelOnRenamed(FileMonitorViewModel renamedViewModel)
        {
            var filepath = renamedViewModel.FilePath;

            renamedViewModel.FileName = GetFileNameForPath(filepath);
        }

        private static string GetFileNameForPath(string filepath)
        {
            return Path.GetFileName(filepath);
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
        private void RefreshLastUpdatedText()
        {
            if (_lastUpdateDateTime != null)
            {
                var dateTime = _lastUpdateDateTime.Value;
                var datestring = dateTime.Date != DateTime.Now.Date ? " on " + dateTime : " at " + dateTime.ToLongTimeString();
                LastUpdated = datestring; //Edited
                //LastUpdated = _lastUpdatedViewModel.FilePath + datestring;
            }
        }
        #endregion

    }
}
