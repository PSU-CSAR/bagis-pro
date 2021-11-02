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
        public const string MAPS_AOI_LOCATION = "AOI Location";
        public const string MAPS_AOI_LOCATION_LAYOUT = "AOI Location Layout";
        public const string MAPS_AOI_LOCATION_MAP_FRAME_NAME = "AOI Location Map Frame";
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
        public const string MAPS_PRECIPITATION_CONTRIBUTION = "Precipitation Contribution";
        public const string MAPS_WINTER_PRECIPITATION = "Winter Precipitation";
        public const string MAPS_SUBBASIN_BOUNDARY = "Subbasin Boundary";
        public const string MAPS_WESTERN_STATES_BOUNDARY = "Western States Boundary";

        public const string MAPS_LEGEND = "Legend";
        public const string MAPS_TITLE = "Title";
        public const string MAPS_SUBTITLE = "SubTitle";
        public const string MAPS_TEXTBOX1 = "TextBox1";
        public const string MAPS_TEXTBOX2 = "TextBox2";

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
        public const string FILE_PREC_MEAN_ELEV_V = "precmeanelev_v";
        public const string FILE_MERGED_SITES = "merged_sites";
        public const string FILE_ELEV_ZONES_TBL = "tblZones";
        public const string FILE_ELEV_ZONES_VECTOR = "elevzone_v";
        public const string FILE_CRITICAL_PRECIP_ZONE = "criticalprecipzone";
        public const string FILE_PRECIPITATION_CONTRIBUTION = "precipcontribzone";
        public const string FILE_PRECIP_CONTRIB_VECTOR = "precipcontribzone_v";
        public const string FILE_WINTER_PRECIPITATION_ZONE = "winterprecipzone";
        public const string FILE_SETTINGS = "analysis.xml";
        public const string FILE_BATCH_TOOL_SETTINGS = "batch_tool_settings.json";
        public const string FILE_BATCH_LOG = "batch_tool_log.txt";
        public const string FILE_EXPORT_MAP_ELEV_PDF = "elevation_distribution.pdf";
        public const string FILE_EXPORT_MAP_SNOTEL_PDF = "map_elevation_snotel.pdf";
        public const string FILE_EXPORT_MAP_SCOS_PDF = "map_elevation_sc.pdf";
        public const string FILE_EXPORT_MAP_SNOTEL_AND_SCOS_PDF = "map_elevation_snotel_sc.pdf";
        public const string FILE_EXPORT_SITE_REPRESENTATION_PDF = "site_representation.pdf";
        public const string FILE_EXPORT_MAP_PRECIPITATION_PDF = "map_precipitation.pdf";
        public const string FILE_EXPORT_MAP_ASPECT_PDF = "map_aspect.pdf";
        public const string FILE_EXPORT_MAP_SLOPE_PDF = "map_slope.pdf";
        public static readonly string[] FILE_EXPORT_MAPS_SWE = new string[] { "map_snodas_swe_november.pdf", "map_snodas_swe_december.pdf", "map_snodas_swe_january.pdf",
                                                                              "map_snodas_swe_february.pdf", "map_snodas_swe_march.pdf", "map_snodas_swe_april.pdf",
                                                                              "map_snodas_swe_may.pdf", "map_snodas_swe_june.pdf", "map_snodas_swe_july.pdf"};
        public static readonly string[] FILE_EXPORT_MAPS_SWE_DELTA = new string[] { "map_nov_swe_delta.pdf", "map_dec_swe_delta.pdf", "map_jan_swe_delta.pdf",
                                                                                    "map_feb_swe_delta.pdf", "map_mar_swe_delta.pdf", "map_apr_swe_delta.pdf",
                                                                                    "map_may_swe_delta.pdf", "map_jun_swe_delta.pdf"};
        public static readonly string[] FILE_EXPORT_MAPS_SEASONAL_PRECIP_CONTRIB = new string[] { "map_seasonal_precip_q1.pdf", "map_seasonal_precip_q2.pdf", "map_seasonal_precip_q3.pdf",
                                                                                                  "map_seasonal_precip_q4.pdf"};
        public static readonly string[] FILE_EXPORT_OVERVIEW_FILES = new string[] { FILE_TITLE_PAGE_PDF, FILE_EXPORT_MAP_AOI_LOCATION_PDF, FILE_SITES_TABLE_PDF };
        public static readonly string[] FILE_EXPORT_ASPECT_FILES = new string[] { FILE_EXPORT_MAP_ASPECT_PDF, FILE_EXPORT_CHART_ASPECT_PDF };
        public static readonly string[] FILE_EXPORT_SLOPE_FILES = new string[] { FILE_EXPORT_MAP_SLOPE_PDF, FILE_EXPORT_CHART_SLOPE_PDF };
        public static readonly string[] FILE_EXPORT_SITE_REPRESENTATION_FILES = new string[] { FILE_EXPORT_MAP_SNOTEL_PDF, FILE_EXPORT_MAP_SCOS_PDF,
                                                                                               FILE_EXPORT_MAP_SNOTEL_AND_SCOS_PDF};
        public static readonly string[] FILE_EXPORT_PRECIPITATION_DISTRIBUTION_FILES = new string[] { FILE_EXPORT_MAP_PRECIPITATION_PDF, FILE_EXPORT_CHART_ELEV_PRECIP_CORR_PDF,
                                                                                               FILE_EXPORT_MAP_PRECIPITATION_CONTRIBUTION_PDF, FILE_EXPORT_CHART_AREA_ELEV_PRECIP_SITE_PDF,
                                                                                               FILE_EXPORT_CHART_PRECIP_REPRESENT_PDF, FILE_EXPORT_CRITICAL_PRECIPITATION_ZONES_PDF };
        public static readonly string[] FILE_EXPORT_SITE_ANALYSIS_FILES = new string[] { FILE_EXPORT_MAP_ROADS_PDF, FILE_EXPORT_MAP_PUBLIC_LAND_ZONES_PDF, FILE_EXPORT_MAP_PUBLIC_LAND_OWNERSHIP_PDF,
                                                                            FILE_EXPORT_MAP_BELOW_TREELINE_PDF, FILE_EXPORT_MAP_SITES_LOCATION_PDF, FILE_EXPORT_MAP_SITES_LOCATION_PRECIP_PDF,
                                                                            FILE_EXPORT_MAP_SITES_LOCATION__PRECIP_CONTRIB_PDF };
        public const string FILE_EXPORT_MAP_ROADS_PDF = "map_roads.pdf";
        public const string FILE_EXPORT_MAP_PUBLIC_LAND_ZONES_PDF = "map_public_land_zones.pdf";
        public const string FILE_EXPORT_MAP_BELOW_TREELINE_PDF = "map_below_treeline.pdf";
        public const string FILE_EXPORT_MAP_SITES_LOCATION_PDF = "map_sites_location.pdf";
        public const string FILE_EXPORT_MAP_SITES_LOCATION_PRECIP_PDF = "map_sites_location_precip.pdf";
        public const string FILE_EXPORT_MAP_SITES_LOCATION__PRECIP_CONTRIB_PDF = "map_sites_location_precip_contrib.pdf";
        public const string FILE_EXPORT_MAP_CRITICAL_PRECIPITATION_ZONES_PDF = "map_critical_precip_zones.pdf";
        public const string FILE_EXPORT_MAP_PUBLIC_LAND_OWNERSHIP_PDF = "map_public_land_ownership.pdf";
        public const string FILE_EXPORT_CHART_AREA_ELEV_PRECIP_SITE_PDF = "chart_area_elev_precip_site.pdf";
        public const string FILE_EXPORT_CHART_SLOPE_PDF = "chart_slope.pdf";
        public const string FILE_EXPORT_CHART_ASPECT_PDF = "chart_aspect.pdf";
        public const string FILE_EXPORT_ASPECT_DISTRIBUTION_PDF = "aspect_distribution.pdf";
        public const string FILE_EXPORT_SLOPE_DISTRIBUTION_PDF = "slope_distribution.pdf";
        public const string FILE_EXPORT_TABLE_PRECIP_REPRESENT_PDF = "table_precip_representation.pdf";
        public const string FILE_EXPORT_CHART_PRECIP_REPRESENT_PDF = "chart_precip_representation.pdf";
        public const string FILE_EXPORT_CHART_ELEV_PRECIP_CORR_PDF = "chart_elev_precip_correlation.pdf";
        public const string FILE_EXPORT_WATERSHED_REPORT_PDF = "watershed_report.pdf";
        public const string FILE_EXPORT_SITE_ANALYSIS_REPORT_PDF = "site_analysis_report.pdf";
        public const string FILE_EXPORT_CRITICAL_PRECIPITATION_ZONES_PDF = "critical_precipitation_zones.pdf";
        public const string FILE_EXPORT_PRECIPITATION_DISTRIBUTION_PDF = "precipitation_distribution.pdf";
        public const string FILE_EXPORT_MAP_PRECIPITATION_CONTRIBUTION_PDF = "map_precipitation_contrib.pdf";
        public const string FILE_EXPORT_MAP_WINTER_PRECIPITATION_PDF = "map_winter_precipitation.pdf";
        public const string FILE_EXPORT_SNODAS_SWE_PDF = "snodas_swe.pdf";
        public const string FILE_EXPORT_SEASONAL_PRECIP_DISTRIBUTION_PDF = "seasonal_precip_distribution.pdf";
        public const string FILE_EXPORT_MAP_AOI_LOCATION_PDF = "map_aoi_location.pdf";
        // List of files in the watershed PDF map package and the order in which they will be assembled
        public static string[] FILES_EXPORT_WATERSHED_PDF = new string[]
        { FILE_EXPORT_OVERVIEW_PDF, FILE_EXPORT_MAP_ELEV_PDF, FILE_EXPORT_ASPECT_DISTRIBUTION_PDF, FILE_EXPORT_SLOPE_DISTRIBUTION_PDF,
          FILE_EXPORT_SITE_REPRESENTATION_PDF,
          FILE_EXPORT_PRECIPITATION_DISTRIBUTION_PDF, FILE_EXPORT_SEASONAL_PRECIP_DISTRIBUTION_PDF, FILE_EXPORT_SNODAS_SWE_PDF,
          FILE_EXPORT_POTENTIAL_SITE_ANALYSIS_PDF
        };    
        // List of files in the site analysis PDF map package and the order in which they will be assembled
        //public static string[] FILES_EXPORT_SITE_ANALYSIS_PDF = new string[] { FILE_TITLE_PAGE_PDF, FILE_SITES_TABLE_PDF,
        //    FILE_EXPORT_MAP_ROADS_PDF, FILE_EXPORT_MAP_PUBLIC_LAND_ZONES_PDF, FILE_EXPORT_MAP_PUBLIC_LAND_OWNERSHIP_PDF,
        //    FILE_EXPORT_MAP_BELOW_TREELINE_PDF, FILE_EXPORT_MAP_SITES_LOCATION_PDF};
        public const string FILE_TITLE_PAGE_XSL = "title_page.xsl";
        public const string FILE_TITLE_PAGE_XML = "title_page.xml";
        public const string FILE_TITLE_PAGE_HTML = "title_page.html";
        public const string FILE_TITLE_PAGE_PDF = "title_page.pdf";
        public const string FILE_SITES_TABLE_XSL = "sites_table.xsl";
        public const string FILE_SITES_TABLE_XML = "sites_table.xml";
        public const string FILE_SITES_TABLE_HTML = "sites_table.html";
        public const string FILE_SITES_TABLE_PDF = "sites_table.pdf";
        public const string FILE_EXPORT_OVERVIEW_PDF = "overview.pdf";
        public const string FILE_EXPORT_POTENTIAL_SITE_ANALYSIS_PDF = "potential_site_analysis.pdf";
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
        public static readonly string[] LAYER_NAMES_SWE_DELTA = new string[] { "November SWE Delta", "December SWE Delta", "January SWE Delta",
                                                                              "February SWE Delta", "March SWE Delta", "April SWE Delta",
                                                                              "May SWE Delta", "June SWE Delta"};
        public static readonly string[] MAP_TITLES_SWE_DELTA = new string[] { "AVERAGE NOVEMBER SWE DELTA", "AVERAGE DECEMBER SWE DELTA", "AVERAGE JANUARY SWE DELTA",
                                                                              "AVERAGE FEBRUARY SWE DELTA", "AVERAGE MARCH SWE DELTA", "AVERAGE APRIL SWE DELTA",
                                                                              "AVERAGE MAY SWE DELTA", "AVERAGE JUNE SWE DELTA"};
        public static readonly string[] FILES_SEASON_PRECIP_CONTRIB = new string[] {"sq1_precip_contrib", "sq2_precip_contrib", "sq3_precip_contrib",
                                                                                    "sq4_precip_contrib"};
        public static readonly string[] LAYER_NAMES_SEASON_PRECIP_CONTRIB = new string[] {"SQ1 Precip Contribution", "SQ2 Precip Contribution", "SQ3 Precip Contribution",
                                                                                          "SQ4 Precip Contribution"};
        public static readonly string[] MAP_NAMES_SEASON_PRECIP_CONTRIB = new string[] {"SQ1 PRECIPITATION CONTRIBUTION", "SQ2 PRECIPITATION CONTRIBUTION", "SQ3 PRECIPITATION CONTRIBUTION",
                                                                                        "SQ4 PRECIPITATION CONTRIBUTION"};
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
        public const string FIELD_DIRECTION = "BA_DIRECT";
        public const string FIELD_SLOPE = "BA_SLOPE";
        public const string FIELD_SITE_ID = "BA_SITE_ID";
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
        public const string FIELD_SUITABLE_PUBLIC = "Suitable_Public";   //Indicates non-wilderness federal land on the Public Lands layer
        public const string FIELD_ALPINE_ABV_TREELINE = "ALPINE_ABV_TREELINE";   //Indicates alpine vegetation types that are above the treeline
        public const string FIELD_GRID_CODE = "gridcode";   //Value after raster is converted to polygon
        public const string FIELD_RUNOFF_AVERAGE = "Average_kac_ft";
        public const string FIELD_RUNOFF_STATION_TRIPLET = "stationtriplet";
        public const string FIELD_VOL_ACRE_FT = "VOL_ACRE_FT";
        public const string FIELD_SUM = "SUM";
        public const string FIELD_NWCCNAME = "nwccname";
        public const string FIELD_WINTER_START_MONTH = "winter_start_month";
        public const string FIELD_WINTER_END_MONTH = "winter_end_month";
        public const string FIELD_HUC = "huc";
        public const string FIELD_LATITUDE = "latitude";
        public const string FIELD_LONGITUDE = "longitude";
        public const string FIELD_SOURCE_ID_FEATURE = "SrcID_Feat";
        public const string FIELD_SOURCE_ID_RASTER = "SrcID_Rast";
        public const string FIELD_SAMPLE_INPUT_1 = "v_raster_10";
        public const string FIELD_SAMPLE_INPUT_2 = "v_raster_20";
        public const string FIELD_SAMPLE_INPUT_3 = "v_raster_30";

        public const string DATA_TYPE_SWE = "Snotel SWE";
        public const string DATA_TYPE_PRECIPITATION = "Precipitation";
        public const string DATA_TYPE_SNOTEL = "SNOTEL";
        public const string DATA_TYPE_SNOW_COURSE = "Snow Course";
        public const string DATA_TYPE_ROADS = "Roads";
        public const string DATA_TYPE_PUBLIC_LAND = "Public Land";
        public const string DATA_TYPE_VEGETATION = "Vegetation Type";
        public const string DATA_TYPE_SWE_DELTA = "Snotel SWE Delta";

        public const int VALUE_NO_DATA_9999 = -9999;
        public const string VALUE_NO_DATA = "NoData";
        public const string VALUE_UNKNOWN = "Unknown";
        // BAGIS V3 allowed elevation intervals up to 5000; This tool only allows up to 500
        public static readonly short[] VALUES_ELEV_INTERVALS = new short[] { 50, 100, 200, 250, 500};

        public const string UNITS_INCHES = "Inches";
        public const string UNITS_MILLIMETERS = "Millimeters";
        public const string UNITS_FEET = "Feet";
        public const string UNITS_METERS = "Meters";
        public const double MAP_BUFFER_FACTOR = 1.1;
        public const string TEXT_SITES_TABLE_DESCR = "See the Active Sites table for individual SNOTEL and Snow Course site descriptions";

        // States that control the map display buttons
        // JanSwe always needs to be last so that we can export the other months directly
        public static string[] STATES_WATERSHED_MAP_BUTTONS => new string[] {"MapButtonPalette_BtnElevation_State",
                                                                "MapButtonPalette_BtnSnotel_State",
                                                                "MapButtonPalette_BtnSnowCourse_State",
                                                                "MapButtonPalette_BtnSitesAll_State",
                                                                "MapButtonPalette_BtnAspect_State",
                                                                "MapButtonPalette_BtnPrism_State",
                                                                "MapButtonPalette_BtnWinterPrecipitation_State",
                                                                "MapButtonPalette_BtnPrecipContrib_State",
                                                                "MapButtonPalette_BtnSlope_State",
                                                                "MapButtonPalette_BtnSweJan_State",
                                                                "MapButtonPalette_BtnSweFeb_State",
                                                                "MapButtonPalette_BtnSweMar_State",
                                                                "MapButtonPalette_BtnSweApr_State",
                                                                "MapButtonPalette_BtnSweMay_State",
                                                                "MapButtonPalette_BtnSweJun_State",
                                                                "MapButtonPalette_BtnSweJul_State",
                                                                "MapButtonPalette_BtnSweNov_State",
                                                                "MapButtonPalette_BtnSweDec_State",
                                                                "MapButtonPalette_BtnSweNovDelta_State",
                                                                "MapButtonPalette_BtnSweDecDelta_State",
                                                                "MapButtonPalette_BtnSweJanDelta_State",
                                                                "MapButtonPalette_BtnSweFebDelta_State",
                                                                "MapButtonPalette_BtnSweMarDelta_State",
                                                                "MapButtonPalette_BtnSweAprDelta_State",
                                                                "MapButtonPalette_BtnSweMayDelta_State",
                                                                "MapButtonPalette_BtnSweJunDelta_State",
                                                                "MapButtonPalette_BtnSeasonalPrecipContribSQ1_State",
                                                                "MapButtonPalette_BtnSeasonalPrecipContribSQ2_State",
                                                                "MapButtonPalette_BtnSeasonalPrecipContribSQ3_State",
                                                                "MapButtonPalette_BtnSeasonalPrecipContribSQ4_State",
                                                                "MapButtonPalette_BtnRoads_State",
                                                                "MapButtonPalette_BtnBelowTreeline_State",
                                                                "MapButtonPalette_BtnPublicLandZones_State",
                                                                "MapButtonPalette_BtnSitesLocationZone_State",
                                                                "MapButtonPalette_BtnSitesLocationPrecip_State",
                                                                "MapButtonPalette_BtnSitesLocationZone_State",
                                                                "MapButtonPalette_BtnAoiLocation_State",
                                                                "MapButtonPalette_BtnSitesLocationPrecipContrib_State"};
        //public static string[] STATES_SITE_ANALYSIS_MAP_BUTTONS => new string[] {
        //                                                        "MapButtonPalette_BtnRoads_State",
        //                                                        "MapButtonPalette_BtnBelowTreeline_State",
        //                                                        "MapButtonPalette_BtnPublicLandZones_State",
        //                                                        "MapButtonPalette_BtnSitesLocationZone_State",
        //                                                        "MapButtonPalette_BtnPublicLandOwnership_State"};

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
        public static int EXCEL_CHART_DESCR_HEIGHT = 80;
        public static int EXCEL_LARGE_CHART_WIDTH = 800;
        public static int EXCEL_LARGE_CHART_HEIGHT = 500;

        public static int[,] ARR_SWE_COLORS = new int[,] {
            {204,204,204,255},
            {156,156,156,255},
            {255,211,127,255},
            {230,152,0,255},
            {115,178,255,255},
            {0,92,230,255},
            {255,255,255,255}};
        }

}
