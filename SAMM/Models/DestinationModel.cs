using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SAMM.Models
{
    public class DestinationModel
    {

        public int ID { get; set; }
        public int tblRouteID { get; set; }
        public string Value { get; set; }
        public string Description { get; set; }
        public int OrderOfArrival { get; set; }
        public string Direction { get; set; }  
        public double Lat { get; set; }
        public double Lng { get; set; }
        public double distanceFromStation { get; set; }
        public int LineID { get; set; }

        public DestinationModel()
        {
            this.OrderOfArrival = 100; //default, means not at any station
        }
        
    }
}
