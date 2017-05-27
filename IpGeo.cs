using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuSpecHelper
{
    internal class IpGeo
    {
        public string ip;
        public string country_code;
        public string country_name;
        public string region_code;
        public string region_name;
        public string city;
        public string zip_code;
        public string time_zone;
        public string latitude;
        public string longitude;
        public string metro_code;

        public double getLongitude()
        {
            double val;
            double.TryParse(longitude, out val);
            return val;
        }

        public double getLatitude()
        {
            double val;
            double.TryParse(latitude, out val);
            return val;
        }
    }
}
