using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bagis_pro
{
    public class AnalysisTools
    {

        public static async Task<BA_ReturnCode> GenerateSiteLayersAsync(double siteBufferDistanceMiles, double siteElevRangeFeet)
        {
            BA_Objects.Aoi currentAoi = Module1.Current.Aoi;
            BA_ReturnCode success = BA_ReturnCode.UnknownError;

            try
            {
                // Check to make sure we have snotel and/or snow course sites
                bool bHasSnotel = false;
                bool bHasSnowCourse = false;
                Uri sitesGdbUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers, false));
                int intSites = await GeodatabaseTools.CountFeaturesAsync(sitesGdbUri, Constants.FILE_SNOTEL);
                if (intSites > 0)
                    bHasSnotel = true;
                intSites = await GeodatabaseTools.CountFeaturesAsync(sitesGdbUri, Constants.FILE_SNOW_COURSE);
                if (intSites > 0)
                    bHasSnowCourse = true;
                if (!bHasSnotel && !bHasSnowCourse)
                {
                    MessageBox.Show("No SNOTEL or Snow Course layers found for AOI. Site Layers cannot be generated!!");
                    return success;
                }

                //1. Get min/max DEM elevation for reclassing raster. We only want to do this once
                Module1.Current.ModuleLogManager.LogDebug(nameof(GenerateSiteLayersAsync),
                    "START: GenerateSiteLayersAsync");
                Module1.Current.ModuleLogManager.LogDebug(nameof(GenerateSiteLayersAsync),
                    "GetDemStatsAsync");
                IList<double> lstResult = await GeoprocessingTools.GetDemStatsAsync(Module1.Current.Aoi.FilePath, "", 0.005);
                double demElevMinMeters = -1;
                double demElevMaxMeters = -1;
                if (lstResult.Count == 2)   // We expect the min and max values in that order
                {
                    demElevMinMeters = lstResult[0];
                    demElevMaxMeters = lstResult[1];
                    Module1.Current.Aoi.MinElevMeters = demElevMinMeters;
                    Module1.Current.Aoi.MaxElevMeters = demElevMaxMeters;
                }
                else
                {
                    MessageBox.Show("Unable to read DEM. No Site layers can be generated!!", "BAGIS-PRO");
                    return success;
                }

                // snotel sites
                IList<BA_Objects.Site> lstSites = null;
                if (bHasSnotel)
                {
                    lstSites = await AnalysisTools.AssembleSitesListAsync(Constants.FILE_SNOTEL, SiteType.Snotel.ToString(), siteBufferDistanceMiles);
                    success = await AnalysisTools.CalculateRepresentedArea(demElevMinMeters, demElevMaxMeters, lstSites, siteElevRangeFeet, Constants.FILE_SNOTEL_REPRESENTED);
                    if (success != BA_ReturnCode.Success)
                        bHasSnotel = false;
                }

                // snow course sites
                if (bHasSnowCourse)
                {
                    lstSites = await AnalysisTools.AssembleSitesListAsync(Constants.FILE_SNOW_COURSE, SiteType.SnowCourse.ToString(), siteBufferDistanceMiles);
                    success = await AnalysisTools.CalculateRepresentedArea(demElevMinMeters, demElevMaxMeters, lstSites, siteElevRangeFeet, Constants.FILE_SCOS_REPRESENTED);
                    if (success != BA_ReturnCode.Success)
                        bHasSnowCourse = false;
                }

                // record buffer distances and units in metadata
                string settingsPath = Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_MAPS + "\\" +
                    Constants.FILE_SETTINGS;
                BA_Objects.Analysis oAnalysis = new BA_Objects.Analysis();
                if (File.Exists(settingsPath))
                {
                    using (var file = new StreamReader(settingsPath))
                    {
                        var reader = new System.Xml.Serialization.XmlSerializer(typeof(BA_Objects.Analysis));
                        oAnalysis = (BA_Objects.Analysis)reader.Deserialize(file);
                    }
                }
                if (oAnalysis != null)
                {
                    oAnalysis.UpperRange = siteElevRangeFeet;
                    oAnalysis.UpperRangeText = Convert.ToString(siteElevRangeFeet);
                    oAnalysis.LowerRange = siteElevRangeFeet;
                    oAnalysis.LowerRangeText = Convert.ToString(siteElevRangeFeet);
                    oAnalysis.ElevUnitsText = "Feet";
                    oAnalysis.BufferDistance = siteBufferDistanceMiles;
                    oAnalysis.BufferUnitsText = "Miles";
                }
                // Save settings file
                using (var file_stream = File.Create(settingsPath))
                {
                    var serializer = new System.Xml.Serialization.XmlSerializer(typeof(BA_Objects.Analysis));
                    serializer.Serialize(file_stream, oAnalysis);
                }

                // combine site layers
                if (bHasSnotel && bHasSnowCourse)
                {
                    success = await AnalysisTools.CombineSitesRepresentedArea();
                }
                else
                {
                    string pathToDelete = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) + Constants.FILE_SITES_REPRESENTED;
                    if (await GeodatabaseTools.FeatureClassExistsAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false)),
                        Constants.FILE_SITES_REPRESENTED))
                    {
                        success = await GeoprocessingTools.DeleteDatasetAsync(pathToDelete);
                    }
                }
                return success;
            }
            catch (Exception e)
            {
                MessageBox.Show("GenerateSiteLayersAsync Exception: " + e.Message, "BAGIS PRO");
                return success;
            }

        }

        public static async Task<IList<BA_Objects.Site>> AssembleSitesListAsync(string sitesFileName, string sType,
            double siteBufferDistanceMiles)
        {
            //2. Buffer point from feature class and query site information
            Uri layersUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers, false));

            IList<BA_Objects.Site> lstSites = new List<BA_Objects.Site>();
            // Open geodatabase for snotel sites
            await QueuedTask.Run(() =>
            {
                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(layersUri)))
                using (FeatureClass fClass = geodatabase.OpenDataset<FeatureClass>(sitesFileName))
                {
                    QueryFilter queryFilter = new QueryFilter();
                    using (RowCursor cursor = fClass.Search(queryFilter, false))
                    {
                        while (cursor.MoveNext())
                        {
                            Feature nextFeature = (Feature)cursor.Current;
                            BA_Objects.Site aSite = new BA_Objects.Site();
                            var pointGeometry = nextFeature.GetShape();
                            double bufferMeters = LinearUnit.Miles.ConvertTo(siteBufferDistanceMiles, LinearUnit.Meters);
                            aSite.Buffer = GeometryEngine.Instance.Buffer(pointGeometry, bufferMeters);

                            int idx = nextFeature.FindField(Constants.FIELD_SITE_ELEV);
                            if (idx > -1)
                            {
                                aSite.ElevMeters = Convert.ToDouble(nextFeature[idx]);
                            }

                            idx = nextFeature.FindField(Constants.FIELD_SITE_NAME);
                            if (idx > -1)
                            {
                                aSite.Name = Convert.ToString(nextFeature[idx]);
                            }
                            idx = nextFeature.FindField(Constants.FIELD_OBJECT_ID);
                            if (idx > -1)
                            {
                                aSite.ObjectId = Convert.ToInt32(nextFeature[idx]);
                            }
                            idx = nextFeature.FindField(Constants.FIELD_LONGITUDE);
                            if (idx > -1)
                            {
                                aSite.Longitude = Convert.ToDouble(nextFeature[idx]);
                            }
                            idx = nextFeature.FindField(Constants.FIELD_LATITUDE);
                            if (idx > -1)
                            {
                                aSite.Latitude = Convert.ToDouble(nextFeature[idx]);
                            }

                            aSite.SiteTypeText = sType;
                            lstSites.Add(aSite);
                            Module1.Current.ModuleLogManager.LogDebug(nameof(AssembleSitesListAsync),
                                "Added site " + aSite.Name + " to list");
                        }
                    }
                }
            });
            return lstSites;
        }

        public static async Task<IList<BA_Objects.Site>> AssembleMergedSitesListAsync(Uri uriGdb)
        {
            IList<BA_Objects.Site> lstSites = new List<BA_Objects.Site>();
            // Open geodatabase for sites
            await QueuedTask.Run(() =>
            {
                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(uriGdb)))
                using (FeatureClass fClass = geodatabase.OpenDataset<FeatureClass>(Constants.FILE_MERGED_SITES))
                {
                    QueryFilter queryFilter = new QueryFilter();
                    using (RowCursor cursor = fClass.Search(queryFilter, false))
                    {
                        while (cursor.MoveNext())
                        {
                            Feature nextFeature = (Feature)cursor.Current;
                            BA_Objects.Site aSite = new BA_Objects.Site();
                            int idx = nextFeature.FindField(Constants.FIELD_SITE_ELEV);
                            if (idx > -1)
                            {
                                aSite.ElevMeters = Convert.ToDouble(nextFeature[idx]);
                            }

                            idx = nextFeature.FindField(Constants.FIELD_SITE_NAME);
                            if (idx > -1)
                            {
                                aSite.Name = Convert.ToString(nextFeature[idx]);
                            }
                            idx = nextFeature.FindField(Constants.FIELD_OBJECT_ID);
                            if (idx > -1)
                            {
                                aSite.ObjectId = Convert.ToInt32(nextFeature[idx]);
                            }
                            idx = nextFeature.FindField(Constants.FIELD_LONGITUDE);
                            if (idx > -1)
                            {
                                aSite.Longitude = Convert.ToDouble(nextFeature[idx]);
                            }
                            idx = nextFeature.FindField(Constants.FIELD_LATITUDE);
                            if (idx > -1)
                            {
                                aSite.Latitude = Convert.ToDouble(nextFeature[idx]);
                            }
                            idx = nextFeature.FindField(Constants.FIELD_SITE_TYPE);
                            if (idx > -1)
                            {
                                aSite.SiteTypeText = Convert.ToString(nextFeature[idx]);
                            }
                            idx = nextFeature.FindField(Constants.FIELD_ASPECT);
                            if (idx > -1)
                            {
                                aSite.Aspect = Convert.ToDouble(nextFeature[idx]);
                            }
                            idx = nextFeature.FindField(Constants.FIELD_DIRECTION);
                            if (idx > -1)
                            {
                                aSite.AspectDirection = Convert.ToString(nextFeature[idx]);
                            }
                            idx = nextFeature.FindField(Constants.FIELD_SLOPE);
                            if (idx > -1)
                            {
                                aSite.Slope = Convert.ToDouble(nextFeature[idx]);
                            }
                            idx = nextFeature.FindField(Constants.FIELD_PRECIP);
                            if (idx > -1)
                            {
                                aSite.Precipitation = Convert.ToDouble(nextFeature[idx]);
                            }

                            lstSites.Add(aSite);
                            Module1.Current.ModuleLogManager.LogDebug(nameof(AssembleMergedSitesListAsync),
                                "Added site " + aSite.Name + " to list");
                        }
                    }
                }
            });
            return lstSites;
        }

        public static async Task<BA_ReturnCode> CalculateRepresentedArea(double demElevMinMeters,
                                                                         double demElevMaxMeters, IList<BA_Objects.Site> lstSites,
                                                                         double siteElevRangeFeet, string strOutputFile)
        {
            IGPResult gpResult = null;
            StringBuilder sb = new StringBuilder();
            string tmpBuffer = "tmpBuffer";
            string tmpOutputFile = "reclElev";
            string tmpUnion = "tmpUnion";
            string tmpDissolve = "tmpDissolve";
            IList<string> lstLayersToDelete = new List<string> { tmpBuffer, tmpUnion, tmpDissolve };

            foreach (BA_Objects.Site aSite in lstSites)
            {
                //3. Create temporary feature class to hold buffered point
                var parameters = Geoprocessing.MakeValueArray(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false),
                    tmpBuffer, "POLYGON", "", "DISABLED", "DISABLED",
                    GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers, true) + Constants.FILE_SNOTEL);
                var environments = Geoprocessing.MakeEnvironmentArray(workspace: Module1.Current.Aoi.FilePath);
                gpResult = await Geoprocessing.ExecuteToolAsync("CreateFeatureclass_management", parameters, environments,
                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateRepresentedArea),
                    "Create temporary feature class for site " + aSite.Name);

                Uri gdbUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false));
                await QueuedTask.Run(() =>
                {
                    using (Geodatabase buffGeodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(gdbUri)))
                    {
                        //4. store buffered point in feature class
                        var featureClass = buffGeodatabase.OpenDataset<FeatureClass>(tmpBuffer);
                        var featureBuffer = featureClass.CreateRowBuffer();
                        var newFeature = featureClass.CreateRow(featureBuffer) as Feature;
                        newFeature.SetShape(aSite.Buffer);
                        newFeature.Store();
                    }
                });
                string strBufferedFeatures = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) + "tmpBuffer";

                // 5.Build list of reclass items
                double minElevMeters = aSite.ElevMeters - LinearUnit.Feet.ConvertToMeters(siteElevRangeFeet);
                IList<string> reclassList = new List<string>();
                // non-represented below min elev
                if (minElevMeters > demElevMinMeters)
                {
                    string belowString = demElevMinMeters + " " + minElevMeters + " NoData; ";
                    reclassList.Add(belowString);
                }
                else
                {
                    minElevMeters = demElevMinMeters;
                }
                bool hasNonRepresentedAbove = true;
                double maxElevMeters = aSite.ElevMeters + LinearUnit.Feet.ConvertToMeters(siteElevRangeFeet);
                if (maxElevMeters > demElevMaxMeters)
                {
                    maxElevMeters = demElevMaxMeters;
                    hasNonRepresentedAbove = false;
                }
                string representedString = minElevMeters + " " + maxElevMeters + " 1;";
                reclassList.Add(representedString);
                if (hasNonRepresentedAbove == true)
                {
                    reclassList.Add(maxElevMeters + " " + demElevMaxMeters + " NoData; ");
                }
                string reclassString = "";
                foreach (string strItem in reclassList)
                {
                    reclassString = reclassString + strItem;
                }

                string inputRasterPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Surfaces, true) + Constants.FILE_DEM_FILLED;
                string maskPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) + tmpBuffer;
                string outputRasterPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) + tmpOutputFile;

                //6. Execute the reclass with the mask set to the buffered point
                parameters = Geoprocessing.MakeValueArray(inputRasterPath, "VALUE", reclassString, outputRasterPath);
                environments = Geoprocessing.MakeEnvironmentArray(mask: maskPath, workspace: Module1.Current.Aoi.FilePath);
                gpResult = await Geoprocessing.ExecuteToolAsync("Reclassify_sa", parameters, environments,
                   CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateRepresentedArea),
                    "Execute reclass with mask set to buffered point");

                //7. Save the reclass as a poly so we can merge with other buffered site polys
                string siteRepFileName = AnalysisTools.GetSiteScenarioFileName(aSite);
                string siteRepresentedPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) + siteRepFileName;
                parameters = Geoprocessing.MakeValueArray(outputRasterPath, siteRepresentedPath);
                gpResult = await Geoprocessing.ExecuteToolAsync("RasterToPolygon_conversion", parameters, environments,
                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (!gpResult.IsFailed)
                {
                    sb.Append(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) + siteRepFileName);
                    sb.Append(";");
                    Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateRepresentedArea),
                        "Finished processing site " + aSite.Name);
                }
            }

            if (sb.Length > 0)
            {
                string inFeatures = sb.ToString().TrimEnd(';');
                string outputUnionPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) + tmpUnion;
                var parameters = Geoprocessing.MakeValueArray(inFeatures, outputUnionPath);
                var environments = Geoprocessing.MakeEnvironmentArray(workspace: Module1.Current.Aoi.FilePath);
                gpResult = await Geoprocessing.ExecuteToolAsync("Union_analysis", parameters, environments,
                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);

                string outputDissolvePath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) + tmpDissolve;
                if (!gpResult.IsFailed)
                {
                    parameters = Geoprocessing.MakeValueArray(outputUnionPath, outputDissolvePath);
                    gpResult = await Geoprocessing.ExecuteToolAsync("Dissolve_management", parameters, environments,
                        CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                    Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateRepresentedArea),
                        "Finished merging sites");
                }
                else
                {
                    return BA_ReturnCode.UnknownError;
                }

                if (!gpResult.IsFailed)
                {
                    string outputClipPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) + strOutputFile;
                    string sMask = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_VECTOR;
                    parameters = Geoprocessing.MakeValueArray(outputDissolvePath, sMask, outputClipPath);
                    gpResult = await Geoprocessing.ExecuteToolAsync("Clip_analysis", parameters, environments,
                        CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                    Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateRepresentedArea),
                        "Finished clipping sites layer");
                    Uri uriAnalysis = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis));
                    foreach (string fileName in lstLayersToDelete)
                    {
                        if (await GeodatabaseTools.FeatureClassExistsAsync(uriAnalysis, fileName))
                        {
                            await GeoprocessingTools.DeleteDatasetAsync(uriAnalysis.LocalPath + "\\" + fileName);
                        }
                    }
                    if (await GeodatabaseTools.RasterDatasetExistsAsync(uriAnalysis, tmpOutputFile))
                    {
                        await GeoprocessingTools.DeleteDatasetAsync(uriAnalysis.LocalPath + "\\" + tmpOutputFile);
                    }
                    Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateRepresentedArea),
                        "Finished deleting temp files");
                }
                else
                {
                    return BA_ReturnCode.UnknownError;
                }

            }

            //if (gpResult != null)
            //{
            //    Geoprocessing.ShowMessageBox(gpResult.Messages, "GP Messages",
            //        gpResult.IsFailed ? GPMessageBoxStyle.Error : GPMessageBoxStyle.Default);
            //}
            return BA_ReturnCode.Success;
        }

        public static async Task<BA_ReturnCode> CombineSitesRepresentedArea()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) + Constants.FILE_SCOS_REPRESENTED);
            sb.Append(";");
            sb.Append(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) + Constants.FILE_SNOTEL_REPRESENTED);
            string tmpUnion = "tmpUnion";
            string outputUnionPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) + tmpUnion;
            string inFeatures = sb.ToString().TrimEnd(';');
            var parameters = Geoprocessing.MakeValueArray(inFeatures, outputUnionPath);
            var environments = Geoprocessing.MakeEnvironmentArray(workspace: Module1.Current.Aoi.FilePath);
            IGPResult gpResult = await Geoprocessing.ExecuteToolAsync("Union_analysis", parameters, environments,
                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);

            string outputDissolvePath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) + Constants.FILE_SITES_REPRESENTED;
            if (!gpResult.IsFailed)
            {
                parameters = Geoprocessing.MakeValueArray(outputUnionPath, outputDissolvePath);
                gpResult = await Geoprocessing.ExecuteToolAsync("Dissolve_management", parameters, environments,
                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                Module1.Current.ModuleLogManager.LogDebug(nameof(CombineSitesRepresentedArea),
                    "Finished merging all sites");
                Uri analysisUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis));
                if (await GeodatabaseTools.FeatureClassExistsAsync(analysisUri, tmpUnion))
                {
                    await GeoprocessingTools.DeleteDatasetAsync(analysisUri.LocalPath + "\\" + tmpUnion);
                }
                Module1.Current.ModuleLogManager.LogDebug(nameof(CombineSitesRepresentedArea),
                    "Deleted temp file");
            }
            else
            {
                return BA_ReturnCode.UnknownError;
            }
            return BA_ReturnCode.Success;
        }


        public static string GetSiteScenarioFileName(BA_Objects.Site site)
        {
            // strip special characters out of site name
            StringBuilder sb = new StringBuilder();
            foreach (char c in site.Name)
            {
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '.' || c == '_')
                {
                    sb.Append(c);
                }
            }
            sb.Append("_");
            sb.Append((int)site.SiteType);
            sb.Append("_");
            sb.Append(site.ObjectId);
            sb.Append("_Rep");
            return sb.ToString();
        }

        public static async Task<string[]> GetStationValues(string aoiFilePath)
        {
            string strTriplet = "";
            string strAwdbId = "";
            string strStationName = "";
            string[] arrReturnValues = new string[] { strTriplet, strStationName };
            Uri ppUri = new Uri(GeodatabaseTools.GetGeodatabasePath(aoiFilePath, GeodatabaseNames.Aoi));
            string strPourpointClassPath = ppUri.LocalPath + "\\" + Constants.FILE_POURPOINT;
            Webservices ws = new Webservices();
            Module1.Current.ModuleLogManager.LogDebug(nameof(GetStationValues),
                "Contacting webservices server to retrieve pourpoint layer uri");
            var url = (string)Module1.Current.BatchToolSettings.EBagisServer + Constants.URI_DESKTOP_SETTINGS;
            var response = new EsriHttpClient().Get(url);
            var json = await response.Content.ReadAsStringAsync();
            dynamic oSettings = JObject.Parse(json);
            if (oSettings == null || String.IsNullOrEmpty(Convert.ToString(oSettings.gaugeStation)))
            {
                Module1.Current.ModuleLogManager.LogDebug(nameof(GetStationValues),
                    "Unable to retrieve pourpoint settings from " + url);
                MessageBox.Show("Unable to retrieve pourpoint settings. Clipping cancelled!!", "BAGIS-PRO");
                return null;
            }
            string strWsUri = oSettings.gaugeStation;
            string usgsServiceLayerId = strWsUri.Split('/').Last();
            int intTrim = usgsServiceLayerId.Length + 1;
            string usgsTempString = strWsUri.Substring(0, strWsUri.Length - intTrim);
            Uri usgsServiceUri = new Uri(usgsTempString);

            bool bUpdateTriplet = false;
            bool bUpdateAwdb = false;
            bool bUpdateStationName = false;
            if (await GeodatabaseTools.FeatureClassExistsAsync(ppUri, Constants.FILE_POURPOINT))
            {
                string[] arrFields = new string[] { Constants.FIELD_STATION_TRIPLET, Constants.FIELD_STATION_NAME };
                foreach (string strField in arrFields)
                {
                    // Check for the field, if it exists query the value
                    if (await GeodatabaseTools.AttributeExistsAsync(ppUri, Constants.FILE_POURPOINT, strField))
                    {
                        QueryFilter queryFilter = new QueryFilter();
                        string strValue = await GeodatabaseTools.QueryTableForSingleValueAsync(ppUri, Constants.FILE_POURPOINT,
                            strField, queryFilter);
                        switch (strField)
                        {
                            case Constants.FIELD_STATION_TRIPLET:
                                strTriplet = strValue;
                                break;
                            case Constants.FIELD_STATION_NAME:
                                strStationName = strValue;
                                break;
                        }
                    }
                    // Add the field if it is missing
                    else
                    {
                        BA_ReturnCode success = await GeoprocessingTools.AddFieldAsync(strPourpointClassPath, strField, "TEXT");
                    }
                }

                if (String.IsNullOrEmpty(strTriplet))
                {
                    // Use the awdb_id to query for the triplet from the pourpoint layer
                    strAwdbId = await GeodatabaseTools.QueryTableForSingleValueAsync(ppUri, Constants.FILE_POURPOINT,
                        Constants.FIELD_AWDB_ID, new QueryFilter());
                    if (!String.IsNullOrEmpty(strAwdbId))
                    {
                        string strAwdbQueryId = strAwdbId.Trim();
                        if (strAwdbQueryId.Length < 8)     // left pad the triplet if less than 8 characters
                        {
                            strAwdbQueryId = strAwdbQueryId.PadLeft(8, '0');
                        }
                        QueryFilter queryFilter = new QueryFilter();
                        queryFilter.WhereClause = Constants.FIELD_USGS_ID + " = '" + strAwdbQueryId + "'";
                        string[] arrSearch = { Constants.FIELD_STATION_TRIPLET, (string)oSettings.gaugeStationName };
                        string[] arrFound = new string[arrSearch.Length];
                        Module1.Current.ModuleLogManager.LogDebug(nameof(GetStationValues),
                            "Using awdb_id to query for the triplet from " + usgsServiceUri.ToString());
                        arrFound = await ws.QueryServiceForValuesAsync(usgsServiceUri, usgsServiceLayerId, arrSearch, queryFilter);
                        if (arrFound != null && arrFound.Length > 1)
                        {
                            strTriplet = arrFound[0];
                            strStationName = arrFound[1];
                        }
                        else if (arrFound != null && arrFound.Length > 1 && arrFound[0] == null)
                        {
                            Module1.Current.ModuleLogManager.LogError(nameof(GetStationValues),
                                "Unable to retrieve at least 1 property from the master aoi webservice");
                        }
                        if (!string.IsNullOrEmpty(strTriplet))
                        {
                            bUpdateTriplet = true;
                        }
                        if (!string.IsNullOrEmpty(strStationName))
                        {
                            bUpdateStationName = true;
                        }
                    }
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(GetStationValues),
                    "Triplet retrieved from pourpoint feature class: " + strTriplet);
                }
                if (string.IsNullOrEmpty(strTriplet))
                {
                    // If triplet is still null, use the near tool
                    BA_ReturnCode success = await GeoprocessingTools.NearAsync(strPourpointClassPath, strWsUri);
                    if (success == BA_ReturnCode.Success)
                    {
                        QueryFilter queryFilter = new QueryFilter();
                        string strNearId = await GeodatabaseTools.QueryTableForSingleValueAsync(ppUri, Constants.FILE_POURPOINT,
                            Constants.FIELD_NEAR_ID, queryFilter);
                        string[] arrSearch = { Constants.FIELD_STATION_TRIPLET, Constants.FIELD_USGS_ID, (string)oSettings.gaugeStationName };
                        string[] arrFound = new string[arrSearch.Length];
                        if (!String.IsNullOrEmpty(strNearId))
                        {
                            queryFilter.WhereClause = Constants.FIELD_OBJECT_ID + " = '" + strNearId + "'";
                            arrFound = await ws.QueryServiceForValuesAsync(usgsServiceUri, usgsServiceLayerId, arrSearch, queryFilter);
                            if (arrFound != null && arrFound.Length == 3 && arrFound[0] != null)
                            {
                                strTriplet = arrFound[0];
                                strAwdbId = arrFound[1];
                                strStationName = arrFound[2];
                            }
                            else
                            {
                                Module1.Current.ModuleLogManager.LogError(nameof(GetStationValues),
                                    "Unable to retrieve at least 1 property from the master aoi webservice");
                            }

                            if (!String.IsNullOrEmpty(strTriplet))
                            {
                                bUpdateTriplet = true;
                            }
                            if (!String.IsNullOrEmpty(strAwdbId))
                            {
                                bUpdateAwdb = true;
                            }
                            if (!string.IsNullOrEmpty(strStationName))
                            {
                                bUpdateStationName = true;
                            }
                        }
                        //Delete fields added by NEAR process: NEAR_DIST and NEAR_ID
                        string[] arrFieldsToDelete = new string[] { Constants.FIELD_NEAR_ID, Constants.FIELD_NEAR_DIST };
                        success = await GeoprocessingTools.DeleteFeatureClassFieldsAsync(strPourpointClassPath, arrFieldsToDelete);
                    }
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(GetStationValues),
                        "Triplet retrieved using the awdb_id and USGS webservice: " + strTriplet);
                }
                //Save the new values to the pourpoint layer if needed
                if (bUpdateAwdb == true || bUpdateTriplet == true || bUpdateStationName == true)
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(GetStationValues),
                        "Updating pourpoint layer attributes");
                    IDictionary<string, string> dictEdits = new Dictionary<string, string>();
                    if (bUpdateAwdb)
                        dictEdits.Add(Constants.FIELD_AWDB_ID, strAwdbId);
                    if (bUpdateTriplet)
                        dictEdits.Add(Constants.FIELD_STATION_TRIPLET, strTriplet);
                    if (bUpdateStationName)
                        dictEdits.Add(Constants.FIELD_STATION_NAME, strStationName);
                    BA_ReturnCode success = await GeodatabaseTools.UpdateFeatureAttributesAsync(ppUri, Constants.FILE_POURPOINT,
                        new QueryFilter(), dictEdits);
                }
            }
            arrReturnValues[0] = strTriplet;
            arrReturnValues[1] = strStationName;
            return arrReturnValues;
        }

        public static async Task<BA_ReturnCode> ClipLayersAsync(string strAoiPath, string strDataType,
            string prismBufferDistance, string prismBufferUnits, string strBufferDistance, string strBufferUnits)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;

            Webservices ws = new Webservices();
            Module1.Current.ModuleLogManager.LogDebug(nameof(ClipLayersAsync),
                "Contacting webservices server to retrieve layer metadata");
            IDictionary<string, dynamic> dictDataSources =
                await ws.QueryDataSourcesAsync((string)Module1.Current.BatchToolSettings.EBagisServer);
            string strWsPrefix = dictDataSources[strDataType].uri;

            string[] arrLayersToDelete = new string[2];
            string strClipGdb = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Aoi, false);
            string strClipFile = Constants.FILE_AOI_PRISM_VECTOR;
            int intError = 0;

            if (!String.IsNullOrEmpty(strWsPrefix))
            {
                await QueuedTask.Run(async () =>
                {
                    try
                    {

                        // if the buffer is different from PRISM, we need to create a new buffer file
                        string strTempBuffer = "tmpBuffer";
                        string strTempBuffer2 = "";
                        if (!strBufferDistance.Trim().Equals(prismBufferDistance.Trim()) ||
                            !strBufferUnits.Trim().Equals(prismBufferUnits.Trim()))
                        {
                            // Allow for possibility of unbuffered PRISM layer
                            if (Convert.ToInt16(strBufferDistance) == 0)
                            {
                                // Copy the updated buffered file into the aoi.gdb
                                var parameters = Geoprocessing.MakeValueArray(GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Aoi, true) +
                                    Constants.FILE_AOI_BUFFERED_VECTOR, GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Aoi, true) +
                                    Constants.FILE_AOI_PRISM_VECTOR);
                                var gpResult = Geoprocessing.ExecuteToolAsync("CopyFeatures", parameters, null,
                                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                                if (gpResult.Result.IsFailed)
                                {
                                    Module1.Current.ModuleLogManager.LogError(nameof(ClipLayersAsync),
                                       "Unable to copy aoib_v to p_aoi_v. Error code: " + gpResult.Result.ErrorCode);
                                    return;
                                }
                                strClipFile = Constants.FILE_AOI_PRISM_VECTOR;
                            }
                            else
                            {
                                string strAoiBoundaryPath = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Aoi, true) +
                                    Constants.FILE_AOI_VECTOR;
                                string strOutputFeatures = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Aoi, true) +
                                    strTempBuffer;
                                string strDistance = strBufferDistance + " " + strBufferUnits;
                                var parameters = Geoprocessing.MakeValueArray(strAoiBoundaryPath, strOutputFeatures, strDistance, "",
                                                                                  "", "ALL");
                                var gpResult = Geoprocessing.ExecuteToolAsync("Buffer_analysis", parameters, null,
                                                     CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                                if (gpResult.Result.IsFailed)
                                {
                                    Module1.Current.ModuleLogManager.LogError(nameof(ClipLayersAsync),
                                       "Unable to buffer aoi_v. Error code: " + gpResult.Result.ErrorCode);
                                    return;
                                }

                                strClipFile = strTempBuffer;
                                arrLayersToDelete[0] = strTempBuffer;
                                if (strDataType.Equals(Constants.DATA_TYPE_PRECIPITATION))
                                {
                                    // Copy the updated buffered file into the aoi.gdb
                                    parameters = Geoprocessing.MakeValueArray(strOutputFeatures, GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Aoi, true) +
                                        Constants.FILE_AOI_PRISM_VECTOR);
                                    gpResult = Geoprocessing.ExecuteToolAsync("CopyFeatures", parameters, null,
                                        CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                                    if (gpResult.Result.IsFailed)
                                    {
                                        MessageBox.Show("Unable to update p_aoi_v. Clipping cancelled!!", "BAGIS-PRO");
                                        return;
                                    }
                                    else
                                    {
                                        Module1.Current.ModuleLogManager.LogDebug(nameof(ClipLayersAsync),
                                            "Saved updated p_aoi_v to aoi.gdb");
                                    }
                                }
                            }

                            // Check to make sure the buffer file only has one feature; No dangles
                            int featureCount = 0;
                            strClipGdb = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Aoi, false);
                            using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strClipGdb))))
                            using (Table table = geodatabase.OpenDataset<Table>(strClipFile))
                            {
                                featureCount = table.GetCount();
                            }
                            Module1.Current.ModuleLogManager.LogDebug(nameof(ClipLayersAsync),
                                "Number of features in clip file: " + featureCount);

                            // If > 1 feature, buffer the clip file again
                            if (featureCount > 1)
                            {
                                strTempBuffer2 = "tempBuffer2";
                                var parameters = Geoprocessing.MakeValueArray(strClipGdb + "\\" + strClipFile,
                                    strClipGdb + "\\" + strTempBuffer2, "0.5 Meters", "", "", "ALL");
                                var gpResult = Geoprocessing.ExecuteToolAsync("Buffer_analysis", parameters, null,
                                                     CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                                if (gpResult.Result.IsFailed)
                                {
                                    Module1.Current.ModuleLogManager.LogError(nameof(ClipLayersAsync),
                                       "Unable to buffer " + strClipFile + ". Error code: " + gpResult.Result.ErrorCode);
                                    return;
                                }
                                strClipFile = strTempBuffer2;
                                arrLayersToDelete[1] = strTempBuffer;
                                Module1.Current.ModuleLogManager.LogDebug(nameof(ClipLayersAsync),
                                    "Run buffer tool again because clip file has > 2 features");
                            }
                        }

                        // Query the extent for the clip
                        string strClipEnvelope = "";
                        using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strClipGdb))))
                        using (Table table = geodatabase.OpenDataset<Table>(strClipFile))
                        {
                            QueryFilter queryFilter = new QueryFilter();
                            using (RowCursor cursor = table.Search(queryFilter, false))
                            {
                                while (cursor.MoveNext())
                                {
                                    using (Feature feature = (Feature)cursor.Current)
                                    {
                                        Geometry aoiGeo = feature.GetShape();
                                        strClipEnvelope = aoiGeo.Extent.XMin + " " + aoiGeo.Extent.YMin + " " + aoiGeo.Extent.XMax + " " + aoiGeo.Extent.YMax;
                                    }
                                }
                            }
                        }
                        if (String.IsNullOrEmpty(strClipEnvelope))
                        {
                            Module1.Current.ModuleLogManager.LogError(nameof(ClipLayersAsync),
                                "Unable obtain clipping envelope from " + strClipGdb + "\\" + strClipFile);
                            return;
                        }

                        // Prepare the data to be clipped depending on data type
                        string[] arrClipUris = Constants.URIS_SNODAS_SWE;
                        string[] arrClippedFileNames = Constants.FILES_SNODAS_SWE;
                        string strOutputGdb = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Layers, true);

                        // Reset some variables if clipping PRISM
                        if (strDataType.Equals(Constants.DATA_TYPE_PRECIPITATION))
                        {
                            int prismCount = Enum.GetNames(typeof(PrismFile)).Length;
                            int seasonalPrismCount = Enum.GetNames(typeof(SeasonalPrismFile)).Length;
                            Array.Resize<string>(ref arrClipUris, prismCount + seasonalPrismCount);
                            int j = 0;
                            int count = 0;
                            foreach (var month in Enum.GetValues(typeof(PrismServiceNames)))
                            {
                                arrClipUris[j] = month.ToString();
                                j++;
                                count++;
                            }
                            Array.Resize<string>(ref arrClippedFileNames, prismCount + seasonalPrismCount);
                            j = 0;
                            foreach (var month in Enum.GetValues(typeof(PrismFile)))
                            {
                                arrClippedFileNames[j] = month.ToString();
                                j++;
                            }
                            j = count;
                            foreach (var quarter in Enum.GetValues(typeof(SeasonalPrismServiceNames)))
                            {
                                arrClipUris[j] = quarter.ToString();
                                j++;
                            }
                            j = count;
                            foreach (var quarter in Enum.GetValues(typeof(SeasonalPrismFile)))
                            {
                                arrClippedFileNames[j] = quarter.ToString();
                                j++;
                            }
                            strOutputGdb = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Prism, true);
                        }

                        int i = 0;
                        foreach (string strUri in arrClipUris)
                        {
                            Uri imageServiceUri = new Uri(strWsPrefix + strUri + Constants.URI_IMAGE_SERVER);
                            string strOutputRaster = strOutputGdb + arrClippedFileNames[i];
                            string strTemplateDataset = strClipGdb + "\\" + strClipFile;
                            var environments = Geoprocessing.MakeEnvironmentArray(workspace: strAoiPath, snapRaster: BA_Objects.Aoi.SnapRasterPath(strAoiPath));
                            var parameters = Geoprocessing.MakeValueArray(imageServiceUri.AbsoluteUri, strClipEnvelope, strOutputRaster, strTemplateDataset,
                                                "", "ClippingGeometry");
                            var gpResult = await Geoprocessing.ExecuteToolAsync("Clip_management", parameters, environments,
                                            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                            if (gpResult.IsFailed)
                            {
                                Module1.Current.ModuleLogManager.LogError(nameof(ClipLayersAsync),
                                   "Unable to clip " + imageServiceUri.AbsoluteUri + " to " + strOutputRaster);
                                foreach (var objMessage in gpResult.Messages)
                                {
                                    IGPMessage msg = (IGPMessage)objMessage;
                                    Module1.Current.ModuleLogManager.LogError(nameof(ClipLayersAsync),
                                        msg.Text);
                                }
                                intError++;     // increment error counter
                            }
                            else
                            {
                                Module1.Current.ModuleLogManager.LogDebug(nameof(ClipLayersAsync),
                                    "Clipped " + arrClippedFileNames[i] + " layer");
                                //We need to add a new tag at "/metadata/dataIdInfo/searchKeys/keyword"
                                StringBuilder sb = new StringBuilder();
                                sb.Append(Constants.META_TAG_PREFIX);
                                // Z Units
                                string strUnits = dictDataSources[strDataType].units;
                                sb.Append(Constants.META_TAG_ZUNIT_CATEGORY + Constants.META_TAG_CATEGORY_DEPTH + "; ");
                                sb.Append(Constants.META_TAG_ZUNIT_VALUE + strUnits + "; ");
                                // Buffer Distance
                                sb.Append(Constants.META_TAG_BUFFER_DISTANCE + strBufferDistance + "; ");
                                // X Units
                                sb.Append(Constants.META_TAG_XUNIT_VALUE + strBufferUnits + "; ");
                                sb.Append(Constants.META_TAG_SUFFIX);

                                //Update the metadata
                                var fc = ItemFactory.Instance.Create(strOutputRaster,
                                    ItemFactory.ItemType.PathItem);
                                if (fc != null)
                                {
                                    string strXml = string.Empty;
                                    strXml = fc.GetXml();
                                    System.Xml.XmlDocument xmlDocument = GeneralTools.UpdateMetadata(strXml, Constants.META_TAG_XPATH, sb.ToString(),
                                        Constants.META_TAG_PREFIX.Length);

                                    fc.SetXml(xmlDocument.OuterXml);
                                }
                            }
                            i++;
                        }

                        // Delete temporary layers
                        for (int j = 0; j < arrLayersToDelete.Length; j++)
                        {
                            if (!string.IsNullOrEmpty(arrLayersToDelete[j]))
                            {
                                var parameters = Geoprocessing.MakeValueArray(strClipGdb + "\\" + arrLayersToDelete[j]);
                                var gpResult = Geoprocessing.ExecuteToolAsync("Delete_management", parameters, null,
                                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                                if (gpResult.Result.IsFailed)
                                {
                                    Module1.Current.ModuleLogManager.LogError(nameof(ClipLayersAsync),
                                        "Unable to delete " + strClipGdb + "\\" + arrLayersToDelete[j] + ". Error code: " + gpResult.Result.ErrorCode);
                                }
                                else
                                {
                                    Module1.Current.ModuleLogManager.LogDebug(nameof(ClipLayersAsync),
                                        "Successfully deleted temp file: " + strClipGdb + "\\" + arrLayersToDelete[j]);
                                }
                            }
                        }

                        // Update layer metadata
                        IDictionary<string, BA_Objects.DataSource> dictLocalDataSources = GeneralTools.QueryLocalDataSources();
                        BA_Objects.DataSource updateDataSource = new BA_Objects.DataSource(dictDataSources[strDataType])
                        {
                            DateClipped = DateTime.Now,
                        };
                        if (dictLocalDataSources.ContainsKey(strDataType))
                        {
                            dictLocalDataSources[strDataType] = updateDataSource;
                        }
                        else
                        {
                            dictLocalDataSources.Add(strDataType, updateDataSource);
                        }
                        success = GeneralTools.SaveDataSourcesToFile(dictLocalDataSources);
                        Module1.Current.ModuleLogManager.LogDebug(nameof(ClipLayersAsync),
                            "Updated settings metadata for " + strDataType);

                    }
                    catch (Exception e)
                    {
                        intError++;
                        Module1.Current.ModuleLogManager.LogError(nameof(ClipLayersAsync),
                            "Exception: " + e.StackTrace);
                    }
                });
            }
            if (intError > 0)
            {
                Module1.Current.ModuleLogManager.LogDebug(nameof(ClipLayersAsync),
                    "At least one error occurred while clipping " + strDataType + " layers!");
            }
            success = BA_ReturnCode.Success;
            return success;
        }

        public static async Task<BA_ReturnCode> CalculateZonesAsync(string strAoiPath, string strSourceLayer,
            IList<BA_Objects.Interval> lstInterval, string strOutputLayer, string strMaskPath, string strMessageKey)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            EditOperation editOperation = new EditOperation();

            await QueuedTask.Run(() =>
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < lstInterval.Count; i++)
                {
                    BA_Objects.Interval nextInterval = lstInterval[i];
                    sb.Append(nextInterval.LowerBound + " " + nextInterval.UpperBound +
                        " " + nextInterval.Value + "; ");
                }
                Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateZonesAsync),
                    "Reclass string: " + sb.ToString());
                var parameters = Geoprocessing.MakeValueArray(strSourceLayer, "VALUE", sb.ToString(), strOutputLayer);
                var environments = Geoprocessing.MakeEnvironmentArray(workspace: strAoiPath, mask: strMaskPath, snapRaster: BA_Objects.Aoi.SnapRasterPath(strAoiPath));
                var gpResult = Geoprocessing.ExecuteToolAsync("Reclassify_sa", parameters, environments,
                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (gpResult.Result.IsFailed)
                {
                    MessageBox.Show("Failed to reclass " + strMessageKey + " raster. No zones calculated!!", "BAGIS-PRO");
                    Module1.Current.ModuleLogManager.LogError(nameof(CalculateZonesAsync),
                        "Failed to reclass " + strMessageKey + " raster. Error code: " + gpResult.Result.ErrorCode);
                    foreach (var objMessage in gpResult.Result.Messages)
                    {
                        IGPMessage msg = (IGPMessage)objMessage;
                        Module1.Current.ModuleLogManager.LogError(nameof(CalculateZonesAsync),
                            msg.Text);
                    }

                }
                else
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateZonesAsync),
                    strMessageKey + " reclass complete");
                    success = BA_ReturnCode.Success;
                }
                if (success == BA_ReturnCode.Success)
                {
                    // Add fields to table so we can process the interval list
                    string strAddFields = Constants.FIELD_NAME + " TEXT # " + Constants.FIELD_NAME_WIDTH + " # #;" +
                                          Constants.FIELD_LBOUND + " DOUBLE # # # #;" +
                                          Constants.FIELD_UBOUND + " DOUBLE # # # #";
                    parameters = Geoprocessing.MakeValueArray(strOutputLayer, strAddFields);
                    gpResult = Geoprocessing.ExecuteToolAsync("AddFields_management", parameters, null,
                        CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                    if (gpResult.Result.IsFailed)
                    {
                        MessageBox.Show("Failed to add fields. Zones not calculated!!", "BAGIS-PRO");
                        Module1.Current.ModuleLogManager.LogError(nameof(CalculateZonesAsync),
                            "Failed to add fields. Error code: " + gpResult.Result.ErrorCode);
                        foreach (var objMessage in gpResult.Result.Messages)
                        {
                            IGPMessage msg = (IGPMessage)objMessage;
                            Module1.Current.ModuleLogManager.LogError(nameof(CalculateZonesAsync),
                                msg.Text);
                        }
                        success = BA_ReturnCode.WriteError;
                    }
                    else
                    {
                        Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateZonesAsync),
                        "New fields added");
                    }
                }
                if (success == BA_ReturnCode.Success)
                {
                    string strGdbFolder = System.IO.Path.GetDirectoryName(strOutputLayer);
                    string strFileName = System.IO.Path.GetFileName(strOutputLayer);
                    using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strGdbFolder))))
                    using (RasterDataset rasterDataset = geodatabase.OpenDataset<RasterDataset>(strFileName))
                    {
                        RasterBandDefinition bandDefinition = rasterDataset.GetBand(0).GetDefinition();
                        Raster raster = rasterDataset.CreateDefaultRaster();
                        Table rasterTable = raster.GetAttributeTable();
                        TableDefinition definition = rasterTable.GetDefinition();
                        QueryFilter oQueryFilter = new QueryFilter();
                        editOperation.Callback(context =>
                        {
                            foreach (BA_Objects.Interval interval in lstInterval)
                            {
                                oQueryFilter.WhereClause = " Value = " + interval.Value;
                                using (RowCursor rowCursor = rasterTable.Search(oQueryFilter, false))
                                {
                                    // Only one row should be returned
                                    rowCursor.MoveNext();
                                    using (Row row = (Row)rowCursor.Current)
                                    {
                                        if (row != null)
                                        {
                                            // In order to update the the attribute table has to be called before any changes are made to the row
                                            context.Invalidate(row);
                                            int idxRow = definition.FindField(Constants.FIELD_NAME);
                                            if (idxRow > 0)
                                            {
                                                row[idxRow] = interval.Name;
                                            }
                                            idxRow = definition.FindField(Constants.FIELD_UBOUND);
                                            if (idxRow > 0)
                                            {
                                                row[idxRow] = interval.UpperBound;
                                            }
                                            idxRow = definition.FindField(Constants.FIELD_LBOUND);
                                            if (idxRow > 0)
                                            {
                                                row[idxRow] = interval.LowerBound;
                                            }
                                            row.Store();
                                            // Has to be called after the store too
                                            context.Invalidate(row);
                                        }
                                    }
                                }
                            }
                        }, rasterTable);
                    }
                }
                if (success == BA_ReturnCode.Success)
                {
                    bool bModificationResult = false;
                    string errorMsg = "";
                    try
                    {
                        bModificationResult = editOperation.Execute();
                        if (!bModificationResult) errorMsg = editOperation.ErrorMessage;
                    }
                    catch (GeodatabaseException exObj)
                    {
                        success = BA_ReturnCode.WriteError;
                        errorMsg = exObj.Message;
                    }

                    if (String.IsNullOrEmpty(errorMsg))
                    {
                        Project.Current.SaveEditsAsync();
                        success = BA_ReturnCode.Success;
                    }
                    else
                    {
                        if (Project.Current.HasEdits)
                            Project.Current.DiscardEditsAsync();
                        Module1.Current.ModuleLogManager.LogError(nameof(CalculateZonesAsync),
                            "Exception: " + errorMsg);
                        success = BA_ReturnCode.UnknownError;
                    }
                }
            });
            return success;
        }

        public static IList<BA_Objects.Interval> GetAspectClasses(int aspectDirections = 16)
        {
            IList<BA_Objects.Interval> lstIntervals = new List<BA_Objects.Interval>();
            int aspectCount = aspectDirections + 2;
            string[] aspectName = new string[aspectCount];
            double interval = 0.0F;

            switch (aspectDirections)
            {
                case 4:
                    interval = 90; // i.e., 360 / 4
                    aspectName[0] = "Flat";
                    aspectName[1] = "N";
                    aspectName[2] = "E";
                    aspectName[3] = "S";
                    aspectName[4] = "W";
                    aspectName[5] = "N";
                    break;
                case 8:
                    interval = 45; //i.e., 360 / 8
                    aspectName[0] = "Flat";
                    aspectName[1] = "N";
                    aspectName[2] = "NE";
                    aspectName[3] = "E";
                    aspectName[4] = "SE";
                    aspectName[5] = "S";
                    aspectName[6] = "SW";
                    aspectName[7] = "W";
                    aspectName[8] = "NW";
                    aspectName[9] = "N";
                    break;
                default:
                    interval = 22.5; //'i.e., 360 / 16
                    aspectName[0] = "Flat";
                    aspectName[1] = "N";
                    aspectName[2] = "NNE";
                    aspectName[3] = "NE";
                    aspectName[4] = "ENE";
                    aspectName[5] = "E";
                    aspectName[6] = "ESE";
                    aspectName[7] = "SE";
                    aspectName[8] = "SSE";
                    aspectName[9] = "S";
                    aspectName[10] = "SSW";
                    aspectName[11] = "SW";
                    aspectName[12] = "WSW";
                    aspectName[13] = "W";
                    aspectName[14] = "WNW";
                    aspectName[15] = "NW";
                    aspectName[16] = "NNW";
                    aspectName[17] = "N";
                    break;
            }

            // flat
            BA_Objects.Interval flatInterval = new BA_Objects.Interval
            {
                Value = -1,
                Name = "Flat",
                LowerBound = -2,
                UpperBound = -0.01
            };
            lstIntervals.Add(flatInterval);

            // north
            BA_Objects.Interval northInterval = new BA_Objects.Interval
            {
                Value = 1,
                Name = "N",
                // Assign 0 azimuth (north-facing direction) was assigned a value of 1
                LowerBound = -0.01,
                UpperBound = interval / 2
            };
            lstIntervals.Add(northInterval);

            for (int i = 3; i < aspectDirections + 2; i++)
            {
                BA_Objects.Interval nextInterval = new BA_Objects.Interval
                {
                    Value = i - 1,
                    Name = aspectName[i - 1],
                    LowerBound = lstIntervals.ElementAt(i - 2).UpperBound,
                    UpperBound = lstIntervals.ElementAt(i - 2).UpperBound + interval
                };
                lstIntervals.Add(nextInterval);
            }

            // north again
            northInterval = new BA_Objects.Interval
            {
                Value = 1,
                Name = "N",
                LowerBound = lstIntervals.ElementAt(aspectDirections).UpperBound,
                UpperBound = 360
            };
            lstIntervals.Add(northInterval);

            return lstIntervals;
        }

        public static IList<BA_Objects.Interval> GetSlopeClasses()
        {
            IList<BA_Objects.Interval> lstIntervals = new List<BA_Objects.Interval>();

            // flat
            BA_Objects.Interval nextInterval = new BA_Objects.Interval
            {
                Value = 1,
                Name = "Flat - 10%",
                LowerBound = 0,
                UpperBound = 10
            };
            lstIntervals.Add(nextInterval);

            // 10 - 20
            nextInterval = new BA_Objects.Interval
            {
                Value = 2,
                Name = "10% - 20%",
                LowerBound = 10,
                UpperBound = 20
            };
            lstIntervals.Add(nextInterval);

            // 20 - 30
            nextInterval = new BA_Objects.Interval
            {
                Value = 3,
                Name = "20% - 30%",
                LowerBound = 20,
                UpperBound = 30
            };
            lstIntervals.Add(nextInterval);

            // 30 - 40
            nextInterval = new BA_Objects.Interval
            {
                Value = 4,
                Name = "30% - 40%",
                LowerBound = 30,
                UpperBound = 40
            };
            lstIntervals.Add(nextInterval);

            // 40 - 50
            nextInterval = new BA_Objects.Interval
            {
                Value = 5,
                Name = "40% - 50%",
                LowerBound = 40,
                UpperBound = 50
            };
            lstIntervals.Add(nextInterval);

            // 50 - 75
            nextInterval = new BA_Objects.Interval
            {
                Value = 6,
                Name = "50% - 70%",
                LowerBound = 50,
                UpperBound = 75
            };
            lstIntervals.Add(nextInterval);

            // 75 - 100
            nextInterval = new BA_Objects.Interval
            {
                Value = 7,
                Name = "75% - 100%",
                LowerBound = 75,
                UpperBound = 100
            };
            lstIntervals.Add(nextInterval);

            // 100 - 150
            nextInterval = new BA_Objects.Interval
            {
                Value = 8,
                Name = "100% - 150%",
                LowerBound = 100,
                UpperBound = 150
            };
            lstIntervals.Add(nextInterval);

            // 150 - 200
            nextInterval = new BA_Objects.Interval
            {
                Value = 9,
                Name = "150% - 200%",
                LowerBound = 150,
                UpperBound = 200
            };
            lstIntervals.Add(nextInterval);

            // > 200
            nextInterval = new BA_Objects.Interval
            {
                Value = 10,
                Name = "> 200%",
                LowerBound = 200,
                UpperBound = 999
            };
            lstIntervals.Add(nextInterval);

            return lstIntervals;
        }

        public static async Task<IList<BA_Objects.Interval>> GetPrismClassesAsync(string strAoiPath,
            string strSourceLayer, int intZonesCount, string strMessageKey)
        {
            IList<BA_Objects.Interval> lstIntervals = new List<BA_Objects.Interval>();

            double dblMin = -999;
            double dblMax = 999;
            double dblInterval = 999;

            await QueuedTask.Run(() =>
            {
                // Get min and max values for layer
                var parameters = Geoprocessing.MakeValueArray(strSourceLayer, "MINIMUM");
                var environments = Geoprocessing.MakeEnvironmentArray(workspace: strAoiPath);
                var gpResult = Geoprocessing.ExecuteToolAsync("GetRasterProperties_management", parameters, environments,
                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                bool isDouble = Double.TryParse(Convert.ToString(gpResult.Result.ReturnValue), out dblMin);
                if (isDouble)
                {
                    dblMin = Math.Round(dblMin - 0.005, 2);
                    Module1.Current.ModuleLogManager.LogDebug(nameof(GetPrismClassesAsync),
                        "Found " + strMessageKey + " minimum to be " + dblMin);
                }
                else
                {
                    MessageBox.Show("Unable to extract minimum " + strMessageKey + " value. Calculation halted !!", "BAGIS-PRO");
                    Module1.Current.ModuleLogManager.LogError(nameof(GetPrismClassesAsync),
                        "Unable to calculate " + strMessageKey + " miniumum");
                    return;
                }
                parameters = Geoprocessing.MakeValueArray(strSourceLayer, "MAXIMUM");
                gpResult = Geoprocessing.ExecuteToolAsync("GetRasterProperties_management", parameters, environments,
                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                isDouble = Double.TryParse(Convert.ToString(gpResult.Result.ReturnValue), out dblMax);
                if (isDouble)
                {
                    dblMax = Math.Round(dblMax + 0.005, 2);
                    Module1.Current.ModuleLogManager.LogDebug(nameof(GetPrismClassesAsync),
                    "Found " + strMessageKey + " maximum to be " + dblMax);
                }
                else
                {
                    MessageBox.Show("Unable to extract maximum " + strMessageKey + " value. Calculation halted !!", "BAGIS-PRO");
                    Module1.Current.ModuleLogManager.LogError(nameof(GetPrismClassesAsync),
                        "Unable to calculate " + strMessageKey + " maximum");
                    return;
                }
                // determine interval value based on # map classes
                dblInterval = (dblMax - dblMin) / intZonesCount;
                // round the number to 1 decimal places
                dblInterval = Math.Round(dblInterval, 1);
            });
            int zones = GeneralTools.CreateRangeArray(dblMin, dblMax, dblInterval, out lstIntervals);
            Module1.Current.PrismZonesInterval = dblInterval;
            return lstIntervals;
        }

        public static IList<BA_Objects.Interval> GetElevationClasses(double dblMin, double dblMax,
            double dblInterval, string strElevUnits, string strDisplayUnits)
        {
            IList<BA_Objects.Interval> lstIntervals = new List<BA_Objects.Interval>();
            // Calculate range values for reclass in Display ZUnits. All of the dbl inputs are
            // in display units
            int zones = GeneralTools.CreateRangeArray(dblMin, dblMax, dblInterval, out lstIntervals);
            // Convert the upper and lower bound values in the interval list if the DEM unit
            // differs from the display unit
            if (!strElevUnits.Equals(strDisplayUnits))
            {
                foreach (var nextInterval in lstIntervals)
                {
                    string strDemUnits = Convert.ToString(Module1.Current.BatchToolSettings.DemUnits);
                    if (strDisplayUnits.Equals("Feet"))
                    {
                        nextInterval.LowerBound = LinearUnit.Feet.ConvertTo(nextInterval.LowerBound, LinearUnit.Meters);
                        nextInterval.UpperBound = LinearUnit.Feet.ConvertTo(nextInterval.UpperBound, LinearUnit.Meters);
                    }
                    else if (strDemUnits.Equals("Feet"))
                    {
                        nextInterval.LowerBound = LinearUnit.Meters.ConvertTo(nextInterval.LowerBound, LinearUnit.Feet);
                        nextInterval.UpperBound = LinearUnit.Meters.ConvertTo(nextInterval.UpperBound, LinearUnit.Feet);
                    }
                }
            }
            return lstIntervals;
        }

        public static async Task<BA_ReturnCode> ClipSnoLayersAsync(string strAoiPath, bool bClipSnotel,
            string snotelBufferDistance, bool bClipSnowCos, string snowCosBufferDistance)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            string strTempBuffer = "tmpBuffer";
            string[] arrLayersToDelete = new string[2];
            string snotelClipLayer = "";
            string[] strOutputFc = new string[2];
            string[] strOutputLayer = { "tmpSno", "tmpSnowCos" };
            string[] strFinalOutputLayer = { Constants.FILE_SNOTEL, Constants.FILE_SNOW_COURSE };
            string snowCosClipLayer = "";
            string strClipGdb = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Aoi, false);
            string strLayers = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Layers);
            Uri uriLayers = new Uri(strLayers);
            string strAnalysis = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Analysis);
            Uri uriAnalysis = new Uri(strAnalysis);

            Webservices ws = new Webservices();
            Module1.Current.ModuleLogManager.LogDebug(nameof(ClipSnoLayersAsync),
                "Contacting webservices server to retrieve layer metadata");
            IDictionary<string, dynamic> dictDataSources =
                await ws.QueryDataSourcesAsync((string)Module1.Current.BatchToolSettings.EBagisServer);
            if (dictDataSources != null)
            {
                if (!dictDataSources.ContainsKey(Constants.DATA_TYPE_SNOTEL) || 
                    !dictDataSources.ContainsKey(Constants.DATA_TYPE_SNOW_COURSE))
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(ClipSnoLayersAsync),
                        "Unable to retrieve snotel datasource information from " + (string) Module1.Current.BatchToolSettings.EBagisServer);
                    MessageBox.Show("Unable to retrieve snotel datasource information. Clipping cancelled!!", "BAGIS-PRO");
                    return success;
                }
            }
            else
            {
                Module1.Current.ModuleLogManager.LogDebug(nameof(ClipSnoLayersAsync),
                    "Unable to retrieve datasource information from " + (string)Module1.Current.BatchToolSettings.EBagisServer);
                MessageBox.Show("Unable to retrieve datasource information. Clipping cancelled!!", "BAGIS-PRO");
                return success;
            }

            var url = (string)Module1.Current.BatchToolSettings.EBagisServer + Constants.URI_DESKTOP_SETTINGS;
            var response = new EsriHttpClient().Get(url);
            var json = await response.Content.ReadAsStringAsync();
            dynamic oSettings = JObject.Parse(json);
            if (oSettings == null || String.IsNullOrEmpty(Convert.ToString(oSettings.snowCourseName)))
            {
                Module1.Current.ModuleLogManager.LogDebug(nameof(ClipSnoLayersAsync),
                    "Unable to retrieve snotel settings from " + url);
                MessageBox.Show("Unable to retrieve snotel layer settings. Clipping cancelled!!", "BAGIS-PRO");
                return success;
            }

            // Get the buffer layers
            if (bClipSnotel)
            {
                // if the buffer distance is null, we will use the AOI boundary to clip
                if (!String.IsNullOrEmpty(snotelBufferDistance))
                {
                    snotelClipLayer = await AnalysisTools.GetSnoClipLayer(strAoiPath, strClipGdb, strTempBuffer, snotelBufferDistance);
                    if (string.IsNullOrEmpty(snotelClipLayer))
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(ClipSnoLayersAsync),
                            "Unable to calculate snotel clip layer");
                        MessageBox.Show("Unable to generate SNOTEL clip layer. Clipping cancelled!!", "BAGIS-PRO");
                        return success;
                    }
                }
                else
                {
                    snotelClipLayer = Constants.FILE_AOI_VECTOR;
                }

                // Delete the existing snotel layer, snotel represented area, and snotel zones layers
                if (await GeodatabaseTools.FeatureClassExistsAsync(uriLayers, strFinalOutputLayer[0]))
                {
                    success = await GeoprocessingTools.DeleteDatasetAsync(strLayers + "\\" + strFinalOutputLayer[0]);
                }
                if (await GeodatabaseTools.FeatureClassExistsAsync(uriAnalysis, Constants.FILE_SNOTEL_REPRESENTED))
                {
                    success = await GeoprocessingTools.DeleteDatasetAsync(strAnalysis + "\\" + Constants.FILE_SNOTEL_REPRESENTED);
                }
                if (await GeodatabaseTools.RasterDatasetExistsAsync(uriAnalysis, Constants.FILE_SNOTEL_ZONE))
                {
                    success = await GeoprocessingTools.DeleteDatasetAsync(strAnalysis + "\\" + Constants.FILE_SNOTEL_ZONE);
                }

                string strWsUri = dictDataSources[Constants.DATA_TYPE_SNOTEL].uri;
                strOutputFc[0] = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Layers, true) + strOutputLayer[0];
                string strTemplateDataset = strClipGdb + "\\" + snotelClipLayer;
                var environmentsClip = Geoprocessing.MakeEnvironmentArray(workspace: strAoiPath);
                var parametersClip = Geoprocessing.MakeValueArray(strWsUri, strTemplateDataset, strOutputFc[0], "");
                var gpResultClip = await Geoprocessing.ExecuteToolAsync("Clip_analysis", parametersClip, environmentsClip,
                                        CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (gpResultClip.IsFailed)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(ClipSnoLayersAsync),
                       "Unable to clip " + snotelClipLayer + ". Error code: " + gpResultClip.ErrorCode);
                    MessageBox.Show("Unable to clip. Clipping cancelled!!", "BAGIS-PRO");
                    return success;
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(ClipSnoLayersAsync),
                        "Clipped " + snotelClipLayer + " layer");
                }
            }

            // Clip Snow Course Layer
            if (bClipSnowCos)
            {
                // if the buffer distance is null, we will use the AOI boundary to clip
                if (!String.IsNullOrEmpty(snowCosBufferDistance))
                {
                    // See if we can re-use the snotel clip layer
                    if (snowCosBufferDistance.Equals(snotelBufferDistance))
                    {
                        snowCosClipLayer = snotelClipLayer;
                    }
                    else
                    {
                        snowCosClipLayer = await AnalysisTools.GetSnoClipLayer(strAoiPath, strClipGdb, strTempBuffer, snowCosBufferDistance);
                        if (string.IsNullOrEmpty(snowCosClipLayer))
                        {
                            Module1.Current.ModuleLogManager.LogError(nameof(ClipSnoLayersAsync),
                                "Unable to calculate snow course clip layer");
                            MessageBox.Show("Unable to generate snow course clip layer. Clipping cancelled!!", "BAGIS-PRO");
                            return success;
                        }
                    }
                }
                else
                {
                    snowCosClipLayer = Constants.FILE_AOI_VECTOR;
                }

                // Delete the existing snow course layer, represented area, and snow course zones
                if (await GeodatabaseTools.FeatureClassExistsAsync(uriLayers, strFinalOutputLayer[1]))
                {
                    success = await GeoprocessingTools.DeleteDatasetAsync(strLayers + "\\" + strFinalOutputLayer[1]);
                }
                if (await GeodatabaseTools.FeatureClassExistsAsync(uriAnalysis, Constants.FILE_SCOS_REPRESENTED))
                {
                    success = await GeoprocessingTools.DeleteDatasetAsync(strAnalysis + "\\" + Constants.FILE_SCOS_REPRESENTED);
                }
                if (await GeodatabaseTools.RasterDatasetExistsAsync(uriAnalysis, Constants.FILE_SCOS_ZONE))
                {
                    success = await GeoprocessingTools.DeleteDatasetAsync(strAnalysis + "\\" + Constants.FILE_SCOS_ZONE);
                }

                string strWsUri = dictDataSources[Constants.DATA_TYPE_SNOW_COURSE].uri;
                strOutputFc[1] = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Layers, true) + strOutputLayer[1];
                string strTemplateDataset = strClipGdb + "\\" + snowCosClipLayer;
                var environmentsClip = Geoprocessing.MakeEnvironmentArray(workspace: strAoiPath);
                var parametersClip = Geoprocessing.MakeValueArray(strWsUri, strTemplateDataset, strOutputFc[1], "");
                var gpResultClip = await Geoprocessing.ExecuteToolAsync("Clip_analysis", parametersClip, environmentsClip,
                                        CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (gpResultClip.IsFailed)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(ClipSnoLayersAsync),
                       "Unable to clip " + snowCosClipLayer + ". Error code: " + gpResultClip.ErrorCode);
                    MessageBox.Show("Unable to clip. Clipping cancelled!!", "BAGIS-PRO");
                    return success;
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(ClipSnoLayersAsync),
                        "Clipped " + snowCosClipLayer + " layer");
                }
            }

            // Delete the temporary buffer layer; This does not error if the buffer doesn't exist
            success = await GeoprocessingTools.DeleteDatasetAsync(strClipGdb + "\\" + strTempBuffer);

            // Add attribute fields
            int snotelCount = 0;
            int snowCosCount = 0;
            int i = 0;
            foreach (var strFc in strOutputFc)
            {
                if (!String.IsNullOrEmpty(strFc))
                {
                    success = await GeoprocessingTools.AddFieldAsync(strFc, Constants.FIELD_SITE_NAME, "TEXT");
                    if (success == BA_ReturnCode.Success)
                    {
                        success = await GeoprocessingTools.AddFieldAsync(strFc, Constants.FIELD_SITE_ELEV, "DOUBLE");
                        if (success == BA_ReturnCode.Success)
                        {
                            success = await GeoprocessingTools.AddFieldAsync(strFc, Constants.FIELD_SITE_TYPE, "TEXT");
                        }
                    }
                    if (success != BA_ReturnCode.Success)
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(ClipSnoLayersAsync),
                             "Unable to add fields to " + strFc);
                        MessageBox.Show("Unable to add fields to " + strFc + ". Clipping cancelled!!", "BAGIS-PRO");
                        return success;
                    }
                }

                bool modificationResult = false;
                string errorMsg = "";
                await QueuedTask.Run(() =>
                {
                    // Copy site name into ba_sname field
                    Uri gdbUri = new Uri(GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Layers));
                    using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(gdbUri)))
                    {
                        if (!String.IsNullOrEmpty(strFc))
                        {
                            string sourceName = Convert.ToString(oSettings.snotelName);
                            if (i == 1)  // This is a snow course layer
                            {
                                sourceName = Convert.ToString(oSettings.snowCourseName);
                            }
                            using (Table table = geodatabase.OpenDataset<Table>(strOutputLayer[i]))
                            {
                                QueryFilter queryFilter = new QueryFilter();
                                EditOperation editOperation = new EditOperation();
                                editOperation.Callback(context =>
                                {
                                    using (RowCursor aCursor = table.Search(queryFilter, false))
                                    {
                                        while (aCursor.MoveNext())
                                        {
                                            using (Feature feature = (Feature)aCursor.Current)
                                            {
                                                // name
                                                int idxSource = feature.FindField(sourceName);
                                                int idxTarget = feature.FindField(Constants.FIELD_SITE_NAME);
                                                if (idxSource > -1 && idxTarget > -1)
                                                {
                                                    feature[idxTarget] = feature[idxSource];
                                                }
                                                string siteType = SiteType.Snotel.ToString();
                                                if (i == 1)
                                                {
                                                    siteType = SiteType.SnowCourse.ToString();
                                                }
                                                idxTarget = feature.FindField(Constants.FIELD_SITE_TYPE);
                                                if (idxTarget > -1)
                                                {
                                                    feature[idxTarget] = siteType;
                                                }
                                                feature.Store();
                                                // Has to be called after the store too
                                                context.Invalidate(feature);
                                            }
                                            if (i == 0)
                                            {
                                                snotelCount++;
                                            }
                                            else
                                            {
                                                snowCosCount++;
                                            }
                                        }
                                    }
                                }, table);
                                try
                                {
                                    modificationResult = editOperation.Execute();
                                    if (!modificationResult) errorMsg = editOperation.ErrorMessage;
                                    // increment feature counter
                                }
                                catch (GeodatabaseException exObj)
                                {
                                    errorMsg = exObj.Message;
                                }
                            }
                        }
                    }
                });

                if (String.IsNullOrEmpty(errorMsg))
                {
                    await Project.Current.SaveEditsAsync();
                }
                else
                {
                    if (Project.Current.HasEdits)
                        await Project.Current.DiscardEditsAsync();
                    Module1.Current.ModuleLogManager.LogError(nameof(ClipSnoLayersAsync),
                        "Exception: " + errorMsg);
                    return BA_ReturnCode.UnknownError;
                }
                i++;
            }

            // Extract the elevation from the DEM for the sites
            int j = 0;
            string finalOutputPath = "";
            foreach (var strFc in strOutputFc)
            {
                // Make sure the feature class has sites before trying to extract the elevation
                bool bValidFc = !String.IsNullOrEmpty(strFc);
                if (bValidFc)
                {
                    if (j == 0 && snotelCount == 0)
                    {
                        bValidFc = false;
                        Module1.Current.ModuleLogManager.LogDebug(nameof(ClipSnoLayersAsync),
                            "Snotel layer has no sites. Elevation cannot be extracted");
                    }
                    else if (j == 1 && snowCosCount == 0)
                    {
                        bValidFc = false;
                        Module1.Current.ModuleLogManager.LogDebug(nameof(ClipSnoLayersAsync),
                            "Snow course layer has no sites. Elevation cannot be extracted");
                    }
                }
                if (bValidFc)
                {
                    finalOutputPath = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Layers, true) + strFinalOutputLayer[j];
                    var environments = Geoprocessing.MakeEnvironmentArray(workspace: strAoiPath);
                    string demPath = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Surfaces, true) +
                        Constants.FILE_DEM_FILLED;
                    var parameters = Geoprocessing.MakeValueArray(strOutputFc[j], demPath, finalOutputPath, "NONE", "ALL");
                    var gpResult = await Geoprocessing.ExecuteToolAsync("ExtractValuesToPoints", parameters, environments,
                                       CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                    if (gpResult.IsFailed)
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(ClipSnoLayersAsync),
                            "Unable to extract values to points for site elevation. Error code: " + gpResult.ErrorCode);
                        MessageBox.Show("Unable extract site elevation. Clipping cancelled!!", "BAGIS-PRO");
                        return success;
                    }
                    else
                    {
                        Module1.Current.ModuleLogManager.LogDebug(nameof(ClipSnoLayersAsync),
                            "Extracted values to points for site elevations");
                        string strExpression = "!" + Constants.FIELD_RASTERVALU + "!";
                        parameters = Geoprocessing.MakeValueArray(finalOutputPath, Constants.FIELD_SITE_ELEV, strExpression);
                        gpResult = await Geoprocessing.ExecuteToolAsync("CalculateField_management", parameters, environments,
                           CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                        if (gpResult.IsFailed)
                        {
                            Module1.Current.ModuleLogManager.LogError(nameof(ClipSnoLayersAsync),
                                "Unable to copy elevation to BA_ELEV. Error code: " + gpResult.ErrorCode);
                            MessageBox.Show("Unable copy site elevation. Clipping cancelled!!", "BAGIS-PRO");
                            return success;
                        }
                        else
                        {
                            string[] arrDeleteFields = { Constants.FIELD_RASTERVALU };
                            success = await GeoprocessingTools.DeleteFeatureClassFieldsAsync(finalOutputPath, arrDeleteFields);
                            if (success != BA_ReturnCode.Success)
                            {
                                Module1.Current.ModuleLogManager.LogError(nameof(ClipSnoLayersAsync),
                                    "Unable to delete field " + Constants.FIELD_RASTERVALU + ". Error code: " + gpResult.ErrorCode);
                                MessageBox.Show("Unable to delete field " + Constants.FIELD_RASTERVALU + ". Clipping cancelled!!", "BAGIS-PRO");
                                return success;
                            }
                        }
                    }
                    // Save buffer distance and units in metadata
                    string[] arrPieces = snotelBufferDistance.Split(' ');
                    string strBufferDistance = "0";
                    string strBufferUnits = "Meters";
                    if (arrPieces.Length == 2)
                    {
                        strBufferDistance = arrPieces[0];
                        strBufferUnits = arrPieces[1];
                    }
                    // We need to add a new tag at "/metadata/dataIdInfo/searchKeys/keyword"
                    StringBuilder sb = new StringBuilder();
                    sb.Append(Constants.META_TAG_PREFIX);
                    // Buffer Distance
                    sb.Append(Constants.META_TAG_BUFFER_DISTANCE + strBufferDistance + "; ");
                    // X Units
                    sb.Append(Constants.META_TAG_XUNIT_VALUE + strBufferUnits + "; ");
                    sb.Append(Constants.META_TAG_SUFFIX);

                    //Update the metadata
                    var fc = ItemFactory.Instance.Create(finalOutputPath,
                        ItemFactory.ItemType.PathItem);
                    if (fc != null)
                    {
                        await QueuedTask.Run(() =>
                        {
                            string strXml = string.Empty;
                            strXml = fc.GetXml();
                            System.Xml.XmlDocument xmlDocument = GeneralTools.UpdateMetadata(strXml, Constants.META_TAG_XPATH, sb.ToString(),
                                Constants.META_TAG_PREFIX.Length);

                            fc.SetXml(xmlDocument.OuterXml);
                        });
                    }
                    // Update layer metadata
                    string strKey = Constants.DATA_TYPE_SNOTEL;
                    if (j == 1)
                    {
                        strKey = Constants.DATA_TYPE_SNOW_COURSE;
                    }
                    IDictionary<string, BA_Objects.DataSource> dictLocalDataSources = GeneralTools.QueryLocalDataSources();
                    BA_Objects.DataSource updateDataSource = new BA_Objects.DataSource(dictDataSources[strKey])
                    {
                        DateClipped = DateTime.Now,
                    };
                    if (dictLocalDataSources.ContainsKey(strKey))
                    {
                        dictLocalDataSources[strKey] = updateDataSource;
                    }
                    else
                    {
                        dictLocalDataSources.Add(strKey, updateDataSource);
                    }
                    success = GeneralTools.SaveDataSourcesToFile(dictLocalDataSources);
                    Module1.Current.ModuleLogManager.LogDebug(nameof(ClipSnoLayersAsync),
                        "Updated settings metadata for AOI");
                    // Delete the temporary layer
                    success = await GeoprocessingTools.DeleteDatasetAsync(strOutputFc[j]);
                }

                j++;
            }

            string returnPath = await AnalysisTools.CreateSitesLayerAsync(uriLayers);
            if (string.IsNullOrEmpty(returnPath))
            {
                success = BA_ReturnCode.UnknownError;
            }

            return success;
        }

        public static async Task<string> GetSnoClipLayer(string strAoiPath, string strClipGdb, string strTempBuffer,
            string strBufferDistance)
        {
            string strSnoClipLayer = "";
            string strLayerToDelete = "";
            string strAoiBoundaryPath = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Aoi, true) +
                Constants.FILE_AOI_BUFFERED_VECTOR;
            string strOutputFeatures = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Aoi, true) +
                strTempBuffer;
            var parametersBuff = Geoprocessing.MakeValueArray(strAoiBoundaryPath, strOutputFeatures, strBufferDistance, "",
                                                              "", "ALL");
            var gpResultBuff = await Geoprocessing.ExecuteToolAsync("Buffer_analysis", parametersBuff, null,
                                 CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            if (gpResultBuff.IsFailed)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(GetSnoClipLayer),
                   "Unable to buffer aoi_v. Error code: " + gpResultBuff.ErrorCode);
                MessageBox.Show("Unable to buffer aoi_v. Clipping cancelled!!", "BAGIS-PRO");
                return strSnoClipLayer;
            }

            // Check to make sure the buffer file only has one feature; No dangles
            int featureCount = 0;
            await QueuedTask.Run(async () =>
            {
                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strClipGdb))))
                using (Table table = geodatabase.OpenDataset<Table>(strTempBuffer))
                {
                    featureCount = table.GetCount();
                }
                Module1.Current.ModuleLogManager.LogDebug(nameof(GetSnoClipLayer),
                    "Number of features in clip file: " + featureCount);

                // If > 1 feature, buffer the clip file again
                if (featureCount > 1)
                {
                    string strTempBuffer2 = "tempBuffer2";
                    strLayerToDelete = strTempBuffer;
                    parametersBuff = Geoprocessing.MakeValueArray(strClipGdb + "\\" + strTempBuffer,
                        strClipGdb + "\\" + strTempBuffer2, "0.5 Meters", "", "", "ALL");
                    gpResultBuff = await Geoprocessing.ExecuteToolAsync("Buffer_analysis", parametersBuff, null,
                                     CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                    if (gpResultBuff.IsFailed)
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(ClipLayersAsync),
                           "Unable to buffer " + strTempBuffer + ". Error code: " + gpResultBuff.ErrorCode);
                        MessageBox.Show("Unable to temporary clip file. Clipping cancelled!!", "BAGIS-PRO");
                        return;
                    }
                    Module1.Current.ModuleLogManager.LogDebug(nameof(ClipLayersAsync),
                        "Ran buffer tool again because clip file has > 2 features");
                    BA_ReturnCode success = await GeoprocessingTools.DeleteDatasetAsync(strClipGdb + "\\" + strTempBuffer);
                    strSnoClipLayer = strTempBuffer2;
                }
                else
                {
                    strSnoClipLayer = strTempBuffer;
                }
            });
            return strSnoClipLayer;
        }

        public static async Task<BA_ReturnCode> ClipFeatureLayerAsync(string strAoiPath, string strOutputFc,
            string strDataType, string strBufferDistance, string strBufferUnits)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;

            Webservices ws = new Webservices();
            Module1.Current.ModuleLogManager.LogDebug(nameof(ClipFeatureLayerAsync),
                "Contacting webservices server to retrieve layer metadata");
            IDictionary<string, dynamic> dictDataSources =
                await ws.QueryDataSourcesAsync((string)Module1.Current.BatchToolSettings.EBagisServer);
            string strWsUri = dictDataSources[strDataType].uri;

            string[] arrLayersToDelete = new string[2];
            string strClipGdb = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Aoi, false);
            string strClipFile = Constants.FILE_AOI_BUFFERED_VECTOR;

            if (!String.IsNullOrEmpty(strWsUri))
            {
                await QueuedTask.Run(async () =>
                {
                    try
                    {
                        string strTempBuffer = "tmpBuffer";
                        // a buffer distance was requested
                        string strTempBuffer2 = "";
                        if (!String.IsNullOrEmpty(strBufferDistance))
                        {
                            string strAoiBoundaryPath = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Aoi, true) +
                                Constants.FILE_AOI_VECTOR;
                            string strOutputFeatures = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Aoi, true) +
                                strTempBuffer;
                            string strDistance = strBufferDistance + " " + strBufferUnits;
                            var parameters = Geoprocessing.MakeValueArray(strAoiBoundaryPath, strOutputFeatures, strDistance, "",
                                                                              "", "ALL");
                            var gpResult = Geoprocessing.ExecuteToolAsync("Buffer_analysis", parameters, null,
                                                 CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                            if (gpResult.Result.IsFailed)
                            {
                                Module1.Current.ModuleLogManager.LogError(nameof(ClipFeatureLayerAsync),
                                   "Unable to buffer aoi_v. Error code: " + gpResult.Result.ErrorCode);
                                MessageBox.Show("Unable to buffer aoi_v. Clipping cancelled!!", "BAGIS-PRO");
                                return;
                            }

                            strClipFile = strTempBuffer;
                            arrLayersToDelete[0] = strTempBuffer;
                        }

                        // Check to make sure the buffer file only has one feature; No dangles
                        int featureCount = 0;
                        strClipGdb = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Aoi, false);
                        using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strClipGdb))))
                        using (Table table = geodatabase.OpenDataset<Table>(strClipFile))
                        {
                            featureCount = table.GetCount();
                        }
                        Module1.Current.ModuleLogManager.LogDebug(nameof(ClipFeatureLayerAsync),
                            "Number of features in clip file: " + featureCount);

                        // If > 1 feature, buffer the clip file again
                        if (featureCount > 1)
                        {
                            strTempBuffer2 = "tempBuffer2";
                            var parameters = Geoprocessing.MakeValueArray(strClipGdb + "\\" + strClipFile,
                                strClipGdb + "\\" + strTempBuffer2, "0.5 Meters", "", "", "ALL");
                            var gpResult = Geoprocessing.ExecuteToolAsync("Buffer_analysis", parameters, null,
                                                 CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                            if (gpResult.Result.IsFailed)
                            {
                                Module1.Current.ModuleLogManager.LogError(nameof(ClipFeatureLayerAsync),
                                   "Unable to buffer " + strClipFile + ". Error code: " + gpResult.Result.ErrorCode);
                                MessageBox.Show("Unable to buffer aoi_v. Clipping cancelled!!", "BAGIS-PRO");
                                return;
                            }
                            strClipFile = strTempBuffer2;
                            arrLayersToDelete[1] = strTempBuffer;
                            Module1.Current.ModuleLogManager.LogDebug(nameof(ClipFeatureLayerAsync),
                                "Run buffer tool again because clip file has > 2 features");
                        }

                        Uri featureServiceUri = new Uri(strWsUri);
                        string strTemplateDataset = strClipGdb + "\\" + strClipFile;
                        var environmentsClip = Geoprocessing.MakeEnvironmentArray(workspace: strAoiPath);
                        var parametersClip = Geoprocessing.MakeValueArray(strWsUri, strTemplateDataset, strOutputFc, "");
                        var gpResultClip = await Geoprocessing.ExecuteToolAsync("Clip_analysis", parametersClip, environmentsClip,
                                                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                        if (gpResultClip.IsFailed)
                        {
                            Module1.Current.ModuleLogManager.LogError(nameof(ClipFeatureLayerAsync),
                               "Unable to clip " + strWsUri + ". Error code: " + gpResultClip.ErrorCode);
                            Module1.Current.ModuleLogManager.LogError(nameof(ClipFeatureLayerAsync),
                                "GP Messages: " + gpResultClip.Messages);
                            MessageBox.Show("Unable to clip. Clipping cancelled!!", "BAGIS-PRO");
                            return;
                        }
                        else
                        {
                            Module1.Current.ModuleLogManager.LogDebug(nameof(ClipFeatureLayerAsync),
                                "Clipped " + strOutputFc + " layer");
                        }

                        if (!String.IsNullOrEmpty(strBufferDistance))
                        {
                            //Update the metadata if there is a custom buffer
                            //We need to add a new tag at "/metadata/dataIdInfo/searchKeys/keyword"
                            StringBuilder sb = new StringBuilder();
                            sb.Append(Constants.META_TAG_PREFIX);
                            // Z Units
                            string strUnits = dictDataSources[strDataType].units;
                            sb.Append(Constants.META_TAG_ZUNIT_CATEGORY + Constants.META_TAG_CATEGORY_DEPTH + "; ");
                            sb.Append(Constants.META_TAG_ZUNIT_VALUE + strUnits + "; ");
                            // Buffer Distance
                            sb.Append(Constants.META_TAG_BUFFER_DISTANCE + strBufferDistance + "; ");
                            // X Units
                            sb.Append(Constants.META_TAG_XUNIT_VALUE + strBufferUnits + "; ");
                            sb.Append(Constants.META_TAG_SUFFIX);

                            //Update the metadata
                            var fc = ItemFactory.Instance.Create(strOutputFc,
                                ItemFactory.ItemType.PathItem);
                            if (fc != null)
                            {
                                string strXml = string.Empty;
                                strXml = fc.GetXml();
                                System.Xml.XmlDocument xmlDocument = GeneralTools.UpdateMetadata(strXml, Constants.META_TAG_XPATH, sb.ToString(),
                                    Constants.META_TAG_PREFIX.Length);

                                fc.SetXml(xmlDocument.OuterXml);
                            }
                        }

                        // Delete temporary layers
                        for (int j = 0; j < arrLayersToDelete.Length; j++)
                        {
                            if (!string.IsNullOrEmpty(arrLayersToDelete[j]))
                            {
                                var parameters = Geoprocessing.MakeValueArray(strClipGdb + "\\" + arrLayersToDelete[j]);
                                var gpResult = Geoprocessing.ExecuteToolAsync("Delete_management", parameters, null,
                                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                                if (gpResult.Result.IsFailed)
                                {
                                    Module1.Current.ModuleLogManager.LogError(nameof(ClipFeatureLayerAsync),
                                        "Unable to delete " + strClipGdb + "\\" + arrLayersToDelete[j] + ". Error code: " + gpResult.Result.ErrorCode);
                                    MessageBox.Show("Unable to delete " + strClipGdb + "\\" + arrLayersToDelete[j] + ".", "BAGIS-PRO");
                                }
                                else
                                {
                                    Module1.Current.ModuleLogManager.LogDebug(nameof(ClipFeatureLayerAsync),
                                        "Successfully deleted temp file: " + strClipGdb + "\\" + arrLayersToDelete[j]);
                                }
                            }
                        }

                        // Update layer metadata
                        IDictionary<string, BA_Objects.DataSource> dictLocalDataSources = GeneralTools.QueryLocalDataSources();
                        BA_Objects.DataSource updateDataSource = new BA_Objects.DataSource(dictDataSources[strDataType])
                        {
                            DateClipped = DateTime.Now,
                        };
                        if (dictLocalDataSources.ContainsKey(strDataType))
                        {
                            dictLocalDataSources[strDataType] = updateDataSource;
                        }
                        else
                        {
                            dictLocalDataSources.Add(strDataType, updateDataSource);
                        }
                        success = GeneralTools.SaveDataSourcesToFile(dictLocalDataSources);
                        Module1.Current.ModuleLogManager.LogDebug(nameof(ClipFeatureLayerAsync),
                            "Updated settings metadata for " + strDataType);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(e.StackTrace);
                    }
                });
            }

            return success;
        }

        public static async Task<BA_ReturnCode> GetFederalNonWildernessLandsAsync(string strAoiPath)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            Uri uriLayers = new Uri(GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Layers));
            bool bExists = await GeodatabaseTools.FeatureClassExistsAsync(uriLayers, Constants.FILE_PUBLIC_LAND);
            if (!bExists)
            {
                MessageBox.Show("The public land layer is missing. Clip the public land layer before creating the public land analysis layer!!", "BAGIS-PRO");
                Module1.Current.ModuleLogManager.LogDebug(nameof(GetFederalNonWildernessLandsAsync),
                    "Unable to extract public lands because public_lands layer does not exist. Process stopped!!");
                return success;
            }
            string strInputFc = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Layers, true) + Constants.FILE_PUBLIC_LAND;
            Uri uriFull = new Uri(strInputFc);
            // Check for attribute before trying to run query
            bExists = await GeodatabaseTools.AttributeExistsAsync(uriLayers, Constants.FILE_PUBLIC_LAND, Constants.FIELD_SUITABLE_PUBLIC);
            if (bExists)
            {
                await QueuedTask.Run(() =>
                {
                    // Create feature layer so we can use definition query to select public lands
                    var slectionLayer = LayerFactory.Instance.CreateFeatureLayer(uriFull, MapView.Active.Map, 0, "Selection Layer");
                    slectionLayer.SetDefinitionQuery(Constants.FIELD_SUITABLE_PUBLIC + " = 1");
                    // Merge features into a single feature for display and analysis
                    var environments = Geoprocessing.MakeEnvironmentArray(workspace: strAoiPath);
                    var parameters = Geoprocessing.MakeValueArray(slectionLayer, GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Analysis, true) + Constants.FILE_PUBLIC_LAND_ZONE,
                        Constants.FIELD_SUITABLE_PUBLIC, "", "MULTI_PART", "DISSOLVE_LINES");
                    var gpResult = Geoprocessing.ExecuteToolAsync("Dissolve_management", parameters, environments,
                        CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                    if (gpResult.Result.IsFailed)
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(GetFederalNonWildernessLandsAsync),
                            "Unable to dissolve public lands temp feature class. Error code: " + gpResult.Result.ErrorCode);
                        success = BA_ReturnCode.UnknownError;
                    }
                    else
                    {
                        success = BA_ReturnCode.Success;
                    }
                    // Remove temporary layer
                    MapView.Active.Map.RemoveLayer(slectionLayer);
                    Module1.Current.ModuleLogManager.LogDebug(nameof(GetFederalNonWildernessLandsAsync),
                        "Dissolved public lands layer");
                });
            }
            else
            {
                Module1.Current.ModuleLogManager.LogError(nameof(GetFederalNonWildernessLandsAsync),
                    Constants.FIELD_SUITABLE_PUBLIC + " missing from " + Constants.FILE_PUBLIC_LAND + 
                    " . Federal non-wilderness lands layer cannot be created!");
                success = BA_ReturnCode.ReadError;
            }
            return success;
        }

        public static async Task<BA_ReturnCode> ExtractBelowTreelineAsync(string strAoiPath)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            Uri uriLayers = new Uri(GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Layers));
            bool bExists = await GeodatabaseTools.RasterDatasetExistsAsync(uriLayers, Constants.FILE_VEGETATION_EVT);
            if (!bExists)
            {
                MessageBox.Show("The vegetation layer is missing. Clip the vegetation layer before generating the area below treeline analysis layer!!", "BAGIS-PRO");
                Module1.Current.ModuleLogManager.LogDebug(nameof(GetFederalNonWildernessLandsAsync),
                    "Unable to extract below treeline because vegetation layer does not exist. Process stopped!!");
                return success;
            }
            string strInputRaster = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Layers, true) + Constants.FILE_VEGETATION_EVT;
            Uri uriFull = new Uri(strInputRaster);
            await QueuedTask.Run(() =>
            {
                // Convert vegetation raster to vector using ALPINE_ABV_TREELINE since that's the only value we care about
                var environments = Geoprocessing.MakeEnvironmentArray(workspace: strAoiPath);
                string strAnalysisGdb = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Analysis);
                string strTempVector = "temp_veg";
                var parameters = Geoprocessing.MakeValueArray(strInputRaster, strAnalysisGdb + "\\" + strTempVector,
                    "SIMPLIFY", Constants.FIELD_ALPINE_ABV_TREELINE);
                var gpResult = Geoprocessing.ExecuteToolAsync("RasterToPolygon_conversion", parameters, environments,
                                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (gpResult.Result.IsFailed)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(ExtractBelowTreelineAsync),
                    "Unable to convert vegetation raster to polygon. Error code: " + gpResult.Result.ErrorCode);
                    MessageBox.Show("Unable to convert vegetation raster to polygon. Process cancelled!!", "BAGIS-PRO");
                    return;
                }

                // Create feature layer so we can use definition query to select public lands
                var uriTemp = new Uri(strAnalysisGdb + "\\" + strTempVector);
                var slectionLayer = LayerFactory.Instance.CreateFeatureLayer(uriTemp, MapView.Active.Map, 0, "Selection Layer");
                slectionLayer.SetDefinitionQuery(Constants.FIELD_GRID_CODE + " <> 1");
                string dissolveOutputPath = strAnalysisGdb + "\\" + Constants.FILE_BELOW_TREELINE_ZONE;
                // Copy selected features to a new, temporary feature class
                environments = Geoprocessing.MakeEnvironmentArray(workspace: strAoiPath);
                parameters = Geoprocessing.MakeValueArray(slectionLayer, dissolveOutputPath, Constants.FIELD_GRID_CODE);
                gpResult = Geoprocessing.ExecuteToolAsync("Dissolve_management", parameters, environments,
                                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);

                if (gpResult.Result.IsFailed)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(ExtractBelowTreelineAsync),
                   "Unable to dissolve selected features. Error code: " + gpResult.Result.ErrorCode);
                }
                // Remove temporary layer
                MapView.Active.Map.RemoveLayer(slectionLayer);
                // Delete temporary dataset
                parameters = Geoprocessing.MakeValueArray(uriTemp.LocalPath);
                gpResult = Geoprocessing.ExecuteToolAsync("Delete_management", parameters, environments,
                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (gpResult.Result.IsFailed)
                {
                    MessageBox.Show("Unable to delete temporary vegetation layer. Process cancelled!!", "BAGIS-PRO");
                    Module1.Current.ModuleLogManager.LogError(nameof(ExtractBelowTreelineAsync),
                        "Failed to delete temporary vegetation layer");
                    return;
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(ExtractBelowTreelineAsync),
                        "Deleted temporary vegetation layer");
                }
            });
            success = BA_ReturnCode.Success;
            return success;
        }

        public static async Task<BA_ReturnCode> ExtractCriticalPrecipitationZonesAsync(string strAoiPath, IList<string> lstCriticalZoneValues)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            Uri uriAnalysisGdb = new Uri(GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Analysis));
            // Make sure the elevation zones vector exists
            if (! await GeodatabaseTools.FeatureClassExistsAsync(uriAnalysisGdb, Constants.FILE_ELEV_ZONES_VECTOR))
            {
                MessageBox.Show("The elevation zones vector is missing. Generate the elevation zones layer and try again!!", "BAGIS-PRO");
                Module1.Current.ModuleLogManager.LogDebug(nameof(GetFederalNonWildernessLandsAsync),
                    "Unable to extract critical precipitation zones because elevation zones vector does not exist. Process stopped!!");
                return success;
            }
            await QueuedTask.Run(() =>
            {
                // Create feature layer so we can use definition query to select public lands
                var uriTemp = new Uri(uriAnalysisGdb.LocalPath + "\\" + Constants.FILE_ELEV_ZONES_VECTOR);
                var slectionLayer = LayerFactory.Instance.CreateFeatureLayer(uriTemp, MapView.Active.Map, 0, "Selection Layer");
                string strZones = "";
                foreach (var sZone in lstCriticalZoneValues)
                {
                    strZones = strZones + sZone + ", ";
                }
                // Trim off trailing characters
                strZones = strZones.Substring(0, strZones.Length - 2);
                slectionLayer.SetDefinitionQuery(Constants.FIELD_GRID_CODE + " IN (" + strZones + ")");
                string dissolveOutputPath = uriAnalysisGdb.LocalPath + "\\" + Constants.FILE_CRITICAL_PRECIP_ZONE;
                // Copy selected features to a new, temporary feature class
                var environments = Geoprocessing.MakeEnvironmentArray(workspace: strAoiPath);
                var parameters = Geoprocessing.MakeValueArray(slectionLayer, dissolveOutputPath, Constants.FIELD_GRID_CODE);
                var gpResult = Geoprocessing.ExecuteToolAsync("Dissolve_management", parameters, environments,
                                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);

                if (gpResult.Result.IsFailed)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(ExtractCriticalPrecipitationZonesAsync),
                   "Unable to dissolve selected features. Error code: " + gpResult.Result.ErrorCode);
                }
                // Remove temporary layer
                MapView.Active.Map.RemoveLayer(slectionLayer);
            });
            success = BA_ReturnCode.Success;
            return success;
        }

        public static async Task<BA_ReturnCode> CalculateElevPrecipCorrAsync(string strAoiPath, Uri uriPrism, string prismFile)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            int intAspectCount = Convert.ToInt16(Module1.Current.BatchToolSettings.AspectDirectionsCount);
            IList<BA_Objects.Interval> lstAspectInterval = AnalysisTools.GetAspectClasses(intAspectCount);

            // Create the elevation-precipitation layer
            Uri uriSurfaces = new Uri(GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Surfaces));
            Uri uriAnalysis = new Uri(GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Analysis));
            Uri uriLayers = new Uri(GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Layers));
            double dblDemCellSize = await GeodatabaseTools.GetCellSizeAsync(uriSurfaces, Constants.FILE_DEM_FILLED);
            double dblPrismCellSize = await GeodatabaseTools.GetCellSizeAsync(uriPrism, prismFile);
            string demPath = uriSurfaces.LocalPath + "\\" + Constants.FILE_DEM_FILLED;
            string precipMeanPath = uriAnalysis.LocalPath + "\\" + Constants.FILE_PREC_MEAN_ELEV;
            int intCellFactor = (int)Math.Round(dblPrismCellSize / dblDemCellSize, 0);
            double cellSize = dblPrismCellSize / intCellFactor;

            try
            {
            // Run aggregate tool
            var environments = Geoprocessing.MakeEnvironmentArray(workspace: strAoiPath,
                snapRaster: uriPrism.LocalPath + "\\" + prismFile, cellSize: cellSize);
            var parameters = Geoprocessing.MakeValueArray(demPath, precipMeanPath, intCellFactor, "MEAN");
            var gpResult = await Geoprocessing.ExecuteToolAsync("Aggregate", parameters, environments,
                                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            if (gpResult.IsFailed)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(CalculateElevPrecipCorrAsync),
                "Unable aggregate DEM. Error code: " + gpResult.ErrorCode);
                MessageBox.Show("Unable aggregate DEM. Calculation cancelled!!", "BAGIS-PRO");
                return success;
            }
            else
            {
                // Remove temporary layer
                Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateElevPrecipCorrAsync),
                    "Aggregate tool run successfully on DEM");
                success = BA_ReturnCode.Success;
            }

            if (success == BA_ReturnCode.Success)
            {
                double dblAspectCellSize = await GeodatabaseTools.GetCellSizeAsync(uriAnalysis, Constants.FILE_ASPECT_ZONE);
                string aspectZonesPath = uriAnalysis.LocalPath + "\\" + Constants.FILE_ASPECT_ZONE;
                if (dblPrismCellSize != dblAspectCellSize)
                {
                    // Execute focal statistics to account for differing cell sizes
                    //"Rectangle 935.365128254473 935.365128254473 MAP"
                    string aspectPath = uriAnalysis.LocalPath + "\\" + Constants.FILE_ASPECT_ZONE;
                    aspectZonesPath = uriAnalysis.LocalPath + "\\" + Constants.FILE_ASP_ZONE_PREC;
                    string neighborhood = "Rectangle " + dblPrismCellSize + " " + dblPrismCellSize + " MAP";
                    parameters = Geoprocessing.MakeValueArray(aspectPath, aspectZonesPath, neighborhood, "MAJORITY", "DATA");
                    environments = Geoprocessing.MakeEnvironmentArray(workspace: strAoiPath,
                        snapRaster: uriPrism.LocalPath + "\\" + prismFile, cellSize: dblPrismCellSize);
                    gpResult = await Geoprocessing.ExecuteToolAsync("FocalStatistics_sa", parameters, environments,
                        CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                    if (gpResult.IsFailed)
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(CalculateElevPrecipCorrAsync),
                        "Focal statistics failed for aspzones layer. Error code: " + gpResult.ErrorCode);
                        MessageBox.Show("Focal statistics failed for aspzones layer. Calculation cancelled!!", "BAGIS-PRO");
                        success = BA_ReturnCode.UnknownError;
                        return success;
                    }
                    else
                    {
                        Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateElevPrecipCorrAsync),
                            "Focal statistics for aspzones layer run successfully");
                    }
                }

                //Run Sample tool to extract elevation/precipitation for PRISM cell locations; The output is a table
                StringBuilder sb = new StringBuilder();
                sb.Append(aspectZonesPath + "; ");    // aspzone or aspzoneprec
                sb.Append(uriPrism.LocalPath + "\\" + prismFile + "; ");
                sb.Append(precipMeanPath);
                //Environment settings are same as Focal Statistics
                parameters = Geoprocessing.MakeValueArray(sb.ToString(), uriPrism.LocalPath + "\\" + prismFile,
                    uriAnalysis.LocalPath + "\\" + Constants.FILE_ASP_ZONE_PREC_TBL, "NEAREST");
                gpResult = await Geoprocessing.ExecuteToolAsync("Sample_sa", parameters, environments,
                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (gpResult.IsFailed)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(CalculateElevPrecipCorrAsync),
                        "Sample tool failed to create precmeanelev_tbl. Error code: " + gpResult.ErrorCode);
                    MessageBox.Show("Sample tool failed to run. Calculation cancelled!!", "BAGIS-PRO");
                    success = BA_ReturnCode.UnknownError;
                    return success;
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateElevPrecipCorrAsync),
                        "Sample tool run successfully");
                }

                success = await GeoprocessingTools.AddFieldAsync(uriAnalysis.LocalPath + "\\" + Constants.FILE_ASP_ZONE_PREC_TBL, Constants.FIELD_DIRECTION, "TEXT");
                if (success == BA_ReturnCode.Success)
                {
                    success = await UpdateAspectDirectionsAsync(uriAnalysis, Constants.FILE_ASP_ZONE_PREC_TBL,
                        lstAspectInterval, Constants.FILE_ASP_ZONE_PREC + "_Band_1");
                    if (success != BA_ReturnCode.Success)
                    {
                        MessageBox.Show("Unable to update aspect directions in " + Constants.FILE_ASP_ZONE_PREC_TBL
                            + ". Calculation cancelled!!", "BAGIS-PRO");
                        return success;
                    }
                }
            }

            }
            catch (Exception e)
            {
                MessageBox.Show(e.StackTrace);
            }
            return success;
        }

        private static async Task<BA_ReturnCode> UpdateAspectDirectionsAsync(Uri uriAnalysis, string strUpdateFc,
            IList<BA_Objects.Interval> lstAspectInterval, string strQueryFieldName)
        {
            string errorMsg = "";
            bool modificationResult = false;
            await QueuedTask.Run(() =>
            {
                Module1.Current.ModuleLogManager.LogDebug(nameof(UpdateAspectDirectionsAsync),
                    "Start UpdateAspectDirectionsAsync");

                // Copy aspect value into ba_aspect field
                EditOperation editOperation = new EditOperation();
                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(uriAnalysis)))
                using (Table table = geodatabase.OpenDataset<Table>(strUpdateFc))
                {
                    TableDefinition defTable = table.GetDefinition();
                    int idxAspect = defTable.FindField(Constants.FIELD_DIRECTION);
                    if (idxAspect < 0)
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(UpdateAspectDirectionsAsync),
                            "Unable to locate BA_ASPECT field in " + strUpdateFc + ". Cannot update field");
                        MessageBox.Show("Unable to locate BA_ASPECT field in " + strUpdateFc + ". Calculation cancelled!!", "BAGIS-PRO");
                        return;
                    }
                    QueryFilter queryFilter = new QueryFilter();
                    editOperation.Callback(context =>
                    {
                        for (int i = 0; i < lstAspectInterval.Count - 1; i++)
                        {
                            queryFilter.WhereClause = strQueryFieldName + " = " + lstAspectInterval[i].Value;
                            using (RowCursor aCursor = table.Search(queryFilter, false))
                            {
                                while (aCursor.MoveNext())
                                {
                                    using (Row row = aCursor.Current)
                                    {
                                        row[idxAspect] = lstAspectInterval[i].Name;
                                        row.Store();
                                        // Has to be called after the store too
                                        context.Invalidate(row);
                                    }
                                }
                            }
                        }
                    }, table);
                    try
                    {
                        modificationResult = editOperation.Execute();
                        if (!modificationResult) errorMsg = editOperation.ErrorMessage;
                        Module1.Current.ModuleLogManager.LogDebug(nameof(UpdateAspectDirectionsAsync),
                            "Save modification result for Aspect Directions");
                    }
                    catch (GeodatabaseException exObj)
                    {
                        errorMsg = exObj.Message;
                        Module1.Current.ModuleLogManager.LogError(nameof(UpdateAspectDirectionsAsync),
                            "An error occurred while trying to update aspect directions");
                        Module1.Current.ModuleLogManager.LogError(nameof(UpdateAspectDirectionsAsync),
                            exObj.StackTrace);
                    }
                }
            });

            if (String.IsNullOrEmpty(errorMsg))
            {
                await Project.Current.SaveEditsAsync();
                Module1.Current.ModuleLogManager.LogDebug(nameof(UpdateAspectDirectionsAsync),
                    "Edits saved to project");
            }
            else
            {
                if (Project.Current.HasEdits)
                    await Project.Current.DiscardEditsAsync();
                Module1.Current.ModuleLogManager.LogError(nameof(UpdateAspectDirectionsAsync),
                    "Exception: " + errorMsg);
                return BA_ReturnCode.UnknownError;
            }
            return BA_ReturnCode.Success;
        }

        public static async Task<BA_ReturnCode> ClipRasterLayerAsync(string strAoiPath, string strOutputRaster,
            string strDataType, string strBufferDistance, string strBufferUnits)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;

            Webservices ws = new Webservices();
            Module1.Current.ModuleLogManager.LogDebug(nameof(ClipLayersAsync),
                "Contacting webservices server to retrieve layer metadata");
            IDictionary<string, dynamic> dictDataSources =
                await ws.QueryDataSourcesAsync((string)Module1.Current.BatchToolSettings.EBagisServer);
            string strWsUri = dictDataSources[strDataType].uri;
            string[] arrLayersToDelete = new string[2];
            string strClipGdb = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Aoi, false);
            string strClipFile = Constants.FILE_AOI_BUFFERED_VECTOR;
            IReadOnlyList<string> parameters = null;
            Task<IGPResult> gpResult = null;

            if (!String.IsNullOrEmpty(strWsUri))
            {
                await QueuedTask.Run(async () =>
                {
                    string strTempBuffer = "tmpBuffer";
                    // a buffer distance was requested
                    string strTempBuffer2 = "";
                    if (!String.IsNullOrEmpty(strBufferDistance))
                    {
                        string strAoiBoundaryPath = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Aoi, true) +
                            Constants.FILE_AOI_VECTOR;
                        string strOutputFeatures = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Aoi, true) +
                            strTempBuffer;
                        string strDistance = strBufferDistance + " " + strBufferUnits;
                        parameters = Geoprocessing.MakeValueArray(strAoiBoundaryPath, strOutputFeatures, strDistance, "",
                                                                          "", "ALL");
                        gpResult = Geoprocessing.ExecuteToolAsync("Buffer_analysis", parameters, null,
                                             CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                        if (gpResult.Result.IsFailed)
                        {
                            Module1.Current.ModuleLogManager.LogError(nameof(ClipRasterLayerAsync),
                               "Unable to buffer aoi_v. Error code: " + gpResult.Result.ErrorCode);
                            MessageBox.Show("Unable to buffer aoi_v. Clipping cancelled!!", "BAGIS-PRO");
                            return;
                        }

                        strClipFile = strTempBuffer;
                        arrLayersToDelete[0] = strTempBuffer;
                    }

                    // Check to make sure the buffer file only has one feature; No dangles
                    int featureCount = 0;
                    strClipGdb = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Aoi, false);
                    using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strClipGdb))))
                    using (Table table = geodatabase.OpenDataset<Table>(strClipFile))
                    {
                        featureCount = table.GetCount();
                    }
                    Module1.Current.ModuleLogManager.LogDebug(nameof(ClipRasterLayerAsync),
                        "Number of features in clip file: " + featureCount);

                    // If > 1 feature, buffer the clip file again
                    if (featureCount > 1)
                    {
                        strTempBuffer2 = "tempBuffer2";
                        parameters = Geoprocessing.MakeValueArray(strClipGdb + "\\" + strClipFile,
                            strClipGdb + "\\" + strTempBuffer2, "0.5 Meters", "", "", "ALL");
                        gpResult = Geoprocessing.ExecuteToolAsync("Buffer_analysis", parameters, null,
                                             CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                        if (gpResult.Result.IsFailed)
                        {
                            Module1.Current.ModuleLogManager.LogError(nameof(ClipRasterLayerAsync),
                               "Unable to buffer " + strClipFile + ". Error code: " + gpResult.Result.ErrorCode);
                            MessageBox.Show("Unable to buffer aoi_v. Clipping cancelled!!", "BAGIS-PRO");
                            return;
                        }
                        strClipFile = strTempBuffer2;
                        arrLayersToDelete[1] = strTempBuffer;
                        Module1.Current.ModuleLogManager.LogDebug(nameof(ClipRasterLayerAsync),
                            "Run buffer tool again because clip file has > 2 features");
                    }

                    // Query the extent for the clip
                    string strClipEnvelope = "";
                    using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strClipGdb))))
                    using (Table table = geodatabase.OpenDataset<Table>(strClipFile))
                    {
                        QueryFilter queryFilter = new QueryFilter();
                        using (RowCursor cursor = table.Search(queryFilter, false))
                        {
                            while (cursor.MoveNext())
                            {
                                using (Feature feature = (Feature)cursor.Current)
                                {
                                    Geometry aoiGeo = feature.GetShape();
                                    strClipEnvelope = aoiGeo.Extent.XMin + " " + aoiGeo.Extent.YMin + " " + aoiGeo.Extent.XMax + " " + aoiGeo.Extent.YMax;
                                }
                            }
                        }
                    }
                    if (String.IsNullOrEmpty(strClipEnvelope))
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(ClipRasterLayerAsync),
                            "Unable obtain clipping envelope from " + strClipGdb + "\\" + strClipFile);
                        MessageBox.Show("Unable obtain clipping envelope from " + strClipGdb + "\\" + strClipFile + " Clipping cancelled!!", "BAGIS-PRO");
                        return;
                    }

                    Uri imageServiceUri = new Uri(strWsUri);
                    string strTemplateDataset = strClipGdb + "\\" + strClipFile;
                    var environments = Geoprocessing.MakeEnvironmentArray(workspace: strAoiPath, snapRaster: BA_Objects.Aoi.SnapRasterPath(strAoiPath));
                    parameters = Geoprocessing.MakeValueArray(imageServiceUri.AbsoluteUri, strClipEnvelope, strOutputRaster, strTemplateDataset,
                                        "", "ClippingGeometry");
                    var finalResult = await Geoprocessing.ExecuteToolAsync("Clip_management", parameters, environments,
                                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                    if (finalResult.IsFailed)
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(ClipRasterLayerAsync),
                           "Unable to clip " + strClipFile + ". Error code: " + finalResult.ErrorCode);
                        MessageBox.Show("Unable to clip. Clipping cancelled!!", "BAGIS-PRO");
                        return;
                    }
                    else
                    {
                        Module1.Current.ModuleLogManager.LogDebug(nameof(ClipRasterLayerAsync),
                            "Clipped " + strOutputRaster + " layer");
                    }

                    if (!String.IsNullOrEmpty(strBufferDistance))
                    {
                        //Update the metadata if there is a custom buffer
                        //We need to add a new tag at "/metadata/dataIdInfo/searchKeys/keyword"
                        StringBuilder sb = new StringBuilder();
                        sb.Append(Constants.META_TAG_PREFIX);
                        // Z Units
                        string strUnits = dictDataSources[strDataType].units;
                        sb.Append(Constants.META_TAG_ZUNIT_CATEGORY + Constants.META_TAG_CATEGORY_DEPTH + "; ");
                        sb.Append(Constants.META_TAG_ZUNIT_VALUE + strUnits + "; ");
                        // Buffer Distance
                        sb.Append(Constants.META_TAG_BUFFER_DISTANCE + strBufferDistance + "; ");
                        // X Units
                        sb.Append(Constants.META_TAG_XUNIT_VALUE + strBufferUnits + "; ");
                        sb.Append(Constants.META_TAG_SUFFIX);

                        //Update the metadata
                        var fc = ItemFactory.Instance.Create(strOutputRaster,
                            ItemFactory.ItemType.PathItem);
                        if (fc != null)
                        {
                            string strXml = string.Empty;
                            strXml = fc.GetXml();
                            System.Xml.XmlDocument xmlDocument = GeneralTools.UpdateMetadata(strXml, Constants.META_TAG_XPATH, sb.ToString(),
                                Constants.META_TAG_PREFIX.Length);

                            fc.SetXml(xmlDocument.OuterXml);
                        }
                    }

                    // Delete temporary layers
                    for (int j = 0; j < arrLayersToDelete.Length; j++)
                    {
                        if (!string.IsNullOrEmpty(arrLayersToDelete[j]))
                        {
                            parameters = Geoprocessing.MakeValueArray(strClipGdb + "\\" + arrLayersToDelete[j]);
                            gpResult = Geoprocessing.ExecuteToolAsync("Delete_management", parameters, null,
                                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                            if (gpResult.Result.IsFailed)
                            {
                                Module1.Current.ModuleLogManager.LogError(nameof(ClipRasterLayerAsync),
                                    "Unable to delete " + strClipGdb + "\\" + arrLayersToDelete[j] + ". Error code: " + gpResult.Result.ErrorCode);
                                MessageBox.Show("Unable to delete " + strClipGdb + "\\" + arrLayersToDelete[j] + ".", "BAGIS-PRO");
                            }
                            else
                            {
                                Module1.Current.ModuleLogManager.LogDebug(nameof(ClipRasterLayerAsync),
                                    "Successfully deleted temp file: " + strClipGdb + "\\" + arrLayersToDelete[j]);
                            }
                        }
                    }

                    // Update layer metadata
                    IDictionary<string, BA_Objects.DataSource> dictLocalDataSources = GeneralTools.QueryLocalDataSources();
                    BA_Objects.DataSource updateDataSource = new BA_Objects.DataSource(dictDataSources[strDataType])
                    {
                        DateClipped = DateTime.Now,
                    };
                    if (dictLocalDataSources.ContainsKey(strDataType))
                    {
                        dictLocalDataSources[strDataType] = updateDataSource;
                    }
                    else
                    {
                        dictLocalDataSources.Add(strDataType, updateDataSource);
                    }
                    success = GeneralTools.SaveDataSourcesToFile(dictLocalDataSources);
                    Module1.Current.ModuleLogManager.LogDebug(nameof(ClipRasterLayerAsync),
                        "Updated settings metadata for " + strDataType);
                });
            }

            return success;
        }

        public static async Task<BA_ReturnCode> CalculateElevationZonesAsync(string aoiFilePath)
        {
            Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateElevationZonesAsync),
                "Get min and max elevation from DEM");
            IList<double> lstResult = await GeoprocessingTools.GetDemStatsAsync(aoiFilePath, "", 0.005);
            double demElevMinMeters = -1;
            double demElevMaxMeters = -1;
            if (lstResult.Count == 2)   // We expect the min and max values in that order
            {
                demElevMinMeters = lstResult[0];
                demElevMaxMeters = lstResult[1];
            }
            else
            {
                MessageBox.Show("Unable to read DEM. Elevation zones cannot be generated!!", "BAGIS-PRO");
                Module1.Current.ModuleLogManager.LogError(nameof(CalculateElevationZonesAsync),
                    "Unable to read min/max elevation from DEM");
                return BA_ReturnCode.UnknownError;
            }

            double aoiElevMin = demElevMinMeters;
            double aoiElevMax = demElevMaxMeters;
            Module1.Current.ModuleLogManager.LogInfo(nameof(CalculateElevationZonesAsync),
                "Elevations before conversion: min: " + aoiElevMin + " max: " + aoiElevMax);
            string strDemUnits = (string)Module1.Current.BatchToolSettings.DemUnits;
            string strDemDisplayUnits = (string)Module1.Current.BatchToolSettings.DemDisplayUnits;
            if (!strDemUnits.Equals(strDemDisplayUnits))
            {
                if (strDemDisplayUnits.Equals("Feet"))
                {
                    aoiElevMin = Math.Round(LinearUnit.Meters.ConvertTo(demElevMinMeters, LinearUnit.Feet), 2);
                    aoiElevMax = Math.Round(LinearUnit.Meters.ConvertTo(demElevMaxMeters, LinearUnit.Feet), 2);
                }
                else if (strDemUnits.Equals("Feet"))
                {
                    aoiElevMin = Math.Round(LinearUnit.Feet.ConvertTo(demElevMinMeters, LinearUnit.Meters), 2);
                    aoiElevMax = Math.Round(LinearUnit.Feet.ConvertTo(demElevMaxMeters, LinearUnit.Meters), 2);
                }
            }
            Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateElevationZonesAsync),
                "Elevations after conversion: min: " + aoiElevMin + " max: " + aoiElevMax);

            List<short> lstTestIntervals = Constants.VALUES_ELEV_INTERVALS.ToList();
            lstTestIntervals.Sort((a, b) => b.CompareTo(a)); // descending sort
            short bestInterval = 50;
            var range = aoiElevMax - aoiElevMin;
            Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateElevationZonesAsync),
                "Elevation range: " + range);
            foreach (var testInterval in lstTestIntervals)
            {
                double dblZoneCount = range / testInterval;
                Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateElevationZonesAsync),
                    "Test interval: " + testInterval + " Zone count: " + dblZoneCount);
                if (dblZoneCount >= (int) Module1.Current.BatchToolSettings.MinElevationZonesCount)
                {
                    bestInterval = testInterval;
                    Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateElevationZonesAsync),
                        "Setting best interval");
                    break;
                }
            }
            Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateElevationZonesAsync),
                "Best interval: " + bestInterval);
            IList<BA_Objects.Interval> lstInterval = AnalysisTools.GetElevationClasses(aoiElevMin, aoiElevMax,
                bestInterval, strDemUnits, strDemDisplayUnits);
            string strLayer = GeodatabaseTools.GetGeodatabasePath(aoiFilePath, GeodatabaseNames.Surfaces, true) +
                Constants.FILE_DEM_FILLED;
            string strZonesRaster = GeodatabaseTools.GetGeodatabasePath(aoiFilePath, GeodatabaseNames.Analysis, true) +
                Constants.FILE_ELEV_ZONE;
            string strMaskPath = GeodatabaseTools.GetGeodatabasePath(aoiFilePath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_BUFFERED_VECTOR;
            BA_ReturnCode success = await AnalysisTools.CalculateZonesAsync(aoiFilePath, strLayer,
                lstInterval, strZonesRaster, strMaskPath, "ELEVATION");
            if (success == BA_ReturnCode.Success)
            {
                // Record the bestInterval to be used later by the charts functionality
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
                // Set the elevation interval on the analysis object and save
                oAnalysis.ElevationZonesInterval = bestInterval;
                using (var file_stream = File.Create(strSettingsFile))
                {
                    var serializer = new System.Xml.Serialization.XmlSerializer(typeof(BA_Objects.Analysis));
                    serializer.Serialize(file_stream, oAnalysis);
                    Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateElevationZonesAsync),
                        "Set elevation interval in analysis.xml file");
                }
            }
            // Make a vector version of the elevation zones
            var environments = Geoprocessing.MakeEnvironmentArray(workspace: aoiFilePath);
            string strAnalysisGdb = GeodatabaseTools.GetGeodatabasePath(aoiFilePath, GeodatabaseNames.Analysis);
            var parameters = Geoprocessing.MakeValueArray(strZonesRaster, strAnalysisGdb + "\\" + Constants.FILE_ELEV_ZONES_VECTOR,
                "NO_SIMPLIFY", Constants.FIELD_VALUE);
            var gpResult = Geoprocessing.ExecuteToolAsync("RasterToPolygon_conversion", parameters, environments,
                                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            if (gpResult.Result.IsFailed)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(ExtractBelowTreelineAsync),
                "Unable to convert elevation zones raster to polygon. Error code: " + gpResult.Result.ErrorCode);
                MessageBox.Show("Unable to convert elevation zones raster to polygon. Process cancelled!!", "BAGIS-PRO");
                success = BA_ReturnCode.UnknownError;
            }
            if (success == BA_ReturnCode.Success)
            {
                success = await GeodatabaseTools.UpdateReclassFeatureAttributesAsync(new Uri(strAnalysisGdb), Constants.FILE_ELEV_ZONES_VECTOR, 
                    lstInterval);
            }
            return success;
        }

        public static async Task<BA_ReturnCode> ClipSweLayersAsync(string precipBufferDistance, string precipBufferUnits,
            string sweBufferDistance, string sweBufferUnits)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            try
            {
                success = await AnalysisTools.ClipLayersAsync(Module1.Current.Aoi.FilePath, Constants.DATA_TYPE_SWE,
                    precipBufferDistance, precipBufferUnits, sweBufferDistance, sweBufferUnits);
                // Calculate and record overall min and max for symbology
                if (success == BA_ReturnCode.Success)
                {
                    double dblOverallMin = 9999;
                    double dblOverallMax = -9999;
                    string strLayersGdb = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers, true);
                    foreach (var fName in Constants.FILES_SNODAS_SWE)
                    {
                        string strOutputPath = strLayersGdb + fName;
                        bool bExists = await GeodatabaseTools.RasterDatasetExistsAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers)), fName);
                        if (bExists)
                        {
                            double dblMin = -1;
                            var parameters = Geoprocessing.MakeValueArray(strOutputPath, "MINIMUM");
                            var environments = Geoprocessing.MakeEnvironmentArray(workspace: Module1.Current.Aoi.FilePath);
                            IGPResult gpResult = await Geoprocessing.ExecuteToolAsync("GetRasterProperties_management", parameters, environments,
                                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                            bool isDouble = Double.TryParse(Convert.ToString(gpResult.ReturnValue), out dblMin);
                            if (isDouble && dblMin < dblOverallMin)
                            {
                                dblOverallMin = dblMin;
                                Module1.Current.ModuleLogManager.LogDebug(nameof(ClipSweLayersAsync),
                                    "Updated overall SWE minimum to " + dblOverallMin);
                            }
                            double dblMax = -1;
                            parameters = Geoprocessing.MakeValueArray(strOutputPath, "MAXIMUM");
                            gpResult = await Geoprocessing.ExecuteToolAsync("GetRasterProperties_management", parameters, environments,
                                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                            isDouble = Double.TryParse(Convert.ToString(gpResult.ReturnValue), out dblMax);
                            if (isDouble && dblMax > dblOverallMax)
                            {
                                dblOverallMax = dblMax;
                                Module1.Current.ModuleLogManager.LogDebug(nameof(ClipSweLayersAsync),
                                    "Updated overall SWE maximum to " + dblOverallMax);
                            }
                        }
                    }
                    // Save overall min and max in metadata
                    if (dblOverallMin != 9999)
                    {
                        IDictionary<string, BA_Objects.DataSource> dictLocalDataSources = GeneralTools.QueryLocalDataSources();
                        if (dictLocalDataSources.ContainsKey(Constants.DATA_TYPE_SWE))
                        {
                            BA_Objects.DataSource dataSource = dictLocalDataSources[Constants.DATA_TYPE_SWE];
                            dataSource.minValue = dblOverallMin;
                            dataSource.maxValue = dblOverallMax;
                            success = GeneralTools.SaveDataSourcesToFile(dictLocalDataSources);
                            Module1.Current.ModuleLogManager.LogDebug(nameof(ClipSweLayersAsync),
                                "Updated settings overall min and max metadata for SWE");
                        }
                        else
                        {
                            Module1.Current.ModuleLogManager.LogError(nameof(ClipSweLayersAsync),
                                "Unable to locate SWE metadata entry to update");
                        }
                        success = GeneralTools.SaveDataSourcesToFile(dictLocalDataSources);
                    }
                }
            }
            catch (Exception e)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(ClipSweLayersAsync),
                    "Exception: " + e.StackTrace);
            }
            return success;
        }

        public static async Task<BA_ReturnCode> GenerateProximityRoadsLayerAsync(Uri uri, string strDistance)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            try
            {
                string strOutputPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) +
                    Constants.FILE_ROADS_ZONE;
                success = await GeoprocessingTools.BufferLinesAsync(uri.AbsolutePath + "\\" + Constants.FILE_ROADS, strOutputPath, strDistance,
                    "", "", "");

                if (success == BA_ReturnCode.Success)
                {
                    // Save buffer distance and units in metadata
                    string strBufferDistance = (string)Module1.Current.BatchToolSettings.RoadsAnalysisBufferDistance;
                    string strBufferUnits = (string)Module1.Current.BatchToolSettings.RoadsAnalysisBufferUnits;
                    // We need to add a new tag at "/metadata/dataIdInfo/searchKeys/keyword"
                    StringBuilder sb = new StringBuilder();
                    sb.Append(Constants.META_TAG_PREFIX);
                    // Buffer Distance
                    sb.Append(Constants.META_TAG_BUFFER_DISTANCE + strBufferDistance + "; ");
                    // X Units
                    sb.Append(Constants.META_TAG_XUNIT_VALUE + strBufferUnits + "; ");
                    sb.Append(Constants.META_TAG_SUFFIX);

                    //Update the metadata
                    var fc = ItemFactory.Instance.Create(strOutputPath,
                        ItemFactory.ItemType.PathItem);
                    if (fc != null)
                    {
                        await QueuedTask.Run(() =>
                        {
                            string strXml = string.Empty;
                            strXml = fc.GetXml();
                            System.Xml.XmlDocument xmlDocument = GeneralTools.UpdateMetadata(strXml, Constants.META_TAG_XPATH, sb.ToString(),
                                Constants.META_TAG_PREFIX.Length);

                            fc.SetXml(xmlDocument.OuterXml);
                        });
                    }
                }
            }
            catch (Exception e)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(GenerateProximityRoadsLayerAsync),
                    "Exception: " + e.StackTrace);
            }
            return success;
        }

        public static async Task<BA_ReturnCode> CalculatePotentialSitesAreaAsync(string aoiFolderPath)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            try
            {
                Uri uriLayersGdb = new Uri(GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Analysis));
                string[] arrSiteFileNames = { Constants.FILE_PUBLIC_LAND_ZONE, Constants.FILE_ROADS_ZONE, Constants.FILE_BELOW_TREELINE_ZONE };
                IList<string> lstIntersectLayers = new List<string>();
                string strOutputPath = uriLayersGdb.LocalPath + "\\" + Constants.FILE_SITES_LOCATION_ZONE;
                foreach (var fileName in arrSiteFileNames)
                {
                    bool bExists = await GeodatabaseTools.FeatureClassExistsAsync(uriLayersGdb, fileName);
                    if (bExists)
                    {
                        lstIntersectLayers.Add(uriLayersGdb.LocalPath + "\\" + fileName);
                    }
                }
                if (lstIntersectLayers.Count > 1)   // Make sure we have > 1 layers to intersect
                {
                    string[] arrIntersectLayers = lstIntersectLayers.ToArray();
                    success = await GeoprocessingTools.IntersectUnrankedAsync(aoiFolderPath, arrIntersectLayers, strOutputPath,
                        "ONLY_FID");
                    if (success != BA_ReturnCode.Success)
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(CalculatePotentialSitesAreaAsync),
                            "No site location layers exist to intersect. sitesloczone cannot be created!");
                    }
                }
                else if (lstIntersectLayers.Count == 1)
                {
                    success = await GeoprocessingTools.CopyFeaturesAsync(aoiFolderPath, lstIntersectLayers[0],
                        strOutputPath);
                    if (success == BA_ReturnCode.Success)
                    {
                        Module1.Current.ModuleLogManager.LogDebug(nameof(CalculatePotentialSitesAreaAsync),
                            "Only one site location layer found. sitesloczone created by copying that layer");
                    }
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(aoiFolderPath),
                        "An error occured while using the Intersect tool to generate sitesloczone !");
                }
            }
            catch (Exception e)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(CalculatePotentialSitesAreaAsync),
                    "Exception: " + e.StackTrace);
            }
            return success;
        }

        public static async Task<BA_ReturnCode> CalculateSitesZonesAsync(string aoiFolderPath, bool hasSnotel, bool hasSnowCourse)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            try
            {
                Uri uri = new Uri(GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Layers));
                Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateSitesZonesAsync), "Get min and max elevation from DEM");
                IList<double> lstResult = await GeoprocessingTools.GetDemStatsAsync(aoiFolderPath, "", 0.005);
                double demElevMinMeters = -1;
                double demElevMaxMeters = -1;
                if (lstResult.Count == 2)   // We expect the min and max values in that order
                {
                    demElevMinMeters = lstResult[0];
                    demElevMaxMeters = lstResult[1];
                }
                // We assume that the DEM and site elevations are both in meters
                IList<BA_Objects.Interval> lstInterval = null;
                string strLayer = null;
                string strZonesRaster = null;
                string strMaskPath = GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_BUFFERED_VECTOR;
                if (hasSnotel)
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateSitesZonesAsync), "Begin create Snotel zone");
                    lstInterval = await GeodatabaseTools.GetUniqueSortedValuesAsync(uri, Constants.FILE_SNOTEL,
                        Constants.FIELD_SITE_ELEV, Constants.FIELD_SITE_NAME, demElevMaxMeters, demElevMinMeters);
                    strLayer = GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Surfaces, true) +
                        Constants.FILE_DEM_FILLED;
                    strZonesRaster = GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_SNOTEL_ZONE;
                    success = await AnalysisTools.CalculateZonesAsync(aoiFolderPath, strLayer,
                        lstInterval, strZonesRaster, strMaskPath, "SNOTEL");
                    if (success == BA_ReturnCode.Success)
                    {
                        Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateSitesZonesAsync), "Snotel zones created");
                    }
                }
                if (hasSnowCourse)
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateSitesZonesAsync), "Begin create Snow Course zone");
                    lstInterval = await GeodatabaseTools.GetUniqueSortedValuesAsync(uri, Constants.FILE_SNOW_COURSE,
                        Constants.FIELD_SITE_ELEV, Constants.FIELD_SITE_NAME, demElevMaxMeters, demElevMinMeters);
                    strZonesRaster = GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_SCOS_ZONE;
                    success = await AnalysisTools.CalculateZonesAsync(aoiFolderPath, strLayer,
                        lstInterval, strZonesRaster, strMaskPath, "SNOW COURSE");
                    if (success == BA_ReturnCode.Success)
                    {
                        Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateSitesZonesAsync), "Snow course zones created");
                    }
                }
            }
            catch (Exception e)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(CalculateSitesZonesAsync),
                    "Exception: " + e.StackTrace);
            }

            return success;
        }

        public static async Task<BA_ReturnCode> CalculatePrecipitationZonesAsync()
        {
            string strLayer = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Prism, true) +
                  System.IO.Path.GetFileName((string)Module1.Current.BatchToolSettings.AoiPrecipFile);
            string strZonesRaster = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) +
                Constants.FILE_PRECIP_ZONE;
            string strMaskPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_PRISM_VECTOR;
            int prismZonesCount = (int)Module1.Current.BatchToolSettings.PrecipZonesCount;
            IList<BA_Objects.Interval> lstInterval = await AnalysisTools.GetPrismClassesAsync(Module1.Current.Aoi.FilePath,
                strLayer, prismZonesCount, "PRISM");
            BA_ReturnCode success = await AnalysisTools.CalculateZonesAsync(Module1.Current.Aoi.FilePath, strLayer,
                lstInterval, strZonesRaster, strMaskPath, "PRISM");
            if (success == BA_ReturnCode.Success)
            {
                // Record the prism zones information to be used later when presenting maps/charts
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
                // Set the prism zones information on the analysis object and save
                oAnalysis.PrecipZonesIntervalCount = prismZonesCount;
                oAnalysis.PrecipZonesInterval = Module1.Current.PrismZonesInterval;
                Module1.Current.PrismZonesInterval = 999;
                string strPrecipFile = Path.GetFileName((string)Module1.Current.BatchToolSettings.AoiPrecipFile);
                oAnalysis.PrecipZonesBegin = strPrecipFile;
                oAnalysis.PrecipZonesEnd = strPrecipFile;
                using (var file_stream = File.Create(strSettingsFile))
                {
                    var serializer = new System.Xml.Serialization.XmlSerializer(typeof(BA_Objects.Analysis));
                    serializer.Serialize(file_stream, oAnalysis);
                    Module1.Current.ModuleLogManager.LogDebug(nameof(CalculatePrecipitationZonesAsync),
                        "Set precipitation zone parameters in analysis.xml file");
                }
            }
            return success;
        }

        public static async Task<BA_ReturnCode> CalculateAspectZonesAsync()
        {
            string strLayer = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Surfaces, true) +
                Constants.FILE_ASPECT;
            string strZonesRaster = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) +
                Constants.FILE_ASPECT_ZONE;
            string strMaskPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_BUFFERED_VECTOR;
            int aspectDirectionsCount = Convert.ToInt16(Module1.Current.BatchToolSettings.AspectDirectionsCount);
            IList<BA_Objects.Interval> lstInterval = AnalysisTools.GetAspectClasses(aspectDirectionsCount);
            BA_ReturnCode success = await AnalysisTools.CalculateZonesAsync(Module1.Current.Aoi.FilePath, strLayer,
                lstInterval, strZonesRaster, strMaskPath, "ASPECT");
            if (success == BA_ReturnCode.Success)
            {
                // Record the aspect directions information
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

                // Recalculate slope and aspect for the sites
                bool bHasSnotel = false;
                bool bHasSnowCourse = false;
                Uri sitesGdbUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers, false));
                Uri uriAnalysis = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis));
                int intSites = await GeodatabaseTools.CountFeaturesAsync(sitesGdbUri, Constants.FILE_SNOTEL);
                if (intSites > 0)
                    bHasSnotel = true;
                intSites = await GeodatabaseTools.CountFeaturesAsync(sitesGdbUri, Constants.FILE_SNOW_COURSE);
                if (intSites > 0)
                    bHasSnowCourse = true;
                if (bHasSnotel == false && bHasSnowCourse == false)
                {
                    Module1.Current.ModuleLogManager.LogInfo(nameof(CalculateAspectZonesAsync),
                        "No sites found. Sites aspect and slope will not be created!");
                }
                IList<BA_Objects.Site> lstAllSites = new List<BA_Objects.Site>();
                if (bHasSnotel || bHasSnowCourse)
                {
                    bool bMergedSitesExists = await GeodatabaseTools.FeatureClassExistsAsync(uriAnalysis, Constants.FILE_MERGED_SITES);
                    if (!bMergedSitesExists)
                    {
                        Module1.Current.ModuleLogManager.LogInfo(nameof(CalculateAspectZonesAsync), Constants.FILE_MERGED_SITES +
                            " is missing. Creating it now...");
                        // Create the merged sites layer if it doesn't exist
                        string returnPath = await AnalysisTools.CreateSitesLayerAsync(sitesGdbUri);
                        if (string.IsNullOrEmpty(returnPath))
                        {
                            bMergedSitesExists = true;
                        }
                    }
                    else
                    {
                        // If it exists, check to make sure the direction and prism fields exist
                        bool bDirectionField = await GeodatabaseTools.AttributeExistsAsync(uriAnalysis, Constants.FILE_MERGED_SITES, Constants.FIELD_DIRECTION);
                        bool bAspectField = await GeodatabaseTools.AttributeExistsAsync(uriAnalysis, Constants.FILE_MERGED_SITES, Constants.FIELD_ASPECT);
                        if (!bDirectionField || !bAspectField)
                        {
                            // At least one of the fields was missing, recreate the layer
                            Module1.Current.ModuleLogManager.LogInfo(nameof(CalculateAspectZonesAsync), Constants.FILE_MERGED_SITES +
                                " was missing a critical field. Recreating it now...");
                            string returnPath = await AnalysisTools.CreateSitesLayerAsync(sitesGdbUri);
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
                    lstAllSites = await AnalysisTools.AssembleMergedSitesListAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis)));
                    if (lstAllSites.Count > 0)
                    {
                        success = await AnalysisTools.UpdateSitesPropertiesAsync(Module1.Current.Aoi.FilePath, SiteProperties.Aspect);
                    }
                }

                // Set the aspect directions information on the analysis object and save
                oAnalysis.AspectDirectionsCount = aspectDirectionsCount;
                GeneralTools.SaveAnalysisSettings(Module1.Current.Aoi.FilePath, oAnalysis);
            }
            return success;
        }

        public static async Task<BA_ReturnCode> CalculateSWEDeltaAsync(string strAoiPath)
        {
            for (int idx = 0; idx < Constants.FILES_SWE_DELTA.Length; idx++)
            {
                string strLayer1 = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers, true) +
                    Constants.FILES_SNODAS_SWE[idx + 1];
                string strLayer2 = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers, true) +
                    Constants.FILES_SNODAS_SWE[idx];
                string strOutputRaster = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) +
                    Constants.FILES_SWE_DELTA[idx];
                IGPResult gpResult = await QueuedTask.Run(() =>
                {
                    var environments = Geoprocessing.MakeEnvironmentArray(workspace: strAoiPath);
                    var parameters = Geoprocessing.MakeValueArray(strLayer1, strLayer2, strOutputRaster);
                    return Geoprocessing.ExecuteToolAsync("Minus_sa", parameters, environments,
                                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                });
                if (gpResult.IsFailed)
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateSWEDeltaAsync),
                        "Unable to execute minus tool for " + strOutputRaster + "!");
                    foreach (var objMessage in gpResult.Messages)
                    {
                        IGPMessage msg = (IGPMessage)objMessage;
                        Module1.Current.ModuleLogManager.LogError(nameof(CalculateSWEDeltaAsync),
                            msg.Text);
                    }
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateSWEDeltaAsync),
                        "Successfully wrote minus tool output to " + strOutputRaster);
                }
            }

            // Calculate and record overall min and max for symbology
            double dblOverallMin = 9999;
            double dblOverallMax = -9999;
            string strAnalysisGdb = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true);
            foreach (var fName in Constants.FILES_SWE_DELTA)
            {
                string strOutputPath = strAnalysisGdb + fName;
                bool bExists = await GeodatabaseTools.RasterDatasetExistsAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis)), fName);
                if (bExists)
                {
                    double dblMin = -1;
                    var parameters = Geoprocessing.MakeValueArray(strOutputPath, "MINIMUM");
                    var environments = Geoprocessing.MakeEnvironmentArray(workspace: Module1.Current.Aoi.FilePath);
                    IGPResult gpResult = await Geoprocessing.ExecuteToolAsync("GetRasterProperties_management", parameters, environments,
                        CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                    bool isDouble = Double.TryParse(Convert.ToString(gpResult.ReturnValue), out dblMin);
                    if (isDouble && dblMin < dblOverallMin)
                    {
                        dblOverallMin = dblMin;
                        Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateSWEDeltaAsync),
                            "Updated overall SWE delta minimum to " + dblOverallMin);
                    }
                    double dblMax = -1;
                    parameters = Geoprocessing.MakeValueArray(strOutputPath, "MAXIMUM");
                    gpResult = await Geoprocessing.ExecuteToolAsync("GetRasterProperties_management", parameters, environments,
                        CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                    isDouble = Double.TryParse(Convert.ToString(gpResult.ReturnValue), out dblMax);
                    if (isDouble && dblMax > dblOverallMax)
                    {
                        dblOverallMax = dblMax;
                        Module1.Current.ModuleLogManager.LogDebug(nameof(ClipSweLayersAsync),
                            "Updated overall SWE delta maximum to " + dblOverallMax);
                    }
                }
            }
            // Save overall min and max in metadata
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            if (dblOverallMin != 9999)
            {
                IDictionary<string, BA_Objects.DataSource> dictLocalDataSources = GeneralTools.QueryLocalDataSources();
                BA_Objects.DataSource dataSource = new BA_Objects.DataSource();
                if (dictLocalDataSources.ContainsKey(Constants.DATA_TYPE_SWE_DELTA))
                {
                    dataSource = dictLocalDataSources[Constants.DATA_TYPE_SWE_DELTA];
                }
                else
                {
                    dataSource.layerType = Constants.DATA_TYPE_SWE_DELTA;
                }
                dataSource.minValue = Math.Round(dblOverallMin - 0.5, 2, MidpointRounding.AwayFromZero);
                dataSource.maxValue = Math.Round(dblOverallMax + 0.5, 2, MidpointRounding.AwayFromZero);
                if (dictLocalDataSources.ContainsKey(Constants.DATA_TYPE_SWE_DELTA))
                {
                    dictLocalDataSources[Constants.DATA_TYPE_SWE_DELTA] = dataSource;
                }
                else
                {
                    dictLocalDataSources.Add(Constants.DATA_TYPE_SWE_DELTA, dataSource);
                }
                success = GeneralTools.SaveDataSourcesToFile(dictLocalDataSources);
                Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateSWEDeltaAsync),
                        "Updated settings overall min and max metadata for SWE Delta");
            }
            return success;
        }

        public static async Task<BA_ReturnCode> CalculatePrecipitationContributionAsync(string aoiFolderPath, double dblThreshold)
        {
            // Create temporary gdb for calculations; We will delete when done
            string tempGdbPath = aoiFolderPath + "\\contrib.gdb";
            if (File.Exists(tempGdbPath))
            {
                File.Delete(tempGdbPath);
            }
            var parameters = Geoprocessing.MakeValueArray(aoiFolderPath, "contrib.gdb");
            var environments = Geoprocessing.MakeEnvironmentArray(workspace: aoiFolderPath);
            var gpResult = await Geoprocessing.ExecuteToolAsync("CreateFileGDB_management", parameters, environments,
                                            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);

            if (gpResult.IsFailed)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(CalculatePrecipitationContributionAsync),
                    "Unable to create file geodatabase. Error code: " + gpResult.ErrorCode);
                return BA_ReturnCode.UnknownError;
            }

            // If negative threshold passed in, we use the standard deviation
            string flowAccumPath = GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Surfaces, true) + Constants.FILE_FLOW_ACCUMULATION;
            if (dblThreshold < 0)
            {
                parameters = Geoprocessing.MakeValueArray(flowAccumPath, "STD");
                gpResult = await Geoprocessing.ExecuteToolAsync("GetRasterProperties_management", parameters, environments,
                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (gpResult.IsFailed)
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(CalculatePrecipitationContributionAsync),
                        "Unable to calculate standard deviation for flow accumulation!");
                    foreach (var objMessage in gpResult.Messages)
                    {
                        IGPMessage msg = (IGPMessage)objMessage;
                        Module1.Current.ModuleLogManager.LogError(nameof(CalculatePrecipitationContributionAsync),
                            msg.Text);
                    }
                    return BA_ReturnCode.UnknownError;
                }
                else
                {
                    bool bSuccess = Double.TryParse(Convert.ToString(gpResult.ReturnValue), out dblThreshold);
                }
            }
            // Run CON tool
            string strWhere = "VALUE > " + Convert.ToString(dblThreshold);
            string conOutputPath = tempGdbPath + "\\con_tool";
            parameters = Geoprocessing.MakeValueArray(flowAccumPath, "1", conOutputPath, "0", strWhere);
            gpResult = await Geoprocessing.ExecuteToolAsync("Con_sa", parameters, environments,
                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            if (gpResult.IsFailed)
            {
                Module1.Current.ModuleLogManager.LogDebug(nameof(CalculatePrecipitationContributionAsync),
                    "Unable to run CON tool on flow accumulation!");
                foreach (var objMessage in gpResult.Messages)
                {
                    IGPMessage msg = (IGPMessage)objMessage;
                    Module1.Current.ModuleLogManager.LogError(nameof(CalculatePrecipitationContributionAsync),
                        msg.Text);
                }
                return BA_ReturnCode.UnknownError;
            }

            // Run STREAM LINK tool
            string linksOutputPath = tempGdbPath + "\\link_tool";
            string flowDirectionPath = GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Surfaces, true) + Constants.FILE_FLOW_DIRECTION;
            parameters = Geoprocessing.MakeValueArray(conOutputPath, flowDirectionPath, linksOutputPath);
            gpResult = await Geoprocessing.ExecuteToolAsync("StreamLink_sa", parameters, environments,
                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            if (gpResult.IsFailed)
            {
                Module1.Current.ModuleLogManager.LogDebug(nameof(CalculatePrecipitationContributionAsync),
                    "Unable to run STREAM LINK tool on CON tool output!");
                foreach (var objMessage in gpResult.Messages)
                {
                    IGPMessage msg = (IGPMessage)objMessage;
                    Module1.Current.ModuleLogManager.LogError(nameof(CalculatePrecipitationContributionAsync),
                        msg.Text);
                }
                return BA_ReturnCode.UnknownError;
            }

            // Run THIN tool
            string thinOutputPath = tempGdbPath + "\\thin_tool";
            parameters = Geoprocessing.MakeValueArray(linksOutputPath, thinOutputPath);
            gpResult = await Geoprocessing.ExecuteToolAsync("Thin_sa", parameters, environments,
                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            if (gpResult.IsFailed)
            {
                Module1.Current.ModuleLogManager.LogDebug(nameof(CalculatePrecipitationContributionAsync),
                    "Unable to run THIN tool on STREAM LINK tool output!");
                foreach (var objMessage in gpResult.Messages)
                {
                    IGPMessage msg = (IGPMessage)objMessage;
                    Module1.Current.ModuleLogManager.LogError(nameof(CalculatePrecipitationContributionAsync),
                        msg.Text);
                }
                return BA_ReturnCode.UnknownError;
            }

            // Convert output of THIN tool to polyline
            string streamsOutputPath = GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Layers, true) + Constants.FILE_STREAMS;
            parameters = Geoprocessing.MakeValueArray(thinOutputPath, streamsOutputPath);
            gpResult = await Geoprocessing.ExecuteToolAsync("RasterToPolyline", parameters, environments,
                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            if (gpResult.IsFailed)
            {
                Module1.Current.ModuleLogManager.LogDebug(nameof(CalculatePrecipitationContributionAsync),
                    "Unable to convert STREAM LINK tool output to polyline!");
                foreach (var objMessage in gpResult.Messages)
                {
                    IGPMessage msg = (IGPMessage)objMessage;
                    Module1.Current.ModuleLogManager.LogError(nameof(CalculatePrecipitationContributionAsync),
                        msg.Text);
                }
                return BA_ReturnCode.UnknownError;
            }

            // Generate watersheds
            string watershedOutputPath = GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Analysis, true) + Constants.FILE_PRECIPITATION_CONTRIBUTION;
            parameters = Geoprocessing.MakeValueArray(flowDirectionPath, linksOutputPath, watershedOutputPath, Constants.FIELD_VALUE);
            gpResult = await Geoprocessing.ExecuteToolAsync("Watershed_sa", parameters, environments,
                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            if (gpResult.IsFailed)
            {
                Module1.Current.ModuleLogManager.LogDebug(nameof(CalculatePrecipitationContributionAsync),
                    "Unable to run STREAM LINK tool on CON tool output!");
                foreach (var objMessage in gpResult.Messages)
                {
                    IGPMessage msg = (IGPMessage)objMessage;
                    Module1.Current.ModuleLogManager.LogError(nameof(CalculatePrecipitationContributionAsync),
                        msg.Text);
                }
                return BA_ReturnCode.UnknownError;
            }

            double dblCellSize = await GeodatabaseTools.GetCellSizeAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Analysis)), 
                Constants.FILE_PRECIPITATION_CONTRIBUTION);
            if (dblCellSize < 1)
            {
                Module1.Current.ModuleLogManager.LogDebug(nameof(CalculatePrecipitationContributionAsync),
                    "Unable to calculate cell size for WATERSHED tool output!");
                return BA_ReturnCode.UnknownError;
            }

            // Zonal statistics as table
            string annualPrismPath = GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Prism, true) + PrismFile.Annual.ToString();
            string tablePath = tempGdbPath + "\\zonal_table";
            environments = Geoprocessing.MakeEnvironmentArray(workspace: aoiFolderPath, cellSize: "MINOF");
            parameters = Geoprocessing.MakeValueArray(watershedOutputPath, Constants.FIELD_VALUE, annualPrismPath, tablePath, "DATA", "SUM");
            gpResult = await Geoprocessing.ExecuteToolAsync("ZonalStatisticsAsTable_sa", parameters, environments,
                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            if (gpResult.IsFailed)
            {
                Module1.Current.ModuleLogManager.LogDebug(nameof(CalculatePrecipitationContributionAsync),
                    "Unable to run ZONAL STATISTICS tool on WATERSHED tool output!");
                foreach (var objMessage in gpResult.Messages)
                {
                    IGPMessage msg = (IGPMessage)objMessage;
                    Module1.Current.ModuleLogManager.LogError(nameof(CalculatePrecipitationContributionAsync),
                        msg.Text);
                }
                return BA_ReturnCode.UnknownError;
            }

            IDictionary<string, string> dictVolAcreFt = new Dictionary<string, string>();
            await QueuedTask.Run(() => {
                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(tempGdbPath))))
            using (Table table = geodatabase.OpenDataset<Table>("zonal_table"))
            {
                QueryFilter queryFilter = new QueryFilter();
                using (RowCursor cursor = table.Search(queryFilter, false))
                {
                    while (cursor.MoveNext())
                    {
                        using (Row pRow = (Row)cursor.Current)
                        {
                            string strZone = Convert.ToString(pRow[Constants.FIELD_VALUE]);
                            double dblSum = Convert.ToDouble(pRow[Constants.FIELD_SUM]);
                            double dblVolAcreFeet = dblSum * dblCellSize * (1 / (4046.8564224 * 12));
                            if (!dictVolAcreFt.Keys.Contains(strZone))
                            {
                                dictVolAcreFt[strZone] = Convert.ToString(Math.Round(dblVolAcreFeet));
                            }
                        }
                    }
                }
            }
            });

            if (dictVolAcreFt.Keys.Count < 1)
            {
                Module1.Current.ModuleLogManager.LogDebug(nameof(CalculatePrecipitationContributionAsync),
                    "Unable to calculate VOL_ACRE_FT in zonal statistics!");
                return BA_ReturnCode.UnknownError;
            }

            // Add field to feature class to hold value
            BA_ReturnCode success = await GeoprocessingTools.AddFieldAsync(watershedOutputPath, Constants.FIELD_VOL_ACRE_FT, "INTEGER");
            if (success != BA_ReturnCode.Success)
            {
                Module1.Current.ModuleLogManager.LogDebug(nameof(CalculatePrecipitationContributionAsync),
                    "Unable to add field to WATERSHED tool output!");
                return BA_ReturnCode.UnknownError;
            }

            EditOperation editOperation = new EditOperation();
            await QueuedTask.Run(() => {
                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Analysis)))))
                using (RasterDataset rasterDataset = geodatabase.OpenDataset<RasterDataset>(Constants.FILE_PRECIPITATION_CONTRIBUTION))
                {
                    RasterBandDefinition bandDefinition = rasterDataset.GetBand(0).GetDefinition();
                    Raster raster = rasterDataset.CreateDefaultRaster();
                    Table rasterTable = raster.GetAttributeTable();
                    TableDefinition definition = rasterTable.GetDefinition();
                    QueryFilter queryFilter = new QueryFilter();
                    editOperation.Callback(context =>
                    {
                        using (RowCursor rowCursor = rasterTable.Search(queryFilter, false))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = (Row)rowCursor.Current)
                                {
                                    if (row != null)
                                    {
                                        // In order to update the the attribute table has to be called before any changes are made to the row
                                        context.Invalidate(row);
                                        int idxRow = definition.FindField(Constants.FIELD_VALUE);
                                        int idxVol = definition.FindField(Constants.FIELD_VOL_ACRE_FT);
                                        if (idxRow > 0 && idxVol > 0)
                                        {
                                            string strKey = Convert.ToString(row[idxRow]);
                                            if (dictVolAcreFt.Keys.Contains(strKey) && dictVolAcreFt[strKey] != null)
                                            {
                                                row[Constants.FIELD_VOL_ACRE_FT] = Convert.ToInt32(dictVolAcreFt[strKey]);
                                            }
                                        }
                                        row.Store();
                                        // Has to be called after the store too
                                        context.Invalidate(row);
                                    }
                                }
                            }
                        }
                    }, rasterTable);
                }
                bool bModificationResult = false;
                string errorMsg = "";
                try
                {
                    bModificationResult = editOperation.Execute();
                    if (!bModificationResult) errorMsg = editOperation.ErrorMessage;
                }
                catch (GeodatabaseException exObj)
                {
                    success = BA_ReturnCode.WriteError;
                    errorMsg = exObj.Message;
                }

                if (String.IsNullOrEmpty(errorMsg))
                {
                    Project.Current.SaveEditsAsync();
                    success = BA_ReturnCode.Success;
                }
                else
                {
                    if (Project.Current.HasEdits)
                        Project.Current.DiscardEditsAsync();
                    Module1.Current.ModuleLogManager.LogError(nameof(CalculatePrecipitationContributionAsync),
                        "Exception: " + errorMsg);
                    success = BA_ReturnCode.UnknownError;
                }
            });
            return success;
        }

        public static async Task<BA_ReturnCode> GenerateWinterPrecipitationLayerAsync(BA_Objects.Aoi oAoi)
        {
            if (oAoi.WinterStartMonth < 1 || oAoi.WinterEndMonth < 1)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(GenerateWinterPrecipitationLayerAsync),
                    "Missing start or end month from aoi master feature service. Unable to generate winter precipitation layer!");
                return BA_ReturnCode.UnknownError;
            }

            IList<int> lstMonths = new List<int>();
            if (oAoi.WinterStartMonth == oAoi.WinterEndMonth)  //only one month is selected
            {
                lstMonths.Add(oAoi.WinterStartMonth);
            }
            else if (oAoi.WinterStartMonth < oAoi.WinterEndMonth) //single calendar year span
            {
                int monthDiff = oAoi.WinterEndMonth - oAoi.WinterStartMonth;
                for (int i = 0; i < monthDiff; i++)
                {
                    lstMonths.Add(oAoi.WinterStartMonth + i);
                }
            }
            else if (oAoi.WinterStartMonth > oAoi.WinterEndMonth) //cross-calendar year span
            {
                for (int i = 1; i < oAoi.WinterEndMonth + 1; i++)
                {
                    lstMonths.Add(i);
                }
                for (int i = oAoi.WinterStartMonth; i < 13; i++)
                {
                    lstMonths.Add(i);
                }
            }

            StringBuilder sb = new StringBuilder();
            Uri gdbUri = new Uri(GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Prism));
            foreach (var intMonth in lstMonths)
            {
                string rasterName = "";
                if (Enum.IsDefined(typeof(PrismFile), intMonth -1))
                    rasterName = ((PrismFile)intMonth - 1).ToString();
                else
                    rasterName = "Invalid Value";
                bool bExists = await GeodatabaseTools.RasterDatasetExistsAsync(gdbUri, rasterName);
                string strLayerPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Prism, true) + rasterName;
                if (bExists)
                {
                    sb.Append(strLayerPath);
                    if (lstMonths.Count > 1)
                    {
                        sb.Append(";");
                    }                    
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(GenerateWinterPrecipitationLayerAsync),
                        "Unable to locate layer " + strLayerPath + "! Winter precipitation layer cannot be created");
                    return BA_ReturnCode.ReadError;
                }
            }
            string strInputLayerPaths = sb.ToString();
            // Remove the ; at the end of the string
            if (lstMonths.Count > 1)
            {
                strInputLayerPaths = strInputLayerPaths.Substring(0, strInputLayerPaths.Length - 1);
            }
            string strOutputPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) 
                + Constants.FILE_WINTER_PRECIPITATION;
            var parameters = Geoprocessing.MakeValueArray(strInputLayerPaths, strOutputPath, "SUM");
            var environments = Geoprocessing.MakeEnvironmentArray(workspace: oAoi.FilePath);
            var gpResult = await Geoprocessing.ExecuteToolAsync("CellStatistics_sa", parameters, environments,
                                            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            if (gpResult.IsFailed)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(GenerateWinterPrecipitationLayerAsync),
                    "Unable to sum raster layers for winter precipitation map. Error code: " + gpResult.ErrorCode);
                success = BA_ReturnCode.UnknownError;
            }
            else
            {
                success = BA_ReturnCode.Success;
            }
            if (success == BA_ReturnCode.Success)
            {
                // Record the winter months information to be used later when presenting maps/charts
                BA_Objects.Analysis oAnalysis = GeneralTools.GetAnalysisSettings(Module1.Current.Aoi.FilePath);
                // Set the prism zones information on the analysis object and save
                if (oAnalysis != null)
                {
                    oAnalysis.WinterStartMonth = ((PrismFile)oAoi.WinterStartMonth - 1).ToString();
                    oAnalysis.WinterEndMonth = ((PrismFile)oAoi.WinterEndMonth - 1).ToString();
                    string strSettingsFile = Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_MAPS + "\\" +
                        Constants.FILE_SETTINGS;
                    using (var file_stream = File.Create(strSettingsFile))
                    {
                        var serializer = new System.Xml.Serialization.XmlSerializer(typeof(BA_Objects.Analysis));
                        serializer.Serialize(file_stream, oAnalysis);
                        Module1.Current.ModuleLogManager.LogDebug(nameof(GenerateWinterPrecipitationLayerAsync),
                            "Set winter start and end months in analysis.xml file");
                    }
                }
            }
            return success;
        }

        //
        //
        //
         public static async Task<BA_ReturnCode> UpdateSitesPropertiesAsync(string strAoiFilePath, 
            SiteProperties siteProperties)
        {
            var environments = Geoprocessing.MakeEnvironmentArray(workspace: strAoiFilePath);
            string fileExtract = "tmpExtract";
            string fieldDirection = "tmp_dir";
            IList<string> lstFields = new List<string>();
            IList<string> lstFieldDataTypes = new List<string>();
            IList<string> lstUri = new List<string>();
            IList<string> lstInputRasters = new List<string>();
            switch (siteProperties)
            {
                case SiteProperties.Aspect:
                    lstFields.Add(Constants.FIELD_ASPECT);
                    lstFields.Add(fieldDirection);
                    lstFieldDataTypes.Add("DOUBLE");
                    lstFieldDataTypes.Add("INTEGER");
                    lstUri.Add(GeodatabaseTools.GetGeodatabasePath(strAoiFilePath, GeodatabaseNames.Surfaces));
                    lstUri.Add(GeodatabaseTools.GetGeodatabasePath(strAoiFilePath, GeodatabaseNames.Analysis));
                    lstInputRasters.Add(Constants.FILE_ASPECT);
                    lstInputRasters.Add(Constants.FILE_ASPECT_ZONE);
                    break;

                case SiteProperties.Precipitation:
                    lstFields.Add(Constants.FIELD_PRECIP);
                    lstFieldDataTypes.Add("DOUBLE");
                    lstUri.Add(GeodatabaseTools.GetGeodatabasePath(strAoiFilePath, GeodatabaseNames.Prism));
                    lstInputRasters.Add(Path.GetFileName((string)Module1.Current.BatchToolSettings.AoiPrecipFile));
                    break;
            }

            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            string analysisPath = GeodatabaseTools.GetGeodatabasePath(strAoiFilePath, GeodatabaseNames.Analysis);
            string featureClassToUpdate = analysisPath + "\\" + Constants.FILE_MERGED_SITES;
            for (int i = 0; i < lstFields.Count; i++)
            {
                if (await GeodatabaseTools.AttributeExistsAsync(new Uri(analysisPath), Constants.FILE_MERGED_SITES, lstFields[i]))
                {
                    success = BA_ReturnCode.Success;
                }
                else
                {
                    success = await GeoprocessingTools.AddFieldAsync(featureClassToUpdate, lstFields[i], lstFieldDataTypes[i]);
                }
                if (success == BA_ReturnCode.Success)
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(UpdateSitesPropertiesAsync),
                        "New field " + lstFields[i] + " added to " + Constants.FILE_MERGED_SITES);
                    string inputRaster = lstUri[i] + "\\" + lstInputRasters[i];
                    if (await GeodatabaseTools.RasterDatasetExistsAsync(new Uri(lstUri[i]), lstInputRasters[i]))
                    {
                        var parameters = Geoprocessing.MakeValueArray(featureClassToUpdate, inputRaster, analysisPath + "\\" + fileExtract, "NONE", "VALUE_ONLY");
                        var gpResult = await Geoprocessing.ExecuteToolAsync("ExtractValuesToPoints_sa", parameters, environments,
                                                        CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                        if (gpResult.IsFailed)
                        {
                            Module1.Current.ModuleLogManager.LogError(nameof(UpdateSitesPropertiesAsync),
                                "Extract values to points tool failed to create tmpExtract. Error code: " + gpResult.ErrorCode);
                            success = BA_ReturnCode.UnknownError;
                        }
                        else
                        {
                            parameters = Geoprocessing.MakeValueArray(featureClassToUpdate, Constants.FIELD_OBJECT_ID, analysisPath + "\\" + fileExtract,
                                Constants.FIELD_OBJECT_ID, "KEEP_ALL");
                            // Need GPExecuteToolFlag to add the layer to the map
                            gpResult = await Geoprocessing.ExecuteToolAsync("management.AddJoin", parameters, environments,
                                CancelableProgressor.None, GPExecuteToolFlags.Default);
                            if (gpResult.IsFailed)
                            {
                                Module1.Current.ModuleLogManager.LogError(nameof(UpdateSitesPropertiesAsync),
                                    "AddJoin tool failed. Error code: " + gpResult.ErrorCode);
                                success = BA_ReturnCode.UnknownError;
                            }
                            else
                            {
                                string lyrJoin = gpResult.ReturnValue;
                                parameters = Geoprocessing.MakeValueArray(lyrJoin, "merged_sites." + lstFields[i],
                                    "!tmpExtract.RASTERVALU!", "PYTHON3", "", "DOUBLE");
                                await Geoprocessing.ExecuteToolAsync("management.CalculateField", parameters, environments,
                                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                                if (gpResult.IsFailed)
                                {
                                    Module1.Current.ModuleLogManager.LogError(nameof(CreateSitesLayerAsync),
                                        "CalculateField tool failed to update field. Error code: " + gpResult.ErrorCode);
                                    success = BA_ReturnCode.UnknownError;
                                }
                                else
                                {
                                    var map = MapView.Active.Map;
                                    await QueuedTask.Run(() =>
                                    {
                                        Layer oLayer =
                                        map.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(lyrJoin, StringComparison.CurrentCultureIgnoreCase));
                                        if (oLayer != null)
                                        {

                                            map.RemoveLayer(oLayer);
                                            success = BA_ReturnCode.Success;
                                        }
                                    });
                                }
                            }
                        }

                    }
                    else
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(UpdateSitesPropertiesAsync),
                            inputRaster + " not found. Values not extracted!!");

                    }
                }
            }

            // Delete tmpExtract layer
            success = await GeoprocessingTools.DeleteDatasetAsync(analysisPath + "\\" + fileExtract);


            if (siteProperties == SiteProperties.Aspect && 
                success == BA_ReturnCode.Success)
            {
                // Update aspect directions
                Uri uriAnalysis = new Uri(analysisPath);
                if (await GeodatabaseTools.AttributeExistsAsync(uriAnalysis,
                    Constants.FILE_MERGED_SITES, fieldDirection))
                {
                    if (! await GeodatabaseTools.AttributeExistsAsync(new Uri(analysisPath), Constants.FILE_MERGED_SITES, Constants.FIELD_DIRECTION))
                    {
                        success = await GeoprocessingTools.AddFieldAsync(featureClassToUpdate, Constants.FIELD_DIRECTION, "TEXT");
                    }                    
                    if (success == BA_ReturnCode.Success)
                    {
                        int intAspectCount = Convert.ToInt16(Module1.Current.BatchToolSettings.AspectDirectionsCount);
                        IList<BA_Objects.Interval> lstAspectInterval = AnalysisTools.GetAspectClasses(intAspectCount);
                        success = await UpdateAspectDirectionsAsync(uriAnalysis, Constants.FILE_MERGED_SITES,
                            lstAspectInterval, fieldDirection);
                        if (success == BA_ReturnCode.Success)
                        {
                            string[] arrFieldsToDelete = { fieldDirection };
                            success = await GeoprocessingTools.DeleteFeatureClassFieldsAsync(uriAnalysis.LocalPath + "\\" + Constants.FILE_MERGED_SITES,
                                arrFieldsToDelete);
                        }
                    }
                }
            }
            return success;
        }

        public static async Task<string> CreateSitesLayerAsync(Uri gdbUri)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            bool hasSnotel = await GeodatabaseTools.FeatureClassExistsAsync(gdbUri, Constants.FILE_SNOTEL);
            bool hasSiteType = false;
            bool bUpdateSnotel = false;
            bool bUpdateSnowCourse = false;
            if (hasSnotel)
            {
                hasSiteType = await GeodatabaseTools.AttributeExistsAsync(gdbUri, Constants.FILE_SNOTEL, Constants.FIELD_SITE_TYPE);
                if (hasSiteType == false)
                {
                    success = await GeoprocessingTools.AddFieldAsync(gdbUri.LocalPath + "\\" + Constants.FILE_SNOTEL,
                        Constants.FIELD_SITE_TYPE, "TEXT");
                    if (success == BA_ReturnCode.Success)
                    {
                        bUpdateSnotel = true;
                        Module1.Current.ModuleLogManager.LogDebug(nameof(CreateSitesLayerAsync),
                            "Added ba_site_type field to Snotel");
                    }
                }
            }
            bool hasSnowCourse = await GeodatabaseTools.FeatureClassExistsAsync(gdbUri, Constants.FILE_SNOW_COURSE);
            if (hasSnowCourse)
            {
                hasSiteType = await GeodatabaseTools.AttributeExistsAsync(gdbUri, Constants.FILE_SNOW_COURSE, Constants.FIELD_SITE_TYPE);
                if (hasSiteType == false)
                {
                    success = await GeoprocessingTools.AddFieldAsync(gdbUri.LocalPath + "\\" + Constants.FILE_SNOW_COURSE,
                        Constants.FIELD_SITE_TYPE, "TEXT");
                    if (success == BA_ReturnCode.Success)
                    {
                        bUpdateSnowCourse = true;
                        Module1.Current.ModuleLogManager.LogDebug(nameof(CreateSitesLayerAsync),
                            "Added ba_site_type field to Snow Course");
                    }
                }
            }

            bool modificationResult = false;
            string errorMsg = "";
            await QueuedTask.Run(() => {
                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(gdbUri)))
                {
                    if (bUpdateSnotel)
                    {
                        using (FeatureClass featureClass = geodatabase.OpenDataset<FeatureClass>(Constants.FILE_SNOTEL))
                        {
                            FeatureClassDefinition featureClassDefinition = featureClass.GetDefinition();
                            int idxSiteType = featureClassDefinition.FindField(Constants.FIELD_SITE_TYPE);
                            if (idxSiteType > 0)
                            {
                                EditOperation editOperation = new EditOperation();
                                editOperation.Callback(context =>
                                {
                                    using (RowCursor rowCursor = featureClass.Search(new QueryFilter(), false))
                                    {
                                        while (rowCursor.MoveNext())
                                        {
                                            using (Feature feature = (Feature)rowCursor.Current)
                                            {
                                                // In order to update the the attribute table has to be called before any changes are made to the row
                                                context.Invalidate(feature);
                                                feature[idxSiteType] = SiteType.Snotel.ToString();
                                                feature.Store();
                                                // Has to be called after the store too
                                                context.Invalidate(feature);
                                            }
                                        }
                                    }
                                }, featureClass);

                                try
                                {
                                    modificationResult = editOperation.Execute();
                                    if (!modificationResult) errorMsg = editOperation.ErrorMessage;
                                }
                                catch (GeodatabaseException exObj)
                                {
                                    errorMsg = exObj.Message;
                                }
                            }
                            else
                            {
                                Module1.Current.ModuleLogManager.LogDebug(nameof(CreateSitesLayerAsync),
                                    "Unable to locate ba_site_type field on snotel_sites. Field could not be updated");
                                return;
                            }
                        }
                    }
                    if (bUpdateSnowCourse)
                    {
                        using (FeatureClass featureClass = geodatabase.OpenDataset<FeatureClass>(Constants.FILE_SNOW_COURSE))
                        {
                            FeatureClassDefinition featureClassDefinition = featureClass.GetDefinition();
                            int idxSiteType = featureClassDefinition.FindField(Constants.FIELD_SITE_TYPE);
                            if (idxSiteType > 0)
                            {
                                EditOperation editOperation = new EditOperation();
                                editOperation.Callback(context =>
                                {
                                    using (RowCursor rowCursor = featureClass.Search(new QueryFilter(), false))
                                    {
                                        while (rowCursor.MoveNext())
                                        {
                                            using (Feature feature = (Feature)rowCursor.Current)
                                            {
                                                // In order to update the the attribute table has to be called before any changes are made to the row
                                                context.Invalidate(feature);
                                                feature[idxSiteType] = SiteType.SnowCourse.ToString();
                                                feature.Store();
                                                // Has to be called after the store too
                                                context.Invalidate(feature);
                                            }
                                        }
                                    }
                                }, featureClass);

                                try
                                {
                                    modificationResult = editOperation.Execute();
                                    if (!modificationResult) errorMsg = editOperation.ErrorMessage;
                                }
                                catch (GeodatabaseException exObj)
                                {
                                    errorMsg = exObj.Message;
                                }
                            }
                            else
                            {
                                Module1.Current.ModuleLogManager.LogDebug(nameof(CreateSitesLayerAsync),
                                    "Unable to locate ba_site_type field on snow_course_sites. Field could not be updated");
                                return;
                            }
                        }
                    }
                }
            });
            if (String.IsNullOrEmpty(errorMsg))
            {
                await Project.Current.SaveEditsAsync();
            }
            else
            {
                if (Project.Current.HasEdits)
                    await Project.Current.DiscardEditsAsync();
                Module1.Current.ModuleLogManager.LogError(nameof(CreateSitesLayerAsync),
                    "Exception: " + errorMsg);
                return "";
            }

            string analysisPath = GeodatabaseTools.GetGeodatabasePath(System.IO.Path.GetDirectoryName(gdbUri.LocalPath), GeodatabaseNames.Analysis, true);
            string returnPath = analysisPath + Constants.FILE_MERGED_SITES;
            if (hasSnotel)
            {
                // No snow course to merge; copy SNOTEL to merged sites
                success = await GeoprocessingTools.CopyFeaturesAsync(Module1.Current.Aoi.FilePath, gdbUri.LocalPath + "\\" + Constants.FILE_SNOTEL, returnPath);
            }
            else if (hasSnowCourse)
            {
                // No Snotel to merge; copy snow courses to merged sites
                success = await GeoprocessingTools.CopyFeaturesAsync(Module1.Current.Aoi.FilePath, gdbUri.LocalPath + "\\" + Constants.FILE_SNOW_COURSE, returnPath);
            }
            if (hasSnotel && hasSnowCourse)
            {
                // Need to append sites
                string featuresToAppend = gdbUri.LocalPath + "\\" + Constants.FILE_SNOW_COURSE;
                    returnPath = analysisPath + Constants.FILE_MERGED_SITES;
                    var parameters = Geoprocessing.MakeValueArray(featuresToAppend, returnPath);
                    IGPResult gpResult = await Geoprocessing.ExecuteToolAsync("Append_management", parameters, null,
                                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                    if (gpResult.IsFailed)
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(CreateSitesLayerAsync),
                            "Unable to append features. Error code: " + gpResult.ErrorCode);
                        returnPath = ""; ;
                    }
                    else
                    {
                        Module1.Current.ModuleLogManager.LogDebug(nameof(CreateSitesLayerAsync),
                            "Snow course sites appended successfully");
                    }
            }

                if (!String.IsNullOrEmpty(returnPath))
                {
                    string strAoiPath = Module1.Current.Aoi.FilePath;
                    var environments = Geoprocessing.MakeEnvironmentArray(workspace: strAoiPath);
                    string fileExtract = "tmpExtract";
                    string fieldDirection = "tmp_dir";
                    string[] arrFields = { Constants.FIELD_PRECIP, Constants.FIELD_ASPECT, Constants.FIELD_SLOPE, fieldDirection };
                    string[] arrFieldDataTypes = { "DOUBLE", "DOUBLE", "DOUBLE", "INTEGER" };
                    string[] arrUri = { GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Prism),
                                        GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Surfaces),
                                        GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Surfaces),
                                        GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Analysis)};
                    string[] arrInputRasters = {Path.GetFileName((string)Module1.Current.BatchToolSettings.AoiPrecipFile),
                                                Constants.FILE_ASPECT,
                                                Constants.FILE_SLOPE,
                                                Constants.FILE_ASPECT_ZONE};

                    for (int i = 0; i < arrFields.Length; i++)
                    {
                        success = await GeoprocessingTools.AddFieldAsync(returnPath, arrFields[i], arrFieldDataTypes[i]);
                        if (success == BA_ReturnCode.Success)
                        {
                            Module1.Current.ModuleLogManager.LogDebug(nameof(CreateSitesLayerAsync),
                                "New field " + arrFields[i] + " added to " + Constants.FILE_MERGED_SITES);
                            string inputRaster = arrUri[i] + "\\" + arrInputRasters[i];
                            if (await GeodatabaseTools.RasterDatasetExistsAsync(new Uri(arrUri[i]), arrInputRasters[i]))
                            {
                                var parameters = Geoprocessing.MakeValueArray(returnPath, inputRaster, analysisPath + "\\" + fileExtract, "NONE", "VALUE_ONLY");
                                var gpResult = await Geoprocessing.ExecuteToolAsync("ExtractValuesToPoints_sa", parameters, environments,
                                                                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                                if (gpResult.IsFailed)
                                {
                                    Module1.Current.ModuleLogManager.LogError(nameof(CreateSitesLayerAsync),
                                        "Extract values to points tool failed to create tmpExtract. Error code: " + gpResult.ErrorCode);
                                    success = BA_ReturnCode.UnknownError;
                                }
                                else
                                {
                                    parameters = Geoprocessing.MakeValueArray(returnPath, Constants.FIELD_OBJECT_ID, analysisPath + "\\" + fileExtract,
                                        Constants.FIELD_OBJECT_ID, "KEEP_ALL");
                                    // Need GPExecuteToolFlag to add the layer to the map
                                    gpResult = await Geoprocessing.ExecuteToolAsync("management.AddJoin", parameters, environments,
                                        CancelableProgressor.None, GPExecuteToolFlags.Default);
                                    if (gpResult.IsFailed)
                                    {
                                        Module1.Current.ModuleLogManager.LogError(nameof(CreateSitesLayerAsync),
                                            "AddJoin tool failed. Error code: " + gpResult.ErrorCode);
                                        success = BA_ReturnCode.UnknownError;
                                    }
                                    else
                                    {
                                        string lyrJoin = gpResult.ReturnValue;
                                        parameters = Geoprocessing.MakeValueArray(lyrJoin, "merged_sites." + arrFields[i],
                                            "!tmpExtract.RASTERVALU!", "PYTHON3", "", "DOUBLE");
                                        await Geoprocessing.ExecuteToolAsync("management.CalculateField", parameters, environments,
                                            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                                        if (gpResult.IsFailed)
                                        {
                                            Module1.Current.ModuleLogManager.LogError(nameof(CreateSitesLayerAsync),
                                                "CalculateField tool failed to update field. Error code: " + gpResult.ErrorCode);
                                            success = BA_ReturnCode.UnknownError;
                                        }
                                        else
                                        {
                                            var map = MapView.Active.Map;
                                            await QueuedTask.Run(() =>
                                            {
                                                Layer oLayer =
                                                map.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(lyrJoin, StringComparison.CurrentCultureIgnoreCase));
                                                if (oLayer != null)
                                                {

                                                    map.RemoveLayer(oLayer);
                                                    success = BA_ReturnCode.Success;
                                                }
                                            });
                                        }
                                    }
                                }

                            }
                            else
                            {
                                Module1.Current.ModuleLogManager.LogError(nameof(CreateSitesLayerAsync),
                                    inputRaster + " not found. Values not extracted!!");

                            }
                        }

                    }
                    if (success == BA_ReturnCode.Success)
                    {
                        // Delete tmpExtract layer
                        success = await GeoprocessingTools.DeleteDatasetAsync(analysisPath + "\\" + fileExtract);
                        
                        // Update aspect directions
                        Uri uriAnalysis = new Uri(GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Analysis));
                        if (await GeodatabaseTools.AttributeExistsAsync(uriAnalysis,
                            Constants.FILE_MERGED_SITES, fieldDirection))
                        {
                            success = await GeoprocessingTools.AddFieldAsync(returnPath, Constants.FIELD_DIRECTION, "TEXT");
                            if (success == BA_ReturnCode.Success)
                            {
                                int intAspectCount = Convert.ToInt16(Module1.Current.BatchToolSettings.AspectDirectionsCount);
                                IList<BA_Objects.Interval> lstAspectInterval = AnalysisTools.GetAspectClasses(intAspectCount);
                                success = await UpdateAspectDirectionsAsync(uriAnalysis, Constants.FILE_MERGED_SITES,
                                    lstAspectInterval, fieldDirection);
                                if (success == BA_ReturnCode.Success)
                                {
                                    string[] arrFieldsToDelete = { fieldDirection };
                                    success = await GeoprocessingTools.DeleteFeatureClassFieldsAsync(uriAnalysis.LocalPath + "\\" + Constants.FILE_MERGED_SITES,
                                        arrFieldsToDelete);
                                }
                            }
                        }
                    }
                }
            return returnPath;
        }


    }

}