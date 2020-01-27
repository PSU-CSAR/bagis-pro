using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bagis_pro
{
    public class AnalysisTools
    {

        public static async Task GenerateSiteLayersAsync()
        {
            double m_bufferDistanceMiles = 5.642;
            double m_elevRangeFeet = 500;
            BA_Objects.Aoi currentAoi = Module1.Current.Aoi;

            try
            {
                //1. Get min/max DEM elevation for reclassing raster. We only want to do this once
                IList<double> lstResult = await GeoprocessingTools.GetDemStatsAsync(Module1.Current.Aoi.FilePath, "", 0.005);
                double demElevMinMeters = -1;
                double demElevMaxMeters = -1;
                if (lstResult.Count == 2)   // We expect the min and max values in that order
                {
                    demElevMinMeters = lstResult[0];
                    demElevMaxMeters = lstResult[1];
                }
                else
                {
                    MessageBox.Show("Unable to read DEM. No Site layers can be generated!!", "BAGIS-PRO");
                    return;
                }

                //2. Create temporary feature class to hold buffered point
                string tmpBuffer = "tmpBuffer";
                var parameters = Geoprocessing.MakeValueArray(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false), 
                    tmpBuffer, "POLYGON", "", "DISABLED", "DISABLED", 
                    GeodatabaseTools.GetGeodatabasePath(currentAoi.FilePath, GeodatabaseNames.Layers, true) + Constants.FILE_SNOTEL);
                var environments = Geoprocessing.MakeEnvironmentArray(workspace: currentAoi.FilePath);
                IGPResult gpResult = await Geoprocessing.ExecuteToolAsync("CreateFeatureclass_management", parameters, environments,
                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);

                //3. Buffer point from feature class and query site information
                Uri layersUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers, false));
                BA_Objects.Site aSite = new BA_Objects.Site();
                //string strInputFeatures = GeodatabaseTools.GetGeodatabasePath(currentAoi.FilePath, GeodatabaseNames.Layers, true) + Constants.FILE_SNOTEL;
                await QueuedTask.Run(() => {
                    Geometry bufferedGeometry = null;
                    using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(layersUri)))
                    using (FeatureClass fClass = geodatabase.OpenDataset<FeatureClass>(Constants.FILE_SNOTEL))
                    {
                        QueryFilter queryFilter = new QueryFilter();
                        using (RowCursor cursor = fClass.Search(queryFilter, false))
                        {
                            cursor.MoveNext();
                            Feature onlyFeature = (Feature)cursor.Current;
                            if (onlyFeature != null)
                            {
                                var pointGeometry = onlyFeature.GetShape();
                                double bufferMeters = LinearUnit.Miles.ConvertTo(m_bufferDistanceMiles, LinearUnit.Meters);
                                bufferedGeometry = GeometryEngine.Instance.Buffer(pointGeometry, bufferMeters);

                                int idx = onlyFeature.FindField(Constants.FIELD_SITE_ELEV);
                                if (idx > -1)
                                {
                                    aSite.ElevMeters = Convert.ToDouble(onlyFeature[idx]);
                                }

                                idx = onlyFeature.FindField(Constants.FIELD_SITE_NAME);
                                if (idx > -1)
                                {
                                    aSite.Name = Convert.ToString(onlyFeature[idx]);
                                }
                                idx = onlyFeature.FindField(Constants.FIELD_OBJECT_ID);
                                if (idx > -1)
                                {
                                    aSite.ObjectId = Convert.ToInt32(onlyFeature[idx]);
                                }

                                aSite.SiteType = SiteType.SNOTEL;

                            }
                        }
                    }

                    Uri gdbUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false));
                    using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(gdbUri)))
                    {
                        var featureClass = geodatabase.OpenDataset<FeatureClass>(tmpBuffer);
                        var featureBuffer = featureClass.CreateRowBuffer();
                        var newFeature = featureClass.CreateRow(featureBuffer) as Feature;
                        newFeature.SetShape(bufferedGeometry);
                        newFeature.Store();
                    }

                    //4. store buffered point in feature class
                    string strBufferedFeatures = GeodatabaseTools.GetGeodatabasePath(currentAoi.FilePath, GeodatabaseNames.Analysis, true) + "tmpBuffer";
                });

                // 5.Build list of reclass items
                double minElevMeters = aSite.ElevMeters - LinearUnit.Feet.ConvertToMeters(m_elevRangeFeet);
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
                double maxElevMeters = aSite.ElevMeters + LinearUnit.Feet.ConvertToMeters(m_elevRangeFeet);
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

                string inputRasterPath = GeodatabaseTools.GetGeodatabasePath(currentAoi.FilePath, GeodatabaseNames.Surfaces, true) + Constants.FILE_DEM_FILLED;
                string maskPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) + tmpBuffer;
                string tmpOutputFile = "reclElev";
                string outputRasterPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) + tmpOutputFile;

                //6. Execute the reclass with the mask set to the buffered point
                parameters = Geoprocessing.MakeValueArray(inputRasterPath, "VALUE", reclassString, outputRasterPath);
                environments = Geoprocessing.MakeEnvironmentArray(mask: maskPath, workspace: currentAoi.FilePath);
                gpResult = await Geoprocessing.ExecuteToolAsync("Reclassify_sa", parameters, environments,
                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);

                //7. Save the reclass as a poly so we can merge with other buffered site polys
                string siteRepFileName = AnalysisTools.GetSiteScenarioFileName(aSite);
                string siteRepresentedPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) + siteRepFileName;
                parameters = Geoprocessing.MakeValueArray(outputRasterPath, siteRepresentedPath);
                gpResult = await Geoprocessing.ExecuteToolAsync("RasterToPolygon_conversion", parameters, environments,
                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);



                Geoprocessing.ShowMessageBox(gpResult.Messages, "GP Messages",
                    gpResult.IsFailed ? GPMessageBoxStyle.Error : GPMessageBoxStyle.Default);


            }
            catch (Exception e)
            {
                MessageBox.Show("GenerateSiteLayers Exception: " + e.Message, "BAGIS PRO");
            }

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
    }

}
