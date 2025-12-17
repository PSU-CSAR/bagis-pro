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
        public const string MAPS_FIRE_MAP_NAME = "Basin Fire Analysis";
        public const string MAPS_FIRE_LAYOUT_NAME = "Basin Fire Analysis Layout";
        public const string MAPS_FIRE_MAP_FRAME_NAME = "Basin Fire Analysis Map Frame";
        public const string MAPS_SNODAS_LAYOUT = "SNODAS Layout";
        public const string MAPS_SNODAS_DELTA_LAYOUT = "SNODAS Delta Layout";
        public const string MAPS_SEASONAL_PRECIP_LAYOUT = "Seasonal Precipitation Layout";
        public const string MAPS_BASIN_BOUNDARY = "Basin Boundary";
        public const string MAPS_STREAMS = "AOI Streams";
        public const string MAPS_ROADS = "Roads";
        public const string MAPS_SNOTEL = "Snotel";
        public const string MAPS_SNOW_COURSE = "Snow Course";
        public const string MAPS_COOP_PILLOW = "Coop Pillow";
        public const string MAPS_SNOLITE = "Snolite";
        public const string MAPS_HILLSHADE = "hillshade";
        public const string MAPS_ELEV_ZONE = "Elevation Zones";
        public const string MAPS_SLOPE_ZONE = "Slope Zones";
        public const string MAPS_ASPECT_ZONE = "Aspect Zones";
        public const string MAPS_PRISM_ZONE = "Precipitation Zones";
        public const string MAPS_AUTOMATED_SITES_REPRESENTED = "Auto Sites Rep Area";
        public const string MAPS_SNOW_COURSE_REPRESENTED = "Snow Course Rep Area";
        public const string MAPS_ALL_SITES_REPRESENTED = "All Sites Rep Area";
        public const string MAPS_SUITABLE_LAND_ZONES = "Suitable Land Ownership";
        public const string MAPS_FORESTED_LAND_COVER = "Forested Land Cover";
        public const string MAPS_POTENTIAL_LOCATIONS = "Potential Locations";
        public const string MAPS_CRITICAL_PRECIPITATION_ZONES = "Critical Precipitation";
        public const string MAPS_LAND_OWNERSHIP = "Land Ownership";
        public const string MAPS_PRECIPITATION_CONTRIBUTION = "Precipitation Contribution";
        public const string MAPS_WINTER_PRECIPITATION = "Winter Precipitation";
        public const string MAPS_SUBBASIN_BOUNDARY = "Subbasin Boundary";
        public const string MAPS_WESTERN_STATES_BOUNDARY = "Western States Boundary";
        public const string MAPS_LAND_COVER = "Land Cover";
        public const string MAPS_WATERBODIES = "Waterbodies";
        public const string MAPS_STREAM_GAGE = "Streamgage";
        public static readonly string[] MAPS_ALL_ARRAY = { Constants.MAPS_BASIN_BOUNDARY, Constants.MAPS_STREAMS, Constants.MAPS_SNOTEL,
            Constants.MAPS_SNOW_COURSE, Constants.MAPS_HILLSHADE, Constants.MAPS_ELEV_ZONE, Constants.MAPS_SNOW_COURSE_REPRESENTED,
            Constants.MAPS_AUTOMATED_SITES_REPRESENTED, Constants.MAPS_SLOPE_ZONE, Constants.MAPS_ASPECT_ZONE,
            Constants.MAPS_ALL_SITES_REPRESENTED, Constants.MAPS_PRISM_ZONE, Constants.MAPS_SUITABLE_LAND_ZONES,
            Constants.MAPS_FORESTED_LAND_COVER, Constants.MAPS_POTENTIAL_LOCATIONS, Constants.MAPS_CRITICAL_PRECIPITATION_ZONES,
            Constants.MAPS_LAND_OWNERSHIP, Constants.MAPS_PRECIPITATION_CONTRIBUTION, Constants.MAPS_WINTER_PRECIPITATION,
            Constants.MAPS_SUBBASIN_BOUNDARY, Constants.MAPS_LAND_COVER, Constants.MAPS_WATERBODIES, Constants.MAPS_ROADS,
            Constants.MAPS_SNOLITE, Constants.MAPS_COOP_PILLOW, Constants.MAPS_STREAM_GAGE };
        public const string MAPS_LEGEND = "Legend";
        public const string MAPS_TITLE = "Title";
        public const string MAPS_SUBTITLE = "SubTitle";
        public const string MAPS_TEXTBOX1 = "TextBox1";
        public const string MAPS_TEXTBOX2 = "TextBox2";
        public const string MAPS_NIFC_PERIMETER = "NIFC Fire Perimeter";
        public static readonly string[] MAPS_FIRE_ARRAY = { Constants.MAPS_BASIN_BOUNDARY, Constants.MAPS_STREAMS, Constants.MAPS_WATERBODIES, 
            Constants.MAPS_SNOTEL, Constants.MAPS_SNOW_COURSE, Constants.MAPS_HILLSHADE, Constants.MAPS_SNOLITE, Constants.MAPS_COOP_PILLOW, 
            Constants.MAPS_STREAM_GAGE, Constants.MAPS_NIFC_PERIMETER};
        public const string MAPS_CLIP_DEM_LAYER = "Clip DEM";
        public const string FILE_AOI_VECTOR = "aoi_v";
        public const string FILE_AOI_BUFFERED_VECTOR = "aoib_v";
        public const string FILE_AOI_PRISM_VECTOR = "p_aoi_v";
        public const string FILE_AOI_RASTER = "aoibagis";
        public const string FILE_AOI_PRISM_GRID = "p_aoi";
        public const string FILE_STREAMS = "aoi_streams";
        public const string FILE_SNOTEL = "snotel_sites";
        public const string FILE_SNOW_COURSE = "snowcourse_sites";
        public const string FILE_HILLSHADE = "hillshade";
        public const string FILE_DEM_FILLED = "dem_filled";
        public const string FILE_DEM_CLIPPED = "dem_clipped";
        public const string FILE_DEM = "dem";
        public const string FILE_ELEV_ZONE = "elevzone";
        public const string FILE_SLOPE_ZONE = "slpzone";
        public const string FILE_ASPECT_ZONE = "aspzone";
        public const string FILE_PRECIP_ZONE = "preczone";
        public const string FILE_SNOTEL_ZONE = "stelzone";
        public const string FILE_SCOS_ZONE = "scoszone";
        public const string FILE_ROADS_ZONE = "roadszone";
        public const string FILE_PUBLIC_LAND_ZONE = "publiclandszone";
        public const string FILE_FORESTED_ZONE = "forestedzone";
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
        public const string FILE_LAND_OWNERSHIP = "land_ownership";
        public const string FILE_LAND_COVER = "nlcd_land_cover";
        public const string FILE_WATER_BODIES = "water_bodies";
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
        public const string FILE_SITES_DEM = "sites_dem";
        public const string FILE_SITES_ASPECT = "sites_aspect";
        public const string FILE_SITES_SLOPE = "sites_slope";
        public const string FILE_SETTINGS = "analysis.xml";
        public const string FILE_FIRE_SETTINGS = "fire_analysis.json";
        public const string FILE_BAGIS_SETTINGS = "bagis_settings.json";
        public const string FILE_BATCH_LOG = "batch_tool_log.txt";
        public const string FILE_SNODAS_GEOJSON_LOG = "snodas_log.txt";
        public const string FILE_GEN_STATISTICS_LOG = "generate_statistics_log.txt";
        public const string FILE_FIRE_DATA_LOG = "generate_fire_data_log.txt";
        public const string FILE_FIRE_REPORT_LOG = "generate_fire_report_log.txt";
        public const string FILE_FIRE_MAPS_LOG = "generate_fire_maps_log.txt";
        public const string FILE_FORECAST_STATION_LOG = "forecast_station_log.csv";
        public const string FILE_AOI_STATISTICS = "forecast_aoi_statistics.csv";
        public const string FILE_SNOLITE = "snolite_sites";
        public const string FILE_COOP_PILLOW = "coop_pillow_sites";
        public const string FILE_FIRE_HISTORY = "firehistory";
        public const string FILE_FIRE_CURRENT = "firecurrent";
        public const string FILE_NIFC_FIRE = "nifcfire";
        public const string FILE_EXPORT_MAP_ELEV_PDF = "elevation_distribution.pdf";
        public const string FILE_EXPORT_MAP_SNOTEL_PDF = "map_elevation_snotel.pdf";
        public const string FILE_EXPORT_MAP_SCOS_PDF = "map_elevation_sc.pdf";
        public const string FILE_EXPORT_MAP_SNOTEL_AND_SCOS_PDF = "map_elevation_snotel_sc.pdf";
        public const string FILE_EXPORT_SITE_REPRESENTATION_PDF = "site_representation.pdf";
        public const string FILE_EXPORT_MAP_PRECIPITATION_PDF = "map_precipitation.pdf";
        public const string FILE_EXPORT_MAP_ASPECT_PDF = "map_aspect.pdf";
        public const string FILE_EXPORT_MAP_SLOPE_PDF = "map_slope.pdf";
        public const string FILE_EXPORT_MAP_SNODAS_SWE_PDF = "map_snodas_swe.pdf";
        public static readonly string[] FILE_EXPORT_OVERVIEW_FILES = new string[] { FILE_TITLE_PAGE_PDF, FILE_DATA_SOURCES_PDF, FILE_EXPORT_MAP_AOI_LOCATION_PDF, FILE_SITES_TABLE_PDF };
        public static readonly string[] FILE_EXPORT_ASPECT_FILES = new string[] { FILE_EXPORT_MAP_ASPECT_PDF, FILE_EXPORT_CHART_ASPECT_PDF };
        public static readonly string[] FILE_EXPORT_SLOPE_FILES = new string[] { FILE_EXPORT_MAP_SLOPE_PDF, FILE_EXPORT_CHART_SLOPE_PDF };
        public static readonly string[] FILE_EXPORT_SITE_REPRESENTATION_FILES = new string[] { FILE_EXPORT_MAP_SNOTEL_PDF, FILE_EXPORT_MAP_SCOS_PDF,
                                                                                               FILE_EXPORT_MAP_SNOTEL_AND_SCOS_PDF};
        public static readonly string[] FILE_EXPORT_PRECIPITATION_DISTRIBUTION_FILES = new string[] { FILE_EXPORT_MAP_PRECIPITATION_PDF, FILE_EXPORT_CHART_ELEV_PRECIP_CORR_PDF,
                                                                                               FILE_EXPORT_MAP_PRECIPITATION_CONTRIBUTION_PDF, FILE_EXPORT_CHART_AREA_ELEV_PRECIP_SITE_PDF,
                                                                                               FILE_EXPORT_CHART_PRECIP_REPRESENT_PDF, FILE_EXPORT_CRITICAL_PRECIPITATION_ZONES_PDF };
        public static readonly string[] FILE_EXPORT_SITE_ANALYSIS_FILES = new string[] { FILE_EXPORT_MAP_SITES_LOCATION_PDF, FILE_EXPORT_MAP_SITES_LOCATION_PRECIP_PDF, FILE_EXPORT_MAP_SITES_LOCATION__PRECIP_CONTRIB_PDF,
                                                                                          FILE_EXPORT_MAP_PUBLIC_LAND_ZONES_PDF, FILE_EXPORT_MAP_LAND_OWNERSHIP_PDF,
                                                                                          FILE_EXPORT_MAP_FORESTED_LAND_COVER_PDF};
        public const string FILE_EXPORT_MAP_PUBLIC_LAND_ZONES_PDF = "map_public_land_zones.pdf";
        public const string FILE_EXPORT_MAP_FORESTED_LAND_COVER_PDF = "map_forested_land_cover.pdf";
        public const string FILE_EXPORT_MAP_SITES_LOCATION_PDF = "map_sites_location.pdf";
        public const string FILE_EXPORT_MAP_SITES_LOCATION_PRECIP_PDF = "map_sites_location_precip.pdf";
        public const string FILE_EXPORT_MAP_SITES_LOCATION__PRECIP_CONTRIB_PDF = "map_sites_location_precip_contrib.pdf";
        public const string FILE_EXPORT_MAP_CRITICAL_PRECIPITATION_ZONES_PDF = "map_critical_precip_zones.pdf";
        public const string FILE_EXPORT_MAP_LAND_OWNERSHIP_PDF = "map_land_ownership.pdf";
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
        public const string FILE_EXPORT_SNODAS_SWE_DELTA_PDF = "snodas_swe_delta.pdf";
        public const string FILE_EXPORT_SEASONAL_PRECIP_DISTRIBUTION_PDF = "seasonal_precip_distribution.pdf";
        public const string FILE_EXPORT_MAP_AOI_LOCATION_PDF = "map_aoi_location.pdf";
        public const string FILE_EXPORT_LAND_COVER_PDF = "land_cover.pdf";
        public const string FILE_MAP_SUFFIX_PDF = "_Fire-Map.pdf";
        public const string FILE_REPORT_SUFFIX_PDF = "_Fire-Report.pdf";

        // List of files in the watershed PDF map package and the order in which they will be assembled
        // Any changes to members or order of this array need to have a corresponding update to FILES_EXPORT_TITLES
        // FILES_EXPORT_TITLES is used to manage the content of filler pages if a document is missing
        public static string[] FILES_EXPORT_WATERSHED_PDF = new string[]
        { FILE_TITLE_PAGE_PDF, FILE_DATA_SOURCES_PDF, FILE_EXPORT_MAP_AOI_LOCATION_PDF, FILE_SITES_TABLE_PDF,
          FILE_EXPORT_MAP_ELEV_PDF, FILE_EXPORT_LAND_COVER_PDF, FILE_EXPORT_MAP_ASPECT_PDF, FILE_EXPORT_CHART_ASPECT_PDF,
          FILE_EXPORT_MAP_SLOPE_PDF, FILE_EXPORT_CHART_SLOPE_PDF, FILE_EXPORT_MAP_SNOTEL_PDF, FILE_EXPORT_MAP_SCOS_PDF,
          FILE_EXPORT_MAP_SNOTEL_AND_SCOS_PDF, FILE_EXPORT_MAP_PRECIPITATION_PDF, FILE_EXPORT_CHART_ELEV_PRECIP_CORR_PDF,
          FILE_EXPORT_MAP_PRECIPITATION_CONTRIBUTION_PDF, FILE_EXPORT_CHART_AREA_ELEV_PRECIP_SITE_PDF,
          FILE_EXPORT_CHART_PRECIP_REPRESENT_PDF, FILE_EXPORT_MAP_CRITICAL_PRECIPITATION_ZONES_PDF, FILE_EXPORT_TABLE_PRECIP_REPRESENT_PDF,
          FILE_EXPORT_MAP_WINTER_PRECIPITATION_PDF, FILE_EXPORT_SEASONAL_PRECIP_DISTRIBUTION_PDF, FILE_EXPORT_SNODAS_SWE_PDF,
          FILE_EXPORT_SNODAS_SWE_DELTA_PDF, FILE_EXPORT_MAP_SITES_LOCATION_PDF,
          FILE_EXPORT_MAP_SITES_LOCATION_PRECIP_PDF, FILE_EXPORT_MAP_SITES_LOCATION__PRECIP_CONTRIB_PDF, FILE_EXPORT_MAP_PUBLIC_LAND_ZONES_PDF,
          FILE_EXPORT_MAP_LAND_OWNERSHIP_PDF, FILE_EXPORT_MAP_FORESTED_LAND_COVER_PDF
        };

        // List of files in the watershed chapters PDF map package and the order in which they will be assembled
        public static string[] FILES_EXPORT_WATERSHED_CHAPTERS_PDF = new string[]
        { FILE_EXPORT_OVERVIEW_PDF, FILE_EXPORT_MAP_ELEV_PDF, FILE_EXPORT_LAND_COVER_PDF, FILE_EXPORT_ASPECT_DISTRIBUTION_PDF,
          FILE_EXPORT_SLOPE_DISTRIBUTION_PDF, FILE_EXPORT_SITE_REPRESENTATION_PDF, FILE_EXPORT_PRECIPITATION_DISTRIBUTION_PDF,
          FILE_EXPORT_SEASONAL_PRECIP_DISTRIBUTION_PDF, FILE_EXPORT_SNODAS_SWE_PDF, FILE_EXPORT_POTENTIAL_SITE_ANALYSIS_PDF
        };
        // List of files in the site analysis PDF map package and the order in which they will be assembled
        //public static string[] FILES_EXPORT_SITE_ANALYSIS_PDF = new string[] { FILE_TITLE_PAGE_PDF, FILE_SITES_TABLE_PDF,
        //    FILE_EXPORT_MAP_ROADS_PDF, FILE_EXPORT_MAP_PUBLIC_LAND_ZONES_PDF, FILE_EXPORT_MAP_PUBLIC_LAND_OWNERSHIP_PDF,
        //    FILE_EXPORT_MAP_BELOW_TREELINE_PDF, FILE_EXPORT_MAP_SITES_LOCATION_PDF};
        public const string FILE_TITLE_PAGE_XSL = "title_page.xsl";
        public const string FILE_TITLE_PAGE_FIRE_XSL = "title_page_fire.xsl";
        public const string FILE_TITLE_PAGE_XML = "title_page.xml";
        public const string FILE_TITLE_PAGE_HTML = "title_page.html";
        public const string FILE_TITLE_PAGE_PDF = "title_page.pdf";
        public const string FILE_DATA_SOURCES_XSL = "data_sources.xsl";
        public const string FILE_DATA_SOURCES_XML = "data_sources.xml";
        public const string FILE_DATA_SOURCES_HTML = "data_sources.html";
        public const string FILE_DATA_SOURCES_PDF = "data_sources.pdf";
        public const string FILE_SITES_TABLE_XSL = "sites_table.xsl";
        public const string FILE_SITES_TABLE_XML = "sites_table.xml";
        public const string FILE_SITES_TABLE_HTML = "sites_table.html";
        public const string FILE_SITES_TABLE_PDF = "sites_table.pdf";
        public const string FILE_BLANK_PAGE_XSL = "blank_page.xsl";
        public const string FILE_BLANK_PAGE_XML = "blank_page.xml";
        public const string FILE_BLANK_PAGE_HTML = "blank_page.html";
        public const string FILE_BLANK_FIRE_PAGE_XSL = "blank_fire_page.xsl";
        public const string FILE_EXPORT_OVERVIEW_PDF = "overview.pdf";
        public const string FILE_EXPORT_POTENTIAL_SITE_ANALYSIS_PDF = "potential_site_analysis.pdf";
        public const string FILE_BLANK_PAGE_PDF = "blank_page.pdf";
        public const string FILE_FLOW_ACCUMULATION = "flow_accumulation";
        public const string FILE_FLOW_DIRECTION = "flow_direction";
        public const string FILE_AOI_BUFFERED_RASTER = "aoib";
        public const string FILE_AOI_PRISM_RASTER = "p_aoi";
        public const string FILE_ANNUAL_RUNOFF_CSV = "annual_runoff_averages.csv";
        public const string FILE_TOO_MANY_SITES = "too_many_sites.pdf";
        public const string FILE_NO_SITES = "no_sites.pdf";
        public const string FILE_SITES_APPENDIX_PDF = "sites_appendix.pdf";
        public const string FILE_MERGED_AOI_POLYS = "merged_aoi_polygons";
        public const string FILE_MERGE_GDB = "merge.gdb";
        public static readonly string[] URIS_SNODAS_SWE = new string[] { "daily_swe_normal_nov_01", "daily_swe_normal_dec_01", "daily_swe_normal_jan_01",
                                                                         "daily_swe_normal_feb_01", "daily_swe_normal_mar_01", "daily_swe_normal_apr_01",
                                                                         "daily_swe_normal_may_01", "daily_swe_normal_jun_01", "daily_swe_normal_jul_01"};
        public const string FILE_SNODAS_SWE_APRIL = "swe_apr_01";
        public static readonly string[] FILES_SNODAS_SWE = new string[] { "swe_nov_01", "swe_dec_01", "swe_jan_01", "swe_feb_01", "swe_mar_01",
                                                                          FILE_SNODAS_SWE_APRIL, "swe_may_01", "swe_jun_01", "swe_jul_01"};
        public static readonly string[] FILES_SWE_ZONES = new string[] { "swe_nov_zone", "swe_dec_zone", "swe_jan_zone", "swe_feb_zone", "swe_mar_zone",
                                                                         "swe_apr_zone", "swe_may_zone", "swe_jun_zone", "swe_jul_zone"};
        public const string MAPS_SNODAS_MEAN_SWE = "SNODAS Mean SWE";
        public const string MAPS_SNODAS_SWE_DELTA = "SNODAS SWE Delta";
        public const string MAPS_SEASONAL_PRECIP_CONTRIB = "Precip Contribution";
        public static readonly string[] FILES_SWE_DELTA = new string[] { "swe_dec_minus_nov", "swe_jan_minus_dec", "swe_feb_minus_jan",
                                                                         "swe_mar_minus_feb", "swe_apr_minus_mar", "swe_may_minus_apr",
                                                                         "swe_jun_minus_may", "swe_jul_minus_jun"};
        public static readonly string[] FILES_SEASON_PRECIP_CONTRIB = new string[] {"sq1_precip_contrib", "sq2_precip_contrib", "sq3_precip_contrib",
                                                                                    "sq4_precip_contrib"};
        public static readonly string[] MAP_NAMES_SEASON_PRECIP_CONTRIB = new string[] {"SQ1 PRECIPITATION CONTRIBUTION", "SQ2 PRECIPITATION CONTRIBUTION", "SQ3 PRECIPITATION CONTRIBUTION",
                                                                                        "SQ4 PRECIPITATION CONTRIBUTION"};
        public const string FILE_BAGIS_MAP_PARAMETERS = "map_parameters.txt";
        public const string URI_IMAGE_SERVER = "/ImageServer";
        public const string URI_BATCH_TOOL_SETTINGS = "https://github.com/PSU-CSAR/bagis-pro/raw/master/Settings/bagis-pro.json";
        public const string LAYER_FILE_NLCD_LAND_COVER = "nlcd_land_cover.lyrx";
        public const string LAYER_FILE_PUBLIC_TRIBAL_LANDS = "Public_and_Tribal_Lands.lyrx";
        public const string LAYER_FILE_MTBS_FIRE = "mtbs_fire_data.lyrx";
        public const string LAYER_FILE_REFERENCE_MAPS = "BAGIS_Reference_Maps.lyrx";
        public const string LAYOUT_FILE_SNODAS_SWE = "SNODAS Layout.pagx";
        public const string LAYOUT_FILE_SNODAS_DELTA_SWE = "SNODAS Delta Layout.pagx";
        public const string LAYOUT_FILE_SEASONAL_PRECIP_CONTRIB = "Seasonal Precipitation Layout.pagx";


        public const string FOLDER_MAP_PACKAGE = "maps_publish";
        public const string FOLDER_MAPS = "maps";
        public const string FOLDER_LOGS = "logs";
        public const string FOLDER_SETTINGS = "BAGIS";
        public const string FOLDER_SNODAS_GEOJSON = "snodas_geojson";
        public const string FOLDER_FIRE_STATISTICS = "fire_statistics";
        public const string FOLDER_CHROME_USER_DATA = "chrome_user_data";

        public const string FIELD_SITE_ELEV = "BA_SELEV";
        public const string FIELD_SITE_NAME = "BA_SNAME";
        public const string FIELD_SITE_TYPE = "BA_STYPE";
        public const string FIELD_ASPECT = "BA_ASPECT";
        public const string FIELD_PRECIP = "BA_PRECIP";
        public const string FIELD_DIRECTION = "BA_DIRECT";
        public const string FIELD_SLOPE = "BA_SLOPE";
        public const string FIELD_SITE_ID = "BA_SITE_ID";
        public const string FIELD_RECALC_AREA = "BA_AREA";  // Feature area recalculated by BAGIS
        public const string FIELD_OBJECT_ID = "OBJECTID";
        public const string FIELD_STATION_TRIPLET = "stationTriplet";
        public const string FIELD_STATION_NAME = "stationName";
        public const string FIELD_NEAR_ID = "NEAR_FID";
        public const string FIELD_NEAR_DIST = "NEAR_DIST";
        public const string FIELD_VALUE = "VALUE";
        public const string FIELD_NAME = "NAME";
        public const int FIELD_NAME_WIDTH = 60;
        public const string FIELD_LBOUND = "LBOUND";
        public const string FIELD_UBOUND = "UBOUND";
        public const string FIELD_COUNT = "COUNT";
        public const string FIELD_RASTERVALU = "RASTERVALU";   //Field generated when using BA_ExtractValuesToPoints to populate BA_SELEV from DEM
        public const string FIELD_SUITABLE = "Suitable";   //Indicates public and tribal lands
        public const string FIELD_GRID_CODE = "gridcode";   //Value after raster is converted to polygon
        public const string FIELD_VOL_ACRE_FT = "VOL_ACRE_FT";
        public const string FIELD_SUM = "SUM";
        public const string FIELD_WINTER_START_MONTH = "winter_start_month";
        public const string FIELD_WINTER_END_MONTH = "winter_end_month";
        public const string FIELD_HUC = "huc";
        public const string FIELD_HUC2 = "huc2";
        public const string FIELD_LATITUDE = "latitude";
        public const string FIELD_LONGITUDE = "longitude";
        public const string FIELD_SOURCE_ID_FEATURE = "SrcID_Feat";
        public const string FIELD_SOURCE_ID_RASTER = "SrcID_Rast";
        public const string FIELD_SAMPLE_INPUT_1 = "v_raster_10";
        public const string FIELD_SAMPLE_INPUT_2 = "v_raster_20";
        public const string FIELD_SAMPLE_INPUT_3 = "v_raster_30";
        public const string FIELD_JOIN_COUNT = "Join_Count";
        public const string FIELD_JSON_TYPE = "type";
        public const string FIELD_JSON_ID = "id";
        public const string FIELD_JSON_NAME = "name";
        public const string FIELD_JSON_SOURCE = "source";
        public const string FIELD_JSON_PROPERTIES = "properties";
        public const string FIELD_JSON_GEOMETRIES = "geometries";
        public const string FIELD_AGBUR = "AGBUR";
        public const string FIELD_YEAR = "YEAR";
        public const string FIELD_FIRECURRENT_DATE = "attr_FireDiscoveryDateTime";
        public const string FIELD_FIRE_YEAR = "FIRE_YEAR";
        public const string FIELD_FIRECURRENT_INCIDENT = "attr_IncidentName";
        public const string FIELD_IRWIN_ID = "IRWINID";
        public const string FIELD_INCIDENT = "INCIDENT";
        public const string FIELD_BASIN = "BASIN";
        public const string FIELD_AOIREFAREA = "AOIREFAREA";
        public const string FIELD_AOIREFUNIT = "AOIREFUNIT";

        public const string DATA_TYPE_SWE = "Snotel SWE";
        public const string DATA_TYPE_PRECIPITATION = "Precipitation";
        public const string DATA_TYPE_SNOTEL = "SNOTEL";
        public const string DATA_TYPE_SNOW_COURSE = "Snow Course";
        public const string DATA_TYPE_ROADS = "Roads";
        public const string DATA_TYPE_LAND_OWNERSHIP = "Land Ownership";
        public const string DATA_TYPE_SWE_DELTA = "Snotel SWE Delta";
        public const string DATA_TYPE_LAND_COVER = "Land Cover";
        public const string DATA_TYPE_SNOLITE = "SNOLITE";
        public const string DATA_TYPE_COOP_PILLOW = "Coop Pillow";
        public const string DATA_TYPE_DEM = "DEM";
        public const string DATA_TYPE_ALASKA_DEM = "Alaska DEM";
        public const string DATA_TYPE_ALASKA_PRECIPITATION = "Alaska Precipitation";
        public const string DATA_TYPE_ALASKA_LAND_COVER = "Alaska Land Cover";
        public const string DATA_SOURCES_DEFAULT = "Default Data Sources";
        public const string DATA_SOURCES_ALASKA = "Alaska Data Sources";
        public const string DATA_TYPE_FIRE_HISTORY = "Fire History";
        public const string DATA_TYPE_FIRE_CURRENT = "Fire Current";
        public const string DATA_TYPE_FIRE_BURN_SEVERITY = "Fire Burn Severity";
        public const string DATA_TYPE_ALASKA_FIRE_BURN_SEVERITY = "Alaska Fire Burn Severity";

        public const int VALUE_NO_DATA_9999 = -9999;
        public const string VALUE_NO_DATA = "NoData";
        public const string VALUE_UNKNOWN = "Unknown";
        // BAGIS V3 allowed elevation intervals up to 5000; This tool only allows up to 1000
        public static readonly short[] VALUES_ELEV_INTERVALS = new short[] { 50, 100, 200, 250, 500, 1000 };
        public const int VALUE_ALASKA_HUC2 = 19;
        public const string VALUE_FORECAST_STATION_SEARCH_RADIUS = "500 Meters";
        public const string VALUE_NOT_SPECIFIED = "Not Specified";
        public const string VALUE_MISSING = "Missing";
        public const string VALUE_MTBS_SEVERITY_LOW = "Low";
        public const string VALUE_MTBS_SEVERITY_MODERATE = "Moderate";
        public const string VALUE_MTBS_SEVERITY_HIGH = "High";
        public static string[] MTBS_INCLUDE_SEVERITIES = { Constants.VALUE_MTBS_SEVERITY_LOW, Constants.VALUE_MTBS_SEVERITY_MODERATE, Constants.VALUE_MTBS_SEVERITY_HIGH };
        public static int[] VALUES_NLCD_FORESTED_AREA = { 41, 42, 43 };
        
        public const string UNITS_INCHES = "Inches";
        public const string UNITS_MILLIMETERS = "Millimeters";
        public const string UNITS_FEET = "Feet";
        public const string UNITS_METERS = "Meters";
        public const string UNITS_SQUARE_KM = "Square Km";
        public const double MAP_BUFFER_FACTOR = 1.3;
        public const string TEXT_SITES_TABLE_DESCR = "See Active Sites Table for characteristics of each snow monitoring site.";
        public const int PDF_EXPORT_RESOLUTION = 300;

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
                                                                "MapButtonPalette_BtnSwe_State",
                                                                "MapButtonPalette_BtnSweDelta_State",
                                                                "MapButtonPalette_BtnSeasonalPrecipContrib_State",
                                                                "MapButtonPalette_BtnLandOwnership_State",
                                                                "MapButtonPalette_BtnForestedArea_State",
                                                                "MapButtonPalette_BtnPublicLandZones_State",
                                                                "MapButtonPalette_BtnSitesLocationZone_State",
                                                                "MapButtonPalette_BtnSitesLocationPrecip_State",
                                                                "MapButtonPalette_BtnSitesLocationZone_State",
                                                                "MapButtonPalette_BtnAoiLocation_State",
                                                                "MapButtonPalette_BtnSitesLocationPrecipContrib_State",
                                                                "MapButtonPalette_BtnLandCover_State"};
        //public static string[] STATES_SITE_ANALYSIS_MAP_BUTTONS => new string[] {
        //                                                        "MapButtonPalette_BtnBelowTreeline_State",
        //                                                        "MapButtonPalette_BtnPublicLandZones_State",
        //                                                        "MapButtonPalette_BtnSitesLocationZone_State",
        //                                                        "MapButtonPalette_BtnPublicLandOwnership_State"};
        public static string[] STATES_BASIN_BUTTONS => new string[] { "bagis_pro_Buttons_SetBasinExtentTool_State",
        "bagis_pro_Buttons_BtnCreateBasin_State", "bagis_pro_Buttons_BtnDefineAoi_State", "bagis_pro_Buttons_BtnSetPourpoint_State",
        "bagis_pro_Buttons_BtnCreateAoi_State"};

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

        public static string TITLE_BASIN_ELEVATION = "BASIN ELEVATION";
        public static string TITLE_SLOPE = "Slope Distribution";
        public static string TITLE_ASPECT = "Aspect Distribution";
        public static string TITLE_PRECIPITATION = "PRECIPITATION DISTRIBUTION";
        public static string TITLE_AUTOMATED_SITES = "AUTOMATED SITE REPRESENTATION";
        public static string TITLE_SCOS_SITES = "SNOW COURSE SITE REPRESENTATION";
        public static string TITLE_SNOTEL_AUTO_SITES = "ALL SITE REPRESENTATION";
        public static string TITLE_ROADS_AND_TRIBAL = "ROADS, FEDERAL NON-WILDERNESS & TRIBAL LAND";
        public static string TITLE_FORESTED_LAND_COVER = "FORESTED LAND COVER";
        public static string TITLE_POTENTIAL_SITE_LOC = "POTENTIAL SITE LOCATIONS";
        public static string TITLE_SUBBASIN_ANNUAL_PRECIP_CONTRIB = "SUBBASIN ANNUAL PRECIPITATION CONTRIBUTION";
        public static string TITLE_CRITICAL_PRECIPITATION = "CRITICAL PRECIPITATION ZONES";
        public static string TITLE_LOCATION_MAP = "LOCATION MAP";
        public static string TITLE_LAND_OWNERSHIP = "LAND OWNERSHIP";
        public static string TITLE_WINTER_PRECIP = "WINTER PRECIPITATION";
        public static string TITLE_LAND_COVER = "LAND COVER";
        public static string TITLE_ELEV_PRECIP_CORR = "Elevation Precipitation";
        public static string TITLE_AREA_ELEV_PRECIP_SITE = "Area-Elevation, Precipitation and Site Distribution";
        public static string TITLE_PRECIP_REPRESENTATION = "Precipitation Representation Table";
        // The following 3 titles are maintained in their respective layout files
        public static string TITLE_SEASONAL_PRECIP_CONTRIB = "SEASONAL PRECIPITATION CONTRIBUTION MAPS";
        public static string TITLE_SNODAS_MEAN_SWE = "SNODAS MEAN SWE MAPS";
        public static string TITLE_SNODAS_MEAN_SWE_DELTA = "SNODAS MEAN SWE DELTA MAPS";
        public static string TITLE_FIRE_BLANK_PAGE = "FIRE DISTURBANCE";  

        public static string[] FILES_EXPORT_TITLES = new string[]
        { "Title Page", "Data Sources Table", TITLE_LOCATION_MAP, "Active Sites Table",
          TITLE_BASIN_ELEVATION + " MAP", TITLE_LAND_COVER + " MAP", TITLE_ASPECT + " MAP", TITLE_ASPECT + " CHART",
          TITLE_SLOPE + " MAP", TITLE_SLOPE + " CHART", TITLE_AUTOMATED_SITES + " MAP", TITLE_SCOS_SITES + " MAP",
          TITLE_SNOTEL_AUTO_SITES + " MAP", TITLE_PRECIPITATION + " MAP", TITLE_ELEV_PRECIP_CORR + " CHART",
          TITLE_SUBBASIN_ANNUAL_PRECIP_CONTRIB + " MAP", TITLE_AREA_ELEV_PRECIP_SITE + " CHART",
          TITLE_AREA_ELEV_PRECIP_SITE + " (CUMULATIVE) CHART", TITLE_CRITICAL_PRECIPITATION + " MAP", TITLE_PRECIP_REPRESENTATION,
          TITLE_WINTER_PRECIP + " MAP", TITLE_SEASONAL_PRECIP_CONTRIB, TITLE_SNODAS_MEAN_SWE, TITLE_SNODAS_MEAN_SWE_DELTA, TITLE_POTENTIAL_SITE_LOC + " MAP",
          TITLE_POTENTIAL_SITE_LOC + " PRECIPITATION MAP", TITLE_POTENTIAL_SITE_LOC + " PRECIP CONTRIB MAP", TITLE_ROADS_AND_TRIBAL + " MAP",
          TITLE_LAND_OWNERSHIP + " MAP", TITLE_FORESTED_LAND_COVER + " MAP" };



        public static int[,] ARR_SWE_COLORS = new int[,] {
            {163,255,115,255},  // green
            {156,156,156,255},  // grey
            {115,223,255,255},  // apatite blue
            {115,178,255,255},  // light blue
            {0,92,230,255},     // dark blue
            {197,0,255,255},  // amethyst
            {132,0,168,255}}; // dark amethyst

        public static int[,] ARR_SWE_DELTA_COLORS = new int[,] {
            {168,0,0,255},
            {255,0,0,255},
            {255,190,190,255},
            {163,255,115,255},  // green
            {190,210,255,255},
            {0,112,255,255},
            {0,77,168,255}};
    }

}
