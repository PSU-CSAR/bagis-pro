using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
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
            Uri gdbUri = new Uri(GetGeodatabasePath(folderPath, GeodatabaseNames.Aoi, false));
            if (System.IO.Directory.Exists(gdbUri.LocalPath))
            {
                Uri uriToCheck = new Uri(gdbUri.LocalPath + Constants.FILE_AOI_RASTER);
                bool bExists = await GeodatabaseTools.RasterDatasetExistsAsync(gdbUri, Constants.FILE_AOI_RASTER);
                if (bExists)
                {
                    return FolderType.AOI;
                }
                bExists = await FeatureClassExistsAsync(gdbUri, Constants.FILE_AOI_VECTOR);
                if (bExists)
                {
                    return FolderType.BASIN;
                }
            }
            return FolderType.FOLDER;
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
                    using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(fileUri)))
                    {
                        Table table = geodatabase.OpenDataset<Table>(featureClassName);
                        using (RowCursor cursor = table.Search(queryFilter, false))
                        {
                            cursor.MoveNext();
                            Feature onlyFeature = (Feature)cursor.Current;
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

        // This method assumes that the polygon feature class contains a single feature. It was written
        // to count the number of points within an AOI
        public static async Task<int> CountPointsWithinInFeatureAsync(Uri pointFeatureGdbUri, string pointFeatureName,
                                                                      Uri polyFeatureGdbUri, string polyFeatureName)
        {
            int retVal = 0;
            Geometry polyGeometry = null;
            await QueuedTask.Run(() =>
            {
                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(polyFeatureGdbUri)))
                using (Table table = geodatabase.OpenDataset<Table>(polyFeatureName))
                {
                    QueryFilter queryFilter = new QueryFilter();
                    double maxArea = -1;    // We will report the points in the largest polygon if > 1
                    using (RowCursor cursor = table.Search(queryFilter, false))
                    {
                        while (cursor.MoveNext())
                        {
                            using (Feature feature = (Feature)cursor.Current)
                            {
                                Geometry areaGeo = feature.GetShape();
                                var area = GeometryEngine.Instance.Area(areaGeo);
                                if (area > maxArea)
                                {
                                    maxArea = area;
                                    polyGeometry = feature.GetShape();
                                }
                            }
                        }
                    }
                }
                if (polyGeometry != null)
                {
                    using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(pointFeatureGdbUri)))
                    using (FeatureClass pointFeatureClass = geodatabase.OpenDataset<FeatureClass>(pointFeatureName))
                    {
                        // Using a spatial query filter to find all features which have a certain district name and lying within a given Polygon.
                        SpatialQueryFilter spatialQueryFilter = new SpatialQueryFilter
                        {
                            FilterGeometry = polyGeometry,
                            SpatialRelationship = SpatialRelationship.Contains
                        };

                        using (RowCursor aCursor = pointFeatureClass.Search(spatialQueryFilter, false))
                        {
                            while (aCursor.MoveNext())
                            {
                                using (Feature feature = (Feature)aCursor.Current)
                                {
                                    retVal++;
                                }
                            }
                        }
                    }
                }
            });
            return retVal;
        }

        public static async Task<int> CountFeaturesAsync(Uri gdbUri, string featureClassName)
        {
            int retVal = -1;
            try
            {
                await QueuedTask.Run(() =>
                {
                    using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(gdbUri)))
                    using (Table table = geodatabase.OpenDataset<Table>(featureClassName))
                    {
                        retVal = table.GetCount();
                    }

                });
            }
            catch (Exception e)
            {
                Debug.Print("CountFeaturesAsync error opening table!!");
                Debug.Print("Exception: " + e.Message);

            }
            return retVal;
        }

        public static async Task<double> CalculateTotalPolygonAreaAsync(Uri gdbUri, string featureClassName)
        {
            double dblRetVal = 0;
            try
            {
                bool bExists = await GeodatabaseTools.FeatureClassExistsAsync(gdbUri, featureClassName);
                if (!bExists)
                    return dblRetVal;

                await QueuedTask.Run(() =>
                {
                    using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(gdbUri)))
                    using (Table table = geodatabase.OpenDataset<Table>(featureClassName))
                    {
                        QueryFilter queryFilter = new QueryFilter();
                        using (RowCursor aCursor = table.Search(queryFilter, false))
                        {
                            while (aCursor.MoveNext())
                            {
                                using (Feature feature = (Feature)aCursor.Current)
                                {
                                    var geometry = feature.GetShape();
                                    var area = GeometryEngine.Instance.Area(geometry);
                                    dblRetVal = dblRetVal + area;
                                }
                            }
                        }
                    }
                });
            }
            catch (Exception e)
            {
                Debug.Print("CalculateTotalPolygonAreaAsync exception: " + e.Message);
                dblRetVal = -1;
            }

            return dblRetVal;
        }

        public static async Task<bool> FeatureClassExistsAsync(Uri gdbUri, string featureClassName)
        {
            bool bExists = false;
            if (gdbUri.IsFile)
            {
                string strFolderPath = System.IO.Path.GetDirectoryName(gdbUri.LocalPath);
                if (System.IO.Directory.Exists(strFolderPath))
                {
                    await QueuedTask.Run(() =>
                    {

                        using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(gdbUri)))
                        {
                            IReadOnlyList<FeatureClassDefinition> definitions = geodatabase.GetDefinitions<FeatureClassDefinition>();
                            foreach (FeatureClassDefinition def in definitions)
                            {
                                if (def.GetName().Equals(featureClassName) || def.GetAliasName().Equals(featureClassName))
                                {
                                    bExists = true;
                                    break;
                                }
                            }
                        }
                    });
                }
            }
            return bExists;
        }

        public static async Task<bool> RasterDatasetExistsAsync(Uri gdbUri, string rasterName)
        {
            bool bExists = false;
            if (gdbUri.IsFile)
            {
                string strFolderPath = System.IO.Path.GetDirectoryName(gdbUri.LocalPath);
                if (System.IO.Directory.Exists(strFolderPath))
                {
                    await QueuedTask.Run(() =>
                    {

                        using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(gdbUri)))
                        {
                            IReadOnlyList<RasterDatasetDefinition> definitions = geodatabase.GetDefinitions<RasterDatasetDefinition>();
                            foreach (RasterDatasetDefinition def in definitions)
                            {
                                if (def.GetName().Equals(rasterName))
                                {
                                    bExists = true;
                                    break;
                                }
                            }
                        }
                    });
                }
            }
            return bExists;
        }

        public static async Task<bool> AttributeExistsAsync(Uri gdbUri, string featureClassName, string fieldName)
        {
            bool bExists = false;
            await QueuedTask.Run(() =>
            {
                try
                {
                    using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(gdbUri)))
                    {
                        using (Table table = geodatabase.OpenDataset<Table>(featureClassName))
                        {
                            TableDefinition definition = table.GetDefinition();
                            int idxField = definition.FindField(fieldName);
                            if (idxField > -1)
                            {
                                bExists = true;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine("AttributeExistsAsync Exception: " + e.Message);
                }
            });
            return bExists;
        }

        public static async Task<BA_ReturnCode> UpdateFeatureAttributesAsync(Uri gdbUri, string featureClassName, QueryFilter oQueryFilter, IDictionary<string, string> dictEdits)
        {
            bool modificationResult = false;
            string errorMsg = "";
            await QueuedTask.Run(() =>
            {
                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(gdbUri)))
                using (FeatureClass featureClass = geodatabase.OpenDataset<FeatureClass>(featureClassName))
                {
                    FeatureClassDefinition featureClassDefinition = featureClass.GetDefinition();

                    EditOperation editOperation = new EditOperation();
                    editOperation.Callback(context => {
                        using (RowCursor rowCursor = featureClass.Search(oQueryFilter, false))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Feature feature = (Feature) rowCursor.Current)
                                {
                                    // In order to update the the attribute table has to be called before any changes are made to the row
                                    context.Invalidate(feature);
                                    // Loop through fields to update
                                    foreach (string strKey in dictEdits.Keys)
                                    {
                                        int idxRow = featureClassDefinition.FindField(strKey);
                                        if (idxRow > -1)
                                        {
                                            feature[idxRow] = dictEdits[strKey];
                                        }
                                    }
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
            });
            if (String.IsNullOrEmpty(errorMsg))
            {
                await Project.Current.SaveEditsAsync();
                return BA_ReturnCode.Success;
            }
            else
            {
                if (Project.Current.HasEdits)
                    await Project.Current.DiscardEditsAsync();
                Debug.Print("UpdateFeatureAttributesAsync: " + errorMsg);
                return BA_ReturnCode.UnknownError;
            }
        }

        public static async Task<string[]> QueryAoiEnvelopeAsync(Uri clipFileGdbUri, string clipName)
        {
            string[] arrRetValues = new string[2];
            Geometry aoiGeo = null;
            int intCount = 0;
            await QueuedTask.Run(() =>
            {
                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(clipFileGdbUri)))
                using (Table table = geodatabase.OpenDataset<Table>(clipName))
                {
                    //check for multiple buffer polygons and buffer AOI if we need to
                    QueryFilter queryFilter = new QueryFilter();
                    using (RowCursor cursor = table.Search(queryFilter, false))
                    {
                        while (cursor.MoveNext())
                        {
                            using (Feature feature = (Feature)cursor.Current)
                            {
                                aoiGeo = feature.GetShape();
                            }
                            intCount++;
                        }
                    }
                }
            });
            if (intCount > 1)
            {
                string tmpClipBuffer = "tmpClipBuffer";
                BA_ReturnCode success = await GeoprocessingTools.BufferAsync(clipFileGdbUri.LocalPath + "\\" + clipName, clipFileGdbUri.LocalPath + "\\" + tmpClipBuffer,
                            "0.5 Meters", "ALL");
                if (success == BA_ReturnCode.Success)
                {
                    await QueuedTask.Run(() =>
                    {
                        using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(clipFileGdbUri)))
                        using (Table table = geodatabase.OpenDataset<Table>(tmpClipBuffer))
                        {
                            //check for multiple buffer polygons and buffer AOI if we need to
                            QueryFilter queryFilter = new QueryFilter();
                            using (RowCursor cursor = table.Search(queryFilter, false))
                            {
                                while (cursor.MoveNext())
                                {
                                    using (Feature feature = (Feature)cursor.Current)
                                    {
                                        aoiGeo = feature.GetShape();    // Replace unbuffered geometry with buffered
                                        clipName = tmpClipBuffer;
                                    }
                                }
                            }
                        }
                    });
                }
            }
            arrRetValues[0] = aoiGeo.Extent.XMin + " " + aoiGeo.Extent.YMin + " " + aoiGeo.Extent.XMax + " " + aoiGeo.Extent.YMax;
            arrRetValues[1] = clipFileGdbUri.LocalPath + "\\" + clipName;
            return arrRetValues;
        }

        public static async Task<IList<BA_Objects.Interval>> ReadReclassRasterAttribute(Uri gdbUri, string rasterName)
        {
            IList<BA_Objects.Interval> lstInterval = new List<BA_Objects.Interval>();
            await QueuedTask.Run(() => {
                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(gdbUri)))
                using (RasterDataset rasterDataset = geodatabase.OpenDataset<RasterDataset>(rasterName))
                {
                    RasterBandDefinition bandDefinition = rasterDataset.GetBand(0).GetDefinition();
                    Tuple<double, double> tupleSize = bandDefinition.GetMeanCellSize();
                    if (Math.Round(tupleSize.Item1, 5) != Math.Round(tupleSize.Item2, 5))
                    {
                        ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("The X and Y cell size values are not the same for " + gdbUri.LocalPath + "\\" +
                                rasterName + ". This may cause problems with some BAGIS functions!!", "BAGIS-PRO");
                    }
                    double cellSize = (tupleSize.Item1 + tupleSize.Item2) / 2;

                    Raster raster = rasterDataset.CreateDefaultRaster();
                    using (Table rasterTable = raster.GetAttributeTable())
                    {
                        TableDefinition definition = rasterTable.GetDefinition();
                        int idxName = definition.FindField(Constants.FIELD_NAME);
                        int idxLowerBound = definition.FindField(Constants.FIELD_LBOUND);
                        int idxUpperBound = definition.FindField(Constants.FIELD_UBOUND);
                        int idxCount = definition.FindField(Constants.FIELD_COUNT);
                        using (RowCursor cursor = rasterTable.Search())
                        {
                            while (cursor.MoveNext())
                            {
                                BA_Objects.Interval interval = new BA_Objects.Interval();
                                Row row = cursor.Current;
                                interval.Value = row[Constants.FIELD_VALUE];
                                if (idxName > 0)
                                {
                                    interval.Name = Convert.ToString(row[idxName]);
                                }
                                else
                                {
                                    interval.Name = Constants.VALUE_UNKNOWN;
                                }
                                if (idxUpperBound > 0)
                                {
                                    interval.UpperBound = Convert.ToDouble(row[idxUpperBound]);
                                }
                                if (idxLowerBound > 0)
                                {
                                    interval.LowerBound = Convert.ToDouble(row[idxLowerBound]);
                                }
                                if (idxCount > 0)
                                {
                                    interval.Area = cellSize * Convert.ToInt32(row[idxCount]);
                                }
                                lstInterval.Add(interval);
                            }
                        }
                    }
                }
            });


            return lstInterval;
        }

    }
}
