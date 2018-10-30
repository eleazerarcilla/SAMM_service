using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SAMM.Models
{
    public class LatLngModel
    {
        public int deviceid { get; set; }
        public string Name { get; set; }
        public double Lat { get; set; }
        public double Lng { get; set; }
        public double PrevLat { get; set; }
        public double PrevLng { get; set; }
        public bool IsParked { get; set; }
        public int LatestStationOA { get; set; }
        public int PrevStationOA { get; set; }
        public string routeIDs { get; set; }
        public string enteredStation { get; set; }
        

        

    }
}
