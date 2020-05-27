using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bagis_pro
{
    public class AnalysisTools
    {

        public static async Task<BA_ReturnCode> GenerateSiteLayersAsync()
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
                    lstSites = await AnalysisTools.AssembleSitesListAsync(Constants.FILE_SNOTEL, SiteType.Snotel.ToString());
                    success = await AnalysisTools.CalculateRepresentedArea(demElevMinMeters, demElevMaxMeters, lstSites, Constants.FILE_SNOTEL_REPRESENTED);
                    if (success != BA_ReturnCode.Success)
                        bHasSnotel = false;
                }

                // snow course sites
                if (bHasSnowCourse)
                {
                    lstSites = await AnalysisTools.AssembleSitesListAsync(Constants.FILE_SNOW_COURSE, SiteType.SnowCourse.ToString());
                    success = await AnalysisTools.CalculateRepresentedArea(demElevMinMeters, demElevMaxMeters, lstSites, Constants.FILE_SCOS_REPRESENTED);
                    if (success != BA_ReturnCode.Success)
                        bHasSnowCourse = false;
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

        public static async Task<IList<BA_Objects.Site>> AssembleSitesListAsync(string sitesFileName, string sType)
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
                            double bufferMeters = LinearUnit.Miles.ConvertTo(Module1.Current.Aoi.SiteBufferDistMiles, LinearUnit.Meters);
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

        public static async Task<BA_ReturnCode> CalculateRepresentedArea(double demElevMinMeters,
                                                                         double demElevMaxMeters, IList<BA_Objects.Site> lstSites,
                                                                         string strOutputFile)
        {
            IGPResult gpResult = null;
            StringBuilder sb = new StringBuilder();
            string tmpBuffer = "tmpBuffer";
            string tmpOutputFile = "reclElev";
            string tmpUnion = "tmpUnion";
            string tmpDissolve = "tmpDissolve";
            IList<string> lstLayersToDelete = new List<string> {tmpBuffer, tmpUnion, tmpDissolve };

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
                double minElevMeters = aSite.ElevMeters - LinearUnit.Feet.ConvertToMeters(Module1.Current.Aoi.SiteElevRangeFeet);
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
                double maxElevMeters = aSite.ElevMeters + LinearUnit.Feet.ConvertToMeters(Module1.Current.Aoi.SiteElevRangeFeet);
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


        public static string GetSiteScenarioFileName (BA_Objects.Site site)
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
            sb.Append((int) site.SiteType);
            sb.Append("_");
            sb.Append(site.ObjectId);
            sb.Append("_Rep");
            return sb.ToString();
        }

        public static async Task<string[]> GetStationValues()
        {
            string strTriplet = "";
            string strAwdbId = "";
            string strStationName = "";
            string[] arrReturnValues = new string[] { strTriplet, strStationName };
            Uri ppUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi));
            string strPourpointClassPath = ppUri.LocalPath + "\\" + Constants.FILE_POURPOINT;
            string usgsServiceLayerId = Module1.Current.Settings.m_pourpointUri.Split('/').Last();
            int intTrim = usgsServiceLayerId.Length + 1;
            string usgsTempString = Module1.Current.Settings.m_pourpointUri.Substring(0, Module1.Current.Settings.m_pourpointUri.Length - intTrim);
            Uri usgsServiceUri = new Uri(usgsTempString);
            
            Webservices ws = new Webservices();
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
                        strTriplet = await ws.QueryServiceForSingleValueAsync(usgsServiceUri, usgsServiceLayerId, Constants.FIELD_STATION_TRIPLET, queryFilter);
                        if (!string.IsNullOrEmpty(strTriplet))
                        {
                            bUpdateTriplet = true;
                        }
                        strStationName = await ws.QueryServiceForSingleValueAsync(usgsServiceUri, usgsServiceLayerId, Module1.Current.Settings.m_nameField, queryFilter);
                        if (!string.IsNullOrEmpty(strStationName))
                        {
                            bUpdateStationName = true;
                        }
                    }
                }
                if (string.IsNullOrEmpty(strTriplet))
                {
                    // If triplet is still null, use the near tool
                    BA_ReturnCode success = await GeoprocessingTools.NearAsync(strPourpointClassPath, Module1.Current.Settings.m_pourpointUri);
                    if (success == BA_ReturnCode.Success)
                    {
                        QueryFilter queryFilter = new QueryFilter();
                        string strNearId = await GeodatabaseTools.QueryTableForSingleValueAsync(ppUri, Constants.FILE_POURPOINT,
                            Constants.FIELD_NEAR_ID, queryFilter);
                        if (!String.IsNullOrEmpty(strNearId))
                        {
                            queryFilter.WhereClause = Constants.FIELD_OBJECT_ID + " = '" + strNearId + "'";
                            strTriplet = await ws.QueryServiceForSingleValueAsync(usgsServiceUri, usgsServiceLayerId, Constants.FIELD_STATION_TRIPLET, queryFilter);
                            if (!String.IsNullOrEmpty(strTriplet))
                            {
                                bUpdateTriplet = true;
                            }
                            strAwdbId = await ws.QueryServiceForSingleValueAsync(usgsServiceUri, usgsServiceLayerId, Constants.FIELD_USGS_ID, queryFilter);
                            if (!String.IsNullOrEmpty(strAwdbId))
                            {
                                bUpdateAwdb = true;
                            }
                            strStationName = await ws.QueryServiceForSingleValueAsync(usgsServiceUri, usgsServiceLayerId, Module1.Current.Settings.m_nameField, queryFilter);
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
                //Save the new values to the pourpoint layer if needed
                if (bUpdateAwdb == true || bUpdateTriplet == true || bUpdateStationName == true)
                {
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

        public static async Task<BA_ReturnCode> ClipSnotelSWELayersAsync(string strAoiPath)
        {
            Webservices ws = new Webservices();
            Module1.Current.ModuleLogManager.LogDebug(nameof(ClipSnotelSWELayersAsync),
                "Contacting webservices server to retrieve Snotel SWE metadata");
            IDictionary<string, dynamic> dictDataSources =
                await ws.QueryDataSourcesAsync(Module1.Current.Settings.m_eBagisServer);
            string strSwePrefix = dictDataSources[Constants.DATA_TYPE_SWE].uri;

            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            if (!String.IsNullOrEmpty(strSwePrefix))
            {
                double dblOverallMin = 9999;
                double dblOverallMax = -9999;
                Uri clipFileUri = new Uri(GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Aoi, false));
                string[] arrReturnValues = await GeodatabaseTools.QueryAoiEnvelopeAsync(clipFileUri, Constants.FILE_AOI_PRISM_VECTOR);
                if (arrReturnValues.Length == 2)
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(ClipSnotelSWELayersAsync),
                        "Retrieved the AOI envelope from " + Constants.FILE_AOI_PRISM_VECTOR + " layer for clipping");
                    string strEnvelopeText = arrReturnValues[0];
                    string strTemplateDataset = arrReturnValues[1];
                    int i = 0;
                    foreach (string strUri in Constants.URIS_SNODAS_SWE)
                    {
                        Uri imageServiceUri = new Uri(strSwePrefix + strUri + Constants.URI_IMAGE_SERVER);
                        string strOutputPath = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Layers, true) + Constants.FILES_SNODAS_SWE[i];
                        success = await GeoprocessingTools.ClipRasterAsync(imageServiceUri.AbsoluteUri, strEnvelopeText, strOutputPath, strTemplateDataset,
                            "", true, strAoiPath, BA_Objects.Aoi.SnapRasterPath(strAoiPath));
                        Module1.Current.ModuleLogManager.LogDebug(nameof(ClipSnotelSWELayersAsync),
                            "Clipped " + Constants.FILES_SNODAS_SWE[i] + " layer");
                        if (success != BA_ReturnCode.Success)
                        {
                            Module1.Current.ModuleLogManager.LogError(nameof(ClipSnotelSWELayersAsync),
                                 "An error occurred while clipping. Process halted");
                            break;
                        }
                        else
                        {
                            double dblMin = -1;
                            var parameters = Geoprocessing.MakeValueArray(strOutputPath, "MINIMUM");
                            var environments = Geoprocessing.MakeEnvironmentArray(workspace: strAoiPath);
                            IGPResult gpResult = await Geoprocessing.ExecuteToolAsync("GetRasterProperties_management", parameters, environments,
                                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                            bool isDouble = Double.TryParse(Convert.ToString(gpResult.ReturnValue), out dblMin);
                            if (isDouble && dblMin < dblOverallMin)
                            {
                                dblOverallMin = dblMin;
                            }
                            double dblMax = -1;
                            parameters = Geoprocessing.MakeValueArray(strOutputPath, "MAXIMUM");
                            gpResult = await Geoprocessing.ExecuteToolAsync("GetRasterProperties_management", parameters, environments,
                                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                            isDouble = Double.TryParse(Convert.ToString(gpResult.ReturnValue), out dblMax);
                            if (isDouble && dblMax > dblOverallMax)
                            {
                                dblOverallMax = dblMax;
                                Module1.Current.ModuleLogManager.LogDebug(nameof(ClipSnotelSWELayersAsync),
                                    "Updated overall SWE maximum to " + dblOverallMax);
                            }
                        }
                        i++;
                        // update units in layer metadata
                        if (success == BA_ReturnCode.Success)
                        {
                            string strUnits = dictDataSources[Constants.DATA_TYPE_SWE].units;
                            success = await GeneralTools.CreateMetadataUnits(strOutputPath, Constants.META_TAG_CATEGORY_DEPTH, strUnits);
                            if (success == BA_ReturnCode.Success)
                            {
                                Module1.Current.ModuleLogManager.LogDebug(nameof(ClipSnotelSWELayersAsync),
                                    "Updated metadata in " + strOutputPath);
                            }
                        }
                    }
                }
                // Update layer metadata
                IDictionary<string, BA_Objects.DataSource> dictLocalDataSources = GeneralTools.QueryLocalDataSources();

                BA_Objects.DataSource updateDataSource = new BA_Objects.DataSource(dictDataSources[Constants.DATA_TYPE_SWE])
                {
                    DateClipped = DateTime.Now,
                    minValue = dblOverallMin,
                    maxValue = dblOverallMax
                };
                if (dictLocalDataSources.ContainsKey(Constants.DATA_TYPE_SWE))
                {
                    dictLocalDataSources[Constants.DATA_TYPE_SWE] = updateDataSource;
                }
                else
                {
                    dictLocalDataSources.Add(Constants.DATA_TYPE_SWE, updateDataSource);
                }
                success = GeneralTools.SaveDataSourcesToFile(dictLocalDataSources);
                Module1.Current.ModuleLogManager.LogDebug(nameof(ClipSnotelSWELayersAsync),
                    "Updated settings metadata for AOI");


            }
            return success;
        }


        //@ToDo: Separate method for calculating and storing SWE overall min/max
        public static async Task <BA_ReturnCode> ClipLayersAsync(string strAoiPath, string strDataType, 
            string prismBufferDistance, string prismBufferUnits, string strBufferDistance, string strBufferUnits)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;

            Webservices ws = new Webservices();
            Module1.Current.ModuleLogManager.LogDebug(nameof(ClipLayersAsync),
                "Contacting webservices server to retrieve layer metadata");
            IDictionary<string, dynamic> dictDataSources =
                await ws.QueryDataSourcesAsync(Module1.Current.Settings.m_eBagisServer);
            string strWsPrefix = dictDataSources[strDataType].uri;

            string strClipGdb = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Prism, false);
            string strClipFile = Constants.FILE_AOI_PRISM_VECTOR;
            
            if (!String.IsNullOrEmpty(strWsPrefix))
            {
                await QueuedTask.Run(() =>
                {
                    // if the buffer is different from PRISM, we need to create a new buffer file
                    string strTempBuffer = "tmpBuffer";
                    string strTempBuffer2 = "";
                    if (!strBufferDistance.Trim().Equals(prismBufferDistance.Trim()) || 
                        !strBufferUnits.Trim().Equals(prismBufferUnits.Trim()))
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
                               "Unable to buffer aoi_v. Error code: " + gpResult.Result.ErrorCode );
                            MessageBox.Show("Unable to buffer aoi_v. Clipping cancelled!!", "BAGIS-PRO");
                            return;
                        }

                        strClipFile = strTempBuffer;
                        if (strDataType.Equals(Constants.DATA_TYPE_PRECIPITATION))
                        {
                            // Copy the buffered file into the aoi.gdb
                            parameters = Geoprocessing.MakeValueArray(strOutputFeatures, GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Aoi, true) +
                                Constants.FILE_AOI_PRISM_VECTOR);
                            gpResult = Geoprocessing.ExecuteToolAsync("CopyFeatures", parameters, null,
                                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                            if (gpResult.Result.IsFailed)
                            {
                                Module1.Current.ModuleLogManager.LogError(nameof(ClipLayersAsync),
                                   "Unable to update p_aoi_v. Error code: " + gpResult.Result.ErrorCode);
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
                            MessageBox.Show("Unable to buffer aoi_v. Clipping cancelled!!", "BAGIS-PRO");
                            return;
                        }
                        strClipFile = strTempBuffer2;
                        Module1.Current.ModuleLogManager.LogDebug(nameof(ClipLayersAsync),
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
                                using (Feature feature = (Feature) cursor.Current)
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
                        MessageBox.Show("Unable obtain clipping envelope from " + strClipGdb + "\\" + strClipFile + " Clipping cancelled!!", "BAGIS-PRO");
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
                        Array.Resize<string>(ref arrClipUris, prismCount);
                        int j = 0;
                        foreach (var month in Enum.GetValues(typeof(PrismServiceNames)))
                        {
                            arrClipUris[j] = month.ToString();
                            j++;
                        }
                        Array.Resize<string>(ref arrClippedFileNames, prismCount);
                        j = 0;
                        foreach (var month in Enum.GetValues(typeof(PrismFile)))
                        {
                            arrClippedFileNames[j] = month.ToString();
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
                        var gpResult = Geoprocessing.ExecuteToolAsync("Clip_management", parameters, null,
                                        CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                        if (gpResult.Result.IsFailed)
                        {
                            Module1.Current.ModuleLogManager.LogError(nameof(ClipLayersAsync),
                               "Unable to clip " + strClipFile + ". Error code: " + gpResult.Result.ErrorCode);
                            MessageBox.Show("Unable to clip. Clipping cancelled!!", "BAGIS-PRO");
                            return;
                        }
                        else
                        {
                            Module1.Current.ModuleLogManager.LogDebug(nameof(ClipLayersAsync),
                                "Clipped " + arrClippedFileNames[i] + " layer");
                        }
                        i++;

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
                        var fc = ArcGIS.Desktop.Core.ItemFactory.Instance.Create(strOutputRaster,
                            ArcGIS.Desktop.Core.ItemFactory.ItemType.PathItem);
                        if (fc != null)
                        {
                            string strXml = string.Empty;
                            strXml = fc.GetXml();
                            System.Xml.XmlDocument xmlDocument = GeneralTools.UpdateMetadata(strXml, Constants.META_TAG_XPATH, sb.ToString(),
                                Constants.META_TAG_PREFIX.Length);

                            fc.SetXml(xmlDocument.OuterXml);
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
                });
            }
            success = BA_ReturnCode.Success;
            return success;
        }
    }


}
