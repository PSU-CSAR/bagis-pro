using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bagis_pro
{
    class GeoprocessingTools
    {
        public static async Task<IList<double>> GetDemStatsAsync(string aoiPath, string maskPath, double adjustmentFactor)
        {
            IList<double> returnList = new List<double>();
            try
            {
                string sDemPath = GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Surfaces, true) + Constants.FILE_DEM_CLIPPED;
                IList<string> lstLayers = await GeneralTools.GetLayersInGeodatabaseAsync(GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Surfaces), "RasterDatasetDefinition");
                bool bFoundIt = false;
                BA_ReturnCode ret1 = BA_ReturnCode.UnknownError;
                foreach (var strLayer in lstLayers)
                {
                    if (strLayer.Equals(Constants.FILE_DEM_CLIPPED))
                    {
                        bFoundIt = true;
                        break;
                    }
                }
                if (! bFoundIt)
                {
                    // Need to create clipped DEM
                    string filledDem = GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Surfaces, true) + Constants.FILE_DEM_FILLED;
                    var parameters = Geoprocessing.MakeValueArray(filledDem, null, sDemPath, maskPath, null, "ClippingGeometry");
                    IGPResult gpResult = await Geoprocessing.ExecuteToolAsync("Clip_management", parameters, null,
                            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                    if (!gpResult.IsFailed)
                    {
                        Module1.Current.ModuleLogManager.LogInfo(nameof(GetDemStatsAsync),
                            "Successfully clipped filled_dem to largest aoi_v polygon");
                        ret1 = BA_ReturnCode.Success;
                    }
                    else
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(GetDemStatsAsync),
                            "Clipped DEM did not exist and clipping to aoi_v failed. Process aborted!");
                        return returnList;
                    }
                }
                else
                {
                    ret1 = BA_ReturnCode.Success;
                }

                if (ret1.Equals(BA_ReturnCode.Success))
                {
                    double dblMin = -1;
                    var parameters = Geoprocessing.MakeValueArray(sDemPath, "MINIMUM");
                    IGPResult gpResult = await Geoprocessing.ExecuteToolAsync("GetRasterProperties_management", parameters, null,
                        CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                    bool success = Double.TryParse(Convert.ToString(gpResult.ReturnValue), out dblMin);
                    returnList.Add(dblMin - adjustmentFactor);
                    double dblMax = -1;
                    parameters = Geoprocessing.MakeValueArray(sDemPath, "MAXIMUM");
                    gpResult = await Geoprocessing.ExecuteToolAsync("GetRasterProperties_management", parameters, null,
                        CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                    success = Double.TryParse(Convert.ToString(gpResult.ReturnValue), out dblMax);
                    returnList.Add(dblMax + adjustmentFactor);
                }
            }
            catch (Exception e)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(GetDemStatsAsync),
                    "Exception: " + e.Message);
            }
            return returnList;
        }

        public static async Task<BA_ReturnCode> DeleteDatasetAsync(string datasetPath)
        {
            var parameters = Geoprocessing.MakeValueArray(datasetPath);
            IGPResult gpResult = await Geoprocessing.ExecuteToolAsync("Delete_management", parameters, null,
                ArcGIS.Desktop.Framework.Threading.Tasks.CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            if (gpResult.IsFailed)
            {
                return BA_ReturnCode.UnknownError;
            }
            else
            {
                return BA_ReturnCode.Success;
            }
        }

        public static async Task<BA_ReturnCode> DeleteFeatureClassFieldsAsync(string featureClassPath, string[] arrFieldsToDelete)
        {
            var parameters = Geoprocessing.MakeValueArray(featureClassPath, arrFieldsToDelete);
            IGPResult gpResult = await Geoprocessing.ExecuteToolAsync("DeleteField_management", parameters, null,
                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            if (gpResult.IsFailed)
            {
                return BA_ReturnCode.UnknownError;
            }
            else
            {
                return BA_ReturnCode.Success;
            }
        }

        public static async Task<BA_ReturnCode> AddFieldAsync(string featureClassPath, string fieldName, string dataType)
        {
            IGPResult gpResult = await QueuedTask.Run(() =>
            {
                var parameters = Geoprocessing.MakeValueArray(featureClassPath, fieldName, dataType);
                return Geoprocessing.ExecuteToolAsync("AddField_management", parameters, null,
                            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            });
            if (gpResult.IsFailed)
            {
                return BA_ReturnCode.UnknownError;
            }
            else
            {
                return BA_ReturnCode.Success;
            }
        }

        public static async Task<BA_ReturnCode> NearAsync(string strInputFeatures, string strNearFeatures)
        {
            IGPResult gpResult = await QueuedTask.Run(() =>
            {
                var parameters = Geoprocessing.MakeValueArray(strInputFeatures, strNearFeatures);
                return Geoprocessing.ExecuteToolAsync("Near_analysis", parameters, null,
                            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            });
            if (gpResult.IsFailed)
            {
                return BA_ReturnCode.UnknownError;
            }
            else
            {
                return BA_ReturnCode.Success;
            }
        }

        public static async Task<BA_ReturnCode> BufferAsync (string strInputFeatures, string strOutputFeatures, string strDistance,
                                                        string p_strDissolveOption)
        {
            string strLineSide = "";
            string strLineEndType = "";
            string strDissolveOption = "";
            if (!String.IsNullOrEmpty(strDissolveOption))
            {
                strDissolveOption = p_strDissolveOption;
            }
            IGPResult gpResult = await QueuedTask.Run(() =>
            {
                var parameters = Geoprocessing.MakeValueArray(strInputFeatures, strOutputFeatures, strDistance, strLineSide,
                                                              strLineEndType, strDissolveOption);
                return Geoprocessing.ExecuteToolAsync("Buffer_analysis", parameters, null,
                            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            });
            if (gpResult.IsFailed)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(BufferAsync),
                    "Unable to buffer features. Error code: " + gpResult.ErrorCode);
                return BA_ReturnCode.UnknownError;
            }
            else
            {
                Module1.Current.ModuleLogManager.LogDebug(nameof(BufferAsync), "Features buffered successfully");
                return BA_ReturnCode.Success;
            }
        }

        public static async Task<BA_ReturnCode> BufferLinesAsync(string strInputFeatures, string strOutputFeatures, string strDistance,
                                                string strLineSide, string strLineEndOption, string strDissolveOption)
        {
            if (String.IsNullOrEmpty(strLineSide))
            {
                strLineSide = "FULL";
            }
            if (String.IsNullOrEmpty(strLineEndOption))
            {
                strLineEndOption = "ROUND";
            }
            if (String.IsNullOrEmpty(strDissolveOption))
            {
                strDissolveOption = "ALL";
            }
            IGPResult gpResult = await QueuedTask.Run(() =>
            {
                var parameters = Geoprocessing.MakeValueArray(strInputFeatures, strOutputFeatures, strDistance, strLineSide,
                                                              strLineEndOption, strDissolveOption);
                return Geoprocessing.ExecuteToolAsync("Buffer_analysis", parameters, null,
                            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            });
            if (gpResult.IsFailed)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(BufferLinesAsync),
                    "Unable to buffer features. Error code: " + gpResult.ErrorCode);
                return BA_ReturnCode.UnknownError;
            }
            else
            {
                Module1.Current.ModuleLogManager.LogDebug(nameof(BufferLinesAsync), "Lines buffered successfully");
                return BA_ReturnCode.Success;
            }
        }

        public static async Task<BA_ReturnCode> ClipRasterAsync(string strInputRaster, string strRectangle, string strOutputRaster,
                                                string strTemplateDataset, string strNoDataValue, bool bUseClippingGeometry,
                                                string strWorkspace, string strSnapRaster)
        {
            string strClippingGeometry = "NONE";
            if (bUseClippingGeometry == true)
            {
                strClippingGeometry = "ClippingGeometry";
            }
            IGPResult gpResult = await QueuedTask.Run(() =>
            {
                var environments = Geoprocessing.MakeEnvironmentArray(workspace: strWorkspace, snapRaster: strSnapRaster);
                var parameters = Geoprocessing.MakeValueArray(strInputRaster, strRectangle, strOutputRaster, strTemplateDataset,
                                    strNoDataValue, strClippingGeometry);
                return Geoprocessing.ExecuteToolAsync("Clip_management", parameters, null,
                            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            });
            if (gpResult.IsFailed)
            {
                return BA_ReturnCode.UnknownError;
            }
            else
            {
                return BA_ReturnCode.Success;
            }
        }

        public static async Task<BA_ReturnCode> IntersectUnrankedAsync(string strWorkspace, string[] arrInputLayers, string outputLayerPath,
                                                                       string joinAttributes)
        {
             IGPResult gpResult = await QueuedTask.Run(() =>
            {
                var environments = Geoprocessing.MakeEnvironmentArray(workspace: strWorkspace);
                var parameters = Geoprocessing.MakeValueArray(arrInputLayers, outputLayerPath, joinAttributes);
                return Geoprocessing.ExecuteToolAsync("Intersect_analysis", parameters, null,
                            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            });
            if (gpResult.IsFailed)
            {
                return BA_ReturnCode.UnknownError;
            }
            else
            {
                return BA_ReturnCode.Success;
            }
        }

        public static async Task<BA_ReturnCode> CopyFeaturesAsync(string strWorkspace, string copyFeatures,
            string outputFeatures)
        {
            IGPResult gpResult = await QueuedTask.Run(() =>
            {
                var environments = Geoprocessing.MakeEnvironmentArray(workspace: strWorkspace);
                var parameters = Geoprocessing.MakeValueArray(copyFeatures, outputFeatures);
                return Geoprocessing.ExecuteToolAsync("CopyFeatures_management", parameters, environments,
                                                     CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            });
            if (gpResult.IsFailed)
            {
                return BA_ReturnCode.UnknownError;
            }
            else
            {
                return BA_ReturnCode.Success;
            }
        }

        public static async Task<BA_ReturnCode> SetNullAsync(string strInputRaster, string strConstant, string strOutputRaster, string strWhere)
        {
            IGPResult gpResult = await QueuedTask.Run(() =>
            {
                var parameters = Geoprocessing.MakeValueArray(strInputRaster, strConstant, strOutputRaster, strWhere);
                return Geoprocessing.ExecuteToolAsync("SetNull_sa", parameters, null,
                            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            });
            if (gpResult.IsFailed)
            {
                return BA_ReturnCode.UnknownError;
            }
            else
            {
                return BA_ReturnCode.Success;
            }
        }

        public static async Task<IList<double>> GetRasterMinMaxStatsAsync(string aoiPath, string rasterPath, 
                                                                          string maskPath, double adjustmentFactor)
        {
            IList<double> returnList = new List<double>();
            try
            {
                double dblMin = -1;
                var parameters = Geoprocessing.MakeValueArray(rasterPath, "MINIMUM");
                var environments = Geoprocessing.MakeEnvironmentArray(workspace: aoiPath, mask: maskPath);
                IGPResult gpResult = await Geoprocessing.ExecuteToolAsync("GetRasterProperties_management", parameters, environments,
                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                bool success = Double.TryParse(Convert.ToString(gpResult.ReturnValue), out dblMin);
                returnList.Add(dblMin - adjustmentFactor);
                double dblMax = -1;
                parameters = Geoprocessing.MakeValueArray(rasterPath, "MAXIMUM");
                gpResult = await Geoprocessing.ExecuteToolAsync("GetRasterProperties_management", parameters, environments,
                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                success = Double.TryParse(Convert.ToString(gpResult.ReturnValue), out dblMax);
                returnList.Add(dblMax + adjustmentFactor);
            }
            catch (Exception e)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(GetRasterMinMaxStatsAsync),
                    "Exception: " + e.Message);
            }
            return returnList;
        }

        public static async Task<BA_ReturnCode> ApplySymbologyFromLayerAsync(string inputLayer, string symbologyLayer, 
            string updateSymbology)
        {
            var parameters = Geoprocessing.MakeValueArray(inputLayer, symbologyLayer, null, updateSymbology);
            IGPResult gpResult = await Geoprocessing.ExecuteToolAsync("ApplySymbologyFromLayer_management", parameters, null,
                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            if (gpResult.IsFailed)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(ApplySymbologyFromLayerAsync),
                    "Geoprocessing failed: " + gpResult.Messages);
                return BA_ReturnCode.UnknownError;
            }
            else
            {
                return BA_ReturnCode.Success;
            }
        }

        public static async Task<BA_ReturnCode> FeaturesToSnodasGeoJsonAsync(string in_features, string out_json_file,
            bool formatJson)
        {
            string strFormatJson = "NOT_FORMATTED";
            if (formatJson)
            {
                strFormatJson = "FORMATTED";
            }
            var parameters = Geoprocessing.MakeValueArray(in_features, out_json_file, strFormatJson, null, null,
                "GEOJSON", "WGS84");
            IGPResult gpResult = await Geoprocessing.ExecuteToolAsync("FeaturesToJSON_conversion", parameters, null,
                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            if (gpResult.IsFailed)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(FeaturesToSnodasGeoJsonAsync),
                    "Geoprocessing failed: " + gpResult.Messages);
                return BA_ReturnCode.UnknownError;
            }
            else
            {
                return BA_ReturnCode.Success;
            }

        }
    }
}
