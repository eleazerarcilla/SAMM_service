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
        public string DeviceName;
        public string PlateNumber;
        public int tblUsersID;
        public int tblLinesID;
        public int tblRoutesID;
        public int tblRoutesIDbyDriver;
        public string IsActive;
        public string serverName;
        public string DriverName;


        public Eloop(int ID, int DeviceID, string DeviceName, string PlateNumber, int tblUsersID, int tblLinesID, int tblRoutesID,
            int tblRoutesIDbyDriver, string IsActive, string serverName, string DriverName)
        {
            this.ID = ID;
            this.DeviceID = DeviceID;
            this.DeviceName = DeviceName;
            this.PlateNumber = PlateNumber;
            this.tblUsersID = tblUsersID;
            this.tblLinesID = tblLinesID;
            this.tblRoutesID = tblRoutesID;
            this.tblRoutesIDbyDriver = tblRoutesIDbyDriver;
            this.IsActive = IsActive;
            this.serverName = serverName;
            this.DriverName = DriverName;
        }

    }
}
    