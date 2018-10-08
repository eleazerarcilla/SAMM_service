﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using NLog;
using System.Timers;
using System.Net;
using SAMM.Models;
using System.IO;
using Newtonsoft.Json;
using SAMM.Extensions;

namespace SAMM
{
    partial class Service1 : ServiceBase
    {
        #region Initializers
        private System.Timers.Timer aTimer;
        int lastctr = 0;
        Logger Log = LogManager.GetCurrentClassLogger();
        List<LatLngModel> LatLngList = new List<LatLngModel>();
        ConstantsModel Constants = new ConstantsModel();
        Helpers help = new Helpers();
        List<DestinationModel> StationList = new List<DestinationModel>();
        List<StationLocationModel> StationLocModelList = new List<StationLocationModel>();
        List<StationLocationModel> OldStationLocModelList = new List<StationLocationModel>();
        List<PositionModel> positions = new List<PositionModel>();
        List<DevicesModel> DevicesList = new List<DevicesModel>();
        List<DestinationModel> _DestList;
        
        #endregion
        public Service1()
        {
            InitializeComponent();
        }

        #region Timer
        protected override void OnStart(string[] args)
        {
            int interval = Constants.TimerIntervalInSeconds * 1000;
            aTimer = new System.Timers.Timer(interval);
            aTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
            aTimer.Interval = interval;
            aTimer.Enabled = true;

        }
        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            Log.Info("Performing: OnTimedEvent");
            try
            {
                GetDevices(help.GenerateURL(Constants.TraccarURI, lastctr).Replace("positions", "devices"));
                //This code below is used for movement of dummy e-loop
                //PerformGETCallback(
                //    Constants.DummyPositionsURL, 
                //    Constants.GMURL, 
                //    Constants.DefZoomLvl, 
                //    Constants.FireBaseURL, 
                //    Constants.FireBaseAuth, 
                //    Constants.TraccarUName, 
                //    Constants.TraccarPword, 
                //    GetStations(Constants.LocationURL) 
                //    );

                //This code below is used for movement of real e-loop
                PerformGETCallback(
                    help.GenerateURL(Constants.TraccarURI, lastctr),
                    Constants.GMURL,
                    Constants.DefZoomLvl,
                    Constants.FireBaseURL,
                    Constants.FireBaseAuth,
                    Constants.TraccarUName,
                    Constants.TraccarPword,
                    GetStations(Constants.LocationURL)
                    );
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            finally
            {
                NLog.LogManager.Flush();
            }
        }

        protected override void OnStop()
        {
        }
        #endregion

        public void PerformGETCallback(string apiURL, string GMURL, string DefZoomLvl, string FireBaseURL, string FireBaseAuth, string TraccarUName, string TraccarPword, List<DestinationModel> DestList)
        {
            Log.Info("Performing: PerformGETCallback | Parameters: apiURL=" + apiURL + ", GMURL=" + GMURL + ", DefZoomLvl=" + DefZoomLvl +
                ", FireBaseURL=" + FireBaseURL + ", FirebaseAuth=" + FireBaseAuth + ", TraccarUName=" + TraccarUName
                + ", TraccarPword=" + TraccarPword);
            try
            {
                string resultOfPost = string.Empty;
                //initialize
                int GPSDeviceCount = 0;
                double NumberOfGPSDeviceToProcess = 0.0;

                #region RealCode
                

                HttpWebRequest httpRequest = (HttpWebRequest)WebRequest.Create(apiURL); //"http://localhost:55764/"

                /***Comment the 2 lines of code below when using dummy e-loop***/
                httpRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                httpRequest.Credentials = new NetworkCredential(TraccarUName, TraccarPword);
                /*******************************************/

                httpRequest.Accept = "application/json";
                List<Logger> logs = new List<Logger>();
                HttpWebResponse httpResponse = (HttpWebResponse)httpRequest.GetResponse();
                positions.Clear();
                string _filePath = Path.GetDirectoryName(System.AppDomain.CurrentDomain.BaseDirectory);
                
                using (StreamReader streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    resultOfPost = streamReader.ReadToEnd();
                    positions = JsonConvert.DeserializeObject<List<PositionModel>>(resultOfPost).OrderBy(x => x.deviceId).ToList();
                    GPSDeviceCount = positions.Count();
                    streamReader.Close();
                }
                httpResponse.GetResponseStream().Close();
                httpResponse.GetResponseStream().Flush();


                if (GPSDeviceCount % 2 == 0)
                    NumberOfGPSDeviceToProcess = GPSDeviceCount / 2;
                else
                    NumberOfGPSDeviceToProcess = (GPSDeviceCount / 2) + 0.5;
                #endregion




                Stopwatch s = new Stopwatch();
                s.Start();

                Parallel.ForEach(positions, position =>
                {
                    LatLngModel currentLatLng = new LatLngModel { deviceid = Convert.ToInt32(position.deviceId), Lat = Convert.ToDouble(position.latitude), Lng = Convert.ToDouble(position.longitude) };
                    LatLngModel existingLatLng = LatLngList.FirstOrDefault(x => x.deviceid == currentLatLng.deviceid);
                    bool IsLatLngExisting = existingLatLng == null ? false : true;

                    if (!IsLatLngExisting)
                    {
                        currentLatLng.routeIDs = Constants.DefaultRouteIDs;
                        currentLatLng.PrevLat = 0;
                        currentLatLng.PrevLng = 0;
                        LatLngList.Add(currentLatLng);

                    }
                    else
                    {
                        DestinationModel stationEntered = IsWithinStation(currentLatLng);
                        int OrderOfArrival = stationEntered.OrderOfArrival;
                        currentLatLng.enteredStation = existingLatLng.enteredStation;
                        if (stationEntered.Value != null && stationEntered.Value != "")
                            currentLatLng.enteredStation = stationEntered.Value;
                        currentLatLng.routeIDs = existingLatLng.routeIDs;
                        if (OrderOfArrival != 100)
                        {
                            currentLatLng.routeIDs = getRouteIDsBasedOnStationWhereEloopIs(stationEntered);
                            //currentLatLng.routeIDs = getRouteIDsBasedOnPreviousAndCurrentStationOfEloop(existingLatLng.enteredStation, currentLatLng.enteredStation);
                        }

                        currentLatLng.LatestStationOA = OrderOfArrival == 100 ? currentLatLng.LatestStationOA : OrderOfArrival;
                        if (!UpdateLatLng(currentLatLng))
                        {
                            Log.Error("----ERROR Updating LatLng!----");
                        }
                    }
                });
              

                string Error = string.Empty;
                //SAMM.exe:


                PushToFirebase(LatLngList.ToList(), FireBaseURL, FireBaseAuth);
                Log.Info("----OVERALL Tasks (E-loop) completed " + s.Elapsed);

                //SAMM_2.exe:
                //StationLocModelList = CheckStations(LatLngList, DestList);

                PushStationUpdatesToFirebase(CheckStations(LatLngList, DestList));
                Log.Info("----OVERALL Tasks (Station) completed " + s.Elapsed);

                s.Stop();

            }

            catch (Exception ex)
            {
                Log.Error(ex);
            }


        }

        private String getRouteIDsBasedOnStationWhereEloopIs(DestinationModel station)
        {
            //Log.Info("Performing: getRouteIDsBasedOnStationWhereEloopIs");
            List<String> routeIDs = _DestList.Where(x => x.Value == (station.Value ?? "")).Select(x => x.tblRouteID.ToString()).ToList<String>();
            return String.Join(",", routeIDs.ToArray());
        }
        private String getRouteIDsBasedOnPreviousAndCurrentStationOfEloop(String previousStation, String currentStation)
        {
            
            //Log.Info("Performing: getRouteIDsBasedOnStationWhereEloopIs");

            List<DestinationModel> nodesOfPreviousStation = _DestList.Where(x => x.Value == (previousStation ?? "")).Select(x => x).ToList<DestinationModel>();
            List<DestinationModel> nodesOfCurrentStation = new List<DestinationModel>();
            foreach(DestinationModel nodePrevious in nodesOfPreviousStation)
            {
                DestinationModel nodeCurrent = _DestList.Where(x => x.Value == (currentStation ?? "") && x.OrderOfArrival == nodePrevious.OrderOfArrival + 1).Select(x => x).FirstOrDefault();
                if(nodeCurrent != null)
                {
                    nodesOfCurrentStation.Add(nodeCurrent);
                }
            }
            List<String> routeIDs = new List<String>();
            if(nodesOfCurrentStation.Count>0)
            {
                routeIDs = nodesOfCurrentStation.Select(x => x.tblRouteID.ToString()).ToList<String>();
                
            }
            else
            {
                routeIDs = _DestList.Where(x => x.Value == (currentStation ?? "")).Select(x => x.tblRouteID.ToString()).ToList<String>();
            }
            return String.Join(",", routeIDs.ToArray());
        }

        #region Utils

        public bool UpdateLatLng(LatLngModel Latlng)
        {
            //Log.Info("Performing: UpdateLatLng");
            bool success = false;
            LatLngModel res = new LatLngModel();
            try
            {
                LatLngModel _entry = LatLngList.First(x => x.deviceid == Latlng.deviceid);
                
                _entry.PrevLat = _entry.Lat;
                _entry.PrevLng = _entry.Lng;
                _entry.Lat = Latlng.Lat;
                _entry.Lng = Latlng.Lng;
                _entry.PrevStationOA = (_entry.LatestStationOA == _entry.PrevStationOA) ? _entry.PrevStationOA : _entry.LatestStationOA;
                _entry.LatestStationOA = Latlng.LatestStationOA;
                _entry.enteredStation = Latlng.enteredStation;

                _entry.routeIDs = Latlng.routeIDs;
                res = _entry;
                success = true;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            return success;
        }
        public List<StationLocationModel> CheckStations(List<LatLngModel> LatLngList, List<DestinationModel> DestinationList)
        {
            Log.Info("Performing: CheckStations");
            
            try
            {
                List<LatLngModel> LatLngListForLoopOnly = LatLngList;
                List<DestinationModel> DestinationListForLoopOnly = DestinationList;

                foreach (DestinationModel DestinationListEntry in DestinationListForLoopOnly)
                {
                    string loopIds = string.Empty;
                    foreach (LatLngModel LatLngListEntry in LatLngListForLoopOnly)
                    {
                        StationLocationModel StationLocModel = new StationLocationModel();
                        StationLocModel.Destination = DestinationListEntry;
                        StationLocModel.OrderOfArrival = DestinationListEntry.OrderOfArrival;
                        int iteratorID = DestinationListEntry.OrderOfArrival;
                        bool isMainTerminal = DestinationListEntry.Value.Contains("Main") ? true : false;
                        bool IsExisting = StationLocModelList.FirstOrDefault(x => x.Destination.Value == DestinationListEntry.Value && x.Destination.tblRouteID == DestinationListEntry.tblRouteID) == null ? false : true;


                        if (help.GetDistance(DestinationListEntry.Lat, DestinationListEntry.Lng, LatLngListEntry.Lat, LatLngListEntry.Lng) <= (Constants.GeoFenceRadiusInKM + (isMainTerminal ? Constants.MainTerminalGeoFenceRadiusInKM : 0.0)))
                        {
                            loopIds += (StationLocModel.LoopIds == string.Empty ? (LatLngListEntry.deviceid != 0 ? LatLngListEntry.deviceid.ToString() + "," : string.Empty) : (LatLngListEntry.deviceid != 0 ? LatLngListEntry.deviceid.ToString() : string.Empty));

                            if (!IsExisting)
                            {
                                StationLocModel.Dwell = loopIds;
                                StationLocModelList.Add(StationLocModel);
                            }
                            else
                                UpdateExistingStationLocationModel(StationLocModelList, LatLngListEntry.deviceid.ToString(), DestinationListEntry, iteratorID, true);
                        }
                        else
                        {
                            if (!IsExisting)
                            {
                                StationLocModelList.Add(StationLocModel);
                            }
                            else
                            {
                                UpdateExistingStationLocationModel(StationLocModelList, LatLngListEntry.deviceid.ToString(), DestinationListEntry, iteratorID, false);
                            }
                            continue;
                        }
                        //DUMMYELOOP
                        RemoveOfflineDevicesFromList(StationLocModelList);
                        //DUMMYELOOP
                    }
                }

            }
            catch (Exception ex)
            {
                Log.Error("----ERROR: " + ex);
            }
            return StationLocModelList;
        }
        public void UpdateExistingStationLocationModel(List<StationLocationModel> SLModelList, string loopids, DestinationModel DestModel, int iteratorID, bool IsDwelling)
        {
            //Log.Info("Performing: UpdateExistingStationLocationModel | Parameters: loopids=" + loopids
            //    + ", iteratorID="
            //    + iteratorID.ToString()
            //    + ", IsDwelling=" + IsDwelling.ToString());
            if (IsDwelling)
            {
                StationLocationModel ExistingRecord = SLModelList.FirstOrDefault(x => x.Destination.Value == DestModel.Value && x.Destination.tblRouteID == DestModel.tblRouteID);
                
                if (!ExistingRecord.Dwell.Split(',').Contains(loopids))
                    ExistingRecord.Dwell = ExistingRecord.Dwell + loopids + ",";
                ExistingRecord.Destination = DestModel;
                ExistingRecord.OrderOfArrival = DestModel.OrderOfArrival;

                List<StationLocationModel> ExistingRecordList = SLModelList.Where(x => x.Destination.Value != DestModel.Value && x.Destination.tblRouteID !=DestModel.tblRouteID)
                    .Where(y => y.Dwell.Split(',').Contains(loopids)).ToList();

                foreach (StationLocationModel entry in ExistingRecordList)
                {
                    List<String> dwellList = new List<String>(entry.Dwell.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries));
                    dwellList.Remove(loopids);
                    entry.Dwell = String.Join(",", dwellList.ToArray()) + ",";
                }
                ExistingRecordList = SLModelList.Where(x => x.Destination.Value != DestModel.Value && x.Destination.tblRouteID != DestModel.tblRouteID)
                    .Where(y => y.LoopIds.Split(',').Contains(loopids)).ToList();
                foreach (StationLocationModel entry in ExistingRecordList)
                {
                    List<String> loopIdsList = new List<String>(entry.LoopIds.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries));
                    loopIdsList.Remove(loopids);
                    entry.LoopIds = String.Join(",", loopIdsList.ToArray()) + ",";
                }
            }
            else
            {
                StationLocationModel ExistingRecord = SLModelList.FirstOrDefault(x => x.Destination.Value == DestModel.Value && x.Destination.tblRouteID == DestModel.tblRouteID);
                String[] existingDwellList = ExistingRecord.Dwell.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);

                if (existingDwellList.Contains(loopids))
                {
                    if (!ExistingRecord.LoopIds.Split(',').Contains(loopids))
                        ExistingRecord.LoopIds = ExistingRecord.LoopIds + loopids + ",";
                    List<String> dwellList = new List<String>(existingDwellList);
                    dwellList.Remove(loopids);
                    ExistingRecord.Dwell = String.Join(",", dwellList.ToArray()) + ",";
                }
                ExistingRecord.Destination = DestModel;
                ExistingRecord.OrderOfArrival = DestModel.OrderOfArrival;

                //List<StationLocationModel> ExistingRecordList = SLModelList.Where(x => x.LoopIds.Contains(loopids) && x.OrderOfArrival != iteratorID).ToList();
                //foreach (StationLocationModel entry in ExistingRecordList)
                //{
                //    entry.LoopIds = entry.LoopIds.Replace((loopids+","), "");
                //}
            }

        }
        public void RemoveOfflineDevicesFromList(List<StationLocationModel> SLModelList)
        {
            //Log.Info("Performing: RemoveOfflineDevicesFromList");
            foreach (StationLocationModel entry in SLModelList)
            {
                if (entry.LoopIds != null && entry.LoopIds.Length != 0)
                {
                    List<string> _tempLoopIds = entry.LoopIds.Substring(0, entry.LoopIds.Length).Split(',').ToList();

                    foreach (string loop in _tempLoopIds)
                    {
                        if (loop != "")
                            if (!IsDeviceOnline(Convert.ToInt32(loop)))
                            {
                                List<String> loopIdsList = new List<String>(entry.LoopIds.Split(','));
                                loopIdsList.Remove(loop);
                                entry.LoopIds = String.Join(",", loopIdsList.ToArray());

                            }

                    }
                }
                if (entry.Dwell != null && entry.Dwell.Length != 0)
                {
                    List<string> _tempDwellIds = entry.Dwell.Substring(0, entry.Dwell.Length).Split(',').ToList();
                    foreach (string loop in _tempDwellIds)
                    {
                        if (loop != "")
                            if (!IsDeviceOnline(Convert.ToInt32(loop)))
                            {
                                List<String> dwellList = new List<String>(entry.Dwell.Split(','));
                                dwellList.Remove(loop);
                                entry.Dwell = String.Join(",", dwellList.ToArray());
                            }
                    }
                }
            }
        }
        public bool IsDeviceOnline(int LoopId)
        {
            //Log.Info("Performing: IsDeviceOnline | Parameters: LoopId=" + LoopId.ToString());
            bool IsOnline = false;
            try
            {
                DevicesModel DeviceInfo = DevicesList.FirstOrDefault(x => x.id == LoopId);
                IsOnline = DeviceInfo.status == "online" ? true : false;
            }
            catch (Exception ex)
            {
                //ignored
                Log.Error(ex);
            }
            return IsOnline;
        }
        public DestinationModel IsWithinStation(LatLngModel LatLngEntry)
        {
            //Log.Info("Performing: IsWithinStation");
            DestinationModel result = new DestinationModel();
            try
            {
                Parallel.ForEach(_DestList, (entry, loopState) =>
                {

                    bool isMainTerminal = entry.Value.Contains("Main") ? true : false;
                    if (help.GetDistance(entry.Lat, entry.Lng, LatLngEntry.Lat, LatLngEntry.Lng) <= (Constants.GeoFenceRadiusInKM + (isMainTerminal ? Constants.MainTerminalGeoFenceRadiusInKM : 0.0)))
                    { 
                        
                        result = entry;
                        
                        loopState.Break();

                    }

                });

            }
            catch (Exception ex)
            {
                //ignored
            }
            
            return result;
        }
        #endregion
        #region JSON Creators
        //public string CreateLatLngJson(LatLngModel Latlng)
        //{
        //    //Log.Info("Performing: CreateLatLngJson");
        //    string res = string.Empty;
        //    try
        //    {
        //        bool isparked = (help.GetDistance(Constants.DepotLat, Constants.DepotLng, Latlng.Lat, Latlng.Lng) <= (Constants.GeoFenceRadiusInKM + 0.02)) ? true : false;

        //        res = JsonConvert.SerializeObject(new LatLngModel
        //        {
        //            Lat = Latlng.Lat,
        //            Lng = Latlng.Lng,
        //            PrevLat = Latlng.PrevLat,
        //            PrevLng = Latlng.PrevLng,
        //            deviceid = Latlng.deviceid,
        //            IsParked = isparked,
        //            LatestStationOA = Latlng.LatestStationOA,
        //            PrevStationOA = Latlng.PrevStationOA,
        //            routeIDs = Latlng.routeIDs

        //        });

        //    }
        //    catch (Exception ex)
        //    {
        //        Log.Error(ex);
        //    }
        //    return res;
        //}
        public string CreateLatLngJson(List<LatLngModel> LatLngList)
        {
            Log.Info("Performing: CreateLatLngJson");
            string res = string.Empty;
            try
            {
                String jsonString = "{";
                foreach (LatLngModel LatLng in LatLngList)
                {
                   
                    bool isparked = (help.GetDistance(Constants.DepotLat, Constants.DepotLng, LatLng.Lat, LatLng.Lng) <= (Constants.GeoFenceRadiusInKM + 0.02)) ? true : false;
                    jsonString += "\"" + LatLng.deviceid 
                        + "\":{\"Lat\":\"" + LatLng.Lat
                        + "\",\"Lng\":\"" + LatLng.Lng
                        + "\",\"PrevLat\":\"" + LatLng.PrevLat
                        + "\",\"PrevLng\":\"" + LatLng.PrevLng
                        + "\",\"deviceid\":\"" + LatLng.deviceid
                        + "\",\"IsParked\":\"" + LatLng.IsParked
                        + "\",\"LatestStationOA\":\"" + LatLng.LatestStationOA
                        + "\",\"PrevStationOA\":\"" + LatLng.PrevStationOA
                        + "\",\"EnteredStation\":\"" + LatLng.enteredStation
                        + "\",\"routeIDs\":\"" + LatLng.routeIDs + "\"},";
                }
                jsonString = jsonString.Substring(0, jsonString.Length - 1);
                jsonString += "}";

                
                
                Log.Info(jsonString);
                //res = JsonConvert.SerializeObject(new StationLocationModel
                //{

                //    tblRouteID = StationLocModel.Destination.tblRouteID,
                //    OrderOfArrival = StationLocModel.OrderOfArrival,
                //    LoopIds = StationLocModel.LoopIds,
                //    Dwell = StationLocModel.Dwell
                //});
                return jsonString;

            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            return res;
        }
        public string CreateDestinationJson(StationLocationModel StationLocModel)
        {
            Log.Info("Performing: CreateDestinationJson");
            string res = string.Empty;

            try
            {

                res = JsonConvert.SerializeObject(new StationLocationModel
                {
                    tblRouteID = StationLocModel.Destination.tblRouteID,
                    OrderOfArrival = StationLocModel.OrderOfArrival,
                    LoopIds = StationLocModel.LoopIds,
                    Dwell = StationLocModel.Dwell
                });

            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            return res;


        }
        #endregion
        #region Requests
        public List<DestinationModel> GetStations(string URL)
        {
            Log.Info("Performing: GetStations | Parameters: URL=" + URL);
            
            List<DestinationModel> StationsLoc = new List<DestinationModel>();
            try
            {
                string GETResult = string.Empty;
                StationList.Clear();
                Stopwatch s = new Stopwatch();
                s.Start();
                HttpWebRequest httpRequest = (HttpWebRequest)WebRequest.Create(URL);
                httpRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                HttpWebResponse httpResponse = (HttpWebResponse)httpRequest.GetResponse();
                using (StreamReader streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    GETResult = streamReader.ReadToEnd();
                    StationsLoc = JsonConvert.DeserializeObject<List<DestinationModel>>(GETResult);
                    streamReader.Close();
                }
                httpResponse.GetResponseStream().Close();
                httpResponse.GetResponseStream().Flush();
                s.Stop();

            }
            catch (Exception ex)
            {
                //ignored

            }
            _DestList = StationsLoc;
            return StationsLoc;
        }
        public async void GetDevices(string URL)
        {
            Log.Info("Performing: GetDevices | Parameters: URL=" + URL);
            await Task.Run(() =>
            {
                try
                {
                    
                    string GETResult = string.Empty;
                    StationList.Clear();
                    Stopwatch s = new Stopwatch();
                    s.Start();
                    HttpWebRequest httpRequest = (HttpWebRequest)WebRequest.Create(URL);
                    httpRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                    httpRequest.Credentials = new NetworkCredential(Constants.TraccarUName, Constants.TraccarPword);
                    HttpWebResponse httpResponse = (HttpWebResponse)httpRequest.GetResponse();
                    using (StreamReader streamReader = new StreamReader(httpResponse.GetResponseStream()))
                    {
                        GETResult = streamReader.ReadToEnd();
                        DevicesList = JsonConvert.DeserializeObject<List<DevicesModel>>(GETResult);
                        streamReader.Close();
                    }
                    httpResponse.GetResponseStream().Close();
                    httpResponse.GetResponseStream().Flush();
                    s.Stop();
                    //  Log.Info("Stations query finished in:" + s.Elapsed);
                    //Log.Info("Found (" + DevicesList.Count() + ") device" + (DevicesList.Count() > 1 ? "s." : "."));

                }
                catch (Exception ex)
                {
                    Log.Error(ex);

                }
            }
            );
        }
        #endregion
        #region Pushing to Firebase
        public void PushToFirebase(List<LatLngModel> LatLngList, string FireBaseURL, string FireBaseAuth)
        {

            Log.Info("Performing: PushToFireBase | Parameters: FireBaseURL=" + FireBaseURL + ", FireBaseAuth=" + FireBaseAuth);
            //NOTE: Removed this await Task.Run portion.. Because it is causing delay in pushing to firebase. IDK why :(
            //await Task.Run(() =>
            //{
            bool res = false;
            string url = string.Empty;
            try
            {
                
                Stopwatch s = new Stopwatch();
                s.Start();
                
                //Parallel.ForEach(LatLngList, LatLngListEntry =>
                //{
                    try
                    {
                        //url = FireBaseURL + LatLngListEntry.deviceid + "/.json?auth=" + FireBaseAuth;
                        url = FireBaseURL + ".json?auth=" + FireBaseAuth;
                        string resultOfPost = string.Empty;


                        HttpWebRequest httpRequest = (HttpWebRequest)WebRequest.Create(url);
                        httpRequest.Method = "PUT";
                        httpRequest.ContentType = "application/json";


                        //String json = CreateLatLngJson(LatLngListEntry);
                        String json = CreateLatLngJson(LatLngList);
                        var buffer = Encoding.UTF8.GetBytes(json);

                            httpRequest.ContentLength = buffer.Length;
                            httpRequest.GetRequestStream().Write(buffer, 0, buffer.Length);
                            var response = httpRequest.GetResponse();

                            response.Close();
                            httpRequest.GetRequestStream().Close();
                            httpRequest.GetRequestStream().Flush();
                        }
                        catch (Exception ex)
                        {
                            Log.Error("Error in E-loop Location:" + url + " | " + ex);
                        }
                //});
                res = true;
                s.Stop();
                // Log.Info("--(" + LatLngList.Count() + ")--(" + positions.Count() + ")--E-Loop position update Task completed in " + s.Elapsed);
            }
            catch (Exception ex)
            {
                Log.Error("Error in E-loop Location:" + url + " | " + ex);

            }
            //Error = "E-Loop location";
            //return res;
            //});

        }
        public void PushStationUpdatesToFirebase(List<StationLocationModel> StationLocModelList)//, out string Error)
        {
            Log.Info("Performing PushStationUpdatesToFirebase");
            bool success = false;
            Stopwatch s = new Stopwatch();
            try
            {
                s.Start();
                //Parallel.ForEach(ModifiedStationLocModelList, StationListEntry =>
                //{
                sendStationUpdatesToFireBase(StationLocModelList);


                    #region comment
                    //if (StationListEntry.Destination.Value.ToString() == "MainTerminalbesideFilinvestFirestation")
                    //{
                    //    await Task.Run(() =>
                    //    {
                    //        try
                    //        {
                    //            HttpWebRequest mainTerminalHistory_webReq = (HttpWebRequest)WebRequest.Create(Constants.SaveMainTerminalHistoryURL
                    //            + "dwell=" + StationListEntry.Dwell.ToString()
                    //            + "&loopids=" + StationListEntry.LoopIds.ToString());
                    //            WebResponse mainTerminalHistory_response = mainTerminalHistory_webReq.GetResponse();
                    //            mainTerminalHistory_response.Close();
                    //        }
                    //        catch (Exception ex)
                    //        {
                    //            Log.Error("Error in E-loop Location: Updating of Mainterminal History | " + ex);

                    //        }

                    //    });
                    //}
                    #endregion
                //});
                s.Stop();
                // Log.Info("----STATION UPDATES Task completed in " + s.Elapsed);
                success = true;
            }
            catch (Exception ex)
            {
                Log.Error("Error:" + ex);
            }
            //Error = "Station";
            //return success;
            //});

        }

        private void sendStationUpdatesToFireBase(List<StationLocationModel> StationListEntry)
        {

            //string url = Constants.FireBaseURL.Replace("drivers", "vehicle_destinations") + StationListEntry.Destination.Value + "_" + StationListEntry.Destination.tblRouteID.ToString() + "/.json?auth=" + Constants.FireBaseAuth;
            string url = Constants.FireBaseURL.Replace("drivers", "vehicle_destinations") + ".json?auth=" + Constants.FireBaseAuth;
            try
            {
                string resultOfPost = string.Empty;
                HttpWebRequest httpRequest = (HttpWebRequest)WebRequest.Create(url);
                httpRequest.Method = "PUT";
                httpRequest.ContentType = "application/json";

                //var buffer = Encoding.UTF8.GetBytes(CreateDestinationJson(StationListEntry));
                var buffer = Encoding.UTF8.GetBytes(CreateVehicleDestinationsJson(StationListEntry));
                httpRequest.ContentLength = buffer.Length;
                httpRequest.GetRequestStream().Write(buffer, 0, buffer.Length);
                WebResponse response = httpRequest.GetResponse();
                response.Close();
                httpRequest.GetRequestStream().Close();
                httpRequest.GetRequestStream().Flush();
            }
            catch (Exception ex)
            {

                Log.Error("Error in Station Updates:" + url + " | " + ex);
            }

        }
        public string CreateVehicleDestinationsJson(List<StationLocationModel> StationLocModel)
        {
            Log.Info("Performing: CreateVehicleDestinationsJson");
            string res = string.Empty;
            //aTimer.Stop();
            //Debugger.Launch();

            try
            {
                String jsonString = "{";
                foreach(StationLocationModel model in StationLocModel)
                {
                    jsonString += "\""+ model.Destination.Value + "_" + model.Destination.tblRouteID + "\":{\"tblRouteID\":\"" + model.Destination.tblRouteID + "\",\"OrderOfArrival\":\"" 
                        + model.OrderOfArrival + "\",\"LoopIds\":\"" + model.LoopIds + "\",\"Dwell\":\"" + model.Dwell + "\"},";

                }
                jsonString = jsonString.Substring(0, jsonString.Length - 1);
                jsonString += "}";
                Log.Info(jsonString);
                //res = JsonConvert.SerializeObject(new StationLocationModel
                //{

                //    tblRouteID = StationLocModel.Destination.tblRouteID,
                //    OrderOfArrival = StationLocModel.OrderOfArrival,
                //    LoopIds = StationLocModel.LoopIds,
                //    Dwell = StationLocModel.Dwell
                //});
                return jsonString;

            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            return res;


        }
        #endregion

    }
}

