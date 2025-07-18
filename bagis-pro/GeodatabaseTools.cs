﻿using ActiproSoftware.Windows.Extensions;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Exceptions;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using bagis_pro.BA_Objects;
using System;
using System.Collections.Generic;
using System.IO;
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
        public static GeodatabaseNames Fire { get { return new GeodatabaseNames("fire.gdb"); } }

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
                            Row onlyRow = cursor.Current;
                            if (onlyRow != null)
                            {
                                int idx = onlyRow.FindField(fieldName);
                                if (idx > -1)
                                {
                                    returnValue = Convert.ToString(onlyRow[idx]);
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

        public static async Task<IList<string>> QueryTableForDistinctValuesAsync(Uri fileUri, string featureClassName, string fieldName, QueryFilter queryFilter)
        {
            // parse the uri for the folder and file
            IList<string> lstReturn = new List<string>();   
            string strFileName = null;
            string strFolderPath = null;
            if (fileUri.IsFile)
            {
                strFileName = System.IO.Path.GetFileName(fileUri.LocalPath);
                strFolderPath = System.IO.Path.GetDirectoryName(fileUri.LocalPath);
            }
            else
            {
                return lstReturn;
            }
            await QueuedTask.Run(() =>
            {
                try
                {
                    using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(fileUri)))
                    {
                        Table table = geodatabase.OpenDataset<Table>(featureClassName);
                        using (RowCursor cursor = table.Search(queryFilter, false))
                        {
                            while (cursor.MoveNext())
                            {
                                Row aRow = cursor.Current;
                                if (aRow != null)
                                {
                                    int idx = aRow.FindField(fieldName);
                                    if (idx > -1)
                                    {
                                        string strValue = Convert.ToString(aRow[idx]);
                                        if (!string.IsNullOrEmpty(strValue) &&
                                            !lstReturn.Contains(strValue))
                                        {
                                            lstReturn.Add(strValue);
                                        }
                                    }
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
            return lstReturn;
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
                            Module1.Current.ModuleLogManager.LogWarn(nameof(CountPointsWithinInFeatureAsync),
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

        public static async Task<long> CountFeaturesWithFilterAsync(Uri gdbUri, string featureClassName,
            string strWhere)
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
                            using (FeatureClass featureClass = geodatabase.OpenDataset<FeatureClass>(featureClassName))
                            {
                                QueryFilter queryFilter = new QueryFilter();
                                queryFilter.WhereClause = strWhere;
                                retVal = featureClass.GetCount(queryFilter);
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

        public static async Task<double> CalculateTotalPolygonAreaAsync(Uri gdbUri, string featureClassName,
            string strWhere)
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
                        if (!string.IsNullOrEmpty(strWhere))
                        {
                            queryFilter.WhereClause = strWhere;
                        }
                        TableDefinition definition = table.GetDefinition();
                        int idxArea = definition.FindField(Constants.FIELD_RECALC_AREA);
                        using (RowCursor aCursor = table.Search(queryFilter, false))
                        {
                            while (aCursor.MoveNext())
                            {
                                if (idxArea > -1)
                                {
                                    Row row = aCursor.Current;
                                    dblRetVal = dblRetVal + Convert.ToDouble(row[idxArea]);
                                }
                                else
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
                if (System.IO.Directory.Exists(gdbUri.LocalPath))
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
                if (System.IO.Directory.Exists(gdbUri.LocalPath))
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
        public static async Task<bool> AttributeExistsShapefileAsync(Uri gdbUri, string shapefileName, string fieldName)
        {
            bool bExists = false;
            await QueuedTask.Run(() =>
            {
                try
                {
                    FileSystemConnectionPath fileConnection = new FileSystemConnectionPath(new Uri(gdbUri.LocalPath), FileSystemDatastoreType.Shapefile);
                    using (FileSystemDatastore shapefile = new FileSystemDatastore(fileConnection))
                    {
                        using (Table table = shapefile.OpenDataset<Table>(shapefileName))
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
                    Module1.Current.ModuleLogManager.LogError(nameof(AttributeExistsShapefileAsync),
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

        public static async Task<BA_ReturnCode> UpdateFeatureAttributeNumericAsync(Uri gdbUri, string featureClassName, QueryFilter oQueryFilter, 
            string strFieldName, double dblNewValue)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            // Geodatabase
            if (gdbUri.LocalPath.IndexOf(".gdb") > -1)
            { 
                await QueuedTask.Run(async () =>
                {
                    using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(gdbUri)))
                    using (FeatureClass featureClass = geodatabase.OpenDataset<FeatureClass>(featureClassName))
                    {
                        success = await UpdateFeatureAttributeNumericAsync(featureClass, oQueryFilter, strFieldName, dblNewValue);
                    }
                });
            }
            else
            {
                // Shapefile
                FileSystemConnectionPath fileConnection = new FileSystemConnectionPath(new Uri(gdbUri.LocalPath), FileSystemDatastoreType.Shapefile);
                await QueuedTask.Run(async () =>
                {
                    using (FileSystemDatastore shapefile = new FileSystemDatastore(fileConnection))
                    {
                        FeatureClass featureClass = shapefile.OpenDataset<FeatureClass>(featureClassName);
                        success = await UpdateFeatureAttributeNumericAsync(featureClass, oQueryFilter, strFieldName, dblNewValue);
                    }
                });
            }
            return success;
        }

        private static async Task<BA_ReturnCode> UpdateFeatureAttributeNumericAsync(FeatureClass featureClass, QueryFilter oQueryFilter,
            string strFieldName, double dblNewValue)
        {
            bool modificationResult = false;
            string errorMsg = "";
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
                    success = await GeoprocessingTools.AddFieldAsync(strFeatureClassPath, arrReclassFields[i], arrReclassFieldTypes[i], null);
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
            if (! await RasterDatasetExistsAsync(gdbUri,rasterName)) 
            {
                return lstInterval;
            }
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
                                    // Square cell size to calculate area
                                    interval.Area = cellSize * cellSize * Convert.ToInt32(row[idxCount]);
                                }
                                lstInterval.Add(interval);
                            }
                        }
                    }
                }
            });


            return lstInterval;
        }
        public static async Task<double> GetCellSizeAsync(Uri uriRaster, WorkspaceType workspaceType)
        {
            double cellSize = -1.0F;
            string rasterName = System.IO.Path.GetFileName(uriRaster.LocalPath);
            switch (workspaceType)
            {
                case WorkspaceType.Geodatabase:
                    Uri gdbUri = new Uri(System.IO.Path.GetDirectoryName(uriRaster.LocalPath));
                    if (await GeodatabaseTools.RasterDatasetExistsAsync(gdbUri, rasterName ))
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
                    break;
                case WorkspaceType.ImageServer:
                    var oMap = await MapTools.SetDefaultMapNameAsync(Constants.MAPS_DEFAULT_MAP_NAME);
                    await QueuedTask.Run(() =>
                    {
                        // Create an image service layer using the url for an image service.
                        var isLayer = LayerFactory.Instance.CreateLayer(uriRaster, oMap) as ImageServiceLayer;
                        if (isLayer != null)
                        {
                            isLayer.SetVisibility(false);
                            var oRaster = isLayer.GetRaster();
                            if (oRaster != null)
                            {
                                var oRasterBand = oRaster.GetBand(0);
                                if (oRasterBand != null)
                                {
                                    Tuple<double, double> tupleSize = oRasterBand.GetDefinition().GetMeanCellSize();
                                    cellSize = (tupleSize.Item1 + tupleSize.Item2) / 2;
                                }
                            }
                            oMap.RemoveLayer(isLayer);
                        }
                    });
                    break;
                case WorkspaceType.Raster:
                    // Create a FileSystemConnectionPath using the folder path
                    Uri folderUri = new Uri(System.IO.Path.GetDirectoryName(uriRaster.LocalPath));
                    await QueuedTask.Run(() => {
                        FileSystemConnectionPath connectionPath =
                            new FileSystemConnectionPath(folderUri, FileSystemDatastoreType.Raster);
                        // Create a new FileSystemDatastore using the FileSystemConnectionPath.
                        using (FileSystemDatastore dataStore = new FileSystemDatastore(connectionPath))
                        using (RasterDataset fileRasterDataset = dataStore.OpenDataset<RasterDataset>(rasterName))
                        {
                            // Open the raster dataset.
                            if (fileRasterDataset != null)
                            {
                                RasterBandDefinition bandDefinition = fileRasterDataset.GetBand(0).GetDefinition();
                                Tuple<double, double> tupleSize = bandDefinition.GetMeanCellSize();
                                cellSize = (tupleSize.Item1 + tupleSize.Item2) / 2;
                            }
                        }
                    });
                    break;
                default:
                    Module1.Current.ModuleLogManager.LogError(nameof(GetCellSizeAsync), "Invalid workspaceType provided!");
                    break;
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

        public static async Task<BA_ReturnCode> CreateGeodatabaseFoldersAsync(string strAoiPath, FolderType fType,
            CancelableProgressor prog)
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
                                                        prog, GPExecuteToolFlags.AddToHistory);

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

        // key is value, value is count
        public static async Task<IDictionary<string, long>> RasterTableToDictionaryAsync(Uri gdbUri, 
            string rasterName, QueryFilter queryFilter)
        {
            IDictionary<string, long> dictReturn = new Dictionary<string, long>(); 
            await QueuedTask.Run(() =>
            {
                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(gdbUri)))
                using (RasterDataset rasterDataset = geodatabase.OpenDataset<RasterDataset>(rasterName))
                {
                    RasterBandDefinition bandDefinition = rasterDataset.GetBand(0).GetDefinition();
                    Raster raster = rasterDataset.CreateDefaultRaster();
                    using (Table rasterTable = raster.GetAttributeTable())
                    {
                        TableDefinition definition = rasterTable.GetDefinition();
                        int idxValue = definition.FindField(Constants.FIELD_VALUE);
                        int idxCount = definition.FindField(Constants.FIELD_COUNT);
                        using (RowCursor cursor = rasterTable.Search(queryFilter))
                        {
                            while (cursor.MoveNext())
                            {
                                Row row = cursor.Current;
                                long lngCount = Convert.ToInt64(row[idxCount]);
                                string strValue = Convert.ToString(row[idxValue]);
                                if (!string.IsNullOrEmpty(strValue))
                                {
                                    dictReturn.Add(strValue, lngCount);
                                }
                            }
                        }
                    }
                }
               });
            return dictReturn;
        }
        public static async Task<BA_ReturnCode> AddAOIVectorAttributesAsync(Uri uriAoiGdb, string aoiName, CancelableProgressorSource source)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            string[] arrAddFields = new string[] { Constants.FIELD_STATION_NAME, Constants.FIELD_STATION_TRIPLET, Constants.FIELD_BASIN };
            string[] arrNewFieldTypes = new string[] { "TEXT", "TEXT", "TEXT" };
            string[] arrNewFieldValues = new string[] { aoiName, "", "" };
            for (int i = 0; i < arrAddFields.Length; i++)
            {
                bool bExists = await GeodatabaseTools.AttributeExistsAsync(uriAoiGdb, Constants.FILE_AOI_VECTOR,arrAddFields[i]);
                if (! bExists)
                {
                    success = await GeoprocessingTools.AddFieldAsync($@"{uriAoiGdb.LocalPath}\{Constants.FILE_AOI_VECTOR}", arrAddFields[i], 
                        arrNewFieldTypes[i], source);
                    if (success == BA_ReturnCode.Success && !string.IsNullOrEmpty(arrNewFieldValues[i]))
                    {
                        IDictionary<string,string> dictUpdate = new Dictionary<string,string>();
                        dictUpdate.Add(arrAddFields[i], arrNewFieldValues[i]);
                        success = await GeodatabaseTools.UpdateFeatureAttributesAsync(uriAoiGdb, Constants.FILE_AOI_VECTOR, new QueryFilter(), dictUpdate);
                    }
                }
            }
            return success;
        }

        public static async Task<BA_ReturnCode> AddPourpointAttributesAsync(string aoiPath, string aoiName, 
            string stationTriplet, string basinName, CancelableProgressorSource status)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            string[] arrAddFields = new string[] { Constants.FIELD_STATION_NAME, Constants.FIELD_STATION_TRIPLET, Constants.FIELD_BASIN, Constants.FIELD_HUC2};
            string[] arrNewFieldTypes = new string[] { "TEXT", "TEXT", "TEXT", "INTEGER" };
            string[] arrNewFieldValues = new string[] { aoiName, stationTriplet, basinName, "" };
            Uri uriAoiGdb = new Uri(GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Aoi));
            for (int i = 0; i < arrAddFields.Length; i++)
            {
                bool bExists = await AttributeExistsAsync(uriAoiGdb, Constants.FILE_POURPOINT, arrAddFields[i]);
                if (!bExists)
                {
                    success = await GeoprocessingTools.AddFieldAsync($@"{uriAoiGdb.LocalPath}\{Constants.FILE_POURPOINT}", arrAddFields[i],
                        arrNewFieldTypes[i], status);
                    if (success == BA_ReturnCode.Success && !string.IsNullOrEmpty(arrNewFieldValues[i]))
                    {
                        IDictionary<string, string> dictUpdate = new Dictionary<string, string>();
                        dictUpdate.Add(arrAddFields[i], arrNewFieldValues[i]);
                        success = await UpdateFeatureAttributesAsync(uriAoiGdb, Constants.FILE_POURPOINT, new QueryFilter(), dictUpdate);
                    }
                }
            }
            int huc2 = await Webservices.QueryHuc2Async(aoiPath);
            success = await GeodatabaseTools.UpdateFeatureAttributeNumericAsync(uriAoiGdb, Constants.FILE_POURPOINT, null, Constants.FIELD_HUC2, huc2);
            return success;
        }
        public static async Task<string> GetEnvelope(string strGdb, string strFile)
        {
            string strClipEnvelope = "";
            await QueuedTask.Run(() =>
            {
                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strGdb))))
                using (Table table = geodatabase.OpenDataset<Table>(strFile))
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
            return strClipEnvelope;
        }
        public static async Task<SpatialReference> GetSpatialReferenceAsync(string strGdb, string strFile)
        {
            SpatialReference spatialReference = null;
            await QueuedTask.Run( () =>
            {
                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strGdb))))
                using (FeatureClass fc = geodatabase.OpenDataset<FeatureClass>(strFile))
                {
                    var classDefinition = fc.GetDefinition() as FeatureClassDefinition;
                    spatialReference = classDefinition.GetSpatialReference();
                }
            });
            return spatialReference;
        }
        public static async Task<(double,bool)> CalculateAoiAreaSqMetersAsync(string aoiPath, double inAreaSqM)
        {
            double areaSqM = inAreaSqM;
            bool bIsMeters = false;
            SpatialReference sr = await GetSpatialReferenceAsync(GetGeodatabasePath(aoiPath, GeodatabaseNames.Aoi), Constants.FILE_AOI_VECTOR);
            if (sr != null)
            {
                double areaUndefined = await CalculateTotalPolygonAreaAsync(new Uri(GetGeodatabasePath(aoiPath, GeodatabaseNames.Aoi)), Constants.FILE_AOI_VECTOR, "");
                var oUnit = sr.Unit;
                if (oUnit.Name.ToUpper().Equals("METER"))
                {
                    areaSqM = areaUndefined;
                    bIsMeters = true;
                }
                else if (oUnit.Name.ToUpper().Equals("FOOT"))
                {
                    areaSqM = AreaUnit.SquareFeet.ConvertTo(areaUndefined, AreaUnit.SquareMeters);
                }
                else
                {
                    areaSqM = -1;    
                }
            }
            return (areaSqM, bIsMeters);
        }
        public static async Task<IList<string>> QueryMissingMtbsRasters(string aoiPath, int minYear, int maxYear)
        {
            IList<string> result = new List<string>();
            IList<string> layerNames = new List<string>();
            IList<int> allYears = new List<int>();
            for (int i = minYear; i <= maxYear; i++)
            {
                string layerName = GeneralTools.GetMtbsLayerFileName(i);
                layerNames.Add(layerName);
                allYears.Add(i);
;           }
            // Get a list of all the rasters in the fire.gdb
            Uri uriFire = new Uri(GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Fire));
            IList<string> lstMtbsRasters = new List<string>();
            if (Directory.Exists(uriFire.LocalPath))
            {
                await QueuedTask.Run(() =>
                {
                    using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(uriFire)))
                    {
                        IReadOnlyList<RasterDatasetDefinition> definitions = geodatabase.GetDefinitions<RasterDatasetDefinition>();
                        foreach (RasterDatasetDefinition def in definitions)
                        {
                            if (def.GetName().IndexOf("mtbs") > -1)
                            {
                                lstMtbsRasters.Add(def.GetName());
                            }
                        }
                    }
                });
            }

            if (lstMtbsRasters.Count > 0)
            {
                for (int i = minYear; i <= maxYear; i++)
                {
                    string validLayerName = GeneralTools.GetMtbsLayerFileName(i);
                    string noDataLayerName = $@"{validLayerName}_{Constants.VALUE_NO_DATA.ToUpper()}";
                    if (!lstMtbsRasters.Contains(validLayerName) && !lstMtbsRasters.Contains(noDataLayerName))
                    {
                        result.Add(Convert.ToString(i));
                    }
                }
            }
            else
            {
                foreach (var year in allYears)
                {
                    result.Add(Convert.ToString(year));
                }
            }
            return result;
        }
        public static async Task<bool> MissingFireHistory(string aoiPath)
        {
            Uri uriFire = new Uri(GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Fire));
            long fireHistoryCount = await GeodatabaseTools.CountFeaturesAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Layers)), Constants.FILE_FIRE_HISTORY);
            if (fireHistoryCount > 0)
            {
                return false;
            }
            bool result = false;
            // Get a list of all the rasters in the fire.gdb
            if (Directory.Exists(uriFire.LocalPath))
            {
                await QueuedTask.Run(() =>
                {
                    using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(uriFire)))
                    {
                        IReadOnlyList<RasterDatasetDefinition> definitions = geodatabase.GetDefinitions<RasterDatasetDefinition>();
                        foreach (RasterDatasetDefinition def in definitions)
                        {
                            if (def.GetName().IndexOf("_RECL") > -1)
                            {
                                result = true;
                                continue;
                            }
                        }
                    }
                });
            }
            return result;
        }
    }

    
}
