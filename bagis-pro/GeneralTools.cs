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
                    OutputFileName = Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_MAP_PACKAGE + "\\"
                    + Module1.Current.DisplayedMap,
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
                    LayoutProjectItem lytItem =
                    ArcGIS.Desktop.Core.Project.Current.GetItems<LayoutProjectItem>()
                        .FirstOrDefault(m => m.Name.Equals(Constants.MAPS_DEFAULT_LAYOUT_NAME, StringComparison.CurrentCultureIgnoreCase));
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

        public static async Task GenerateMapsTitlePage()
        {
            try
            {
                // Query for the station triplet and name
                string[] arrValues = await AnalysisTools.GetStationValues();
                string strStationId = arrValues[0];
                string strStationName = arrValues[1];
                if (String.IsNullOrEmpty(strStationId))
                {
                    strStationId = "XXXXXXXX:XX:USGS";
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

                //Test querying metadata; Layer metadata methods not currently available in Pro
                //string snowCourseSitesPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers, true) +  Constants.FILE_SNOW_COURSE;
                //Item featureClassItem = ItemFactory.Instance.Create(snowCourseSitesPath);
                //IMetadata fcMetadataItem = featureClassItem as IMetadata;

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

                // Calculating percent represented area
                gdbUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, false));
                double pctSnotelRepresented = 0;
                double pctSnowCourseRepresented = 0;
                double pctAllSitesRepresented = 0;
                double aoiArea = await GeodatabaseTools.CalculateTotalPolygonAreaAsync(gdbUri, Constants.FILE_AOI_VECTOR);
                bool hasSnotelSites = false;
                bool hasScosSites = false;
                if (aoiArea > 0)
                {
                    gdbUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false));
                    if (totalSnotelSites > 0)
                    {
                        double repArea = await GeodatabaseTools.CalculateTotalPolygonAreaAsync(gdbUri, Constants.FILE_SNOTEL_REPRESENTED);
                        pctSnotelRepresented = Math.Round(repArea / aoiArea * 100);
                        hasSnotelSites = true;
                    }
                    if (totalScosSites > 0)
                    {
                        double repArea = await GeodatabaseTools.CalculateTotalPolygonAreaAsync(gdbUri, Constants.FILE_SCOS_REPRESENTED);
                        pctSnowCourseRepresented = Math.Round(repArea / aoiArea * 100);
                        hasScosSites = true;
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
                                  Constants.DATA_TYPE_SNOW_COURSE, Constants.DATA_TYPE_ROADS, Constants.DATA_TYPE_PUBLIC_LAND };
                IList < BA_Objects.DataSource > lstDataSources = new List<BA_Objects.DataSource>();
                foreach (string strKey in keys)
                {
                    if (dictLocalDataSources.ContainsKey(strKey))
                    {
                        BA_Objects.DataSource newSource = dictLocalDataSources[strKey];
                        lstDataSources.Add(newSource);
                    }
                }

                // Serialize the title page object
                BA_Objects.ExportTitlePage tPage = new BA_Objects.ExportTitlePage
                {
                    aoi_name = Module1.Current.Aoi.Name,
                    comments = "This is a test",
                    publisher = "Lesley Bross",
                    local_path = Module1.Current.Aoi.FilePath,
                    streamgage_station = strStationId,
                    streamgage_station_name = strStationName,
                    drainage_area_sqkm = areaSqKm,
                    elevation_min_meters = elevMinMeters,
                    elevation_max_meters = elevMaxMeters,
                    has_snotel_sites = hasSnotelSites,
                    snotel_sites_in_basin = snotelInBasin,
                    snotel_sites_in_buffer = snotelInBuffer,
                    has_scos_sites = hasScosSites,
                    scos_sites_in_basin = scosInBasin,
                    scos_sites_in_buffer = scosInBuffer,
                    site_elev_range_ft = Module1.Current.Aoi.SiteElevRangeFeet,
                    site_buffer_dist_mi = Module1.Current.Aoi.SiteBufferDistMiles,
                    represented_snotel_percent = pctSnotelRepresented,
                    represented_snow_course_percent = pctSnowCourseRepresented,
                    represented_all_sites_percent = pctAllSitesRepresented,
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
                Module1.Current.ModuleLogManager.LogDebug(nameof(GenerateMapsTitlePage),
                    "Title page created!!");
            }
            catch (Exception e)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(GenerateMapsTitlePage),
                    "Exception: " + e.Message);
                MessageBox.Show("An error occurred while trying to parse the XML!! " + e.Message, "BAGIS PRO");
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
                bool bPrecMeanElevTableExists = await GeodatabaseTools.TableExistsAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false)), Constants.FILE_ASP_ZONE_PREC_TBL);
                bool bPrecStelLayerExists = await GeodatabaseTools.FeatureClassExistsAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false)), Constants.FILE_PREC_STEL);
                if (bPrecMeanElevTableExists)
                {
                    // Create Elevation Precipitation Worksheet
                    pPrecipDemElevWorksheet = bkWorkBook.Sheets.Add();
                    pPrecipDemElevWorksheet.Name = "Elev-Precip AOI";

                    // Create Elev-Precip Chart Worksheet
                    pPrecipChartWorksheet = bkWorkBook.Sheets.Add();
                    pPrecipChartWorksheet.Name = "Elev-Precip Chart";
                }
                if (bPrecStelLayerExists)
                {
                    // Create Site Precipitation Worksheet
                    pPrecipSiteWorksheet = bkWorkBook.Sheets.Add();
                    pPrecipSiteWorksheet.Name = "Elev-Precip Sites";
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(GenerateTablesAsync), Constants.FILE_PREC_STEL +
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
                    success = await ExcelTools.CreateSitesTableAsync(pSNOTELWorksheet, pAreaElvWorksheet, Constants.FILE_SNOTEL_ZONE);
                    Module1.Current.ModuleLogManager.LogInfo(nameof(GenerateTablesAsync), "Created Snotel sites Table");
                }

                Module1.Current.Aoi.HasSnowCourse = true;
                intSites = await GeodatabaseTools.CountFeaturesAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers, false)), Constants.FILE_SNOW_COURSE);
                if (intSites < 1)
                {
                    Module1.Current.Aoi.HasSnowCourse = false;
                }
                if (Module1.Current.Aoi.HasSnowCourse)
                {
                    success = await ExcelTools.CreateSitesTableAsync(pSnowCourseWorksheet, pAreaElvWorksheet, Constants.FILE_SCOS_ZONE);
                    Module1.Current.ModuleLogManager.LogInfo(nameof(GenerateTablesAsync), "Created Snow Course sites Table");
                }

                string strPrecipPath = Module1.Current.Aoi.FilePath + Module1.Current.Settings.m_precipFile;
                 double MaxPRISMValue = await ExcelTools.CreatePrecipitationTableAsync(pPRISMWorkSheet,
                    strPrecipPath, elevMinMeters);

            // copy DEM area and %_area to the PRISM table
            success = ExcelTools.CopyCells(pAreaElvWorksheet, 3, pPRISMWorkSheet, 12);
            success = ExcelTools.CopyCells(pAreaElvWorksheet, 10, pPRISMWorkSheet, 13);
            success = ExcelTools.EstimatePrecipitationVolume(pPRISMWorkSheet, 12, 7, 14, 15);
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
                double Y_Unit = Module1.Current.Settings.m_elevInterval;
                if (oAnalysis.ElevationZonesInterval > 0)
                {
                    Y_Unit = oAnalysis.ElevationZonesInterval;
                }
            double Y_Max = -99.0F;
            double minValue = elevMinMeters;
            double maxValue = elevMaxMeters;
                int leftPosition = Constants.EXCEL_CHART_WIDTH + (Constants.EXCEL_CHART_SPACING * 10);
            //aoiDemMin is always in meters
            if (Module1.Current.Settings.m_demDisplayUnits.Equals("Feet"))
            {
                minValue = ArcGIS.Core.Geometry.LinearUnit.Meters.ConvertTo(elevMinMeters, ArcGIS.Core.Geometry.LinearUnit.Feet);
                maxValue = ArcGIS.Core.Geometry.LinearUnit.Meters.ConvertTo(elevMaxMeters, ArcGIS.Core.Geometry.LinearUnit.Feet);
            }

                double Y_Min = ExcelTools.ConfigureYAxis(minValue, maxValue, Y_Unit, ref Y_Max);
                success = ExcelTools.CreateCombinedChart(pPRISMWorkSheet, pAreaElvWorksheet, pChartsWorksheet, pSNOTELWorksheet,
                    pSnowCourseWorksheet, Constants.EXCEL_CHART_SPACING, Y_Max, Y_Min, Y_Unit, MaxPRISMValue);
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

                //Elevation-Precipitation Correlation Chart
                int intMinPrecip = -1;
                if (bPrecMeanElevTableExists)
                {
                    intMinPrecip = await ExcelTools.CreateRepresentPrecipTableAsync(pPrecipDemElevWorksheet, strPrecipPath);
                    Module1.Current.ModuleLogManager.LogInfo(nameof(GenerateTablesAsync), "Created represented precip table");
                }
                if (intMinPrecip != 999 && bPrecStelLayerExists)
                {
                    success = await ExcelTools.CreateSnotelPrecipTableAsync(pPrecipSiteWorksheet, 
                            new List<BA_Objects.Site>());
                    Module1.Current.ModuleLogManager.LogInfo(nameof(GenerateTablesAsync), "Created Snotel Represented Precip Table");

                }
                if (intMinPrecip != 999)
                {
                    success = ExcelTools.CreateRepresentPrecipChart(pPrecipDemElevWorksheet, pPrecipSiteWorksheet, pPrecipChartWorksheet, intMinPrecip, Y_Min);
                    Module1.Current.ModuleLogManager.LogInfo(nameof(GenerateTablesAsync), "Created Represented Precip Chart");
                }

                //Publish Charts Tab
                if (bInteractive == false)
            {
                // Combined chart
                string sOutputFolder = Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_MAP_PACKAGE + "\\";
                string pathToSave = sOutputFolder + Constants.FILE_EXPORT_CHART_AREA_ELEV_PRECIP_SITE_PDF;

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
                pChartsWorksheet.PageSetup.PrintArea = "$A$1:$M$29";
                pChartsWorksheet.PageSetup.CenterHeader = "&C&\"Arial,Bold\"&16 " + Module1.Current.Aoi.Name;
                pChartsWorksheet.ExportAsFixedFormat(XlFixedFormatType.xlTypePDF, pathToSave);
                Module1.Current.ModuleLogManager.LogInfo(nameof(GenerateTablesAsync), "Published combined chart to PDF");

                // slope chart
                pathToSave = sOutputFolder + "\\" + Constants.FILE_EXPORT_CHART_SLOPE_PDF;
                pChartsWorksheet.PageSetup.PrintArea = "$N$1:$AA$29";
                pChartsWorksheet.ExportAsFixedFormat(XlFixedFormatType.xlTypePDF, pathToSave);
                Module1.Current.ModuleLogManager.LogInfo(nameof(GenerateTablesAsync), "Published slope chart to PDF");

                // aspect chart
                pathToSave = sOutputFolder + "\\" + Constants.FILE_EXPORT_CHART_ASPECT_PDF;
                pChartsWorksheet.PageSetup.PrintArea = "$A$32:$M$61";
                pChartsWorksheet.ExportAsFixedFormat(XlFixedFormatType.xlTypePDF, pathToSave);
                Module1.Current.ModuleLogManager.LogInfo(nameof(GenerateTablesAsync), "Published aspect chart to PDF");

                    // Elev-Precip Chart Tab
                    if (bPrecMeanElevTableExists)
                    {
                        oPaperSize = pPrecipChartWorksheet.PageSetup.PaperSize;
                        pPrecipChartWorksheet.PageSetup.Orientation = XlPageOrientation.xlLandscape;
                        pPrecipChartWorksheet.PageSetup.Zoom = false;
                        pPrecipChartWorksheet.PageSetup.PaperSize = oReqPaperSize;
                        pPrecipChartWorksheet.PageSetup.FitToPagesTall = 1;
                        pPrecipChartWorksheet.PageSetup.FitToPagesWide = 1;
                        pPrecipChartWorksheet.PageSetup.CenterHeader = "&C&\"Arial,Bold\"&16 " + Module1.Current.Aoi.Name;
                        pathToSave = sOutputFolder + "\\" + Constants.FILE_EXPORT_CHART_ELEV_PRECIP_CORR_PDF;
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
                    bkWorkBook.Close(false);
                    objExcel.Quit();
                    objExcel = null;
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
                    foreach(XmlNode pNode in propertyNodes)
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
                    if (strValue.Substring(strValue.Length - 1)== ";")
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
                    foreach(XmlNode pNode in propertyNodeList)
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

        public static void SetAoi(string strAoiPath)
        {
            // Initialize AOI object
            BA_Objects.Aoi oAoi = new BA_Objects.Aoi(Path.GetFileName(strAoiPath), strAoiPath);
            // Set reference to dockpane to update layers
            var layersPane = (DockpaneLayersViewModel) FrameworkApplication.DockPaneManager.Find("bagis_pro_DockpaneLayers");
            try
            {
                string strSurfacesGdb = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Surfaces, false);
                var fcPath = strSurfacesGdb + "\\" + Constants.FILE_DEM_FILLED;
                QueuedTask.Run(() =>
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

                    // Check for default units
                    var fc = ItemFactory.Instance.Create(fcPath, ItemFactory.ItemType.PathItem);
                    if (fc != null)
                    {
                        string strXml = string.Empty;
                        strXml = fc.GetXml();
                        //check metadata was returned
                        string strBagisTag = GetBagisTag(strXml);
                        if (! string.IsNullOrEmpty(strBagisTag))
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
                    // Set logger to AOI directory
                    string logFolderName = strAoiPath + "\\" + Constants.FOLDER_LOGS;
                    Module1.Current.ModuleLogManager.UpdateLogFileLocation(logFolderName);

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
                                    Module1.Current.ModuleLogManager.LogDebug(nameof(SetAoi),
                                        "Unable to locate PRISM buffer distance on annual layer. Using default");
                                }
                                if (!String.IsNullOrEmpty(tempBufferUnits))
                                {
                                    layersPane.PrismBufferUnits = tempBufferUnits;
                                }
                                else
                                {
                                    Module1.Current.ModuleLogManager.LogDebug(nameof(SetAoi),
                                        "Unable to locate PRISM units on annual layer. Using default");
                                }
                            }
                        }                       
                    }

                    // Update data status for files in layers.gdb
                    string[] arrCheckLayers = new string[] { Constants.FILE_SNODAS_SWE_APRIL, Constants.FILE_SNOTEL,
                                                             Constants.FILE_SNOW_COURSE, Constants.FILE_ROADS,
                                                             Constants.FILE_PUBLIC_LAND};
                    bool[] arrLayerExists = new bool[] { false, false, false, false, false};
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
                                        default:
                                            Module1.Current.ModuleLogManager.LogError(nameof(SetAoi),
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
                    Module1.Current.CboCurrentAoi.SetAoiName(oAoi.Name);
                    MapTools.DeactivateMapButtons();
                    Module1.ActivateState("Aoi_Selected_State");
                    Module1.ActivateState("BtnExcelTables_State");


                    MessageBox.Show("AOI is set to " + Module1.Current.Aoi.Name + "!", "BAGIS PRO");
                });
               
            }
            catch (Exception e)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(SetAoi),
                    "Exception: " + e.Message);
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
                scalefactor = Convert.ToInt32(Math.Pow(10,(intvstring.Length - position)));
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
                rangearr[j].Value = j;
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

            rangearr[ninterval - 1].Value = ninterval - 1;
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
    }



    }