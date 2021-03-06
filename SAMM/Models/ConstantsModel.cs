﻿using System;
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
        public string DummyPositionsURL { get; set; }
        public int TimerIntervalInSeconds { get; set; }
        public double DepotLat { get; set; }
        public double DepotLng { get; set; }
        public double GeoFenceRadiusInKM { get; set; }
        public double MainTerminalGeoFenceRadiusInKM { get; set; }
        public double PlazaBBuildingGeoFenceRadiusInKM { get; set; }
        public double SouthStationGeoFenceRadiusInKM { get; set; }
        public string TraccarURI { get; set; }
        public string SaveMainTerminalHistoryURL { get; set; }
        public string InsertVehicleGeofenceRecordURL { get; set; }
        public string VehicleSummaryReportGeneratorURL { get; set; }
        public string DefaultRouteIDs { get; set; }
        public int StartIDforDummyEloop { get; set; }
        public int EndIDforDummyEloop { get; set; }
        public int ReportGeneratorHour { get; set; }
        public int ReportGeneratorMinute { get; set; }
        public string GPSProviderURL { get;set;}

        public ConstantsModel()
        {
            this.GMURL = ConfigurationManager.AppSettings["GMbaseURL"];
            this.DefZoomLvl = ConfigurationManager.AppSettings["DefaultZoomLevel"];
            this.FireBaseURL = ConfigurationManager.AppSettings["FirebaseURL"];
            this.FireBaseAuth = ConfigurationManager.AppSettings["FirebaseAUTH"];
            this.TraccarUName = ConfigurationManager.AppSettings["TraccarUsername"];
            this.TraccarPword = ConfigurationManager.AppSettings["TraccarPassword"];
            this.LocationURL = ConfigurationManager.AppSettings["LocationProviderURL"];
            this.DummyPositionsURL = ConfigurationManager.AppSettings["DummyPositionsProviderURL"];
            this.DepotLat = Convert.ToDouble(ConfigurationManager.AppSettings["DepotLat"]);
            this.DepotLng = Convert.ToDouble(ConfigurationManager.AppSettings["DepotLng"]);
            this.GeoFenceRadiusInKM = Convert.ToDouble(ConfigurationManager.AppSettings["GeoFenceRadiusInKM"]);
            this.TimerIntervalInSeconds = Convert.ToInt32(ConfigurationManager.AppSettings["TimerIntervalInSeconds"]);
            this.TraccarURI = ConfigurationManager.AppSettings["traccarwebapiURL"];
            this.SaveMainTerminalHistoryURL = ConfigurationManager.AppSettings["SaveMainTerminalHistoryURL"];
            this.MainTerminalGeoFenceRadiusInKM = Convert.ToDouble(ConfigurationManager.AppSettings["MainTerminalGeoFenceRadiusInKM"]);
            this.PlazaBBuildingGeoFenceRadiusInKM = Convert.ToDouble(ConfigurationManager.AppSettings["PlazaBBuildingGeoFenceRadiusInKM"]);
            this.SouthStationGeoFenceRadiusInKM = Convert.ToDouble(ConfigurationManager.AppSettings["SouthStationGeoFenceRadiusInKM"]);
            this.DefaultRouteIDs = ConfigurationManager.AppSettings["DefaultRouteIDs"];
            this.StartIDforDummyEloop = Convert.ToInt32(ConfigurationManager.AppSettings["StartIDforDummyEloop"]);
            this.EndIDforDummyEloop = Convert.ToInt32(ConfigurationManager.AppSettings["EndIDforDummyEloop"]);
            this.InsertVehicleGeofenceRecordURL = ConfigurationManager.AppSettings["InsertVehicleGeofenceRecordURL"];
            this.VehicleSummaryReportGeneratorURL = ConfigurationManager.AppSettings["VehicleSummaryReportGeneratorURL"];
            this.ReportGeneratorHour = Convert.ToInt32(ConfigurationManager.AppSettings["ReportGeneratorHour"]);
            this.ReportGeneratorMinute = Convert.ToInt32(ConfigurationManager.AppSettings["ReportGeneratorMinute"]);
            this.GPSProviderURL = ConfigurationManager.AppSettings["GPSProviderURL"];
        }
    }
}
