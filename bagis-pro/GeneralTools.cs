using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;
using Microsoft.Office.Interop.Excel;
using ArcGIS.Desktop.Core;
using System.Reflection;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Desktop.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using ArcGIS.Desktop.Catalog;
using Microsoft.VisualBasic.FileIO;

namespace bagis_pro
{
    public class GeneralTools
    {

        public static string GetAddInDirectory()
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            return System.IO.Path.GetDirectoryName(
                              Uri.UnescapeDataString(
                                      new Uri(asm.CodeBase).LocalPath));
        }

        public static async Task<BA_ReturnCode> ExportMapToPdfAsync()
        {
            try
            {
                //Export a single page layout to PDF.

                //Create a PDF format with appropriate settings
                //BMP, EMF, EPS, GIF, JPEG, PNG, SVG, TGA, and TFF formats are also available for export
                PDFFormat PDF = new PDFFormat()
                {
                    OutputFileName = GetFullPdfFileName(Module1.Current.DisplayedMap),
                    Resolution = 300,
                    DoCompressVectorGraphics = true,
                    DoEmbedFonts = true,
                    HasGeoRefInfo = true,
                    ImageCompression = ImageCompression.Adaptive,
                    ImageQuality = ImageQuality.Best,
                    LayersAndAttributes = LayersAndAttributes.LayersAndAttributes
                };

                // Get a handle to the layout
                Layout layout = null;
                await QueuedTask.Run(() =>
                {
                    string layoutName = Constants.MAPS_DEFAULT_LAYOUT_NAME;
                    if (Module1.Current.DisplayedMap.Equals(Constants.FILE_EXPORT_MAP_AOI_LOCATION_PDF))
                    {
                        layoutName = Constants.MAPS_AOI_LOCATION_LAYOUT;
                    }
                    LayoutProjectItem lytItem =
                        Project.Current.GetItems<LayoutProjectItem>()
                        .FirstOrDefault(m => m.Name.Equals(layoutName, StringComparison.CurrentCultureIgnoreCase));
                    if (lytItem != null)
                    {
                        layout = lytItem.GetLayout();
                    }
                });

                //Check to see if the path is valid and export
                if (layout != null)
                {
                    if (PDF.ValidateOutputFilePath())
                    {
                        await QueuedTask.Run(() => layout.Export(PDF));  //Export the layout to PDF on the worker thread
                    }
                    Module1.Current.ModuleLogManager.LogDebug(nameof(ExportMapToPdfAsync),
                        "Published map to " + PDF.OutputFileName);
                    return BA_ReturnCode.Success;
                }
                else
                {
                    MessageBox.Show("Could not find default layout. Map cannot be exported!!", "BAGIS PRO");
                    Module1.Current.ModuleLogManager.LogError(nameof(ExportMapToPdfAsync),
                        "Could not find default layout. Map cannot be exported!!");
                    return BA_ReturnCode.UnknownError;
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("An error occurred while trying to export the map!! " + e.Message, "BAGIS PRO");
                Module1.Current.ModuleLogManager.LogError(nameof(ExportMapToPdfAsync),
                    "Exception: " + e.Message);
                return BA_ReturnCode.UnknownError;
            }

        }

        public static async Task<BA_ReturnCode> GenerateMapsTitlePageAsync(ReportType rType, 
            string strPublisher, string strComments)
        {
            try
            {
                // Download the runoff csv file from the NRCS Portal                
                string documentId = (string) Module1.Current.BatchToolSettings.AnnualRunoffItemId;
                string annualRunoffDataDescr = (string)Module1.Current.BatchToolSettings.AnnualRunoffDataDescr;
                string annualRunoffDataYear = (string)Module1.Current.BatchToolSettings.AnnualRunoffDataYear;

                Webservices ws = new Webservices();
                var success = await ws.GetPortalFile(Constants.PORTAL_ORGANIZATION, documentId, Module1.Current.SettingsPath + "\\" + Constants.FOLDER_SETTINGS +
                    "\\" + Constants.FILE_ANNUAL_RUNOFF_CSV);

                // Retrieve AOI Analysis object with settings for future use
                BA_Objects.Analysis oAnalysis = GetAnalysisSettings(Module1.Current.Aoi.FilePath);

                // Query for the annual runoff value
                double dblAnnualRunoff = QueryAnnualRunoffValue(Module1.Current.Aoi.StationTriplet);
                double dblRunoffRatio = -1;
                string strRunoffRatio = "No stream flow data";  // This is what we print if we couldn't get the runoff numbers
                if (dblAnnualRunoff >= 0)
                {
                    if (oAnalysis != null)
                    {
                        if (oAnalysis.PrecipVolumeKaf > 0)
                        {
                            dblRunoffRatio = dblAnnualRunoff / oAnalysis.PrecipVolumeKaf;
                            strRunoffRatio = dblRunoffRatio.ToString("0.##%");
                        }
                        else
                        {
                            Module1.Current.ModuleLogManager.LogDebug(nameof(GenerateMapsTitlePageAsync),
                                "PrecipVolumeKaf is missing from the analysis.xml file. Please generate the Excel tables before generating the title page!");
                        }
                    }            
                }

                // Query for the drainage area
                Uri gdbUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi));
                string strAreaSqKm = await GeodatabaseTools.QueryTableForSingleValueAsync(gdbUri, Constants.FILE_POURPOINT,
                                        Constants.FIELD_AOI_AREA, new QueryFilter());
                double areaSqKm = -1;
                bool isDouble = Double.TryParse(strAreaSqKm, out areaSqKm);

                //Query min/max from dem
                string sMask = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_VECTOR;
                IList<double> lstResult = await GeoprocessingTools.GetDemStatsAsync(Module1.Current.Aoi.FilePath, sMask, 0.005);
                double elevMinMeters = -1;
                double elevMaxMeters = -1;
                if (lstResult.Count == 2)   // We expect the min and max values in that order
                {
                    elevMinMeters = lstResult[0];
                    elevMaxMeters = lstResult[1];
                }

                // Counting Snotel Sites in AOI boundary
                gdbUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, false));
                Uri sitesGdbUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers, false));
                int snotelInBasin = await GeodatabaseTools.CountPointsWithinInFeatureAsync(sitesGdbUri, Constants.FILE_SNOTEL,
                    gdbUri, Constants.FILE_AOI_VECTOR);
                int snotelInBuffer = 0;
                int totalSnotelSites = await GeodatabaseTools.CountFeaturesAsync(sitesGdbUri, Constants.FILE_SNOTEL);
                if (totalSnotelSites > 0)
                {
                    snotelInBuffer = totalSnotelSites - snotelInBasin;
                }

                // Counting Snow Course Sites in AOI boundary
                int scosInBasin = await GeodatabaseTools.CountPointsWithinInFeatureAsync(sitesGdbUri, Constants.FILE_SNOW_COURSE,
                    gdbUri, Constants.FILE_AOI_VECTOR);
                int scosInBuffer = 0;
                int totalScosSites = await GeodatabaseTools.CountFeaturesAsync(sitesGdbUri, Constants.FILE_SNOW_COURSE);
                if (totalScosSites > 0)
                {
                    scosInBuffer = totalScosSites - scosInBasin;
                }

                // Retrieve Represented Area Parameters
                double siteElevRange = 0;
                string siteElevRangeUnits = "";
                double siteBufferDistance = 0;
                string siteBufferDistanceUnits = "";
                if (oAnalysis != null)
                {
                    siteElevRange = oAnalysis.UpperRange;
                    siteElevRangeUnits = oAnalysis.ElevUnitsText.ToLower();
                    siteBufferDistance = oAnalysis.BufferDistance;
                    siteBufferDistanceUnits = oAnalysis.BufferUnitsText.ToLower();
                }

                // Calculating percent represented area
                gdbUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, false));
                double pctSnotelRepresented = 0;
                double pctSnowCourseRepresented = 0;
                double pctAllSitesRepresented = 0;
                double aoiArea = await GeodatabaseTools.CalculateTotalPolygonAreaAsync(gdbUri, Constants.FILE_AOI_VECTOR);
                bool hasSnotelSites = false;
                bool hasScosSites = false;
                string snotelSitesBufferSize = "?";
                string snowCourseSitesBufferSize = "?";
                if (aoiArea > 0)
                {
                    gdbUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false));
                    if (totalSnotelSites > 0)
                    {
                        double repArea = await GeodatabaseTools.CalculateTotalPolygonAreaAsync(gdbUri, Constants.FILE_SNOTEL_REPRESENTED);
                        pctSnotelRepresented = Math.Round(repArea / aoiArea * 100);
                        hasSnotelSites = true;
                        string strPath = sitesGdbUri.LocalPath + "\\" + Constants.FILE_SNOTEL;
                        string strBagisTag = await GeneralTools.GetBagisTagAsync(strPath, Constants.META_TAG_XPATH);
                        if (!String.IsNullOrEmpty(strBagisTag))
                        {
                            string strBufferDistance = GeneralTools.GetValueForKey(strBagisTag, Constants.META_TAG_BUFFER_DISTANCE, ';');
                            string strBufferUnits = GeneralTools.GetValueForKey(strBagisTag, Constants.META_TAG_XUNIT_VALUE, ';');
                            if (!string.IsNullOrEmpty(strBufferDistance) && !string.IsNullOrEmpty(strBufferUnits))
                            {
                                snotelSitesBufferSize = strBufferDistance + " " + strBufferUnits;
                            }
                        }
                    }
                    if (totalScosSites > 0)
                    {
                        double repArea = await GeodatabaseTools.CalculateTotalPolygonAreaAsync(gdbUri, Constants.FILE_SCOS_REPRESENTED);
                        pctSnowCourseRepresented = Math.Round(repArea / aoiArea * 100);
                        hasScosSites = true;
                        string strPath = sitesGdbUri.LocalPath + "\\" + Constants.FILE_SNOW_COURSE;
                        string strBagisTag = await GeneralTools.GetBagisTagAsync(strPath, Constants.META_TAG_XPATH);
                        if (!String.IsNullOrEmpty(strBagisTag))
                        {
                            string strBufferDistance = GeneralTools.GetValueForKey(strBagisTag, Constants.META_TAG_BUFFER_DISTANCE, ';');
                            string strBufferUnits = GeneralTools.GetValueForKey(strBagisTag, Constants.META_TAG_XUNIT_VALUE, ';');
                            if (!string.IsNullOrEmpty(strBufferDistance) && !string.IsNullOrEmpty(strBufferUnits))
                            {
                                snowCourseSitesBufferSize = strBufferDistance + " " + strBufferUnits;
                            }
                        }
                    }
                    if (totalSnotelSites > 0 && totalScosSites > 0)
                    {
                        double repArea = await GeodatabaseTools.CalculateTotalPolygonAreaAsync(gdbUri, Constants.FILE_SITES_REPRESENTED);
                        pctAllSitesRepresented = Math.Round(repArea / aoiArea * 100);
                    }
                    else if (totalSnotelSites > 0)
                    {
                        pctAllSitesRepresented = pctSnotelRepresented;
                    }
                    else if (totalScosSites > 0)
                    {
                        pctAllSitesRepresented = pctSnowCourseRepresented;
                    }
                }

                //Printing data sources
                IDictionary<string, BA_Objects.DataSource> dictLocalDataSources = GeneralTools.QueryLocalDataSources();
                string[] keys = { Constants.DATA_TYPE_SWE, Constants.DATA_TYPE_PRECIPITATION, Constants.DATA_TYPE_SNOTEL,
                                  Constants.DATA_TYPE_SNOW_COURSE, Constants.DATA_TYPE_ROADS,
                                  Constants.DATA_TYPE_PUBLIC_LAND, Constants.DATA_TYPE_VEGETATION};
                //if (rType.Equals(ReportType.SiteAnalysis))
                //{
                //    Array.Resize(ref keys, 5);
                //    keys[0] = Constants.DATA_TYPE_SNOTEL;
                //    keys[1] = Constants.DATA_TYPE_SNOW_COURSE;
                //    keys[2] = Constants.DATA_TYPE_ROADS;
                //    keys[3] = Constants.DATA_TYPE_PUBLIC_LAND;
                //    keys[4] = Constants.DATA_TYPE_VEGETATION;
                //}
                IList<BA_Objects.DataSource> lstDataSources = new List<BA_Objects.DataSource>();
                foreach (string strKey in keys)
                {
                    if (dictLocalDataSources.ContainsKey(strKey))
                    {
                        BA_Objects.DataSource newSource = dictLocalDataSources[strKey];
                        lstDataSources.Add(newSource);
                    }
                }

                if (!string.IsNullOrEmpty(strComments))
                {
                    strComments = strComments.Trim();   // strip white space from comments
                }
                string strReportTitle = "Watershed Characteristics Report";
                //if (rType.Equals(ReportType.SiteAnalysis))
                //{
                //    strReportTitle = "Site Analysis Report";
                //}

                // Serialize the title page object
                BA_Objects.ExportTitlePage tPage = new BA_Objects.ExportTitlePage
                {
                    aoi_name = Module1.Current.Aoi.NwccName,
                    comments = strComments,
                    publisher = strPublisher,
                    local_path = Module1.Current.Aoi.FilePath,
                    streamgage_station = Module1.Current.Aoi.StationTriplet,
                    streamgage_station_name = Module1.Current.Aoi.StationName,
                    drainage_area_sqkm = areaSqKm,
                    elevation_min_meters = elevMinMeters,
                    elevation_max_meters = elevMaxMeters,
                    has_snotel_sites = hasSnotelSites,
                    snotel_sites_in_basin = snotelInBasin,
                    snotel_sites_in_buffer = snotelInBuffer,
                    snotel_sites_buffer_size = snotelSitesBufferSize,
                    has_scos_sites = hasScosSites,
                    scos_sites_in_basin = scosInBasin,
                    scos_sites_in_buffer = scosInBuffer,
                    scos_sites_buffer_size = snowCourseSitesBufferSize,
                    site_elev_range = siteElevRange,
                    site_elev_range_units = siteElevRangeUnits,
                    site_buffer_dist = siteBufferDistance,
                    site_buffer_dist_units = siteBufferDistanceUnits,
                    represented_snotel_percent = pctSnotelRepresented,
                    represented_snow_course_percent = pctSnowCourseRepresented,
                    represented_all_sites_percent = pctAllSitesRepresented,
                    annual_runoff_ratio = strRunoffRatio,
                    annual_runoff_data_descr = annualRunoffDataDescr,
                    annual_runoff_data_year = annualRunoffDataYear,
                    report_title = strReportTitle,
                    date_created = DateTime.Now
                };
                if (lstDataSources.Count > 0)
                {
                    BA_Objects.DataSource[] data_sources = new BA_Objects.DataSource[lstDataSources.Count];
                    lstDataSources.CopyTo(data_sources, 0);
                    tPage.data_sources = data_sources;
                }
                string publishFolder = Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_MAP_PACKAGE;
                string myXmlFile = publishFolder + "\\" + Constants.FILE_TITLE_PAGE_XML;
                System.Xml.Serialization.XmlSerializer writer = new System.Xml.Serialization.XmlSerializer(tPage.GetType());
                using (System.IO.FileStream fs = System.IO.File.Create(myXmlFile))
                {
                    writer.Serialize(fs, tPage);
                }

                // Process the title page through the xsl template
                string myStyleSheet = GeneralTools.GetAddInDirectory() + "\\" + Constants.FILE_TITLE_PAGE_XSL;
                XPathDocument myXPathDoc = new XPathDocument(myXmlFile);
                XslCompiledTransform myXslTrans = new XslCompiledTransform();
                myXslTrans.Load(myStyleSheet);
                string htmlFilePath = publishFolder + "\\" + Constants.FILE_TITLE_PAGE_HTML;
                using (XmlTextWriter myWriter = new XmlTextWriter(htmlFilePath, null))
                {
                    myXslTrans.Transform(myXPathDoc, null, myWriter);
                }

                // Convert the title page to PDF
                if (System.IO.File.Exists(htmlFilePath))
                {
                    PdfSharp.Pdf.PdfDocument titlePageDoc = TheArtOfDev.HtmlRenderer.PdfSharp.PdfGenerator.GeneratePdf(System.IO.File.ReadAllText(htmlFilePath),
                        PdfSharp.PageSize.Letter);
                    titlePageDoc.Save(publishFolder + "\\" + Constants.FILE_TITLE_PAGE_PDF);
                }
                Module1.Current.ModuleLogManager.LogDebug(nameof(GenerateMapsTitlePageAsync),
                    "Title page created!!");
                return BA_ReturnCode.Success;
            }
            catch (Exception e)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(GenerateMapsTitlePageAsync),
                    "Exception: " + e.Message);
                MessageBox.Show("An error occurred while trying to parse the XML!! " + e.Message, "BAGIS PRO");
                return BA_ReturnCode.UnknownError;
            }
        }

        public static IDictionary<string, BA_Objects.DataSource> QueryLocalDataSources()
        {
            IDictionary<string, BA_Objects.DataSource> dictDataSources = new Dictionary<string, BA_Objects.DataSource>();
            string settingsPath = Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_MAPS + "\\" +
                Constants.FILE_SETTINGS;
            if (File.Exists(settingsPath))
            {
                BA_Objects.Analysis oAnalysis = null;
                using (var file = new StreamReader(settingsPath))
                {
                    var reader = new System.Xml.Serialization.XmlSerializer(typeof(BA_Objects.Analysis));
                    oAnalysis = (BA_Objects.Analysis)reader.Deserialize(file);
                }
                if (oAnalysis.DataSources != null && oAnalysis.DataSources.Count > 0)
                {
                    foreach (var oSource in oAnalysis.DataSources)
                    {
                        string key = oSource.layerType;
                        if (!dictDataSources.ContainsKey(key))
                        {
                            dictDataSources.Add(key, oSource);
                        }
                    }
                }
                else
                {
                    // Make a copy of the file under a different name. It is from BAGIS V3 and we want to preserve it
                    string destPath = Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_MAPS + "\\" +
                        "analysis_" + DateTime.Now.ToString("yyyyMMdd") + ".xml";
                    File.Copy(settingsPath, destPath, true);
                }
            }
            return dictDataSources;
        }

        public static BA_ReturnCode SaveDataSourcesToFile(IDictionary<string, BA_Objects.DataSource> dictDataSources)
        {
            // Put the data sources into a list that can be added to the Analysis object
            List<BA_Objects.DataSource> lstDataSources = new List<BA_Objects.DataSource>();
            foreach (string key in dictDataSources.Keys)
            {
                lstDataSources.Add(dictDataSources[key]);
            }

            // Open the current Analysis.xml from disk, if it exists
            BA_Objects.Analysis oAnalysis = new BA_Objects.Analysis();
            string strSettingsFile = Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_MAPS + "\\" +
                Constants.FILE_SETTINGS;
            if (File.Exists(strSettingsFile))
            {
                using (var file = new StreamReader(strSettingsFile))
                {
                    var reader = new System.Xml.Serialization.XmlSerializer(typeof(BA_Objects.Analysis));
                    oAnalysis = (BA_Objects.Analysis)reader.Deserialize(file);
                }
            }

            // Overwrite the datasources on the analysis object and save
            oAnalysis.DataSources = lstDataSources;
            using (var file_stream = File.Create(strSettingsFile))
            {
                var serializer = new System.Xml.Serialization.XmlSerializer(typeof(BA_Objects.Analysis));
                serializer.Serialize(file_stream, oAnalysis);
            }
            return BA_ReturnCode.Success;
        }

        public static async Task<BA_ReturnCode> GenerateTablesAsync(bool bInteractive)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            // Declare Excel object variables
            Application objExcel = new Application();
            Workbook bkWorkBook = objExcel.Workbooks.Add();  //a file in excel

            try
            {
                // Create SNOTEL Distribution Worksheet
                Worksheet pSNOTELWorksheet = bkWorkBook.Sheets.Add();
                pSNOTELWorksheet.Name = "SNOTEL";

                // Create Snow Course Distribution Worksheet
                Worksheet pSnowCourseWorksheet = bkWorkBook.Sheets.Add();
                pSnowCourseWorksheet.Name = "Snow Course";

                //Create Slope Distribution Worksheet
                Worksheet pSlopeWorksheet = bkWorkBook.Sheets.Add();
                pSlopeWorksheet.Name = "Slope";

                //Create Aspect Distribution Worksheet
                Worksheet pAspectWorksheet = bkWorkBook.Sheets.Add();
                pAspectWorksheet.Name = "Aspect";

                // Create Elevation Distribution Worksheet
                Worksheet pAreaElvWorksheet = bkWorkBook.Sheets.Add();
                pAreaElvWorksheet.Name = "Area Elevations";

                // Create PRISM Distribution Worksheet
                Worksheet pPRISMWorkSheet = bkWorkBook.Sheets.Add();
                pPRISMWorkSheet.Name = "PRISM";

                // Do we create the Elevation-Precip tables and chart?
                Worksheet pPrecipSiteWorksheet = null;
                Worksheet pPrecipDemElevWorksheet = null;
                Worksheet pPrecipChartWorksheet = null;

                Uri uriAnalysis = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false));
                Uri uriLayers = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers, false));
                bool bPrecMeanElevTableExists = await GeodatabaseTools.FeatureClassExistsAsync(uriAnalysis, Constants.FILE_PREC_MEAN_ELEV_V);
                bool bMergedSitesExists = await GeodatabaseTools.FeatureClassExistsAsync(uriAnalysis, Constants.FILE_MERGED_SITES);
                if (!bMergedSitesExists)
                {
                    Module1.Current.ModuleLogManager.LogInfo(nameof(GenerateTablesAsync), Constants.FILE_MERGED_SITES +
                        " is missing. Creating it now...");
                    // Create the merged sites layer if it doesn't exist
                    string returnPath = await AnalysisTools.CreateSitesLayerAsync(uriAnalysis);
                    if (string.IsNullOrEmpty(returnPath))
                    {
                        bMergedSitesExists = true;
                    }
                }
                else
                {
                    // If it exists, check to make sure the direction and prism fields exist
                    bool bDirectionField = await GeodatabaseTools.AttributeExistsAsync(uriAnalysis, Constants.FILE_MERGED_SITES, Constants.FIELD_DIRECTION);
                    bool bPrecipField = await GeodatabaseTools.AttributeExistsAsync(uriAnalysis, Constants.FILE_MERGED_SITES, Constants.FIELD_PRECIP);
                    bool bSiteIdField = await GeodatabaseTools.AttributeExistsAsync(uriAnalysis, Constants.FILE_MERGED_SITES, Constants.FIELD_SITE_ID);
                    if (!bDirectionField || !bPrecipField || !bSiteIdField)
                    {
                        // At least one of the fields was missing, recreate the layer
                        Module1.Current.ModuleLogManager.LogInfo(nameof(GenerateTablesAsync), Constants.FILE_MERGED_SITES +
                            " was missing a critical field. Recreating it now...");
                        string returnPath = await AnalysisTools.CreateSitesLayerAsync(uriLayers);
                        if (string.IsNullOrEmpty(returnPath))
                        {
                            bMergedSitesExists = true;
                        }
                        else
                        {
                            bMergedSitesExists = false;
                        }
                    }
                }
                if (bPrecMeanElevTableExists)
                {
                    // Create Elevation Precipitation Worksheet
                    pPrecipDemElevWorksheet = bkWorkBook.Sheets.Add();
                    pPrecipDemElevWorksheet.Name = "Elev-Precip AOI";

                    // Create Elev-Precip Chart Worksheet
                    pPrecipChartWorksheet = bkWorkBook.Sheets.Add();
                    pPrecipChartWorksheet.Name = "Elev-Precip Chart";
                }
                if (bMergedSitesExists)
                {
                    // Create Site Precipitation Worksheet
                    pPrecipSiteWorksheet = bkWorkBook.Sheets.Add();
                    pPrecipSiteWorksheet.Name = "Elev-Precip Sites";
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(GenerateTablesAsync), Constants.FILE_MERGED_SITES +
                        " is missing. Precipitation correlation tables and chart cannot be created!");
                }

                // Create Charts Worksheet
                Worksheet pChartsWorksheet = bkWorkBook.Sheets.Add();
                pChartsWorksheet.Name = "Charts";

                // Query min/max from dem
                string sMask = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_RASTER;
                IList<double> lstResult = await GeoprocessingTools.GetDemStatsAsync(Module1.Current.Aoi.FilePath, sMask, 0.005);
                double elevMinMeters = -1;
                double elevMaxMeters = -1;
                if (lstResult.Count == 2)   // We expect the min and max values in that order
                {
                    elevMinMeters = lstResult[0];
                    elevMaxMeters = lstResult[1];
                }
                Module1.Current.ModuleLogManager.LogDebug(nameof(GenerateTablesAsync), "Queried min/max from DEM. Min is " + elevMinMeters);

                success = await ExcelTools.CreateElevationTableAsync(pAreaElvWorksheet, elevMinMeters);
                if (success == BA_ReturnCode.Success)
                {
                    Module1.Current.ModuleLogManager.LogInfo(nameof(GenerateTablesAsync), "Created Elevation Table");
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogInfo(nameof(GenerateTablesAsync), "An error occurred while trying to create the Elevation Table");
                    return success;
                }

                Module1.Current.Aoi.HasSnotel = true;
                int intSites = await GeodatabaseTools.CountFeaturesAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers, false)), Constants.FILE_SNOTEL);
                if (intSites < 1)
                {
                    Module1.Current.Aoi.HasSnotel = false;
                }
                if (Module1.Current.Aoi.HasSnotel)
                {
                    if (await GeodatabaseTools.RasterDatasetExistsAsync(uriAnalysis, Constants.FILE_SNOTEL_ZONE))
                    {
                        success = await ExcelTools.CreateSitesTableAsync(pSNOTELWorksheet, pAreaElvWorksheet, Constants.FILE_SNOTEL_ZONE);
                        Module1.Current.ModuleLogManager.LogInfo(nameof(GenerateTablesAsync), "Created Snotel sites Table");
                    }
                    else
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(GenerateTablesAsync), Constants.FILE_SNOTEL_ZONE + " is missing. " +
                            "Snotel sites table could not be created!");
                    }
                }

                Module1.Current.Aoi.HasSnowCourse = true;
                intSites = await GeodatabaseTools.CountFeaturesAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers, false)), Constants.FILE_SNOW_COURSE);
                if (intSites < 1)
                {
                    Module1.Current.Aoi.HasSnowCourse = false;
                }
                if (Module1.Current.Aoi.HasSnowCourse)
                {
                    if (await GeodatabaseTools.RasterDatasetExistsAsync(uriAnalysis, Constants.FILE_SCOS_ZONE))
                    {
                        success = await ExcelTools.CreateSitesTableAsync(pSnowCourseWorksheet, pAreaElvWorksheet, Constants.FILE_SCOS_ZONE);
                        Module1.Current.ModuleLogManager.LogInfo(nameof(GenerateTablesAsync), "Created Snow Course sites Table");
                    }
                    else
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(GenerateTablesAsync), Constants.FILE_SCOS_ZONE + " is missing. " +
                            " Snow Course sites table could not be created!");
                    }
                }

                string strPrecipPath = Module1.Current.Aoi.FilePath + "\\" + Module1.Current.BatchToolSettings.AoiPrecipFile;
                success = await ExcelTools.CreatePrecipitationTableAsync(pPRISMWorkSheet,
                   strPrecipPath, elevMinMeters);

                double MaxPct = -1;
                int lastRow = -1;
                if (success == BA_ReturnCode.Success)
                {
                    // copy DEM area and %_area to the PRISM table
                    success = ExcelTools.CopyCells(pAreaElvWorksheet, 3, pPRISMWorkSheet, 12);
                    success = ExcelTools.CopyCells(pAreaElvWorksheet, 10, pPRISMWorkSheet, 13);
                    int rowCountPrism = ExcelTools.EstimatePrecipitationVolume(pPRISMWorkSheet, 12, 7, 14, 15);
                    lastRow = rowCountPrism + 2;
                    // get %_VOL for top PRISM axis
                    WorksheetFunction wsf = objExcel.WorksheetFunction;
                    Microsoft.Office.Interop.Excel.Range rng = pPRISMWorkSheet.Range["O3:O" + lastRow];
                    MaxPct = wsf.Max(rng);

                    // Update table with Critical Precipitation Zone information
                    Uri uriElevZones = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false));
                    IList<BA_Objects.Interval> lstIntervals = await GeodatabaseTools.ReadReclassRasterAttribute(uriElevZones, Constants.FILE_ELEV_ZONE);
                    double dblMinVolume = (double) Module1.Current.BatchToolSettings.CriticalPrecipMinMeanVolInches;                   
                    double dblMaxPctVolume = (double)Module1.Current.BatchToolSettings.CriticalPrecipTotalMaxVolPct;
                    IList<string> lstCriticalZoneValues = ExcelTools.CreateCriticalPrecipitationZones(pPRISMWorkSheet, lstIntervals, dblMinVolume, dblMaxPctVolume);

                    // Add textbox comments to worksheet
                    if (lastRow > 10)
                    {
                        ChartTextBoxSettings textBoxSettings = new ChartTextBoxSettings
                        {
                            Left = 10,
                            Top = lastRow * 15,
                            Height = 80,
                            Width = 725,
                            Message = "Critical precipitation elevation zone selection criteria: \r\n" +
                            "Criterion #1. mean annual precipitation > 20 inches \r\n" +
                            "Criterion #2. %_VOL > 100% / (2 x zone#) \r\n" +
                            "Criterion #3. higher %_VOL, but the total %_VOL of the selected zones must not exceed 66.67% (i.e., 2/3) \r\n" +
                            "Red cells are zones meeting all criteria \r\n" +
                            "Blue cells are zones meeting criterion #1 \r\n" +
                            "Orange cells are zones meeting criteria #1 and #2"
                        };
                        pPRISMWorkSheet.Shapes.AddTextbox(Microsoft.Office.Core.MsoTextOrientation.msoTextOrientationHorizontal, 
                                                           textBoxSettings.Left,
                                                           textBoxSettings.Top, textBoxSettings.Width, textBoxSettings.Height).
                                                           TextFrame.Characters().Text = textBoxSettings.Message;
                    }
                    
                    // Extract Critical Precipitation Zone layer
                    if (lstCriticalZoneValues.Count > 0)
                    {
                        success = await AnalysisTools.ExtractCriticalPrecipitationZonesAsync(Module1.Current.Aoi.FilePath, lstCriticalZoneValues);
                    }
                    else
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(GenerateTablesAsync), "No critical precipitation zones were identified. " +
                            "Critical precipitation zone layer not created!!");
                    }
                }

                // Try to get elevation interval from analysis.xml settings file
                BA_Objects.Analysis oAnalysis = new BA_Objects.Analysis();
                string strSettingsFile = Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_MAPS + "\\" +
                    Constants.FILE_SETTINGS;
                if (File.Exists(strSettingsFile))
                {
                    using (var file = new StreamReader(strSettingsFile))
                    {
                        var reader = new System.Xml.Serialization.XmlSerializer(typeof(BA_Objects.Analysis));
                        oAnalysis = (BA_Objects.Analysis)reader.Deserialize(file);
                    }
                }
                double Y_Unit = Convert.ToDouble(Module1.Current.BatchToolSettings.DefaultDemInterval);
                if (oAnalysis.ElevationZonesInterval > 0)
                {
                    Y_Unit = oAnalysis.ElevationZonesInterval;
                }
                double Y_Max = -99.0F;
                double minValue = elevMinMeters;
                double maxValue = elevMaxMeters;
                int leftPosition = Constants.EXCEL_CHART_WIDTH + (Constants.EXCEL_CHART_SPACING * 15);
                //aoiDemMin is always in meters
                string strDemDisplayUnits = (string)Module1.Current.BatchToolSettings.DemDisplayUnits;
                if (strDemDisplayUnits.Equals("Feet"))
                {
                    minValue = ArcGIS.Core.Geometry.LinearUnit.Meters.ConvertTo(elevMinMeters, ArcGIS.Core.Geometry.LinearUnit.Feet);
                    maxValue = ArcGIS.Core.Geometry.LinearUnit.Meters.ConvertTo(elevMaxMeters, ArcGIS.Core.Geometry.LinearUnit.Feet);
                }

                double Y_Min = ExcelTools.ConfigureYAxis(minValue, maxValue, Y_Unit, ref Y_Max);
                success = ExcelTools.CreateCombinedChart(pPRISMWorkSheet, pAreaElvWorksheet, pChartsWorksheet, pSNOTELWorksheet,
                    pSnowCourseWorksheet, Constants.EXCEL_CHART_SPACING, Constants.EXCEL_CHART_SPACING, Y_Max, Y_Min, Y_Unit,
                    Math.Ceiling(MaxPct), false);
                Module1.Current.ModuleLogManager.LogInfo(nameof(GenerateTablesAsync), "Created Combined Chart");

                success = await ExcelTools.CreateSlopeTableAsync(pSlopeWorksheet);
                Module1.Current.ModuleLogManager.LogInfo(nameof(GenerateTablesAsync), "Created Slope Table");
                if (success == BA_ReturnCode.Success)
                {
                    success = ExcelTools.CreateSlopeChart(pSlopeWorksheet, pChartsWorksheet,
                        Constants.EXCEL_CHART_SPACING, leftPosition);
                    Module1.Current.ModuleLogManager.LogInfo(nameof(GenerateTablesAsync), "Created Slope Chart");
                }

                success = await ExcelTools.CreateAspectTableAsync(pAspectWorksheet);
                Module1.Current.ModuleLogManager.LogInfo(nameof(GenerateTablesAsync), "Created Aspect Table");
                int topPosition = Constants.EXCEL_CHART_HEIGHT + (Constants.EXCEL_CHART_SPACING * 25);
                if (success == BA_ReturnCode.Success)
                {
                    success = ExcelTools.CreateAspectChart(pAspectWorksheet, pChartsWorksheet,
                        topPosition, Constants.EXCEL_CHART_SPACING);
                    Module1.Current.ModuleLogManager.LogInfo(nameof(GenerateTablesAsync), "Created Aspect Chart");
                }

                if (success == BA_ReturnCode.Success)
                {
                    success = ExcelTools.CreateCombinedChart(pPRISMWorkSheet, pAreaElvWorksheet, pChartsWorksheet, pSNOTELWorksheet,
                        pSnowCourseWorksheet, topPosition, leftPosition, Y_Max, Y_Min, Y_Unit, Math.Ceiling(MaxPct), true);
                }

                //Elevation-Precipitation Correlation Chart
                int intMinPrecip = -1;
                if (bPrecMeanElevTableExists)
                {
                    intMinPrecip = await ExcelTools.CreateRepresentPrecipTableAsync(pPrecipDemElevWorksheet, strPrecipPath);
                    Module1.Current.ModuleLogManager.LogInfo(nameof(GenerateTablesAsync), "Created represented precip table");
                }
                if (intMinPrecip != 999 && bMergedSitesExists)
                {
                    success = await ExcelTools.CreateSnotelPrecipTableAsync(pPrecipSiteWorksheet,
                            new List<BA_Objects.Site>());
                    Module1.Current.ModuleLogManager.LogInfo(nameof(GenerateTablesAsync), "Created Snotel Represented Precip Table");

                }
                if (intMinPrecip != 999 && intMinPrecip != -1)
                {
                    success = ExcelTools.CreateRepresentPrecipChart(pPrecipDemElevWorksheet, pPrecipSiteWorksheet, pPrecipChartWorksheet, intMinPrecip, Y_Min);
                    Module1.Current.ModuleLogManager.LogInfo(nameof(GenerateTablesAsync), "Created Represented Precip Chart");
                }

                //Publish Charts Tab
                if (bInteractive == false)
                {
                    // Combined chart
                    string sOutputFolder = Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_MAP_PACKAGE + "\\";
                    string pathToSave = GetFullPdfFileName(Constants.FILE_EXPORT_CHART_AREA_ELEV_PRECIP_SITE_PDF);

                    XlPaperSize oPaperSize = XlPaperSize.xlPaperLetter;
                    try
                    {
                        oPaperSize = pChartsWorksheet.PageSetup.PaperSize;
                    }
                    catch (Exception e)
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(GenerateTablesAsync),
                            "Exception: " + e.Message);
                        MessageBox.Show("An error occurred while querying Excel's paper size! Please test printing from Excel and try again", "BAGIS-PRO");
                        return BA_ReturnCode.UnknownError;
                    }

                    XlPaperSize oReqPaperSize = XlPaperSize.xlPaperLetter;
                    pChartsWorksheet.PageSetup.Zoom = false;
                    pChartsWorksheet.PageSetup.PaperSize = oReqPaperSize;
                    pChartsWorksheet.PageSetup.FitToPagesTall = 1;
                    pChartsWorksheet.PageSetup.FitToPagesWide = 1;
                    pChartsWorksheet.PageSetup.PrintArea = "$A$1:$M$30";
                    pChartsWorksheet.PageSetup.CenterHeader = "&C&\"Arial,Bold\"&16 " + Module1.Current.Aoi.NwccName;
                    pChartsWorksheet.ExportAsFixedFormat(XlFixedFormatType.xlTypePDF, pathToSave);
                    Module1.Current.ModuleLogManager.LogInfo(nameof(GenerateTablesAsync), "Published combined chart to PDF");

                    // slope chart
                    pathToSave = GetFullPdfFileName(Constants.FILE_EXPORT_CHART_SLOPE_PDF);
                    pChartsWorksheet.PageSetup.PrintArea = "$O$1:$AA$30";
                    pChartsWorksheet.ExportAsFixedFormat(XlFixedFormatType.xlTypePDF, pathToSave);
                    Module1.Current.ModuleLogManager.LogInfo(nameof(GenerateTablesAsync), "Published slope chart to PDF");

                    // aspect chart
                    pathToSave = GetFullPdfFileName(Constants.FILE_EXPORT_CHART_ASPECT_PDF);
                    pChartsWorksheet.PageSetup.PrintArea = "$A$32:$M$61";
                    pChartsWorksheet.ExportAsFixedFormat(XlFixedFormatType.xlTypePDF, pathToSave);
                    Module1.Current.ModuleLogManager.LogInfo(nameof(GenerateTablesAsync), "Published aspect chart to PDF");

                    //Cumulative precip table
                    pathToSave = GetFullPdfFileName(Constants.FILE_EXPORT_TABLE_PRECIP_REPRESENT_PDF);
                    pPRISMWorkSheet.PageSetup.PrintArea = "$A$1:$P$" + (lastRow + 7);  // Extend print area for comment textbox
                    pPRISMWorkSheet.PageSetup.Orientation = XlPageOrientation.xlLandscape;
                    pPRISMWorkSheet.PageSetup.Zoom = false;     // Required to print on one page
                    pPRISMWorkSheet.PageSetup.PaperSize = oReqPaperSize;    // Required to print on one page
                    pPRISMWorkSheet.PageSetup.PrintGridlines = true;
                    pPRISMWorkSheet.PageSetup.CenterHeader = "&C&\"Arial,Bold\"&16 " + Module1.Current.Aoi.NwccName; ;
                    string strTitle = "Precipitation Representation Table";
                    if (!String.IsNullOrEmpty(oAnalysis.PrecipZonesBegin))
                    {
                        strTitle = "Precipitation (" + LookupTables.PrismText[oAnalysis.PrecipZonesBegin] + ") Representation Table";
                    }
                    pPRISMWorkSheet.PageSetup.LeftHeader = ((char)13).ToString() + "&\"Arial,Bold\"&12 " +
                        strTitle;
                    pPRISMWorkSheet.PageSetup.TopMargin = 0.8 * 72;   // Convert inches to points
                    pPRISMWorkSheet.get_Range("B:C").EntireColumn.Hidden = true;
                    pPRISMWorkSheet.PageSetup.FitToPagesTall = 1;   // Required to print on one page
                    pPRISMWorkSheet.PageSetup.FitToPagesWide = 1;   // Required to print on one page
                    pPRISMWorkSheet.ExportAsFixedFormat(XlFixedFormatType.xlTypePDF, pathToSave);
                    Module1.Current.ModuleLogManager.LogInfo(nameof(GenerateTablesAsync), "Published represented precip table to PDF");

                    // cumulative precipitation chart
                    pathToSave = GetFullPdfFileName(Constants.FILE_EXPORT_CHART_PRECIP_REPRESENT_PDF);
                    pChartsWorksheet.PageSetup.PrintArea = "$O$32:$AA$61";
                    pChartsWorksheet.ExportAsFixedFormat(XlFixedFormatType.xlTypePDF, pathToSave);
                    Module1.Current.ModuleLogManager.LogInfo(nameof(GenerateTablesAsync), "Published represented precip chart to PDF");

                    // Elev-Precip Chart Tab
                    if (bPrecMeanElevTableExists && intMinPrecip != 999)
                    {
                        oPaperSize = pPrecipChartWorksheet.PageSetup.PaperSize;
                        pPrecipChartWorksheet.PageSetup.Orientation = XlPageOrientation.xlLandscape;
                        pPrecipChartWorksheet.PageSetup.Zoom = false;
                        pPrecipChartWorksheet.PageSetup.PaperSize = oReqPaperSize;
                        pPrecipChartWorksheet.PageSetup.FitToPagesTall = 1;
                        pPrecipChartWorksheet.PageSetup.FitToPagesWide = 1;
                        pPrecipChartWorksheet.PageSetup.CenterHeader = "&C&\"Arial,Bold\"&16 " + Module1.Current.Aoi.NwccName;
                        pathToSave = GetFullPdfFileName(Constants.FILE_EXPORT_CHART_ELEV_PRECIP_CORR_PDF);
                        pPrecipChartWorksheet.ExportAsFixedFormat(XlFixedFormatType.xlTypePDF, pathToSave);
                        pPrecipChartWorksheet.PageSetup.PaperSize = oPaperSize;
                        Module1.Current.ModuleLogManager.LogInfo(nameof(GenerateTablesAsync), "Published elevation-precipitation correlation chart to PDF");

                    }

                }

                return success;
            }
            catch (Exception e)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(GenerateTablesAsync), "Exception: " + e.StackTrace);
                return BA_ReturnCode.UnknownError;
            }
            finally
            {
                if (bInteractive == true && success == BA_ReturnCode.Success)
                {
                    objExcel.Visible = true;
                }
                else
                {
                    if (bkWorkBook != null)
                    {
                        bkWorkBook.Close(false);
                    }
                    if (objExcel != null)
                    {
                        objExcel.Quit();
                        objExcel = null;
                    }
                }
            }
        }

        public static async Task<string> GetBagisTagAsync(string layerPath, string propertyPath)
        {
            string strReturn = "";
            var fc = ItemFactory.Instance.Create(layerPath, ItemFactory.ItemType.PathItem);
            if (fc != null)
            {
                string strXml = string.Empty;
                strXml = await QueuedTask.Run(() => fc.GetXml());
                //check metadata was returned
                if (!string.IsNullOrEmpty(strXml))
                {
                    //use the metadata; Create a .NET XmlDocument and load the schema
                    XmlDocument myXml = new XmlDocument();
                    myXml.LoadXml(strXml);
                    //Select the nodes from the fully qualified XPath
                    XmlNodeList propertyNodes = myXml.SelectNodes(propertyPath);
                    //Place each innerText into a list to return
                    foreach (XmlNode pNode in propertyNodes)
                    {
                        if (pNode.InnerText.IndexOf(Constants.META_TAG_PREFIX) > -1)
                        {
                            return pNode.InnerText;
                        }
                    }
                }
            }
            return strReturn;
        }

        //strSplit should be ; for local files and ! for image services
        public static string GetValueForKey(string innerText, string keyText, char charSplit)
        {
            string strValue = "";
            string[] arrContents = innerText.Split(charSplit);
            foreach (string pValue in arrContents)
            {
                //This tag contains the zUnitCategory
                if (pValue.IndexOf(keyText) > -1)
                {
                    //Example: ZUnitCategory|Depth
                    strValue = pValue.Substring(pValue.IndexOf("|") + 1);
                    //Strip trailing ";" if exists
                    if (strValue.Substring(strValue.Length - 1) == ";")
                    {
                        strValue = strValue.Remove(strValue.Length - 1, 1);
                    }
                    break;
                }
            }
            return strValue;
        }

        public static async Task<BA_ReturnCode> UpdateMetadataAsync(string layerPath, string propertyPath,
                                                               string innerText, int matchLength)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            var fc = ItemFactory.Instance.Create(layerPath, ItemFactory.ItemType.PathItem);
            if (fc != null)
            {
                string strXml = string.Empty;
                strXml = await QueuedTask.Run(() => fc.GetXml());
                //check metadata was returned
                if (!string.IsNullOrEmpty(strXml))
                {
                    //use the metadata; Create a .NET XmlDocument and load the schema
                    XmlDocument myXml = new XmlDocument();
                    myXml.LoadXml(strXml);
                    //Check to see if the parent node exists
                    char sep = '/';
                    int lastSep = propertyPath.LastIndexOf(sep);
                    string parentNodePath = propertyPath.Substring(0, lastSep);
                    XmlNodeList parentNodeList = myXml.SelectNodes(parentNodePath);
                    if (parentNodeList.Count < 1)
                    {
                        AddMetadataNode(ref myXml, parentNodePath, sep);
                        parentNodeList = myXml.SelectNodes(parentNodePath);
                    }
                    // Assume we want the first one
                    XmlNode parentNode = parentNodeList[0];
                    string childNodeName = propertyPath.Substring(lastSep + 1);
                    XmlNodeList propertyNodeList = parentNode.ChildNodes;
                    string matchPrefix = innerText.Substring(0, matchLength);
                    bool foundIt = false;
                    foreach (XmlNode pNode in propertyNodeList)
                    {
                        //Is the node the same node name we need to update?
                        if (pNode.Name == childNodeName)
                        {
                            //Is the first part of the innerText the same as what we want to update? 
                            if (pNode.InnerText.Length > matchLength &&
                                pNode.InnerText.Substring(0, matchLength) == matchPrefix)
                            {
                                //If so, update the innerText
                                pNode.InnerText = innerText;
                                foundIt = true;
                            }
                        }
                    }
                    //If it didn't exist, we need to create a new node
                    if (foundIt == false)
                    {
                        // Create the child node
                        XmlNode childNode = myXml.CreateNode(XmlNodeType.Element, childNodeName, null);
                        childNode.InnerText = innerText;
                        // Attach the child to the parent
                        parentNode.AppendChild(childNode);
                    }

                    await QueuedTask.Run(() => fc.SetXml(myXml.OuterXml));

                }
            }
            success = BA_ReturnCode.Success;
            return success;
        }

        public static XmlDocument UpdateMetadata(string strXml, string propertyPath,
                                                   string innerText, int matchLength)
        {
            XmlDocument myXml = new XmlDocument();
            if (!string.IsNullOrEmpty(strXml))
            {
                //use the metadata; Create a .NET XmlDocument and load the schema
                myXml.LoadXml(strXml);
                //Check to see if the parent node exists
                char sep = '/';
                int lastSep = propertyPath.LastIndexOf(sep);
                string parentNodePath = propertyPath.Substring(0, lastSep);
                XmlNodeList parentNodeList = myXml.SelectNodes(parentNodePath);
                if (parentNodeList.Count < 1)
                {
                    AddMetadataNode(ref myXml, parentNodePath, sep);
                    parentNodeList = myXml.SelectNodes(parentNodePath);
                }
                // Assume we want the first one
                XmlNode parentNode = parentNodeList[0];
                string childNodeName = propertyPath.Substring(lastSep + 1);
                XmlNodeList propertyNodeList = parentNode.ChildNodes;
                string matchPrefix = innerText.Substring(0, matchLength);
                bool foundIt = false;
                foreach (XmlNode pNode in propertyNodeList)
                {
                    //Is the node the same node name we need to update?
                    if (pNode.Name == childNodeName)
                    {
                        //Is the first part of the innerText the same as what we want to update? 
                        if (pNode.InnerText.Length > matchLength &&
                            pNode.InnerText.Substring(0, matchLength) == matchPrefix)
                        {
                            //If so, update the innerText
                            pNode.InnerText = innerText;
                            foundIt = true;
                        }
                    }
                }
                //If it didn't exist, we need to create a new node
                if (foundIt == false)
                {
                    // Create the child node
                    XmlNode childNode = myXml.CreateNode(XmlNodeType.Element, childNodeName, null);
                    childNode.InnerText = innerText;
                    // Attach the child to the parent
                    parentNode.AppendChild(childNode);
                }

                //await QueuedTask.Run(() => fc.SetXml(myXml.OuterXml));
            }
            return myXml;
        }

        //This is a simplified updating of the BAGIS tag for a new layer
        //Because we create the new layer, we assume there is no existing metadata
        public static async Task<BA_ReturnCode> CreateMetadataUnits(string layerPath, string strCategory,
                                                                    string strUnits)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            //We need to add a new tag at "/metadata/dataIdInfo/searchKeys/keyword"
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(Constants.META_TAG_PREFIX);
            sb.Append(Constants.META_TAG_ZUNIT_CATEGORY + strCategory + "; ");
            sb.Append(Constants.META_TAG_ZUNIT_VALUE + strUnits + "; ");
            sb.Append(Constants.META_TAG_SUFFIX);
            success = await GeneralTools.UpdateMetadataAsync(layerPath, Constants.META_TAG_XPATH, sb.ToString(),
                Constants.META_TAG_PREFIX.Length);
            success = BA_ReturnCode.Success;
            return success;
        }

        //This function adds a node to an xml document; The node is identified by its XPath
        //The separator is usually a "/"
        //An example XPath is: /metadata/dataIdInfo/searchKeys/keyword
        public static BA_ReturnCode AddMetadataNode(ref XmlDocument pXmlDoc, string nodePath, char sep)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            // Parse the propertyPath into its components
            string[] propNames = nodePath.Split(sep);
            string parentXPath = null;
            XmlNode parentNode = null;
            foreach (var pName in propNames)
            {
                if (!string.IsNullOrEmpty(pName))
                {
                    parentXPath = parentXPath + sep + pName;
                    // Select the nodes from the fully qualified XPath
                    XmlNodeList propertyNodes = pXmlDoc.SelectNodes(parentXPath);
                    if (propertyNodes.Count > 0)
                        // parentXPath exists
                        // assume the node we want is the first one
                        parentNode = propertyNodes[0];
                    else
                    {
                        // Create the new child node
                        XmlNode newChildNode = pXmlDoc.CreateNode(XmlNodeType.Element, pName, null/* TODO Change to default(_) if this is not a reference type */);
                        // append it to the parent
                        parentNode.AppendChild(newChildNode);
                        parentNode = newChildNode;
                    }
                }
            }
            success = BA_ReturnCode.Success;
            return success;
        }

        public static async Task<BA_Objects.Aoi> SetAoiAsync(string strAoiPath)
        {
            // Initialize AOI object
            BA_Objects.Aoi oAoi = new BA_Objects.Aoi(Path.GetFileName(strAoiPath), strAoiPath);
            // Set reference to dockpane to update layers
            var layersPane = (DockpaneLayersViewModel)FrameworkApplication.DockPaneManager.Find("bagis_pro_DockpaneLayers");
            try
            {
                // Load batch tool settings; Make sure we have the central BAGIS folder
                string strSettingsPath = GetBagisSettingsPath();
                if (!string.IsNullOrEmpty(strSettingsPath))
                {
                    string strTempPath = strSettingsPath + @"\" + Constants.FOLDER_SETTINGS;
                    if (!Directory.Exists(strTempPath))
                    {
                        DirectoryInfo dirInfo = Directory.CreateDirectory(Constants.FOLDER_SETTINGS);
                    }
                    strSettingsPath = strTempPath;

                }
                // Check to see if batch tool settings are already there
                if (!File.Exists(strSettingsPath + @"\" + Constants.FILE_BATCH_TOOL_SETTINGS))
                {
                    Webservices ws = new Webservices();
                    var success = Task.Run(() => ws.DownloadBatchSettingsAsync(Module1.Current.DefaultEbagisServer,
                        strSettingsPath + @"\" + Constants.FILE_BATCH_TOOL_SETTINGS));
                    if ((BA_ReturnCode)success.Result == BA_ReturnCode.Success)
                    {
                        Module1.Current.ModuleLogManager.LogDebug(nameof(SetAoiAsync),
                            "Copied default batch tool settings to BAGIS folder");
                    }
                    else
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(SetAoiAsync),
                            "Unable to copy default batch tool settings to BAGIS folder");
                    }

                }
                // Load batch tool settings from file
                if (File.Exists(strSettingsPath + @"\" + Constants.FILE_BATCH_TOOL_SETTINGS))
                {
                    // read JSON directly from a file
                    using (FileStream fs = File.OpenRead(strSettingsPath + @"\" + Constants.FILE_BATCH_TOOL_SETTINGS))
                    {
                        using (JsonTextReader reader = new JsonTextReader(new StreamReader(fs)))
                        {
                            dynamic oBatchSettings = (JObject)JToken.ReadFrom(reader);
                            if (oBatchSettings != null)
                            {
                                Module1.Current.BatchToolSettings = oBatchSettings;
                            }
                            string server = oBatchSettings.EBagisServer;
                        }
                    }
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(SetAoiAsync),
                        "Unable to locate batch tool settings in BAGIS folder");
                }

                // Set logger to AOI directory
                string logFolderName = strAoiPath + "\\" + Constants.FOLDER_LOGS;
                Module1.Current.ModuleLogManager.UpdateLogFileLocation(logFolderName);

                // Query for station information and save it in the aoi object
                string[] arrValues = new string[2];
                await QueuedTask.Run(async() =>
                {
                    arrValues = await AnalysisTools.GetStationValues(oAoi.FilePath);
                    Module1.Current.ModuleLogManager.LogDebug(nameof(SetAoiAsync),
                        "Station values returned. Array length: " +arrValues.Length);

                    if (arrValues.Length == 2)
                    {
                        oAoi.StationTriplet = arrValues[0];
                        oAoi.StationName = arrValues[1];
                    }
                    if (!string.IsNullOrEmpty(oAoi.StationTriplet))
                    {
                        string[] arrResults = await GeneralTools.QueryMasterAoiProperties(oAoi.StationTriplet);
                        Module1.Current.ModuleLogManager.LogDebug(nameof(SetAoiAsync),
                            "Master AOI properties returned. Array length: " + arrValues.Length);
                        if (arrResults.Length == 4)
                        {
                            oAoi.NwccName = arrResults[0];
                            if (! string.IsNullOrEmpty(arrResults[1]))
                            {
                                oAoi.WinterStartMonth = Convert.ToInt32(arrResults[1]);
                            }
                            if (!string.IsNullOrEmpty(arrResults[2]))
                            {
                                oAoi.WinterEndMonth = Convert.ToInt32(arrResults[2]);
                            }
                            if (!string.IsNullOrEmpty(arrResults[3]))
                            {
                                oAoi.Huc = Convert.ToString(arrResults[3]);
                                Module1.Current.ModuleLogManager.LogDebug(nameof(SetAoiAsync),
                                    "HUC set to " + oAoi.Huc);
                            }
                        }
                        else
                        {
                            Module1.Current.ModuleLogManager.LogError(nameof(SetAoiAsync),
                                "Unable to retrieve nwcc name from feature service!");
                        }
                    }
                    else
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(SetAoiAsync),
                            "Unable to retrieve station triplet. Nwcc name could not be found!");
                    }
                });
                string strSurfacesGdb = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Surfaces, false);
                var fcPath = strSurfacesGdb + "\\" + Constants.FILE_DEM_FILLED;
                await QueuedTask.Run(() =>
                {
                    FolderType fType = FolderType.FOLDER;
                    Uri gdbUri = new Uri(GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Aoi, false));
                    if (Directory.Exists(gdbUri.LocalPath))
                    {
                        Uri uriToCheck = new Uri(gdbUri.LocalPath + Constants.FILE_AOI_RASTER);
                        using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(gdbUri)))
                        {
                            IReadOnlyList<RasterDatasetDefinition> definitions = geodatabase.GetDefinitions<RasterDatasetDefinition>();
                            foreach (RasterDatasetDefinition def in definitions)
                            {
                                if (def.GetName().Equals(Constants.FILE_AOI_RASTER))
                                {
                                    fType = FolderType.AOI;
                                    break;
                                }
                            }
                        }
                    }

                    if (fType != FolderType.AOI)
                    {
                        MessageBox.Show("!!The selected folder does not contain a valid AOI", "BAGIS Pro");
                        return;
                    }

                    // Make sure that maps and maps_publish folders exist
                    string[] arrDirectories = { strAoiPath + "\\" + Constants.FOLDER_MAPS, strAoiPath + "\\" + Constants.FOLDER_MAP_PACKAGE };
                    foreach (var directory in arrDirectories)
                    {
                        if (!Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }
                    }

                    // Check for default units
                    var fc = ItemFactory.Instance.Create(fcPath, ItemFactory.ItemType.PathItem);
                    if (fc != null)
                    {
                        string strXml = string.Empty;
                        strXml = fc.GetXml();
                        //check metadata was returned
                        string strBagisTag = GetBagisTag(strXml);
                        if (!string.IsNullOrEmpty(strBagisTag))
                        {
                            oAoi.ElevationUnits = GetValueForKey(strBagisTag, Constants.META_TAG_ZUNIT_VALUE, ';');
                        }
                    }

                    // Make directory for log if it doesn't exist
                    if (!Directory.Exists(strAoiPath + "\\" + Constants.FOLDER_LOGS))
                    {
                        DirectoryInfo info = Directory.CreateDirectory(Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_LOGS);
                        if (info == null)
                        {
                            MessageBox.Show("Unable to create logs directory in Aoi folder!!", "BAGIS-PRO");
                        }
                    }

                    // Update PRISM data status
                    layersPane.ResetView();
                    gdbUri = new Uri(GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Prism, false));
                    bool bExists = false;
                    if (gdbUri.IsFile)
                    {
                        string strFolderPath = Path.GetDirectoryName(gdbUri.LocalPath);
                        if (Directory.Exists(strFolderPath))
                        {
                            using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(gdbUri)))
                            {
                                IReadOnlyList<RasterDatasetDefinition> definitions = geodatabase.GetDefinitions<RasterDatasetDefinition>();
                                foreach (RasterDatasetDefinition def in definitions)
                                {
                                    if (def.GetName().Equals(PrismFile.Annual.ToString()))
                                    {
                                        bExists = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    layersPane.Prism_Checked = bExists;
                    fcPath = gdbUri.LocalPath + "\\" + PrismFile.Annual.ToString();
                    if (bExists)
                    {
                        // Check for default units
                        fc = ItemFactory.Instance.Create(fcPath, ItemFactory.ItemType.PathItem);
                        if (fc != null)
                        {
                            string strXml = string.Empty;
                            strXml = fc.GetXml();
                            //check metadata was returned
                            string strBagisTag = GetBagisTag(strXml);
                            if (!string.IsNullOrEmpty(strBagisTag))
                            {
                                string tempBufferDistance = GetValueForKey(strBagisTag, Constants.META_TAG_BUFFER_DISTANCE, ';');
                                string tempBufferUnits = GetValueForKey(strBagisTag, Constants.META_TAG_XUNIT_VALUE, ';');
                                if (!String.IsNullOrEmpty(tempBufferDistance))
                                {
                                    layersPane.PrismBufferDistance = tempBufferDistance;
                                }
                                else
                                {
                                    Module1.Current.ModuleLogManager.LogDebug(nameof(SetAoiAsync),
                                        "Unable to locate PRISM buffer distance on annual layer. Using default");
                                }
                                if (!String.IsNullOrEmpty(tempBufferUnits))
                                {
                                    layersPane.PrismBufferUnits = tempBufferUnits;
                                }
                                else
                                {
                                    Module1.Current.ModuleLogManager.LogDebug(nameof(SetAoiAsync),
                                        "Unable to locate PRISM units on annual layer. Using default");
                                }
                            }
                        }
                    }

                    // Update data status for files in layers.gdb
                    string[] arrCheckLayers = new string[] { Constants.FILE_SNODAS_SWE_APRIL, Constants.FILE_SNOTEL,
                                                             Constants.FILE_SNOW_COURSE, Constants.FILE_ROADS,
                                                             Constants.FILE_PUBLIC_LAND, Constants.FILE_VEGETATION_EVT};
                    bool[] arrLayerExists = new bool[] { false, false, false, false, false, false };
                    gdbUri = new Uri(GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Layers, false));
                    bExists = false;
                    if (gdbUri.IsFile)
                    {
                        string strFolderPath = Path.GetDirectoryName(gdbUri.LocalPath);
                        if (Directory.Exists(strFolderPath))
                        {
                            using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(gdbUri)))
                            {
                                IReadOnlyList<RasterDatasetDefinition> definitions = geodatabase.GetDefinitions<RasterDatasetDefinition>();
                                IReadOnlyList<FeatureClassDefinition> featureDefinitions = geodatabase.GetDefinitions<FeatureClassDefinition>();
                                int j = 0;
                                foreach (var layerName in arrCheckLayers)
                                {
                                    foreach (RasterDatasetDefinition def in definitions)
                                    {
                                        if (def.GetName().Equals(layerName))
                                        {
                                            arrLayerExists[j] = true;
                                            break;
                                        }
                                    }
                                    foreach (FeatureClassDefinition def in featureDefinitions)
                                    {
                                        if (def.GetName().Equals(layerName))
                                        {
                                            arrLayerExists[j] = true;
                                            break;
                                        }
                                    }
                                    j++;
                                }
                            }

                            int i = 0;
                            foreach (var layerName in arrCheckLayers)
                            {
                                string bufferDistance = "";
                                string bufferUnits = "";
                                if (arrLayerExists[i] == true)
                                {
                                    // Check for default units
                                    fcPath = gdbUri.LocalPath + "\\" + layerName;
                                    fc = ItemFactory.Instance.Create(fcPath, ItemFactory.ItemType.PathItem);
                                    if (fc != null)
                                    {
                                        string strXml = string.Empty;
                                        strXml = fc.GetXml();
                                        //check metadata was returned
                                        string strBagisTag = GetBagisTag(strXml);
                                        if (!string.IsNullOrEmpty(strBagisTag))
                                        {
                                            bufferDistance = GetValueForKey(strBagisTag, Constants.META_TAG_BUFFER_DISTANCE, ';');
                                            bufferUnits = GetValueForKey(strBagisTag, Constants.META_TAG_XUNIT_VALUE, ';');
                                        }
                                    }
                                    switch (layerName)
                                    {
                                        case Constants.FILE_SNODAS_SWE_APRIL:
                                            layersPane.SWE_Checked = true;
                                            if (!string.IsNullOrEmpty(bufferDistance))
                                                layersPane.SWEBufferDistance = bufferDistance;
                                            if (!string.IsNullOrEmpty(bufferUnits))
                                                layersPane.SWEBufferUnits = bufferUnits;
                                            break;
                                        case Constants.FILE_SNOTEL:
                                            layersPane.SNOTEL_Checked = true;
                                            if (!string.IsNullOrEmpty(bufferDistance))
                                            {
                                                layersPane.SnotelBufferDistance = bufferDistance;
                                            }
                                            else
                                            {
                                                layersPane.SnotelBufferDistance = "";
                                            }
                                            if (!string.IsNullOrEmpty(bufferUnits))
                                                layersPane.SnotelBufferUnits = bufferUnits;
                                            break;
                                        case Constants.FILE_SNOW_COURSE:
                                            layersPane.SnowCos_Checked = true;
                                            if (!string.IsNullOrEmpty(bufferDistance))
                                            {
                                                layersPane.SnowCosBufferDistance = bufferDistance;
                                            }
                                            else
                                            {
                                                layersPane.SnowCosBufferDistance = "";
                                            }
                                            if (!string.IsNullOrEmpty(bufferUnits))
                                                layersPane.SnowCosBufferUnits = bufferUnits;
                                            break;
                                        case Constants.FILE_ROADS:
                                            layersPane.Roads_Checked = true;
                                            if (!string.IsNullOrEmpty(bufferDistance))
                                            {
                                                layersPane.RoadsBufferDistance = bufferDistance;
                                            }
                                            else
                                            {
                                                layersPane.RoadsBufferDistance = "";
                                            }
                                            if (!string.IsNullOrEmpty(bufferUnits))
                                                layersPane.RoadsBufferUnits = bufferUnits;
                                            break;
                                        case Constants.FILE_PUBLIC_LAND:
                                            layersPane.PublicLands_Checked = true;
                                            if (!string.IsNullOrEmpty(bufferDistance))
                                            {
                                                layersPane.PublicLandsBufferDistance = bufferDistance;
                                            }
                                            else
                                            {
                                                layersPane.PublicLandsBufferDistance = "";
                                            }
                                            if (!string.IsNullOrEmpty(bufferUnits))
                                                layersPane.PublicLandsBufferUnits = bufferUnits;
                                            break;
                                        case Constants.FILE_VEGETATION_EVT:
                                            layersPane.Vegetation_Checked = true;
                                            if (!string.IsNullOrEmpty(bufferDistance))
                                            {
                                                layersPane.VegetationBufferDistance = bufferDistance;
                                            }
                                            else
                                            {
                                                layersPane.VegetationBufferDistance = "";
                                            }
                                            if (!string.IsNullOrEmpty(bufferUnits))
                                                layersPane.VegetationBufferUnits = bufferUnits;
                                            break;
                                        default:
                                            Module1.Current.ModuleLogManager.LogError(nameof(SetAoiAsync),
                                                "Unidentified layer name");
                                            break;
                                    }
                                }

                                i++;
                            }
                        }
                    }

                    // Store current AOI in Module1
                    Module1.Current.Aoi = oAoi;
                });

                MapTools.DeactivateMapButtons();
                Module1.ActivateState("Aoi_Selected_State");
                Module1.ActivateState("BtnExcelTables_State");
                return oAoi;
            }
            catch (Exception e)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(SetAoiAsync),
                    "Exception: " + e.Message);
                Module1.Current.ModuleLogManager.LogError(nameof(SetAoiAsync), e.StackTrace);
                return null;
            }
        }

        public static string GetBagisTag(string strXml)
        {
            string strRetVal = "";
            //check metadata was returned
            if (!string.IsNullOrEmpty(strXml))
            {
                //use the metadata; Create a .NET XmlDocument and load the schema
                XmlDocument myXml = new XmlDocument();
                myXml.LoadXml(strXml);
                //Select the nodes from the fully qualified XPath
                XmlNodeList propertyNodes = myXml.SelectNodes(Constants.META_TAG_XPATH);
                //Place each innerText into a list to return
                foreach (XmlNode pNode in propertyNodes)
                {
                    if (pNode.InnerText.IndexOf(Constants.META_TAG_PREFIX) > -1)
                    {
                        strRetVal = pNode.InnerText;
                    }
                }
            }
            return strRetVal;
        }

        public static int CreateRangeArray(double minval, double maxval, double interval, out IList<BA_Objects.Interval> rangearr)
        {
            //check the decimal place of the interval value
            string intvstring = Convert.ToString(interval);
            //determine the interval decimal place to add an increment value to the lower bound
            int position = intvstring.IndexOf(".") + 1;
            int scalefactor;
            double inc_value;

            if (position == 0 && interval > 1)
            {
                scalefactor = 1;    //interval is an integer larger than 1
                inc_value = 1;
            }
            else if (interval == 1)
            {
                scalefactor = 10;
                inc_value = 1 / 10;
            }
            else
            {
                scalefactor = Convert.ToInt32(Math.Pow(10, (intvstring.Length - position)));
                inc_value = 1 / (Math.Pow(10, (intvstring.Length - position)));
            }
            //adjust value based on the scalefactor
            if (scalefactor > 1)
            {
                minval = Math.Round(minval * scalefactor - 0.5);
                maxval = Math.Round(maxval * scalefactor + 0.5);
                interval = interval * scalefactor;
            }
            // calculate the number of intervals
            int begincnt = Convert.ToInt16(Math.Floor(minval / interval));
            int endcnt = Convert.ToInt16(Math.Floor(maxval / interval)) + 1;
            int rightoffset;
            //rightoffset indicates if the upperbound of the last interval equals maxval
            if (maxval % interval == 0)
            {
                rightoffset = 1;
            }
            else
            {
                rightoffset = 0;
            }
            int ninterval = endcnt - begincnt - rightoffset;
            if (ninterval <= 0)
            {
                ninterval = 1;
            }
            // set the min and max range values
            rangearr = new List<BA_Objects.Interval>();
            for (int i = 0; i < ninterval; i++)
            {
                rangearr.Add(new BA_Objects.Interval());
            }
            rangearr[0].LowerBound = minval / scalefactor;

            // set intermediate range values
            double Value = (begincnt + 1) * interval;
            for (int j = 0; j < ninterval; j++)
            {
                rangearr[j].Value = j + 1;  // Add 1 to counter because we don't want any zone 0
                rangearr[j].UpperBound = Value / scalefactor;
                if (j + 1 < ninterval)
                {
                    rangearr[j + 1].LowerBound = Value / scalefactor;
                }
                Value = Value + interval;
                if (j == 1)
                {
                    rangearr[j].Name = rangearr[j].LowerBound + " - " + rangearr[j].UpperBound;
                }
                else
                {
                    rangearr[j].Name = rangearr[j].LowerBound + " - " + rangearr[j].UpperBound;
                }
            }

            rangearr[ninterval - 1].Value = ninterval;
            rangearr[ninterval - 1].UpperBound = maxval / scalefactor;
            if (ninterval > 1)
            {
                rangearr[ninterval - 1].Name = rangearr[ninterval - 1].LowerBound + " - " + rangearr[ninterval - 1].UpperBound;
            }
            else
            {
                rangearr[ninterval - 1].Name = rangearr[ninterval - 1].LowerBound + " - " + rangearr[ninterval - 1].UpperBound;
            }
            return ninterval;
        }

        public static string GetBagisSettingsPath()
        {
            string settingsPath = Environment.GetEnvironmentVariable(Constants.FOLDER_SETTINGS);
            // Note: this environment variable is starting with Windows 7; We use this for Windows 7 instead of the TEMP variables because
            // ArcGIS 10 deletes items from the TEMP folder under Windows 7
            if (string.IsNullOrEmpty(settingsPath))
            {
                settingsPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            }
            if (string.IsNullOrEmpty(settingsPath))     // then, try the TMP folder
            {
                settingsPath = Environment.GetEnvironmentVariable("TMP");
            }
            if (string.IsNullOrEmpty(settingsPath))     // then, try the TEMP folder
            {
                settingsPath = Environment.GetEnvironmentVariable("TEMP");
            }
            Module1.Current.SettingsPath = settingsPath;
            Module1.Current.ModuleLogManager.LogDebug(nameof(GetBagisSettingsPath),
                "Settings path set to " + settingsPath);
            return settingsPath;
        }

        public static BA_ReturnCode PublishFullPdfDocument(string outputPath, ReportType rType)
        {
            // Initialize output document
            PdfDocument outputDocument = new PdfDocument();
            //Iterate through files
            string[] arrAllFiles = Constants.FILES_EXPORT_WATERSHED_PDF;
            //if (rType.Equals(ReportType.SiteAnalysis))
            //{
            //    arrAllFiles = Constants.FILES_EXPORT_SITE_ANALYSIS_PDF;
            //}
            //else
            //{
            // Create intro section of report
            int idx = 0;
            PdfDocument combineDocument = new PdfDocument();
            foreach (var strFileName in Constants.FILE_EXPORT_OVERVIEW_FILES)
            {
                string fullPath = GetFullPdfFileName(strFileName);
                if (File.Exists(fullPath))
                {
                    PdfDocument inputDocument = PdfReader.Open(fullPath, PdfDocumentOpenMode.Import);
                    // Iterate pages
                    int count = inputDocument.PageCount;
                    for (idx = 0; idx < count; idx++)
                    {
                        // Get the page from the external document...
                        PdfPage page = inputDocument.Pages[idx];
                        combineDocument.AddPage(page);
                    }
                    File.Delete(fullPath);
                }
            }
            if (idx > 0)
            {
                combineDocument.Save(GetFullPdfFileName(Constants.FILE_EXPORT_OVERVIEW_PDF));
            }
            // Combine aspect files
            combineDocument = new PdfDocument();
            idx = 0;
            foreach (var strFileName in Constants.FILE_EXPORT_ASPECT_FILES)
            {
                string fullPath = GetFullPdfFileName(strFileName);
                if (File.Exists(fullPath))
                {
                    PdfDocument inputDocument = PdfReader.Open(fullPath, PdfDocumentOpenMode.Import);
                    // Get the page from the external document...
                    PdfPage page = inputDocument.Pages[0];
                    combineDocument.AddPage(page);
                    idx++;
                    File.Delete(fullPath);
                }
            }
            if (idx > 0)
            {
                combineDocument.Save(GetFullPdfFileName(Constants.FILE_EXPORT_ASPECT_DISTRIBUTION_PDF));
            }
            // Combine slope files
            combineDocument = new PdfDocument();
            idx = 0;
            foreach (var strFileName in Constants.FILE_EXPORT_SLOPE_FILES)
            {
                string fullPath = GetFullPdfFileName(strFileName);
                if (File.Exists(fullPath))
                {
                    PdfDocument inputDocument = PdfReader.Open(fullPath, PdfDocumentOpenMode.Import);
                    // Get the page from the external document...
                    PdfPage page = inputDocument.Pages[0];
                    combineDocument.AddPage(page);
                    idx++;
                    File.Delete(fullPath);
                }
            }
            if (idx > 0)
            {
                combineDocument.Save(GetFullPdfFileName(Constants.FILE_EXPORT_SLOPE_DISTRIBUTION_PDF));
            }
            // Combine sites files
            combineDocument = new PdfDocument();
            idx = 0;
            foreach (var strFileName in Constants.FILE_EXPORT_SITE_REPRESENTATION_FILES)
            {
                string fullPath = GetFullPdfFileName(strFileName);
                if (File.Exists(fullPath))
                {
                    PdfDocument inputDocument = PdfReader.Open(fullPath, PdfDocumentOpenMode.Import);
                    // Get the page from the external document...
                    PdfPage page = inputDocument.Pages[0];
                    combineDocument.AddPage(page);
                    idx++;
                    File.Delete(fullPath);
                }
            }
            if (idx > 0)
            {
                combineDocument.Save(GetFullPdfFileName(Constants.FILE_EXPORT_SITE_REPRESENTATION_PDF));
            }

            // Combine precipitation distribution files
            combineDocument = new PdfDocument();
            idx = 0;
            foreach (var strFileName in Constants.FILE_EXPORT_PRECIPITATION_DISTRIBUTION_FILES)
            {
                string fullPath = GetFullPdfFileName(strFileName);
                if (File.Exists(fullPath))
                {
                    PdfDocument inputDocument = PdfReader.Open(fullPath, PdfDocumentOpenMode.Import);
                    // Iterate pages
                    int count = inputDocument.PageCount;
                    for (idx = 0; idx < count; idx++)
                    {
                        // Get the page from the external document...
                        PdfPage page = inputDocument.Pages[idx];
                        combineDocument.AddPage(page);
                    }
                    File.Delete(fullPath);
                }
            }
            if (idx > 0)
            {
                combineDocument.Save(GetFullPdfFileName(Constants.FILE_EXPORT_PRECIPITATION_DISTRIBUTION_PDF));
            }
            // Potential site locations
            combineDocument = new PdfDocument();
            idx = 0;
            foreach (var strFileName in Constants.FILE_EXPORT_SITE_ANALYSIS_FILES)
            {
                string fullPath = GetFullPdfFileName(strFileName);
                if (File.Exists(fullPath))
                {
                    PdfDocument inputDocument = PdfReader.Open(fullPath, PdfDocumentOpenMode.Import);
                    // Get the page from the external document...
                    PdfPage page = inputDocument.Pages[0];
                    combineDocument.AddPage(page);
                    idx++;
                    File.Delete(fullPath);
                }
            }
            if (idx > 0)
            {
                combineDocument.Save(GetFullPdfFileName(Constants.FILE_EXPORT_POTENTIAL_SITE_ANALYSIS_PDF));
            }

            // Combine SWE Delta maps into a single .pdf document
            // Initialize output document
            PdfDocument snodasOutputDocument = new PdfDocument();
            // Combine monthly SNODAS maps into a single .pdf document
            foreach (var strFileName in Constants.FILE_EXPORT_MAPS_SWE)
            {
                string fullPath = GetFullPdfFileName(strFileName);
                if (File.Exists(fullPath))
                {
                        PdfDocument inputDocument = PdfReader.Open(fullPath, PdfDocumentOpenMode.Import);
                        // Iterate pages
                        int count = inputDocument.PageCount;
                        for (idx = 0; idx < count; idx++)
                        {
                            // Get the page from the external document...
                            PdfPage page = inputDocument.Pages[idx];
                            snodasOutputDocument.AddPage(page);
                        }
                        File.Delete(fullPath);
                }
            }
            foreach (var strFileName in Constants.FILE_EXPORT_MAPS_SWE_DELTA)
            {
                string fullPath = GetFullPdfFileName(strFileName);
                if (File.Exists(fullPath))
                {
                    PdfDocument inputDocument = PdfReader.Open(fullPath, PdfDocumentOpenMode.Import);
                    // Iterate pages
                    int count = inputDocument.PageCount;
                    for (idx = 0; idx < count; idx++)
                    {
                        // Get the page from the external document...
                        PdfPage page = inputDocument.Pages[idx];
                        snodasOutputDocument.AddPage(page);
                    }
                    File.Delete(fullPath);
                }
            }
            if (idx > 0)
                {
                    snodasOutputDocument.Save(GetFullPdfFileName(Constants.FILE_EXPORT_SNODAS_SWE_PDF));
                }

            // Combine monthly SQ PrecipContribution maps into a single .pdf document
            PdfDocument seasonalPrecipOutputDocument = new PdfDocument();
            idx = 0;
            // Winter Precipitation map
            PdfDocument winterDocument = PdfReader.Open(GetFullPdfFileName(Constants.FILE_EXPORT_MAP_WINTER_PRECIPITATION_PDF), 
                PdfDocumentOpenMode.Import);
            PdfPage winterPage = winterDocument.Pages[0];
            seasonalPrecipOutputDocument.AddPage(winterPage);
            File.Delete(GetFullPdfFileName(Constants.FILE_EXPORT_MAP_WINTER_PRECIPITATION_PDF));
            foreach (var strFileName in Constants.FILE_EXPORT_MAPS_SEASONAL_PRECIP_CONTRIB)
            {
                string fullPath = GetFullPdfFileName(strFileName);
                if (File.Exists(fullPath))
                {
                    PdfDocument inputDocument = PdfReader.Open(fullPath, PdfDocumentOpenMode.Import);
                    // Iterate pages
                    int count = inputDocument.PageCount;
                    for (idx = 0; idx < count; idx++)
                    {
                        // Get the page from the external document...
                        PdfPage page = inputDocument.Pages[idx];
                        seasonalPrecipOutputDocument.AddPage(page);
                    }
                    File.Delete(fullPath);
                }
            }
            if (idx > 0)
            {
                seasonalPrecipOutputDocument.Save(GetFullPdfFileName(Constants.FILE_EXPORT_SEASONAL_PRECIP_DISTRIBUTION_PDF));
            }

            //}
            foreach (string strFileName in arrAllFiles)
            {
                string fullPath = GetFullPdfFileName(strFileName);
                if (File.Exists(fullPath))
                {
                    PdfDocument inputDocument = PdfReader.Open(fullPath, PdfDocumentOpenMode.Import);
                    // Iterate pages
                    int count = inputDocument.PageCount;
                    for (idx = 0; idx < count; idx++)
                    {
                        // Get the page from the external document...
                        PdfPage page = inputDocument.Pages[idx];
                        outputDocument.AddPage(page);
                    }
                }
            }
            // Save final document
            outputDocument.Save(outputPath);
            return BA_ReturnCode.Success;
        }

        public static BA_ReturnCode ConcatenatePagesInPdf(string outputPath, IList<string> lstToConcat)
        {
            // Initialize output document
            PdfDocument outputDocument = new PdfDocument();
            //Iterate through files
            foreach (string fullPath in lstToConcat)
            {
                if (File.Exists(fullPath))
                {
                    PdfDocument inputDocument = PdfReader.Open(fullPath, PdfDocumentOpenMode.Import);
                    // Iterate pages
                    int count = inputDocument.PageCount;
                    for (int idx = 0; idx < count; idx++)
                    {
                        // Get the page from the external document...
                        PdfPage page = inputDocument.Pages[idx];
                        outputDocument.AddPage(page);
                    }
                }
            }

            // Save final document
            outputDocument.Save(outputPath);
            return BA_ReturnCode.Success;
        }

        public static BA_ReturnCode ReadBagisMapParameters(string aoiFilePath)
        {
            BA_ReturnCode success = BA_ReturnCode.ReadError;
            // Create the analysis object
            BA_Objects.Analysis oAnalysis = new BA_Objects.Analysis();
            string strSettingsFile = aoiFilePath + "\\" + Constants.FOLDER_MAPS + "\\" +
                Constants.FILE_SETTINGS;
            if (File.Exists(strSettingsFile))
            {
                using (var file = new StreamReader(strSettingsFile))
                {
                    var reader = new System.Xml.Serialization.XmlSerializer(typeof(BA_Objects.Analysis));
                    oAnalysis = (BA_Objects.Analysis)reader.Deserialize(file);
                }
            }
            string strFilePath = aoiFilePath + "\\" + Constants.FOLDER_MAPS + "\\" + Constants.FILE_BAGIS_MAP_PARAMETERS;
            using (StreamReader sr = new StreamReader(strFilePath))
            {
                string line;
                // Read and display lines from the file until the end of
                // the file is reached.
                while ((line = sr.ReadLine()) != null)
                {
                    string strNextLine = sr.ReadLine().Trim(); // map display unit text
                    bool bValue = Convert.ToBoolean(strNextLine);
                    if (bValue == true)
                    {
                        oAnalysis.DemDisplayUnits = "Meters";
                    }
                    else
                    {
                        oAnalysis.DemDisplayUnits = "Feet";
                    }
                    strNextLine = sr.ReadLine().Trim(); // selected elevation interval
                    int idxSelected = Convert.ToInt16(strNextLine);
                    oAnalysis.ElevationZonesInterval = Constants.VALUES_ELEV_INTERVALS[idxSelected];
                    int elevZonesCount = Convert.ToInt16(sr.ReadLine().Trim());
                    oAnalysis.ElevZonesCount = elevZonesCount;
                    if (elevZonesCount > 0)
                    {

                        List<string> lstElevIntervals = new List<string>();
                        List<double> lstElevZonesPctArea = new List<double>();
                        List<int> lstElevZonesSnotelCount = new List<int>();
                        List<int> lstElevZonesSnowCourseCount = new List<int>();
                        for (int i = 0; i < elevZonesCount; i++)    // elevation interval list
                        {
                            strNextLine = sr.ReadLine().Trim();
                            string[] pieces = strNextLine.Split(',');
                            if (pieces.Length == 4)
                            {
                                lstElevIntervals.Add(pieces[0].Trim());
                                var pctArea = double.Parse(pieces[1].Trim(new char[] { '%', ' ' }));    // strip out formatting
                                lstElevZonesPctArea.Add(pctArea);
                                lstElevZonesSnotelCount.Add(Convert.ToInt16(pieces[2].Trim()));
                                lstElevZonesSnowCourseCount.Add(Convert.ToInt16(pieces[3].Trim()));
                            }
                        }
                        oAnalysis.ElevZonesIntervals = lstElevIntervals;
                        oAnalysis.ElevZonesPctArea = lstElevZonesPctArea;
                        oAnalysis.ElevZonesSnotelCount = lstElevZonesSnotelCount;
                        oAnalysis.ElevZonesSnowCourseCount = lstElevZonesSnowCourseCount;
                    }

                    // PRISM configuration
                    idxSelected = Convert.ToInt16(sr.ReadLine().Trim());
                    int intPrecipZonesBegin = Convert.ToInt16(sr.ReadLine().Trim());
                    int intPrecipZonesEnd = Convert.ToInt16(sr.ReadLine().Trim());
                    switch (idxSelected)
                    {
                        case 0:
                            oAnalysis.PrecipZonesBegin = PrismFile.Annual.ToString();
                            oAnalysis.PrecipZonesEnd = PrismFile.Annual.ToString();
                            break;
                        case 1:
                            oAnalysis.PrecipZonesBegin = PrismFile.Q1.ToString();
                            oAnalysis.PrecipZonesEnd = PrismFile.Q1.ToString();
                            break;
                        case 2:
                            oAnalysis.PrecipZonesBegin = PrismFile.Q2.ToString();
                            oAnalysis.PrecipZonesEnd = PrismFile.Q2.ToString();
                            break;
                        case 3:
                            oAnalysis.PrecipZonesBegin = PrismFile.Q3.ToString();
                            oAnalysis.PrecipZonesEnd = PrismFile.Q3.ToString();
                            break;
                        case 4:
                            oAnalysis.PrecipZonesBegin = PrismFile.Q4.ToString();
                            oAnalysis.PrecipZonesEnd = PrismFile.Q4.ToString();
                            break;
                        default:
                            PrismFile begin = (PrismFile)intPrecipZonesBegin;
                            PrismFile end = (PrismFile)intPrecipZonesEnd;
                            oAnalysis.PrecipZonesBegin = begin.ToString();
                            oAnalysis.PrecipZonesEnd = end.ToString();
                            break;
                    }
                    strNextLine = sr.ReadLine().Trim();
                    oAnalysis.PrecipZonesMin = Convert.ToDouble(strNextLine);
                    strNextLine = sr.ReadLine().Trim();
                    oAnalysis.PrecipZonesMax = Convert.ToDouble(strNextLine);
                    strNextLine = sr.ReadLine().Trim(); // Don't save this. It is the range which is the difference of min and max
                    strNextLine = sr.ReadLine().Trim();
                    oAnalysis.PrecipZonesInterval = Convert.ToDouble(strNextLine);
                    strNextLine = sr.ReadLine().Trim();
                    oAnalysis.PrecipZonesIntervalCount = Convert.ToInt16(strNextLine);
                    int listCount = Convert.ToInt16(sr.ReadLine().Trim());
                    List<string> lstPrismIntervals = new List<string>();
                    if (listCount > 0)
                    {
                        for (int i = 0; i < listCount; i++)    // elevation interval list
                        {
                            strNextLine = sr.ReadLine().Trim();
                            lstPrismIntervals.Add(strNextLine);
                        }
                    }
                    oAnalysis.PrecipZonesIntervals = lstPrismIntervals;
                    oAnalysis.ElevSubdivisionCount = Convert.ToInt16(sr.ReadLine().Trim()); // this is the actual number of subdivisions rather than the cboBox index
                    strNextLine = sr.ReadLine().Trim(); // subrange analysis enabled
                    oAnalysis.SubrangeEnabled = Convert.ToBoolean(strNextLine);
                    strNextLine = sr.ReadLine().Trim();
                    oAnalysis.SubrangeElevMin = Convert.ToDouble(strNextLine);
                    strNextLine = sr.ReadLine().Trim();
                    oAnalysis.SubrangeElevMax = Convert.ToDouble(strNextLine);
                    if (sr.Peek() > -1) // check if additional parameters were added after BAGIS Ver 1. Aspect was added in version 2
                    {
                        strNextLine = sr.ReadLine().Trim(); // skip the REVISION text
                        strNextLine = sr.ReadLine().Trim();
                        string[] pieces = strNextLine.Split(' ');
                        if (pieces.Length == 2)
                        {
                            oAnalysis.AspectDirectionsCount = Convert.ToInt16(pieces[1]);
                        }
                    }
                    if (sr.Peek() > -1) // This line indicated if we were partway through an analysis and need to set generate to true
                                        // Skipping translation for now. Example: 'ENABLE_GENERATE False'
                    {
                        strNextLine = sr.ReadLine().Trim();
                    }
                }
                oAnalysis.DateBagisSettingsConverted = DateTime.Now;
            }

            //@ToDo: Change this to overwrite the analysis.xml file when we are ready
            string strTempFile = aoiFilePath + "\\" + Constants.FOLDER_MAPS + "\\analysis_2.xml";
            using (var file_stream = File.Create(strTempFile))
            {
                var serializer = new System.Xml.Serialization.XmlSerializer(typeof(BA_Objects.Analysis));
                serializer.Serialize(file_stream, oAnalysis);
                Module1.Current.ModuleLogManager.LogDebug(nameof(ReadBagisMapParameters),
                    "Saved map parameters to analysis.xml file");
            }

            return success;
        }

        public static async Task<IList<BA_Objects.Aoi>> GetAoiFoldersAsync(string parentFolder, string strLogFile)
        {
            IList<BA_Objects.Aoi> lstAoiPaths = new List<BA_Objects.Aoi>();

            try
            {
                // Check the parent directory
                FolderType fType = await GeodatabaseTools.GetAoiFolderTypeAsync(parentFolder);
                if (fType == FolderType.AOI)
                {
                    BA_Objects.Aoi aoi = new BA_Objects.Aoi(Path.GetFileName(parentFolder), parentFolder);
                    lstAoiPaths.Add(aoi);
                }

                // Check all the child directories
                string[] folders = Directory.GetDirectories(parentFolder, "*", System.IO.SearchOption.AllDirectories);
                foreach (var item in folders)
                {
                    fType = await GeodatabaseTools.GetAoiFolderTypeAsync(item);
                    if (fType == FolderType.AOI)
                    {
                        BA_Objects.Aoi aoi = new BA_Objects.Aoi(Path.GetFileName(item), item);
                        lstAoiPaths.Add(aoi);
                    }
                }
            }
            catch (Exception e)
            {
                string strLogEntry = "An error occurred while interrogating the subdirectories " + e.StackTrace + "\r\n";
                File.WriteAllText(strLogFile, strLogEntry);     // overwrite any existing files
            }
            return lstAoiPaths;
        }

        // Takes a list of paths we want to check and returns the datasets that exist
        public static async Task<IList<string>> RasterDatasetsExistAsync(ICollection<string> lstDatasetPaths)
        {
            IList<string> layerListExists = new List<string>();
            await QueuedTask.Run(() =>
            {
                foreach (var nextPath in lstDatasetPaths)
                {
                    string strFolder = Path.GetDirectoryName(nextPath);
                    string strFile = Path.GetFileName(nextPath);
                    if (!Directory.Exists(strFolder))
                    {
                        // Do nothing, the folder doesn't exist
                    }
                    else
                    {
                        // Create a FileSystemConnectionPath using the folder path.
                        FileSystemConnectionPath connectionPath =
                          new FileSystemConnectionPath(new System.Uri(strFolder), FileSystemDatastoreType.Raster);
                        // Create a new FileSystemDatastore using the FileSystemConnectionPath.
                        FileSystemDatastore dataStore = new FileSystemDatastore(connectionPath);
                        try
                        {
                            // Open the raster dataset.
                            RasterDataset fileRasterDataset = dataStore.OpenDataset<RasterDataset>(strFile);
                            if (fileRasterDataset != null)
                            {
                                layerListExists.Add(nextPath);
                            }
                        }
                        catch (Exception)
                        {
                            // munch; We tried to open the dataset and it didn't exist
                        }
                    }
                }
            });
            return layerListExists;
        }

        // Takes a list of paths we want to check and returns the datasets that exist
        public static async Task<IList<string>> ShapefilesExistAsync(ICollection<string> lstDatasetPaths)
        {
            IList<string> layerListExists = new List<string>();
            await QueuedTask.Run(() =>
            {
                foreach (var nextPath in lstDatasetPaths)
                {
                    string strFolder = Path.GetDirectoryName(nextPath);
                    string strFile = Path.GetFileName(nextPath);
                    if (!Directory.Exists(strFolder))
                    {
                        // Do nothing, the folder doesn't exist
                    }
                    else
                    {
                        // Create a FileSystemConnectionPath using the folder path.
                        FileSystemConnectionPath connectionPath =
                          new FileSystemConnectionPath(new System.Uri(strFolder), FileSystemDatastoreType.Shapefile);
                        // Create a new FileSystemDatastore using the FileSystemConnectionPath.
                        FileSystemDatastore dataStore = new FileSystemDatastore(connectionPath);
                        try
                        {
                            // Open the raster dataset.
                            FeatureClass fClass = dataStore.OpenDataset<FeatureClass>(strFile);
                            if (fClass != null)
                            {
                                layerListExists.Add(nextPath);
                            }
                        }
                        catch (Exception)
                        {
                            // munch; We tried to open the dataset and it didn't exist
                        }
                    }
                }
            });
            return layerListExists;
        }

        public static async Task<IList<string>> GetLayersInFolderAsync(string strFolderPath, string strSearchString)
        {
            IList<string> layerNames = new List<string>();
            // Add a folder to the Project
            var folderToAdd = ItemFactory.Instance.Create(strFolderPath);
            await QueuedTask.Run(() => Project.Current.AddItem(folderToAdd as IProjectItem));

            // find the folder project item
            FolderConnectionProjectItem folder = Project.Current.GetItems<FolderConnectionProjectItem>().
                FirstOrDefault(f => f.Path.Equals(strFolderPath, StringComparison.CurrentCultureIgnoreCase));
            if (folder == null) return layerNames;

            // do the search
            IEnumerable<Item> folderFiles = null;
            await QueuedTask.Run(() => folderFiles = folder.GetItems().Where(f => f.Type == strSearchString));
            foreach (Item item in folderFiles)
            {
                layerNames.Add(item.Name);
            }
            await QueuedTask.Run(() => Project.Current.RemoveItem(folderToAdd as IProjectItem));
            return layerNames;
        }

        public static async Task<IList<string>> GetLayersInGeodatabaseAsync(string strGdbPath, string strType)
        {
            IList<string> layerNames = new List<string>();

            await QueuedTask.Run(() => {
                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strGdbPath))))
                {
                    IReadOnlyList<Definition> lstDefinitions = null;
                    switch (strType)
                    {
                        case "TableDefinition":
                            lstDefinitions = geodatabase.GetDefinitions<TableDefinition>();
                            break;
                        case "FeatureClassDefinition":
                            lstDefinitions = geodatabase.GetDefinitions<FeatureClassDefinition>();
                            break;
                        case "RasterDatasetDefinition":
                            lstDefinitions = geodatabase.GetDefinitions<RasterDatasetDefinition>();
                            break;
                        default:
                            Module1.Current.ModuleLogManager.LogError(nameof(GetLayersInGeodatabaseAsync),
                                strType + " is not a supported dataset type!");
                            break;
                    }
                    foreach (var item in lstDefinitions)
                    {
                        layerNames.Add(item.GetName());
                    }
                }
            });
            return layerNames;
        }

        private static double QueryAnnualRunoffValue (string stationTriplet)
        {
            double returnValue = -1.0F;
            string strCsvPath = Module1.Current.SettingsPath + "\\" + Constants.FOLDER_SETTINGS +
                    "\\" + Constants.FILE_ANNUAL_RUNOFF_CSV;
            if (!File.Exists(strCsvPath))
            {
                Module1.Current.ModuleLogManager.LogError(nameof(QueryAnnualRunoffValue),
                    "The file containing annual runoff could not be found. Runoff will not be calculated!");
                return returnValue;
            }

            try
            {
                using (TextFieldParser parser = new TextFieldParser(strCsvPath))
                {
                    parser.TextFieldType = Microsoft.VisualBasic.FileIO.FieldType.Delimited;
                parser.SetDelimiters(",");
                int idxId = -1;
                int idxValue = -1;
                bool headerRow = true;
                while (!parser.EndOfData)
                {
                    //Process header row
                    string[] fields = parser.ReadFields();
                    int i = 0;
                    foreach (string field in fields)
                    {
                        if (headerRow == true)
                        {
                            if (field.ToUpper().Trim().Equals(Constants.FIELD_RUNOFF_STATION_TRIPLET.ToUpper().Trim()))
                            {
                                idxId = i;
                            }
                            else if (field.ToUpper().Trim().Equals(Constants.FIELD_RUNOFF_AVERAGE.ToUpper().Trim()))
                            {
                                idxValue = i;
                            }
                            if (idxId > 0 && idxValue > 0)
                            {
                                break;
                            }
                            i++;
                        }
                        else
                        {
                            string strId =fields[idxId];
                            if (strId.Trim().Equals(stationTriplet.Trim()))
                            {
                                if (!string.IsNullOrEmpty(fields[idxValue]))
                                {
                                    bool isDouble;
                                    isDouble = Double.TryParse(fields[idxValue], out returnValue);  
                                    break;
                                }
                                else
                                {
                                    // This is how we handle nulls
                                    // For now we return the same thing if the record is missing or null
                                    // returnValue = 0;
                                    break;
                                }
                            }
                        }
                        }
                        headerRow = false;
                    }
                }

                Module1.Current.ModuleLogManager.LogDebug(nameof(QueryAnnualRunoffValue),
                    "Found Runoff value of " + returnValue);
            }
            catch (Exception e)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(QueryAnnualRunoffValue),
                    "Exception: " + e.Message);
                Module1.Current.ModuleLogManager.LogError(nameof(QueryAnnualRunoffValue),
                    "Stacktrace: " + e.StackTrace);
                returnValue = -1.0F;
            }
            return returnValue;
        }

        public static string GetFullPdfFileName(string strBaseFileName)
        {
            if (Constants.FILE_TITLE_PAGE_PDF.Equals(strBaseFileName) || Constants.FILE_SITES_TABLE_PDF.Equals(strBaseFileName))
            {
                // The title page doesn't have the station name prefix
                return Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_MAP_PACKAGE + "\\" + strBaseFileName;
            }
            else
            {
                return Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_MAP_PACKAGE + "\\"
                    + Module1.Current.Aoi.FileStationName + "_" + strBaseFileName;
            } 
        }

        public static BA_Objects.Analysis GetAnalysisSettings(string strAoiPath)
        {
            BA_Objects.Analysis oAnalysis = new BA_Objects.Analysis();
            string strSettingsFile = strAoiPath + "\\" + Constants.FOLDER_MAPS + "\\" +
                Constants.FILE_SETTINGS;
            try
            {
                if (File.Exists(strSettingsFile))
                {
                    using (var file = new StreamReader(strSettingsFile))
                    {
                        var reader = new System.Xml.Serialization.XmlSerializer(typeof(BA_Objects.Analysis));
                        oAnalysis = (BA_Objects.Analysis)reader.Deserialize(file);
                    }
                }
            }
            catch (Exception e)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(GetAnalysisSettings),
                    "An error occurred while trying to retrieve the Analysis settings!");
                Module1.Current.ModuleLogManager.LogError(nameof(GetAnalysisSettings),
                    "Exception: " + e.StackTrace);
            }
            return oAnalysis;
        }

        public static BA_ReturnCode SaveAnalysisSettings(string strAoiPath, BA_Objects.Analysis oAnalysis)
        {
            string strSettingsFile = strAoiPath + "\\" + Constants.FOLDER_MAPS + "\\" +
                Constants.FILE_SETTINGS;
            using (var file_stream = File.Create(strSettingsFile))
            {
                var serializer = new System.Xml.Serialization.XmlSerializer(typeof(BA_Objects.Analysis));
                serializer.Serialize(file_stream, oAnalysis);
                return BA_ReturnCode.Success;
            }
        }

        public static async Task<string[]> QueryMasterAoiProperties(string stationTriplet)
        {
            string[] arrResults = { };
            try
            {
                string strMasterUrl = (string) Module1.Current.BatchToolSettings.MasterAoiList;
                Uri uriMaster = new Uri(strMasterUrl);
                Webservices ws = new Webservices();
                QueryFilter queryFilter = new QueryFilter
                {
                    WhereClause = Constants.FIELD_STATION_TRIPLET + " = '" + stationTriplet + "'"
                };
                string[] arrSearch = { Constants.FIELD_NWCCNAME, Constants.FIELD_WINTER_START_MONTH, Constants.FIELD_WINTER_END_MONTH,
                    Constants.FIELD_HUC};
                arrResults = await ws.QueryServiceForValuesAsync(uriMaster, "0", arrSearch, queryFilter);
                if (arrResults.Length != arrSearch.Length)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(QueryMasterAoiProperties),
                        "An error occurred while retrieving properties from the master aoi webservice");
                }
                else if (arrResults != null && arrResults.Length > 1 && arrResults[0] == null)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(QueryMasterAoiProperties),
                        "Unable to retrieve at least 1 property from the master aoi webservice");
                }
            }
            catch (Exception e)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(QueryMasterAoiProperties),
                    "Exception: " + e.StackTrace);
            }
            return arrResults;
        }

        public static async Task<BA_ReturnCode> GenerateSitesTableAsync(BA_Objects.Aoi oAoi)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            try
            {
                bool bHasSnotel = false;
                bool bHasSnowCourse = false;
                Uri sitesGdbUri = new Uri(GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Layers, false));
                int intSites = await GeodatabaseTools.CountFeaturesAsync(sitesGdbUri, Constants.FILE_SNOTEL);
                if (intSites > 0)
                    bHasSnotel = true;
                intSites = await GeodatabaseTools.CountFeaturesAsync(sitesGdbUri, Constants.FILE_SNOW_COURSE);
                if (intSites > 0)
                    bHasSnowCourse = true;
                if (bHasSnotel == false && bHasSnowCourse == false)
                {
                    Module1.Current.ModuleLogManager.LogInfo(nameof(GenerateSitesTableAsync),
                        "No sites found. Sites table will not be created!");
                    return success;
                }

                // Initialize the title page object
                BA_Objects.ExportTitlePage tPage = new BA_Objects.ExportTitlePage
                {
                    aoi_name = oAoi.NwccName,
                    local_path = oAoi.FilePath,
                    date_created = DateTime.Now
                };
                //DEM is always in meters
                string strDemDisplayUnits = (string)Module1.Current.BatchToolSettings.DemDisplayUnits;
                IList<BA_Objects.Site> lstAllSites = new List<BA_Objects.Site>();
                if (bHasSnotel || bHasSnowCourse)
                {
                    lstAllSites = await AnalysisTools.AssembleMergedSitesListAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, false)));
                }

                foreach (var site in lstAllSites)
                {
                    // set the elevation text with units conversion
                    if (strDemDisplayUnits.Equals("Feet"))
                    {
                        site.ElevationText = 
                            String.Format("{0:0}", ArcGIS.Core.Geometry.LinearUnit.Meters.ConvertTo(site.ElevMeters, ArcGIS.Core.Geometry.LinearUnit.Feet));
                    }
                    else
                    {
                        site.ElevationText = String.Format("{0:0}", site.ElevMeters);
                    }
                }
                BA_Objects.Site[] arrSites = new BA_Objects.Site[lstAllSites.Count];
                lstAllSites.CopyTo(arrSites, 0);
                tPage.all_sites = arrSites;
                //Set the site elevation units
                BA_Objects.Analysis oAnalysis = GetAnalysisSettings(oAoi.FilePath);
                string siteElevationUnits = "?";
                if (oAnalysis != null && !String.IsNullOrEmpty(oAnalysis.ElevUnitsText))
                {
                    siteElevationUnits = oAnalysis.ElevUnitsText;
                }
                tPage.site_elev_range_units = siteElevationUnits;

                string publishFolder = Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_MAP_PACKAGE;
                string myXmlFile = publishFolder + "\\" + Constants.FILE_SITES_TABLE_XML;
                System.Xml.Serialization.XmlSerializer writer = new System.Xml.Serialization.XmlSerializer(tPage.GetType());
                using (FileStream fs = System.IO.File.Create(myXmlFile))
                {
                    writer.Serialize(fs, tPage);
                }

                // Process the sites table page through the xsl template
                string myStyleSheet = GeneralTools.GetAddInDirectory() + "\\" + Constants.FILE_SITES_TABLE_XSL;
                XPathDocument myXPathDoc = new XPathDocument(myXmlFile);
                XslCompiledTransform myXslTrans = new XslCompiledTransform();
                myXslTrans.Load(myStyleSheet);
                string htmlFilePath = publishFolder + "\\" + Constants.FILE_SITES_TABLE_HTML;
                using (XmlTextWriter myWriter = new XmlTextWriter(htmlFilePath, null))
                {
                    myXslTrans.Transform(myXPathDoc, null, myWriter);
                }

                // Convert the sites table to PDF
                if (File.Exists(htmlFilePath))
                {
                    PdfSharp.Pdf.PdfDocument sitesPageDoc = TheArtOfDev.HtmlRenderer.PdfSharp.PdfGenerator.GeneratePdf(System.IO.File.ReadAllText(htmlFilePath),
                        PdfSharp.PageSize.Letter);
                    sitesPageDoc.Save(publishFolder + "\\" + Constants.FILE_SITES_TABLE_PDF);
                }
                Module1.Current.ModuleLogManager.LogDebug(nameof(GenerateSitesTableAsync),
                    "Sites table created!!");
                success = BA_ReturnCode.Success;
            }
            catch (Exception e)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(GenerateSitesTableAsync),
                    "Exception: " + e.Message);
                MessageBox.Show("An error occurred while trying to parse the XML!! " + e.Message, "BAGIS PRO");
                return success;
            }
            return success;
        }
    }

}