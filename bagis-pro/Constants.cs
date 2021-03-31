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
        public const string MAPS_FEDERAL_PUBLIC_LAND_ZONES = "Federal Non-Wilderness Land";
        public const string MAPS_BELOW_TREELINE = "Area Below Treeline";
        public const string MAPS_SITES_LOCATION = "Meet All Criteria";
        public const string MAPS_CRITICAL_PRECIPITATION_ZONES = "Critical Precipitation";
        public const string MAPS_PUBLIC_LAND_OWNERSHIP = "Public Land Ownership";

        public const string MAPS_LEGEND = "Legend";
        public const string MAPS_TITLE = "Title";
        public const string MAPS_SUBTITLE = "SubTitle";
        public const string MAPS_TEXTBOX1 = "TextBox1";

        public const string FILE_AOI_VECTOR = "aoi_v";
        public const string FILE_AOI_BUFFERED_VECTOR = "aoib_v";
        public const string FILE_AOI_PRISM_VECTOR = "p_aoi_v";
        public const string FILE_AOI_RASTER = "aoibagis";
        public const string FILE_STREAMS = "aoi_streams";
        public const string FILE_SNOTEL = "snotel_sites";
        public const string FILE_SNOW_COURSE = "snowcourse_sites";
        public const string FILE_HILLSHADE = "hillshade";
        public const string FILE_DEM_FILLED = "dem_filled";
        public const string FILE_DEM = "dem";
        public const string FILE_ELEV_ZONE = "elevzone";
        public const string FILE_SLOPE_ZONE = "slpzone";
        public const string FILE_ASPECT_ZONE = "aspzone";
        public const string FILE_PRECIP_ZONE = "preczone";
        public const string FILE_SNOTEL_ZONE = "stelzone";
        public const string FILE_SCOS_ZONE = "scoszone";
        public const string FILE_ROADS_ZONE = "roadszone";
        public const string FILE_PUBLIC_LAND_ZONE = "publiclandszone";
        public const string FILE_BELOW_TREELINE_ZONE = "belowtreelinezone";
        public const string FILE_SITES_LOCATION_ZONE = "sitesloczone";
        //public const string FILE_SNOTEL_REPRESENTED = "npactual";
        public const string FILE_SNOTEL_REPRESENTED = "snotel_rep";
        //public const string FILE_SCOS_REPRESENTED = "nppseduo";
        public const string FILE_SCOS_REPRESENTED = "scos_rep";
        public const string FILE_SITES_REPRESENTED = "sites_rep";
        public const string FILE_POURPOINT = "pourpoint";
        public const string FILE_UNSNAPPED_POURPOINT = "unsnappedpp";
        public const string FILE_SLOPE = "slope";
        public const string FILE_ASPECT = "aspect";
        public const string FILE_ROADS = "fs_roads";
        public const string FILE_PUBLIC_LAND = "public_lands";
        public const string FILE_VEGETATION_EVT = "LF_us_140evt";
        public const string FILE_PREC_MEAN_ELEV = "precmeanelev";
        public const string FILE_ASP_ZONE_PREC = "aspzoneprec";
        public const string FILE_ASP_ZONE_PREC_TBL = "precmeanelev_tbl";
        public const string FILE_MERGED_SITES = "merged_sites";
        public const string FILE_PREC_STEL = "prec_stel";
        public const string FILE_ELEV_ZONES_TBL = "tblZones";
        public const string FILE_ELEV_ZONES_VECTOR = "elevzone_v";
        public const string FILE_CRITICAL_PRECIP_ZONE = "criticalprecipzone";
        public const string FILE_PUBLIC_LAND_OWNERSHIP = "public_land_ownership";
        public const string FILE_SETTINGS = "analysis.xml";
        public const string FILE_BATCH_TOOL_SETTINGS = "batch_tool_settings.json";
        public const string FILE_BATCH_LOG = "batch_tool_log.txt";
        public const string FILE_EXPORT_MAP_ELEV_PDF = "map_elevation.pdf";
        public const string FILE_EXPORT_MAP_SNOTEL_PDF = "map_elevation_snotel.pdf";
        public const string FILE_EXPORT_MAP_SCOS_PDF = "map_elevation_sc.pdf";
        public const string FILE_EXPORT_MAP_SNOTEL_AND_SCOS_PDF = "map_elevation_snotel_sc.pdf";
        public const string FILE_EXPORT_MAP_PRECIPITATION_PDF = "map_precipitation.pdf";
        public const string FILE_EXPORT_MAP_ASPECT_PDF = "map_aspect.pdf";
        public const string FILE_EXPORT_MAP_SLOPE_PDF = "map_slope.pdf";
        public const string FILE_EXPORT_MAP_ELEVATION_SNOTEL_PDF = "map_elevation_snotel.pdf";
        public static readonly string[] FILE_EXPORT_MAPS_SWE = new string[] { "map_snodas_swe_november.pdf", "map_snodas_swe_december.pdf", "map_snodas_swe_january.pdf",
                                                                              "map_snodas_swe_february.pdf", "map_snodas_swe_march.pdf", "map_snodas_swe_april.pdf",
                                                                              "map_snodas_swe_may.pdf", "map_snodas_swe_june.pdf", "map_snodas_swe_july.pdf"};
        public static readonly string[] FILE_EXPORT_MAPS_SWE_DELTA = new string[] { "map_swe_dec_minus_nov.pdf", "map_swe_jan_minus_dec.pdf", "map_swe_feb_minus_jan.pdf",
                                                                                    "map_swe_mar_minus_feb.pdf", "map_swe_apr_minus_mar.pdf", "map_swe_may_minus_apr.pdf",
                                                                                    "map_swe_jun_minus_may.pdf", "map_swe_jul_minus_jun.pdf"};
        public const string FILE_EXPORT_MAP_ROADS_PDF = "map_roads.pdf";
        public const string FILE_EXPORT_MAP_PUBLIC_LAND_ZONES_PDF = "map_public_land_zones.pdf";
        public const string FILE_EXPORT_MAP_BELOW_TREELINE_PDF = "map_below_treeline.pdf";
        public const string FILE_EXPORT_MAP_SITES_LOCATION_PDF = "map_sites_location.pdf";
        public const string FILE_EXPORT_MAP_CRITICAL_PRECIPITATION_ZONES_PDF = "map_critical_precip_zones.pdf";
        public const string FILE_EXPORT_MAP_PUBLIC_LAND_OWNERSHIP_PDF = "map_public_land_ownership.pdf";
        public const string FILE_EXPORT_CHART_AREA_ELEV_PRECIP_SITE_PDF = "chart_area_elev_precip_site.pdf";
        public const string FILE_EXPORT_CHART_SLOPE_PDF = "chart_slope.pdf";
        public const string FILE_EXPORT_CHART_ASPECT_PDF = "chart_aspect.pdf";
        public const string FILE_EXPORT_TABLE_PRECIP_REPRESENT_PDF = "table_precip_representation.pdf";
        public const string FILE_EXPORT_CHART_PRECIP_REPRESENT_PDF = "chart_precip_representation.pdf";
        public const string FILE_EXPORT_CHART_ELEV_PRECIP_CORR_PDF = "chart_elev_precip_correlation.pdf";
        public const string FILE_EXPORT_MAPS_ALL_PDF = "all_maps_charts.pdf";
        // List of files in the PDF map package and the order in which they will be assembled
        public static string[] FILES_EXPORT_ALL_PDF = new string[] { FILE_TITLE_PAGE_PDF, FILE_EXPORT_MAP_ELEV_PDF,
            FILE_EXPORT_MAP_SNOTEL_PDF, FILE_EXPORT_MAP_SCOS_PDF, FILE_EXPORT_MAP_SNOTEL_AND_SCOS_PDF,
            FILE_EXPORT_MAP_PRECIPITATION_PDF, FILE_EXPORT_MAPS_SWE[0], FILE_EXPORT_MAPS_SWE[1],
            FILE_EXPORT_MAPS_SWE[2], FILE_EXPORT_MAPS_SWE[3], FILE_EXPORT_MAPS_SWE[4],
            FILE_EXPORT_MAPS_SWE[5], FILE_EXPORT_MAPS_SWE[6], FILE_EXPORT_MAPS_SWE[7],
            FILE_EXPORT_MAPS_SWE[8], FILE_EXPORT_MAP_ASPECT_PDF,
            FILE_EXPORT_MAP_SLOPE_PDF, FILE_EXPORT_CHART_AREA_ELEV_PRECIP_SITE_PDF,
            FILE_EXPORT_CHART_PRECIP_REPRESENT_PDF, FILE_EXPORT_TABLE_PRECIP_REPRESENT_PDF,
            FILE_EXPORT_MAP_CRITICAL_PRECIPITATION_ZONES_PDF, FILE_EXPORT_CHART_SLOPE_PDF, FILE_EXPORT_CHART_ASPECT_PDF,
            FILE_EXPORT_CHART_ELEV_PRECIP_CORR_PDF, FILE_EXPORT_MAP_ROADS_PDF, FILE_EXPORT_MAP_PUBLIC_LAND_ZONES_PDF,
            FILE_EXPORT_MAP_BELOW_TREELINE_PDF, FILE_EXPORT_MAP_SITES_LOCATION_PDF, FILE_EXPORT_MAP_PUBLIC_LAND_OWNERSHIP_PDF,
            FILE_EXPORT_MAPS_SWE_DELTA[0], FILE_EXPORT_MAPS_SWE_DELTA[1], FILE_EXPORT_MAPS_SWE_DELTA[2],
            FILE_EXPORT_MAPS_SWE_DELTA[3], FILE_EXPORT_MAPS_SWE_DELTA[4]};
        public const string FILE_TITLE_PAGE_XSL = "title_page.xsl";
        public const string FILE_TITLE_PAGE_XML = "title_page.xml";
        public const string FILE_TITLE_PAGE_HTML = "title_page.html";
        public const string FILE_TITLE_PAGE_PDF = "title_page.pdf";
        public const string FILE_FLOW_ACCUMULATION = "flow_accumulation";
        public const string FILE_FLOW_DIRECTION = "flow_direction";
        public const string FILE_AOI_BUFFERED_RASTER = "aoib";
        public const string FILE_AOI_PRISM_RASTER = "p_aoi";
        public const string FILE_ANNUAL_RUNOFF_CSV = "annual_runoff_averages.csv";
        public static readonly string[] URIS_SNODAS_SWE = new string[] { "daily_swe_normal_nov_01", "daily_swe_normal_dec_01", "daily_swe_normal_jan_01",
                                                                         "daily_swe_normal_feb_01", "daily_swe_normal_mar_01", "daily_swe_normal_apr_01",
                                                                         "daily_swe_normal_may_01", "daily_swe_normal_jun_01", "daily_swe_normal_jul_01"};
        public const string FILE_SNODAS_SWE_APRIL = "swe_apr_01";
        public static readonly string[] FILES_SNODAS_SWE = new string[] { "swe_nov_01", "swe_dec_01", "swe_jan_01", "swe_feb_01", "swe_mar_01",
                                                                          FILE_SNODAS_SWE_APRIL, "swe_may_01", "swe_jun_01", "swe_jul_01"};
        public static readonly string[] LAYER_NAMES_SNODAS_SWE = new string[] { "November 1 SWE", "December 1 SWE", "January 1 SWE", "February 1 SWE",
                                                                                "March 1 SWE", "April 1 SWE", "May 1 SWE", "June 1 SWE",
                                                                                "July 1 SWE"};
        public static readonly string[] MAP_TITLES_SNODAS_SWE = new string[] { "SNODAS SWE NOV 1ST", "SNODAS SWE DEC 1ST", "SNODAS SWE JAN 1ST", "SNODAS SWE FEB 1ST",
                                                                               "SNODAS SWE MAR 1ST", "SNODAS SWE APR 1ST", "SNODAS SWE MAY 1ST",
                                                                               "SNODAS SWE JUN 1ST", "SNODAS SWE JUL 1ST" };
        public static readonly string[] FILES_SWE_DELTA = new string[] { "swe_dec_minus_nov", "swe_jan_minus_dec", "swe_feb_minus_jan",
                                                                         "swe_mar_minus_feb", "swe_apr_minus_mar", "swe_may_minus_apr",
                                                                         "swe_jun_minus_may", "swe_jul_minus_jun"};
        public static readonly string[] LAYER_NAMES_SWE_DELTA = new string[] { "Dec - Nov SWE", "Jan - Dec SWE", "Feb - Jan SWE", "Mar - Feb SWE",
                                                                               "Apr - Mar SWE", "May - Apr SWE", "Jun - May SWE", "Jul - Jun SWE" };
        public static readonly string[] MAP_TITLES_SWE_DELTA = new string[] { "DEC 01 - NOV 01 SWE CHANGE", "JAN 01 - DEC 01 SWE CHANGE", "FEB 01 - JAN 01 SWE CHANGE",
                                                                              "MAR 01 - FEB 01 SWE CHANGE", "APR 01 - MAR 01 SWE CHANGE", "MAY 01 - APR 01 SWE CHANGE",
                                                                              "JUN 01 - MAY 01 SWE CHANGE", "JUL 01 - JUN 01 SWE CHANGE"};
        public const string FILE_BAGIS_MAP_PARAMETERS = "map_parameters.txt";
        public const string URI_IMAGE_SERVER = "/ImageServer";
        public const string URI_DESKTOP_SETTINGS = "/api/rest/desktop/settings/";


        public const string FOLDER_MAP_PACKAGE = "maps_publish";
        public const string FOLDER_MAPS = "maps";
        public const string FOLDER_LOGS = "logs";
        public const string FOLDER_SETTINGS = "BAGIS";

        public const string FIELD_AOI_AREA = "AOISHPAREA";
        public const string FIELD_SITE_ELEV = "BA_SELEV";
        public const string FIELD_SITE_NAME = "BA_SNAME";
        public const string FIELD_SITE_TYPE = "BA_STYPE";
        public const string FIELD_ASPECT = "BA_ASPECT";
        public const string FIELD_PRECIP = "BA_PRECIP";
        public const string FIELD_OBJECT_ID = "OBJECTID";
        public const string FIELD_STATION_TRIPLET = "stationTriplet";
        public const string FIELD_STATION_NAME = "stationName";
        public const string FIELD_AWDB_ID = "AWDB_ID";
        public const string FIELD_USGS_ID = "USGS_ID";
        public const string FIELD_NEAR_ID = "NEAR_FID";
        public const string FIELD_NEAR_DIST = "NEAR_DIST";
        public const string FIELD_VALUE = "VALUE";
        public const string FIELD_NAME = "NAME";
        public const int FIELD_NAME_WIDTH = 60;
        public const string FIELD_LBOUND = "LBOUND";
        public const string FIELD_UBOUND = "UBOUND";
        public const string FIELD_COUNT = "COUNT";
        public const string FIELD_RASTERVALU = "RASTERVALU";   //Field generated when using BA_ExtractValuesToPoints to populate BA_SELEV from DEM
        public const string FIELD_PUBLIC = "Public_";   //Indicates public land on the Public Lands layer
        public const string FIELD_ALPINE_ABV_TREELINE = "ALPINE_ABV_TREELINE";   //Indicates alpine vegetation types that are above the treeline
        public const string FIELD_GRID_CODE = "gridcode";   //Value after raster is converted to polygon
        public const string FIELD_RUNOFF_AVERAGE = "Average_kac_ft";
        public const string FIELD_RUNOFF_STATION_TRIPLET = "stationtriplet";

        public const string DATA_TYPE_SWE = "Snotel SWE";
        public const string DATA_TYPE_PRECIPITATION = "Precipitation";
        public const string DATA_TYPE_SNOTEL = "SNOTEL";
        public const string DATA_TYPE_SNOW_COURSE = "Snow Course";
        public const string DATA_TYPE_ROADS = "Roads";
        public const string DATA_TYPE_PUBLIC_LAND = "Public Land";
        public const string DATA_TYPE_VEGETATION = "Vegetation Type";
        public const string DATA_TYPE_SWE_DELTA = "Snotel SWE Delta";
        public const string SITE_TYPE_SNOW_COURSE = "scos";
        public const string SITE_TYPE_SNOTEL = "stel";
        public const string SITE_TYPE_PSEUDO = "psite";

        public const int VALUE_NO_DATA_9999 = -9999;
        public const string VALUE_NO_DATA = "NoData";
        public const string VALUE_UNKNOWN = "Unknown";
        public static readonly short[] VALUES_ELEV_INTERVALS = new short[] { 50, 100, 200, 250, 500, 1000, 2500, 5000};

        public const string UNITS_INCHES = "Inches";
        public const string UNITS_MILLIMETERS = "Millimeters";
        public const string UNITS_FEET = "Feet";
        public const string UNITS_METERS = "Meters";

        // States that control the map display buttons
        // JanSwe always needs to be last so that we can export the other months directly
        public static string[] STATES_MAP_BUTTONS => new string[] {"MapButtonPalette_BtnElevation_State",
                                                                "MapButtonPalette_BtnSnotel_State",
                                                                "MapButtonPalette_BtnSnowCourse_State",
                                                                "MapButtonPalette_BtnSitesAll_State",
                                                                "MapButtonPalette_BtnAspect_State",
                                                                "MapButtonPalette_BtnPrism_State",
                                                                "MapButtonPalette_BtnSlope_State",
                                                                "MapButtonPalette_BtnSweJan_State",
                                                                "MapButtonPalette_BtnSweFeb_State",
                                                                "MapButtonPalette_BtnSweMar_State",
                                                                "MapButtonPalette_BtnSweApr_State",
                                                                "MapButtonPalette_BtnSweMay_State",
                                                                "MapButtonPalette_BtnSweJun_State",
                                                                "MapButtonPalette_BtnSweDec_State",
                                                                "MapButtonPalette_BtnRoads_State",
                                                                "MapButtonPalette_BtnBelowTreeline_State",
                                                                "MapButtonPalette_BtnPublicLandZones_State",
                                                                "MapButtonPalette_BtnSitesLocationZone_State",
                                                                "MapButtonPalette_BtnPublicLandOwnership_State",
                                                                "MapButtonPalette_BtnSweDeltaDecToJan_State",
                                                                "MapButtonPalette_BtnSweDeltaJanToFeb_State",
                                                                "MapButtonPalette_BtnSweDeltaMarToFeb_State",
                                                                "MapButtonPalette_BtnSweDeltaAprToMar_State"};

        public static string META_TAG_XPATH = @"/metadata/dataIdInfo/searchKeys/keyword";
        public static string META_TAG_PREFIX = "BAGIS Tag < Please do not modify: ";
        public static string META_TAG_SUFFIX = " > End Tag";
        public static string META_TAG_ZUNIT_CATEGORY = "ZUnitCategory|";
        public static string META_TAG_ZUNIT_VALUE = "ZUnit|";
        public static string META_TAG_BUFFER_DISTANCE = "BufferDistance|";
        public static string META_TAG_XUNIT_VALUE = "XUnit|";
        public static string META_TAG_CATEGORY_DEPTH = "Depth";

        public static int EXCEL_CHART_SPACING = 5;
        public static int EXCEL_CHART_HEIGHT = 330;
        public static int EXCEL_CHART_WIDTH = 600;
        public static int EXCEL_CHART_DESCR_HEIGHT = 70;
        public static int EXCEL_LARGE_CHART_WIDTH = 800;
        public static int EXCEL_LARGE_CHART_HEIGHT = 500;

        public static string PORTAL_ORGANIZATION = "Natural Resources Conservation Service";

    }

}
