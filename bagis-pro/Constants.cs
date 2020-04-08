using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bagis_pro
{
    static class Constants
    {
        public const string MAPS_DEFAULT_LAYOUT_NAME = "Basin Analysis Layout";
        public const string MAPS_DEFAULT_MAP_FRAME_NAME = "Basin Analysis Map Frame";
        public const string MAPS_DEFAULT_MAP_NAME = "Basin Analysis";
        public const string MAPS_AOI_BOUNDARY = "AOI Boundary";
        public const string MAPS_STREAMS = "AOI Streams";
        public const string MAPS_SNOTEL = "SNOTEL Sites";
        public const string MAPS_SNOW_COURSE = "Snow Courses";
        public const string MAPS_HILLSHADE = "hillshade";
        public const string MAPS_ELEV_ZONE = "Elevation Zones";
        public const string MAPS_SLOPE_ZONE = "Slope Zones";
        public const string MAPS_ASPECT_ZONE = "Aspect Zones";
        public const string MAPS_PRISM_ZONE = "Precipitation Zones";
        public const string MAPS_SNOTEL_REPRESENTED = "SNOTEL Sites Represented Area";
        public const string MAPS_SNOW_COURSE_REPRESENTED = "Snow Course Sites Represented Area";
        public const string MAPS_ALL_SITES_REPRESENTED = "All Sites Represented Area";
        public const string MAPS_SNODAS_SWE_JAN = "January 1 SWE";

        public const string MAPS_LEGEND = "Legend";
        public const string MAPS_TITLE = "Title";
        public const string MAPS_SUBTITLE = "SubTitle";
        public const string MAPS_TEXTBOX1 = "TextBox1";

        public const string FILE_AOI_VECTOR = "aoi_v";
        public const string FILE_AOI_PRISM_VECTOR = "p_aoi_v";
        public const string FILE_AOI_RASTER = "aoibagis";
        public const string FILE_STREAMS = "aoi_streams";
        public const string FILE_SNOTEL = "snotel_sites";
        public const string FILE_SNOW_COURSE = "snowcourse_sites";
        public const string FILE_HILLSHADE = "hillshade";
        public const string FILE_DEM_FILLED = "dem_filled";
        public const string FILE_ELEV_ZONE = "elevzone";
        public const string FILE_SLOPE_ZONE = "slpzone";
        public const string FILE_ASPECT_ZONE = "aspzone";
        public const string FILE_PRECIP_ZONE = "preczone";
        public const string FILE_SNOTEL_ZONE = "stelzone";
        public const string FILE_SCOS_ZONE = "scoszone";
        //public const string FILE_SNOTEL_REPRESENTED = "npactual";
        public const string FILE_SNOTEL_REPRESENTED = "snotel_rep";
        //public const string FILE_SCOS_REPRESENTED = "nppseduo";
        public const string FILE_SCOS_REPRESENTED = "scos_rep";
        public const string FILE_SITES_REPRESENTED = "sites_rep";
        public const string FILE_POURPOINT = "pourpoint";
        public const string FILE_MAP_PARAMETERS = "map_parameters.json";
        public const string FILE_EXPORT_MAP_ELEV_PDF = "map_elevation.pdf";
        public const string FILE_EXPORT_MAP_SNOTEL_PDF = "map_elevation_snotel.pdf";
        public const string FILE_EXPORT_MAP_SCOS_PDF = "map_elevation_sc.pdf";
        public const string FILE_EXPORT_MAP_SNOTEL_AND_SCOS_PDF = "map_elevation_snotel_sc.pdf";
        public const string FILE_EXPORT_MAP_PRECIPITATION_PDF = "map_precipitation.pdf";
        public const string FILE_EXPORT_MAP_ASPECT_PDF = "map_aspect.pdf";
        public const string FILE_EXPORT_MAP_SLOPE_PDF = "map_slope.pdf";
        public const string FILE_EXPORT_MAP_ELEVATION_SNOTEL_PDF = "map_elevation_snotel.pdf";
        public const string FILE_EXPORT_MAP_SWE_JANUARY_PDF = "map_snodas_swe_january.pdf";
        public static readonly string[] FILE_EXPORT_MAPS_SWE = new string[] { FILE_EXPORT_MAP_SWE_JANUARY_PDF, "map_snodas_swe_february.pdf", "map_snodas_swe_march.pdf", 
                                                                              "map_snodas_swe_april.pdf", "map_snodas_swe_may.pdf", "map_snodas_swe_june.pdf",
                                                                              "map_snodas_swe_december.pdf"};
        public const string FILE_EXPORT_MAPS_ALL_PDF = "all_maps_charts.pdf";
        public const string FILE_TITLE_PAGE_XSL = "title_page.xsl";
        public const string FILE_TITLE_PAGE_XML = "title_page.xml";
        public const string FILE_TITLE_PAGE_HTML = "title_page.html";
        public const string FILE_TITLE_PAGE_PDF = "title_page.pdf";
        public static readonly string[] URIS_SNODAS_SWE = new string[] { "daily_swe_normal_jan_01", "daily_swe_normal_feb_01", "daily_swe_normal_mar_01",
                                                                         "daily_swe_normal_apr_01", "daily_swe_normal_may_01", "daily_swe_normal_jun_01",
                                                                         "daily_swe_normal_dec_01"};
        public static readonly string[] FILES_SNODAS_SWE = new string[] { "swe_jan_01", "swe_feb_01", "swe_mar_01",
                                                                          "swe_apr_01", "swe_may_01", "swe_jun_01",
                                                                          "swe_dec_01"};
        public static readonly string[] LAYER_NAMES_SNODAS_SWE = new string[] { MAPS_SNODAS_SWE_JAN, "February 1 SWE", "March 1 SWE", "April 1 SWE",
                                                                                "May 1 SWE", "June 1 SWE", "December 1 SWE" };
        public static readonly string[] MAP_TITLES_SNODAS_SWE = new string[] { "SNODAS SWE JAN 1ST", "SNODAS SWE FEB 1ST", "SNODAS SWE MAR 1ST",
                                                                               "SNODAS SWE APR 1ST", "SNODAS SWE MAY 1ST", "SNODAS SWE JUN 1ST",
                                                                               "SNODAS SWE DEC 1ST"};
        public const string URI_IMAGE_SERVER = "/ImageServer";


        public const string FOLDER_MAP_PACKAGE = "maps_publish";
        public const string FOLDER_MAPS = "maps";

        public const string FIELD_AOI_AREA = "AOISHPAREA";
        public const string FIELD_SITE_ELEV = "BA_SELEV";
        public const string FIELD_SITE_NAME = "BA_SNAME";
        public const string FIELD_OBJECT_ID = "OBJECTID";
        public const string FIELD_STATION_TRIPLET = "stationTriplet";
        public const string FIELD_STATION_NAME = "stationName";
        public const string FIELD_AWDB_ID = "AWDB_ID";
        public const string FIELD_USGS_ID = "USGS_ID";
        public const string FIELD_NEAR_ID = "NEAR_FID";
        public const string FIELD_NEAR_DIST = "NEAR_DIST";
        public const string FIELD_VALUE = "VALUE";
        public const string FIELD_NAME = "NAME";
        public const string FIELD_LBOUND = "LBOUND";
        public const string FIELD_UBOUND = "UBOUND";
        public const string FIELD_COUNT = "COUNT";

        public const string DATA_TYPE_SWE = "Snotel SWE";
        public const string DATA_TYPE_PRECIPITATION = "Precipitation";

        public const int VALUE_NO_DATA_9999 = -9999;
        public const string VALUE_NO_DATA = "NoData";
        public const string VALUE_UNKNOWN = "Unknown";

        public const string UNITS_INCHES = "Inches";
        public const string UNITS_MILLIMETERS = "Millimeters";
        public const string UNITS_FEET = "Feet";
        public const string UNITS_METERS = "Meters";

        // States that control the map display buttons
        // JanSwe always needs to be last so that we can export the other months directly
        public static string[] STATES_MAP_BUTTON => new string[] {"MapButtonPalette_BtnElevation_State",
                                                                "MapButtonPalette_BtnSnotel_State",
                                                                "MapButtonPalette_BtnSnowCourse_State",
                                                                "MapButtonPalette_BtnSitesAll_State",
                                                                "MapButtonPalette_BtnAspect_State",
                                                                "MapButtonPalette_BtnPrism_State",
                                                                "MapButtonPalette_BtnSlope_State",
                                                                "MapButtonPalette_BtnJanSwe_State"};

        public static string META_TAG_XPATH = @"/metadata/dataIdInfo/searchKeys/keyword";
        public static string META_TAG_PREFIX = "BAGIS Tag < Please do not modify: ";
        public static string META_TAG_SUFFIX = " > End Tag";
        public static string META_TAG_ZUNIT_CATEGORY = "ZUnitCategory|";
        public static string META_TAG_ZUNIT_VALUE = "ZUnit|";
        public static string META_TAG_BUFFER_DISTANCE = "BufferDistance|";
        public static string META_TAG_XUNIT_VALUE = "XUnit|";
        public static string META_TAG_CATEGORY_SNODAS = "Snotel SWE";
    }

}
