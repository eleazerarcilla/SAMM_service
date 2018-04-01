using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SAMM.Models
{
    public class ConstantsModel
    {
        public string GMURL { get; set; }
        public string DefZoomLvl { get; set; }
        public string FireBaseURL { get; set; }
        public string FireBaseAuth { get; set; }
        public string TraccarUName { get; set; }
        public string TraccarPword { get; set; }
        public string LocationURL { get; set; }
        public int TimerIntervalInSeconds { get; set; }
        public double DepotLat { get; set; }
        public double DepotLng { get; set; }
        public double GeoFenceRadiusInKM { get; set; }
        public double MainTerminalGeoFenceRadiusInKM { get; set; }
        public string TraccarURI { get; set; }
        public string SaveMainTerminalHistoryURL { get; set; }

        public ConstantsModel()
        {
            this.GMURL = ConfigurationManager.AppSettings["GMbaseURL"];
            this.DefZoomLvl = ConfigurationManager.AppSettings["DefaultZoomLevel"];
            this.FireBaseURL = ConfigurationManager.AppSettings["FirebaseURL"];
            this.FireBaseAuth = ConfigurationManager.AppSettings["FirebaseAUTH"];
            this.TraccarUName = ConfigurationManager.AppSettings["TraccarUsername"];
            this.TraccarPword = ConfigurationManager.AppSettings["TraccarPassword"];
            this.LocationURL = ConfigurationManager.AppSettings["LocationProviderURL"];
            this.DepotLat = Convert.ToDouble(ConfigurationManager.AppSettings["DepotLat"]);
            this.DepotLng = Convert.ToDouble(ConfigurationManager.AppSettings["DepotLng"]);
            this.GeoFenceRadiusInKM = Convert.ToDouble(ConfigurationManager.AppSettings["GeoFenceRadiusInKM"]);
            this.TimerIntervalInSeconds = Convert.ToInt32(ConfigurationManager.AppSettings["TimerIntervalInSeconds"]);
            this.TraccarURI = ConfigurationManager.AppSettings["traccarwebapiURL"];
            this.SaveMainTerminalHistoryURL = ConfigurationManager.AppSettings["SaveMainTerminalHistoryURL"];
            this.MainTerminalGeoFenceRadiusInKM = Convert.ToDouble(ConfigurationManager.AppSettings["MainTerminalGeoFenceRadiusInKM"]);

        }
    }
}
