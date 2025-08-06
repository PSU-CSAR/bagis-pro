using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bagis_pro.BA_Objects
{
    public class DataSource
    {
        public string uri;
        public string description;
        public DateTime DateClipped;
        public string units;
        public double minValue;
        public double maxValue;
        public string layerType;
        public string heading;
        public string shortDescription;

        public DataSource()
        {

        }

        public DataSource(dynamic dynSource)
        {
            uri = dynSource.uri;
            description = dynSource.description;
            units = dynSource.units;
            layerType = dynSource.layerType;
            heading = dynSource.heading;
            shortDescription = dynSource.shortDescription;
            if (dynSource.DateClipped != null) 
                DateClipped = dynSource.DateClipped;
        }

        public static JObject DataSourceJson(dynamic oDataSource)
        {
            dynamic newDataSource = new JObject();
            newDataSource.uri = oDataSource.uri;
            newDataSource.description = oDataSource.description;
            newDataSource.layerType = oDataSource.layerType;
            newDataSource.heading = oDataSource.heading;
            //newDataSource.dateClippedText = DateTime.Now.ToString("MMMM d, yyyy");
            newDataSource.DateClipped = DateTime.Now;
            return newDataSource;
        }

        public string DateClippedText
        {
            get
            {
                return DateClipped.ToString("MMMM d, yyyy");
            }
            set
            { /*Do nothing; Required for serialization */}
        }

        public static string GetDemKey
        {
            get
            {
                switch (Module1.Current.DataSourceGroup)
                {
                    case Constants.DATA_SOURCES_ALASKA:
                        return Constants.DATA_TYPE_ALASKA_DEM;
                    default:
                        return Constants.DATA_TYPE_DEM;
                }
            }

        }

        public static string GetPrecipitationKey
        {
            get
            {
                switch (Module1.Current.DataSourceGroup)
                {
                    case Constants.DATA_SOURCES_ALASKA:
                        return Constants.DATA_TYPE_ALASKA_PRECIPITATION;
                    default:
                        return Constants.DATA_TYPE_PRECIPITATION;
                }
            }

        }
        public static string GetLandCoverKey
        {
            get
            {
                switch (Module1.Current.DataSourceGroup)
                {
                    case Constants.DATA_SOURCES_ALASKA:
                        return Constants.DATA_TYPE_ALASKA_LAND_COVER;
                    default:
                        return Constants.DATA_TYPE_LAND_COVER;
                }
            }

        }
        public static string GetFireBurnSeverityKey
        {
            get
            {
                switch (Module1.Current.DataSourceGroup)
                {
                    case Constants.DATA_SOURCES_ALASKA:
                        return Constants.DATA_TYPE_ALASKA_FIRE_BURN_SEVERITY;
                    default:
                        return Constants.DATA_TYPE_FIRE_BURN_SEVERITY;
                }
            }
        }
    }
}
