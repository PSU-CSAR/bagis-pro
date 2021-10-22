using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bagis_pro.BA_Objects
{
    public class AGSPortalProperties
    {
        public static string PORTAL_ORGANIZATION = "USDA NRCS ArcGIS Online";
        public static string NWCC_NRCS_USER = "nwcc_nrcs";
        public bool IsSignedIn = false;
        public bool IsNrcsPortal = false;
        public bool IsNrcsUser = false;
        public string UserName = "";
    }
}
