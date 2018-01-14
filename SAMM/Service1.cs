using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using System.Configuration;

namespace SAMM
{
    partial class Service1 : ServiceBase
    {
        private System.Timers.Timer aTimer;
        int lastctr = 0;
        Logger Log = LogManager.GetCurrentClassLogger();
        List<LatLngModel> LatLngList = new List<LatLngModel>();
        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            int interval = Convert.ToInt32(ConfigurationManager.AppSettings["TimerIntervalInSeconds"]) * 1000;
            aTimer = new System.Timers.Timer(interval);
            aTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
            aTimer.Interval = interval;
            aTimer.Enabled = true;
        }
        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            PerformGETCallback();
        }

        protected override void OnStop()
        {
        }

        public void PerformGETCallback()
        {
            try
            {
                string resultOfPost = string.Empty;
                string url = GenerateURL();
                //initialize
                HttpWebRequest httpRequest = (HttpWebRequest)WebRequest.Create(url);
                httpRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                httpRequest.Credentials = new NetworkCredential("eleazer.arcilla@yahoo.com.ph", "eleazgpsdemo");
                List<PositionModel> pos = new List<PositionModel>();
                List<Logger> logs = new List<Logger>();
                HttpWebResponse httpResponse = (HttpWebResponse)httpRequest.GetResponse();
                using (StreamReader streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    resultOfPost = streamReader.ReadToEnd();
                    pos = JsonConvert.DeserializeObject<List<PositionModel>>(resultOfPost);
                    streamReader.Close();
                }
                httpResponse.GetResponseStream().Close();
                httpResponse.GetResponseStream().Flush();
                foreach (var entry in pos)
                {
                    LatLngModel _LatLng = new LatLngModel { deviceid = Convert.ToInt32(entry.deviceId), Lat = Convert.ToDouble(entry.latitude), Lng = Convert.ToDouble(entry.longitude) };
                    if (LatLngList.Where(x => x.deviceid == _LatLng.deviceid).Count() == 0)
                    {
                        LatLngList.Add(_LatLng);
                    }
                    Log.Info(GenerateWEBURL(_LatLng) + PushToFirebase(_LatLng) + " GPSDeviceCount ("+LatLngList.Count()+")");
                }
                //Log.Info(GenerateWEBURL(LatLngList) + PushToFirebase(LatLngList));

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

        public string GenerateURL()
        {
            string res = string.Empty;
            try
            {
                string TraccarAPI = ConfigurationManager.AppSettings["traccarwebapiURL"];
                TraccarAPI += "positions?_dc" + GeneratePosQS();
                res = TraccarAPI;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            return res;
        }
        public int GeneratePosQS()
        {
            return lastctr++;
        }
        public string GenerateWEBURL(LatLngModel LatLng)
        {
            string res = string.Empty;
            string GMURL = ConfigurationManager.AppSettings["GMbaseURL"];
            string DefZoomLvl = ConfigurationManager.AppSettings["DefaultZoomLevel"];
            try
            {
                GMURL += "maps?f=q&q=" + LatLng.Lat + "," + LatLng.Lng + "&z=" + DefZoomLvl;
                res = GMURL;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            return res;
        }

        public string PushToFirebase(LatLngModel LatLngList)
        {
            string res = string.Empty;
            string FireBaseURL = ConfigurationManager.AppSettings["FirebaseURL"];
            string FireBaseAuth = ConfigurationManager.AppSettings["FirebaseAUTH"];
            string url = string.Empty;
            try
            {
                Stopwatch s = new Stopwatch();
                s.Start();
                url = FireBaseURL + LatLngList.deviceid + "/.json?auth=" + FireBaseAuth;
                string resultOfPost = string.Empty;

                HttpWebRequest httpRequest = (HttpWebRequest)WebRequest.Create(url);
                httpRequest.Method = "PATCH";
                httpRequest.ContentType = "application/json";

                var buffer = Encoding.UTF8.GetBytes(CreateJson(LatLngList));
                httpRequest.ContentLength = buffer.Length;
                httpRequest.GetRequestStream().Write(buffer, 0, buffer.Length);
                var response = httpRequest.GetResponse();

                response.Close();
                httpRequest.GetRequestStream().Close();
                httpRequest.GetRequestStream().Flush();
                res = " " + url + " |  Success - " + s.Elapsed;
                s.Stop();


            }
            catch (Exception ex)
            {
                Log.Error("Error:" + url + " | " + ex);

            }
            return res;
        }

        public string CreateJson(LatLngModel Latlng)
        {
            string res = string.Empty;
            try
            {
                Latlng = UpdateLatLng(Latlng);
                res = JsonConvert.SerializeObject(new LatLngModel
                {
                    Lat = Latlng.Lat,
                    Lng = Latlng.Lng,
                    PrevLat = Latlng.PrevLat,
                    PrevLng = Latlng.PrevLng,
                    deviceid = Latlng.deviceid
                });

            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            return res;
        }
        public LatLngModel UpdateLatLng(LatLngModel Latlng)
        {
            LatLngModel res = new LatLngModel();
            try
            {
                LatLngModel _entry = LatLngList.First(x => x.deviceid == Latlng.deviceid);
                _entry.PrevLat = (_entry.Lat == _entry.PrevLat) ? _entry.PrevLat : _entry.Lat;
                _entry.PrevLng = (_entry.Lng == _entry.PrevLng) ? _entry.PrevLng : _entry.Lng;
                _entry.Lat = Latlng.Lat;
                _entry.Lng = Latlng.Lng;
                res = _entry;

            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            return res;
        }
    }
}
