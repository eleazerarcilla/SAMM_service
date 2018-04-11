using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using System.Timers;
using System.Net;
using SAMM.Models;
using System.IO;
using Newtonsoft.Json;
using System.Configuration;
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
        List<PositionModel> pos = new List<PositionModel>();
        List<DevicesModel> DevicesList = new List<DevicesModel>();
        List<DevicesModel> PreviousDevicesList = new List<DevicesModel>();

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
            try
            {
                GetDevices(help.GenerateURL(Constants.TraccarURI, lastctr).Replace("positions", "devices"));
                PerformGETCallback(help.GenerateURL(Constants.TraccarURI, lastctr), Constants.GMURL, Constants.DefZoomLvl, Constants.FireBaseURL, Constants.FireBaseAuth, Constants.TraccarUName, Constants.TraccarPword, GetStations(Constants.LocationURL));
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
            try
            {
                string resultOfPost = string.Empty;

                //initialize
                int GPSDeviceCount = 0;
                double NumberOfGPSDeviceToProcess = 0.0;
                HttpWebRequest httpRequest = (HttpWebRequest)WebRequest.Create(apiURL); //"http://localhost:55764/"
                httpRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                httpRequest.Credentials = new NetworkCredential(TraccarUName, TraccarPword);
                httpRequest.Accept = "application/json";
                List<Logger> logs = new List<Logger>();
                HttpWebResponse httpResponse = (HttpWebResponse)httpRequest.GetResponse();
                pos.Clear();
                using (StreamReader streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    resultOfPost = streamReader.ReadToEnd();
                    pos = JsonConvert.DeserializeObject<List<PositionModel>>(resultOfPost).OrderBy(x => x.deviceId).ToList();
                    GPSDeviceCount = pos.Count();
                    streamReader.Close();
                }
                httpResponse.GetResponseStream().Close();
                httpResponse.GetResponseStream().Flush();

                if (GPSDeviceCount % 2 == 0)
                    NumberOfGPSDeviceToProcess = GPSDeviceCount / 2;
                else
                    NumberOfGPSDeviceToProcess = (GPSDeviceCount / 2) + 0.5;

                Stopwatch s = new Stopwatch();
                s.Start();

                foreach (PositionModel entry in pos)
                {
                    LatLngModel _LatLng = new LatLngModel { deviceid = Convert.ToInt32(entry.deviceId), Lat = Convert.ToDouble(entry.latitude), Lng = Convert.ToDouble(entry.longitude) };
                    bool IsLatLngExisting = LatLngList.FirstOrDefault(x => x.deviceid == _LatLng.deviceid) == null ? false : true;
                    if (!IsLatLngExisting)
                    {
                        LatLngList.Add(_LatLng);
                    }
                    else
                    {
                        int OrderOfArrival = IsWithinStation(_LatLng).OrderOfArrival;
                        _LatLng.LatestStationOA = OrderOfArrival == 100 ? _LatLng.LatestStationOA : OrderOfArrival;
                        if (!UpdateLatLng(_LatLng))
                        {
                            Log.Error("----ERROR Updating LatLng!----");
                        }
                    }
                }

                string Error = string.Empty;

                //SAMM.exe:
                //PushToFirebase(LatLngList.ToList(), FireBaseURL, FireBaseAuth);

                //SAMM_2.exe:
                PushStationUpdatesToFirebase(CheckStations(LatLngList, DestList));


                Log.Info("----OVERALL Tasks (Station) completed " + s.Elapsed);
                s.Stop();

            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }

        }

        #region Utils

        public bool UpdateLatLng(LatLngModel Latlng)
        {
            bool success = false;
            LatLngModel res = new LatLngModel();
            try
            {
                LatLngModel _entry = LatLngList.First(x => x.deviceid == Latlng.deviceid);
                _entry.PrevLat = (_entry.Lat == _entry.PrevLat) ? _entry.PrevLat : _entry.Lat;
                _entry.PrevLng = (_entry.Lng == _entry.PrevLng) ? _entry.PrevLng : _entry.Lng;
                _entry.Lat = Latlng.Lat;
                _entry.Lng = Latlng.Lng;
                _entry.PrevStationOA = (_entry.LatestStationOA == _entry.PrevStationOA) ? _entry.PrevStationOA : _entry.LatestStationOA;
                _entry.LatestStationOA = Latlng.LatestStationOA;
                res = _entry;
                success = true;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            return success;
        }

        public async void SaveMainTerminalHistory(int deviceid, String action, string reason = "")
        {
            //await Task.Run(() =>
            //{
            //    try
            //    {
            //        HttpWebRequest mainTerminalHistory_webReq = (HttpWebRequest)WebRequest.Create(Constants.SaveMainTerminalHistoryURL + "deviceid=" + deviceid + "&action=" + action + "&reason=" + reason);
            //        WebResponse mainTerminalHistory_response = mainTerminalHistory_webReq.GetResponse();
            //        mainTerminalHistory_response.Close();
            //    }

            //    catch (Exception ex)
            //    {
            //        Log.Error("Error in E-loop Location: Updating of Mainterminal History | " + ex);
            //    }
            //});

        }
        public List<StationLocationModel> CheckStations(List<LatLngModel> LatLngList, List<DestinationModel> DestinationList)
        {
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
                        bool IsExisting = StationLocModelList.FirstOrDefault(x => x.Destination.Value == DestinationListEntry.Value) == null ? false : true;
                        if (help.GetDistance(DestinationListEntry.Lat, DestinationListEntry.Lng, LatLngListEntry.Lat, LatLngListEntry.Lng) <= (Constants.GeoFenceRadiusInKM + (isMainTerminal ? Constants.MainTerminalGeoFenceRadiusInKM : 0.0)))
                        {

                            loopIds += (StationLocModel.LoopIds == string.Empty ? (LatLngListEntry.deviceid != 0 ? LatLngListEntry.deviceid.ToString() + "," : string.Empty) : (LatLngListEntry.deviceid != 0 ? LatLngListEntry.deviceid.ToString() : string.Empty));

                            if (!IsExisting)
                            {
                                if (DestinationListEntry.Value.Contains("Main"))
                                {
                                    if (IsDeviceOnline(LatLngListEntry.deviceid))
                                        SaveMainTerminalHistory(Convert.ToInt32(LatLngListEntry.deviceid), "IN");
                                }
                                StationLocModel.Dwell = loopIds;
                                StationLocModelList.Add(StationLocModel);
                            }
                            else
                                UpdateExistingStationLocationModel(LatLngListEntry.deviceid.ToString(), DestinationListEntry, iteratorID, true);
                        }
                        else
                        {
                            if (!IsExisting)
                            {
                                StationLocModelList.Add(StationLocModel);
                            }
                            else
                            {
                                UpdateExistingStationLocationModel(LatLngListEntry.deviceid.ToString(), DestinationListEntry, iteratorID, false);
                            }
                            continue;
                        }
                        RemoveOfflineDevicesFromList(StationLocModelList);
                    }
                }

            }
            catch (Exception ex)
            {
                Log.Error("----ERROR: " + ex);
            }

            return StationLocModelList;
        }
        public void UpdateExistingStationLocationModel(string loopids, DestinationModel DestModel, int iteratorID, bool IsDwelling)
        {
            if (IsDwelling)
            {
                StationLocationModel ExistingRecord = StationLocModelList.FirstOrDefault(x => x.Destination.Value == DestModel.Value);
                if (!ExistingRecord.Dwell.Split(',').Contains(loopids))
                {
                    if (ExistingRecord.Destination.Value.Contains("Main"))
                    {
                        int deviceid = Convert.ToInt32(loopids);
                        if (IsDeviceOnline(deviceid))
                            SaveMainTerminalHistory(deviceid, "IN");
                    }
                    ExistingRecord.Dwell = ExistingRecord.Dwell + loopids + ",";
                }
                ExistingRecord.Destination = DestModel;
                ExistingRecord.OrderOfArrival = DestModel.OrderOfArrival;

                List<StationLocationModel> ExistingRecordList = StationLocModelList.Where(x => x.Destination.Value != DestModel.Value)
                    .Where(y => y.Dwell.Split(',').Contains(loopids)).ToList();

                foreach (StationLocationModel entry in ExistingRecordList)
                {
                    List<String> dwellList = new List<String>(entry.Dwell.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries));
                    dwellList.Remove(loopids);
                    entry.Dwell = String.Join(",", dwellList.ToArray()) + ",";

                }
                ExistingRecordList = StationLocModelList.Where(x => x.Destination.Value != DestModel.Value)
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
                StationLocationModel ExistingRecord = StationLocModelList.FirstOrDefault(x => x.Destination.Value == DestModel.Value);
                String[] existingDwellList = ExistingRecord.Dwell.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);

                if (existingDwellList.Contains(loopids))
                {

                    if (!ExistingRecord.LoopIds.Split(',').Contains(loopids))
                        ExistingRecord.LoopIds = ExistingRecord.LoopIds + loopids + ",";
                    List<String> dwellList = new List<String>(existingDwellList);
                    dwellList.Remove(loopids);
                    ExistingRecord.Dwell = String.Join(",", dwellList.ToArray()) + ",";
                    if (ExistingRecord.Destination.Value.Contains("Main"))
                    {
                        int deviceId = Convert.ToInt32(loopids);
                        if (IsDeviceOnline(deviceId))
                            SaveMainTerminalHistory(Convert.ToInt32(loopids), "OUT");
                    }
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
            foreach (StationLocationModel entry in SLModelList)
            {
                if (entry.LoopIds != null && entry.LoopIds.Length != 0)
                {
                    //Log.Info(entry.LoopIds + "length: " + entry.LoopIds.Length);
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
            bool IsOnline = false;
            try
            {
                DevicesModel DeviceInfo = DevicesList.FirstOrDefault(x => x.id == LoopId);
                IsOnline = DeviceInfo.status == "online" ? true : false;
            }
            catch (Exception ex)
            {
                //ignored
            }
            return IsOnline;
        }
        public DestinationModel IsWithinStation(LatLngModel LatLngEntry)
        {
            DestinationModel result = new DestinationModel();
            try
            {
                Parallel.ForEach(StationList, (entry, loopState) =>
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
        public string CreateLatLngJson(LatLngModel Latlng)
        {
            string res = string.Empty;
            try
            {
                bool isparked = (help.GetDistance(Constants.DepotLat, Constants.DepotLng, Latlng.Lat, Latlng.Lng) <= (Constants.GeoFenceRadiusInKM + 0.02)) ? true : false;

                res = JsonConvert.SerializeObject(new LatLngModel
                {
                    Lat = Latlng.Lat,
                    Lng = Latlng.Lng,
                    PrevLat = Latlng.PrevLat,
                    PrevLng = Latlng.PrevLng,
                    deviceid = Latlng.deviceid,
                    IsParked = isparked,
                    LatestStationOA = Latlng.LatestStationOA,
                    PrevStationOA = Latlng.PrevStationOA
                });

            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            return res;
        }
        public string CreateDestinationJson(StationLocationModel StationLocModel)
        {
            string res = string.Empty;

            try
            {

                res = JsonConvert.SerializeObject(new StationLocationModel
                {
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
            return StationsLoc;
        }
        public async void GetDevices(string URL)
        {
            //List<DevicesModel> DevicesList = new List<DevicesModel>();
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
                        if (DevicesList.Count > 0)
                            PreviousDevicesList = new List<DevicesModel>(DevicesList);
                        DevicesList = JsonConvert.DeserializeObject<List<DevicesModel>>(GETResult);
                        streamReader.Close();
                    }
                    httpResponse.GetResponseStream().Close();
                    httpResponse.GetResponseStream().Flush();
                    s.Stop();
                    foreach (DevicesModel previousDevice in PreviousDevicesList)
                    {
                        DevicesModel currentDevice = DevicesList.Where(x => x.id == previousDevice.id).FirstOrDefault();
                        if (currentDevice.status == "offline" && previousDevice.status == "online")
                            SaveMainTerminalHistory(currentDevice.id, "OUT", "Device went offline");
                    }
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
        public async void PushToFirebase(List<LatLngModel> LatLngList, string FireBaseURL, string FireBaseAuth)
        //public bool PushToFirebase(List<LatLngModel> LatLngList, string FireBaseURL, string FireBaseAuth, out String Error)
        {
            await Task.Run(() =>
            {
                bool res = false;
                string url = string.Empty;
                try
                {
                    Stopwatch s = new Stopwatch();
                    s.Start();
                    Parallel.ForEach(LatLngList, LatLngListEntry =>
                    {
                        //await Task.Run(() =>
                        //{
                        try
                        {
                            url = FireBaseURL + LatLngListEntry.deviceid + "/.json?auth=" + FireBaseAuth;
                            string resultOfPost = string.Empty;

                            HttpWebRequest httpRequest = (HttpWebRequest)WebRequest.Create(url);
                            httpRequest.Method = "PATCH";
                            httpRequest.ContentType = "application/json";

                            var buffer = Encoding.UTF8.GetBytes(CreateLatLngJson(LatLngListEntry));
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
                    });
                    res = true;
                    s.Stop();
                    // Log.Info("--(" + LatLngList.Count() + ")--(" + pos.Count() + ")--E-Loop position update Task completed in " + s.Elapsed);
                }
                catch (Exception ex)
                {
                    Log.Error("Error in E-loop Location:" + url + " | " + ex);

                }
                //Error = "E-Loop location";
                //return res;
            });

        }
        public void PushStationUpdatesToFirebase(List<StationLocationModel> StationLocModelList)//, out string Error)
        {
            //await Task.Run(() => {
            bool success = false;
            Stopwatch s = new Stopwatch();
            try
            {
                s.Start();
                Parallel.ForEach(StationLocModelList, async StationListEntry =>
                {
                    await Task.Run(() =>
                    {
                        string url = Constants.FireBaseURL.Replace("drivers", "vehicle_destinations") + StationListEntry.Destination.Value + "/.json?auth=" + Constants.FireBaseAuth;
                        try
                        {
                            string resultOfPost = string.Empty;
                            HttpWebRequest httpRequest = (HttpWebRequest)WebRequest.Create(url);
                            httpRequest.Method = "PUT";
                            httpRequest.ContentType = "application/json";

                            var buffer = Encoding.UTF8.GetBytes(CreateDestinationJson(StationListEntry));
                            httpRequest.ContentLength = buffer.Length;
                            httpRequest.GetRequestStream().Write(buffer, 0, buffer.Length);
                            WebResponse response = httpRequest.GetResponse();
                            response.Close();
                            httpRequest.GetRequestStream().Close();
                            httpRequest.GetRequestStream().Flush();

                        }
                        catch (Exception ex)
                        {
                            Log.Error("Error in E-loop Location:" + url + " | " + ex);

                        }


                    });

                });
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
        #endregion

    }
}
