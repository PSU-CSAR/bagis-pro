using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using Newtonsoft.Json.Linq;
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

        public static async Task<BA_ReturnCode> ClipLayersAsync(string strAoiPath, string strDataType,
            string prismBufferDistance, string prismBufferUnits, string strBufferDistance, string strBufferUnits)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;

            Webservices ws = new Webservices();
            Module1.Current.ModuleLogManager.LogDebug(nameof(ClipLayersAsync),
                "Contacting webservices server to retrieve layer metadata");
            IDictionary<string, dynamic> dictDataSources =
                await ws.QueryDataSourcesAsync(Module1.Current.Settings.m_eBagisServer);
            string strWsPrefix = dictDataSources[strDataType].uri;

            string[] arrLayersToDelete = new string[2];
            string strClipGdb = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Prism, false);
            string strClipFile = Constants.FILE_AOI_PRISM_VECTOR;

            if (!String.IsNullOrEmpty(strWsPrefix))
            {
                await QueuedTask.Run(async () =>
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
                               "Unable to buffer aoi_v. Error code: " + gpResult.Result.ErrorCode);
                            MessageBox.Show("Unable to buffer aoi_v. Clipping cancelled!!", "BAGIS-PRO");
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
                        arrLayersToDelete[1] = strTempBuffer;
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
                        var gpResult = await Geoprocessing.ExecuteToolAsync("Clip_management", parameters, environments,
                                        CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                        if (gpResult.IsFailed)
                        {
                            Module1.Current.ModuleLogManager.LogError(nameof(ClipLayersAsync),
                               "Unable to clip " + strClipFile + ". Error code: " + gpResult.ErrorCode);
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
                            var parameters = Geoprocessing.MakeValueArray(strClipGdb + "\\" + arrLayersToDelete[j]);
                            var gpResult = Geoprocessing.ExecuteToolAsync("Delete_management", parameters, null,
                                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                            if (gpResult.Result.IsFailed)
                            {
                                Module1.Current.ModuleLogManager.LogError(nameof(ClipLayersAsync),
                                    "Unable to delete " + strClipGdb + "\\" + arrLayersToDelete[j] + ". Error code: " + gpResult.Result.ErrorCode);
                                MessageBox.Show("Unable to delete " + strClipGdb + "\\" + arrLayersToDelete[j] + ".", "BAGIS-PRO");
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
                });
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
                        editOperation.Callback(context => {
                            foreach (BA_Objects.Interval interval in lstInterval)
                            {
                                oQueryFilter.WhereClause = " Value = " + interval.Value;
                                using (RowCursor rowCursor = rasterTable.Search(oQueryFilter, false))
                                {
                                    // Only one row should be returned
                                    rowCursor.MoveNext();
                                    using (Row row = (Row)rowCursor.Current)
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
                    LowerBound = lstIntervals.ElementAt(i-2).UpperBound,
                    UpperBound = lstIntervals.ElementAt(i-2).UpperBound + interval
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
            string strSourceLayer, int intZonesCount)
        {
            IList<BA_Objects.Interval> lstIntervals = new List<BA_Objects.Interval>();
            string strMessageKey = "PRISM";

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
            if (! strElevUnits.Equals(strDisplayUnits))
            {
                foreach (var nextInterval in lstIntervals)
                {
                    if (strDisplayUnits.Equals("Feet"))
                    {
                        nextInterval.LowerBound = LinearUnit.Feet.ConvertTo(nextInterval.LowerBound, LinearUnit.Meters);
                        nextInterval.UpperBound = LinearUnit.Feet.ConvertTo(nextInterval.UpperBound, LinearUnit.Meters);
                    }
                    else if (Module1.Current.Settings.m_demUnits.Equals("Feet"))
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
            string[] arrLayersToDelete = new string[2];
            string snotelClipLayer = "";
            string[] strOutputFc = new string[2];
            string[] strOutputLayer = { "tmpSno", "tmpSnowCos" };
            string strFinalSnotelOutputLayer = Constants.FILE_SNOTEL;
            string snowCosClipLayer = "";
            string strClipGdb = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Aoi, false);


            Webservices ws = new Webservices();
            Module1.Current.ModuleLogManager.LogDebug(nameof(ClipSnoLayersAsync),
                "Contacting webservices server to retrieve layer metadata");
            IDictionary<string, dynamic> dictDataSources =
                await ws.QueryDataSourcesAsync(Module1.Current.Settings.m_eBagisServer);

            var url = Module1.Current.Settings.m_settingsUri;
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
                    snotelClipLayer = await AnalysisTools.GetSnoClipLayer(strAoiPath, strClipGdb, snotelBufferDistance);
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
                        snowCosClipLayer = await AnalysisTools.GetSnoClipLayer(strAoiPath, strClipGdb, snowCosBufferDistance);
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

            // Add attribute fields
            foreach (var strFc in strOutputFc)
            {
                if (!String.IsNullOrEmpty(strFc))
                {
                    success = await GeoprocessingTools.AddFieldAsync(strFc, Constants.FIELD_SITE_NAME, "TEXT");
                    if (success == BA_ReturnCode.Success)
                    {
                        success = await GeoprocessingTools.AddFieldAsync(strFc, Constants.FIELD_SITE_ELEV, "DOUBLE");
                    }
                    if (success != BA_ReturnCode.Success)
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(ClipSnoLayersAsync),
                             "Unable to add fields to " + strFc);
                        MessageBox.Show("Unable to add fields to " + strFc + ". Clipping cancelled!!", "BAGIS-PRO");
                        return success;
                    }
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
                    int i = 0;
                    foreach (var strFc in strOutputFc)
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
                                                if (idxSource > -1)
                                                {
                                                    feature[idxTarget] = feature[idxSource];
                                                }
                                                feature.Store();
                                            // Has to be called after the store too
                                            context.Invalidate(feature);
                                            }
                                        }
                                    }
                                }, table);
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
                        }
                        i++;
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

            var environments = Geoprocessing.MakeEnvironmentArray(workspace: strAoiPath);
            string demPath = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Surfaces, true) +
                Constants.FILE_DEM_FILLED;
            string finalOutputPath = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Layers, true) + strFinalSnotelOutputLayer;
            var parameters = Geoprocessing.MakeValueArray(strOutputFc[0], demPath, finalOutputPath, "NONE", "ALL");
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


            return success;
        }

        public static async Task<string> GetSnoClipLayer(string strAoiPath, string strClipGdb, string strBufferDistance)
        {
            string strTempBuffer = "tmpBuffer";
            string strSnoClipLayer = "";
            string strLayerToDelete = "";
            string strAoiBoundaryPath = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Aoi, true) +
                Constants.FILE_AOI_VECTOR;
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
    }

}