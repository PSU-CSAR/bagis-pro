using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
                    Debug.WriteLine("PDF file created!!");
                    return BA_ReturnCode.Success;
                }
                else
                {
                    MessageBox.Show("Could not find default layout. Map cannot be exported!!", "BAGIS PRO");
                    return BA_ReturnCode.UnknownError;
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("An error occurred while trying to export the map!! " + e.Message, "BAGIS PRO");
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
                IDictionary<string, dynamic> dictLocalDataSources = GeneralTools.QueryLocalDataSources();
                string[] keys = { Constants.DATA_TYPE_SWE, Constants.DATA_TYPE_PRECIPITATION};
                IList < BA_Objects.DataSource > lstDataSources = new List<BA_Objects.DataSource>();
                foreach (string strKey in keys)
                {
                    if (dictLocalDataSources.ContainsKey(strKey))
                    {
                        dynamic dynSource = dictLocalDataSources[strKey];
                        BA_Objects.DataSource newSource = new BA_Objects.DataSource(dynSource);
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
                Debug.WriteLine("Title page created!!");
            }
            catch (Exception e)
            {
                MessageBox.Show("An error occurred while trying to parse the XML!! " + e.Message, "BAGIS PRO");
            }
        }

        public static IDictionary<string, dynamic> QueryLocalDataSources()
        {
            IDictionary<string, dynamic> dictDataSources = new Dictionary<string, dynamic>();
            string strDataSourcesFile = Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_MAPS + "\\" +
                Constants.FILE_MAP_PARAMETERS;
            if (File.Exists(strDataSourcesFile))
            {
                using (StreamReader file = File.OpenText(strDataSourcesFile))
                using (JsonTextReader reader = new JsonTextReader(file))
                {
                    JObject jsonVal = (JObject)JToken.ReadFrom(reader);
                    JArray arrDataSources = (JArray)jsonVal["dataSources"];
                    foreach (dynamic dSource in arrDataSources)
                    {
                        string key = dSource.layerType;
                        if (!dictDataSources.ContainsKey(key))
                        {
                            dictDataSources.Add(key, dSource);
                        }
                    }
                }
            }
            return dictDataSources;
        }

        public static BA_ReturnCode SaveDataSourcesToFile(IDictionary<string, dynamic> dictDataSources)
        {
            JObject jsonVal = new JObject();
            string strDataSourcesFile = Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_MAPS + "\\" +
                Constants.FILE_MAP_PARAMETERS;
            if (File.Exists(strDataSourcesFile))
            {
                using (StreamReader file = File.OpenText(strDataSourcesFile))
                using (JsonTextReader reader = new JsonTextReader(file))
                {
                    jsonVal = (JObject)JToken.ReadFrom(reader);
                }
            }

            JArray arrDataSources = new JArray();
            foreach (string key in dictDataSources.Keys)
            {
                arrDataSources.Add(dictDataSources[key]);
            }
            jsonVal["dataSources"] = arrDataSources;
            File.WriteAllText(strDataSourcesFile, jsonVal.ToString());
            return BA_ReturnCode.Success;
        }

        public static async Task<BA_ReturnCode> GenerateTablesAsync()
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;

            // Declare Excel object variables
            Application objExcel = new Application();
            Workbook bkWorkBook = objExcel.Workbooks.Add();  //a file in excel

            // Create SNOTEL Distribution Worksheet
            Worksheet pSNOTELWorksheet = bkWorkBook.Sheets.Add();
            pSNOTELWorksheet.Name = "SNOTEL";

            // Create Elevation Distribution Worksheet
            Worksheet pAreaElvWorksheet = bkWorkBook.Sheets.Add();
            pAreaElvWorksheet.Name = "Area Elevations";

            //Query min/max from dem
            string sMask = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_RASTER;
            IList<double> lstResult = await GeoprocessingTools.GetDemStatsAsync(Module1.Current.Aoi.FilePath, sMask, 0.005);
            double elevMinMeters = -1;
            if (lstResult.Count == 2)   // We expect the min and max values in that order
            {
                elevMinMeters = lstResult[0];
            }

            success = await ExcelTools.CreateElevationTableAsync(pAreaElvWorksheet, elevMinMeters);

            bool bHasSnotel = await GeodatabaseTools.RasterDatasetExistsAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false)), Constants.FILE_SNOTEL_ZONE);
            if (bHasSnotel)
            {
                success = await ExcelTools.CreateSnotelTableAsync(pSNOTELWorksheet, pAreaElvWorksheet);
            }
            objExcel.Visible = true;

            success = BA_ReturnCode.Success;
            return success;
        }

        public static async Task<string> GetBagisTag(string layerPath, string propertyPath)
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

        public static async Task<BA_ReturnCode> UpdateMetadata(string layerPath, string propertyPath,
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
            success = await GeneralTools.UpdateMetadata(layerPath, Constants.META_TAG_XPATH, sb.ToString(),
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




    }



    }