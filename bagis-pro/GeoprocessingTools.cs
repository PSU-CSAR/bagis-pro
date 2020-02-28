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
                string sDemPath = GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Surfaces, true) + Constants.FILE_DEM_FILLED;
                double dblMin = -1;
                var parameters = Geoprocessing.MakeValueArray(sDemPath, "MINIMUM");
                var environments = Geoprocessing.MakeEnvironmentArray(workspace: aoiPath, mask: maskPath);              
                IGPResult gpResult = await Geoprocessing.ExecuteToolAsync("GetRasterProperties_management", parameters, environments,
                    ArcGIS.Desktop.Framework.Threading.Tasks.CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                bool success = Double.TryParse(Convert.ToString(gpResult.ReturnValue), out dblMin);
                returnList.Add(dblMin - adjustmentFactor);
                double dblMax = -1;
                parameters = Geoprocessing.MakeValueArray(sDemPath, "MAXIMUM");
                gpResult = await Geoprocessing.ExecuteToolAsync("GetRasterProperties_management", parameters, environments,
                    ArcGIS.Desktop.Framework.Threading.Tasks.CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                success = Double.TryParse(Convert.ToString(gpResult.ReturnValue), out dblMax);
                returnList.Add(dblMax + adjustmentFactor);
            }
            catch (Exception e)
            {
                Debug.WriteLine("GetDemStatsAsync: " + e.Message);
            }
            return returnList;
        }

        public static async Task<BA_ReturnCode> DeleteDatasetAsync(string featureClassPath)
        {
            var parameters = Geoprocessing.MakeValueArray(featureClassPath);
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
                return BA_ReturnCode.UnknownError;
            }
            else
            {
                return BA_ReturnCode.Success;
            }
        }
    }
}
