using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using System;
using System.Collections.Generic;
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

        public static async Task<FolderType> GetAoiFolderType(string folderPath)
        {
            FolderType retVal = FolderType.FOLDER;
            Uri gdbUri = new Uri(GetGeodatabasePath(folderPath, GeodatabaseNames.Aoi));
            if (System.IO.Directory.Exists(gdbUri.LocalPath))
            {
                Uri uriToCheck = new Uri(gdbUri.LocalPath + "\\aoi_v");
                bool bExists = await FileExists(uriToCheck);
                if (bExists)
                {
                    retVal = FolderType.AOI;
                }
            }
            return retVal;
        }

        public static async Task<bool> FileExists(Uri fileUri)
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
                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strFolderPath))))
                {
                    // Open a featureClass (within a feature dataset or outside a feature dataset).
                    using (FeatureClass featureClass = geodatabase.OpenDataset<FeatureClass>(strFileName))
                    {
                        if (featureClass != null)
                        {
                            return true;
                        }
                    }
                }
                return false;
            });

        }
    }
}
