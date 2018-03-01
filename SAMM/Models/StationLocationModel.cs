using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SAMM.Models
{
   public class StationLocationModel
    {
       public DestinationModel Destination { get; set; }
        public string LoopIds { get; set; }
        public string RecentlyLeftLoopIds { get; set; }
        public int OrderOfArrival { get; set; }
        public string Dwell { get; set; }

        public StationLocationModel()
        {
            this.LoopIds = string.Empty;
            this.Dwell = string.Empty;
        }
    }
}
