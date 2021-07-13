using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace iTracker
{
    public sealed class Logger
    {
        #region Log File Writing
        public static bool Listening { get; private set; }
        public static FileInfo TargetLogFile { get; private set; }
        public static DirectoryInfo TargetDirectory { get { return TargetLogFile.Directory; } }

        public static bool LogToConsole = false;
        public static int BatchInterval = 1000;
        public static bool IgnoreDebug = false;

        private static Timer Timer = new Timer(Tick);
        private static readonly StringBuilder LogQueue = new StringBuilder();

        public static void Start(FileInfo targetLogFile)
        {
            if (Listening)
                return;

            Listening = true;
            TargetLogFile = targetLogFile;
            VerifyTargetDirectory();
            if (Timer == null)
            {
                Timer = new Timer(Tick, null, 0, (int)TimeSpan.FromHours(2).TotalMilliseconds);
            }
            Timer.Change(BatchInterval, Timeout.Infinite); // A one-off tick event that is reset every time.
        }

        private static void VerifyTargetDirectory()
        {
            if (TargetDirectory == null)
                throw new DirectoryNotFoundException("Target logging directory not found.");

            TargetDirectory.Refresh();
            if (!TargetDirectory.Exists)
                TargetDirectory.Create();
        }

        private static void Tick(object state)
        {
            try
            {
                var logMessage = "";
                lock (LogQueue)
                {
                    logMessage = LogQueue.ToString();
                    LogQueue.Length = 0;
                }

                if (string.IsNullOrEmpty(logMessage))
                    return;

                if (LogToConsole)
                    Console.Write(logMessage);

                VerifyTargetDirectory(); // File may be deleted after initialization.
                File.AppendAllText(TargetLogFile.FullName, logMessage);
            }
            finally
            {
                if (Listening)
                {
                    if (Timer == null)
                    {
                        Timer = new Timer(Tick, null, 0, (int)TimeSpan.FromHours(2).TotalMilliseconds);
                    }
                    Timer.Change(BatchInterval, Timeout.Infinite); // Reset timer for next tick.
                }
            }
        }

        public static void ShutDown()
        {
            if (!Listening)
                return;

            Listening = false;
            Timer.Dispose();
            Timer = null;
            Tick(null); // Flush.
        }
        #endregion

        public readonly string Name;
        public EventHandler<LogMessageInfo> LogMessageAdded;
        private bool _startedErrorShown = false;

        public const string DEBUG = "[DEBUG1]";
        public const string INFO = "[INFO]";
        public const string WARN = "[WARN]";
        public const string ERROR = "[ERROR]";

        public Logger(Type t) : this(t.Name)
        {
        }

        public Logger(string name)
        {
            Name = name;
        }

        public void Debug(string message)
        {
            if (IgnoreDebug)
                return;

            Log(DEBUG, message);
        }

        public void Info(string message)
        {
            Log(INFO, message);
        }

        public void Warn(string message, Exception ex = null)
        {
            Log(WARN, message, ex);
        }

        public void Error(string message, Exception ex = null)
        {
            if (ex != null)
            {
                Log(ERROR, message, ex);
            }
            else
            {
                Log(ERROR, message);
            }

        }

        public void Log(string level, string message, Exception ex = null)
        {
            if (!CheckListening())
                return;

            if (ex != null)
            {
                //message += string.Format("\r\n{0}\r\n{1}", ex.Message, ex.StackTrace);
                message = message + Environment.NewLine + GetErrorMessageDetails(ex);
            }

            var info = new LogMessageInfo(level, Name, message);
            var msg = info.ToString();

            lock (LogQueue)
            {
                LogQueue.AppendLine(msg);
            }

            var evnt = LogMessageAdded;
            if (evnt != null)
                evnt.Invoke(this, info); // Block caller.
        }
        /// <summary>
        /// Logs exception information to the assigned log file.
        /// </summary>
        /// <param name="exception">Exception to log.</param>
        public string GetErrorMessageDetails(Exception exception)
        {
            List<string> errorString = new List<string>(0);
            errorString.Add("------------------------------------------------------------------------------");
            errorString.Add("Exception Information");
            errorString.Add("------------------------------------------------------------------------------");
            errorString.Add("Time         : " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
            errorString.Add("Run Time     : " + (DateTime.Now - Process.GetCurrentProcess().StartTime).ToString());
            errorString.Add("Source       : " + exception.Source.ToString().Trim());
            errorString.Add("Method       : " + exception.TargetSite.Name.ToString());
            errorString.Add("Type         : " + exception.GetType().ToString());
            errorString.Add("Error        : " + GetExceptionStack(exception));
            errorString.Add("------------------------------------------------------------------------------");
            errorString.Add("Stack Trace");
            var st = new StackTrace(exception, true);
            StackFrame[] stackFrames = st.GetFrames();
            if (stackFrames != null)
            {
                List<StackFrame> frames = stackFrames.ToList();
                foreach (var frame in frames)
                {
                    if (frame.GetFileLineNumber() < 1)
                        continue;
                    errorString.Add("File: " + Path.GetFileName(frame.GetFileName()) + " | " + "Method: " +
                                    frame.GetMethod().Name + " | " + "LineNumber: " + frame.GetFileLineNumber());
                }
            }
            errorString.Add("------------------------------------------------------------------------------");
            string @join = string.Join(Environment.NewLine, errorString.ToArray());
            return @join;
        }
        private string GetExceptionStack(Exception e)
        {
            StringBuilder message = new StringBuilder();
            message.Append(e.Message);
            while (e.InnerException != null)
            {
                e = e.InnerException;
                message.Append(Environment.NewLine);
                message.Append(e.Message);
            }
            return message.ToString();
        }
        private bool CheckListening()
        {
            if (Listening)
                return true;

            if (!_startedErrorShown)
            {
                Console.WriteLine(@"Logging has not been started.");
                _startedErrorShown = true; // No need to excessively repeat this message.
            }

            return false;
        }
    }

    public sealed class LogMessageInfo : EventArgs
    {
        public readonly DateTime Timestamp;
        public readonly string ThreadId;
        public readonly string Level;
        public readonly string Logger;
        public readonly string Message;


        public bool IsError { get { return iTracker.Logger.ERROR.Equals(Level, StringComparison.Ordinal); } }
        public bool IsWarning { get { return iTracker.Logger.WARN.Equals(Level, StringComparison.Ordinal); } }
        public bool IsInformation { get { return iTracker.Logger.INFO.Equals(Level, StringComparison.Ordinal); } }
        public bool IsDebug { get { return iTracker.Logger.DEBUG.Equals(Level, StringComparison.Ordinal); } }

        public LogMessageInfo(string level, string logger, string message)
        {
            Timestamp = DateTime.Now;
            var thread = Thread.CurrentThread;
            ThreadId = string.IsNullOrEmpty(thread.Name) ? thread.ManagedThreadId.ToString() : thread.Name;
            Level = level;
            Logger = logger;
            Message = message;
        }

        public override string ToString()
        {
            //"yyyy-MM-dd HH:mm:ss"
            return string.Format("{0:yyyy-MM-dd HH:mm:ss} {1} {2} {3} {4}",
                Timestamp, ThreadId, Logger, Level, Message);
        }
    }
}
