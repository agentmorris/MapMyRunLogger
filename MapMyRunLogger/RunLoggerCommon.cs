using System;
using System.Xml.Serialization;

namespace MapMyRunLogger
{
    public class MMRConstants
    {
        public const String OUTPUT_PATH = @"..\..\data\runData";
    }

    [Serializable, XmlRoot("Workout")]
    public class Workout
    {
        public DateTime date = DateTime.MinValue;
        public double distanceMiles = -1.0;
        public double durationSeconds = -1.0;
        public String link = "";
        public String title = "";
        public String id = "";
        public String warning = "";
        public String startLocation = "";
        public String routeLink = "";
        public double weight = 0.0;
        public double paceMinutesPerMile = 0.0;
    }
}
