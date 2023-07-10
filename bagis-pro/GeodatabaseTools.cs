using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Exceptions;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
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

        public static string[] AllNames = { Aoi.Value, Prism.Value, Layers.Value, Surfaces.Value, Analysis.Value };
        public static string[] BasinNames = { Aoi.Value, Surfaces.Value };
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
                    // Multiple BAGIS-Pro functions depend on the existence of the pourpoint feature class
                    bExists = await GeodatabaseTools.FeatureClassExistsAsync(gdbUri, Constants.FILE_POURPOINT);
                    if (bExists)
                    {
                        return FolderType.AOI;
                    }                    
                }
                bExists = await FeatureClassExistsAsync(gdbUri, Constants.FILE_AOI_VECTOR);
                if (bExists)
                {
                    return FolderType.BASIN;
                }
            }
            return FolderType.FOLDER;
        }

        public static async Task<FolderType> GetWeaselAoiFolderTypeAsync(string folderPath)
        {
            if (folderPath.IndexOf(".gdb") > -1)
            {
                // The calls below will fail if the folder is a geodatabase folder
                return FolderType.FOLDER;
            }
            IList<string> lstAoiLayers = new List<string>
            {
                folderPath + @"\aoi",
                folderPath + @"\aoibagis"
            };
            IList<string> lstExistingLayers = await GeneralTools.RasterDatasetsExistAsync(lstAoiLayers);
            if (lstExistingLayers.Count > 0)
            {
                return FolderType.AOI;
            }
            lstAoiLayers.Clear();
            lstAoiLayers.Add(folderPath + @"\aoi_v.shp");
            lstExistingLayers = await GeneralTools.ShapefilesExistAsync(lstAoiLayers);
            if (lstExistingLayers.Count > 0)
            {
                return FolderType.BASIN;
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
                    Module1.Current.ModuleLogManager.LogError(nameof(QueryTableForSingleValueAsync),
                        "Exception: " + e.Message);
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
                        Module1.Current.ModuleLogManager.LogError(nameof(GetRasterStats),
                            "Unable to open raster " + strFileName);
                        Module1.Current.ModuleLogManager.LogError(nameof(GetRasterStats),
                            "Exception: " + e.Message);
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
                {
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
                }
                if (polyGeometry != null)
                {
                    using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(pointFeatureGdbUri)))
                    {
                        bool bExists = false;
                        IReadOnlyList<FeatureClassDefinition> definitions = geodatabase.GetDefinitions<FeatureClassDefinition>();
                        foreach (FeatureClassDefinition def in definitions)
                        {
                            if (def.GetName().Equals(pointFeatureName) || def.GetAliasName().Equals(pointFeatureName))
                            {
                                bExists = true;
                                break;
                            }
                        }
                        if (bExists)
                        {
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
                        else
                        {
                            Module1.Current.ModuleLogManager.LogError(nameof(CountPointsWithinInFeatureAsync),
                                "Unable to locate point class " + pointFeatureName);
                        }
                    }
                }
            });
            return retVal;
        }

        public static async Task<long> CountFeaturesAsync(Uri gdbUri, string featureClassName)
        {
            long retVal = -1;
            try
            {
                await QueuedTask.Run(() =>
                {
                    using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(gdbUri)))
                    {
                        bool bExists = false;
                        IReadOnlyList<FeatureClassDefinition> definitions = geodatabase.GetDefinitions<FeatureClassDefinition>();
                        foreach (FeatureClassDefinition def in definitions)
                        {
                            if (def.GetName().Equals(featureClassName) || def.GetAliasName().Equals(featureClassName))
                            {
                                bExists = true;
                                break;
                            }
                        }

                        if (bExists)
                        {
                            using (Table table = geodatabase.OpenDataset<Table>(featureClassName))
                            {
                                retVal = table.GetCount();
                            }
                        }
                        else
                        {
                            retVal = 0;
                            Module1.Current.ModuleLogManager.LogDebug(nameof(CountFeaturesAsync),
                                "Feature class " + featureClassName + " not found. Returning 0 features");
                        }
                    }
                });
            }
            catch (Exception e)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(CountFeaturesAsync),
                    "Exception: " + e.Message);
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
                Module1.Current.ModuleLogManager.LogError(nameof(CalculateTotalPolygonAreaAsync),
                    "Exception: " + e.Message);
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
                    Module1.Current.ModuleLogManager.LogError(nameof(AttributeExistsAsync),
                        "Exception: " + e.Message);
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
                Module1.Current.ModuleLogManager.LogError(nameof(UpdateFeatureAttributesAsync),
                    "Exception: " + errorMsg);
                return BA_ReturnCode.UnknownError;
            }
        }

        public static async Task<BA_ReturnCode> UpdateFeatureAttributeNumericAsync(Uri gdbUri, string featureClassName, QueryFilter oQueryFilter, string strFieldName, double dblNewValue)
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
                                using (Feature feature = (Feature)rowCursor.Current)
                                {
                                    // In order to update the the attribute table has to be called before any changes are made to the row
                                    context.Invalidate(feature);
                                    int idxRow = featureClassDefinition.FindField(strFieldName);
                                    if (idxRow > -1)
                                    {
                                        feature[idxRow] = dblNewValue;
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
                Module1.Current.ModuleLogManager.LogError(nameof(UpdateFeatureAttributesAsync),
                    "Exception: " + errorMsg);
                return BA_ReturnCode.UnknownError;
            }
        }

        public static async Task<BA_ReturnCode> UpdateReclassFeatureAttributesAsync(Uri uriFeatureClass, string strFeatureClassName, 
            IList<BA_Objects.Interval> lstIntervals)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            // Add fields to be updated to feature class, if missing
            string[] arrReclassFields = { Constants.FIELD_NAME, Constants.FIELD_LBOUND, Constants.FIELD_UBOUND};
            string[] arrReclassFieldTypes = { "TEXT", "DOUBLE", "DOUBLE"};
            string strFeatureClassPath = uriFeatureClass.LocalPath + "\\" + strFeatureClassName;
            for (int i = 0; i < arrReclassFields.Length; i++)
            {
                if (await AttributeExistsAsync(uriFeatureClass, strFeatureClassName, arrReclassFields[i]) == false)
                {
                    success = await GeoprocessingTools.AddFieldAsync(strFeatureClassPath, arrReclassFields[i], arrReclassFieldTypes[i]);
                }
            }
            if (success != BA_ReturnCode.Success)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(UpdateReclassFeatureAttributesAsync),
                     "Unable to add fields to " + strFeatureClassPath);
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Unable to add fields to " + strFeatureClassPath + "!!", "BAGIS-PRO");
                return success;
            }

            // Populate the fields
            bool modificationResult = false;
            string errorMsg = "";
            await QueuedTask.Run(() =>
            {
                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(uriFeatureClass)))
                {
                    if (!String.IsNullOrEmpty(strFeatureClassPath))
                    {
                        QueryFilter queryFilter = new QueryFilter();
                        using (Table table = geodatabase.OpenDataset<Table>(strFeatureClassName))
                        {
                            foreach (var oInterval in lstIntervals)
                            {
                                queryFilter.WhereClause = Constants.FIELD_GRID_CODE + " = " + oInterval.Value;
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
                                                int idxTarget = feature.FindField(arrReclassFields[0]);
                                                if (idxTarget > -1)
                                                {
                                                    feature[idxTarget] = oInterval.Name;
                                                }
                                                // lower bound
                                                idxTarget = feature.FindField(arrReclassFields[1]);
                                                if (idxTarget > -1)
                                                {
                                                    feature[idxTarget] = oInterval.LowerBound;
                                                }
                                                // upper bound
                                                idxTarget = feature.FindField(arrReclassFields[2]);
                                                if (idxTarget > -1)
                                                {
                                                    feature[idxTarget] = oInterval.UpperBound;
                                                }
                                                feature.Store();
                                                // Has to be called after the store too
                                                context.Invalidate(feature);
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
                Module1.Current.ModuleLogManager.LogError(nameof(UpdateReclassFeatureAttributesAsync),
                    "Exception: " + errorMsg);
                return BA_ReturnCode.UnknownError;
            }
            return success;
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

        public static async Task<double> GetCellSizeAsync(Uri gdbUri, string rasterName)
        {
            double cellSize = -1.0F;
            if (await GeodatabaseTools.RasterDatasetExistsAsync(gdbUri, rasterName))
            {
                await QueuedTask.Run(() => {
                    using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(gdbUri)))
                    using (RasterDataset rasterDataset = geodatabase.OpenDataset<RasterDataset>(rasterName))
                    {
                        RasterBandDefinition bandDefinition = rasterDataset.GetBand(0).GetDefinition();
                        Tuple<double, double> tupleSize = bandDefinition.GetMeanCellSize();
                        cellSize = (tupleSize.Item1 + tupleSize.Item2) / 2;
                    }
                });
            }
            else
            {
                Module1.Current.ModuleLogManager.LogDebug(nameof(GetCellSizeAsync),
                    $@"Unable to calculate cell size for {gdbUri.LocalPath}\{rasterName}. Raster does not exist!");
            }
            return cellSize;
        }

        public static async Task<bool> TableExistsAsync(Uri gdbUri, string tableName)
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
                            IReadOnlyList<TableDefinition> definitions = geodatabase.GetDefinitions<TableDefinition>();
                            foreach (TableDefinition def in definitions)
                            {
                                if (def.GetName().Equals(tableName) || def.GetAliasName().Equals(tableName))
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

        public static async Task<IList<BA_Objects.Interval>> GetUniqueSortedValuesAsync(Uri gdbUri, SiteType sType, 
            string valueFieldName, string nameFieldName, double upperBound, double lowerBound)
        {
            IList<BA_Objects.Interval> lstInterval = new List<BA_Objects.Interval>();
            if (gdbUri.IsFile)
            {
                string strFolderPath = System.IO.Path.GetDirectoryName(gdbUri.LocalPath);
                if (System.IO.Directory.Exists(strFolderPath))
                {
                    await QueuedTask.Run(() =>
                    {
                        //get Dictionary of unique elevations from the vector att
                        IDictionary<String, String> dictElev = new Dictionary<String, String>();

                        using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(gdbUri)))
                        using (FeatureClass featureClass = geodatabase.OpenDataset<FeatureClass>(Constants.FILE_MERGED_SITES))
                        {
                            FeatureClassDefinition def = featureClass.GetDefinition();
                            int idxElev = def.FindField(valueFieldName);
                            int idxName = def.FindField(nameFieldName);
                            if (idxElev < 0 || idxName < 0)
                            {
                                Module1.Current.ModuleLogManager.LogError(nameof(GetUniqueSortedValuesAsync),
                                    "A required field was missing from " + Constants.FILE_MERGED_SITES + ". Process failed!");
                                return;
                            }
                            QueryFilter queryFilter = new QueryFilter();
                            if (sType == SiteType.Snotel)
                            {
                                string strInClause = $@" IN ('{SiteType.Snotel.ToString()}', '{SiteType.CoopPillow.ToString()}', '{SiteType.Snolite.ToString()}')";
                                queryFilter.WhereClause = Constants.FIELD_SITE_TYPE + strInClause;
                            }
                            else
                            {
                                queryFilter.WhereClause = Constants.FIELD_SITE_TYPE + " = '" + sType + "'";
                            }                            
                            using (RowCursor rowCursor = featureClass.Search(queryFilter, false))
                            {
                                while (rowCursor.MoveNext())
                                {
                                    using (Feature feature = (Feature)rowCursor.Current)
                                    {
                                        string strElev = Convert.ToString(feature[idxElev]);
                                        string strName = "";
                                        if (feature[idxName] == null)
                                        {
                                            strName = "Name missing";
                                        }
                                        else
                                        {
                                            strName = Convert.ToString(feature[idxName]);
                                            if (String.IsNullOrEmpty(strName))
                                            {
                                                strName = "Name missing";
                                            }
                                        }
                                        if (dictElev.ContainsKey(strElev))
                                        {
                                            strName = dictElev[strElev] + ", " + strName;
                                            dictElev[strElev] = strName;
                                        }
                                        else
                                        {
                                            dictElev.Add(strElev, strName);
                                        }
                                    }
                                }
                            }
                        }
                            List<double> lstValidValues = new List<double>();
                            int nuniquevalue = dictElev.Keys.Count;
                            double value = -1.0F;
                            bool bSuccess = false;
                            foreach (var strElev in dictElev.Keys)
                            {
                                bSuccess = Double.TryParse(strElev, out value);
                                if ((int) (value - 0.5) < (int) upperBound && (int) value + 0.5 > (int) lowerBound)
                                {
                                    lstValidValues.Add(value);
                                }
                                else if (value > upperBound || value < lowerBound)  //invalid data in the attribute field, out of bound
                                {
                                    Module1.Current.ModuleLogManager.LogError(nameof(GetUniqueSortedValuesAsync),
                                        "WARNING!! A monitoring site is ignored in the analysis! The site's elevation (" + 
                                        value + ") is outside the DEM range (" + lowerBound + ", " + upperBound + ")!");
                                }
                            }
                        //add upper and lower bnds to the dictionary
                        if (!dictElev.ContainsKey(Convert.ToString(upperBound)))
                        {
                            dictElev.Add(Convert.ToString(upperBound), "Not represented");
                            lstValidValues.Add(upperBound);
                        }
                        if (!dictElev.ContainsKey(Convert.ToString(lowerBound)))
                        {
                            dictElev.Add(Convert.ToString(lowerBound), "Min Value");
                            lstValidValues.Add(lowerBound);
                        }

                    // Sort the list
                    lstValidValues.Sort();
                    // Add lower bound to interval list
                    for (int i = 0; i<lstValidValues.Count -1; i++)
                        {
                            BA_Objects.Interval interval = new BA_Objects.Interval();
                            interval.Value = i+1;
                            interval.LowerBound = lstValidValues[i];
                            double nextItem = lstValidValues[i + 1];
                            interval.UpperBound = nextItem;
                            interval.Name = dictElev[Convert.ToString(nextItem)]; // use the upperbnd name to represent the interval
                            lstInterval.Add(interval);
                        }
                    });
                }
            }
            return lstInterval;
        }

        public static async Task<BA_ReturnCode> CreateGeodatabaseFoldersAsync(string strAoiPath, FolderType fType)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            await QueuedTask.Run(() =>
            {
                var environments = Geoprocessing.MakeEnvironmentArray(workspace: strAoiPath);
                string[] arrGeodatabaseNames = GeodatabaseNames.AllNames;
                if (fType == FolderType.BASIN)
                {
                    arrGeodatabaseNames = GeodatabaseNames.BasinNames;
                }
                {
                    foreach (var item in arrGeodatabaseNames)
                    {
                        var parameters = Geoprocessing.MakeValueArray(strAoiPath, item);
                        var gpResult = Geoprocessing.ExecuteToolAsync("CreateFileGDB_management", parameters, environments,
                                                        CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);

                        if (gpResult.Result.IsFailed)
                        {
                            Module1.Current.ModuleLogManager.LogError(nameof(CreateGeodatabaseFoldersAsync),
                                "Unable to create file geodatabase. Error code: " + gpResult.Result.ErrorCode);
                        }
                        else
                        {
                            success = BA_ReturnCode.Success;
                        }
                    }
                }
            });
            return success;
        }
    }

    
}
