using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Desktop.Framework.Threading.Tasks;
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
    }
}
