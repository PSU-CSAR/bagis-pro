using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bagis_pro.BA_Objects
{
    public class ExportTitlePage
    {
        public string aoi_name;
        public string publisher;
        public string comments;
        public string local_path;
        public string streamgage_station;
        public DateTime date_created;
        public double drainage_area_sqkm;
        public double elevation_min;
        public double elevation_max;
        public int snotel_sites_in_basin;
        public int snotel_sites_in_buffer;
        public string snotel_sites_buffer_size;

        public string DateCreatedText
        {
            get
            {
                TimeZoneInfo zone = TimeZoneInfo.Local;
                string strDate = date_created.ToString("MMMM d, yyyy a\\t h:mm tt ");
                return strDate + zone.DisplayName;
            }
            set
            { /*Do nothing; Required for serialization */}
        }

        public double drainage_area_sqmi
        {
            get
            {
                double dblArea = ArcGIS.Core.Geometry.AreaUnit.SquareKilometers.ConvertTo(drainage_area_sqkm, ArcGIS.Core.Geometry.AreaUnit.SquareMiles);
                return Math.Round(dblArea, 2, MidpointRounding.AwayFromZero);
            }
            set
            { /*Do nothing; Required for serialization */}
        }

    }
}
