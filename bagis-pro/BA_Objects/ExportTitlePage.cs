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
        public DateTime date_created;

        public string DateCreatedText
        {
            get
            {
                TimeZoneInfo zone = TimeZoneInfo.Local;
                string strDate = date_created.ToString("MMMM d, yyyy a\t h:mm tt ");
                return strDate + zone.DisplayName;
            }
            set
            { /*Do nothing; Required for serialization */}
        }

    }
}
