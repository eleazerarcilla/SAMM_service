using SAMM.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
namespace SAMM.Extensions
{
    public class Helpers
    {
        Logger Log = LogManager.GetCurrentClassLogger();
        #region Distance Methods
        public double GetDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371; 
            var dLat = ToRadians(lat2 - lat1);  
            var dLon = ToRadians(lon2 - lon1);
            var a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            var d = R * c; // Distance in km
            return d;
        }
        public double ToRadians(double deg)
        {
            return deg * (Math.PI / 180);
        }
        #endregion
        #region URL Methods
        public string GenerateURL(string TraccarURI, int lastctr)
        {
            string res = string.Empty;
            try
            {
                string TraccarAPI = TraccarURI;
                TraccarAPI += "positions?_dc" + GeneratePosQS(lastctr);
                res = TraccarAPI;
            }
            catch (Exception ex)
            {
                //ignored;
                Log.Error(ex);
            }
            return res;
        }
        public int GeneratePosQS(int prevctr)
        {
            return prevctr++;
        }
        public string GenerateWEBURL(LatLngModel LatLng, string GMURL, string DefZoomLvl)
        {
            string res = string.Empty;
            try
            {
                GMURL += "maps?f=q&q=" + LatLng.Lat + "," + LatLng.Lng + "&z=" + DefZoomLvl;
                res = GMURL;
            }
            catch (Exception ex)
            {
                //ignored
                Log.Error(ex);
            }
            return res;
        }
        #endregion
    }
}
