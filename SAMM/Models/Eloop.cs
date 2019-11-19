using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SAMM.Models
{
    public class Eloop
    {
        public int ID { get; set; }
        public int DeviceID { get; set; }
        public string DeviceName { get; set; }
        public string PlateNumber { get; set; }
        public string tblUsersID { get; set; } = "0";
        public string tblLinesID { get; set; } = "0";
        public string tblRoutesID { get; set; } = "0";
        public string tblRoutesIDbyDriver { get; set; } = "0";
        public string IsActive { get; set; }
        public string serverName { get; set; }
        public string DriverName { get; set; }


        //public Eloop(int ID, int DeviceID, string DeviceName, string PlateNumber, int tblUsersID, int tblLinesID, int tblRoutesID,
        //    int tblRoutesIDbyDriver, string IsActive, string serverName, string DriverName)
        //{
        //    this.ID = ID;
        //    this.DeviceID = DeviceID;
        //    this.DeviceName = DeviceName;
        //    this.PlateNumber = PlateNumber;
        //    this.tblUsersID = tblUsersID.ToString();
        //    this.tblLinesID = tblLinesID.ToString();
        //    this.tblRoutesID = tblRoutesID.ToString();
        //    this.tblRoutesIDbyDriver = tblRoutesIDbyDriver.ToString();
        //    this.IsActive = IsActive;
        //    this.serverName = serverName;
        //    this.DriverName = DriverName;
        //}

    }
}
