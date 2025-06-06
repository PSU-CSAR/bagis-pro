﻿using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Core.Internal.CIM;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
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

        public static async Task<BA_ReturnCode> DeleteDatasetAsync(string datasetPath, CancelableProgressor prog)
        {
            var parameters = Geoprocessing.MakeValueArray(datasetPath);
            IGPResult gpResult = await Geoprocessing.ExecuteToolAsync("Delete_management", parameters, null,
                prog, GPExecuteToolFlags.AddToHistory);
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

        public static async Task<BA_ReturnCode> AddFieldAsync(string featureClassPath, string fieldName, string dataType,
            CancelableProgressorSource status)
        {
            IGPResult gpResult = await QueuedTask.Run(() =>
            {
                var parameters = Geoprocessing.MakeValueArray(featureClassPath, fieldName, dataType);
                CancelableProgressor prog = CancelableProgressor.None;
                if (status != null)
                {
                    prog = status.Progressor;
                }
                return Geoprocessing.ExecuteToolAsync("AddField_management", parameters, null,
                            prog, GPExecuteToolFlags.AddToHistory);
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

        public static async Task<BA_ReturnCode> DeleteFieldAsync(string featureClassPath, string fieldName)
        {
            IGPResult gpResult = await QueuedTask.Run(() =>
            {
                var parameters = Geoprocessing.MakeValueArray(featureClassPath, fieldName);
                return Geoprocessing.ExecuteToolAsync("DeleteField_management", parameters, null,
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

        public static async Task<BA_ReturnCode> NearAsync(string strInputFeatures, string strNearFeatures, string strRadius)
        {
            IGPResult gpResult = await QueuedTask.Run(() =>
            {
                var parameters = Geoprocessing.MakeValueArray(strInputFeatures, strNearFeatures, strRadius);
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
                                                             string p_strDissolveOption, CancelableProgressor prog)
        {
            string strLineSide = "FULL";
            string strLineEndType = "ROUND";
            string strDissolveOption = "NONE";
            if (!String.IsNullOrEmpty(p_strDissolveOption))
            {
                strDissolveOption = p_strDissolveOption;
            }
            IGPResult gpResult = await QueuedTask.Run(() =>
            {
                var parameters = Geoprocessing.MakeValueArray(strInputFeatures, strOutputFeatures, strDistance, strLineSide,
                                                              strLineEndType, strDissolveOption);
                return Geoprocessing.ExecuteToolAsync("Buffer_analysis", parameters, null,
                            prog, GPExecuteToolFlags.AddToHistory);
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
                // Always set the extent when clipping from an image service
                var environments = Geoprocessing.MakeEnvironmentArray(workspace: strWorkspace, snapRaster: strSnapRaster, extent: strRectangle);
                var parameters = Geoprocessing.MakeValueArray(strInputRaster, strRectangle, strOutputRaster, strTemplateDataset,
                                    strNoDataValue, strClippingGeometry);
                return Geoprocessing.ExecuteToolAsync("Clip_management", parameters, environments,
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

        public static async Task<BA_ReturnCode> ClipRasterAsLayerAsync(string strInputRaster, string strRectangle, string strOutputRaster,
                                        string strTemplateDataset, string strNoDataValue, bool bUseClippingGeometry,
                                        string strWorkspace, string strSnapRaster)
        {
            string strLayerName = "ClipRasterSource";
            Uri uri = new Uri(strInputRaster);
            BA_ReturnCode success = await MapTools.DisplayRasterLayerAsync(Constants.MAPS_DEFAULT_MAP_NAME, uri, strLayerName, false);
            if (success == BA_ReturnCode.Success)            
            {
                string strClippingGeometry = "NONE";
                if (bUseClippingGeometry == true)
                {
                    strClippingGeometry = "ClippingGeometry";
                }
                IGPResult gpResult = await QueuedTask.Run(() =>
                {
                    var environments = Geoprocessing.MakeEnvironmentArray(workspace: strWorkspace, snapRaster: strSnapRaster, extent: strRectangle);
                    // Always set the extent when clipping from an image service
                    var parameters = Geoprocessing.MakeValueArray(strLayerName, strRectangle, strOutputRaster, strTemplateDataset,
                                        strNoDataValue, strClippingGeometry);
                    return Geoprocessing.ExecuteToolAsync("Clip_management", parameters, environments,
                                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                });

                // Remove the temp layer
                var oMap = await MapTools.SetDefaultMapNameAsync(Constants.MAPS_DEFAULT_MAP_NAME);
                Layer oLayer =
                    oMap.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(strLayerName, StringComparison.CurrentCultureIgnoreCase));
                if (oLayer != null)
                {
                    await QueuedTask.Run(() =>
                    {
                        oMap.RemoveLayer(oLayer);
                    });
                }
                if (gpResult.IsFailed)
                {
                    return BA_ReturnCode.UnknownError;
                }
                else
                {
                    return BA_ReturnCode.Success;
                }
            }
            else
            {
                return success;
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

        public static async Task<BA_ReturnCode> CalculateGeometryAsync(string inFeatures, string strGeometryProperties, string strAreaUnits)
        {
            var parameters = Geoprocessing.MakeValueArray(inFeatures, strGeometryProperties, null, strAreaUnits);
            IGPResult gpResult = await Geoprocessing.ExecuteToolAsync("CalculateGeometryAttributes_management", parameters, null,
                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            if (gpResult.IsFailed)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(CalculateGeometryAsync),
                    "Geoprocessing failed: " + gpResult.Messages);
                return BA_ReturnCode.UnknownError;
            }
            else
            {
                return BA_ReturnCode.Success;
            }
        }

        public static async Task<BA_ReturnCode> ExportSelectedFeatures (string sourceFc, string whereClause, string strTargetFc)
        {
            // Create selection layer
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            FeatureLayer lyrSelect = null;
            var oMap = await MapTools.SetDefaultMapNameAsync(Constants.MAPS_DEFAULT_MAP_NAME);
            await QueuedTask.Run(() =>
            {
                var historyParams = new FeatureLayerCreationParams(new Uri(sourceFc))
                {
                    Name = "Select Layer",
                    IsVisible = false,
                    MapMemberIndex = 0,
                    MapMemberPosition = 0,
                };
                lyrSelect = LayerFactory.Instance.CreateLayer<FeatureLayer>(historyParams, oMap);
                lyrSelect.SetDefinitionQuery($@"{whereClause}");
            });

            var parameters = Geoprocessing.MakeValueArray(lyrSelect.Name, strTargetFc);
            var gpResult = await Geoprocessing.ExecuteToolAsync("ExportFeatures_conversion", parameters, null,
               CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            if (gpResult.IsFailed)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(ExportSelectedFeatures),
                    $@"Export Features could not create{strTargetFc}");
                success = BA_ReturnCode.UnknownError;
            }
            else
            {
                success = BA_ReturnCode.Success;
            }
            // Remove temporary layer
            await QueuedTask.Run(() =>
            {
                oMap.RemoveLayer(lyrSelect);
            });
            return success;
        }
        public static async Task<BA_ReturnCode> ConAsync(string inputRaster, string outputValue, string outputRaster, double cellValue,
            CancelableProgressor prog)
        {
            string strWhereClause = $@"value = {Convert.ToString(cellValue)}";
            var parameters = Geoprocessing.MakeValueArray(inputRaster, outputValue, outputRaster, "", strWhereClause);
            IGPResult gpResult = await Geoprocessing.ExecuteToolAsync("Con_sa", parameters, null,
                prog, GPExecuteToolFlags.AddToHistory);
            if (gpResult.IsFailed)
            {
                return BA_ReturnCode.UnknownError;
            }
            else
            {
                return BA_ReturnCode.Success;
            }
        }

        public static async Task<BA_ReturnCode> RasterToPointAsync(string inputRaster, string strField, 
            string outputName, CancelableProgressor prog)
        {
            var parameters = Geoprocessing.MakeValueArray(inputRaster, outputName, strField);
            IGPResult gpResult = await Geoprocessing.ExecuteToolAsync("RasterToPoint", parameters, null,
                prog, GPExecuteToolFlags.AddToHistory);
            if (gpResult.IsFailed)
            {
                return BA_ReturnCode.UnknownError;
            }
            else
            {
                return BA_ReturnCode.Success;
            }
        }
    }
}
