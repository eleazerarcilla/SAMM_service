using System;
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
        List<Eloop> VehicleList = new List<Eloop>();
        List<DestinationModel> _DestList;
        Boolean _isReportGenerated = false;

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
                GetVehicles(Constants.GPSProviderURL);
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
            BeginReportGenerator();
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

                foreach (PositionModel position in positions)
                {
                    LatLngModel currentLatLng = new LatLngModel { deviceid = Convert.ToInt32(position.deviceId), Lat = Convert.ToDouble(position.latitude), Lng = Convert.ToDouble(position.longitude) };
                    LatLngModel existingLatLng = LatLngList.FirstOrDefault(x => x.deviceid == currentLatLng.deviceid);
                    bool IsLatLngExisting = existingLatLng == null ? false : true;

                    if (!IsLatLngExisting)
                    {
                        String defaultRouteIDs = Constants.DefaultRouteIDs;
                        Eloop eloop = VehicleList.Where(x => x.DeviceID.ToString() == position.deviceId).Select(x=>x).FirstOrDefault();
                        if (eloop!=null)
                        {
                            List<String> routeIDs = DestList.Where(x => x.LineID == eloop.tblLinesID).Select(x => x.tblRouteID.ToString()).Distinct().ToList<String>();
                            defaultRouteIDs = String.Join(",", routeIDs);

                        }
                        currentLatLng.routeIDs = defaultRouteIDs;
                        currentLatLng.PrevLat = 0;
                        currentLatLng.PrevLng = 0;
                        LatLngList.Add(currentLatLng);

                    }
                    else
                    {
                        if (existingLatLng.enteredStation == null)
                            existingLatLng.enteredStation = "";
                        bool isparked = (help.GetDistance(Constants.DepotLat, Constants.DepotLng, currentLatLng.Lat, currentLatLng.Lng) <= (Constants.MainTerminalGeoFenceRadiusInKM)) ? true : false;
                        currentLatLng.IsParked = isparked;
                        currentLatLng.enteredStation = existingLatLng.enteredStation;
                        DestinationModel stationEntered = IsWithinStation(currentLatLng);
                        int OrderOfArrival = stationEntered.OrderOfArrival;
                        
                        
                        if (stationEntered.Value != null && stationEntered.Value != "")
                        {
                            currentLatLng.enteredStation = stationEntered.Value;
                            currentLatLng.isDwelling = true;
                        }
                        else
                        {
                            currentLatLng.isDwelling = false;
                        }
                            
                        currentLatLng.routeIDs = existingLatLng.routeIDs;
                        if (OrderOfArrival != 100)
                        {
                            //currentLatLng.routeIDs = getRouteIDsBasedOnStationWhereEloopIs(stationEntered);
                            if (!existingLatLng.enteredStation.Equals(currentLatLng.enteredStation, StringComparison.OrdinalIgnoreCase))
                            {
                                currentLatLng.routeIDs = getRouteIDsBasedOnPreviousAndCurrentStationOfEloop(existingLatLng.enteredStation, currentLatLng.enteredStation);
                                //save history here
                                insertVehicleGeofenceRecord(stationEntered.Value, position.deviceId, "Entered");
                               
                            }
                            else
                                currentLatLng.routeIDs = existingLatLng.routeIDs;
                        }

                        currentLatLng.LatestStationOA = OrderOfArrival == 100 ? currentLatLng.LatestStationOA : OrderOfArrival;
                        
                        currentLatLng.Name = DevicesList.Where(x => x.id == currentLatLng.deviceid).Select(x => x.name).First().ToString();
                        if (!UpdateLatLng(currentLatLng))
                        {
                            Log.Error("----ERROR Updating LatLng!----");
                        }
                    }
                };


                string Error = string.Empty;
                //SAMM.exe:


                PushToFirebase(LatLngList.ToList(), FireBaseURL, FireBaseAuth);
                Log.Info("----OVERALL Tasks (E-loop) completed " + s.Elapsed);

                //SAMM_2.exe:
                //StationLocModelList = CheckStations(LatLngList, DestList);

                //PushStationUpdatesToFirebase(CheckStations(LatLngList, DestList));
                //Log.Info("----OVERALL Tasks (Station) completed " + s.Elapsed);

                s.Stop();

            }

            catch (Exception ex)
            {
                Log.Error(ex);
            }


        }

        private async void insertVehicleGeofenceRecord(String destinationValue, String tblGPSID, String action)
        {
            await Task.Run(() =>
            {
                try
                {
                    Log.Info("Inserting Vehicle Geofence Record | Parameters: destinationValue=" + destinationValue
                        + ", tblGPSID=" + tblGPSID
                        + ", action=" + action);
                    HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(Constants.InsertVehicleGeofenceRecordURL
                    + "destinationValue=" + destinationValue
                    + "&tblGPSID=" + tblGPSID
                    + "&Action=" + action);
                    WebResponse response = webRequest.GetResponse();
                    response.Close();
                }
                catch (Exception ex)
                {
                    Log.Error("Error in E-loop Location: Inserting Vehicle Geofence Record | " + ex);

                }

            });
        }

        private String getRouteIDsBasedOnStationWhereEloopIs(DestinationModel station)
        {
            //Log.Info("Performing: getRouteIDsBasedOnStationWhereEloopIs");
            List<String> routeIDs = _DestList.Where(x => x.Value == (station.Value ?? "")).Select(x => x.tblRouteID.ToString()).ToList<String>();
            return String.Join(",", routeIDs.ToArray());
        }
        public String getRouteIDsBasedOnPreviousAndCurrentStationOfEloop(String previousStation, String currentStation)
        {

            //Log.Info("Performing: getRouteIDsBasedOnStationWhereEloopIs");
            List<DestinationModel> nodesOfPreviousStation = _DestList.Where(x => x.Value == (previousStation ?? "")).Select(x => x).ToList<DestinationModel>();
            List<DestinationModel> nodesOfCurrentStation = _DestList.Where(x => x.Value == (currentStation ?? "")).Select(x => x).ToList<DestinationModel>();
            List<DestinationModel> finalNode = new List<DestinationModel>();


            foreach (DestinationModel nodePrevious in nodesOfPreviousStation)
            {
                int maxOrderOfArrival = _DestList.Where(x => x.tblRouteID == nodePrevious.tblRouteID).Max(x => x.OrderOfArrival);
                int minOrderOfArrival = _DestList.Where(x => x.tblRouteID == nodePrevious.tblRouteID).Min(x => x.OrderOfArrival);
                foreach (DestinationModel nodeCurrent in nodesOfCurrentStation)
                {   
                    if (nodePrevious.OrderOfArrival + 1 == nodeCurrent.OrderOfArrival && nodePrevious.tblRouteID == nodeCurrent.tblRouteID)
                        finalNode.Add(nodeCurrent);
                    if (maxOrderOfArrival == nodePrevious.OrderOfArrival && nodeCurrent.OrderOfArrival == minOrderOfArrival && nodePrevious.tblRouteID == nodeCurrent.tblRouteID)
                        finalNode.Add(nodeCurrent);
                }
            }
            List<String> routeIDs = new List<String>();
            
            if (finalNode.Count > 0)
            {
                routeIDs = finalNode.Select(x => x.tblRouteID.ToString()).Distinct().ToList<String>();
            }
            else
            {
                routeIDs = _DestList.Where(x => x.Value == (currentStation ?? "")).Select(x => x.tblRouteID.ToString()).Distinct().ToList<String>();
            }
            //if (previousStation != null)
            //    if (previousStation.Contains("PlazaBBuilding")&& previousStation != currentStation)
            //    {
            //        aTimer.Stop();
            //        Debugger.Launch();
            //    }

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
                _entry.IsParked = Latlng.IsParked;
                _entry.PrevStationOA = (_entry.LatestStationOA == _entry.PrevStationOA) ? _entry.PrevStationOA : _entry.LatestStationOA;
                _entry.LatestStationOA = Latlng.LatestStationOA;
                _entry.enteredStation = Latlng.enteredStation;
                _entry.isDwelling = Latlng.isDwelling;
                _entry.Name = Latlng.Name;
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
                        bool IsExisting = StationLocModelList.FirstOrDefault(x => x.Destination.Value == DestinationListEntry.Value && x.Destination.tblRouteID == DestinationListEntry.tblRouteID) == null ? false : true;

                        bool isMainTerminal = DestinationListEntry.Value.ToUpper().Contains("MAIN") ? true : false;
                        bool isPlazaBBuilding = DestinationListEntry.Value.ToUpper().Contains("PLAZAB") ? true : false;
                        Double permiter = Constants.GeoFenceRadiusInKM;
                        if (isMainTerminal)
                            permiter = Constants.MainTerminalGeoFenceRadiusInKM;
                        if (isPlazaBBuilding)
                            permiter = Constants.PlazaBBuildingGeoFenceRadiusInKM;

                        if (help.GetDistance(DestinationListEntry.Lat, DestinationListEntry.Lng, LatLngListEntry.Lat, LatLngListEntry.Lng) <= permiter)
                        {


                            //if (StationLocModel.LoopIds == string.Empty)
                            if (LatLngListEntry.deviceid != 0)
                                loopIds += LatLngListEntry.deviceid.ToString() + ",";
                            //else
                            //    if (LatLngListEntry.deviceid != 0)
                            //    loopIds += LatLngListEntry.deviceid.ToString();

                            //loopIds += (StationLocModel.LoopIds == string.Empty ?
                            //    (LatLngListEntry.deviceid != 0 ?
                            //        LatLngListEntry.deviceid.ToString() + ","
                            //        : string.Empty) :
                            //    (LatLngListEntry.deviceid != 0 ?
                            //        LatLngListEntry.deviceid.ToString() : string.Empty));

                            if (!IsExisting)
                            {
                                StationLocModel.Dwell = loopIds;
                                StationLocModelList.Add(StationLocModel);
                            }
                            else
                                UpdateExistingStationLocationModel(StationLocModelList, LatLngListEntry.deviceid.ToString(), DestinationListEntry, iteratorID, true);
                        } 
                        else if (help.GetDistance(Constants.DepotLat, Constants.DepotLng, LatLngListEntry.Lat, LatLngListEntry.Lng) <= (Constants.MainTerminalGeoFenceRadiusInKM))
                        {
                            RemoveLoopIDsfromDestinationWhenTheyEnteredMainDepot(StationLocModelList, LatLngListEntry.deviceid.ToString());
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

                List<StationLocationModel> ExistingRecordList = SLModelList.Where(x => !x.Destination.Value.Equals(DestModel.Value, StringComparison.OrdinalIgnoreCase) && x.Destination.tblRouteID != DestModel.tblRouteID)
                    .Where(y => y.Dwell.Split(',').Contains(loopids)).ToList();

                foreach (StationLocationModel entry in ExistingRecordList)
                {
                    List<String> dwellList = new List<String>(entry.Dwell.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries));
                    dwellList.Remove(loopids);
                    entry.Dwell = String.Join(",", dwellList.ToArray()) + ",";
                }
                ExistingRecordList = SLModelList.Where(x => !x.Destination.Value.Equals(DestModel.Value, StringComparison.OrdinalIgnoreCase) && x.Destination.tblRouteID != DestModel.tblRouteID)
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
        public void RemoveLoopIDsfromDestinationWhenTheyEnteredMainDepot(List<StationLocationModel> SLModelList, string deviceID)
        {
            try
            {

                List<StationLocationModel> ExistingRecordList = SLModelList.Where(y => y.Dwell.Split(',').Contains(deviceID)).ToList();

                foreach (StationLocationModel entry in ExistingRecordList)
                {
                    List<String> dwellList = new List<String>(entry.Dwell.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries));
                    dwellList.Remove(deviceID);
                    entry.Dwell = String.Join(",", dwellList.ToArray()) + ",";
                }
                ExistingRecordList = SLModelList.Where(y => y.LoopIds.Split(',').Contains(deviceID)).ToList();
                foreach (StationLocationModel entry in ExistingRecordList)
                {
                    List<String> loopIdsList = new List<String>(entry.LoopIds.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries));
                    loopIdsList.Remove(deviceID);
                    entry.LoopIds = String.Join(",", loopIdsList.ToArray()) + ",";
                }
            }
            catch(Exception ex)
            {
                Log.Error(ex.Message);
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
            //Created a list because there could be more than 1 entered station because we increased the geofence
            List<DestinationModel> enteredStations = new List<DestinationModel>();
            //Log.Info("Performing: IsWithinStation");
            DestinationModel result = new DestinationModel();
            int lineIDofVehicle = 0;

            try
            {
                
                lineIDofVehicle = VehicleList.Where(x => x.DeviceID == LatLngEntry.deviceid).Select(x => x.tblLinesID).First();
            } catch (Exception ex) { }
            
            try
            {
                Parallel.ForEach(_DestList, (entry, loopState) =>
                {
                if (entry.LineID == (lineIDofVehicle == 0 ? entry.LineID:lineIDofVehicle))
                {

                    bool isMainTerminal = entry.Value.ToUpper().Contains("MAIN") ? true : false;
                    bool isPlazaBBuilding = entry.Value.ToUpper().Contains("PLAZAB") ? true : false;
                        bool isSouthStation = entry.Value.ToUpper().Contains("SOUTHSTATIONTERMINAL") ? true : false;
                    Double perimeter = Constants.GeoFenceRadiusInKM;
                    if (isMainTerminal)
                        perimeter = Constants.MainTerminalGeoFenceRadiusInKM;
                    if (isPlazaBBuilding)
                        perimeter = Constants.PlazaBBuildingGeoFenceRadiusInKM;
                    if (isSouthStation)
                        perimeter = Constants.SouthStationGeoFenceRadiusInKM;
                    Double distanceFromStation = help.GetDistance(entry.Lat, entry.Lng, LatLngEntry.Lat, LatLngEntry.Lng);
                    if (distanceFromStation <= perimeter)
                    {
                        entry.distanceFromStation = distanceFromStation;
                        enteredStations.Add(entry);
                        //result = entry;
                        //loopState.Break();
                    }
                }
                });
                if (enteredStations.Count>0)
                {
                    result = enteredStations.Select(x => x).OrderBy(x => x.distanceFromStation).FirstOrDefault();
                    if (LatLngEntry.enteredStation.ToUpper().Contains("CAPITALONE") && result.Value.ToUpper().Contains("BELLEVUE"))
                    {
                        result = _DestList.Select(x => x).Where(x => x.Value.ToUpper().Contains("CONVERGYS")).FirstOrDefault();
                    }
                    else if (LatLngEntry.enteredStation.ToUpper().Contains("VIVEREHOTEL") && result.Value.ToUpper().Contains("FRONTOFVIVERE"))
                    {
                        result = new DestinationModel();
                    }
                }
                return result;

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
            try { 
                
                String jsonString = "{";
                foreach (LatLngModel LatLng in LatLngList)
                {
                    if (!IsDeviceOnline(LatLng.deviceid))
                    {
                        LatLng.enteredStation = "";
                        LatLng.isDwelling = false;
                    }
                    if (LatLng.IsParked)
                        LatLng.enteredStation = "";

                    jsonString += "\"" + LatLng.deviceid
                        + "\":{\"Name\":\"" + LatLng.Name
                        + "\",\"Lat\":\"" + LatLng.Lat
                        + "\",\"Lng\":\"" + LatLng.Lng
                        + "\",\"PrevLat\":\"" + LatLng.PrevLat
                        + "\",\"PrevLng\":\"" + LatLng.PrevLng
                        + "\",\"deviceid\":\"" + LatLng.deviceid
                        + "\",\"IsParked\":\"" + LatLng.IsParked
                        + "\",\"LatestStationOA\":\"" + LatLng.LatestStationOA
                        + "\",\"PrevStationOA\":\"" + LatLng.PrevStationOA
                        + "\",\"EnteredStation\":\"" + LatLng.enteredStation
                        + "\",\"IsDwelling\":\"" + LatLng.isDwelling
                        + "\",\"routeIDs\":\"" + LatLng.routeIDs + "\"},";

                    

                    
                  

                }
                
                jsonString = jsonString.Substring(0, jsonString.Length - 1);
                jsonString += "}";



                //Log.Info(jsonString);
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


        public async void GetVehicles(string URL)
        {
            Log.Info("Performing: GetVehicles | Parameters: URL=" + URL);
            await Task.Run(() =>
            {
                try
                {

                    string GETResult = string.Empty;
                    VehicleList.Clear();
                    Stopwatch s = new Stopwatch();
                    s.Start();
                    HttpWebRequest httpRequest = (HttpWebRequest)WebRequest.Create(URL);
                    httpRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                    HttpWebResponse httpResponse = (HttpWebResponse)httpRequest.GetResponse();
                    using (StreamReader streamReader = new StreamReader(httpResponse.GetResponseStream()))
                    {
                        GETResult = streamReader.ReadToEnd();
                        VehicleList = JsonConvert.DeserializeObject<List<Eloop>>(GETResult);
                        streamReader.Close();
                    }
                    httpResponse.GetResponseStream().Close();
                    
                    s.Stop();
                    //  Log.Info("Stations query finished in:" + s.Elapsed);
                    //Log.Info("Found (" + DevicesList.Count() + ") device" + (DevicesList.Count() > 1 ? "s." : "."));
                    //foreach(Eloop eloop in VehicleList)
                    //{
                    //    Log.Info(eloop.DeviceName);
                    //}
                  
                }
                catch (Exception ex)
                {
                    Log.Error(ex);

                }
            }
            );
        }

        public Dictionary<String, DriversModel> GetCurrentDriversNode(string URL)
        {
            Log.Info("Performing: GetCurrentDriversNode | Parameters: URL=" + URL);
            Dictionary<String, DriversModel> DriversNode = new Dictionary<string, DriversModel>();
            
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
                    DriversNode = JsonConvert.DeserializeObject<Dictionary<String,DriversModel>>(GETResult);
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
            
            return DriversNode;
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
                    httpRequest.Method = "PATCH";
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
                httpRequest.Method = "PATCH";
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
                foreach (StationLocationModel model in StationLocModel)
                {

                    jsonString += "\"" + model.Destination.Value + "_" + model.Destination.tblRouteID + "\":{\"tblRouteID\":\"" + model.Destination.tblRouteID + "\",\"OrderOfArrival\":\""
                        + model.OrderOfArrival + "\",\"LoopIds\":\"" + model.LoopIds + "\",\"Dwell\":\"" + model.Dwell + "\"},";

                }
                jsonString = jsonString.Substring(0, jsonString.Length - 1);
                jsonString += "}";
                //Log.Info(jsonString);
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
        private void BeginReportGenerator()
        {

            DateTime currentDate = DateTime.Now;
            Log.Info("Started BeginReportGenerator | currentDate.Hour = " 
                + currentDate.Hour.ToString() 
                + ", currentDate.Minute = " 
                + currentDate.Minute.ToString());
            try
            {
                
                if (currentDate.Hour == Constants.ReportGeneratorHour && currentDate.Minute == Constants.ReportGeneratorMinute && !_isReportGenerated)
                {
                    //Call here all report-generating methods
                    saveVehicleSummaryReport();
                    _isReportGenerated = true;
                }
                if (currentDate.Hour == Constants.ReportGeneratorHour && currentDate.Minute == Constants.ReportGeneratorMinute + 1 && _isReportGenerated)
                {
                    _isReportGenerated = false;
                }

            }
            catch (Exception ex)
            {
                Log.Error("Error in BeginReportGenerator");
            }
            
          
        }
        private void saveVehicleSummaryReport()
        {
            try
            {
                Log.Info("Started saveVehicleSummaryReport");
                HttpWebRequest httpRequest = (HttpWebRequest)WebRequest.Create(Constants.VehicleSummaryReportGeneratorURL);

                /***Comment the 2 lines of code below when using dummy e-loop***/

                /*******************************************/

                httpRequest.Accept = "application/json";
                List<Logger> logs = new List<Logger>();
                HttpWebResponse httpResponse = (HttpWebResponse)httpRequest.GetResponse();
            }
            catch(Exception ex)
            {
                Log.Error("Error in saveVehicleSummaryReport: " + ex.Message);
            }
            
        }
        #endregion

    }
}

