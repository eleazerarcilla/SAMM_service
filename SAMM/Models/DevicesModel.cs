using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SAMM.Models
{
    public class DevicesModel
    {
       public int id { get; set; }
        public AttributeModel attributes { get; set; }
        public string name { get; set; }
        public string uniqueId { get; set; }
        public string status { get; set; }
        public string lastUpdate { get; set; }
        public int positionId { get; set; }
        public int groupId { get; set; }
        public string[] geofenceIds { get; set; }
        public string phone { get; set; }
        public string model { get; set; }
        public string contact { get; set; }
        public string category { get; set; }
        
    }
}
