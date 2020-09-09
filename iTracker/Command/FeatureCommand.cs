using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using Newtonsoft.Json;

namespace iTracker.Command
{
    public class FeatureCommand
    {
        //public string TimeStamp { get; set; }
        //[BsonId(IdGenerator = typeof(ObjectIdGenerator))]
        //public ObjectId Id { get; set; }

        [BsonElement("Part Name")]
        public string PartName { get; set; }

        [BsonElement("FeatureName")]
        public string FeatureName { get; set; }

        [BsonIgnore]
        //[BsonElement("Edit_Mode")]
        public bool IsEditMode { get; set; }

        [BsonElement("Time_Stamp")]
        public string TimeStamp { get; set; }

        //[BsonElement("User Name")]
        //public string UserName { get; set; }

        [BsonElement("NX Version")]
        public string NxVersion { get; set; }

        [BsonElement("Department")]
        public string Department { get; set; }

        [BsonElement("Business Unit")]
        public string BusinessUnit { get; set; }

        [BsonElement("Windows Version")]
        public string OsVersion { get; set; }

        [BsonElement("Error")]
        public int ErrorStatus { get; set; }

        [BsonElement("Error_Message")]
        public string ErrorMessage { get; set; }

        [BsonElement("Alert_Message")]
        public string AlertMessage { get; set; }

        public BsonArray CaptureSetting { get; set; }

        [BsonIgnore]
        public string InitialSettings { get; set; }

        [BsonIgnore]
        public string ErrorSettings { get; set; }

        [BsonIgnore]
        public string FinalSettings { get; set; }

        [BsonIgnore]
        //[BsonElement("Callback_Type")]
        public string CallBackType { get; set; }

        [BsonElement("Attributes")] 
        public string AttributesString { get; set; }

        /// <summary>
        /// Time and date of Creation of the tool
        /// </summary>
        [BsonElement("Creation")]
        public string TimeOfCreation { get; set; }

        [BsonElement("Machine Type")]
        public string OsMachineType { get; set; }

        [BsonElement("Machine Memory")]
        public string OsInstalledMemory { get; set; }
        public FeatureCommand()
        {
            IsEditMode = false;
            AttributesString = string.Empty;
            TimeOfCreation = string.Empty;
        }
    }
}
