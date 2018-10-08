using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SAMM.Models
{
    public class PositionModel
    {
            public string accuracy { get; set; }
            public string address { get; set; }
            public string altitude { get; set; }
            public AttributeModel attributes { get; set; }
            public string course { get; set; }
            public string deviceId { get; set; }
            public string deviceTime { get; set; }
            public string fixTime { get; set; }
            public string id { get; set; }
            public string latitude { get; set; }
            public string longitude { get; set; }
            public string network { get; set; }
            public string outdated { get; set; }
            public string protocol { get; set; }
            public string serverTime { get; set; }
            public string speed { get; set; }
            public string type { get; set; }
            public string valid { get; set; }

            public PositionModel()
            {
                this.accuracy = "";
                this.address = "";
                this.altitude = "";
                this.attributes = null;
                this.course = "";
                this.deviceId = "";
                this.deviceTime = "";
                this.fixTime = "";
                this.id = "";
                this.latitude = "";
                this.longitude = "";
                this.network = "";
                this.outdated = "";
                this.protocol = "";
                this.serverTime = "";
                this.speed = "";
                this.type = "";
                this.valid = "";
            }
       
    }
}
