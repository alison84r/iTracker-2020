using NXOpen;
using NXOpen.Features;
using NXOpen.UF;
using System;
using System.Collections.Generic;
using System.Linq;

namespace iTracker
{
    public class Nx
    {
        public static Logger Log = new Logger("NX_Methods");
        private static Nx _singleInstanceNx;

        private static Session _theSession;
        private static UFSession _theUfSession;

        private Nx()
        {
            _theSession = Session.GetSession();
            _theUfSession = UFSession.GetUFSession();
        }

        public static Nx NxSession => _singleInstanceNx ?? (_singleInstanceNx = new Nx());

        /// <summary>
        /// Getting System OS Name
        /// </summary>
        /// <returns></returns>
        public string GetSystemOsName()
        {
            try
            {
                _theUfSession.UF.AskSystemInfo(out var sysInfo);
                return sysInfo.os_name;
            }
            catch (Exception e)
            {
                Log.Error("Error in getting system Os Name",e);
            }

            return string.Empty;

        }

        /// <summary>
        /// Get System Os Version
        /// </summary>
        /// <returns></returns>
        public string GetSystemOsVersion()
        {
            try
            {
                _theUfSession.UF.AskSystemInfo(out var sysInfo);
                return sysInfo.os_version;
            }
            catch (Exception e)
            {
                Log.Error("Error in getting System OS version",e);
            }
            return string.Empty;
        }

        /// <summary>
        /// Get Machine Type
        /// </summary>
        /// <returns></returns>
        public string GetSystemMachineType()
        {
            try
            {
                _theUfSession.UF.AskSystemInfo(out var sysInfo);
                return sysInfo.machine_type;
            }
            catch (Exception e)
            {
                Log.Error("Error in getting System Machine Type",e);
            }

            return string.Empty;
        }

        /// <summary>
        /// Get Installed Memory 
        /// </summary>
        /// <returns></returns>
        public string GetSystemMemory()
        {
            try
            {
                _theUfSession.UF.AskSystemInfo(out var sysInfo);
                return sysInfo.physical_memory + " MB";
            }
            catch (Exception e)
            {
               Log.Error("Error in system memory",e);
            }

            return string.Empty;
        }

        /// <summary>
        /// Get the log file name with full path of the present session
        /// </summary>
        /// <returns></returns>
        public string GetLogFileOfCurrentSession()
        {

            LogFile theSessionLogFile = _theSession.LogFile;
            return theSessionLogFile.FileName;
        }

        /// <summary>
        /// Unloads the image explicitly, via an unload dialog
        /// </summary>
        /// <returns></returns>
        public int UnloadExplicitly()
        {
            //Unloads the image explicitly, via an unload dialog
            return System.Convert.ToInt32(Session.LibraryUnloadOption.Explicitly);
        }

        /// <summary>
        /// Unloads the image when the NX session terminates
        /// </summary>
        /// <returns></returns>
        public int UnloadAtTermination()
        {
            //Unloads the image when the NX session terminates
            return System.Convert.ToInt32(Session.LibraryUnloadOption.AtTermination);
        }

        /// <summary>
        /// Get Nx Full Version
        /// </summary>
        /// <returns></returns>
        public string GetNxVersion()
        {
            return _theSession.GetEnvironmentVariableValue("UGII_FULL_VERSION");
        }

        /// <summary>
        /// Get Os Version of the system
        /// </summary>
        /// <returns></returns>
        public string GetWindowsOsVersion()
        {
            return GetSystemOsName() + " " + GetSystemOsVersion();
        }

        /// <summary>
        /// Get Attribute value
        /// </summary>
        /// <param name="iAttributeName">Name of the attribute</param>
        /// <returns></returns>
        public string GetStringAttributeValue(string iAttributeName)
        {
            if (!CheckIfApplicationInModeling())
            {
                return string.Empty;
            }
            string attributeValue = String.Empty;
            try
            {
                Part part = _theSession.Parts.Display;
                if (part == null)
                {
                    part = _theSession.Parts.Work;
                }

                if (part == null)
                {
                    return attributeValue;
                }
                if (part.HasUserAttribute(iAttributeName, NXObject.AttributeType.String, -1))
                {
                    attributeValue = part.GetStringUserAttribute(iAttributeName, -1);
                }
            }
            catch (Exception e)
            {
                Log.Error("Error in getting attribute value",e);
            }
            return attributeValue;
        }

        /// <summary>
        /// Getting the attribute value from group of attributes
        /// </summary>
        /// <param name="iAttributeNames">Attribute Name list</param>
        /// <returns></returns>
        public string GetStringAttributeValue(List<string> iAttributeNames)
        {
            if (!CheckIfApplicationInModeling())
            {
                return string.Empty;
            }
            string attributeValue = string.Empty;
            try
            {
                List<string> attrs = new List<string>(0);
                Part part = _theSession.Parts.Display;
                if (part == null)
                {
                    part = _theSession.Parts.Work;
                }

                if (part == null)
                {
                    return attributeValue;
                }

                foreach (string iAttributeName in iAttributeNames)
                {
                    if (part.HasUserAttribute(iAttributeName, NXObject.AttributeType.String, -1))
                    {
                        string value = part.GetStringUserAttribute(iAttributeName, -1);

                        attrs.Add(iAttributeName + "=" + value);

                    }
                }

                attributeValue = string.Join(";", attrs);
            }
            catch (Exception e)
            {
               Log.Error("Error in getting attribute value",e);
            }
            return attributeValue;
        }

        /// <summary>
        /// Get Display part number
        /// </summary>
        /// <returns></returns>
        public string GetDisplayPartNumber()
        {
            try
            {
                if (!CheckIfApplicationInModeling())
                {
                    return string.Empty;
                }
                Part part = _theSession.Parts.Work;
                if (part != null)
                {
                    return part.Name.ToUpper();
                }
            }
            catch (Exception e)
            {
              Log.Error("Error in getting display part number",e);
            }
            
            return string.Empty;
        }

        /// <summary>
        /// Get timestamp of the feature created
        /// </summary>
        /// <returns></returns>
        public string GetTimeStampOfFeature()
        {
            if (!CheckIfApplicationInModeling())
            {
                return string.Empty;
            }

            Part part = _theSession.Parts.Work;
            try
            {
                if (part != null)
                {
                    Feature[] features = part.Features.ToArray();
                    if (features.Length > 0)
                    {
                        Feature currentFeature = features.LastOrDefault();
                        if (currentFeature != null)
                        {
                            return currentFeature.Timestamp.ToString();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("Error in getting Timestamp of the feature",e);
            }
            return string.Empty;
        }

        private bool CheckIfApplicationInModeling()
        {
            try
            {
                string theSessionApplicationName = _theSession.ApplicationName;
                if (theSessionApplicationName == "UG_APP_MODELING")
                {
                    return true;
                }
            }
            catch (Exception e)
            {
                Log.Error("Error whle checking modeling workbench",e);
                return false;
            }
           
            return true;
        }
    }
}
