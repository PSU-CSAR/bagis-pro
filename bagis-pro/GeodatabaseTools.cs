using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
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
    public class GeodatabaseNames
    {
        private GeodatabaseNames(string value) { Value = value; }

        public string Value { get; set; }

        public static GeodatabaseNames Aoi { get { return new GeodatabaseNames("aoi.gdb"); } }
        public static GeodatabaseNames Prism { get { return new GeodatabaseNames("prism.gdb"); } }
        public static GeodatabaseNames Layers { get { return new GeodatabaseNames("layers.gdb"); } }
        public static GeodatabaseNames Surfaces { get { return new GeodatabaseNames("surfaces.gdb"); } }
        public static GeodatabaseNames Analysis { get { return new GeodatabaseNames("analysis.gdb"); } }
    }

    public class GeodatabaseTools
    {
        public static string GetGeodatabasePath(string aoiPath, GeodatabaseNames gdbNames,
                                                bool hasTrailingBackSlash = false)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(aoiPath);
            sb.Append("\\");
            sb.Append(gdbNames.Value);
            if (hasTrailingBackSlash)
                sb.Append("\\");
            return sb.ToString();
        }

        public static async Task<FolderType> GetAoiFolderTypeAsync(string folderPath)
        {
            Uri gdbUri = new Uri(GetGeodatabasePath(folderPath, GeodatabaseNames.Aoi, true));
            if (System.IO.Directory.Exists(gdbUri.LocalPath))
            {
                Uri uriToCheck = new Uri(gdbUri.LocalPath + Constants.FILE_AOI_RASTER);
                bool bExists = await FileExistsAsync(uriToCheck, esriDatasetType.esriDTRasterDataset);
                if (bExists)
                {
                    return FolderType.AOI;
                }
                uriToCheck = new Uri(gdbUri.LocalPath + Constants.FILE_AOI_VECTOR);
                bExists = await FileExistsAsync(uriToCheck, esriDatasetType.esriDTFeatureClass);
                if (bExists)
                {
                    return FolderType.BASIN;
                }
            }
            return FolderType.FOLDER;
        }

        public static async Task<bool> FileExistsAsync(Uri fileUri, esriDatasetType datasetType)
        {
            // parse the uri for the folder and file
            string strFileName = null;
            string strFolderPath = null;
            if (fileUri.IsFile)
            {
                strFileName = System.IO.Path.GetFileName(fileUri.LocalPath);
                strFolderPath = System.IO.Path.GetDirectoryName(fileUri.LocalPath);
            }
            else
            {
                return false;
            }

            return await QueuedTask.Run<bool>(() =>
            {
                try
                {
                    using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strFolderPath))))
                    {
                        // The openDataset throws an exception if the dataset cannot be opened
                        if (datasetType.Equals(esriDatasetType.esriDTFeatureClass))
                        {
                            // Open a featureClass (within a feature dataset or outside a feature dataset).
                            using (FeatureClass featureClass = geodatabase.OpenDataset<FeatureClass>(strFileName))
                            {
                                return true;
                            }
                        }
                        else if (datasetType.Equals(esriDatasetType.esriDTRasterDataset))
                        {
                            // Open a featureClass (within a feature dataset or outside a feature dataset).
                            using (RasterDataset raster = geodatabase.OpenDataset<RasterDataset>(strFileName))
                            {
                                return true;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine("FileExistsAsync Exception: " + e.Message);
                }
                return false;
            });

        }

        public static async Task<string> QueryTableForSingleValueAsync(Uri fileUri, string featureClassName, string fieldName, QueryFilter queryFilter)
        {
            // parse the uri for the folder and file
            string strFileName = null;
            string strFolderPath = null;
            if (fileUri.IsFile)
            {
                strFileName = System.IO.Path.GetFileName(fileUri.LocalPath);
                strFolderPath = System.IO.Path.GetDirectoryName(fileUri.LocalPath);
            }
            else
            {
                return "";
            }

            string returnValue = "";
            await QueuedTask.Run(() =>
            {
                try
                {
                    using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strFolderPath))))
                    {
                        Table table = geodatabase.OpenDataset<Table>(featureClassName);
                        using (RowCursor cursor = table.Search(queryFilter, false))
                        {
                            cursor.MoveNext();
                            Feature onlyFeature = (Feature) cursor.Current;
                            if (onlyFeature != null)
                            {
                                int idx = onlyFeature.FindField(fieldName);
                                if (idx > -1)
                                {
                                    returnValue = Convert.ToString(onlyFeature[idx]);
                                }
                            }

                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine("QueryTableForSingleValueAsync Exception: " + e.Message);
                }
            });
            return returnValue;
        }

        public static async Task<TableStatisticsResult> GetRasterStats(Uri rasterUri, string field)
        {
            // parse the uri for the folder and file
            string strFileName = null;
            string strFolderPath = null;
            if (rasterUri.IsFile)
            {
                strFileName = System.IO.Path.GetFileName(rasterUri.LocalPath);
                strFolderPath = System.IO.Path.GetDirectoryName(rasterUri.LocalPath);
            }

            RasterDataset rDataset = null;
            await QueuedTask.Run(() =>
            {                
                // Opens a file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                using (Geodatabase geodatabase =
                    new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strFolderPath))))
                {
                    // Use the geodatabase.
                    try
                    {
                        rDataset = geodatabase.OpenDataset<RasterDataset>(strFileName);
                    }
                    catch (GeodatabaseTableException e)
                    {
                        Debug.WriteLine("DisplayRasterAsync: Unable to open raster " + strFileName);
                        Debug.WriteLine("DisplayRasterAsync: " + e.Message);
                        return;
                    }
                }
             });
            TableStatisticsResult tableStatisticsResult = null;
            if (rDataset != null)
            {
                await QueuedTask.Run(() =>
                {
                    Raster raster = rDataset.CreateRaster(new int[] { 0 });
                    if (raster != null)
                    {
                        var table = raster.GetAttributeTable();
                        if (table != null)
                        {
                            Field statField = table.GetDefinition().GetFields().First(x => x.Name.Equals(field));

                            StatisticsDescription statisticsDescription = new StatisticsDescription(statField, new List<StatisticsFunction>() { StatisticsFunction.Min, StatisticsFunction.Max });
                            TableStatisticsDescription tableStatisticsDescription = new TableStatisticsDescription(new List<StatisticsDescription>() { statisticsDescription });
                            IReadOnlyList<TableStatisticsResult> statResult = table.CalculateStatistics(tableStatisticsDescription);
                            tableStatisticsResult = statResult[0];
                        }
                    }
                });
            }
            return tableStatisticsResult;
        }
    }
}
