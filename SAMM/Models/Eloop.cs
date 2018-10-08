using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SAMM.Models
{
    class Eloop
    {
        public int ID;
        public int DeviceID;
        public String GPS;
        public DateTime DateTime;
        public float Latitude;
        public float Longitude;
        public Boolean isProcessed;


        public Eloop(int ID, int DeviceID, String GPS, DateTime DateTime, float Latitude, float Longitude)
        {
            this.ID = ID;
            this.DeviceID = DeviceID;
            this.GPS = GPS;
            this.DateTime = DateTime;
            this.Latitude = Latitude;
            this.Longitude = Longitude;
            this.isProcessed = false;
        }

    }
}
    