using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SAMM.Models
{
    public class DriversModel
    {
        public string EnteredStation { get; set; }
        public string IsParked { get; set; }
        public string Lat { get; set; }
        public string Lng { get; set; }
        public string Name { get; set; }
        public string PrevLat { get; set; }
        public string PrevLng { get; set; }
        public string PrevStationOA { get; set; }
        public string LatestStationOA { get; set; }
        public string routeIDs { get; set; }
        public string deviceid { get; set; }
        public string routeIDFromDriver { get; set; }
        public DriversModel()
        {

        }
    }
}
