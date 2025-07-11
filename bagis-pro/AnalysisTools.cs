﻿using ActiproSoftware.Windows.Extensions;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Exceptions;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Internal.Catalog;
using ArcGIS.Desktop.Internal.Catalog.PropertyPages.NetworkDataset;
using ArcGIS.Desktop.Internal.Core.Conda;
using ArcGIS.Desktop.Internal.GeoProcessing;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using bagis_pro.BA_Objects;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Ink;

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
            string strStationName = "";
            string[] arrReturnValues = new string[] { strTriplet, strStationName };
            Uri ppUri = new Uri(GeodatabaseTools.GetGeodatabasePath(aoiFilePath, GeodatabaseNames.Aoi));
            string strPourpointClassPath = ppUri.LocalPath + "\\" + Constants.FILE_POURPOINT;
            // Note: Refactored this 2024-FEB-02 but couldn't test it because it's not in use
            string strWsUri = (string) Module1.Current.BagisSettings.GaugeStationUri;
            string fcstServiceLayerId = strWsUri.Split('/').Last();
            int intTrim = fcstServiceLayerId.Length + 1;
            string fcstTempString = strWsUri.Substring(0, strWsUri.Length - intTrim);
            Uri wsUri = new Uri(fcstTempString);

            bool bUpdateTriplet = false;
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
                        BA_ReturnCode success = await GeoprocessingTools.AddFieldAsync(strPourpointClassPath, strField, "TEXT", null);
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
                        string[] arrSearch = { Constants.FIELD_STATION_TRIPLET, (string)Module1.Current.BagisSettings.GaugeStationUri};
                        Webservices ws = new Webservices();
                        string[] arrFound = new string[arrSearch.Length];
                        if (!String.IsNullOrEmpty(strNearId))
                        {
                            queryFilter.WhereClause = Constants.FIELD_OBJECT_ID + " = " + strNearId;
                            arrFound = await ws.QueryServiceForValuesAsync(wsUri, "0", arrSearch, queryFilter);
                            if (arrFound != null && arrFound.Length == 3 && arrFound[0] != null)
                            {
                                strTriplet = arrFound[0];
                                strStationName = arrFound[1];
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
                if (bUpdateTriplet == true || bUpdateStationName == true)
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(GetStationValues),
                        "Updating pourpoint layer attributes");
                    IDictionary<string, string> dictEdits = new Dictionary<string, string>();
                    if (bUpdateTriplet)
                        dictEdits.Add(Constants.FIELD_STATION_TRIPLET, strTriplet);
                    if (bUpdateStationName)
                        dictEdits.Add(Constants.FIELD_STATION_NAME, strStationName);
                    BA_ReturnCode success = await GeodatabaseTools.UpdateFeatureAttributesAsync(ppUri, Constants.FILE_POURPOINT,
                        new QueryFilter(), dictEdits);

                    //Save the new values to aoi_v
                    string strAoiVPath = ppUri.LocalPath + "\\" + Constants.FILE_AOI_VECTOR;
                    string[] arrPpFields = { Constants.FIELD_STATION_TRIPLET, Constants.FIELD_STATION_NAME };
                    foreach (var strField in arrPpFields)
                    {
                        if (!await GeodatabaseTools.AttributeExistsAsync(ppUri, Constants.FILE_AOI_VECTOR, strField))
                        {
                            success = await GeoprocessingTools.AddFieldAsync(strAoiVPath, strField, "TEXT", null);
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
            string strTriplet = Constants.VALUE_MISSING;
            string strTempHuc2 = "-1";
            string strStationName = Constants.VALUE_MISSING;
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
                            success = await GeoprocessingTools.AddFieldAsync(strPourpointClassPath, strField, "INTEGER", null);
                        }
                        else
                        {
                            success = await GeoprocessingTools.AddFieldAsync(strPourpointClassPath, strField, "TEXT", null);
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
            string strWsPrefix = Module1.Current.DataSources[strDataType].uri;
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
                        // if no buffer distance selected use the AOI buffer distance
                        if (strBufferDistance.Equals("0"))
                        {
                            string[] arrResult = await GeneralTools.QueryBufferDistanceAsync(strAoiPath, strClipGdb, Constants.FILE_AOI_BUFFERED_VECTOR, false);
                            strBufferDistance = arrResult[0];
                            strBufferUnits = arrResult[1];
                        }
                        bool bCreateClipLayer = false;
                        if (string.IsNullOrEmpty(prismBufferDistance) || string.IsNullOrEmpty(prismBufferUnits))
                        {
                            bCreateClipLayer = true;
                        }
                        else if (!strBufferDistance.Trim().Equals(prismBufferDistance.Trim()))
                        {
                            bCreateClipLayer = true;
                        }
                        else if (!strBufferUnits.Trim().Equals(prismBufferUnits.Trim()))
                        {
                            bCreateClipLayer = true;
                        }
                        if (bCreateClipLayer)
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
                                parameters = Geoprocessing.MakeValueArray(strClipGdb + "\\" + strClipFile,
                                    strClipGdb + "\\" + strTempBuffer2, "0.5 Meters", "", "", "ALL");
                                gpResult = Geoprocessing.ExecuteToolAsync("Buffer_analysis", parameters, null,
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
                            // Always set the extent when clipping from an image service
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
                                if (i == 0)   // Check the first raster only
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
                                string strUnits = Module1.Current.DataSources[strDataType].units;
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
                            BA_Objects.DataSource updateDataSource = new BA_Objects.DataSource(Module1.Current.DataSources[strDataType])
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
                    string strDemUnits = Convert.ToString(Module1.Current.BagisSettings.DemUnits);
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
            if (Module1.Current.DataSources != null)
            {
                if (!Module1.Current.DataSources.ContainsKey(Constants.DATA_TYPE_SNOTEL) ||
                    !Module1.Current.DataSources.ContainsKey(Constants.DATA_TYPE_SNOW_COURSE) ||
                    !Module1.Current.DataSources.ContainsKey(Constants.DATA_TYPE_SNOLITE) ||
                    !Module1.Current.DataSources.ContainsKey(Constants.DATA_TYPE_COOP_PILLOW))
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(ClipSnoLayersAsync),
                        "Unable to retrieve snotel datasource information from " + Constants.FILE_BAGIS_SETTINGS +
                        ". Clipping cancelled!!");
                    return success;
                }
            }
            else
            {
                Module1.Current.ModuleLogManager.LogError(nameof(ClipSnoLayersAsync),
                    "Unable to retrieve datasource information from " + Constants.FILE_BAGIS_SETTINGS +
                    ". Clipping cancelled!!");
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

                string strWsUri = Module1.Current.DataSources[Constants.DATA_TYPE_SNOTEL].uri;
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
                strWsUri = Module1.Current.DataSources[Constants.DATA_TYPE_SNOLITE].uri;
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
                strWsUri = Module1.Current.DataSources[Constants.DATA_TYPE_COOP_PILLOW].uri;
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

                string strWsUri = Module1.Current.DataSources[Constants.DATA_TYPE_SNOW_COURSE].uri;
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
                    success = await GeoprocessingTools.AddFieldAsync(strFc, Constants.FIELD_SITE_NAME, "TEXT", null);
                    if (success == BA_ReturnCode.Success)
                    {
                        success = await GeoprocessingTools.AddFieldAsync(strFc, Constants.FIELD_SITE_ELEV, "DOUBLE", null);
                        if (success == BA_ReturnCode.Success)
                        {
                            success = await GeoprocessingTools.AddFieldAsync(strFc, Constants.FIELD_SITE_TYPE, "TEXT", null);
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
                                string sourceName = Convert.ToString(Module1.Current.BagisSettings.SnotelName);
                                if (i == 1)  // This is a snow course layer
                                {
                                    sourceName = Convert.ToString(Module1.Current.BagisSettings.SnotelName);
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
                    if (j == 1)   // Working with snow course layer
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
                    BA_Objects.DataSource updateDataSource = new BA_Objects.DataSource(Module1.Current.DataSources[strKey])
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
            string strTestDistance = arrResult[0] + " " + arrResult[1];
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
            string strWsUri = Module1.Current.DataSources[strDataType].uri;

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
                                strClipGdb + "\\" + strTempBuffer2, "0.5 Meters", "ALL", CancelableProgressor.None);
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
                            string strUnits = Module1.Current.DataSources[strDataType].units;
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
                        BA_Objects.DataSource updateDataSource = new BA_Objects.DataSource(Module1.Current.DataSources[strDataType])
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
                StringBuilder sb = new StringBuilder();
                foreach (var code in Constants.VALUES_NLCD_FORESTED_AREA)
                {
                    sb.Append($@"{code},");
                }
                string strCodes = sb.ToString().TrimEnd(',');
                slectionLayer.SetDefinitionQuery(Constants.FIELD_GRID_CODE + " IN (" + strCodes + ")");
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
            if (!await GeodatabaseTools.FeatureClassExistsAsync(uriAnalysisGdb, Constants.FILE_ELEV_ZONES_VECTOR))
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
            int intAspectCount = Convert.ToInt16(Module1.Current.BagisSettings.AspectDirectionsCount);
            IList<BA_Objects.Interval> lstAspectInterval = AnalysisTools.GetAspectClasses(intAspectCount);

            // Create the elevation-precipitation layer
            Uri uriSurfaces = new Uri(GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Surfaces, true) + Constants.FILE_DEM_CLIPPED);
            Uri uriAnalysis = new Uri(GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Analysis));
            Uri uriLayers = new Uri(GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Layers));
            double dblDemCellSize = await GeodatabaseTools.GetCellSizeAsync(uriSurfaces, WorkspaceType.Geodatabase);
            Uri uriPrismFull = new Uri($@"{uriPrism.LocalPath}\{prismFile}");
            double dblPrismCellSize = await GeodatabaseTools.GetCellSizeAsync(uriPrismFull, WorkspaceType.Geodatabase);
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
                    string aspectZonesPath = uriAnalysis.LocalPath + "\\" + Constants.FILE_ASPECT_ZONE;
                    double dblAspectCellSize = await GeodatabaseTools.GetCellSizeAsync(new Uri(aspectZonesPath), WorkspaceType.Geodatabase);                    
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
                        "", "", "", "", "ROW_WISE", "FEATURE_CLASS");
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

                    success = await GeoprocessingTools.AddFieldAsync(uriAnalysis.LocalPath + "\\" + Constants.FILE_PREC_MEAN_ELEV_V, Constants.FIELD_DIRECTION, "TEXT", null);
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
            string strWsUri = Module1.Current.DataSources[strDataType].uri;
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
                        success = await MapTools.DisplayRasterLayerAsync(Constants.MAPS_DEFAULT_MAP_NAME, uri, strInputRaster, false);
                    }
                    if (success != BA_ReturnCode.Success)
                    {
                        return;
                    }
                    string strTemplateDataset = strClipGdb + "\\" + strClipFile;
                    // Always set the extent when clipping from an image service
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
                        string strUnits = Module1.Current.DataSources[strDataType].units;
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
                    BA_Objects.DataSource updateDataSource = new BA_Objects.DataSource(Module1.Current.DataSources[strDataType])
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

        public static async Task<BA_ReturnCode> ClipRasterLayerNoBufferAsync(string strAoiPath, string strClipPath, string strClipExtent, 
            string inputRaster, string outputRaster, string snapRasterPath, CancelableProgressor prog)
        {
            // Query the extent for the clip
            string strClipGdb = Path.GetDirectoryName(strClipPath);
            string strClipFile = Path.GetFileName(strClipPath);
            string strClipEnvelope = await GeodatabaseTools.GetEnvelope(strClipGdb, strClipFile);
            if (String.IsNullOrEmpty(strClipEnvelope))
            {
                Module1.Current.ModuleLogManager.LogError(nameof(ClipRasterLayerAsync),
                    "Unable obtain clipping envelope from " + strClipGdb + "\\" + strClipFile);
                return BA_ReturnCode.ReadError;
            }
            var parameters = Geoprocessing.MakeValueArray(inputRaster, strClipEnvelope, outputRaster, strClipPath,
                "", "ClippingGeometry");
            // Always set the extent if clipping from an image service
            var environments = Geoprocessing.MakeEnvironmentArray(workspace: strAoiPath, snapRaster: snapRasterPath, extent: strClipEnvelope);
            var gpResult = await Geoprocessing.ExecuteToolAsync("Clip_management", parameters, environments,
                            prog, GPExecuteToolFlags.AddToHistory);
            if (gpResult.IsFailed)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(ClipRasterLayerAsync), "Unable to clip " + inputRaster + " to " + outputRaster);
                foreach (var objMessage in gpResult.Messages)
                {
                    IGPMessage msg = (IGPMessage)objMessage;
                    Module1.Current.ModuleLogManager.LogError(nameof(ClipRasterLayerAsync), msg.Text);
                }
                return BA_ReturnCode.ReadError;
            }
            else
            {
                return BA_ReturnCode.Success;
            }
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
            string strDemUnits = (string)Module1.Current.BagisSettings.DemUnits;
            string strDemDisplayUnits = (string)Module1.Current.BagisSettings.DemDisplayUnits;
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
                if (dblZoneCount >= (int)Module1.Current.BagisSettings.MinElevationZonesCount)
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
                    string strBufferDistance = (string)Module1.Current.BagisSettings.RoadsAnalysisBufferDistance;
                    string strBufferUnits = (string)Module1.Current.BagisSettings.RoadsAnalysisBufferUnits;
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
            int prismZonesCount = (int)Module1.Current.BagisSettings.PrecipZonesCount;
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
            int aspectDirectionsCount = Convert.ToInt16(Module1.Current.BagisSettings.AspectDirectionsCount);
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

            double dblCellSize = await GeodatabaseTools.GetCellSizeAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Analysis, true) + Constants.FILE_PRECIPITATION_CONTRIBUTION),
                WorkspaceType.Geodatabase);
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
                                // Square cell size to calculate area for volume in acre feet
                                double dblVolAcreFeet = dblSum * dblCellSize * dblCellSize * (1 / (4046.8564224 * 12));
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
            BA_ReturnCode success = await GeoprocessingTools.AddFieldAsync(watershedOutputPath, Constants.FIELD_VOL_ACRE_FT, "INTEGER", null);
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
                    var environments = Geoprocessing.MakeEnvironmentArray(workspace: strPrismGdb);
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
                if (Enum.IsDefined(typeof(PrismFile), intMonth - 1))
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
                        lstInputRasters.Add(Path.GetFileName((string)Module1.Current.BagisSettings.AoiPrecipFile));
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
                    success = await GeoprocessingTools.AddFieldAsync(featureClassToUpdate, lstFields[i], lstFieldDataTypes[i], null);
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
                if (!await GeodatabaseTools.AttributeExistsAsync(new Uri(analysisPath), Constants.FILE_MERGED_SITES, Constants.FIELD_DIRECTION))
                {
                    success = await GeoprocessingTools.AddFieldAsync(featureClassToUpdate, Constants.FIELD_DIRECTION, "TEXT", null);
                }
                if (success == BA_ReturnCode.Success)
                {
                    int intAspectCount = Convert.ToInt16(Module1.Current.BagisSettings.AspectDirectionsCount);
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
                            Constants.FIELD_SITE_TYPE, "TEXT", null);
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
                            Constants.FIELD_SITE_ID, "INTEGER", null);
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
                    returnPath = "";
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
            string[] arrFields = { Constants.FIELD_PRECIP, Constants.FIELD_ASPECT, Constants.FIELD_SLOPE };
            string[] arrFieldDataTypes = { "DOUBLE", "DOUBLE", "DOUBLE" };
            string[] arrUri = { GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Prism),
                                    GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Surfaces),
                                    GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Surfaces)};
            string[] arrInputRasters = {Path.GetFileName((string)Module1.Current.BagisSettings.AoiPrecipFile),
                                            Constants.FILE_ASPECT,
                                            Constants.FILE_SLOPE};
            bool[] arrIsImageService = { false, false, false };
            string prismImageUri = await GetPrismImageUriAsync(sitesGdbUri, aoiUri, totalSites);
            if (!string.IsNullOrEmpty(prismImageUri))
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
                if (!await GeodatabaseTools.AttributeExistsAsync(analysisUri, Constants.FILE_MERGED_SITES, arrFields[i]))
                {
                    success = await GeoprocessingTools.AddFieldAsync(returnPath, arrFields[i], arrFieldDataTypes[i], null);
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
                success = await GeoprocessingTools.AddFieldAsync(returnPath, Constants.FIELD_DIRECTION, "TEXT", null);
                if (success == BA_ReturnCode.Success)
                {
                    int intAspectCount = Convert.ToInt16(Module1.Current.BagisSettings.AspectDirectionsCount);
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
            string demUri = "";
            DataSource dsDem = new BA_Objects.DataSource(Module1.Current.DataSources[DataSource.GetDemKey]);
            if (dsDem != null)
            {
                demUri = dsDem.uri;
            }
            if (string.IsNullOrEmpty(demUri))
            {
                Module1.Current.ModuleLogManager.LogError(nameof(ReclipSurfacesAsync),
                    $@"Unable to find element 30m DEM in server data sources");
                return BA_ReturnCode.ReadError;
            }
            string clipEnvelope = "";
            string strOutputFeatures = GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Analysis, true) +
                "tmpBuffer";
            string strDistance = "5 Kilometers";
            if (Module1.Current.BagisSettings.SnotelBufferDistance != null)
            {
                strDistance = (string)Module1.Current.BagisSettings.SnotelBufferDistance + " " +
                    (string)Module1.Current.BagisSettings.PrecipBufferUnits;
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
            string outputRaster = GeodatabaseTools.GetGeodatabasePath(aoiFolderPath, GeodatabaseNames.Analysis, true) + Constants.FILE_SITES_DEM;
            var environments = Geoprocessing.MakeEnvironmentArray(workspace: aoiFolderPath,
                snapRaster: BA_Objects.Aoi.SnapRasterPath(aoiFolderPath));
            IGPResult gpResult = null;

            BA_ReturnCode success = await GeoprocessingTools.ClipRasterAsync(demUri, clipEnvelope, outputRaster, null, null, false,
                    aoiFolderPath, BA_Objects.Aoi.SnapRasterPath(aoiFolderPath));
            if (success != BA_ReturnCode.Success)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(ReclipSurfacesAsync),
                    "Failed to clip DEM for buffered sites layer using ClipRasterAsync. Attempting ClipRasterToLayerAsync");
                success = await GeoprocessingTools.ClipRasterAsLayerAsync(demUri, clipEnvelope, outputRaster, null, null, false,
                aoiFolderPath, BA_Objects.Aoi.SnapRasterPath(aoiFolderPath));
            }
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
            if (!String.IsNullOrEmpty(clipEnvelope))
            {
                success = await GeoprocessingTools.DeleteDatasetAsync(strOutputFeatures);
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
                string strWsPrefix = Module1.Current.DataSources[DataSource.GetPrecipitationKey].uri;
                if (!string.IsNullOrEmpty(strWsPrefix))
                {
                    string localLayerName = Path.GetFileName((string)Module1.Current.BagisSettings.AoiPrecipFile);
                    PrismFile prismFile = (PrismFile)Enum.Parse(typeof(PrismFile), localLayerName);
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
            int maxSitesAllowed = (int)Module1.Current.BagisSettings.MaximumSitesAllowed;
            if (Module1.Current.DataSources != null)
            {
                if (!Module1.Current.DataSources.ContainsKey(Constants.DATA_TYPE_SNOTEL) ||
                    !Module1.Current.DataSources.ContainsKey(Constants.DATA_TYPE_SNOW_COURSE))
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(TooManySitesAsync),
                        "Unable to retrieve snotel datasource information from " + Constants.FILE_BAGIS_SETTINGS +
                        ". Processing cancelled!!");
                    return true;
                }
            }
            string[] arrWsUri = new string[]
                { Module1.Current.DataSources[Constants.DATA_TYPE_SNOTEL].uri, Module1.Current.DataSources[Constants.DATA_TYPE_SNOW_COURSE].uri };
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

        private static async Task<BA_ReturnCode> UpdateSiteIdsAsync(string analysisPath, Uri uriLayers, bool[] arrHasSites,
            string[] arrSiteFiles)
        {
            // Sort by elevation and set site id
            Uri analysisUri = new Uri(analysisPath);
            bool hasSiteId = await GeodatabaseTools.AttributeExistsAsync(analysisUri, Constants.FILE_MERGED_SITES, Constants.FIELD_SITE_ID);
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            if (hasSiteId == false)
            {
                success = await GeoprocessingTools.AddFieldAsync(analysisPath + "\\" + Constants.FILE_MERGED_SITES, Constants.FIELD_SITE_ID, "INTEGER", null);
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
            string strInRaster, string strOutputTable, string strAnalysisMask, IDictionary<string, string> dictZoneNames, string strLogFile)
        {
            var parameters = Geoprocessing.MakeValueArray(strZonalLayerPath, strZonalField, strInRaster, strOutputTable, "DATA", "MINIMUM");
            var environments = Geoprocessing.MakeEnvironmentArray(workspace: strAoiFolder, mask: strAnalysisMask);
            var gpResult = await Geoprocessing.ExecuteToolAsync("ZonalStatisticsAsTable_sa", parameters, environments,
                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            string strLogEntry = "";
            IDictionary<string, string> dictZonalPercentages = new Dictionary<string, string>();
            IDictionary<string, long> dictCounts = new Dictionary<string, long>();
            if (gpResult.IsFailed)
            {
                strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Unable to execute zonal statistics on filled_dem. Aspect Percent areas are not available! \r\n";
                File.AppendAllText(strLogFile, strLogEntry);       // append
            }
            else
            {
                foreach (var strKey in dictZoneNames.Keys)
                {
                    dictCounts.Add(strKey, 0);
                }
                Uri gdbUri = new Uri(Path.GetDirectoryName(strOutputTable));
                long lngTotalCount = 0;
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
                                            long lngCount = Convert.ToInt64(aRow[idxCount]);
                                            if (!string.IsNullOrEmpty(strName) && dictCounts.ContainsKey(strName))
                                            {
                                                dictCounts[strName] = lngCount;
                                                lngTotalCount = lngTotalCount + lngCount;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    foreach (var key in dictCounts.Keys)
                    {
                        double dblPercent = (double)Math.Round((double)(100 * dictCounts[key]) / lngTotalCount, 1);
                        dictZonalPercentages.Add(key, Convert.ToString(dblPercent));
                    }
                });
            }
            return dictZonalPercentages;
        }

        public static async Task<string[]> CalculateSiteCountElevZone(string strAoiFilePath, IList<BA_Objects.Interval> lstInterval)
        {
            // 1. Put intervals into 2 Dictionaries (auto zones and scos zones)
            IDictionary<string, int> dictAutoSites = new Dictionary<string, int>();
            IDictionary<string, int> dictScosSites = new Dictionary<string, int>();
            foreach (var interval in lstInterval)
            {
                string value = Convert.ToString(interval.Value);
                if (!dictAutoSites.ContainsKey(value))
                {
                    dictAutoSites.Add(value, 0);
                    dictScosSites.Add(value, 0);
                }
            }
            // 2. Use AssembleSitesListAsync() twice to query the sites
            IList<Site> lstAllSites = new List<Site>();
            Uri uriAnalysis = new Uri(GeodatabaseTools.GetGeodatabasePath(strAoiFilePath, GeodatabaseNames.Analysis));
            if (await GeodatabaseTools.FeatureClassExistsAsync(uriAnalysis, Constants.FILE_MERGED_SITES))
            {
                lstAllSites = await AssembleMergedSitesListAsync(uriAnalysis);
            }
            // 3. Loop through the sites list and assign site to an interval depending on elevation
            foreach (var oSite in lstAllSites)
            {
                string key = "-1";
                double sElev = oSite.ElevMeters;
                for (int i = 0; i < lstInterval.Count; i++)
                {
                    Interval oInterval = lstInterval[i];
                    if (oSite.ElevMeters > oInterval.LowerBound && oSite.ElevMeters <= oInterval.UpperBound)
                    {
                        key = Convert.ToString(oInterval.Value);
                        break;
                    }
                    // Site is outside aoi elevation range
                    if (key.Equals("-1"))
                    {
                        Interval firstInterval = lstInterval[0];
                        Interval lastInterval = lstInterval[lstInterval.Count - 1];
                        if (oSite.ElevMeters <= firstInterval.UpperBound)
                        {
                            key = Convert.ToString(firstInterval.Value);
                        }
                        else if (oSite.ElevMeters > lastInterval.LowerBound)
                        {
                            key = Convert.ToString(lastInterval.Value);
                        }
                    }
                }
                switch (oSite.SiteType)
                {
                    case SiteType.Snotel:
                        dictAutoSites[key] = dictAutoSites[key] + 1;
                        break;
                    case SiteType.SnowCourse:
                        dictScosSites[key] = dictScosSites[key] + 1;
                        break;
                    case SiteType.Snolite:
                        dictAutoSites[key] = dictAutoSites[key] + 1;
                        break;
                    case SiteType.CoopPillow:
                        dictAutoSites[key] = dictAutoSites[key] + 1;
                        break;
                    default:
                        break;
                }
            }
            string strAutoSites = "";
            string strScosSites = "";
            StringBuilder sb = new StringBuilder();
            foreach (var entry in dictAutoSites)
            {
                sb.Append(Convert.ToString(dictAutoSites[entry.Key]));
                sb.Append(',');
            }
            if (sb.Length > 0)
            {
                string strTrimmed = sb.ToString().TrimEnd(',');
                strAutoSites = $@"""{strTrimmed}""";
            }
            sb.Clear();
            foreach (var entry in dictScosSites)
            {
                sb.Append(Convert.ToString(dictScosSites[entry.Key]));
                sb.Append(',');
            }
            if (sb.Length > 0)
            {
                string strTrimmed = sb.ToString().TrimEnd(',');
                strScosSites = $@"""{strTrimmed}""";
            }
            string[] retVal = new string[2] { strAutoSites, strScosSites };
            return retVal;
        }

        public static async Task<IList<string>> QueryCriticalPrecipElevZones(BA_Objects.Aoi oAoi, string strLogFile)
        {
            IList<string> lstValues = new List<string>();
            Uri uriAnalysis = new Uri(GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis));
            if (!await GeodatabaseTools.FeatureClassExistsAsync(uriAnalysis, Constants.FILE_CRITICAL_PRECIP_ZONE))
            {
                string strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + Constants.FILE_CRITICAL_PRECIP_ZONE + " is missing. Run the batch tool before generating forecast statistics ! \r\n";
                File.AppendAllText(strLogFile, strLogEntry);       // append
                return lstValues;
            }

            await QueuedTask.Run(() =>
            {
                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(uriAnalysis)))
                {
                    using (FeatureClass oFeatureClass = geodatabase.OpenDataset<FeatureClass>(Constants.FILE_CRITICAL_PRECIP_ZONE))
                    {
                        int idxValue = oFeatureClass.GetDefinition().FindField(Constants.FIELD_GRID_CODE);
                        if (idxValue > -1)
                        {
                            using (RowCursor aCursor = oFeatureClass.Search(new QueryFilter(), false))
                            {
                                while (aCursor.MoveNext())
                                {
                                    using (Feature feature = (Feature)aCursor.Current)
                                    {
                                        string strValue = Convert.ToString(feature[idxValue]);
                                        if (!lstValues.Contains(strValue))
                                        {
                                            lstValues.Add(strValue);
                                        }

                                    }
                                }
                            }
                        }
                    }
                }
            });
            return lstValues;
        }

        private static async Task<double> CalculateBufferedSiteAreaSqM(Aoi oAoi, string strBufferDistance)
        {
            double dblSiteArea = -1;
            string aoiFcPath = $@"{GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Aoi, true)}{Constants.FILE_AOI_VECTOR}";
            string outputGdb = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis);
            string strTempBuffer = "tmpSitesBuffer";
            BA_ReturnCode success = await GeoprocessingTools.BufferAsync(aoiFcPath,
                outputGdb + "\\" + strTempBuffer, strBufferDistance, "ALL", CancelableProgressor.None);
            if (success == BA_ReturnCode.Success)
            {
                double dblArea = await GeodatabaseTools.CalculateTotalPolygonAreaAsync(new Uri(outputGdb), strTempBuffer, "");
                if (dblArea > 0)
                    dblSiteArea = dblArea;
            }
            if (await GeodatabaseTools.FeatureClassExistsAsync(new Uri(outputGdb), strTempBuffer))
            {
                success = await GeoprocessingTools.DeleteDatasetAsync($@"{outputGdb}\{strTempBuffer}");
            }
            return dblSiteArea;
        }

        public static async Task<IList<string>> GenerateForecastStatisticsList(BA_Objects.Aoi oAoi, string strLogFile, BA_ReturnCode runOffData)
        {
            IList<string> lstElements = new List<string>();
            lstElements.Add(oAoi.StationTriplet);   // Station triplet
            lstElements.Add(oAoi.Name);  //AOI Name
            // Retrieve AOI Analysis object with settings for future use
            BA_Objects.Analysis oAnalysis = GeneralTools.GetAnalysisSettings(oAoi.FilePath);
            string strLogEntry;

            // aoiArea_SqMeters
            double areaSqM = -1;
            bool bIsMeters = false;
            string strAreaSqM = Convert.ToString(areaSqM);
            string strAreaSqMiles = strAreaSqM;
            string strAnnRunoffRatioPct = strAreaSqM;
            Uri aoiUri = new Uri(GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Aoi));
            // Calculate AOI area in SqM
            var result = await GeodatabaseTools.CalculateAoiAreaSqMetersAsync(oAoi.FilePath, areaSqM);
            areaSqM = result.Item1;
            bIsMeters = result.Item2;
            if (areaSqM != -1)
            {
                strAreaSqM = String.Format("{0:0.00}", areaSqM);
                // aoiArea_SqMiles
                strAreaSqMiles = String.Format("{0:0.00}", AreaUnit.SquareMeters.ConvertTo(areaSqM, AreaUnit.SquareMiles));
            }
            if (!bIsMeters)
            {
                strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Projection mismatch: Linear units for aoi_v are NOT in meters! \r\n";
                File.AppendAllText(strLogFile, strLogEntry);       // append

            }
            lstElements.Add(strAreaSqM);
            lstElements.Add(strAreaSqMiles);
            // ann_runoff_ratio_pct
            if (runOffData == BA_ReturnCode.Success && oAnalysis != null)
            {
                // Query for the annual runoff value
                string annualRunoffField = (string)Module1.Current.BagisSettings.AnnualRunoffDataField;
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
            string strPctAreaOutsideUsa = "Not Found";
            if (Module1.Current.BagisSettings.USStateBoundaries != null)
            {
                string strStatesUrl = Convert.ToString(Module1.Current.BagisSettings.USStateBoundaries);
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

                    // Area outside USA
                    string strTmpStatesProj = "tmpStatesProj";
                    strOutputFeatureProj = $@"{aoiUri.LocalPath}\{strTmpStatesProj}";
                    // Spatial reference for NAD 1983 Albers North America
                    SpatialReference oSpatialReference = SpatialReferenceBuilder.CreateSpatialReference(102008);
                    parameters = Geoprocessing.MakeValueArray(strOutputFeature, strOutputFeatureProj, oSpatialReference);
                    gpResult = await Geoprocessing.ExecuteToolAsync("Project_management", parameters, null,
                        CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                    if (gpResult.IsFailed)
                    {
                        strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Unable to Project tmpStates. Area outside USA is not available! \r\n";
                        File.AppendAllText(strLogFile, strLogEntry);       // append
                    }
                    else
                    {
                        double dblArea = await GeodatabaseTools.CalculateTotalPolygonAreaAsync(aoiUri, strTmpStatesProj, "");
                        if (dblArea > 0)
                        {
                            double dblAreaSqM = -1;
                            bool isDouble = Double.TryParse(strAreaSqM, out dblAreaSqM);
                            if (isDouble)
                            {

                                double dblPctOutside = ((dblAreaSqM - dblArea) / dblAreaSqM) * 100;
                                // dblPctOutside may be < 0 due to rounding/projection differences
                                if (dblPctOutside > 0)
                                {
                                    strPctAreaOutsideUsa = String.Format("{0:0.#}", dblPctOutside);
                                }
                                else
                                {
                                    strPctAreaOutsideUsa = "0";
                                }
                            }
                        }
                    }

                    // Delete temp file(s)
                    if (await GeodatabaseTools.FeatureClassExistsAsync(aoiUri, strTmpStates))
                    {
                        success = await GeoprocessingTools.DeleteDatasetAsync(strOutputFeature);
                    }
                    if (await GeodatabaseTools.FeatureClassExistsAsync(aoiUri, strTmpStatesProj))
                    {
                        success = await GeoprocessingTools.DeleteDatasetAsync(strOutputFeatureProj);
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
                double dblMinFt = elevMinMeters;
                double dblMaxFt = elevMaxMeters;
                if (lstResult.Count == 2)   // We expect the min and max values in that order
                {
                    elevMinMeters = lstResult[0];
                    dblMinFt = LinearUnit.Meters.ConvertTo(elevMinMeters, LinearUnit.Feet);
                    strElevMinFt = Convert.ToString(Math.Round(dblMinFt, 2, MidpointRounding.AwayFromZero));
                    elevMaxMeters = lstResult[1];
                    dblMaxFt = LinearUnit.Meters.ConvertTo(elevMaxMeters, LinearUnit.Feet);
                    strElevMaxFt = Convert.ToString(Math.Round(dblMaxFt, 2, MidpointRounding.AwayFromZero));
                    if (dblMinFt >= 0 && dblMaxFt >= 0)
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
                parameters = Geoprocessing.MakeValueArray(strInputFeatures, Constants.FIELD_STATION_NAME, strFilledDem, strOutputFeature, "DATA", "MEDIAN");
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
                    if (await GeodatabaseTools.TableExistsAsync(aoiUri, strTmpMedian))
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
                long[] arrSitesAll = new long[] { 0, 0, 0, 0 };
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
                await QueuedTask.Run(() =>
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
                Uri uriAnalysis = new Uri(GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis));
                double aoiArea = await GeodatabaseTools.CalculateTotalPolygonAreaAsync(aoiUri, Constants.FILE_AOI_VECTOR, null);
                double repArea = 0;
                string[] arrRepAreaFiles = new string[] { Constants.FILE_SNOTEL_REPRESENTED, Constants.FILE_SCOS_REPRESENTED };
                for (int i = 0; i < arrRepAreaFiles.Length; i++)
                {
                    bool bExists = await GeodatabaseTools.FeatureClassExistsAsync(uriAnalysis, arrRepAreaFiles[i]);
                    if (bExists)
                    {
                        repArea = await GeodatabaseTools.CalculateTotalPolygonAreaAsync(uriAnalysis, arrRepAreaFiles[i], null);
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
                        double forestedArea = await GeodatabaseTools.CalculateTotalPolygonAreaAsync(uriAnalysis, strTmpForested, null);
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
                string strAnalysisMask = $@"{aoiUri.LocalPath}\{Constants.FILE_AOI_VECTOR}";
                IList<BA_Objects.Interval> lstInterval = new List<BA_Objects.Interval>();
                IDictionary<string, string> dictZoneNames = new Dictionary<string, string>();
                if (oAnalysis.AspectDirectionsCount > 0)
                {
                    lstInterval = GetAspectClasses(oAnalysis.AspectDirectionsCount);
                    StringBuilder sb = new StringBuilder();
                    foreach (var item in lstInterval)
                    {
                        if (!dictZoneNames.ContainsKey((item.Name)))
                        {
                            dictZoneNames.Add(item.Name, "");
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
                            strOutputFeature, strAnalysisMask, dictZoneNames, strLogFile);
                        StringBuilder sb2 = new StringBuilder();
                        foreach (var strKey in dictZoneNames.Keys)
                        {
                            if (dictZonalPercentages.ContainsKey(strKey))
                            {
                                sb2.Append(dictZonalPercentages[strKey]);
                            }
                            else
                            {
                                sb2.Append("0");
                            }
                            sb2.Append(",");
                        }
                        string strTrimmed = sb2.ToString().TrimEnd(',');
                        strAspectAreaPct = $@"""{strTrimmed}""";
                        // Delete temp file
                        if (await GeodatabaseTools.FeatureClassExistsAsync(uriAnalysis, strTmpAspect))
                        {
                            success = await GeoprocessingTools.DeleteDatasetAsync(strOutputFeature);
                        }
                    }
                }
                string strElevZonesDef = "Not Found";
                string strElevAreaPct = "Not Found";
                string strTmpElev = "tmpElev";
                IDictionary<string, string> dictZoneValues = new Dictionary<string, string>();
                IDictionary<string, string> dictElevZonalPercentages = new Dictionary<string, string>();
                if (oAnalysis.ElevationZonesInterval > 0 &&
                    dblMinFt > -1 && dblMaxFt > -1)
                {
                    StringBuilder sb = new StringBuilder();
                    string strDemUnits = (string)Module1.Current.BagisSettings.DemUnits;
                    string strDemDisplayUnits = (string)Module1.Current.BagisSettings.DemDisplayUnits;
                    lstInterval = GetElevationClasses(dblMinFt, dblMaxFt, oAnalysis.ElevationZonesInterval,
                        strDemUnits, strDemDisplayUnits);
                    for (int i = 0; i < lstInterval.Count; i++)

                    {
                        // Need to massage the start and end intervals for rounding
                        Interval item = lstInterval[i];
                        string[] arrPieces = item.Name.Split(' ');
                        string strName = "";
                        if (arrPieces.Count() == 3)
                        {
                            double dblLowerBound = Convert.ToDouble(arrPieces[0]);
                            string strLowerBound = String.Format("{0:0.##}", dblLowerBound);
                            double dblUpperBound = Convert.ToDouble(arrPieces[2]);
                            string strUpperBound = String.Format("{0:0.##}", dblUpperBound);
                            if (i == 0)
                            {
                                // The first interval
                                strName = $@"<{strUpperBound}";
                            }
                            else if (i == (lstInterval.Count - 1))
                            {
                                strName = $@">{strLowerBound}";
                            }
                            else
                            {
                                strName = $@"{strLowerBound}-{strUpperBound}";
                            }
                            sb.Append(strName);
                            sb.Append(",");
                        }
                        string strValue = Convert.ToString(item.Value);
                        if (!dictZoneValues.ContainsKey((strValue)))
                        {
                            dictZoneValues.Add(strValue, strName);
                        }
                    }
                    if (sb.Length > 0)
                    {
                        string strTrimmed = sb.ToString().TrimEnd(',');
                        strElevZonesDef = $@"""{strTrimmed}""";
                    }
                    if (lstInterval.Count > 0)
                    {
                        string elevZonesPath = $@"{uriAnalysis.LocalPath}\{Constants.FILE_ELEV_ZONE}";
                        strOutputFeature = $@"{uriAnalysis.LocalPath}\{strTmpElev}";
                        dictElevZonalPercentages =
                            await CalculateZonalAreaPercentages(oAoi.FilePath, elevZonesPath, Constants.FIELD_VALUE, strFilledDem,
                            strOutputFeature, strAnalysisMask, dictZoneValues, strLogFile);
                        StringBuilder sb2 = new StringBuilder();
                        foreach (var strKey in dictZoneValues.Keys)
                        {
                            if (dictElevZonalPercentages.ContainsKey(strKey))
                            {
                                sb2.Append(dictElevZonalPercentages[strKey]);
                            }
                            else
                            {
                                sb2.Append('0');
                            }
                            sb2.Append(',');
                        }
                        string strTrimmed = sb2.ToString().TrimEnd(',');
                        strElevAreaPct = $@"""{strTrimmed}""";
                        // Delete temp file
                        if (await GeodatabaseTools.FeatureClassExistsAsync(uriAnalysis, strTmpElev))
                        {
                            success = await GeoprocessingTools.DeleteDatasetAsync(strOutputFeature);
                        }
                    }
                }

                // Site counts per elevation zone
                string strAutoSiteCountElevZone = "Not Found";
                string strScosSiteCountElevZone = "Not Found";
                string[] arrCounts = await CalculateSiteCountElevZone(oAoi.FilePath, lstInterval);
                if (arrCounts.Length == 2)
                {
                    if (arrCounts[0] != null && arrCounts[0].Length > 0)
                    {
                        strAutoSiteCountElevZone = arrCounts[0];
                    }
                    if (arrCounts[1] != null && arrCounts[1].Length > 0)
                    {
                        strScosSiteCountElevZone = arrCounts[1];
                    }
                }

                // Critical precipitation zones definition
                string strCriticalPrecipZonesDef = "Not Found";
                // Get a list of the critical precip zone (gridcode) ids
                IList<string> lstValues = await QueryCriticalPrecipElevZones(oAoi, strLogFile);
                if (lstValues.Count > 0)
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (var item in lstValues)
                    {
                        if (dictZoneValues.ContainsKey(item))
                        {
                            sb.Append(dictZoneValues[item]);
                            sb.Append(',');
                        }
                    }
                    if (sb.Length > 0)
                    {
                        string strTrimmed = sb.ToString().TrimEnd(',');
                        strCriticalPrecipZonesDef = $@"""{strTrimmed}""";
                    }
                }

                // Critical precip zones pct of AOI area
                string strCriticalPrecipPctArea = "Not Found";
                double dblCriticalPrecipPctArea = -1;
                foreach (var strKey in dictElevZonalPercentages.Keys)
                {
                    if (lstValues.Contains(strKey))
                    {
                        dblCriticalPrecipPctArea = dblCriticalPrecipPctArea + Convert.ToDouble(dictElevZonalPercentages[strKey]);
                    }
                }
                if (dblCriticalPrecipPctArea > 0)
                {
                    strCriticalPrecipPctArea = String.Format("{0:0.#}", dblCriticalPrecipPctArea);
                }

                // Wilderness area percent
                string strWildernessAreaPct = "Not Found";
                string strTmpWilderness = "tmpWilderness";
                strOutputFeature = $@"{uriAnalysis.LocalPath}\{strTmpWilderness}";
                string strWildernessInput = $@"{layersUri.LocalPath}\{Constants.FILE_LAND_OWNERSHIP}";
                parameters = Geoprocessing.MakeValueArray(strWildernessInput, strInputFeatures, strOutputFeature);
                gpResult = await Geoprocessing.ExecuteToolAsync("Clip_analysis", parameters, null,
                        CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (gpResult.IsFailed)
                {
                    strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Unable to clip Land Ownership. wilderness_area_pct is not available! \r\n";
                    File.AppendAllText(strLogFile, strLogEntry);       // append
                }
                else
                {
                    string strQuery = $@"UPPER({Constants.FIELD_AGBUR}) LIKE '%WILDERNESS%'";
                    double wildernessArea = await GeodatabaseTools.CalculateTotalPolygonAreaAsync(uriAnalysis, strTmpWilderness, strQuery);
                    if (wildernessArea > 0)
                    {
                        strWildernessAreaPct = Convert.ToString(Math.Round(wildernessArea / aoiArea * 100));
                    }
                    else if (wildernessArea == 0)
                    {
                        strWildernessAreaPct = "0";
                    }
                }

                //Public non-wilderness area percent
                string strPublicNonWildAreaPct = "Not Found";
                if (await GeodatabaseTools.FeatureClassExistsAsync(uriAnalysis, strTmpWilderness))
                {
                    string strQuery = $@"{Constants.FIELD_SUITABLE} = 1 And {Constants.FIELD_AGBUR} <> 'AIR'";
                    double publicNonWildArea = await GeodatabaseTools.CalculateTotalPolygonAreaAsync(uriAnalysis, strTmpWilderness, strQuery);
                    if (publicNonWildArea > 0)
                    {
                        strPublicNonWildAreaPct = Convert.ToString(Math.Round(publicNonWildArea / aoiArea * 100));
                    }
                    else if (publicNonWildArea == 0)
                    {
                        strPublicNonWildAreaPct = "0";
                    }
                }

                //American indian reservation area percent
                string strAirPct = "Not Found";
                if (await GeodatabaseTools.FeatureClassExistsAsync(uriAnalysis, strTmpWilderness))
                {
                    string strQuery = $@"{Constants.FIELD_AGBUR} = 'AIR'";
                    double airArea = await GeodatabaseTools.CalculateTotalPolygonAreaAsync(uriAnalysis, strTmpWilderness, strQuery);
                    if (airArea > 0)
                    {
                        strAirPct = Convert.ToString(Math.Round(airArea / aoiArea * 100));
                    }
                    else if (airArea == 0)
                    {
                        strAirPct = "0";
                    }

                    // Delete temp file
                    success = await GeoprocessingTools.DeleteDatasetAsync(strOutputFeature);
                }

                // Site density Total # of sites / buffered area (sq miles)
                string strAllSiteDensity = "Not Found";
                string strAutoSiteDensity = strAllSiteDensity;
                string strScosSiteDensity = strAllSiteDensity;
                long lngAutoSitesCount = 0;
                long lngScosSitesCount = 0;
                bool bIsNumber = false;
                long lngCount = 0;

                string[] arrAutoSiteCounts = new string[] { strSnotelAll, strSnoliteAll, strCoopAll };
                for (int i = 0; i < arrAutoSiteCounts.Length; i++)
                {
                    bIsNumber = long.TryParse(arrAutoSiteCounts[i], out lngCount);
                    if (bIsNumber)
                    {
                        lngAutoSitesCount = lngAutoSitesCount + lngCount;
                    }
                }
                bIsNumber = long.TryParse(strScosAll, out lngCount);
                if (bIsNumber)
                {
                    lngScosSitesCount = lngScosSitesCount + lngCount;
                }
                double dblAutoSitesAreaSqMi = -1;
                string strDistance = (string)Module1.Current.BagisSettings.SnotelBufferDistance;
                string strUnits = (string)Module1.Current.BagisSettings.SnotelBufferUnits;
                string strDefaultSitesBuffer = $@"{strDistance} {strUnits}";
                if (lngAutoSitesCount > 0)
                {
                    if (string.IsNullOrEmpty(strAutoSitesBuffer))
                    {
                        strAutoSitesBuffer = strDefaultSitesBuffer;
                    }
                    double dblAutoSitesArea = await CalculateBufferedSiteAreaSqM(oAoi, strAutoSitesBuffer);
                    if (dblAutoSitesArea > 0)
                    {
                        dblAutoSitesAreaSqMi = AreaUnit.SquareMeters.ConvertTo(dblAutoSitesArea, AreaUnit.SquareMiles);
                        double dblResult = lngAutoSitesCount / dblAutoSitesAreaSqMi * 100;  // Units of measure are # site / 100 square miles
                        strAutoSiteDensity = String.Format("{0:0.##}", dblResult);
                    }
                    else
                    {
                        strAutoSiteDensity = "0";
                    }
                }
                else
                {
                    strAutoSiteDensity = "0";
                }
                double dblScosSitesAreaSqMi = -1;
                if (lngScosSitesCount > 0)
                {
                    if (string.IsNullOrEmpty(strScosSitesBuffer))
                    {
                        strScosSitesBuffer = strDefaultSitesBuffer;
                    }
                    if (strAutoSitesBuffer.Equals(strScosSitesBuffer))
                    {
                        if (dblAutoSitesAreaSqMi > 0)
                        {
                            dblScosSitesAreaSqMi = dblAutoSitesAreaSqMi;
                            double dblResult = lngScosSitesCount / dblScosSitesAreaSqMi * 100;  // Units of measure are # site / 100 square miles
                            strScosSiteDensity = String.Format("{0:0.##}", dblResult);
                        }
                    }
                    else
                    {
                        double dblScosSitesArea = await CalculateBufferedSiteAreaSqM(oAoi, strScosSitesBuffer);
                        if (dblScosSitesArea > 0)
                        {
                            dblScosSitesAreaSqMi = AreaUnit.SquareMeters.ConvertTo(dblScosSitesArea, AreaUnit.SquareMiles);
                            double dblResult = lngScosSitesCount / dblScosSitesAreaSqMi * 100;  // Units of measure are # site / 100 square miles
                            strScosSiteDensity = String.Format("{0:0.##}", dblResult);
                        }
                        else
                        {
                            strScosSiteDensity = "0";
                        }
                    }
                }
                else
                {
                    strScosSiteDensity = "0";
                }
                if (lngAutoSitesCount > 0 && lngScosSitesCount == 0)
                {
                    strAllSiteDensity = strAutoSiteDensity;
                }
                else if (lngAutoSitesCount == 0 && lngScosSitesCount > 0)
                {
                    strAllSiteDensity = strScosSiteDensity;
                }
                else if (lngAutoSitesCount == 0 && lngScosSitesCount == 0)
                {
                    strAllSiteDensity = "0";
                }
                else
                {
                    long lngAllSitesCount = lngAutoSitesCount + lngScosSitesCount;
                    if (strAutoSitesBuffer.Equals(strScosSitesBuffer))
                    {
                        double dblResult = lngAllSitesCount / dblScosSitesAreaSqMi * 100;  // Units of measure are # site / 100 square miles
                        strAllSiteDensity = String.Format("{0:0.##}", dblResult);
                    }
                    else if (dblAutoSitesAreaSqMi > dblScosSitesAreaSqMi)
                    {
                        {
                            double dblResult = lngAllSitesCount / dblAutoSitesAreaSqMi * 100;  // Units of measure are # site / 100 square miles
                            strAllSiteDensity = String.Format("{0:0.##}", dblResult);
                        }
                    }
                    else
                    {
                        double dblResult = lngAllSitesCount / dblScosSitesAreaSqMi * 100;  // Units of measure are # site / 100 square miles
                        strAllSiteDensity = String.Format("{0:0.##}", dblResult);
                    }
                }

                // slope zones definition
                string strSlopeZones = "Not Found";
                string strSlopeAreaPct = "Not Found";
                string strTmpSlope = "tmpSlope";
                lstInterval.Clear();
                dictZoneNames.Clear();
                lstInterval = await GeodatabaseTools.ReadReclassRasterAttribute(uriAnalysis, Constants.FILE_SLOPE_ZONE);
                StringBuilder sbSlope = new StringBuilder();
                foreach (var item in lstInterval)
                {
                    if (!dictZoneNames.ContainsKey((item.Name)))
                    {
                        dictZoneNames.Add(item.Name, "");
                        sbSlope.Append(item.Name);
                        sbSlope.Append(",");
                    }
                }
                if (sbSlope.Length > 0)
                {
                    string strTrimmed = sbSlope.ToString().TrimEnd(',');
                    strSlopeZones = $@"""{strTrimmed}""";
                }
                if (lstInterval.Count > 0)
                {
                    string slopeZonesPath = $@"{uriAnalysis.LocalPath}\{Constants.FILE_SLOPE_ZONE}";
                    strOutputFeature = $@"{uriAnalysis.LocalPath}\{strTmpSlope}";
                    IDictionary<string, string> dictZonalPercentages =
                        await CalculateZonalAreaPercentages(oAoi.FilePath, slopeZonesPath, Constants.FIELD_NAME, strFilledDem,
                        strOutputFeature, strAnalysisMask, dictZoneNames, strLogFile);
                    StringBuilder sb2 = new StringBuilder();
                    foreach (var strKey in dictZoneNames.Keys)
                    {
                        if (dictZonalPercentages.ContainsKey(strKey))
                        {
                            sb2.Append(dictZonalPercentages[strKey]);
                        }
                        else
                        {
                            sb2.Append("0");
                        }
                        sb2.Append(",");
                    }
                    string strTrimmed = sb2.ToString().TrimEnd(',');
                    strSlopeAreaPct = $@"""{strTrimmed}""";
                    // Delete temp file
                    if (await GeodatabaseTools.FeatureClassExistsAsync(uriAnalysis, strTmpSlope))
                    {
                        success = await GeoprocessingTools.DeleteDatasetAsync(strOutputFeature);
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
                lstElements.Add(strElevZonesDef);
                lstElements.Add(strElevAreaPct);
                lstElements.Add(strAutoSiteCountElevZone);
                lstElements.Add(strScosSiteCountElevZone);
                lstElements.Add(strCriticalPrecipZonesDef);
                lstElements.Add(strCriticalPrecipPctArea);
                lstElements.Add(strWildernessAreaPct);
                lstElements.Add(strPublicNonWildAreaPct);
                lstElements.Add(strAirPct);
                lstElements.Add(strAllSiteDensity);
                lstElements.Add(strAutoSiteDensity);
                lstElements.Add(strScosSiteDensity);
                lstElements.Add(strPctAreaOutsideUsa);
                lstElements.Add(strSlopeZones);
                lstElements.Add(strSlopeAreaPct);

            }
            return lstElements;
        }

        public async static Task<BA_ReturnCode> DeleteDuplicatesAsync(string strFeatureClass, string strWhere1, string strJoinField,
            string strNotWhere1)
        {
            string Delete_YN = "DELETE_YN";
            string strGeodatabase = Path.GetDirectoryName(strFeatureClass);
            string strData = Path.GetFileName(strFeatureClass);
            FeatureLayer lyrDelete = null;
            BA_ReturnCode success = await GeoprocessingTools.AddFieldAsync(strFeatureClass, Delete_YN, "TEXT", null);
            if (success == BA_ReturnCode.Success)
            {
                await QueuedTask.Run(() =>
                {
                    using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strGeodatabase))))
                    {
                        using (FeatureClass fClass = geodatabase.OpenDataset<FeatureClass>(strData))
                        {
                            QueryFilter queryFilter = new QueryFilter
                            {
                                WhereClause = strWhere1,
                            };
                            // create an edit operation and name.
                            var editOp = new EditOperation
                            {
                                Name = "modify delete field",
                                // set the ExecuteMOde
                                ExecuteMode = ExecuteModeType.Sequential
                            };
                            using (RowCursor rowCursor = fClass.Search(queryFilter, false))
                            {
                                while (rowCursor.MoveNext())
                                {
                                    using (Row row = rowCursor.Current)
                                    {
                                        string strKey = Convert.ToString(row[strJoinField]);
                                        string strWhere2 = $@"{strJoinField} = '{strKey}' AND {strNotWhere1}";
                                        QueryFilter queryFilter2 = new QueryFilter
                                        {
                                            WhereClause = strWhere2,
                                        };
                                        using (RowCursor rowCursor2 = fClass.Search(queryFilter2, false))
                                        {
                                            if (rowCursor2.MoveNext())
                                            {
                                                // We found a duplicate with the same key
                                                editOp.Modify(row, Delete_YN, "Y");
                                            }
                                            else
                                            {
                                                editOp.Modify(row, Delete_YN, "N");
                                            }
                                        }
                                    }
                                }

                            }
                            if (!editOp.IsEmpty)
                            {
                                bool result = editOp.Execute();
                                if (!result)
                                {
                                    Module1.Current.ModuleLogManager.LogError(nameof(DeleteDuplicatesAsync), "Failed to mark duplicates in feature class");
                                }
                                else
                                {
                                    Project.Current.SaveEditsAsync();
                                }
                            }

                        }
                    }
                });
                var oMap = await MapTools.SetDefaultMapNameAsync(Constants.MAPS_DEFAULT_MAP_NAME);
                await QueuedTask.Run( async () =>
                {
                    var deleteParams = new FeatureLayerCreationParams(new Uri(strFeatureClass))
                    {
                        Name = "Selection Layer",
                        IsVisible = false,
                        MapMemberIndex = 0,
                        MapMemberPosition = 0,
                    };
                    lyrDelete = LayerFactory.Instance.CreateLayer<FeatureLayer>(deleteParams, oMap);
                    lyrDelete.SetDefinitionQuery($@"{Delete_YN} = 'Y'");

                    var parameters = Geoprocessing.MakeValueArray(lyrDelete.Name);
                    var gpResult = await Geoprocessing.ExecuteToolAsync("DeleteFeatures_management", parameters, null,
                       CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                    if (gpResult.IsFailed)
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(DeleteDuplicatesAsync),
                            "Failed to execute DeleteFeatures tool");
                        success = BA_ReturnCode.UnknownError;
                    }
                    else
                    {
                        oMap.RemoveLayer(lyrDelete);
                        success = BA_ReturnCode.Success;
                    }                    
                });

            }
            if (success == BA_ReturnCode.Success)
            {
                success = await GeoprocessingTools.DeleteFieldAsync(strFeatureClass, Delete_YN);
                success = await GeoprocessingTools.DeleteFieldAsync(strFeatureClass, "poly_SourceOID");
            }
            return success;
        }

        public async static Task<BA_ReturnCode> DeleteIrwinDuplicatesAsync(string strAoiPath)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            string strNifc = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Fire, true) + Constants.FILE_NIFC_FIRE;
            // Create selection layer
            FeatureLayer lyrIrwin = null;
            var oMap = await MapTools.SetDefaultMapNameAsync(Constants.MAPS_DEFAULT_MAP_NAME);
            await QueuedTask.Run(() =>
            {
                var historyParams = new FeatureLayerCreationParams(new Uri(strNifc))
                {
                    Name = "Irwin Layer",
                    IsVisible = false,
                    MapMemberIndex = 0,
                    MapMemberPosition = 0,
                };
                lyrIrwin = LayerFactory.Instance.CreateLayer<FeatureLayer>(historyParams, oMap);
                lyrIrwin.SetDefinitionQuery($@"{Constants.FIELD_IRWIN_ID} IS NOT NULL");
            });

            string tmpIrwinFeatures = "tmpIrwin";
            var parameters = Geoprocessing.MakeValueArray(lyrIrwin.Name, $@"{GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Fire, true)}{tmpIrwinFeatures}");
            var gpResult = await Geoprocessing.ExecuteToolAsync("ExportFeatures_conversion", parameters, null,
               CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            if (gpResult.IsFailed)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(DeleteIrwinDuplicatesAsync),
                    $@"Export Features could not create{tmpIrwinFeatures}");
                success = BA_ReturnCode.UnknownError;
            }
            else
            {
                success = BA_ReturnCode.Success;
            }
            if (success == BA_ReturnCode.Success)
            {
                parameters = Geoprocessing.MakeValueArray(lyrIrwin.Name);
                gpResult = await Geoprocessing.ExecuteToolAsync("DeleteFeatures_management", parameters, null,
                   CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (gpResult.IsFailed)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(DeleteIrwinDuplicatesAsync),
                        "Failed to execute DeleteFeatures tool on Irwin Layer");
                    success = BA_ReturnCode.UnknownError;
                }
            }
            string tmpIrwinDissolve = "tmpIrwinDissolve";
            if (success == BA_ReturnCode.Success)
            {
                parameters = Geoprocessing.MakeValueArray($@"{GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Fire, true)}{tmpIrwinFeatures}",
                    $@"{GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Fire, true)}{tmpIrwinDissolve}",
                    $@"{Constants.FIELD_IRWIN_ID};{Constants.FIELD_INCIDENT};{Constants.FIELD_YEAR}");
                gpResult = await Geoprocessing.ExecuteToolAsync("Dissolve_management", parameters, null,
                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (gpResult.IsFailed)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(DeleteIrwinDuplicatesAsync), "Unable to generate dissolved tmpIrwin layer");
                    success = BA_ReturnCode.UnknownError;
                }
                else
                {
                    success = BA_ReturnCode.Success;
                }
            }
            if (success == BA_ReturnCode.Success)
            {
                parameters = Geoprocessing.MakeValueArray($@"{GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Fire, true)}{tmpIrwinDissolve}", 
                    strNifc,"TEST");
                gpResult = await Geoprocessing.ExecuteToolAsync("Append_management", parameters, null,
                            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (gpResult.IsFailed)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(DeleteIrwinDuplicatesAsync), "Unable to append dissolved tmpIrwin layer to nifcfire");
                    success = BA_ReturnCode.UnknownError;
                }
                else
                {
                    success = BA_ReturnCode.Success;
                }
            }

            // Remove temporary layer
            await QueuedTask.Run(() =>
            {
                oMap.RemoveLayer(lyrIrwin);
            });

            // Delete working files
            string[] arrTempFiles = { tmpIrwinFeatures, tmpIrwinDissolve};
            for (int i = 0; i < arrTempFiles.Length; i++)
            {
                if (await GeodatabaseTools.FeatureClassExistsAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Fire)), arrTempFiles[i]))
                {
                    await GeoprocessingTools.DeleteDatasetAsync($@"{GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Fire, true)}{arrTempFiles[i]}");
                }
            }
            return success;
        }

        public async static Task<BA_ReturnCode> DissolveIncidentDuplicatesAsync(string strAoiPath)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            string strNifc = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Fire, true) + Constants.FILE_NIFC_FIRE;
            // Create selection layer
            FeatureLayer lyrIrwin = null;
            var oMap = await MapTools.SetDefaultMapNameAsync(Constants.MAPS_DEFAULT_MAP_NAME);
            await QueuedTask.Run(() =>
            {
                var historyParams = new FeatureLayerCreationParams(new Uri(strNifc))
                {
                    Name = "Irwin Layer",
                    IsVisible = false,
                    MapMemberIndex = 0,
                    MapMemberPosition = 0,
                };
                lyrIrwin = LayerFactory.Instance.CreateLayer<FeatureLayer>(historyParams, oMap);
                lyrIrwin.SetDefinitionQuery($@"{Constants.FIELD_IRWIN_ID} IS NULL");
            });

            string tmpIrwinFeatures = "tmpIrwinNull";
            var parameters = Geoprocessing.MakeValueArray(lyrIrwin.Name, $@"{GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Fire, true)}{tmpIrwinFeatures}");
            var gpResult = await Geoprocessing.ExecuteToolAsync("ExportFeatures_conversion", parameters, null,
               CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            if (gpResult.IsFailed)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(DissolveIncidentDuplicatesAsync),
                    $@"Export Features could not create{tmpIrwinFeatures}");
                success = BA_ReturnCode.UnknownError;
            }
            else
            {
                success = BA_ReturnCode.Success;
            }
            if (success == BA_ReturnCode.Success)
            {
                parameters = Geoprocessing.MakeValueArray(lyrIrwin.Name);
                gpResult = await Geoprocessing.ExecuteToolAsync("DeleteFeatures_management", parameters, null,
                   CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (gpResult.IsFailed)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(DissolveIncidentDuplicatesAsync),
                        "Failed to execute DeleteFeatures tool on Irwin Layer");
                    success = BA_ReturnCode.UnknownError;
                }
            }
            string tmpIrwinDissolve = "tmpIrwinDissolve";
            if (success == BA_ReturnCode.Success)
            {
                parameters = Geoprocessing.MakeValueArray($@"{GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Fire, true)}{tmpIrwinFeatures}",
                    $@"{GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Fire, true)}{tmpIrwinDissolve}",
                    $@"{Constants.FIELD_IRWIN_ID};{Constants.FIELD_INCIDENT};{Constants.FIELD_YEAR}");
                gpResult = await Geoprocessing.ExecuteToolAsync("Dissolve_management", parameters, null,
                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (gpResult.IsFailed)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(DissolveIncidentDuplicatesAsync), "Unable to generate dissolved tmpIrwinNull layer");
                    success = BA_ReturnCode.UnknownError;
                }
                else
                {
                    success = BA_ReturnCode.Success;
                }
            }
            if (success == BA_ReturnCode.Success)
            {
                parameters = Geoprocessing.MakeValueArray($@"{GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Fire, true)}{tmpIrwinDissolve}",
                    strNifc, "TEST");
                gpResult = await Geoprocessing.ExecuteToolAsync("Append_management", parameters, null,
                            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (gpResult.IsFailed)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(DissolveIncidentDuplicatesAsync), "Unable to append dissolved tmpIrwinNull layer to nifcfire");
                    success = BA_ReturnCode.UnknownError;
                }
                else
                {
                    success = BA_ReturnCode.Success;
                }
            }

            // Remove temporary layer
            await QueuedTask.Run(() =>
            {
                oMap.RemoveLayer(lyrIrwin);
            });

            // Delete working files
            string[] arrTempFiles = { tmpIrwinFeatures, tmpIrwinDissolve };
            for (int i = 0; i < arrTempFiles.Length; i++)
            {
                if (await GeodatabaseTools.FeatureClassExistsAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Fire)), arrTempFiles[i]))
                {
                    await GeoprocessingTools.DeleteDatasetAsync($@"{GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Fire, true)}{arrTempFiles[i]}");
                }
            }
            return success;
        }

        public static async Task<BA_ReturnCode> DeleteDuplicatesByLocationAsync(string strAoiPath)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            Uri uriFire = new Uri(GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Fire));
            string nifcPath = $@"{uriFire.LocalPath}\{Constants.FILE_NIFC_FIRE}";
            string tmpOverlap = "tmpOverlap";
            string tmpCentroid = "tmpCentroid";
            var environments = Geoprocessing.MakeEnvironmentArray(workspace: strAoiPath);
            IGPResult gpResult = await QueuedTask.Run(() =>
            {
                var parameters = Geoprocessing.MakeValueArray(nifcPath, $@"{uriFire.LocalPath}\{tmpOverlap}",
                    $@"{uriFire.LocalPath}\{tmpCentroid}", Constants.FIELD_YEAR);
                return Geoprocessing.ExecuteToolAsync("FindOverlaps", parameters, environments,
                            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            });
            if (gpResult.IsFailed)
            {
                return success;
            }
            else
            {
                if (await GeodatabaseTools.FeatureClassExistsAsync(uriFire, tmpCentroid))
                {
                    success = await GeoprocessingTools.DeleteDatasetAsync($@"{uriFire.LocalPath}\{tmpCentroid}");
                }
            }
            // Add nifc feature layer to map
            var oMap = await MapTools.SetDefaultMapNameAsync(Constants.MAPS_DEFAULT_MAP_NAME);
            FeatureLayer lyrNifc = null;
            FeatureLayer lyrOverlap = null;
            await QueuedTask.Run(() =>
            {
                var historyParams = new FeatureLayerCreationParams(new Uri($@"{uriFire.LocalPath}\{tmpOverlap}"))
                {
                    Name = "Overlap Layer",
                    IsVisible = true,
                    MapMemberIndex = 0,
                    MapMemberPosition = 0,
                };
                lyrOverlap = LayerFactory.Instance.CreateLayer<FeatureLayer>(historyParams, oMap);
            });

            string fieldShapeArea = "Shape_Area";
            IList<string> lstObjectIds = await GeodatabaseTools.QueryTableForDistinctValuesAsync(uriFire, tmpOverlap,
                Constants.FIELD_OBJECT_ID, new QueryFilter());
            IList<string> lstAreas = await GeodatabaseTools.QueryTableForDistinctValuesAsync(uriFire, tmpOverlap,
                fieldShapeArea, new QueryFilter());
            if (lstObjectIds.Count != lstAreas.Count)
            {
                return BA_ReturnCode.UnknownError;
            }

            for (int i = 0; i < lstObjectIds.Count; i++)
            {
                string strOid = lstObjectIds[i];
                string strWhere = $@"{Constants.FIELD_OBJECT_ID} = {strOid}";
                QueryFilter qf = new QueryFilter();
                qf.WhereClause = strWhere;
                await QueuedTask.Run(() =>
                {
                    lyrOverlap.Select(qf);

                    var historyParams = new FeatureLayerCreationParams(new Uri(nifcPath))
                    {
                        Name = "Nifc Layer",
                        IsVisible = true,
                        MapMemberIndex = 0,
                        MapMemberPosition = 0,
                    };
                    lyrNifc = LayerFactory.Instance.CreateLayer<FeatureLayer>(historyParams, oMap);
                });

                gpResult = await QueuedTask.Run(() =>
                {
                    var parameters = Geoprocessing.MakeValueArray(lyrNifc, "CONTAINS",
                        lyrOverlap);
                    return Geoprocessing.ExecuteToolAsync("SelectLayerByLocation", parameters, environments,
                                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                });

                List<long> lstOIDs = new List<long>();
                await QueuedTask.Run(() =>
                {
                    var gsSelection = lyrNifc.GetSelection();
                    IReadOnlyList<long> selectedOIDs = gsSelection.GetObjectIDs();
                    foreach (var oid in selectedOIDs)
                    {
                        lstOIDs.Add(oid);
                    }
                });


                List<long> lstDeleteOIDs = new List<long>();
                long lngDeleteCount = 0;
                double dblMaxArea = Convert.ToDouble(lstAreas[i]) * .8;
                if (lstOIDs.Count > 0)
                {
                    gpResult = await QueuedTask.Run(() =>
                    {
                        string strWhere = $@"{Constants.FIELD_IRWIN_ID} IS NULL And {fieldShapeArea} > {dblMaxArea}";
                        var parameters = Geoprocessing.MakeValueArray(lyrNifc, "SUBSET_SELECTION", strWhere);
                        return Geoprocessing.ExecuteToolAsync("SelectLayerByAttribute", parameters, environments,
                        CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                    });
                    await QueuedTask.Run(() =>
                    {
                        var gsSelection = lyrNifc.GetSelection();
                        IReadOnlyList<long> selectedOIDs = gsSelection.GetObjectIDs();
                        foreach (var oid in selectedOIDs)
                        {
                            lngDeleteCount++;
                        }
                    });
                }

                if (lngDeleteCount > 0)
                {
                    gpResult = await QueuedTask.Run(() =>
                    {
                        var parameters = Geoprocessing.MakeValueArray(lyrNifc);
                        return Geoprocessing.ExecuteToolAsync("DeleteFeatures_management", parameters, environments,
                                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                    });
                    if (gpResult.IsFailed)
                    {
                        return BA_ReturnCode.UnknownError;
                    }
                }

                await QueuedTask.Run(() =>
                {
                    oMap.RemoveLayer(lyrNifc);
                });
            }
            // Remove temporary layer
            await QueuedTask.Run(() =>
            {
                oMap.RemoveLayer(lyrOverlap);
            });
            return success;
        }
        public static async Task<int> ClipMtbsLayersAsync(string strAoiPath, string strClipFile, List<string> lstImageServiceUri, List<string> lstRasterFileName, int intLastMtbsYear, bool bReclipMtbs)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;

            // Check to make sure the buffer file only has one feature; No dangles
            long featureCount = 0;
            string[] arrLayersToDelete = new string[1];
            // Assumes clip file is in aoi.gdb
            string strClipGdb = GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Aoi, false);
            string strClipEnvelope = "";
            await QueuedTask.Run(() =>
            {
                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strClipGdb))))
                using (Table table = geodatabase.OpenDataset<Table>(strClipFile))
                {
                    featureCount = table.GetCount();
                }
                Module1.Current.ModuleLogManager.LogDebug(nameof(ClipMtbsLayersAsync),
                    "Number of features in clip file: " + featureCount);

                // If > 1 feature, buffer the clip file
                if (featureCount > 1)
                {
                    string strTempBuffer2 = "tempBuffer2";
                    var parameters = Geoprocessing.MakeValueArray(strClipGdb + "\\" + strClipFile, strClipGdb + "\\" + strTempBuffer2, "0.5 Meters", "", "", "ALL");
                    var gpResult = Geoprocessing.ExecuteToolAsync("Buffer_analysis", parameters, null,
                                     CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                    if (gpResult.Result.IsFailed)
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(ClipMtbsLayersAsync),
                            "Unable to buffer " + strClipFile + ". Error code: " + gpResult.Result.ErrorCode);
                        return;
                    }
                    strClipFile = strTempBuffer2;
                    Module1.Current.ModuleLogManager.LogDebug(nameof(ClipMtbsLayersAsync),
                        "Ran buffer tool again because clip file has > 2 features");
                }

                // Query the extent for the clip
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
            });
            
            var environments = Geoprocessing.MakeEnvironmentArray(workspace: strAoiPath, snapRaster: Aoi.SnapRasterPath(strAoiPath),
                extent: strClipEnvelope);
            string strTemplateDataset = strClipGdb + "\\" + strClipFile;
            int clippedLayersCount = 0;
            bool bNoDataRasterExists = false;
            string tempNoData = "tmpNoData";
            for (int i = 0; i < lstImageServiceUri.Count; i++)
            {
                string strOutputRaster = $@"{GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Fire, true)}{lstRasterFileName[i]}";
                string strNoDataRaster = $@"{GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Fire, true)}{lstRasterFileName[i]}_{Constants.VALUE_NO_DATA.ToUpper()}";
                bool bClipThisLayer = true;
                // Check for both regular and NODATA rasters
                bool bExists = await GeodatabaseTools.RasterDatasetExistsAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Fire)), lstRasterFileName[i]);
                if (bExists)
                {
                    bClipThisLayer = false;
                }
                else
                {
                    bExists = await GeodatabaseTools.RasterDatasetExistsAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Fire)), $@"{lstRasterFileName[i]}_{Constants.VALUE_NO_DATA.ToUpper()}");
                    if (bExists)
                    {
                        bClipThisLayer = false;
                    }
                }
                if (bReclipMtbs || bClipThisLayer)
                {
                    clippedLayersCount++;    
                    var parameters = Geoprocessing.MakeValueArray(lstImageServiceUri[i], strClipEnvelope, strOutputRaster, strTemplateDataset,
                            "", "ClippingGeometry");
                    // Always set the extent if clipping from an image service
                    var gpResult = await Geoprocessing.ExecuteToolAsync("Clip_management", parameters, environments,
                                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                    if (gpResult.IsFailed)
                    {
                        // A common error for Alaska MTBS is that the aoi is outside of the imageservice extent. The imageservice
                        // extent is bounded by the envelops of the fire perimeter so it is variable. We need to handle this.
                        string searchString = "clip feature is outside the raster extent";
                        bool bOutsideExtent = false;
                        foreach (var objMessage in gpResult.Messages)
                        {
                            IGPMessage msg = (IGPMessage)objMessage;
                            if (msg.Text.ToLower().Contains(searchString))
                            {
                                Module1.Current.ModuleLogManager.LogInfo(nameof(ClipMtbsLayersAsync), "AOI polygon is not within " + lstImageServiceUri[i]);
                                bOutsideExtent = true;
                                break;
                            }
                        }
                        if (bOutsideExtent && !bNoDataRasterExists)
                        {
                            string sourceRaster = $@"{GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Aoi)}\{Constants.FILE_AOI_RASTER}";
                            parameters = Geoprocessing.MakeValueArray(sourceRaster, sourceRaster, $@"{GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Fire)}\{tempNoData}");
                            gpResult = await Geoprocessing.ExecuteToolAsync("SetNull_sa", parameters, environments,
                                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                            if (gpResult.IsFailed)
                            {
                                Module1.Current.ModuleLogManager.LogError(nameof(ClipMtbsLayersAsync), "Unable to create NODATA raster dataset");
                            }
                            else
                            {
                                bNoDataRasterExists = true;
                            }
                        }
                        if (bOutsideExtent && bNoDataRasterExists)
                        {
                            parameters = Geoprocessing.MakeValueArray($@"{GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Fire)}\{tempNoData}",
                                strNoDataRaster);
                            gpResult = await Geoprocessing.ExecuteToolAsync("CopyRaster_management", parameters, null,
                                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                            if (gpResult.IsFailed)
                            {
                                Module1.Current.ModuleLogManager.LogError(nameof(ClipMtbsLayersAsync), $@"Unable to create NODATA raster dataset {strNoDataRaster}");
                            }
                        }
                        else
                        {
                            Module1.Current.ModuleLogManager.LogError(nameof(ClipMtbsLayersAsync), "Unable to clip " + lstImageServiceUri[i] + " to " + strOutputRaster);
                        }
                    }
                    else
                    {
                        parameters = Geoprocessing.MakeValueArray(strOutputRaster, "ALLNODATA");
                        gpResult = await Geoprocessing.ExecuteToolAsync("GetRasterProperties_management", parameters, null,
                            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                        if (gpResult.IsFailed)
                        {
                            Module1.Current.ModuleLogManager.LogError(nameof(ClipMtbsLayersAsync), "Unable to get raster properties!");
                        }
                        else
                        {
                            int intReturn = Convert.ToInt16(gpResult.ReturnValue);
                            if (intReturn > 0)  // The raster is ALLNODATA                                
                            {
                                // Delete the original _NODATA dataset if it exists; Rename tool will not overwrite
                                bExists = await GeodatabaseTools.RasterDatasetExistsAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Fire)), $@"{lstRasterFileName[i]}_{Constants.VALUE_NO_DATA.ToUpper()}");
                                if (bExists)
                                {
                                    success = await GeoprocessingTools.DeleteDatasetAsync(strNoDataRaster);
                                }
                                if (!bExists || success == BA_ReturnCode.Success)
                                {
                                    parameters = Geoprocessing.MakeValueArray(strOutputRaster, strNoDataRaster);
                                    gpResult = await Geoprocessing.ExecuteToolAsync("Rename_management", parameters, null,
                                        CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                                    if (gpResult.IsFailed)
                                    {
                                        Module1.Current.ModuleLogManager.LogError(nameof(ClipMtbsLayersAsync), "Failed to rename ALLNODATA raster!");
                                    }
                                }
                            }
                            else
                            {
                                // Reclassify 5 and 6 values to 1 to make future functions easier
                                //Sample reclass string: 1 1; 2 2; 3 3; 4 4; 5 1; 6 1
                                JArray arrMtbsLegend = Module1.Current.BagisSettings.MtbsLegend;
                                StringBuilder sb = new StringBuilder();
                                foreach (dynamic item in arrMtbsLegend)
                                {
                                    string severity = Convert.ToString(item.Severity);
                                    string value = Convert.ToString(item.Value);
                                    if (Constants.MTBS_INCLUDE_SEVERITIES.Contains(severity))
                                    {
                                        sb.Append($@"{Convert.ToString(value)} {Convert.ToString(value)}");
                                        sb.Append(";");
                                    }
                                    else
                                    {
                                        sb.Append($@"{Convert.ToString(value)} 1");
                                        sb.Append(";");
                                    }
                                }
                                string strReclass = sb.ToString().TrimEnd(';');
                                string strOutputLayer = $@"{strOutputRaster}_RECL";
                                parameters = Geoprocessing.MakeValueArray(strOutputRaster, "VALUE", strReclass, strOutputLayer);
                                // Something in the environments variable above stops the reclassify from working; Passing null instead for environment variables
                                gpResult = await Geoprocessing.ExecuteToolAsync("Reclassify_sa", parameters, null,
                                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                                if (gpResult.IsFailed)
                                {
                                    Module1.Current.ModuleLogManager.LogError(nameof(ClipMtbsLayersAsync), "Failed to reclass raster!");
                                }

                            }
                        }
                    }
                }
            }
            // Delete tmpNoData layer if it exists
            if (await GeodatabaseTools.RasterDatasetExistsAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Fire)), tempNoData))
            {
                success = await GeoprocessingTools.DeleteDatasetAsync($@"{GeodatabaseTools.GetGeodatabasePath(strAoiPath, GeodatabaseNames.Fire)}\{tempNoData}");
            }            
            return clippedLayersCount;
        }

        public static async Task<double> QueryPerimeterStatisticsByYearAsync(string aoiPath, int intYear, 
            double aoiAreaSqMeters, FireStatisticType fireStatType, string strLogFile)
        {
            double dblReturn = -1;
            string strGdbFire = GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Fire);
            string strTmpIntersect = "tmpIntersect";
            switch (fireStatType)
            {
                case FireStatisticType.Count:
                    await QueuedTask.Run(() =>
                    {
                        using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strGdbFire))))
                        using (FeatureClass featureClass = geodatabase.OpenDataset<FeatureClass>(Constants.FILE_NIFC_FIRE))
                        {
                            QueryFilter queryFilter = new QueryFilter();
                            queryFilter.WhereClause = $@"{Constants.FIELD_YEAR} = {intYear}";
                            long count = featureClass.GetCount(queryFilter);
                            dblReturn = Convert.ToDouble(count);
                        }
                    });
                    break;
                case FireStatisticType.AreaSqMiles:
                    string strWhere = $@"{Constants.FIELD_YEAR} = {intYear}";
                    double dblAreaSqMeters = await GeodatabaseTools.CalculateTotalPolygonAreaAsync(new Uri(strGdbFire), Constants.FILE_NIFC_FIRE, strWhere);
                    if (dblAreaSqMeters > 0)
                    {
                        dblReturn  = Math.Round(AreaUnit.SquareMeters.ConvertTo(dblAreaSqMeters, AreaUnit.SquareMiles), 2);
                    }
                    else
                    {
                        dblReturn = 0;
                    }
                    break;
                case FireStatisticType.NifcBurnedAreaPct:
                    strWhere = $@"{Constants.FIELD_YEAR} = {intYear}";
                    dblAreaSqMeters = await GeodatabaseTools.CalculateTotalPolygonAreaAsync(new Uri(strGdbFire), Constants.FILE_NIFC_FIRE, strWhere);
                    if (dblAreaSqMeters > 0)
                    {
                        dblReturn = Math.Round(dblAreaSqMeters/aoiAreaSqMeters * 100, 3);
                    }
                    else
                    {
                        dblReturn = 0;
                    }
                    break;
                case FireStatisticType.BurnedForestedArea:
                    string strGdbAnalysis = GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Analysis);
                    FeatureLayer lyrCurrYear = null;
                    Map oMap = await MapTools.SetDefaultMapNameAsync(Constants.MAPS_DEFAULT_MAP_NAME);
                    long lCount = -1;
                    if (await GeodatabaseTools.FeatureClassExistsAsync(new Uri(strGdbAnalysis), Constants.FILE_FORESTED_ZONE))
                    {
                        string strNifcLayer = $@"{strGdbFire}\{Constants.FILE_NIFC_FIRE}";
                        await QueuedTask.Run(() =>
                        {
                            var historyParams = new FeatureLayerCreationParams(new Uri(strNifcLayer))
                            {
                                Name = "Intersect Layer",
                                IsVisible = false,
                                MapMemberIndex = 0,
                                MapMemberPosition = 0,
                            };
                            lyrCurrYear = LayerFactory.Instance.CreateLayer<FeatureLayer>(historyParams, oMap);
                            string strWhere = $@"{Constants.FIELD_YEAR} = {intYear}";
                            lyrCurrYear.SetDefinitionQuery(strWhere);
                            // This is a workaround because I can't find an api to get the correct count when using a definition query
                            Table table = lyrCurrYear.GetTable();
                            QueryFilter qf = new QueryFilter()
                            {
                                WhereClause = strWhere
                            };
                            lCount = table.GetCount(qf);
                        });

                        if (lCount > 0)
                        {
                            string strForestedZone = $@"{strGdbAnalysis}\{Constants.FILE_FORESTED_ZONE}";
                            // Feature layer name needs to be surrounded by single quotes
                            string[] arrInputLayers = {strForestedZone, $@"'{lyrCurrYear.Name}'"};
                            BA_ReturnCode success = await GeoprocessingTools.IntersectUnrankedAsync(aoiPath, arrInputLayers,
                                $@"{strGdbFire}\{strTmpIntersect}", "ONLY_FID");
                            if (success != BA_ReturnCode.Success)
                            {
                                string strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "An error occurred while running the Intersect tool. Burned forested area cannot be calculated!" + "\r\n";
                                File.AppendAllText(strLogFile, strLogEntry);       // append
                            }
                            else
                            {
                                dblAreaSqMeters = await GeodatabaseTools.CalculateTotalPolygonAreaAsync(new Uri(strGdbFire), strTmpIntersect, "");
                                if (dblAreaSqMeters > 0)
                                {
                                    dblReturn = Math.Round(AreaUnit.SquareMeters.ConvertTo(dblAreaSqMeters, AreaUnit.SquareMiles), 2);
                                }
                                else
                                {
                                    dblReturn = 0;
                                }

                            }
                        }
                        else
                        {
                            dblReturn = 0;
                        }

                        // Remove temporary layer
                        await QueuedTask.Run(() =>
                        {
                            oMap.RemoveLayer(lyrCurrYear);
                        });
                    }
                    else
                    {
                        string strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "forestedzone is missing. Burned forested area cannot be calculated!" + "\r\n";
                        File.AppendAllText(strLogFile, strLogEntry);       // append
                    }
                    break;
                case FireStatisticType.BurnedForestedAreaPct:
                    double burnedForestAreaSqMeters = -1;                    
                    if (await GeodatabaseTools.FeatureClassExistsAsync(new Uri(strGdbFire), strTmpIntersect))
                    {
                        burnedForestAreaSqMeters = await GeodatabaseTools.CalculateTotalPolygonAreaAsync(new Uri(strGdbFire), strTmpIntersect, "");
                    }
                    else
                    {
                        double burnedForestAreaSqMiles = await QueryPerimeterStatisticsByYearAsync(aoiPath, intYear, aoiAreaSqMeters, FireStatisticType.BurnedForestedArea, strLogFile);
                        burnedForestAreaSqMeters = AreaUnit.SquareMiles.ConvertTo(burnedForestAreaSqMiles, AreaUnit.SquareMeters);
                    }
                    if (burnedForestAreaSqMeters > 0)
                    {
                        double forestedAreaSqMeters =
                            await GeodatabaseTools.CalculateTotalPolygonAreaAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Analysis)), Constants.FILE_FORESTED_ZONE, "");
                        dblReturn = Math.Round(burnedForestAreaSqMeters / forestedAreaSqMeters * 100, 3);
                    }
                    if (await GeodatabaseTools.FeatureClassExistsAsync(new Uri(strGdbFire), strTmpIntersect)) 
                    {
                        // Clean up temporary file
                        BA_ReturnCode success = await GeoprocessingTools.DeleteDatasetAsync($@"{strGdbFire}\{strTmpIntersect}");
                    }
                    break;
            }

            return dblReturn;
        }

        public static async Task<double> QueryPerimeterStatisticsByIncrementAsync(string aoiPath, Interval oInterval,
            double aoiAreaSqMeters, FireStatisticType fireStatType, string strLogFile)
        {
            double dblReturn = -1;
            string strGdbFire = GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Fire);
            string strDissolveFc = $@"tmpDiss{Convert.ToString(oInterval.Value)}";
            string strTmpDissolve = $@"{strGdbFire}\{strDissolveFc}";
            string strTmpIntersect = $@"tmpIntersect_{Convert.ToString(oInterval.Value)}";
            switch (fireStatType)
            {
                case FireStatisticType.Count:
                    await QueuedTask.Run(() =>
                    {
                        using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strGdbFire))))
                        using (FeatureClass featureClass = geodatabase.OpenDataset<FeatureClass>(Constants.FILE_NIFC_FIRE))
                        {
                            QueryFilter queryFilter = new QueryFilter();
                            queryFilter.WhereClause = $@"{Constants.FIELD_YEAR} > {oInterval.LowerBound - 1} And {Constants.FIELD_YEAR} < {oInterval.UpperBound} + 1";
                            long count = featureClass.GetCount(queryFilter);
                            dblReturn = Convert.ToDouble(count);
                        }
                    });
                    break;
                case FireStatisticType.AreaSqMiles:
                    // Create selection layer
                    BA_ReturnCode success = BA_ReturnCode.UnknownError;
                    string strWhere = $@"{Constants.FIELD_YEAR} > {oInterval.LowerBound - 1} And {Constants.FIELD_YEAR} < {oInterval.UpperBound} + 1";
                    string strNifc = $@"{strGdbFire}\{Constants.FILE_NIFC_FIRE}";
                    string strIntervalFc = $@"tmpInterval_{Convert.ToString(oInterval.Value)}";
                    string strTmpSelect = $@"{strGdbFire}\{strIntervalFc}";
                    success = await GeoprocessingTools.ExportSelectedFeatures(strNifc, strWhere, strTmpSelect);
                    if (success == BA_ReturnCode.Success)
                    {
                        string fieldDissolve = "DISS1";
                        success = await GeoprocessingTools.AddFieldAsync(strTmpSelect, fieldDissolve, "INTEGER", null);
                        if (success == BA_ReturnCode.Success)
                        {
                            QueryFilter qf = new QueryFilter();
                            qf.WhereClause = "OID > -1";
                            success = await GeodatabaseTools.UpdateFeatureAttributeNumericAsync(new Uri(strGdbFire), strIntervalFc,
                                new QueryFilter(), fieldDissolve, 1);
                        }
                        if (success == BA_ReturnCode.Success)
                        {
                            IGPResult gpResult = await QueuedTask.Run(() =>
                            {
                                var parameters = Geoprocessing.MakeValueArray(strTmpSelect, strTmpDissolve, fieldDissolve);
                                return Geoprocessing.ExecuteToolAsync("Dissolve_management", parameters, null,
                                            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                            });
                            if (gpResult.IsFailed)
                            {
                                success = BA_ReturnCode.UnknownError;
                            }
                        }
                        if (success == BA_ReturnCode.Success)
                        {
                            success = await GeoprocessingTools.DeleteDatasetAsync(strTmpSelect);
                            success = await GeoprocessingTools.AddFieldAsync(strTmpDissolve, Constants.FIELD_RECALC_AREA, "Double", null);
                            if (success == BA_ReturnCode.Success)
                            {
                                // Recalculate area due to bug in Pro
                                string strAreaProperties = Constants.FIELD_RECALC_AREA + " AREA_GEODESIC";
                                success = await GeoprocessingTools.CalculateGeometryAsync(strTmpDissolve, strAreaProperties, "SQUARE_METERS");

                            }
                            if (success == BA_ReturnCode.Success)
                            {
                                double dblAreaSqMeters1 = await GeodatabaseTools.CalculateTotalPolygonAreaAsync(new Uri(strGdbFire), strDissolveFc, "");
                                if (dblAreaSqMeters1 > 0)
                                {
                                    dblReturn = Math.Round(AreaUnit.SquareMeters.ConvertTo(dblAreaSqMeters1, AreaUnit.SquareMiles), 2);
                                }
                                else
                                {
                                    dblReturn = 0;
                                }
                            }
                        }
                    }
                    break;
                case FireStatisticType.NifcBurnedAreaPct:
                    // This feature class should already be there if the previous statistic succeeded
                    if (! await GeodatabaseTools.FeatureClassExistsAsync(new Uri(strGdbFire), strDissolveFc))
                    {
                        return 0;
                    }
                    double dblAreaSqMeters = await GeodatabaseTools.CalculateTotalPolygonAreaAsync(new Uri(strGdbFire), strDissolveFc, "");
                    if (dblAreaSqMeters > 0)
                    {
                        dblReturn = Math.Round(dblAreaSqMeters / aoiAreaSqMeters * 100, 3);
                    }
                    else
                    {
                        dblReturn = 0;
                    }
                    break;
                case FireStatisticType.BurnedForestedArea:
                    string strGdbAnalysis = GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Analysis);
                    Map oMap = await MapTools.SetDefaultMapNameAsync(Constants.MAPS_DEFAULT_MAP_NAME);
                    if (await GeodatabaseTools.FeatureClassExistsAsync(new Uri(strGdbAnalysis), Constants.FILE_FORESTED_ZONE) &&
                        await GeodatabaseTools.FeatureClassExistsAsync(new Uri(strGdbFire), strDissolveFc))
                    {
                        string strNifcLayer = $@"{strGdbFire}\{Constants.FILE_NIFC_FIRE}";
                        long polyCount = await GeodatabaseTools.CountFeaturesAsync(new Uri(strGdbFire), strDissolveFc);
                        if (polyCount > 0)
                        {
                            string strForestedZone = $@"{strGdbAnalysis}\{Constants.FILE_FORESTED_ZONE}";
                            // Feature layer name needs to be surrounded by single quotes
                            string[] arrInputLayers = { strForestedZone, strTmpDissolve };
                            string strIntersectFull = $@"{strGdbFire}\{strTmpIntersect}";
                            success = await GeoprocessingTools.IntersectUnrankedAsync(aoiPath, arrInputLayers,
                               strIntersectFull, "ONLY_FID");
                            if (success != BA_ReturnCode.Success)
                            {
                                string strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "An error occurred while running the Intersect tool. Burned forested area cannot be calculated!" + "\r\n";
                                File.AppendAllText(strLogFile, strLogEntry);       // append
                            }
                            else
                            {
                                // Recalculate area due to bug in Pro
                                success = await GeoprocessingTools.AddFieldAsync(strIntersectFull, Constants.FIELD_RECALC_AREA, "Double", null);
                                if (success == BA_ReturnCode.Success)
                                {
                                    // Recalculate area due to bug in Pro
                                    string strAreaProperties = Constants.FIELD_RECALC_AREA + " AREA_GEODESIC";
                                    success = await GeoprocessingTools.CalculateGeometryAsync(strIntersectFull, strAreaProperties, "SQUARE_METERS");

                                }
                                dblAreaSqMeters = await GeodatabaseTools.CalculateTotalPolygonAreaAsync(new Uri(strGdbFire), strTmpIntersect, "");
                                if (dblAreaSqMeters > 0)
                                {
                                    dblReturn = Math.Round(AreaUnit.SquareMeters.ConvertTo(dblAreaSqMeters, AreaUnit.SquareMiles), 2);
                                }
                                else
                                {
                                    dblReturn = 0;
                                }

                            }
                        }
                        else
                        {
                            dblReturn = 0;
                        }
                    }
                    else
                    {
                        string strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "forestedzone is missing. Burned forested area cannot be calculated!" + "\r\n";
                        File.AppendAllText(strLogFile, strLogEntry);       // append
                    }
                    break;
                case FireStatisticType.BurnedForestedAreaPct:
                    double burnedForestAreaSqMeters = -1;
                    if (await GeodatabaseTools.FeatureClassExistsAsync(new Uri(strGdbFire), strTmpIntersect))
                    {
                        burnedForestAreaSqMeters = await GeodatabaseTools.CalculateTotalPolygonAreaAsync(new Uri(strGdbFire), strTmpIntersect, "");
                    }
                    else
                    {
                        double burnedForestAreaSqMiles = await QueryPerimeterStatisticsByIncrementAsync(aoiPath, oInterval, aoiAreaSqMeters, FireStatisticType.BurnedForestedArea, strLogFile);
                        burnedForestAreaSqMeters = AreaUnit.SquareMiles.ConvertTo(burnedForestAreaSqMiles, AreaUnit.SquareMeters);
                    }
                    if (burnedForestAreaSqMeters > 0)
                    {
                        double forestedAreaSqMeters = 
                            await GeodatabaseTools.CalculateTotalPolygonAreaAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Analysis)), Constants.FILE_FORESTED_ZONE, "");
                        dblReturn = Math.Round(burnedForestAreaSqMeters / forestedAreaSqMeters * 100, 3);
                    }
                    else if (burnedForestAreaSqMeters == 0)
                    {
                        dblReturn = 0;
                    }
                    if (await GeodatabaseTools.FeatureClassExistsAsync(new Uri(strGdbFire), strDissolveFc))
                    {
                        // Clean up temporary file
                        success = await GeoprocessingTools.DeleteDatasetAsync($@"{strGdbFire}\{strDissolveFc}");
                    }
                    if (await GeodatabaseTools.FeatureClassExistsAsync(new Uri(strGdbFire), strTmpIntersect))
                    {
                        // Clean up temporary file
                        success = await GeoprocessingTools.DeleteDatasetAsync($@"{strGdbFire}\{strTmpIntersect}");
                    }
                    break;
            }
            return dblReturn;
        }

        public static async Task<double> QueryMtbsStatisticByYearAsync(string aoiPath, int intYear,
            double aoiAreaSqMeters, double dblMtbsCellSize, FireStatisticType fireStatType, bool bMtbsZeroData)
        {
            double dblReturn = 0;
            double dblAreaSqMeters = 0;
            string strGdbFire = GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Fire);
            string strMtbsLayer = GeneralTools.GetMtbsLayerFileName(intYear);
            if (bMtbsZeroData) return dblReturn;
            switch (fireStatType)
            {
                case FireStatisticType.MtbsBurnedAreaPct:
                    dblAreaSqMeters = await QueryMtbsAreaSqMetersAsync(aoiPath, strMtbsLayer, dblMtbsCellSize);
                    if (dblAreaSqMeters > 0)
                    {
                        dblReturn = Math.Round(dblAreaSqMeters / aoiAreaSqMeters * 100, 3);
                    }
                    else
                    {
                        dblReturn = 0;
                    }
                    break;
                case FireStatisticType.MtbsBurnedAreaSqMiles:
                    dblAreaSqMeters = await QueryMtbsAreaSqMetersAsync(aoiPath, strMtbsLayer, dblMtbsCellSize);
                    dblReturn = Math.Round(AreaUnit.SquareMeters.ConvertTo(dblAreaSqMeters, AreaUnit.SquareMiles), 2);
                    break;
            }
            return dblReturn;
        }

        public static async Task<IList<double>> QueryMtbsAreasByYearAsync(string aoiPath, int intYear, double aoiAreaSqMeters, 
            double dblMtbsCellSize)
        {
            IList<double> lstReturn = new List<double>();
            dynamic oFireSettings = GeneralTools.GetFireSettings(aoiPath);
            JArray arrMtbsLegend = oFireSettings.mtbsLegend;
            string[] arrIncludeSeverities = { Constants.VALUE_MTBS_SEVERITY_LOW, Constants.VALUE_MTBS_SEVERITY_MODERATE, Constants.VALUE_MTBS_SEVERITY_HIGH };
            QueryFilter queryFilter = new QueryFilter();
            string strGdbFire = GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Fire);
            string strMtbsLayer = GeneralTools.GetMtbsLayerFileName(intYear);
            if (arrMtbsLegend != null)
            {
                StringBuilder sb2 = new StringBuilder();
                foreach (dynamic item in arrMtbsLegend)
                {
                    string severity = Convert.ToString(item.Severity);
                    if (arrIncludeSeverities.Contains(severity))
                    {
                        sb2.Append($@"{Convert.ToString(item.Value)}");
                        sb2.Append(",");
                    }
                }
                if (sb2.Length > 0)
                {
                    string strWhere = $@"{Constants.FIELD_VALUE} IN ({sb2.ToString().TrimEnd(',')})";
                    queryFilter.WhereClause = strWhere;
                }
                IDictionary<string, long> dictMtbsAreas = await GeodatabaseTools.RasterTableToDictionaryAsync(new Uri(strGdbFire), strMtbsLayer, queryFilter);
                // Low severity
                IList<string> lstSelectedValues = new List<string>();
                foreach (dynamic item in arrMtbsLegend)
                {
                    string severity = Convert.ToString(item.Severity);
                    if (Constants.VALUE_MTBS_SEVERITY_LOW.Equals(severity))
                    {
                        lstSelectedValues.Add(Convert.ToString(item.Value));
                    }
                }
                long lngTotal = 0;
                double dblLowBurnedAreaSqMiles = 0;
                double dblLowBurnedAreaPct = 0;
                if (lstSelectedValues.Count > 0)
                {
                    foreach (var key in dictMtbsAreas.Keys)
                    {
                        if (lstSelectedValues.Contains(key))
                        {
                            lngTotal = lngTotal + dictMtbsAreas[key];
                        }
                    }
                    if (lngTotal > 0)
                    {
                        double dblAreaSqMeters = lngTotal * dblMtbsCellSize * dblMtbsCellSize;
                        dblLowBurnedAreaSqMiles = Math.Round(AreaUnit.SquareMeters.ConvertTo(dblAreaSqMeters, AreaUnit.SquareMiles),2);
                        dblLowBurnedAreaPct = Math.Round(dblAreaSqMeters / aoiAreaSqMeters * 100, 3);
                    }
                }
                lstReturn.Add(dblLowBurnedAreaSqMiles);
                lstReturn.Add(dblLowBurnedAreaPct);
                // Moderate severity
                lstSelectedValues.Clear();
                foreach (dynamic item in arrMtbsLegend)
                {
                    string severity = Convert.ToString(item.Severity);
                    if (Constants.VALUE_MTBS_SEVERITY_MODERATE.Equals(severity))
                    {
                        lstSelectedValues.Add(Convert.ToString(item.Value));
                    }
                }
                lngTotal = 0;
                double dblMedBurnedAreaSqMiles = 0;
                double dblMedBurnedAreaPct = 0;
                if (lstSelectedValues.Count > 0)
                {
                    foreach (var key in dictMtbsAreas.Keys)
                    {
                        if (lstSelectedValues.Contains(key))
                        {
                            lngTotal = lngTotal + dictMtbsAreas[key];
                        }
                    }
                    if (lngTotal > 0)
                    {
                        double dblAreaSqMeters = lngTotal * dblMtbsCellSize * dblMtbsCellSize;
                        dblMedBurnedAreaSqMiles = Math.Round(AreaUnit.SquareMeters.ConvertTo(dblAreaSqMeters, AreaUnit.SquareMiles), 2);
                        dblMedBurnedAreaPct = Math.Round(dblAreaSqMeters / aoiAreaSqMeters * 100, 3);
                    }
                }
                lstReturn.Add(dblMedBurnedAreaSqMiles);
                lstReturn.Add(dblMedBurnedAreaPct);
                // High severity
                lstSelectedValues.Clear();
                foreach (dynamic item in arrMtbsLegend)
                {
                    string severity = Convert.ToString(item.Severity);
                    if (Constants.VALUE_MTBS_SEVERITY_HIGH.Equals(severity))
                    {
                        lstSelectedValues.Add(Convert.ToString(item.Value));
                    }
                }
                lngTotal = 0;
                double dblHighBurnedAreaSqMiles = 0;
                double dblHighBurnedAreaPct = 0;
                if (lstSelectedValues.Count > 0)
                {
                    foreach (var key in dictMtbsAreas.Keys)
                    {
                        if (lstSelectedValues.Contains(key))
                        {
                            lngTotal = lngTotal + dictMtbsAreas[key];
                        }
                    }
                    if (lngTotal > 0)
                    {
                        double dblAreaSqMeters = lngTotal * dblMtbsCellSize * dblMtbsCellSize;
                        dblHighBurnedAreaSqMiles = Math.Round(AreaUnit.SquareMeters.ConvertTo(dblAreaSqMeters, AreaUnit.SquareMiles), 2);
                        dblHighBurnedAreaPct = Math.Round(dblAreaSqMeters / aoiAreaSqMeters * 100, 3);
                    }
                }
                lstReturn.Add(dblHighBurnedAreaSqMiles);
                lstReturn.Add(dblHighBurnedAreaPct);
            }
            return lstReturn;
        }
        public static async Task<IList<double>> QueryMtbsForestedAreasAsync(string aoiPath, string strMtbsLayerName, double forestedAreaSqMeters, CancelableProgressor prog)
        {
            IList<double> lstReturn = new List<double>();
            string strInputFeatures = $@"{GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Analysis)}\{Constants.FILE_FORESTED_ZONE}";
            string strOutputTable = "tmpTabulate";
            IGPResult gpResult = await QueuedTask.Run(() =>
            {
                var parameters = Geoprocessing.MakeValueArray(strInputFeatures, Constants.FIELD_GRID_CODE, strMtbsLayerName, Constants.FIELD_VALUE,
                                                              $@"{GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Fire)}\{strOutputTable}", 
                                                              strMtbsLayerName);
                return Geoprocessing.ExecuteToolAsync("TabulateArea_sa", parameters, null,
                           prog, GPExecuteToolFlags.AddToHistory);
            });
            if (gpResult.IsFailed)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(QueryMtbsForestedAreasAsync),
                    "Unable to Tabulate Area. Error code: " + gpResult.ErrorCode);
                return lstReturn;
            }

            StringBuilder sb = new StringBuilder();
            // Building query string for forested area nlcd codes
            foreach (var code in Constants.VALUES_NLCD_FORESTED_AREA)
            {
                sb.Append($@"{code},");
            }
            string strCodes = sb.ToString().TrimEnd(',');
            string strWhere = Constants.FIELD_GRID_CODE + " IN (" + strCodes + ")";
            QueryFilter queryFilter = new QueryFilter();
            queryFilter.WhereClause = strWhere;
            dynamic oFireSettings = GeneralTools.GetFireSettings(aoiPath);
            JArray arrMtbsLegend = oFireSettings.mtbsLegend;
            IList<string> lstLow = new List<string>();
            IList<string> lstModerate = new List<string>();
            IList<string> lstHigh = new List<string>();
            foreach (dynamic item in arrMtbsLegend)
            {
                string severity = Convert.ToString(item.Severity);
                switch (severity)
                {
                    case Constants.VALUE_MTBS_SEVERITY_LOW:
                        lstLow.Add($@"VALUE_{item.Value}");
                        break;
                    case Constants.VALUE_MTBS_SEVERITY_MODERATE:
                        lstModerate.Add($@"VALUE_{item.Value}");
                        break;
                    case Constants.VALUE_MTBS_SEVERITY_HIGH:
                        lstHigh.Add($@"VALUE_{item.Value}");
                        break;
                    default:
                        break;
                }
            }
            double lowSeveritySqMeters = 0;
            double modSeveritySqMeters = 0;
            double highSeveritySqMeters = 0;
            Uri uriFire = new Uri($@"{GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Fire)}");
            IList<string> lstAreas = null;
            foreach (var aField in lstLow)
            {
                lstAreas = await GeodatabaseTools.QueryTableForDistinctValuesAsync(uriFire, strOutputTable, aField, queryFilter);
                foreach (string area in lstAreas)
                {
                    double dblArea = Convert.ToDouble(area);
                    lowSeveritySqMeters = lowSeveritySqMeters + dblArea;
                }
            }
            double lowSeveritySqMiles = Math.Round(AreaUnit.SquareMeters.ConvertTo(lowSeveritySqMeters, AreaUnit.SquareMiles), 2);
            lstReturn.Add(lowSeveritySqMiles);
            if (lowSeveritySqMeters > 0)
            {
                lstReturn.Add(Math.Round(lowSeveritySqMeters / forestedAreaSqMeters * 100, 3));
            }
            else
            {
                lstReturn.Add(0);
            }
            foreach (var aField in lstModerate)
            {
                lstAreas = await GeodatabaseTools.QueryTableForDistinctValuesAsync(uriFire, strOutputTable, aField, queryFilter);
                foreach (string area in lstAreas)
                {
                    double dblArea = Convert.ToDouble(area);
                    modSeveritySqMeters = modSeveritySqMeters + dblArea;
                }
            }
            double modSeveritySqMiles = Math.Round(AreaUnit.SquareMeters.ConvertTo(modSeveritySqMeters, AreaUnit.SquareMiles), 2);
            lstReturn.Add(modSeveritySqMiles);
            if (modSeveritySqMeters > 0)
            {
                lstReturn.Add(Math.Round(modSeveritySqMeters / forestedAreaSqMeters * 100, 3));
            }
            else
            {
                lstReturn.Add(0);
            }
            foreach (var aField in lstHigh)
            {
                lstAreas = await GeodatabaseTools.QueryTableForDistinctValuesAsync(uriFire, strOutputTable, aField, queryFilter);
                foreach (string area in lstAreas)
                {
                    double dblArea = Convert.ToDouble(area);
                    highSeveritySqMeters = highSeveritySqMeters + dblArea;
                }
            }
            double highSeveritySqMiles = Math.Round(AreaUnit.SquareMeters.ConvertTo(highSeveritySqMeters, AreaUnit.SquareMiles), 2);
            lstReturn.Add(highSeveritySqMiles);
            if (highSeveritySqMeters > 0)
            {
                lstReturn.Add(Math.Round(highSeveritySqMeters / forestedAreaSqMeters * 100, 3));
            }
            else
            {
                lstReturn.Add(0);
            }

            if (await GeodatabaseTools.TableExistsAsync(uriFire, strOutputTable))
            {
                BA_ReturnCode success = await GeoprocessingTools.DeleteDatasetAsync($@"{uriFire.LocalPath}\{strOutputTable}");
            }
            return lstReturn;
        }

        public static async Task<IList<string>> GenerateAnnualFireStatisticsList(BA_Objects.Aoi oAoi, string strLogFile, double aoiAreaSqMeters,
                double dblMtbsCellSize, int intYear, int intReportEndYear, IList<string> lstMissingMtbsYears)
        {
            IList<string> lstElements = new List<string>();
            lstElements.Add(oAoi.StationTriplet);   // Station triplet
            lstElements.Add(oAoi.Name);  //AOI Name
            lstElements.Add(Convert.ToString(intReportEndYear));
            string gdbFire = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Fire);
            bool bMtbsExists = true;
            bool bMtbsZeroData = false;
            if (lstMissingMtbsYears.Contains(Convert.ToString(intYear)))
            {
                bMtbsExists = false;
            }
            if (! await GeodatabaseTools.RasterDatasetExistsAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Fire)), 
                GeneralTools.GetMtbsLayerFileName(intYear)))
            {
                bMtbsZeroData = true;
            }

            double dblFireCount = await QueryPerimeterStatisticsByYearAsync(oAoi.FilePath, intYear, aoiAreaSqMeters, FireStatisticType.Count, strLogFile);
            lstElements.Add(Convert.ToString(dblFireCount));
            double dblAreaSqMiles = await QueryPerimeterStatisticsByYearAsync(oAoi.FilePath, intYear, aoiAreaSqMeters, FireStatisticType.AreaSqMiles, strLogFile);
            lstElements.Add(Convert.ToString(dblAreaSqMiles));
            if (bMtbsExists)
            {
                double dblMtbsAreaSqMiles = await QueryMtbsStatisticByYearAsync(oAoi.FilePath, intYear, aoiAreaSqMeters, dblMtbsCellSize,
                        FireStatisticType.MtbsBurnedAreaSqMiles, bMtbsZeroData); 
                lstElements.Add(Convert.ToString(dblMtbsAreaSqMiles));
            }
            else
            {
                lstElements.Add("");    // leave cell null if missing values
            }
            double dblBurnedAreaPct = await QueryPerimeterStatisticsByYearAsync(oAoi.FilePath, intYear, aoiAreaSqMeters, FireStatisticType.NifcBurnedAreaPct, strLogFile);
            lstElements.Add(Convert.ToString(dblBurnedAreaPct));
            if (bMtbsExists)
            {
                double dblMtbsBurnedAreaPct = await QueryMtbsStatisticByYearAsync(oAoi.FilePath, intYear, aoiAreaSqMeters, dblMtbsCellSize,
                    FireStatisticType.MtbsBurnedAreaPct, bMtbsZeroData);
                lstElements.Add(Convert.ToString(dblMtbsBurnedAreaPct));
            }
            else
            {
                lstElements.Add("");    // leave cell null if missing values
            }
            double dblForestAreaSqMiles = await QueryPerimeterStatisticsByYearAsync(oAoi.FilePath, intYear, aoiAreaSqMeters, FireStatisticType.BurnedForestedArea, strLogFile);
            lstElements.Add(Convert.ToString(dblForestAreaSqMiles));
            double dblForestBurnedAreaPct = 0;
            if (dblForestAreaSqMiles > 0)
            {
                dblForestBurnedAreaPct = await QueryPerimeterStatisticsByYearAsync(oAoi.FilePath, intYear, aoiAreaSqMeters, FireStatisticType.BurnedForestedAreaPct, strLogFile);
            }
            lstElements.Add(Convert.ToString(dblForestBurnedAreaPct));
            bool bMtbsError = true;

            if (bMtbsExists)
            {
                double forestedAreaSqMeters =
                    await GeodatabaseTools.CalculateTotalPolygonAreaAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis)), Constants.FILE_FORESTED_ZONE, "");
                // mtbs FORESTED burned areas by severity
                if (bMtbsZeroData || forestedAreaSqMeters <= 0)
                {
                    lstElements.Add("0");
                    lstElements.Add("0");
                    lstElements.Add("0");
                    lstElements.Add("0");
                    lstElements.Add("0");
                    lstElements.Add("0");
                }
                else
                {
                    string strMtbsLayerName = $@"{GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Fire)}\{GeneralTools.GetMtbsLayerFileName(intYear)}_RECL";
                    IList<double> lstMtbsForestedAreas = await QueryMtbsForestedAreasAsync(oAoi.FilePath, strMtbsLayerName, forestedAreaSqMeters, CancelableProgressor.None);
                    foreach (var area in lstMtbsForestedAreas)
                    {
                        lstElements.Add(Convert.ToString(area));
                    }                    
                }
                // mtbs all burned areas by severity
                if (bMtbsZeroData)
                {
                    lstElements.Add("0");
                    lstElements.Add("0");
                    lstElements.Add("0");
                    lstElements.Add("0");
                    lstElements.Add("0");
                    lstElements.Add("0");
                }
                else
                {
                    IList<double> lstMtbsAreas = await QueryMtbsAreasByYearAsync(oAoi.FilePath, intYear, aoiAreaSqMeters, dblMtbsCellSize);
                    if (lstMtbsAreas.Count == 6)
                    {
                        bMtbsError = false;
                        string strLowSevArea = Convert.ToString(lstMtbsAreas[0]);
                        string strLowSevPct = Convert.ToString(lstMtbsAreas[1]);
                        string strModSevArea = Convert.ToString(lstMtbsAreas[2]);
                        string strModSevPct = Convert.ToString(lstMtbsAreas[3]);
                        string strHighSevArea = Convert.ToString(lstMtbsAreas[4]);
                        string strHighSevPct = Convert.ToString(lstMtbsAreas[5]);
                        lstElements.Add(strLowSevArea);
                        lstElements.Add(strLowSevPct);
                        lstElements.Add(strModSevArea);
                        lstElements.Add(strModSevPct);
                        lstElements.Add(strHighSevArea);
                        lstElements.Add(strHighSevPct);
                    }
                }
            }
            if (!bMtbsExists || bMtbsError)
            {
                lstElements.Add("");
                lstElements.Add("");
                lstElements.Add("");
                lstElements.Add("");
                lstElements.Add("");
                lstElements.Add("");
            }

            return lstElements;
        }

        public static async Task<IList<string>> GenerateIncrementFireStatisticsList(BA_Objects.Aoi oAoi, string strLogFile, double aoiAreaSqMeters,
            double dblMtbsCellSize, IList<Interval> lstInterval, IList<string> lstMtbsMissingYears)
        {
            Interval objInterval = lstInterval.Last();
            IList<string> lstElements = new List<string>();
            lstElements.Add(oAoi.StationTriplet);   // Station triplet
            lstElements.Add(oAoi.Name);  //AOI Name
            lstElements.Add(Convert.ToString(objInterval.UpperBound));
            string missingYears = "";
            StringBuilder sb = new StringBuilder();
            int countMissing = 0;
            if (lstMtbsMissingYears.Count > 0)
            {
                foreach (string year in lstMtbsMissingYears)
                {
                    int intYear = Convert.ToInt16(year);
                    if (intYear >= objInterval.LowerBound && intYear <= objInterval.UpperBound)
                    {
                        sb.Append($@"{year}|");
                        countMissing++;
                    }
                }
                if (countMissing == (objInterval.UpperBound-objInterval.LowerBound+1))
                {
                    missingYears = "ALL";
                }
                else if (sb.ToString().Length > 0)
                {
                    missingYears = sb.ToString().TrimEnd('|');  //Delimit missing years with pipe           
                }
            }
            lstElements.Add(missingYears);
            string gdbFire = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Fire);
            foreach (var oInterval in lstInterval)
            {
                double dblFireCount = await QueryPerimeterStatisticsByIncrementAsync(oAoi.FilePath, oInterval, aoiAreaSqMeters, FireStatisticType.Count, strLogFile);
                lstElements.Add(Convert.ToString(dblFireCount));
            }
            foreach (var oInterval in lstInterval)
            {
                string strPeriod = $@"{oInterval.LowerBound}-{oInterval.UpperBound}|";
                lstElements.Add(strPeriod.TrimEnd('|'));
            }
            foreach (var oInterval in lstInterval)
            {
                double dblAreaSqMiles = await QueryPerimeterStatisticsByIncrementAsync(oAoi.FilePath, oInterval, aoiAreaSqMeters, FireStatisticType.AreaSqMiles, strLogFile);
                lstElements.Add(Convert.ToString(dblAreaSqMiles));
            }
            foreach (var oInterval in lstInterval)
            {
                string strBurnedMtbsArea = await QueryMtbsAreaPctByIncrementAsync(oAoi.FilePath, oInterval, aoiAreaSqMeters, dblMtbsCellSize, FireStatisticType.MtbsBurnedAreaSqMiles, lstMtbsMissingYears);
                lstElements.Add(strBurnedMtbsArea);
            }
            foreach (var oInterval in lstInterval)
            {
                double dblBurnedAreaPct = await QueryPerimeterStatisticsByIncrementAsync(oAoi.FilePath, oInterval, aoiAreaSqMeters, FireStatisticType.NifcBurnedAreaPct, strLogFile);
                lstElements.Add(Convert.ToString(dblBurnedAreaPct));
            }
            foreach (var oInterval in lstInterval)
            {
                string strBurnedAreaPct = await QueryMtbsAreaPctByIncrementAsync(oAoi.FilePath, oInterval, aoiAreaSqMeters, dblMtbsCellSize, FireStatisticType.MtbsBurnedAreaPct,lstMtbsMissingYears);
                lstElements.Add(Convert.ToString(strBurnedAreaPct));
            }
            foreach (var oInterval in lstInterval)
            {
                double dblBurnedForestedArea = await QueryPerimeterStatisticsByIncrementAsync(oAoi.FilePath, oInterval, aoiAreaSqMeters, FireStatisticType.BurnedForestedArea, strLogFile);
                lstElements.Add(Convert.ToString(dblBurnedForestedArea));
            }
            foreach (var oInterval in lstInterval)
            {
                double dblBurnedForestedAreaPct = await QueryPerimeterStatisticsByIncrementAsync(oAoi.FilePath, oInterval, aoiAreaSqMeters, FireStatisticType.BurnedForestedAreaPct, strLogFile);
                lstElements.Add(Convert.ToString(dblBurnedForestedAreaPct));
            }

            // mtbs FORESTED burned areas by severity
            double forestedAreaSqMeters = -1;
            bool bForestedZonesExists = await GeodatabaseTools.FeatureClassExistsAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis)), Constants.FILE_FORESTED_ZONE);
            if (bForestedZonesExists)
            {
                forestedAreaSqMeters = await GeodatabaseTools.CalculateTotalPolygonAreaAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis)), Constants.FILE_FORESTED_ZONE, "");
            }
            int j = 0;
            IList<string> lstLowSevAreas1 = new List<string>();
            IList<string> lstLowSevPcts1 = new List<string>();
            IList<string> lstModSevAreas1 = new List<string>();
            IList<string> lstModSevPcts1 = new List<string>();
            IList<string> lstHighSevAreas1 = new List<string>();
            IList<string> lstHighSevPcts1 = new List<string>();
            foreach (var oInterval in lstInterval)
            {
                bool bAllYearsMissing = true;
                bool bMtbsMaxLayerExists = false;
                for (int i = (int)oInterval.LowerBound; i <= (int)oInterval.UpperBound; i++)
                {
                    if (!lstMtbsMissingYears.Contains(i.ToString()))
                    {
                        bAllYearsMissing = false;
                        break;
                    }
                }
                if (!bAllYearsMissing)
                {
                    bMtbsMaxLayerExists = await GeodatabaseTools.RasterDatasetExistsAsync(new Uri(gdbFire), $@"Max_{oInterval.Value}");
                }
                if (!bForestedZonesExists)
                {
                    lstLowSevAreas1.Add("-1");  // low forested area
                    lstLowSevPcts1.Add("-1");  // low forested pct
                    lstModSevAreas1.Add("-1");  // med forested area
                    lstModSevPcts1.Add("-1");  // med forested pct
                    lstHighSevAreas1.Add("-1");  // high forested area
                    lstHighSevPcts1.Add("-1");  // high forested pct
                }
                else if (bAllYearsMissing)
                {
                    lstLowSevAreas1.Add("");  // low forested area
                    lstLowSevPcts1.Add("");  // low forested pct
                    lstModSevAreas1.Add("");  // med forested area
                    lstModSevPcts1.Add("");  // med forested pct
                    lstHighSevAreas1.Add("");  // high forested area
                    lstHighSevPcts1.Add("");  // high forested pct
                }
                else if (!bMtbsMaxLayerExists)
                {
                    lstLowSevAreas1.Add("0");  // low forested area
                    lstLowSevPcts1.Add("0");  // low forested pct
                    lstModSevAreas1.Add("0");  // med forested area
                    lstModSevPcts1.Add("0");  // med forested pct
                    lstHighSevAreas1.Add("0");  // high forested area
                    lstHighSevPcts1.Add("0");  // high forested pct
                }
                else if (forestedAreaSqMeters <= 0)
                {
                    lstLowSevAreas1.Add("0");  // low forested area
                    lstLowSevPcts1.Add("0");  // low forested pct
                    lstModSevAreas1.Add("0");  // med forested area
                    lstModSevPcts1.Add("0");  // med forested pct
                    lstHighSevAreas1.Add("0");  // high forested area
                    lstHighSevPcts1.Add("0");  // high forested pct
                }
                else
                {
                    string strMtbsLayerName = $@"{GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Fire)}\Max_{oInterval.Value}";
                    IList<double> lstMtbsForestedAreas = await QueryMtbsForestedAreasAsync(oAoi.FilePath, strMtbsLayerName, forestedAreaSqMeters, CancelableProgressor.None);
                    if (lstMtbsForestedAreas.Count == 6)
                    {
                        lstLowSevAreas1.Add(Convert.ToString(lstMtbsForestedAreas[0]));  // low forested area
                        lstLowSevPcts1.Add(Convert.ToString(lstMtbsForestedAreas[1]));  // low forested pct
                        lstModSevAreas1.Add(Convert.ToString(lstMtbsForestedAreas[2]));  // mod forested area
                        lstModSevPcts1.Add(Convert.ToString(lstMtbsForestedAreas[3]));  // mod forested pct
                        lstHighSevAreas1.Add(Convert.ToString(lstMtbsForestedAreas[4])); // high forested area
                        lstHighSevPcts1.Add(Convert.ToString(lstMtbsForestedAreas[5])); // high forested pct
                    }
                }
                j++;
            }
            if (lstLowSevAreas1.Count > 0)
            {
                lstElements.AddRange(lstLowSevAreas1);
                lstElements.AddRange(lstLowSevPcts1);
                lstElements.AddRange(lstModSevAreas1);
                lstElements.AddRange(lstModSevPcts1);
                lstElements.AddRange(lstHighSevAreas1);
                lstElements.AddRange(lstHighSevPcts1);
            }

            IList<string> lstLowSevAreas = new List<string>();
            IList<string> lstLowSevPcts = new List<string>();
            IList<string> lstModSevAreas = new List<string>();
            IList<string> lstModSevPcts = new List<string>();
            IList<string> lstHighSevAreas = new List<string>();
            IList<string> lstHighSevPcts = new List<string>();
            foreach (var oInterval in lstInterval)
            {
                IList<string> lstMtbsAreas = await QueryMtbsAreasByIncrementAsync(oAoi.FilePath, oInterval, aoiAreaSqMeters, dblMtbsCellSize, lstMtbsMissingYears);
                if (lstMtbsAreas.Count == 6)
                {
                    string strLowSevArea = Convert.ToString(lstMtbsAreas[0]);
                    string strLowSevPct = Convert.ToString(lstMtbsAreas[1]);
                    string strModSevArea = Convert.ToString(lstMtbsAreas[2]);
                    string strModSevPct = Convert.ToString(lstMtbsAreas[3]);
                    string strHighSevArea = Convert.ToString(lstMtbsAreas[4]);
                    string strHighSevPct = Convert.ToString(lstMtbsAreas[5]);
                    lstLowSevAreas.Add(strLowSevArea);
                    lstLowSevPcts.Add(strLowSevPct);
                    lstModSevAreas.Add(strModSevArea);
                    lstModSevPcts.Add(strModSevPct);
                    lstHighSevAreas.Add(strHighSevArea);
                    lstHighSevPcts.Add(strHighSevPct);
                }
            }

            lstElements.AddRange(lstLowSevAreas);
            lstElements.AddRange(lstLowSevPcts);
            lstElements.AddRange(lstModSevAreas);
            lstElements.AddRange(lstModSevPcts);
            lstElements.AddRange(lstHighSevAreas);
            lstElements.AddRange(lstHighSevPcts);
            return lstElements;
        }

        public static async Task<IList<string>> QueryMtbsAreasByIncrementAsync(string aoiPath, Interval oInterval, double aoiAreaSqMeters,
            double dblMtbsCellSize, IList<string> lstMtbsMissingYears)
        {
            IList<string> lstReturn = new List<string>();
            dynamic oFireSettings = GeneralTools.GetFireSettings(aoiPath);
            JArray arrMtbsLegend = oFireSettings.mtbsLegend;
            string[] arrIncludeSeverities = { Constants.VALUE_MTBS_SEVERITY_LOW, Constants.VALUE_MTBS_SEVERITY_MODERATE, Constants.VALUE_MTBS_SEVERITY_HIGH };
            QueryFilter queryFilter = new QueryFilter();
            string strGdbFire = GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Fire);
            // This file is created in QueryMtbsAreaPctByIncrementAsync()
            string strMaxFileName = $@"Max_{oInterval.Value}";
            bool bAllYearsMissing = true;
            for (int i = (int)oInterval.LowerBound; i <= (int)oInterval.UpperBound; i++)
            {
                if (!lstMtbsMissingYears.Contains(i.ToString()))
                {
                    bAllYearsMissing = false;
                    break;
                }
            }
            if (await GeodatabaseTools.RasterDatasetExistsAsync(new Uri(strGdbFire), strMaxFileName))
            {
                if (arrMtbsLegend != null)
                {
                    StringBuilder sb2 = new StringBuilder();
                    foreach (dynamic item in arrMtbsLegend)
                    {
                        string severity = Convert.ToString(item.Severity);
                        if (arrIncludeSeverities.Contains(severity))
                        {
                            sb2.Append($@"{Convert.ToString(item.Value)}");
                            sb2.Append(",");
                        }
                    }
                    if (sb2.Length > 0)
                    {
                        string strWhere = $@"{Constants.FIELD_VALUE} IN ({sb2.ToString().TrimEnd(',')})";
                        queryFilter.WhereClause = strWhere;
                    }
                    IDictionary<string, long> dictMtbsAreas = await GeodatabaseTools.RasterTableToDictionaryAsync(new Uri(strGdbFire), strMaxFileName, queryFilter);
                    // Low severity
                    IList<string> lstSelectedValues = new List<string>();
                    foreach (dynamic item in arrMtbsLegend)
                    {
                        string severity = Convert.ToString(item.Severity);
                        if (Constants.VALUE_MTBS_SEVERITY_LOW.Equals(severity))
                        {
                            lstSelectedValues.Add(Convert.ToString(item.Value));
                        }
                    }
                    long lngTotal = 0;
                    double dblLowBurnedAreaSqMiles = 0;
                    double dblLowBurnedAreaPct = 0;
                    if (lstSelectedValues.Count > 0)
                    {
                        foreach (var key in dictMtbsAreas.Keys)
                        {
                            if (lstSelectedValues.Contains(key))
                            {
                                lngTotal = lngTotal + dictMtbsAreas[key];
                            }
                        }
                        if (lngTotal > 0)
                        {
                            double dblAreaSqMeters = lngTotal * dblMtbsCellSize * dblMtbsCellSize;
                            dblLowBurnedAreaSqMiles = Math.Round(AreaUnit.SquareMeters.ConvertTo(dblAreaSqMeters, AreaUnit.SquareMiles), 2);
                            dblLowBurnedAreaPct = Math.Round(dblAreaSqMeters / aoiAreaSqMeters * 100, 3);
                        }
                    }
                    lstReturn.Add(Convert.ToString(dblLowBurnedAreaSqMiles));
                    lstReturn.Add(Convert.ToString(dblLowBurnedAreaPct));
                    // Moderate severity
                    lstSelectedValues.Clear();
                    foreach (dynamic item in arrMtbsLegend)
                    {
                        string severity = Convert.ToString(item.Severity);
                        if (Constants.VALUE_MTBS_SEVERITY_MODERATE.Equals(severity))
                        {
                            lstSelectedValues.Add(Convert.ToString(item.Value));
                        }
                    }
                    lngTotal = 0;
                    double dblMedBurnedAreaSqMiles = 0;
                    double dblMedBurnedAreaPct = 0;
                    if (lstSelectedValues.Count > 0)
                    {
                        foreach (var key in dictMtbsAreas.Keys)
                        {
                            if (lstSelectedValues.Contains(key))
                            {
                                lngTotal = lngTotal + dictMtbsAreas[key];
                            }
                        }
                        if (lngTotal > 0)
                        {
                            double dblAreaSqMeters = lngTotal * dblMtbsCellSize * dblMtbsCellSize;
                            dblMedBurnedAreaSqMiles = Math.Round(AreaUnit.SquareMeters.ConvertTo(dblAreaSqMeters, AreaUnit.SquareMiles), 2);
                            dblMedBurnedAreaPct = Math.Round(dblAreaSqMeters / aoiAreaSqMeters * 100, 3);
                        }
                    }
                    lstReturn.Add(Convert.ToString(dblMedBurnedAreaSqMiles));
                    lstReturn.Add(Convert.ToString(dblMedBurnedAreaPct));
                    // High severity
                    lstSelectedValues.Clear();
                    foreach (dynamic item in arrMtbsLegend)
                    {
                        string severity = Convert.ToString(item.Severity);
                        if (Constants.VALUE_MTBS_SEVERITY_HIGH.Equals(severity))
                        {
                            lstSelectedValues.Add(Convert.ToString(item.Value));
                        }
                    }
                    lngTotal = 0;
                    double dblHighBurnedAreaSqMiles = 0;
                    double dblHighBurnedAreaPct = 0;
                    if (lstSelectedValues.Count > 0)
                    {
                        foreach (var key in dictMtbsAreas.Keys)
                        {
                            if (lstSelectedValues.Contains(key))
                            {
                                lngTotal = lngTotal + dictMtbsAreas[key];
                            }
                        }
                        if (lngTotal > 0)
                        {
                            double dblAreaSqMeters = lngTotal * dblMtbsCellSize * dblMtbsCellSize;
                            dblHighBurnedAreaSqMiles = Math.Round(AreaUnit.SquareMeters.ConvertTo(dblAreaSqMeters, AreaUnit.SquareMiles), 2);
                            dblHighBurnedAreaPct = Math.Round(dblAreaSqMeters / aoiAreaSqMeters * 100, 3);
                        }
                    }
                    lstReturn.Add(Convert.ToString(dblHighBurnedAreaSqMiles));
                    lstReturn.Add(Convert.ToString(dblHighBurnedAreaPct));
                    // 05-JUN-2025: Stop deleting max files; They will be used by the maps
                    //BA_ReturnCode success = await GeoprocessingTools.DeleteDatasetAsync($@"{strGdbFire}\{strMaxFileName}");
                }
            }
            else if (bAllYearsMissing)
            {
                lstReturn.Add("");
                lstReturn.Add("");
                lstReturn.Add("");
                lstReturn.Add("");
                lstReturn.Add("");
                lstReturn.Add("");
            }
            else
            {
                lstReturn.Add("0");
                lstReturn.Add("0");
                lstReturn.Add("0");
                lstReturn.Add("0");
                lstReturn.Add("0");
                lstReturn.Add("0");
            }

            return lstReturn;
        }
        public static async Task<string> QueryMtbsAreaPctByIncrementAsync(string aoiPath, Interval oInterval, double aoiAreaSqMeters, double dblMtbsCellSize,
            FireStatisticType oStatisticType, IList<string> lstMtbsMissingYears)        
        {
            double dblReturn = 0;
            dynamic oFireSettings = GeneralTools.GetFireSettings(aoiPath);
            JArray arrMtbsLegend = oFireSettings.mtbsLegend;
            string[] arrIncludeSeverities = { Constants.VALUE_MTBS_SEVERITY_LOW, Constants.VALUE_MTBS_SEVERITY_MODERATE, Constants.VALUE_MTBS_SEVERITY_HIGH };
            QueryFilter queryFilter = new QueryFilter();
            string strGdbFire = GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Fire);
            bool bAllYearsMissing = true;
            for (int i = (int)oInterval.LowerBound; i <= (int)oInterval.UpperBound; i++)
            {
                if (!lstMtbsMissingYears.Contains(i.ToString()))
                {
                    bAllYearsMissing = false;
                    break;
                }
            }
            string strMaxFileName = $@"Max_{oInterval.Value}";
            if (bAllYearsMissing)
            {
                if (await GeodatabaseTools.RasterDatasetExistsAsync(new Uri(strGdbFire),strMaxFileName))
                {
                    BA_ReturnCode success = await GeoprocessingTools.DeleteDatasetAsync($@"{strGdbFire}\{strMaxFileName}");
                }
                return "";
            }
            StringBuilder sb = new StringBuilder();
            for (int i = (int)oInterval.LowerBound; i <= (int)oInterval.UpperBound; i++)
            {
                string strLayerName = $@"{GeneralTools.GetMtbsLayerFileName(i)}_RECL";
                if (await GeodatabaseTools.RasterDatasetExistsAsync(new Uri(strGdbFire), strLayerName))
                {
                    sb.Append($@"{strGdbFire}\{strLayerName};");
                }
            }
            if (sb.Length > 0)
            {
                string strInputLayerPaths = sb.ToString();
                // Remove the ; at the end of the string
                strInputLayerPaths = strInputLayerPaths.Substring(0, strInputLayerPaths.Length - 1);
                string strMtbsLayer = $@"{strGdbFire}\{strMaxFileName}";
                // Calculate maximum of all 4 layers using Cell Statistics
                var parameters = Geoprocessing.MakeValueArray(strInputLayerPaths, strMtbsLayer, "MAXIMUM");
                var environments = Geoprocessing.MakeEnvironmentArray(workspace: aoiPath, extent: "MAXOF");
                var gpResult = await Geoprocessing.ExecuteToolAsync("CellStatistics_sa", parameters, environments,
                                                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (gpResult.IsFailed)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(QueryMtbsAreaPctByIncrementAsync),
                        "Error Code: " + gpResult.ErrorCode + ". Unable to calculate Cell Statistics for increment!");
                }
                else
                {
                    if (arrMtbsLegend != null)
                    {
                        StringBuilder sb2 = new StringBuilder();
                        foreach (dynamic item in arrMtbsLegend)
                        {
                            string severity = Convert.ToString(item.Severity);
                            if (arrIncludeSeverities.Contains(severity))
                            {
                                sb2.Append($@"{Convert.ToString(item.Value)}");
                                sb2.Append(",");
                            }
                        }
                        if (sb2.Length > 0)
                        {
                            string strWhere = $@"{Constants.FIELD_VALUE} IN ({sb2.ToString().TrimEnd(',')})";
                            queryFilter.WhereClause = strWhere;
                        }
                        IDictionary<string, long> dictMtbsAreas = await GeodatabaseTools.RasterTableToDictionaryAsync(new Uri(strGdbFire), strMaxFileName, queryFilter);
                        if (dictMtbsAreas.Keys.Count > 0)
                        {
                            double dblTotalBurnedArea = 0;
                            foreach (var key in dictMtbsAreas.Keys)
                            {
                                long cellCount = dictMtbsAreas[key];
                                double dblAreaSqMeters = cellCount * dblMtbsCellSize * dblMtbsCellSize;
                                dblTotalBurnedArea = dblTotalBurnedArea + dblAreaSqMeters;
                            }
                            dblReturn = Math.Round(AreaUnit.SquareMeters.ConvertTo(dblTotalBurnedArea, AreaUnit.SquareMiles), 2);
                            if (oStatisticType == FireStatisticType.MtbsBurnedAreaPct)
                            {
                                dblReturn = Math.Round(dblTotalBurnedArea / aoiAreaSqMeters * 100, 3);
                            }                            
                        }
                    }
                }
            }
            return Convert.ToString(dblReturn);
        }
        public static async Task<double> QueryMtbsAreaSqMetersAsync(string aoiPath, string strMtbsLayer, double dblMtbsCellSize)
        {
            StringBuilder sb = new StringBuilder();
            dynamic oFireSettings = GeneralTools.GetFireSettings(aoiPath);
            JArray arrMtbsLegend = oFireSettings.mtbsLegend;
            QueryFilter queryFilter = new QueryFilter();
            if (arrMtbsLegend != null)
            {
                foreach (dynamic item in arrMtbsLegend)
                {
                    string severity = Convert.ToString(item.Severity);
                    if (Constants.MTBS_INCLUDE_SEVERITIES.Contains(severity))
                    {
                        sb.Append($@"{Convert.ToString(item.Value)}");
                        sb.Append(",");
                    }
                }
                if (sb.Length > 0)
                {
                    string strWhere = $@"{Constants.FIELD_VALUE} IN ({sb.ToString().TrimEnd(',')})";
                    queryFilter.WhereClause = strWhere;
                }
            }
            Uri uriFire = new Uri(GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Fire));
            IDictionary<string, long> dictMtbsArea = await GeodatabaseTools.RasterTableToDictionaryAsync(uriFire, strMtbsLayer, queryFilter);
            long lngTotal = 0;
            foreach (var strKey in dictMtbsArea.Keys)
            {
                lngTotal = lngTotal + dictMtbsArea[strKey];
            }
            double dblAreaSqMeters = lngTotal * dblMtbsCellSize * dblMtbsCellSize;
            return dblAreaSqMeters;
        }
    }

}