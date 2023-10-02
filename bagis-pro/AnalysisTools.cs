using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Exceptions;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Data.UtilityNetwork.Trace;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Internal.Core.Conda;
using ArcGIS.Desktop.Internal.GeoProcessing;
using ArcGIS.Desktop.Mapping;
using bagis_pro.BA_Objects;
using Microsoft.Office.Interop.Excel;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
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
                long lngSites = await GeodatabaseTools.CountFeaturesAsync(sitesGdbUri, Constants.FILE_SNOTEL);
                if (lngSites > 0)
                    bHasSnotel = true;
                lngSites = await GeodatabaseTools.CountFeaturesAsync(sitesGdbUri, Constants.FILE_SNOLITE);
                if (lngSites > 0)
                {
                    bHasSnotel = true;
                }
                lngSites = await GeodatabaseTools.CountFeaturesAsync(sitesGdbUri, Constants.FILE_COOP_PILLOW);
                if (lngSites > 0)
                {
                    bHasSnotel = true;
                }
                lngSites = await GeodatabaseTools.CountFeaturesAsync(sitesGdbUri, Constants.FILE_SNOW_COURSE);
                if (lngSites > 0)
                    bHasSnowCourse = true;
                if (!bHasSnotel && !bHasSnowCourse)
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(GenerateSiteLayersAsync),
                        "No SNOTEL or Snow Course layers found for AOI. Site Layers cannot be generated!!");
                    return BA_ReturnCode.Success;
                }

                //1. Get min/max DEM elevation for reclassing raster. We only want to do this once
                Module1.Current.ModuleLogManager.LogDebug(nameof(GenerateSiteLayersAsync),
                    "START: GenerateSiteLayersAsync");
                Module1.Current.ModuleLogManager.LogDebug(nameof(GenerateSiteLayersAsync),
                    "GetDemStatsAsync");
                
                string sDemPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Surfaces, true) + Constants.FILE_DEM_CLIPPED;
                Uri uriAnalysis = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis));
                string sMask = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_VECTOR;
                IList<double> lstResult = await GeoprocessingTools.GetDemStatsAsync(Module1.Current.Aoi.FilePath, sMask, 0.005);
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
                    lstSites = await AnalysisTools.AssembleSitesListAsync(SiteType.Snotel, siteBufferDistanceMiles);
                    success = await AnalysisTools.CalculateRepresentedArea(sDemPath, demElevMinMeters, demElevMaxMeters, lstSites, siteElevRangeFeet, Constants.FILE_SNOTEL_REPRESENTED);
                    if (success != BA_ReturnCode.Success)
                        bHasSnotel = false;
                }

                // snow course sites
                if (bHasSnowCourse)
                {
                    lstSites = await AnalysisTools.AssembleSitesListAsync(SiteType.SnowCourse, siteBufferDistanceMiles);
                    success = await AnalysisTools.CalculateRepresentedArea(sDemPath, demElevMinMeters, demElevMaxMeters, lstSites, siteElevRangeFeet, Constants.FILE_SCOS_REPRESENTED);
                    if (success != BA_ReturnCode.Success)
                        bHasSnowCourse = false;
                }

                if (bHasSnotel || bHasSnowCourse)
                {
                    // record buffer distances, buffer units, elevation distance, and elevation units in metadata
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

        public static async Task<IList<BA_Objects.Site>> AssembleSitesListAsync(SiteType sType,
            double siteBufferDistanceMiles)
        {
            //2. Buffer point from feature class and query site information
            Uri layersUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false));

            IList<BA_Objects.Site> lstSites = new List<BA_Objects.Site>();
            // Open geodatabase for snotel sites
            await QueuedTask.Run(() =>
            {
                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(layersUri)))
                using (FeatureClass fClass = geodatabase.OpenDataset<FeatureClass>(Constants.FILE_MERGED_SITES))
                {
                    QueryFilter queryFilter = new QueryFilter();
                    if (sType == SiteType.Snotel)
                    {
                        string strInClause = $@" IN ('{SiteType.Snotel.ToString()}', '{SiteType.CoopPillow.ToString()}', '{SiteType.Snolite.ToString()}')";
                        queryFilter.WhereClause = Constants.FIELD_SITE_TYPE + strInClause;
                    }
                    else
                    {
                        queryFilter.WhereClause = Constants.FIELD_SITE_TYPE + " = '" + sType.ToString() + "'";
                    }                    
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

                            aSite.SiteTypeText = sType.ToString();
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
                    // Create SortDescription for SiteId field
                    FeatureClassDefinition featureClassDefinition = fClass.GetDefinition();
                    Field idField = featureClassDefinition.GetFields()
                        .First(x => x.Name.Equals(Constants.FIELD_SITE_ID));
                    SortDescription sortDescription = new SortDescription(idField)
                    {
                        SortOrder = SortOrder.Ascending
                    };

                    // Create our TableSortDescription
                    var tableSortDescription = new TableSortDescription(new List<SortDescription>() { sortDescription });
                    using (RowCursor cursor = fClass.Sort(tableSortDescription))
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
                            idx = nextFeature.FindField(Constants.FIELD_SITE_ID);
                            if (idx > -1)
                            {
                                aSite.SiteId = Convert.ToInt32(nextFeature[idx]);
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

        public static async Task<BA_ReturnCode> CalculateRepresentedArea(string demPath, double demElevMinMeters,
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
            BA_ReturnCode success = BA_ReturnCode.UnknownError;

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

                //string inputRasterPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Surfaces, true) + Constants.FILE_DEM_FILLED;
                string maskPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) + tmpBuffer;
                string outputRasterPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) + tmpOutputFile;

                //6. Execute the reclass with the mask set to the buffered point
                parameters = Geoprocessing.MakeValueArray(demPath, "VALUE", reclassString, outputRasterPath);
                environments = Geoprocessing.MakeEnvironmentArray(mask: maskPath, workspace: Module1.Current.Aoi.FilePath);
                gpResult = await Geoprocessing.ExecuteToolAsync("Reclassify_sa", parameters, environments,
                   CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (gpResult.IsFailed)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(CalculateRepresentedArea),
                        "Failed to execute reclass with mask set to buffered point: " + reclassString);
                    break;
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateRepresentedArea),
                        "Execute reclass with mask set to buffered point");
                    success = BA_ReturnCode.Success;
                }


                //7. Save the reclass as a poly so we can merge with other buffered site polys
                if (success == BA_ReturnCode.Success)
                {
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
                            "Created file site file named " + siteRepFileName);
                    }
                    else
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(CalculateRepresentedArea),
                            "Failed to convert raster to polygon for site file!");
                        success = BA_ReturnCode.UnknownError;
                        break;
                    }
                }
            }

            if (sb.Length > 0)
            {
                string inFeatures = sb.ToString().TrimEnd(';');
                Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateRepresentedArea),
                    "Site files to be merged " + inFeatures);
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
                    success = BA_ReturnCode.UnknownError;
                }

            }
            return success;
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
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '_')
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
            var response = new EsriHttpClient().Get(Constants.URI_DESKTOP_SETTINGS);
            var json = await response.Content.ReadAsStringAsync();
            dynamic oSettings = JObject.Parse(json);
            if (oSettings == null || String.IsNullOrEmpty(Convert.ToString(oSettings.gaugeStation)))
            {
                Module1.Current.ModuleLogManager.LogDebug(nameof(GetStationValues),
                    "Unable to retrieve gauge station uri from " + Constants.URI_DESKTOP_SETTINGS);
                MessageBox.Show("Unable to retrieve gauge station uri. Station values cannot be retrieved!!", "BAGIS-PRO");
                return null;
            }
            string strWsUri = (string) Module1.Current.BatchToolSettings.MasterAoiList;
            //string usgsServiceLayerId = strWsUri.Split('/').Last();
            //int intTrim = usgsServiceLayerId.Length + 1;
            //string usgsTempString = strWsUri.Substring(0, strWsUri.Length - intTrim);
            Uri wsUri = new Uri(strWsUri);

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

                if (!string.IsNullOrEmpty(strTriplet))
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(GetStationValues),
                    "Triplet retrieved from pourpoint feature class: " + strTriplet);
                }
                if (string.IsNullOrEmpty(strTriplet))
                {
                    // If triplet is null, use the near tool
                    BA_ReturnCode success = await GeoprocessingTools.NearAsync(strPourpointClassPath, strWsUri + "/0", Constants.VALUE_FORECAST_STATION_SEARCH_RADIUS);
                    if (success == BA_ReturnCode.Success)
                    {
                        QueryFilter queryFilter = new QueryFilter();
                        string strNearId = await GeodatabaseTools.QueryTableForSingleValueAsync(ppUri, Constants.FILE_POURPOINT,
                            Constants.FIELD_NEAR_ID, queryFilter);
                        string[] arrSearch = { Constants.FIELD_STATION_TRIPLET, Constants.FIELD_USGS_ID, (string)oSettings.gaugeStationName };
                        string[] arrFound = new string[arrSearch.Length];
                        if (!String.IsNullOrEmpty(strNearId))
                        {
                            queryFilter.WhereClause = Constants.FIELD_OBJECT_ID + " = " + strNearId;
                            arrFound = await ws.QueryServiceForValuesAsync(wsUri, "0", arrSearch, queryFilter);
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
                        "Triplet retrieved using the NEAR tool and AOI Master forecast list: " + strTriplet);
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

                    //Save the new values to aoi_v
                    string strAoiVPath = ppUri.LocalPath + "\\" + Constants.FILE_AOI_VECTOR;
                    string[] arrPpFields = { Constants.FIELD_STATION_TRIPLET, Constants.FIELD_STATION_NAME, Constants.FIELD_AWDB_ID };
                    foreach (var strField in arrPpFields)
                    {
                        if (!await GeodatabaseTools.AttributeExistsAsync(ppUri, Constants.FILE_AOI_VECTOR, strField))
                        {
                            success = await GeoprocessingTools.AddFieldAsync(strAoiVPath, strField, "TEXT");
                        }
                    }
                    if (success != BA_ReturnCode.Success)
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(GetStationValues),
                            "Unable to add 1 or more pourpoint fields to : " + strAoiVPath);
                    }
                    else
                    {
                        success = await GeodatabaseTools.UpdateFeatureAttributesAsync(ppUri, Constants.FILE_AOI_VECTOR,
                            new QueryFilter(), dictEdits);
                        if (success != BA_ReturnCode.Success)
                        {
                            Module1.Current.ModuleLogManager.LogError(nameof(GetStationValues),
                                "Unable to update 1 or more pourpoint fields to : " + strAoiVPath);
                        }
                    }
                }
            }
            arrReturnValues[0] = strTriplet;
            arrReturnValues[1] = strStationName;
            return arrReturnValues;
        }

        public static async Task<string[]> QueryLocalStationValues(string aoiFilePath)
        {
            string strTriplet = Constants.VALUE_NOT_SPECIFIED;
            string strTempHuc2 = "-1";
            string strStationName = Constants.VALUE_NOT_SPECIFIED;
            Uri ppUri = new Uri(GeodatabaseTools.GetGeodatabasePath(aoiFilePath, GeodatabaseNames.Aoi));
            string strPourpointClassPath = ppUri.LocalPath + "\\" + Constants.FILE_POURPOINT;
            if (await GeodatabaseTools.FeatureClassExistsAsync(ppUri, Constants.FILE_POURPOINT))
            {
                string[] arrFields = new string[] { Constants.FIELD_STATION_TRIPLET, Constants.FIELD_STATION_NAME, Constants.FIELD_HUC2 };
                foreach (string strField in arrFields)
                {
                    // Check for the field, if it exists query the value
                    if (await GeodatabaseTools.AttributeExistsAsync(ppUri, Constants.FILE_POURPOINT, strField))
                    {
                        QueryFilter queryFilter = new QueryFilter();
                        string strValue = await GeodatabaseTools.QueryTableForSingleValueAsync(ppUri, Constants.FILE_POURPOINT,
                            strField, queryFilter);
                        if (!string.IsNullOrEmpty(strValue))
                        {
                            switch (strField)
                            {
                                case Constants.FIELD_STATION_TRIPLET:
                                    strTriplet = strValue;
                                    break;
                                case Constants.FIELD_STATION_NAME:
                                    strStationName = strValue;
                                    break;
                                case Constants.FIELD_HUC2:
                                    strTempHuc2 = strValue;
                                    break;
                            }
                        }
                    }
                    // Add the field if it is missing
                    else
                    {
                        BA_ReturnCode success = BA_ReturnCode.UnknownError;
                        if (strField.Equals(Constants.FIELD_HUC2))
                        {
                            success = await GeoprocessingTools.AddFieldAsync(strPourpointClassPath, strField, "INTEGER");
                        }
                        else
                        {
                            success = await GeoprocessingTools.AddFieldAsync(strPourpointClassPath, strField, "TEXT");
                        }
                        if (success != BA_ReturnCode.Success)
                        {
                            Module1.Current.ModuleLogManager.LogError(nameof(QueryLocalStationValues), 
                                $@"Unable to add field {strField} to {strPourpointClassPath}");

                        }
                    }
                }
            }
            else
            {
                Module1.Current.ModuleLogManager.LogError(nameof(QueryLocalStationValues), 
                    "Unable to locate pourpoint feature class: " + strPourpointClassPath);

            }

            string[] arrReturnValues = new string[] { strTriplet, strStationName, strTempHuc2 };
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
                        if (string.IsNullOrEmpty(strBufferDistance))
                        {
                            strBufferDistance = "0";
                        }
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
                                if (strDataType.Equals(BA_Objects.DataSource.GetPrecipitationKey))
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
                            long featureCount = 0;
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
                        if (strDataType.Equals(BA_Objects.DataSource.GetPrecipitationKey))
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
                        bool bIsNoData = false;
                        foreach (string strUri in arrClipUris)
                        {
                            Uri imageServiceUri = new Uri(strWsPrefix + strUri + Constants.URI_IMAGE_SERVER);
                            string strOutputRaster = strOutputGdb + arrClippedFileNames[i];
                            string strTemplateDataset = strClipGdb + "\\" + strClipFile;
                            var environments = Geoprocessing.MakeEnvironmentArray(workspace: strAoiPath, snapRaster: BA_Objects.Aoi.SnapRasterPath(strAoiPath),
                                extent: strClipEnvelope);
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
                                // Check for NoData
                                if (i==0)   // Check the first raster only
                                {
                                    bIsNoData = await IsNoDataRasterAsync(strAoiPath, strOutputGdb, arrClippedFileNames[i]);
                                    if (bIsNoData)
                                    {
                                        intError++;
                                        break;
                                    }
                                }
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
                        if (bIsNoData == false)
                        {
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
                return BA_ReturnCode.UnknownError;
            }
            return BA_ReturnCode.Success;
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

            double dblMin = 9999;
            double dblMax = -9999;
            double dblInterval = 999;
            bool bTooSmall = false;

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
                    Module1.Current.ModuleLogManager.LogError(nameof(GetPrismClassesAsync),
                        "Unable to calculate " + strMessageKey + " maximum");
                    return;
                }
                double dblRange = dblMax - dblMin;
                if (dblRange < intZonesCount)
                {
                    dblInterval = 0.5;
                    bTooSmall = true;
                }
                else
                {
                    // determine interval value based on desired # map classes
                    dblInterval = dblRange / intZonesCount;
                    // round the number to 1 decimal places
                    dblInterval = Math.Round(dblInterval);
                }
            });
            if (dblMin == 9999 || dblMax == -9999)
            {
                Module1.Current.PrismZonesInterval = -1;
                return null;
            }
            int zones = GeneralTools.CreateRangeArray(dblMin, dblMax, dblInterval, out lstIntervals);
            // issue #18
            // Check to be sure the highest interval isn't too small; if it is merge into the second-to-last
            if (lstIntervals.Count > 0)
            {
                double minInterval = 1;
                if (bTooSmall)
                {
                    minInterval = 0.5;
                }
                var firstInterval = lstIntervals[0];
                if (firstInterval.UpperBound - firstInterval.LowerBound < minInterval)
                {
                    var secondInterval = lstIntervals[1];
                    secondInterval.LowerBound = firstInterval.LowerBound;
                    secondInterval.Name = secondInterval.LowerBound + " - " + secondInterval.UpperBound;
                    lstIntervals.Remove(firstInterval);
                    zones = zones - 1;
                }
                var lastInterval = lstIntervals[lstIntervals.Count - 1];
                if (lastInterval.UpperBound - lastInterval.LowerBound < minInterval)
                {
                    var almostLastInterval = lstIntervals[lstIntervals.Count - 2];
                    almostLastInterval.UpperBound = lastInterval.UpperBound;
                    almostLastInterval.Name = almostLastInterval.LowerBound + " - " + almostLastInterval.UpperBound;
                    lstIntervals.Remove(lastInterval);
                    zones = zones - 1;
                }
            }
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
            string snotelClipLayer = "";
            string[] strOutputFc = new string[4];
            string[] strOutputLayer = { "tmpSno", "tmpSnowCos", "tmpSnoLite", "tmpSnowPil" };
            string[] strFinalOutputLayer = { Constants.FILE_SNOTEL, Constants.FILE_SNOW_COURSE, Constants.FILE_SNOLITE, Constants.FILE_COOP_PILLOW };
            bool[] bHasSites = { false, false, false, false };
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
                    !dictDataSources.ContainsKey(Constants.DATA_TYPE_SNOW_COURSE) ||
                    !dictDataSources.ContainsKey(Constants.DATA_TYPE_SNOLITE) ||
                    !dictDataSources.ContainsKey(Constants.DATA_TYPE_COOP_PILLOW))
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(ClipSnoLayersAsync),
                        "Unable to retrieve snotel datasource information from " + (string) Module1.Current.BatchToolSettings.EBagisServer +
                        ". Clipping cancelled!!");
                    return success;
                }
            }
            else
            {
                Module1.Current.ModuleLogManager.LogError(nameof(ClipSnoLayersAsync),
                    "Unable to retrieve datasource information from " + (string)Module1.Current.BatchToolSettings.EBagisServer +
                    ". Clipping cancelled!!");
                return success;
            }

            var response = new EsriHttpClient().Get(Constants.URI_DESKTOP_SETTINGS);
            var json = await response.Content.ReadAsStringAsync();
            dynamic oSettings = JObject.Parse(json);
            if (oSettings == null || String.IsNullOrEmpty(Convert.ToString(oSettings.snowCourseName)))
            {
                Module1.Current.ModuleLogManager.LogError(nameof(ClipSnoLayersAsync),
                    "Unable to retrieve snotel settings from " + Constants.URI_DESKTOP_SETTINGS + " .Clipping cancelled!!");
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
                            "Unable to calculate snotel clip layer. Clipping cancelled!!");
                        return success;
                    }
                }
                else
                {
                    snotelBufferDistance = "0 Meters";
                    snotelClipLayer = await AnalysisTools.GetSnoClipLayer(strAoiPath, strClipGdb, strTempBuffer, snotelBufferDistance);
                }

                // Delete the existing snotel layer, snotel represented area, snotel zones, snolite, and snow pillow layers
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
                if (await GeodatabaseTools.RasterDatasetExistsAsync(uriAnalysis, strFinalOutputLayer[2]))
                {
                    success = await GeoprocessingTools.DeleteDatasetAsync(strLayers + "\\" + strFinalOutputLayer[2]);
                }
                if (await GeodatabaseTools.RasterDatasetExistsAsync(uriAnalysis, strFinalOutputLayer[3]))
                {
                    success = await GeoprocessingTools.DeleteDatasetAsync(strLayers + "\\" + strFinalOutputLayer[3]);
                }

                string strWsUri = dictDataSources[Constants.DATA_TYPE_SNOTEL].uri;
                strOutputFc[0] = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Layers, true) + strOutputLayer[0];
                strOutputFc[2] = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Layers, true) + strOutputLayer[2];
                strOutputFc[3] = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Layers, true) + strOutputLayer[3];
                string strTemplateDataset = strClipGdb + "\\" + snotelClipLayer;
                // SNOTEL
                var environmentsClip = Geoprocessing.MakeEnvironmentArray(workspace: strAoiPath);
                var parametersClip = Geoprocessing.MakeValueArray(strWsUri, strTemplateDataset, strOutputFc[0], "");
                var gpResultClip = await Geoprocessing.ExecuteToolAsync("Clip_analysis", parametersClip, environmentsClip,
                                        CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (gpResultClip.IsFailed)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(ClipSnoLayersAsync),
                       "Unable to clip " + strOutputFc[0] + ". Error code: " + gpResultClip.ErrorCode);
                    return success;
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(ClipSnoLayersAsync),
                        "Clipped " + strOutputFc[0] + " layer");
                    long lngSites = await GeodatabaseTools.CountFeaturesAsync(uriLayers, strOutputLayer[0]);
                    if (lngSites > 0)
                    {
                        bHasSites[0] = true;
                    }
                }
                // SNOLITE
                strWsUri = dictDataSources[Constants.DATA_TYPE_SNOLITE].uri;
                parametersClip = Geoprocessing.MakeValueArray(strWsUri, strTemplateDataset, strOutputFc[2], "");
                gpResultClip = await Geoprocessing.ExecuteToolAsync("Clip_analysis", parametersClip, environmentsClip,
                                        CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (gpResultClip.IsFailed)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(ClipSnoLayersAsync),
                       "Unable to clip " + strOutputFc[2] + ". Error code: " + gpResultClip.ErrorCode);
                    return success;
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(ClipSnoLayersAsync),
                        "Clipped " + strOutputFc[2] + " layer");
                    long lngSites = await GeodatabaseTools.CountFeaturesAsync(uriLayers, strOutputLayer[2]);
                    if (lngSites > 0)
                    {
                        bHasSites[2] = true;
                    }
                }
                // Snow pillow
                strWsUri = dictDataSources[Constants.DATA_TYPE_COOP_PILLOW].uri;
                parametersClip = Geoprocessing.MakeValueArray(strWsUri, strTemplateDataset, strOutputFc[3], "");
                gpResultClip = await Geoprocessing.ExecuteToolAsync("Clip_analysis", parametersClip, environmentsClip,
                                        CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (gpResultClip.IsFailed)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(ClipSnoLayersAsync),
                       "Unable to clip " + strOutputFc[3] + ". Error code: " + gpResultClip.ErrorCode);
                    return success;
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(ClipSnoLayersAsync),
                        "Clipped " + strOutputFc[3] + " layer");
                    long lngSites = await GeodatabaseTools.CountFeaturesAsync(uriLayers, strOutputLayer[3]);
                    if (lngSites > 0)
                    {
                        bHasSites[3] = true;
                    }
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
                                "Unable to calculate snow course clip layer. Clipping cancelled!!");
                            return success;
                        }
                    }
                }
                else
                {
                    snowCosBufferDistance = "0 Meters";
                    snowCosClipLayer = await AnalysisTools.GetSnoClipLayer(strAoiPath, strClipGdb, strTempBuffer, snowCosBufferDistance);
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
                    return success;
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(ClipSnoLayersAsync),
                        "Clipped " + snowCosClipLayer + " layer");
                    long lngSites = await GeodatabaseTools.CountFeaturesAsync(uriLayers, strOutputLayer[1]);
                    if (lngSites > 0)
                    {
                        bHasSites[1] = true;
                    }
                }
            }

            // Delete the temporary buffer layer; This does not error if the buffer doesn't exist
            success = await GeoprocessingTools.DeleteDatasetAsync(strClipGdb + "\\" + strTempBuffer);

            // Add attribute fields
            int snotelCount = 0;
            int snowCosCount = 0;
            for (int i = 0; i < strOutputFc.Length; i++)
            {
                string strFc = strOutputFc[i];
                if (bHasSites[i])
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
                    //}

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
                                                    string siteType = SiteType.Missing.ToString();
                                                    switch (i)
                                                    {
                                                        case 0:
                                                            siteType = SiteType.Snotel.ToString();
                                                            break;
                                                        case 1:
                                                            siteType = SiteType.SnowCourse.ToString();
                                                            break;
                                                        case 2:
                                                            siteType = SiteType.Snolite.ToString();
                                                            break;
                                                        case 3:
                                                            siteType = SiteType.CoopPillow.ToString();
                                                            break;
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
                }
            }

            // Extract the elevation from the DEM for the sites
            int j = 0;
            string finalOutputPath = "";
            foreach (var strFc in strOutputFc)
            {
                // Make sure the feature class has sites before trying to extract the elevation
                if (bHasSites[j] == false)
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(ClipSnoLayersAsync),
                    "This has no sites. Elevation cannot be extracted");
                }
                else
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
                    if (j==1)   // Working with snow course layer
                    {
                        arrPieces = snowCosBufferDistance.Split(' ');
                    }
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
                    switch (j)
                    {
                        case 1:
                            strKey = Constants.DATA_TYPE_SNOW_COURSE;
                            break;
                        case 2:
                            strKey = Constants.DATA_TYPE_SNOLITE;
                            break;
                        case 3:
                            strKey = Constants.DATA_TYPE_COOP_PILLOW;
                            break;
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
            string strSnoClipLayer = Constants.FILE_AOI_BUFFERED_VECTOR;
            string strLayerToDelete = "";
            string strAoiBoundaryPath = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Aoi, true) +
                Constants.FILE_AOI_VECTOR;
            if (string.IsNullOrEmpty(strBufferDistance))
            {
                strBufferDistance = "0 Meters";
            }
            // Check to see if the buffer distance is the same as the AOI; If so, we don't need to create a new buffer vector
            bool bNewBufferFile = true;
            string[] arrResult = await GeneralTools.QueryBufferDistanceAsync(strAoiPath, strClipGdb, Constants.FILE_AOI_BUFFERED_VECTOR, false);
            string strTestDistance = arrResult[0] +" " + arrResult[1];
            if (strBufferDistance.Equals(strTestDistance))
            {
                bNewBufferFile = false;
            }
            if (bNewBufferFile)
            {
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
                long featureCount = 0;
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
            }
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
                        // Check to see if the buffer distance is the same as the AOI; If so, we don't need to create a new buffer vector
                        if (string.IsNullOrEmpty(strBufferDistance))
                        {
                            strBufferDistance = "0";
                        }
                        bool bNewBufferFile = true;
                        string[] arrResult = await GeneralTools.QueryBufferDistanceAsync(strAoiPath, strClipGdb, strClipFile, false);
                        if (arrResult[0].Equals(strBufferDistance) && arrResult[1].Equals(strBufferUnits))
                        {
                            bNewBufferFile = false;
                        }
                        string strTempBuffer = "tmpBuffer";
                        // a buffer distance was requested
                        string strTempBuffer2 = "";
                        if (bNewBufferFile)
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
                        long featureCount = 0;
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
                            success = await GeoprocessingTools.BufferAsync(strClipGdb + "\\" + strClipFile,
                                strClipGdb + "\\" + strTempBuffer2, "0.5 Meters", "ALL");
                            if (success != BA_ReturnCode.Success)
                            {
                                Module1.Current.ModuleLogManager.LogError(nameof(ClipFeatureLayerAsync),
                                   "Unable to buffer " + strClipFile + "!!");
                                MessageBox.Show("Unable to buffer aoi_v. Clipping cancelled!!", "BAGIS-PRO");
                                return;
                            }
                            strClipFile = strTempBuffer2;
                            arrLayersToDelete[1] = strTempBuffer;
                            Module1.Current.ModuleLogManager.LogDebug(nameof(ClipFeatureLayerAsync),
                                "Ran buffer tool again because clip file has > 2 features");
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
            bool bExists = await GeodatabaseTools.FeatureClassExistsAsync(uriLayers, Constants.FILE_LAND_OWNERSHIP);
            if (!bExists)
            {
                Module1.Current.ModuleLogManager.LogDebug(nameof(GetFederalNonWildernessLandsAsync),
                    "Unable to extract federal non-wilderness lands because land_ownership layer does not exist. Process stopped!!");
                return success;
            }
            string strInputFc = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Layers, true) + Constants.FILE_LAND_OWNERSHIP;
            Uri uriFull = new Uri(strInputFc);
            // Check for attribute before trying to run query
            bExists = await GeodatabaseTools.AttributeExistsAsync(uriLayers, Constants.FILE_LAND_OWNERSHIP, Constants.FIELD_SUITABLE);
            var oMap = await MapTools.SetDefaultMapNameAsync(Constants.MAPS_DEFAULT_MAP_NAME);
            if (bExists && oMap != null)
            {
                await QueuedTask.Run(() =>
                {
                    // Create feature layer so we can use definition query to select public lands
                    // Migrate from 2.x
                    //var slectionLayer = LayerFactory.Instance.CreateFeatureLayer(uriFull, MapView.Active.Map, 0, "Selection Layer");
                    var flyrCreatnParam = new FeatureLayerCreationParams(uriFull)
                    {
                        Name = "Selection Layer",
                        IsVisible = false,
                        MapMemberIndex = 0,
                        MapMemberPosition = 0,
                    };
                    var slectionLayer = LayerFactory.Instance.CreateLayer<FeatureLayer>(flyrCreatnParam, MapView.Active.Map);
                    slectionLayer.SetDefinitionQuery(Constants.FIELD_SUITABLE + " = 1");
                    // Merge features into a single feature for display and analysis
                    var environments = Geoprocessing.MakeEnvironmentArray(workspace: strAoiPath);
                    var parameters = Geoprocessing.MakeValueArray(slectionLayer, GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Analysis, true) + Constants.FILE_PUBLIC_LAND_ZONE,
                        Constants.FIELD_SUITABLE, "", "MULTI_PART", "DISSOLVE_LINES");
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
                    oMap.RemoveLayer(slectionLayer);
                    Module1.Current.ModuleLogManager.LogDebug(nameof(GetFederalNonWildernessLandsAsync),
                        "Dissolved public lands layer");
                });
            }
            else
            {
                Module1.Current.ModuleLogManager.LogError(nameof(GetFederalNonWildernessLandsAsync),
                    Constants.FIELD_SUITABLE + " missing from " + Constants.FILE_LAND_OWNERSHIP + 
                    " . Federal non-wilderness lands layer cannot be created!");
                success = BA_ReturnCode.ReadError;
            }
            return success;
        }

        public static async Task<BA_ReturnCode> ExtractForestedAreaAsync(string strAoiPath)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            Uri uriLayers = new Uri(GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Layers));
            bool bExists = await GeodatabaseTools.RasterDatasetExistsAsync(uriLayers, Constants.FILE_LAND_COVER);
            if (!bExists)
            {
                Module1.Current.ModuleLogManager.LogDebug(nameof(ExtractForestedAreaAsync),
                    "Unable to extract forested area because " + Constants.FILE_LAND_COVER + " layer does not exist. Process stopped!!");
                return success;
            }
            string strInputRaster = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Layers, true) + Constants.FILE_LAND_COVER;
            Uri uriFull = new Uri(strInputRaster);
            await QueuedTask.Run(() =>
            {
                // Convert vegetation raster to vector using VALUE
                var environments = Geoprocessing.MakeEnvironmentArray(workspace: strAoiPath);
                string strAnalysisGdb = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Analysis);
                string strTempVector = "temp_nlcd";
                var parameters = Geoprocessing.MakeValueArray(strInputRaster, strAnalysisGdb + "\\" + strTempVector,
                    "SIMPLIFY", Constants.FIELD_VALUE);
                var gpResult = Geoprocessing.ExecuteToolAsync("RasterToPolygon_conversion", parameters, environments,
                                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (gpResult.Result.IsFailed)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(ExtractForestedAreaAsync),
                    "Unable to convert NLCD raster to polygon. Error code: " + gpResult.Result.ErrorCode);
                    return;
                }

                // Create feature layer so we can use definition query to select public lands
                var uriTemp = new Uri(strAnalysisGdb + "\\" + strTempVector);
                //Migrate from 2.x
                //var slectionLayer = LayerFactory.Instance.CreateFeatureLayer(uriTemp, MapView.Active.Map, 0, "Selection Layer");
                var flyrCreatnParam = new FeatureLayerCreationParams(uriTemp)
                {
                    Name = "Selection Layer",
                    IsVisible = false,
                    MapMemberIndex = 0,
                    MapMemberPosition = 0,
                };
                var slectionLayer = LayerFactory.Instance.CreateLayer<FeatureLayer>(flyrCreatnParam, MapView.Active.Map);
                slectionLayer.SetDefinitionQuery(Constants.FIELD_GRID_CODE + " IN (41, 42, 43)");
                string dissolveOutputPath = strAnalysisGdb + "\\" + Constants.FILE_FORESTED_ZONE;
                // Copy selected features to a new, temporary feature class
                environments = Geoprocessing.MakeEnvironmentArray(workspace: strAoiPath);
                parameters = Geoprocessing.MakeValueArray(slectionLayer, dissolveOutputPath, Constants.FIELD_GRID_CODE);
                gpResult = Geoprocessing.ExecuteToolAsync("Dissolve_management", parameters, environments,
                                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);

                if (gpResult.Result.IsFailed)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(ExtractForestedAreaAsync),
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
                    Module1.Current.ModuleLogManager.LogError(nameof(ExtractForestedAreaAsync),
                        "Failed to delete temporary forested area layer");
                    return;
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(ExtractForestedAreaAsync),
                        "Deleted temporary vegetation forested area layer");
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
            var oMap = await MapTools.SetDefaultMapNameAsync(Constants.MAPS_DEFAULT_MAP_NAME);
            await QueuedTask.Run(() =>
            {
                // Create feature layer so we can use definition query to select public lands
                var uriTemp = new Uri(uriAnalysisGdb.LocalPath + "\\" + Constants.FILE_ELEV_ZONES_VECTOR);
                //Migrate from 2.x
                //var slectionLayer = LayerFactory.Instance.CreateFeatureLayer(uriTemp, MapView.Active.Map, 0, "Selection Layer");
                var flyrCreatnParam = new FeatureLayerCreationParams(uriTemp)
                {
                    Name = "Selection Layer",
                    IsVisible = false,
                    MapMemberIndex = 0,
                    MapMemberPosition = 0,
                };
                var slectionLayer = LayerFactory.Instance.CreateLayer<FeatureLayer>(flyrCreatnParam, oMap);
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
                oMap.RemoveLayer(slectionLayer);
                success = BA_ReturnCode.Success;
            });
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
            double dblDemCellSize = await GeodatabaseTools.GetCellSizeAsync(uriSurfaces, Constants.FILE_DEM_CLIPPED);
            double dblPrismCellSize = await GeodatabaseTools.GetCellSizeAsync(uriPrism, prismFile);
            if (dblPrismCellSize < 0)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(CalculateElevPrecipCorrAsync),
                    "Unable to extract Prism cell size. Calculation cancelled!");
                return success;
            }
            string demPath = uriSurfaces.LocalPath + "\\" + Constants.FILE_DEM_CLIPPED;
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
                    uriAnalysis.LocalPath + "\\" + Constants.FILE_PREC_MEAN_ELEV_V, "NEAREST", Constants.FIELD_VALUE, "CURRENT_SLICE",
                    "","","","", "ROW_WISE", "FEATURE_CLASS");
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

                success = await GeoprocessingTools.AddFieldAsync(uriAnalysis.LocalPath + "\\" + Constants.FILE_PREC_MEAN_ELEV_V, Constants.FIELD_DIRECTION, "TEXT");
                if (success == BA_ReturnCode.Success)
                {
                        success = await UpdateAspectDirectionsFromZonesAsync(uriAnalysis, Constants.FILE_PREC_MEAN_ELEV_V,
                            lstAspectInterval, Constants.FIELD_SAMPLE_INPUT_1);
                        if (success != BA_ReturnCode.Success)
                    {
                        MessageBox.Show("Unable to update aspect directions in " + Constants.FILE_PREC_MEAN_ELEV_V
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
            IList<BA_Objects.Interval> lstAspectInterval, string fieldAspect)
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
                    int idxAspectDirect = defTable.FindField(Constants.FIELD_DIRECTION);
                    int idxAspect = defTable.FindField(fieldAspect);
                    if (idxAspectDirect < 0 || idxAspect < 0)
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(UpdateAspectDirectionsAsync),
                            "Unable to locate BA_ASPECT field in " + strUpdateFc + ". Cannot update field");
                         return;
                    }
                    QueryFilter queryFilter = new QueryFilter();
                    editOperation.Callback(context =>
                    {
                        using (RowCursor aCursor = table.Search(queryFilter, false))
                        {
                            while (aCursor.MoveNext())
                            {
                                using (Row row = aCursor.Current)
                                {
                                    double dblAspect = Convert.ToDouble(row[idxAspect]);
                                    foreach (var interval in lstAspectInterval)
                                    {
                                        if (dblAspect > interval.LowerBound && dblAspect <= interval.UpperBound)
                                        {
                                            row[idxAspectDirect] = interval.Name;
                                            row.Store();
                                            // Has to be called after the store too
                                            context.Invalidate(row);
                                            break;
                                        }
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

        private static async Task<BA_ReturnCode> UpdateAspectDirectionsFromZonesAsync(Uri uriAnalysis, string strUpdateFc,
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
            bool bClipFromLayer = false;
            if (strDataType == Constants.DATA_TYPE_ALASKA_LAND_COVER)
            {
                bClipFromLayer = true;
            }

            Webservices ws = new Webservices();
            Module1.Current.ModuleLogManager.LogDebug(nameof(ClipLayersAsync),
                "Contacting webservices server to retrieve layer metadata");
            IDictionary<string, dynamic> dictDataSources =
                await ws.QueryDataSourcesAsync((string)Module1.Current.BatchToolSettings.EBagisServer);
            string strWsUri = dictDataSources[strDataType].uri;
            string strInputRaster = strWsUri;
            string[] arrLayersToDelete = new string[2];
            string strClipGdb = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Aoi, false);
            string strClipFile = Constants.FILE_AOI_BUFFERED_VECTOR;
            IReadOnlyList<string> parameters = null;
            Task<IGPResult> gpResult = null;

            if (!String.IsNullOrEmpty(strWsUri))
            {
                await QueuedTask.Run(async () =>
                {
                    // Check to see if the buffer distance is the same as the AOI; If so, we don't need to create a new buffer vector
                    bool bNewBufferFile = true;
                    if (string.IsNullOrEmpty(strBufferDistance))
                    {
                        strBufferDistance = "0";
                    }
                    string[] arrResult = await GeneralTools.QueryBufferDistanceAsync(strAoiPath, strClipGdb, strClipFile, false);
                    if (arrResult[0].Equals(strBufferDistance) && arrResult[1].Equals(strBufferUnits))
                    {
                        bNewBufferFile = false;
                    }
                    string strTempBuffer = "tmpBuffer";
                    string strTempBuffer2 = "";
                    if (bNewBufferFile)
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
                            return;
                        }

                        strClipFile = strTempBuffer;
                        arrLayersToDelete[0] = strTempBuffer;
                    }

                    // Check to make sure the buffer file only has one feature; No dangles
                    long featureCount = 0;
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
                        return;
                    }

                    if (bClipFromLayer)
                    {
                        strInputRaster = "ClipRasterSource";
                        Uri uri = new Uri(strWsUri);
                        success = await MapTools.DisplayMapServiceLayerAsync(Constants.MAPS_DEFAULT_MAP_NAME, uri, strInputRaster, false);
                    }
                    string strTemplateDataset = strClipGdb + "\\" + strClipFile;
                    var environments = Geoprocessing.MakeEnvironmentArray(workspace: strAoiPath, snapRaster: BA_Objects.Aoi.SnapRasterPath(strAoiPath),
                        extent: strClipEnvelope);
                    parameters = Geoprocessing.MakeValueArray(strInputRaster, strClipEnvelope, strOutputRaster, strTemplateDataset,
                                        "", "ClippingGeometry");
                    var finalResult = await Geoprocessing.ExecuteToolAsync("Clip_management", parameters, environments,
                                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                    if (bClipFromLayer)
                    {
                        // Remove the temp layer
                        var oMap = await MapTools.SetDefaultMapNameAsync(Constants.MAPS_DEFAULT_MAP_NAME);
                        Layer oLayer =
                            oMap.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(strInputRaster, StringComparison.CurrentCultureIgnoreCase));
                        if (oLayer != null)
                        {
                            oMap.RemoveLayer(oLayer);
                        }
                    }
                    if (finalResult.IsFailed)
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(ClipRasterLayerAsync),
                           "Unable to clip " + strClipFile + ". Error code: " + finalResult.ErrorCode);
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
            string sMask = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_VECTOR;
            IList<double> lstResult = await GeoprocessingTools.GetDemStatsAsync(aoiFilePath, sMask, 0.005);
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
                Constants.FILE_DEM_CLIPPED;
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
                Module1.Current.ModuleLogManager.LogError(nameof(CalculateElevationZonesAsync),
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
                //Calculate and record overall min and max for symbology
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

                        // Create classifed layers
                        int idxDefaultMonth = 8;
                        IList<BA_Objects.Interval> lstInterval = await MapTools.CalculateSweZonesAsync(idxDefaultMonth);
                        if (lstInterval.Count > 0)
                        {
                            string strAnalysisGdb = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis);
                            for (int j = 0; j < Constants.FILES_SNODAS_SWE.Length; j++)
                            {
                                string strInput = $@"{GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers)}\{Constants.FILES_SNODAS_SWE[j]}";
                                string strOutput = strAnalysisGdb + "\\" + Constants.FILES_SWE_ZONES[j];
                                string strMaskPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_PRISM_VECTOR;
                                success = await AnalysisTools.CalculateZonesAsync(Module1.Current.Aoi.FilePath, strInput, lstInterval,
                                    strOutput, strMaskPath, "SWE SNODAS");
                            }
                        }
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
                string[] arrSiteFileNames = { Constants.FILE_PUBLIC_LAND_ZONE, Constants.FILE_ROADS_ZONE, Constants.FILE_FORESTED_ZONE };
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
                string sMask = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_VECTOR;
                IList<double> lstResult = await GeoprocessingTools.GetDemStatsAsync(aoiFolderPath, sMask, 0.005);
                string sDemPath = GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Surfaces, true) +
                            Constants.FILE_DEM_CLIPPED;
                double demElevMinMeters = -1;
                double demElevMaxMeters = -1;
                Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateSitesZonesAsync), "Get min and max elevation from " + sDemPath);

                if (lstResult.Count == 2)   // We expect the min and max values in that order
                {
                    demElevMinMeters = lstResult[0];
                    demElevMaxMeters = lstResult[1];
                }
                // We assume that the DEM and site elevations are both in meters
                Uri uriAnalysis = new Uri(GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Analysis));
                IList<BA_Objects.Interval> lstInterval = null;
                string strZonesRaster = null;
                string strMaskPath = GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Surfaces, true) + Constants.FILE_DEM_CLIPPED;
                if (hasSnotel)
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateSitesZonesAsync), "Begin create Snotel zone");
                    lstInterval = await GeodatabaseTools.GetUniqueSortedValuesAsync(uriAnalysis, SiteType.Snotel,
                        Constants.FIELD_SITE_ELEV, Constants.FIELD_SITE_NAME, demElevMaxMeters, demElevMinMeters);
                    if (lstInterval.Count > 0)
                    {
                        strZonesRaster = GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Analysis, true) +
                            Constants.FILE_SNOTEL_ZONE;
                        success = await AnalysisTools.CalculateZonesAsync(aoiFolderPath, sDemPath,
                            lstInterval, strZonesRaster, strMaskPath, "SNOTEL");
                        if (success == BA_ReturnCode.Success)
                        {
                            Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateSitesZonesAsync), "Snotel zones created");
                        }
                    }
                    else
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(CalculateSitesZonesAsync), "Could not create interval list. Snotel zones NOT created");
                    }
                }
                if (hasSnowCourse)
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateSitesZonesAsync), "Begin create Snow Course zone");
                    lstInterval = await GeodatabaseTools.GetUniqueSortedValuesAsync(uriAnalysis, SiteType.SnowCourse,
                        Constants.FIELD_SITE_ELEV, Constants.FIELD_SITE_NAME, demElevMaxMeters, demElevMinMeters);
                    if (lstInterval.Count > 0)
                    {
                        strZonesRaster = GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Analysis, true) +
                            Constants.FILE_SCOS_ZONE;
                        success = await AnalysisTools.CalculateZonesAsync(aoiFolderPath, sDemPath,
                            lstInterval, strZonesRaster, strMaskPath, "SNOW COURSE");
                        if (success == BA_ReturnCode.Success)
                        {
                            Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateSitesZonesAsync), "Snow course zones created");
                        }
                    }
                    else
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(CalculateSitesZonesAsync), "Could not create interval list. Snow course zones NOT created");
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

        public static async Task<BA_ReturnCode> CalculatePrecipitationZonesAsync(string strLayer, string strZonesRaster)
        {
            string strMaskPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_PRISM_VECTOR;
            int prismZonesCount = (int)Module1.Current.BatchToolSettings.PrecipZonesCount;
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            IList<BA_Objects.Interval> lstInterval = await AnalysisTools.GetPrismClassesAsync(Module1.Current.Aoi.FilePath,
                strLayer, prismZonesCount, "PRISM");
            if (lstInterval != null)
            {
                success = await AnalysisTools.CalculateZonesAsync(Module1.Current.Aoi.FilePath, strLayer,
                    lstInterval, strZonesRaster, strMaskPath, "PRISM");
                string zonesFile = Path.GetFileName(strZonesRaster);
                if (success == BA_ReturnCode.Success && !Constants.FILE_WINTER_PRECIPITATION_ZONE.Equals(zonesFile))
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
                    string strPrecipFile = Path.GetFileName(strLayer);
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
                long lngSites = await GeodatabaseTools.CountFeaturesAsync(sitesGdbUri, Constants.FILE_SNOTEL);
                if (lngSites > 0)
                    bHasSnotel = true;
                lngSites = await GeodatabaseTools.CountFeaturesAsync(sitesGdbUri, Constants.FILE_SNOW_COURSE);
                if (lngSites > 0)
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
                            bMergedSitesExists = false;
                        }
                    }
                    else
                    {
                        // If it exists, check to make sure the direction and prism fields exist
                        bool bDirectionField = await GeodatabaseTools.AttributeExistsAsync(uriAnalysis, Constants.FILE_MERGED_SITES, Constants.FIELD_DIRECTION);
                        bool bAspectField = await GeodatabaseTools.AttributeExistsAsync(uriAnalysis, Constants.FILE_MERGED_SITES, Constants.FIELD_ASPECT);
                        bool bSiteIdField = await GeodatabaseTools.AttributeExistsAsync(uriAnalysis, Constants.FILE_MERGED_SITES, Constants.FIELD_SITE_ID);
                        if (!bDirectionField || !bAspectField || !bSiteIdField)
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
            string strAnalysisGdb = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Analysis, true);

            for (int idx = 0; idx < Constants.FILES_SWE_DELTA.Length; idx++)
            {
                string strLayer1 = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Layers, true) +
                    Constants.FILES_SNODAS_SWE[idx + 1];
                string strLayer2 = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Layers, true) +
                    Constants.FILES_SNODAS_SWE[idx];
                string strOutputRaster = $@"{strAnalysisGdb}\temp{idx}";
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
            for (int idx = 0; idx < Constants.FILES_SWE_DELTA.Length; idx++)
            {
                string strOutputPath = $@"{strAnalysisGdb}\temp{idx}";
                bool bExists = await GeodatabaseTools.RasterDatasetExistsAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Analysis)), $@"temp{idx}");
                if (bExists)
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

                int intDefaultMonth = 8;
                IList<BA_Objects.Interval> lstInterval = await MapTools.CalculateSweDeltaZonesAsync(intDefaultMonth);
                //Reclassify into 7 classes for map
                if (lstInterval.Count > 0)
                {
                    for (int j = 0; j < Constants.FILES_SWE_DELTA.Length; j++)
                    {
                        string strInput = $@"{strAnalysisGdb}\temp{j}";
                        string strOutput = strAnalysisGdb + "\\" + Constants.FILES_SWE_DELTA[j];
                        string strMaskPath = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_PRISM_VECTOR;
                        success = await AnalysisTools.CalculateZonesAsync(strAoiPath, strInput, lstInterval,
                            strOutput, strMaskPath, "SWE DELTA");
                        if (success == BA_ReturnCode.Success)
                        {
                            success = await GeoprocessingTools.DeleteDatasetAsync(strInput);
                        }
                    }
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(CalculateQuarterlyPrecipitationAsync),
                        "Unable to calculate interval list and publish classified quarterly precipitation layers!");
                    return BA_ReturnCode.UnknownError;
                }
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

            // Delete temporary file gdb
            parameters = Geoprocessing.MakeValueArray(tempGdbPath);
            gpResult = await Geoprocessing.ExecuteToolAsync("Delete_management", parameters, environments,
                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            if (gpResult.IsFailed)
            {
                Module1.Current.ModuleLogManager.LogDebug(nameof(CalculatePrecipitationContributionAsync),
                    "Unable to run delete temporary file gdb!");
                foreach (var objMessage in gpResult.Messages)
                {
                    IGPMessage msg = (IGPMessage)objMessage;
                    Module1.Current.ModuleLogManager.LogError(nameof(CalculatePrecipitationContributionAsync),
                        msg.Text);
                }
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

            // Create vector representation of watersheds for map display
            string vectorPath = GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Analysis, true) + Constants.FILE_PRECIP_CONTRIB_VECTOR;
            parameters = Geoprocessing.MakeValueArray(watershedOutputPath, vectorPath);
            gpResult = await Geoprocessing.ExecuteToolAsync("RasterToPolygon_conversion", parameters, environments,
                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            if (gpResult.IsFailed)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(CalculatePrecipitationContributionAsync),
                    "Failed to create vector representation of sub basin contribution zones!");
                success = BA_ReturnCode.UnknownError;
            }

            return success;
        }

        public static async Task<BA_ReturnCode> CalculateQuarterlyPrecipitationAsync(BA_Objects.Aoi oAoi)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            string[] arrInput = { SeasonalPrismFile.Sq1.ToString(), SeasonalPrismFile.Sq2.ToString(), SeasonalPrismFile.Sq3.ToString(),
                                  SeasonalPrismFile.Sq4.ToString()};
            string strPrismGdb = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Prism);
            string strAnalysisGdb = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis);
            IList<string> lstLayers = await GeneralTools.GetLayersInGeodatabaseAsync(strPrismGdb, "RasterDatasetDefinition");
            // Make sure annual layer exists; Can't divide by 0
            if (!lstLayers.Contains(PrismFile.Annual.ToString()))
            {
                Module1.Current.ModuleLogManager.LogError(nameof(CalculateQuarterlyPrecipitationAsync),
                    "Unable to locate annual PRISM layer. Process halted!");
                return success;
            }
            string annualRaster = strPrismGdb + "\\" + PrismFile.Annual.ToString();
            int i = 0;
            StringBuilder sb = new StringBuilder();
            foreach (var fName in arrInput)
            {
                if (lstLayers.Contains(fName))
                {
                    // define the map algebra expression
                    string quarterlyRaster = strPrismGdb + "\\" + fName;
                    string maExpression = String.Format("\"{1}\" / \"{0}\" * 100", annualRaster, quarterlyRaster);
                    string outRaster = $@"{strAnalysisGdb}\temp{i}";
                    // make the input parameter values array
                    var valueArray = Geoprocessing.MakeValueArray(maExpression, outRaster);
                    var environments = Geoprocessing.MakeEnvironmentArray(workspace:strPrismGdb);
                    // execute the Raster calculator tool to process the map algebra expression
                    var gpResult = await Geoprocessing.ExecuteToolAsync("RasterCalculator_sa", valueArray, environments,
                        CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                    if (gpResult.IsFailed)
                    {
                        Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateQuarterlyPrecipitationAsync),
                            "Raster Calculator failed for " + quarterlyRaster + "!");
                        foreach (var objMessage in gpResult.Messages)
                        {
                            IGPMessage msg = (IGPMessage)objMessage;
                            Module1.Current.ModuleLogManager.LogError(nameof(CalculateQuarterlyPrecipitationAsync),
                                msg.Text);
                        }
                        return BA_ReturnCode.UnknownError;
                    }
                    else
                    {
                        sb.Append(outRaster + ";");
                    }                   
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(CalculateQuarterlyPrecipitationAsync),
                        "Unable to locate " + fName + " layer. Quarter skipped!");
                }
                i++;
            }
            if (sb.Length > 0)
            {
                string strInputLayerPaths = sb.ToString();
                // Remove the ; at the end of the string
                strInputLayerPaths = strInputLayerPaths.Substring(0, strInputLayerPaths.Length - 1);
                string strMaxPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true)
                    + "tmpMax";
                // Calculate maximum of all 4 layers using Cell Statistics
                var parameters = Geoprocessing.MakeValueArray(strInputLayerPaths, strMaxPath, "MAXIMUM");
                var environments = Geoprocessing.MakeEnvironmentArray(workspace: oAoi.FilePath);
                var gpResult = await Geoprocessing.ExecuteToolAsync("CellStatistics_sa", parameters, environments,
                                                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (gpResult.IsFailed)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(CalculateQuarterlyPrecipitationAsync),
                        "Error Code: " + gpResult.ErrorCode + ". Unable to run cell statistics for seasonal quarters!");
                    return success;
                }

                string strMinPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true)
                    + "tmpMin";
                // Calculate minimum of all 4 layers using Cell Statistics
                parameters = Geoprocessing.MakeValueArray(strInputLayerPaths, strMinPath, "MINIMUM");
                gpResult = await Geoprocessing.ExecuteToolAsync("CellStatistics_sa", parameters, environments,
                                                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (gpResult.IsFailed)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(CalculateQuarterlyPrecipitationAsync),
                        "Error Code: " + gpResult.ErrorCode + ". Unable to run cell statistics for seasonal quarters!");
                    return success;
                }

                // Save outputs from GP tools into analysis.xml file
                double dblMax = -1;
                parameters = Geoprocessing.MakeValueArray(strMaxPath, "MAXIMUM");
                gpResult = await Geoprocessing.ExecuteToolAsync("GetRasterProperties_management", parameters, environments,
                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (gpResult.IsFailed)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(CalculateQuarterlyPrecipitationAsync),
                        "Error Code: " + gpResult.ErrorCode + ". Unable to calculate maximum value from cell statistics layer!");
                    return success;
                }
                bool bIsDouble = Double.TryParse(Convert.ToString(gpResult.ReturnValue), out dblMax);

                double dblMin = -1;
                parameters = Geoprocessing.MakeValueArray(strMinPath, "MINIMUM");
                gpResult = await Geoprocessing.ExecuteToolAsync("GetRasterProperties_management", parameters, environments,
                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (gpResult.IsFailed)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(CalculateQuarterlyPrecipitationAsync),
                        "Error Code: " + gpResult.ErrorCode + ". Unable to calculate minimum value from cell statistics layer!");
                    return success;
                }
                bIsDouble = Double.TryParse(Convert.ToString(gpResult.ReturnValue), out dblMin);

                IList<BA_Objects.Interval> lstInterval = new List<BA_Objects.Interval>();
                if (dblMax > -1 && dblMin > -1)
                {
                        // save the min/max values to the configuration file
                        dblMin = Math.Round(dblMin, 2) + 0.05;
                        dblMax = Math.Round(dblMax, 2) + 0.05;
                        BA_Objects.Analysis oAnalysis = GeneralTools.GetAnalysisSettings(oAoi.FilePath);
                        if (oAnalysis != null)
                        {
                            oAnalysis.SeasonalPrecipMax = dblMax;
                            oAnalysis.SeasonalPrecipMin = dblMin;
                            success = GeneralTools.SaveAnalysisSettings(oAoi.FilePath, oAnalysis);
                            if (success != BA_ReturnCode.Success)
                            {
                                Module1.Current.ModuleLogManager.LogError(nameof(CalculateQuarterlyPrecipitationAsync),
                                    "Unable to save min/max values for quarterly precip to analysis settings!");
                            }
                        }
                    lstInterval = CalculateQuarterlyPrecipIntervals(dblMin, dblMax);
                }
                    // Delete results of cell statistics
                    success = await GeoprocessingTools.DeleteDatasetAsync(strMaxPath);
                    success = await GeoprocessingTools.DeleteDatasetAsync(strMinPath);

                //Reclassify into 7 classes for map
                if (lstInterval.Count > 0)
                {
                    for (int j = 0; j < Constants.FILES_SEASON_PRECIP_CONTRIB.Length; j++)
                    {
                        string strInput = $@"{strAnalysisGdb}\temp{j}";
                        string strOutput = strAnalysisGdb + "\\" + Constants.FILES_SEASON_PRECIP_CONTRIB[j];
                        string strMaskPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_PRISM_VECTOR;
                        success = await AnalysisTools.CalculateZonesAsync(oAoi.FilePath, strInput, lstInterval,
                            strOutput, strMaskPath, "QUARTERLY PRECIPITATION");
                        if (success == BA_ReturnCode.Success)
                        {
                            success = await GeoprocessingTools.DeleteDatasetAsync(strInput);
                        }
                    }
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(CalculateQuarterlyPrecipitationAsync),
                        "Unable to calculate interval list and publish classified quarterly precipitation layers!");
                    return BA_ReturnCode.UnknownError;
                }
            }
            return success;
        }

        public static IList<BA_Objects.Interval> CalculateQuarterlyPrecipIntervals(double SeasonalPrecipMin, double SeasonalPrecipMax)
        {
            // Calculate interval list
            int intZones = 7;
            intZones = intZones - 1;  //Subtract the zones in the middle that we create
            int halfZones = intZones / 2;
            if (SeasonalPrecipMin > 0 && SeasonalPrecipMax > 0)
            {
                // Calculate interval list for lower-range values
                double lBound = 23.0F;
                double uBound = 27.0F;
                IList<BA_Objects.Interval> lstNegInterval = new List<BA_Objects.Interval>();
                double dblRange = -1.0F;
                double dblInterval = -1.0F;
                int zones = -1;
                if (SeasonalPrecipMin >= lBound)
                {
                    lBound = SeasonalPrecipMin;
                    // Manually build middle intervals; Spec is defined
                    BA_Objects.Interval oInterval = new BA_Objects.Interval
                    {
                        LowerBound = lBound,
                        UpperBound = uBound,
                        Value = 1
                    };
                    lstNegInterval.Add(oInterval);
                }
                else
                {
                    dblRange = lBound - SeasonalPrecipMin;
                    dblInterval = Math.Round(dblRange / halfZones, 2);
                    //determine the interval decimal place to add an increment value to the lower bound
                    zones = GeneralTools.CreateRangeArray(SeasonalPrecipMin, lBound, dblInterval, out lstNegInterval);
                    // Make sure we don't have > than intzones / 2
                    if (zones > halfZones)
                    {
                        // Merge 2 lower zones
                        if (lstNegInterval.Count > halfZones)
                        {
                            Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateQuarterlyPrecipIntervals),
                                "Merging 2 lowest intervals. Too many intervals created.");
                            var interval = lstNegInterval[0];
                            interval.UpperBound = lstNegInterval[1].UpperBound;
                            lstNegInterval.RemoveAt(1);
                        }
                    }
                    // Reset upper interval to mesh with middle interval
                    lstNegInterval[halfZones - 1].UpperBound = lBound;
                    // Manually build middle intervals; Spec is defined
                    BA_Objects.Interval oInterval = new BA_Objects.Interval
                    {
                        LowerBound = lstNegInterval[halfZones - 1].UpperBound,
                        UpperBound = uBound,
                        Value = halfZones + 1
                    };
                    lstNegInterval.Add(oInterval);
                }

                // Calculate interval list for positive values
                dblRange = SeasonalPrecipMax - uBound;
                dblInterval = Math.Round(dblRange / halfZones, 2);
                IList<BA_Objects.Interval> lstPosInterval = new List<BA_Objects.Interval>();
                zones = GeneralTools.CreateRangeArray(uBound, SeasonalPrecipMax, dblInterval, out lstPosInterval);
                // Make sure we don't have > than half zones
                if (zones > halfZones)
                {
                    // Merge 2 upper zones
                    if (lstPosInterval.Count > halfZones)
                    {
                        Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateQuarterlyPrecipIntervals),
                            "Merging 2 highest intervals. Too many intervals created.");
                        var interval = lstPosInterval[halfZones - 1];
                        interval.UpperBound = lstPosInterval[halfZones].UpperBound;
                        lstPosInterval.RemoveAt(halfZones);
                    }
                }
                // Reset lower interval to mesh with middle interval
                lstPosInterval[0].LowerBound = lstNegInterval.Last().UpperBound;

                // Merge intervals to create 1 list
                foreach (var item in lstPosInterval)
                {
                    lstNegInterval.Add(item);
                }

                // Reset values in calculated interval list
                int idx = 1;
                foreach (var item in lstNegInterval)
                {
                    item.Value = idx;
                    // Format name property
                    item.Name = String.Format("{0:0.0}", item.LowerBound) + " - " +
                            String.Format("{0:0.0}", item.UpperBound);
                    idx++;
                }
                return lstNegInterval;
            }
            else
            {
                Module1.Current.ModuleLogManager.LogError(nameof(CalculateQuarterlyPrecipIntervals),
                    "Invalid min/max precip values. Calculation halted!");
                return null;
            }
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
            string strLayer = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true)
                + "tmpWinter";
            var parameters = Geoprocessing.MakeValueArray(strInputLayerPaths, strLayer, "SUM");
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
                // Calculate zones from output file
                string strZonesRaster = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true)
                + Constants.FILE_WINTER_PRECIPITATION_ZONE;
                success = await CalculatePrecipitationZonesAsync(strLayer, strZonesRaster);
                if (success == BA_ReturnCode.Success)
                {
                    await GeoprocessingTools.DeleteDatasetAsync(strLayer);
                }
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
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            string analysisPath = GeodatabaseTools.GetGeodatabasePath(strAoiFilePath, GeodatabaseNames.Analysis);
            string featureClassToUpdate = analysisPath + "\\" + Constants.FILE_MERGED_SITES;

            // Check to see if all sites are within the buffered AOI. If not, need to reclip DEM and generate slope and aspect
            long outsideCount = 0;
            Uri gdbUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, false));
            Uri sitesGdbUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false));
            int sitesInBasin = await GeodatabaseTools.CountPointsWithinInFeatureAsync(sitesGdbUri, Constants.FILE_MERGED_SITES,
                gdbUri, Constants.FILE_AOI_VECTOR);
            long totalSites = await GeodatabaseTools.CountFeaturesAsync(sitesGdbUri, Constants.FILE_MERGED_SITES);
            if (totalSites > 0)
            {
                outsideCount = totalSites - sitesInBasin;
            }
            if (outsideCount > 0)
            {
                success = await ReclipSurfacesAsync(strAoiFilePath, Constants.FILE_MERGED_SITES);
            }

            var environments = Geoprocessing.MakeEnvironmentArray(workspace: strAoiFilePath, snapRaster: BA_Objects.Aoi.SnapRasterPath(strAoiFilePath));
            string fileExtract = "tmpExtract";
            IList<string> lstFields = new List<string>();
            IList<string> lstFieldDataTypes = new List<string>();
            IList<string> lstUri = new List<string>();
            IList<string> lstInputRasters = new List<string>();
            IList<bool> lstIsImageService = new List<bool>();
            switch (siteProperties)
            {
                case SiteProperties.Aspect:
                    lstFields.Add(Constants.FIELD_ASPECT);
                    lstFieldDataTypes.Add("DOUBLE");
                    lstIsImageService.Add(false);
                    if (outsideCount == 0)
                    {
                        lstUri.Add(GeodatabaseTools.GetGeodatabasePath(strAoiFilePath, GeodatabaseNames.Surfaces));
                        lstInputRasters.Add(Constants.FILE_ASPECT);
                    }
                    else
                    {
                        lstUri.Add(GeodatabaseTools.GetGeodatabasePath(strAoiFilePath, GeodatabaseNames.Analysis));
                        lstInputRasters.Add(Constants.FILE_SITES_ASPECT);
                    }
                    break;

                case SiteProperties.Precipitation:
                    lstFields.Add(Constants.FIELD_PRECIP);
                    lstFieldDataTypes.Add("DOUBLE");
                    string prismImageUri = await GetPrismImageUriAsync(sitesGdbUri, gdbUri, totalSites);
                    if (string.IsNullOrEmpty(prismImageUri))
                    {
                        lstUri.Add(GeodatabaseTools.GetGeodatabasePath(strAoiFilePath, GeodatabaseNames.Prism));
                        lstInputRasters.Add(Path.GetFileName((string)Module1.Current.BatchToolSettings.AoiPrecipFile));
                        lstIsImageService.Add(false);
                    }
                    else
                    {
                        lstUri.Add(prismImageUri);
                        lstInputRasters.Add("");
                        lstIsImageService.Add(true);
                    }
                    break;
            }

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
                    if (lstIsImageService[i] == true)
                    {
                        inputRaster = lstUri[i];
                    }
                    if (lstIsImageService[i] == true || await GeodatabaseTools.RasterDatasetExistsAsync(new Uri(lstUri[i]), lstInputRasters[i]))
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
                    if (! await GeodatabaseTools.AttributeExistsAsync(new Uri(analysisPath), Constants.FILE_MERGED_SITES, Constants.FIELD_DIRECTION))
                    {
                        success = await GeoprocessingTools.AddFieldAsync(featureClassToUpdate, Constants.FIELD_DIRECTION, "TEXT");
                    }                    
                    if (success == BA_ReturnCode.Success)
                    {
                        int intAspectCount = Convert.ToInt16(Module1.Current.BatchToolSettings.AspectDirectionsCount);
                        IList<BA_Objects.Interval> lstAspectInterval = AnalysisTools.GetAspectClasses(intAspectCount);
                        success = await UpdateAspectDirectionsAsync(uriAnalysis, Constants.FILE_MERGED_SITES,
                            lstAspectInterval, Constants.FIELD_ASPECT);
                    }
            }
            return success;
        }

        public static async Task<string> CreateSitesLayerAsync(Uri gdbUri)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            bool hasSiteType = false;
            bool hasSiteId = false;
            string[] arrSiteFiles = new string[] { Constants.FILE_SNOTEL, Constants.FILE_SNOW_COURSE, Constants.FILE_SNOLITE, Constants.FILE_COOP_PILLOW };
            bool[] arrHasSites = new bool[] { false, false, false, false };
            for (int i = 0; i < arrSiteFiles.Length; i++)
            {
                if (await GeodatabaseTools.CountFeaturesAsync(gdbUri, arrSiteFiles[i]) > 0)
                {
                    arrHasSites[i] = true;
                    hasSiteType = await GeodatabaseTools.AttributeExistsAsync(gdbUri, arrSiteFiles[i], Constants.FIELD_SITE_TYPE);
                    if (hasSiteType == false)
                    {
                        success = await GeoprocessingTools.AddFieldAsync(gdbUri.LocalPath + "\\" + arrSiteFiles[i],
                            Constants.FIELD_SITE_TYPE, "TEXT");
                        if (success == BA_ReturnCode.Success)
                        {
                            Module1.Current.ModuleLogManager.LogDebug(nameof(CreateSitesLayerAsync),
                                "Added ba_site_type field to " + arrSiteFiles[i]);
                        }
                    }
                    hasSiteId = await GeodatabaseTools.AttributeExistsAsync(gdbUri, arrSiteFiles[i], Constants.FIELD_SITE_ID);
                    if (hasSiteId == false)
                    {
                        success = await GeoprocessingTools.AddFieldAsync(gdbUri.LocalPath + "\\" + arrSiteFiles[i],
                            Constants.FIELD_SITE_ID, "INTEGER");
                        if (success == BA_ReturnCode.Success)
                        {
                            Module1.Current.ModuleLogManager.LogDebug(nameof(CreateSitesLayerAsync),
                                "Added ba_site_id field to " + arrSiteFiles[i]);
                        }
                    }
                }
            }

            int siteLayersCount = 0;
            string analysisPath = GeodatabaseTools.GetGeodatabasePath(System.IO.Path.GetDirectoryName(gdbUri.LocalPath), GeodatabaseNames.Analysis);
            string returnPath = analysisPath + "\\" + Constants.FILE_MERGED_SITES;
            for (int i = 0; i < arrHasSites.Length; i++)
            {
                bool modificationResult = false;
                string errorMsg = "";
                await QueuedTask.Run(() => {
                    using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(gdbUri)))
                    {
                        if (arrHasSites[i])
                        {
                            SiteType siteType = SiteType.Missing;
                            switch (i)
                            {
                                case 0:
                                    siteType = SiteType.Snotel;
                                    break;
                                case 1:
                                    siteType = SiteType.SnowCourse;
                                    break;
                                case 2:
                                    siteType = SiteType.Snolite;
                                    break;
                                case 3:
                                    siteType = SiteType.CoopPillow;
                                    break;
                            }
                            using (FeatureClass featureClass = geodatabase.OpenDataset<FeatureClass>(arrSiteFiles[i]))
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
                                                    feature[idxSiteType] = siteType.ToString();
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
                                        "Unable to locate ba_site_type field on " + arrSiteFiles[i] + ". Field could not be updated");
                                    return;
                                }
                            }
                            siteLayersCount++;
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
            }

            // There is only one sites layer; We copy that to the merged sites file
            if (siteLayersCount == 1)            
            {
                for (int i = 0; i < arrHasSites.Length; i++)
                {
                    if (arrHasSites[i])
                    {
                        success = await GeoprocessingTools.CopyFeaturesAsync(Module1.Current.Aoi.FilePath, gdbUri.LocalPath + "\\" + arrSiteFiles[i], returnPath);
                        break;
                    }
                }
            }
            else if (siteLayersCount == 0)
            {
                return returnPath;
            }
            else if (siteLayersCount > 1)
            {
                // Need to append sites
                StringBuilder sb = new StringBuilder();
                bool bCopyFirstLayer = false;
                for (int i = 0; i < arrHasSites.Length; i++)
                {
                    if (arrHasSites[i])
                    {
                        if (bCopyFirstLayer == false)
                        {
                            success = await GeoprocessingTools.CopyFeaturesAsync(Module1.Current.Aoi.FilePath, gdbUri.LocalPath + "\\" + arrSiteFiles[i], returnPath);
                            if (success == BA_ReturnCode.Success)
                            {
                                bCopyFirstLayer = true;
                            }
                        }
                        else
                        {
                            sb.Append($@"{gdbUri.LocalPath}\{arrSiteFiles[i]};");
                        }                        
                    }
                }
                string featuresToAppend = sb.ToString().TrimEnd(';');
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
                        "merged_sites layer created successfully");
                }
            }

            string strAoiPath = Module1.Current.Aoi.FilePath;
            // Check to see if all sites are within the buffered AOI. If not, need to reclip DEM and generate slope and aspect
            long outsideCount = 0;
            Uri aoiUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, false));
            Uri sitesGdbUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false));
            int sitesInBasin = await GeodatabaseTools.CountPointsWithinInFeatureAsync(sitesGdbUri, Constants.FILE_MERGED_SITES,
                aoiUri, Constants.FILE_AOI_VECTOR);
            long totalSites = await GeodatabaseTools.CountFeaturesAsync(sitesGdbUri, Constants.FILE_MERGED_SITES);
            if (totalSites > 0)
            {
                outsideCount = totalSites - sitesInBasin;
            }
            if (outsideCount > 0)
            {
                // This sets the elevation for sites outside the filled_dem extent
                success = await ReclipSurfacesAsync(strAoiPath, returnPath);
            }
            else
            {
                success = BA_ReturnCode.Success;
            }
            if (success != BA_ReturnCode.Success)
            {
                return "";
            }
            
            // Assign the site id by elevation
            success = await UpdateSiteIdsAsync(analysisPath, gdbUri, arrHasSites, arrSiteFiles);
            var environments = Geoprocessing.MakeEnvironmentArray(workspace: strAoiPath, snapRaster: BA_Objects.Aoi.SnapRasterPath(strAoiPath),
                extent: returnPath);
                string fileExtract = "tmpExtract";
                string[] arrFields = { Constants.FIELD_PRECIP, Constants.FIELD_ASPECT, Constants.FIELD_SLOPE};
                string[] arrFieldDataTypes = { "DOUBLE", "DOUBLE", "DOUBLE" };
                string[] arrUri = { GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Prism),
                                    GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Surfaces),
                                    GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Surfaces)};
                string[] arrInputRasters = {Path.GetFileName((string)Module1.Current.BatchToolSettings.AoiPrecipFile),
                                            Constants.FILE_ASPECT,
                                            Constants.FILE_SLOPE};
                bool[] arrIsImageService = {false, false, false};
                string prismImageUri = await GetPrismImageUriAsync(sitesGdbUri, aoiUri, totalSites);
                if (! string.IsNullOrEmpty(prismImageUri))
                {
                    arrUri[0] = prismImageUri;
                    arrIsImageService[0] = true;
                }
                if (outsideCount > 0 && success == BA_ReturnCode.Success)
                {
                    arrUri[1] = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Analysis);
                    arrUri[2] = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Analysis);
                    arrInputRasters[1] = Constants.FILE_SITES_ASPECT;
                    arrInputRasters[2] = Constants.FILE_SITES_SLOPE;
                }

            Uri analysisUri = new Uri(analysisPath);
            for (int i = 0; i < arrFields.Length; i++)
            {
                if (! await GeodatabaseTools.AttributeExistsAsync(analysisUri, Constants.FILE_MERGED_SITES, arrFields[i]))
                {
                    success = await GeoprocessingTools.AddFieldAsync(returnPath, arrFields[i], arrFieldDataTypes[i]);
                    Module1.Current.ModuleLogManager.LogDebug(nameof(CreateSitesLayerAsync),
                        "New field " + arrFields[i] + " added to " + Constants.FILE_MERGED_SITES);
                }
                else
                {
                    success = BA_ReturnCode.Success;
                }
                if (success == BA_ReturnCode.Success)
                {
                    string inputRaster = arrUri[i] + "\\" + arrInputRasters[i];
                    if (arrIsImageService[i] == true)
                    {
                        inputRaster = arrUri[i];
                    }
                    if (arrIsImageService[i] == true || await GeodatabaseTools.RasterDatasetExistsAsync(new Uri(arrUri[i]), arrInputRasters[i]))
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
                                    var map = await MapTools.SetDefaultMapNameAsync(Constants.MAPS_DEFAULT_MAP_NAME);
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
                                } //

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
                        success = await GeoprocessingTools.AddFieldAsync(returnPath, Constants.FIELD_DIRECTION, "TEXT");
                        if (success == BA_ReturnCode.Success)
                        {
                            int intAspectCount = Convert.ToInt16(Module1.Current.BatchToolSettings.AspectDirectionsCount);
                            IList<BA_Objects.Interval> lstAspectInterval = AnalysisTools.GetAspectClasses(intAspectCount);
                            success = await UpdateAspectDirectionsAsync(uriAnalysis, Constants.FILE_MERGED_SITES,
                                    lstAspectInterval, Constants.FIELD_ASPECT);
                            }
                    }
            return returnPath;
        }

        public static async Task<BA_ReturnCode> ClipLandCoverAsync(string aoiFolderPath, string landCoverBufferDistance, string landCoverBufferUnits)
        {
            string strOutputRaster = GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Layers, true)
                + Constants.FILE_LAND_COVER;
            BA_ReturnCode success = await AnalysisTools.ClipRasterLayerAsync(aoiFolderPath, strOutputRaster, BA_Objects.DataSource.GetLandCoverKey,
                landCoverBufferDistance, landCoverBufferUnits);
            string strNullOutput = GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Analysis, true)
                + "tmpNull";
            if (success == BA_ReturnCode.Success)
            {
                string strConstant = "11";  // water bodies
                string strWaterbodies = GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Analysis, true)
                    + Constants.FILE_WATER_BODIES;
                string strWhere = "Value <> 11";
                success = await GeoprocessingTools.SetNullAsync(strOutputRaster, strConstant, strNullOutput, strWhere);
            }
            if (success == BA_ReturnCode.Success)
            {
                // Create vector representation of waterbodies for map display
                string strWaterbodies = GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Analysis, true)
                    + Constants.FILE_WATER_BODIES;
                var parameters = Geoprocessing.MakeValueArray(strNullOutput, strWaterbodies);
                var gpResult = await Geoprocessing.ExecuteToolAsync("RasterToPolygon_conversion", parameters, null,
                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (gpResult.IsFailed)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(ClipLandCoverAsync),
                        "Failed to create vector representation of waterbodies!");
                    success = BA_ReturnCode.UnknownError;
                }
                else
                {
                    // Delete temp null raster
                    success = await GeoprocessingTools.DeleteDatasetAsync(strNullOutput);
                }
            }
            return success;
        }

        public static async Task<BA_ReturnCode> ReclipSurfacesAsync(string aoiFolderPath, string strSitesPath)
        {
            Webservices ws = new Webservices();
            string demUri = await ws.GetDem30UriAsync();

            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            string clipEnvelope = "";
            string strOutputFeatures = GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Analysis, true) +
                "tmpBuffer";
            string strDistance = "5 Kilometers";
            if (Module1.Current.BatchToolSettings.SnotelBufferDistance != null)
            {
                strDistance = (string)Module1.Current.BatchToolSettings.SnotelBufferDistance + " " +
                    (string)Module1.Current.BatchToolSettings.PrecipBufferUnits;
            }
            
            string strAoiPath = GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_VECTOR;
            var parameters = Geoprocessing.MakeValueArray(strAoiPath, strOutputFeatures, strDistance, "",
                                                              "", "ALL");
            var res = Geoprocessing.ExecuteToolAsync("Buffer_analysis", parameters, null,
                                 CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            if (res.Result.IsFailed)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(ReclipSurfacesAsync),
                   "Unable to buffer sites layer. Error code: " + res.Result.ErrorCode);
            }
            else
            {
                await QueuedTask.Run(() =>
                {
                    using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Analysis)))))
                    using (FeatureClass fClass = geodatabase.OpenDataset<FeatureClass>("tmpBuffer"))
                    {
                        Envelope env = fClass.GetExtent();
                        clipEnvelope = env.Extent.XMin + " " + env.Extent.YMin + " " + env.Extent.XMax + " " + env.Extent.YMax;
                    }
                });
            }
            if (! String.IsNullOrEmpty(clipEnvelope))
            {
                success = await GeoprocessingTools.DeleteDatasetAsync(strOutputFeatures);
            }
            string outputRaster = GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Analysis, true) + Constants.FILE_SITES_DEM;
            var environments = Geoprocessing.MakeEnvironmentArray(workspace: aoiFolderPath, 
                snapRaster: BA_Objects.Aoi.SnapRasterPath(aoiFolderPath));
            IGPResult gpResult = null;
            if (success == BA_ReturnCode.Success)
            {
                success = await GeoprocessingTools.ClipRasterAsync(demUri, clipEnvelope, outputRaster, null, null, false,
                    aoiFolderPath, BA_Objects.Aoi.SnapRasterPath(aoiFolderPath));
                if (success != BA_ReturnCode.Success)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(ReclipSurfacesAsync),
                        "Failed to clip DEM for buffered sites layer using ClipRasterAsync. Attempting ClipRasterToLayerAsync");
                    success = await GeoprocessingTools.ClipRasterAsLayerAsync(demUri, clipEnvelope, outputRaster, null, null, false,
                    aoiFolderPath, BA_Objects.Aoi.SnapRasterPath(aoiFolderPath));
;                }
                if (success == BA_ReturnCode.Success)
                {
                    // Recalculate slope layer on clipped DEM
                    parameters = Geoprocessing.MakeValueArray(outputRaster, GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_SITES_SLOPE, "PERCENT_RISE");
                    gpResult = await Geoprocessing.ExecuteToolAsync("Slope_sa", parameters, environments,
                                                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                    if (gpResult.IsFailed)
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(ReclipSurfacesAsync),
                            "Slope tool failed to create sites_slope layer. Error code: " + gpResult.ErrorCode);
                    }
                }

            }

            // Recalculate aspect layer on clipped DEM
            if (success == BA_ReturnCode.Success)
            {
                parameters = Geoprocessing.MakeValueArray(outputRaster, GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_SITES_ASPECT, "Planar");
                gpResult = await Geoprocessing.ExecuteToolAsync("Aspect_sa", parameters, environments,
                                            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (gpResult.IsFailed)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(ReclipSurfacesAsync),
                        "Aspect tool failed to create sites_aspect layer. Error code: " + gpResult.ErrorCode);
                    success = BA_ReturnCode.UnknownError;
                }
            }
            if (success == BA_ReturnCode.Success)
            {
                //Run the ExtractValuesToPoints tool to get all the elevations
                string tempPath = GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Analysis, true) + "tmpExtract";
                parameters = Geoprocessing.MakeValueArray(strSitesPath, outputRaster, tempPath, "NONE", "ALL");
                gpResult = await Geoprocessing.ExecuteToolAsync("ExtractValuesToPoints_sa", parameters, environments,
                                                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (gpResult.IsFailed)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(ReclipSurfacesAsync),
                        "Extract values to points tool failed to create tmpExtract. Error code: " + gpResult.ErrorCode);
                    success = BA_ReturnCode.UnknownError;
                }
                else
                {
                    parameters = Geoprocessing.MakeValueArray(strSitesPath, Constants.FIELD_OBJECT_ID, tempPath,
                        Constants.FIELD_OBJECT_ID, "KEEP_ALL");
                    // Need GPExecuteToolFlag to add the layer to the map
                    gpResult = await Geoprocessing.ExecuteToolAsync("management.AddJoin", parameters, environments,
                        CancelableProgressor.None, GPExecuteToolFlags.Default);
                    if (gpResult.IsFailed)
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(ReclipSurfacesAsync),
                            "AddJoin tool failed. Error code: " + gpResult.ErrorCode);
                        success = BA_ReturnCode.UnknownError;
                    }
                    else
                    {
                        string lyrJoin = gpResult.ReturnValue;
                        parameters = Geoprocessing.MakeValueArray(lyrJoin, "merged_sites." + Constants.FIELD_SITE_ELEV,
                            "!tmpExtract.RASTERVALU!", "PYTHON3", "", "DOUBLE");
                        await Geoprocessing.ExecuteToolAsync("management.CalculateField", parameters, environments,
                            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                        if (gpResult.IsFailed)
                        {
                            Module1.Current.ModuleLogManager.LogError(nameof(ReclipSurfacesAsync),
                                "CalculateField tool failed to update field. Error code: " + gpResult.ErrorCode);
                            success = BA_ReturnCode.UnknownError;
                        }
                        else
                        {
                            var map = await MapTools.SetDefaultMapNameAsync(Constants.MAPS_DEFAULT_MAP_NAME);
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
            return success;
        }

        public static async Task<string> GetPrismImageUriAsync(Uri sitesGdbUri, Uri aoiUri, long totalSites)
        {
            string strUri = "";
            // Check to see if all sites are with the PRISM buffered AOI. If not, need to get the correct service name
            long outsidePrism = 0;
            int sitesInBasin = await GeodatabaseTools.CountPointsWithinInFeatureAsync(sitesGdbUri, Constants.FILE_MERGED_SITES,
                aoiUri, Constants.FILE_AOI_VECTOR);
            if (totalSites > 0)
            {
                outsidePrism = totalSites - sitesInBasin;
            }

            if (outsidePrism > 0)
            {
                Webservices ws = new Webservices();
                Module1.Current.ModuleLogManager.LogDebug(nameof(GetPrismImageUriAsync),
                    "Contacting webservices server to retrieve prism layer uri");
                IDictionary<string, dynamic> dictDataSources =
                    await ws.QueryDataSourcesAsync((string)Module1.Current.BatchToolSettings.EBagisServer);
                string strWsPrefix = dictDataSources[BA_Objects.DataSource.GetPrecipitationKey].uri;
                if (!string.IsNullOrEmpty(strWsPrefix))
                {
                    string localLayerName = Path.GetFileName((string)Module1.Current.BatchToolSettings.AoiPrecipFile);
                    PrismFile prismFile = (PrismFile) Enum.Parse(typeof(PrismFile), localLayerName);
                    int index = Array.IndexOf(Enum.GetValues(prismFile.GetType()), prismFile);
                    PrismServiceNames serviceName = (PrismServiceNames)index;
                    Uri imageServiceUri = new Uri(strWsPrefix + serviceName.ToString() +
                        Constants.URI_IMAGE_SERVER);
                    strUri = imageServiceUri.AbsoluteUri;
                }
            }
            return strUri;
        }

        public static async Task<bool> TooManySitesAsync(string strAoiPath)
        {
            int maxSitesAllowed = (int)Module1.Current.BatchToolSettings.MaximumSitesAllowed;
            Webservices ws = new Webservices();
            IDictionary<string, dynamic> dictDataSources =
                await ws.QueryDataSourcesAsync((string)Module1.Current.BatchToolSettings.EBagisServer);
            if (dictDataSources != null)
            {
                if (!dictDataSources.ContainsKey(Constants.DATA_TYPE_SNOTEL) ||
                    !dictDataSources.ContainsKey(Constants.DATA_TYPE_SNOW_COURSE))
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(TooManySitesAsync),
                        "Unable to retrieve snotel datasource information from " + (string)Module1.Current.BatchToolSettings.EBagisServer +
                        ". Processing cancelled!!");
                    return true;
                }
            }
            string[] arrWsUri = new string[]
                { dictDataSources[Constants.DATA_TYPE_SNOTEL].uri, dictDataSources[Constants.DATA_TYPE_SNOW_COURSE].uri };
            Uri uriAoi = new Uri(GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Aoi));
            string tempJoin = "tmpJoin";
            string strAoiPoly = uriAoi.LocalPath + "\\" + Constants.FILE_AOI_BUFFERED_VECTOR;
            string strTempOutput = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Aoi, true) + tempJoin;
            int i = 0;
            int totalSites = 0;
            foreach (var strWsUri in arrWsUri)
            {
                var parameters = Geoprocessing.MakeValueArray(strAoiPoly, strWsUri, strTempOutput, "JOIN_ONE_TO_ONE", "KEEP_COMMON",
                    null, "CONTAINS");
                var res = Geoprocessing.ExecuteToolAsync("SpatialJoin_analysis", parameters, null,
                                     CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (res.Result.IsFailed)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(TooManySitesAsync),
                       "Unable to execute spatial join with sites webservice. Error code: " + res.Result.ErrorCode);
                }
                else
                {
                    var result = await GeodatabaseTools.QueryTableForSingleValueAsync(uriAoi, tempJoin, Constants.FIELD_JOIN_COUNT, new QueryFilter());
                    int intCount = -1;
                    bool isInteger = Int32.TryParse(result, out intCount);
                    if (isInteger)
                    {
                        string sType = Constants.DATA_TYPE_SNOTEL;
                        if (i == 1)
                        {
                            sType = Constants.DATA_TYPE_SNOW_COURSE;
                        }
                        Module1.Current.ModuleLogManager.LogInfo(nameof(TooManySitesAsync),
                            result + " " + sType + " sites found in AOI");
                        totalSites = totalSites + intCount;
                    }
                    if (totalSites > maxSitesAllowed)
                    {
                        return true;
                    }
                }
                i++;
            }
            if (await GeodatabaseTools.FeatureClassExistsAsync(uriAoi, tempJoin))
            {
                BA_ReturnCode success = await GeoprocessingTools.DeleteDatasetAsync(strTempOutput);
            }
            return false;
        }

        private static async Task<BA_ReturnCode>  UpdateSiteIdsAsync(string analysisPath, Uri uriLayers, bool[] arrHasSites,
            string[] arrSiteFiles)
        {
            // Sort by elevation and set site id
            Uri analysisUri = new Uri(analysisPath);
            bool hasSiteId = await GeodatabaseTools.AttributeExistsAsync(analysisUri, Constants.FILE_MERGED_SITES, Constants.FIELD_SITE_ID);
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            if (hasSiteId == false)
            {
                success = await GeoprocessingTools.AddFieldAsync(analysisPath + "\\" + Constants.FILE_MERGED_SITES, Constants.FIELD_SITE_ID, "INTEGER");
                if (success == BA_ReturnCode.Success)
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(CreateSitesLayerAsync),
                        "Added ba_site_id field to merged_sites");
                    hasSiteId = true;
                }
            }
            if (hasSiteId)
            {
                int intSiteId = 1;
                string errorMsg = null;
                IList<BA_Objects.Site> lstSites = new List<BA_Objects.Site>();
                await QueuedTask.Run(() => {
                    using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(analysisUri)))
                    {
                        using (FeatureClass featureClass = geodatabase.OpenDataset<FeatureClass>(Constants.FILE_MERGED_SITES))
                        {
                            FeatureClassDefinition featureClassDefinition = featureClass.GetDefinition();
                            Field elevationField = featureClassDefinition.GetFields()
                                .First(x => x.Name.Equals(Constants.FIELD_SITE_ELEV));

                            // Create SortDescription for Elevation field
                            SortDescription elevSortDescription = new SortDescription(elevationField)
                            {
                                SortOrder = SortOrder.Ascending
                            };

                            // Create our TableSortDescription
                            var tableSortDescription = new TableSortDescription(new List<SortDescription>() { elevSortDescription });
                            RowCursor rowCursor = featureClass.Sort(tableSortDescription);
                            Module1.Current.ModuleLogManager.LogDebug(nameof(CreateSitesLayerAsync), "Created sorted row cursor");
                            int idxSiteId = featureClassDefinition.FindField(Constants.FIELD_SITE_ID);
                            int idxSiteName = featureClassDefinition.FindField(Constants.FIELD_SITE_NAME);
                            int idxSiteType = featureClassDefinition.FindField(Constants.FIELD_SITE_TYPE);
                            EditOperation editOperation = new EditOperation();
                            editOperation.Callback(context =>
                            {
                                while (rowCursor.MoveNext())
                                {
                                    using (Feature feature = (Feature)rowCursor.Current)
                                    {
                                        string sName = Convert.ToString(feature[idxSiteName]);
                                        string sType = Convert.ToString(feature[idxSiteType]);
                                        BA_Objects.Site aSite = new BA_Objects.Site();
                                        aSite.Name = sName;
                                        aSite.SiteTypeText = sType;
                                        aSite.SiteId = intSiteId;
                                        lstSites.Add(aSite);
                                        // In order to update the the attribute table has to be called before any changes are made to the row
                                        context.Invalidate(feature);
                                        // increment the site id
                                        feature[idxSiteId] = intSiteId;
                                        feature.Store();
                                        // Has to be called after the store too
                                        context.Invalidate(feature);
                                        intSiteId++;
                                    }
                                }
                            }, featureClass);

                            try
                            {
                                bool modificationResult = editOperation.Execute();
                                if (!modificationResult) errorMsg = editOperation.ErrorMessage;
                            }
                            catch (GeodatabaseException exObj)
                            {
                                errorMsg = exObj.Message;
                            }
                        }
                    }
                    if (String.IsNullOrEmpty(errorMsg))
                    {
                        Project.Current.SaveEditsAsync();
                        Module1.Current.ModuleLogManager.LogDebug(nameof(CreateSitesLayerAsync),
                            "ba_site_id edits saved");
                        success = BA_ReturnCode.Success;
                    }
                    else
                    {
                        if (Project.Current.HasEdits)
                            Project.Current.DiscardEditsAsync();
                        Module1.Current.ModuleLogManager.LogError(nameof(CreateSitesLayerAsync),
                            "Edit Exception: " + errorMsg);
                        success = BA_ReturnCode.UnknownError;
                    }
                });

                success = await UpdateSnoIdsAsync(uriLayers, lstSites, arrHasSites, arrSiteFiles);
            }
            return success;
        }

        private static async Task<BA_ReturnCode> UpdateSnoIdsAsync(Uri uriLayers, IList<BA_Objects.Site> lstSites, bool[] arrHasSites,
            string[] arrSiteFiles)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            await QueuedTask.Run(() => {
                string errorMsg = "";
                for (int i = 0; i < arrSiteFiles.Length; i++)
                {
                    if (arrHasSites[i])
                    {

                        string sType = SiteType.Missing.ToString();
                        switch (i)
                        {
                            case 0:
                                sType = SiteType.Snotel.ToString();
                                break;
                            case 1:
                                sType = SiteType.SnowCourse.ToString();
                                break;
                            case 2:
                                sType = SiteType.Snolite.ToString();
                                break;
                            case 3:
                                sType = SiteType.CoopPillow.ToString();
                                break;
                        }

                        using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(uriLayers)))
                        {
                            using (FeatureClass featureClass = geodatabase.OpenDataset<FeatureClass>(arrSiteFiles[i]))
                            {
                                FeatureClassDefinition featureClassDefinition = featureClass.GetDefinition();
                                int idxSiteId = featureClassDefinition.FindField(Constants.FIELD_SITE_ID);
                                int idxSiteName = featureClassDefinition.FindField(Constants.FIELD_SITE_NAME);
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
                                            // Find the site
                                            string sName = Convert.ToString(feature[idxSiteName]);
                                                int intNewSiteId = -1;
                                                foreach (var aSite in lstSites)
                                                {
                                                    if (aSite.SiteTypeText.Equals(sType) && sName.Equals(aSite.Name))
                                                    {
                                                        intNewSiteId = aSite.SiteId;
                                                        break;
                                                    }
                                                }
                                                feature[idxSiteId] = intNewSiteId;
                                                feature.Store();
                                            // Has to be called after the store too
                                            context.Invalidate(feature);
                                            }
                                        }
                                    }
                                }, featureClass);
                                try
                                {
                                    bool modificationResult = editOperation.Execute();
                                    if (!modificationResult) errorMsg = editOperation.ErrorMessage;
                                }
                                catch (GeodatabaseException exObj)
                                {
                                    errorMsg = exObj.Message;
                                }

                            }
                        }
                    }
                    if (String.IsNullOrEmpty(errorMsg))
                    {
                        Project.Current.SaveEditsAsync();
                        Module1.Current.ModuleLogManager.LogDebug(nameof(CreateSitesLayerAsync),
                            "ba_site_id edits saved");
                        success = BA_ReturnCode.Success;
                    }
                    else
                    {
                        if (Project.Current.HasEdits)
                            Project.Current.DiscardEditsAsync();
                        Module1.Current.ModuleLogManager.LogError(nameof(CreateSitesLayerAsync),
                            "Edit Exception: " + errorMsg);
                        success = BA_ReturnCode.UnknownError;
                    }
                }
            });
            return success;
        }

        private static async Task<bool> IsNoDataRasterAsync(string strAoiPath, string strGdbFolder, string strRasterFile)
        {
            char[] charsToTrim = { '\\' };
            strGdbFolder = strGdbFolder.TrimEnd(charsToTrim);
            var parameters = Geoprocessing.MakeValueArray($@"{strGdbFolder}\{strRasterFile}", "MAXIMUM");
            var environments = Geoprocessing.MakeEnvironmentArray(workspace: strAoiPath);
            var gpResult = await Geoprocessing.ExecuteToolAsync("GetRasterProperties_management", parameters, environments,
                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            if (gpResult.IsFailed)
            {
                if (gpResult.ErrorCode == 2)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(IsNoDataRasterAsync),
                        "Unable to calculate maximum for raster data. Failed because no statistics are available!");
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(IsNoDataRasterAsync),
                        "Error Code: " + gpResult.ErrorCode + ". Unable to calculate maximum for raster data!");
                }
                return true;
            }
            string strMax = Convert.ToString(gpResult.ReturnValue);
            string strNoData = "-9998";
            await QueuedTask.Run(() =>
            {
                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strGdbFolder))))
                using (RasterDataset rasterDataset = geodatabase.OpenDataset<RasterDataset>(strRasterFile))
                {
                    RasterBandDefinition bandDefinition = rasterDataset.GetBand(0).GetDefinition();
                    Raster raster = rasterDataset.CreateDefaultRaster();
                    strNoData = Convert.ToString(raster.GetNoDataValue());
                }
            });
            if (strMax.Equals(strNoData))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static async Task<IDictionary<string, string>> CalculateZonalAreaPercentages(string strAoiFolder, string strZonalLayerPath, string strZonalField,
            string strInRaster, string strOutputTable, IList<string> lstZoneNames, string strLogFile)
        {
            var parameters = Geoprocessing.MakeValueArray(strZonalLayerPath, strZonalField, strInRaster, strOutputTable, "DATA", "MINIMUM");
            var environments = Geoprocessing.MakeEnvironmentArray(workspace: strAoiFolder);
            var gpResult = await Geoprocessing.ExecuteToolAsync("ZonalStatisticsAsTable_sa", parameters, environments,
                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            string strLogEntry = "";
            IDictionary<string, string> dictZonalPercentages = new Dictionary<string, string>();
            IDictionary<string, int> dictCounts = new Dictionary<string, int>();
            if (gpResult.IsFailed)
            {
                strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Unable to execute zonal statistics on filled_dem. Aspect Percent areas are not available! \r\n";
                File.AppendAllText(strLogFile, strLogEntry);       // append
            }
            else
            {
                foreach (var item in lstZoneNames)
                {
                    dictCounts.Add(item, 0);
                }
                Uri gdbUri = new Uri(Path.GetDirectoryName(strOutputTable));
                int intTotalCount = 0;
                await QueuedTask.Run(() =>
                {
                    using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(gdbUri)))
                    using (Table table = geodatabase.OpenDataset<Table>(Path.GetFileName(strOutputTable)))
                    {
                        QueryFilter queryFilter = new QueryFilter();
                        using (RowCursor aCursor = table.Search(queryFilter, false))
                        {
                            while (aCursor.MoveNext())
                            {
                                using (Row aRow = (Row)aCursor.Current)
                                {
                                    if (aRow != null)
                                    {
                                        int idxName = aRow.FindField(strZonalField);
                                        int idxCount = aRow.FindField(Constants.FIELD_COUNT);
                                        if (idxName > -1 && idxCount > -1)
                                        {
                                            string strName = Convert.ToString(aRow[idxName]);
                                            int intCount = Convert.ToInt16(aRow[idxCount]);
                                            if (!string.IsNullOrEmpty(strName) && dictCounts.ContainsKey(strName))
                                            {
                                                dictCounts[strName] = intCount;
                                                intTotalCount = intTotalCount + intCount;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    foreach (var key in dictCounts.Keys)
                    {
                        double dblPercent = (double)Math.Round((double)(100 * dictCounts[key]) / intTotalCount,1);
                        dictZonalPercentages.Add(key, Convert.ToString(dblPercent));
                    }
                });
            }
            return dictZonalPercentages;
        }

        public static async Task<IList<string>> GenerateForecastStatisticsList(BA_Objects.Aoi oAoi, string strLogFile, BA_ReturnCode runOffData)
        {
            IList<string> lstElements = new List<string>();
            lstElements.Add(oAoi.Name);  //AOI Name
            lstElements.Add(oAoi.StationTriplet);   // Station triplet
            // Retrieve AOI Analysis object with settings for future use
            BA_Objects.Analysis oAnalysis = GeneralTools.GetAnalysisSettings(oAoi.FilePath);
            string strLogEntry;

            // aoiArea_SqMeters
            string strAreaSqM = "Not Found";
            string strAreaSqMiles = strAreaSqM;
            string strAnnRunoffRatioPct = strAreaSqM;
            QueryFilter queryFilter = new QueryFilter();
            Uri aoiUri = new Uri(GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Aoi));
            string strAreaSqKm = await GeodatabaseTools.QueryTableForSingleValueAsync(aoiUri, Constants.FILE_POURPOINT,
                                    Constants.FIELD_AOI_AREA, new QueryFilter());
            double dblAreaSqKm = -1;
            bool isDouble = Double.TryParse(strAreaSqKm, out dblAreaSqKm);
            if (isDouble)
            {
                strAreaSqM = String.Format("{0:0.00}",AreaUnit.SquareKilometers.ConvertTo(dblAreaSqKm, AreaUnit.SquareMeters));
                // aoiArea_SqMiles
                strAreaSqMiles = String.Format("{0:0.00}", AreaUnit.SquareKilometers.ConvertTo(dblAreaSqKm, AreaUnit.SquareMiles));

            }
            lstElements.Add(strAreaSqM);
            lstElements.Add(strAreaSqMiles);
            // ann_runoff_ratio_pct
            if (runOffData == BA_ReturnCode.Success && oAnalysis != null)
            {
                // Query for the annual runoff value
                string annualRunoffField = (string)Module1.Current.BatchToolSettings.AnnualRunoffDataField;
                double dblAnnualRunoff = GeneralTools.QueryAnnualRunoffValue(oAoi.StationTriplet, annualRunoffField);
                double dblRunoffRatio = -1;
                if (dblAnnualRunoff >= 0)
                {
                    if (oAnalysis != null)
                    {
                        if (oAnalysis.PrecipVolumeKaf > 0)
                        {
                            dblRunoffRatio = dblAnnualRunoff / oAnalysis.PrecipVolumeKaf;
                            strAnnRunoffRatioPct = dblRunoffRatio.ToString("0.##");
                        }
                        else
                        {
                            strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "PrecipVolumeKaf is missing from the analysis.xml file. Generate the Excel tables before generating the statistics ! \r\n";
                            File.AppendAllText(strLogFile, strLogEntry);       // append
                        }
                    }
                }
            }
            lstElements.Add(strAnnRunoffRatioPct);
            // centroid_x_dd and centroid_y_dd
            string strCentroidX = "Not Found";
            string strCentroidY = strCentroidX;
            string strInputFeatures = $@"{aoiUri.LocalPath}\{Constants.FILE_AOI_VECTOR}";
            string strTmpPoint = "tmpPoint";
            string strTmpPointProj = "tmpPointProj";
            string strOutputFeature = $@"{aoiUri.LocalPath}\{strTmpPoint}";
            string strOutputFeatureProj = $@"{aoiUri.LocalPath}\{strTmpPointProj}";
            var parameters = Geoprocessing.MakeValueArray(strInputFeatures, strOutputFeature, "INSIDE");
            IGPResult gpResult = await Geoprocessing.ExecuteToolAsync("FeatureToPoint_management", parameters, null,
                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            if (gpResult.IsFailed)
            {
                strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Unable to run FeatureToPoint tool. Centroid x and y are not available! \r\n";
                File.AppendAllText(strLogFile, strLogEntry);       // append
            }
            else            
            {
                parameters = Geoprocessing.MakeValueArray(strOutputFeature, strOutputFeatureProj, SpatialReferences.WGS84, "NAD_1983_To_WGS_1984_1");
                gpResult = await Geoprocessing.ExecuteToolAsync("Project_management", parameters, null,
                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (gpResult.IsFailed)
                {
                    strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Unable to Project tmpPoint. Centroid x and y are not available! \r\n";
                    File.AppendAllText(strLogFile, strLogEntry);       // append
                }
                else
                {
                    string strPointX = "POINT_X";
                    string strPointY = "POINT_Y";
                    string strProperties = @$"{strPointX} {strPointX};{strPointY} {strPointY}";
                    parameters = Geoprocessing.MakeValueArray(strOutputFeatureProj, strProperties);
                    gpResult = await Geoprocessing.ExecuteToolAsync("CalculateGeometryAttributes_management", parameters, null,
                        CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                    if (gpResult.IsFailed)
                    {
                        strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Unable to CalculateGeometryAttributs for tmpPoint. Centroid x and y are not available! \r\n";
                        File.AppendAllText(strLogFile, strLogEntry);       // append
                    }
                    else
                    {
                        double dblX = Convert.ToDouble(await GeodatabaseTools.QueryTableForSingleValueAsync(aoiUri, strTmpPointProj, strPointX, new QueryFilter()));
                        strCentroidX = dblX.ToString("0.###");
                        lstElements.Add(strCentroidX);
                        double dblY = Convert.ToDouble(await GeodatabaseTools.QueryTableForSingleValueAsync(aoiUri, strTmpPointProj, strPointY, new QueryFilter()));
                        strCentroidY = dblY.ToString("0.###");
                        lstElements.Add(strCentroidY);
                    }
                }
            }
            // Delete temp files
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            if (await GeodatabaseTools.FeatureClassExistsAsync(aoiUri, strTmpPoint))
            {
                success = await GeoprocessingTools.DeleteDatasetAsync(strOutputFeature);
            }
            if (await GeodatabaseTools.FeatureClassExistsAsync(aoiUri, strTmpPointProj))
            {
                success = await GeoprocessingTools.DeleteDatasetAsync(strOutputFeatureProj);
            }

            // state_codes
            string strStateCodes = "Not Found";
            if (Module1.Current.BatchToolSettings.USStateBoundaries != null)
            {
                string strStatesUrl = Convert.ToString(Module1.Current.BatchToolSettings.USStateBoundaries);
                string strTmpStates = "tmpStates";
                strOutputFeature = $@"{aoiUri.LocalPath}\{strTmpStates}";
                parameters = Geoprocessing.MakeValueArray(strStatesUrl, strInputFeatures, strOutputFeature);
                gpResult = await Geoprocessing.ExecuteToolAsync("PairwiseClip", parameters, null,
                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (gpResult.IsFailed)
                {
                    strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Unable to clip state boundaries layer. State codes are not available! \r\n";
                    File.AppendAllText(strLogFile, strLogEntry);       // append
                }
                else
                {
                    IList<string> lstStateCodes = await GeodatabaseTools.QueryTableForDistinctValuesAsync(aoiUri, strTmpStates, "STATE_ABBR", new QueryFilter());
                    StringBuilder sb = new StringBuilder();
                    foreach (var item in lstStateCodes)
                    {
                        sb.Append(item);
                        sb.Append(',');
                    }
                    strStateCodes = sb.ToString();
                    if (!string.IsNullOrEmpty(strStateCodes))
                    {
                        strStateCodes = "\"" + strStateCodes.TrimEnd(',') + "\"";                       
                    }
                    // Delete temp file
                    if (await GeodatabaseTools.FeatureClassExistsAsync(aoiUri, strTmpStates))
                    {
                        success = await GeoprocessingTools.DeleteDatasetAsync(strOutputFeature);
                    }
                }
                lstElements.Add(strStateCodes);

                //elev_min_ft,elev_max_ft,elev_range_ft
                string strElevMinFt = "Not Found";
                string strElevMaxFt = strElevMinFt;
                string strElevRangeFt = strElevMinFt;
                IList<double> lstResult = await GeoprocessingTools.GetDemStatsAsync(oAoi.FilePath, strInputFeatures, 0.005);
                double elevMinMeters = -1;
                double elevMaxMeters = -1;
                if (lstResult.Count == 2)   // We expect the min and max values in that order
                {
                    elevMinMeters = lstResult[0];
                    double dblMinFt = ArcGIS.Core.Geometry.LinearUnit.Meters.ConvertTo(elevMinMeters, ArcGIS.Core.Geometry.LinearUnit.Feet);
                    strElevMinFt = Convert.ToString(Math.Round(dblMinFt, 2, MidpointRounding.AwayFromZero));
                    elevMaxMeters = lstResult[1];
                    double dblMaxFt = ArcGIS.Core.Geometry.LinearUnit.Meters.ConvertTo(elevMaxMeters, ArcGIS.Core.Geometry.LinearUnit.Feet);
                    strElevMaxFt = Convert.ToString(Math.Round(dblMaxFt, 2, MidpointRounding.AwayFromZero));
                    if (dblMinFt >= 0 && dblMaxFt >=0)
                    {
                        double dblRangeFt = dblMaxFt - dblMinFt;
                        strElevRangeFt = Convert.ToString(Math.Round(dblRangeFt, 2, MidpointRounding.AwayFromZero));
                    }
                }
                lstElements.Add(strElevMinFt);
                lstElements.Add(strElevMaxFt);
                lstElements.Add(strElevRangeFt);

                //elev_median_ft
                string strElevMedianFt = "Not Found";
                string strTmpMedian = "tmpMedian";
                strOutputFeature = $@"{aoiUri.LocalPath}\{strTmpMedian}";
                string strFilledDem = $@"{GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Surfaces)}\{Constants.FILE_DEM_FILLED}";
                parameters = Geoprocessing.MakeValueArray(strInputFeatures, "AOINAME", strFilledDem,strOutputFeature, "DATA", "MEDIAN");
                gpResult = await Geoprocessing.ExecuteToolAsync("ZonalStatisticsAsTable_sa", parameters, null,
                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (gpResult.IsFailed)
                {
                    strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Unable to execute zonal statistics on filled_dem. Median elevation is not available! \r\n";
                    File.AppendAllText(strLogFile, strLogEntry);       // append
                }
                else
                {
                    // Field name is MEDIAN
                    string strMedian = await GeodatabaseTools.QueryTableForSingleValueAsync(aoiUri, strTmpMedian, "MEDIAN", new QueryFilter());
                    if (!string.IsNullOrEmpty(strMedian))
                    {
                        double dblMedian = -1;
                        bool bIsDouble = Double.TryParse(strMedian, out dblMedian);
                        if (bIsDouble)
                        {
                            double dblMedianFt = LinearUnit.Meters.ConvertTo(dblMedian, LinearUnit.Feet);
                            strElevMedianFt = Convert.ToString(Math.Round(dblMedianFt, 2, MidpointRounding.AwayFromZero));
                        }

                    }
                    // Delete temp file
                    if (await GeodatabaseTools.FeatureClassExistsAsync(aoiUri, strTmpMedian))
                    {
                        success = await GeoprocessingTools.DeleteDatasetAsync(strOutputFeature);
                    }
                }
                lstElements.Add(strElevMedianFt);

                //snotel_sites_all, snolite_sites_all, scos_sites_all, coop_sites_all
                string strSnotelAll = "0";
                string strSnoliteAll = "0";
                string strScosAll = "0";
                string strCoopAll = "0";
                string strSnotelInside = "0";
                string strSnoliteInside = "0";
                string strScosInside = "0";
                string strCoopInside = "0";
                string strSnotelOutside = "0";
                string strSnoliteOutside = "0";
                string strScosOutside = "0";
                string strCoopOutside = "0";


                Uri layersUri = new Uri(GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Layers));
                string[] arrSiteFiles = new string[] { Constants.FILE_SNOTEL, Constants.FILE_SNOLITE, Constants.FILE_SNOW_COURSE,
                    Constants.FILE_COOP_PILLOW};
                long[] arrSitesAll = new long[] { 0,0,0,0};
                long[] arrSitesInside = new long[] { 0, 0, 0, 0 };
                // Load statistics into arrays
                for (int i = 0; i < arrSiteFiles.Length; i++)
                {
                    long sitesCount = await GeodatabaseTools.CountFeaturesAsync(layersUri, arrSiteFiles[i]);
                    if (sitesCount > 0)
                    {
                        arrSitesAll[i] = sitesCount;
                    }
                    long sitesInside = await GeodatabaseTools.CountPointsWithinInFeatureAsync(layersUri, arrSiteFiles[i],
                        aoiUri, Constants.FILE_AOI_VECTOR);
                    if (sitesInside > 0)
                    {
                        arrSitesInside[i] = sitesInside;
                    }
                }
                for (int i = 0; i < arrSiteFiles.Length; i++)
                {
                    long sitesOutside = arrSitesAll[i] - arrSitesInside[i];
                    switch (arrSiteFiles[i])
                    {
                        case Constants.FILE_SNOTEL:
                            strSnotelAll = Convert.ToString(arrSitesAll[i]);
                            strSnotelInside = Convert.ToString(arrSitesInside[i]);
                            strSnotelOutside = Convert.ToString(sitesOutside);
                            break;
                        case Constants.FILE_SNOLITE:
                            strSnoliteAll = Convert.ToString(arrSitesAll[i]);
                            strSnoliteInside = Convert.ToString(arrSitesInside[i]);
                            strSnoliteOutside = Convert.ToString(sitesOutside);
                            break;
                        case Constants.FILE_SNOW_COURSE:
                            strScosAll = Convert.ToString(arrSitesAll[i]);
                            strScosInside = Convert.ToString(arrSitesInside[i]);
                            strScosOutside = Convert.ToString(sitesOutside);
                            break;
                        case Constants.FILE_COOP_PILLOW:
                            strCoopAll = Convert.ToString(arrSitesAll[i]);
                            strCoopInside = Convert.ToString(arrSitesInside[i]);
                            strCoopOutside = Convert.ToString(sitesOutside);
                            break;
                    }
                }

                //sites
                string strAutoSitesBuffer = "Not Found";
                string strScosSitesBuffer = "Not Found";
                string autoSitesPath = "";
                string scosSitesPath = "";
                if (arrSitesAll[0] > 0) // Snotel
                {
                    autoSitesPath = layersUri.LocalPath + "\\" + arrSiteFiles[0];
                }
                else if (arrSitesAll[1] > 0)    // Snolite
                {
                    autoSitesPath = layersUri.LocalPath + "\\" + arrSiteFiles[1];
                }
                else if (arrSitesAll[3] > 0)    // Coop Pillow
                {
                    autoSitesPath = layersUri.LocalPath + "\\" + arrSiteFiles[3];
                }
                if (arrSitesAll[2] > 0) // Snow Course
                {
                    scosSitesPath = layersUri.LocalPath + "\\" + arrSiteFiles[2];
                }
                    string bufferDistance = "";
                    string bufferUnits = "";
                    // Check for default units
                    await QueuedTask.Run( () =>
                    {
                        if (!string.IsNullOrEmpty(autoSitesPath))
                        {
                            Item fc = ItemFactory.Instance.Create(autoSitesPath, ItemFactory.ItemType.PathItem);
                            if (fc != null)
                            {
                                string strXml = string.Empty;
                                strXml = fc.GetXml();
                                //check metadata was returned
                                string strBagisTag = GeneralTools.GetBagisTag(strXml);
                                if (!string.IsNullOrEmpty(strBagisTag))
                                {
                                    bufferDistance = GeneralTools.GetValueForKey(strBagisTag, Constants.META_TAG_BUFFER_DISTANCE, ';');
                                    bufferUnits = GeneralTools.GetValueForKey(strBagisTag, Constants.META_TAG_XUNIT_VALUE, ';');
                                    strAutoSitesBuffer = $@"{bufferDistance} {bufferUnits}";
                                }
                            }
                        }
                        if (!string.IsNullOrEmpty(scosSitesPath))
                        {
                            Item fc = ItemFactory.Instance.Create(scosSitesPath, ItemFactory.ItemType.PathItem);
                            if (fc != null)
                            {
                                string strXml = string.Empty;
                                strXml = fc.GetXml();
                                //check metadata was returned
                                string strBagisTag = GeneralTools.GetBagisTag(strXml);
                                if (!string.IsNullOrEmpty(strBagisTag))
                                {
                                    bufferDistance = GeneralTools.GetValueForKey(strBagisTag, Constants.META_TAG_BUFFER_DISTANCE, ';');
                                    bufferUnits = GeneralTools.GetValueForKey(strBagisTag, Constants.META_TAG_XUNIT_VALUE, ';');
                                    strScosSitesBuffer = $@"{bufferDistance} {bufferUnits}";
                                }
                            }
                        }
                    });

                // auto_rep_area_pct, scos_rep_area_pct
                string strAutoRepAreaPct = "0";
                string strScosRepAreaPct = "0";
                Uri uriAnalysis = new Uri(GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath,GeodatabaseNames.Analysis));
                double aoiArea = await GeodatabaseTools.CalculateTotalPolygonAreaAsync(aoiUri, Constants.FILE_AOI_VECTOR);
                double repArea = 0;
                string[] arrRepAreaFiles = new string[] { Constants.FILE_SNOTEL_REPRESENTED, Constants.FILE_SCOS_REPRESENTED };
                for (int i = 0; i < arrRepAreaFiles.Length; i++)
                {
                    bool bExists = await GeodatabaseTools.FeatureClassExistsAsync(uriAnalysis, arrRepAreaFiles[i]);
                    if (bExists)
                    {
                        repArea = await GeodatabaseTools.CalculateTotalPolygonAreaAsync(uriAnalysis, arrRepAreaFiles[i]);
                        if (repArea > 0)
                        {
                            double repPct = (repArea / aoiArea) * 100;
                            switch (i)
                            {
                                case 0:
                                    strAutoRepAreaPct = Convert.ToString(Math.Round(repArea / aoiArea * 100));
                                    break;
                                case 1:
                                    strScosRepAreaPct = Convert.ToString(Math.Round(repArea / aoiArea * 100));
                                    break;
                            }
                        }
                    }
                }

                //forested_area_pct
                string strForestedAreaPct = "0";
                if (await GeodatabaseTools.FeatureClassExistsAsync(uriAnalysis, Constants.FILE_FORESTED_ZONE))
                {
                    string strTmpForested = "tmpForested";
                    strOutputFeature = $@"{uriAnalysis.LocalPath}\{strTmpForested}";
                    string strForestedInput = $@"{uriAnalysis.LocalPath}\{Constants.FILE_FORESTED_ZONE}";
                    parameters = Geoprocessing.MakeValueArray(strForestedInput, strInputFeatures, strOutputFeature);
                    var environments = Geoprocessing.MakeEnvironmentArray(workspace: oAoi.FilePath);
                    var gpResultClip = await Geoprocessing.ExecuteToolAsync("Clip_analysis", parameters, environments,
                            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                    if (gpResultClip.IsFailed)
                    {
                        strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Unable to clip forestedzone. forested_area_pct is not available! \r\n";
                        File.AppendAllText(strLogFile, strLogEntry);       // append
                    }
                    else
                    {
                        double forestedArea = await GeodatabaseTools.CalculateTotalPolygonAreaAsync(uriAnalysis, strTmpForested);
                        if (forestedArea > 0)
                        {
                            strForestedAreaPct = Convert.ToString(Math.Round(forestedArea / aoiArea * 100));
                        }
                        // Delete temp file
                        if (await GeodatabaseTools.FeatureClassExistsAsync(uriAnalysis, strTmpForested))
                        {
                            success = await GeoprocessingTools.DeleteDatasetAsync(strOutputFeature);
                        }
                    }
                }
                string strAspectZones = "Not Found";
                string strAspectAreaPct = "Not Found";
                string strTmpAspect = "tmpAspect";
                IList<BA_Objects.Interval> lstInterval = new List<BA_Objects.Interval>();
                IList<string> lstZoneNames = new List<string>();
                if (oAnalysis.AspectDirectionsCount > 0)
                {
                    lstInterval = GetAspectClasses(oAnalysis.AspectDirectionsCount);
                    StringBuilder sb = new StringBuilder();
                    foreach (var item in lstInterval)
                    {
                        if (! lstZoneNames.Contains(item.Name))
                        {
                            lstZoneNames.Add(item.Name);
                            sb.Append(item.Name);
                            sb.Append(",");
                        }
                    }
                    if (sb.Length > 0)
                    {
                        string strTrimmed = sb.ToString().TrimEnd(',');
                        strAspectZones = $@"""{strTrimmed}""";                        
                    }
                    if (lstInterval.Count > 0)
                    {
                        string aspectZonesPath = $@"{uriAnalysis.LocalPath}\{Constants.FILE_ASPECT_ZONE}";
                        strOutputFeature = $@"{uriAnalysis.LocalPath}\{strTmpAspect}";
                        IDictionary<string, string> dictZonalPercentages =
                            await CalculateZonalAreaPercentages(oAoi.FilePath, aspectZonesPath, Constants.FIELD_NAME, strFilledDem,
                            strOutputFeature, lstZoneNames, strLogFile);
                        StringBuilder sb2 = new StringBuilder();
                        foreach (var item in lstZoneNames)
                        {
                            if (dictZonalPercentages.ContainsKey(item))
                            {
                                sb2.Append(dictZonalPercentages[item]);
                            }
                            else
                            {
                                sb2.Append("0");
                            }
                            sb2.Append(",");
                        }
                        string strTrimmed = sb2.ToString().TrimEnd(',');
                        strAspectAreaPct = $@"""{strTrimmed}""";
                    }
                }

                lstElements.Add(strAutoSitesBuffer);
                lstElements.Add(strScosSitesBuffer);
                lstElements.Add(strSnotelAll);
                lstElements.Add(strSnoliteAll);
                lstElements.Add(strScosAll);
                lstElements.Add(strCoopAll);
                lstElements.Add(strSnotelInside);
                lstElements.Add(strSnoliteInside);
                lstElements.Add(strScosInside);
                lstElements.Add(strCoopInside);
                lstElements.Add(strSnotelOutside);
                lstElements.Add(strSnoliteOutside);
                lstElements.Add(strScosOutside);
                lstElements.Add(strCoopOutside);
                lstElements.Add(strAutoRepAreaPct);
                lstElements.Add(strScosRepAreaPct);
                lstElements.Add(strForestedAreaPct);
                lstElements.Add(strAspectZones);
                lstElements.Add(strAspectAreaPct);

            }
            return lstElements;
        }

    }

}