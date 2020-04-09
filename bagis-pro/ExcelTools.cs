using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using Microsoft.Office.Interop.Excel;

namespace bagis_pro
{
    class ExcelTools
    {
        public static async Task<BA_ReturnCode> CreateElevationTableAsync(Worksheet pworksheet, double aoiDemMin)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;

            //read class definition for chart and table labeling
            Uri uriElevZones = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false));
            IList<BA_Objects.Interval> lstInterval = await GeodatabaseTools.ReadReclassRasterAttribute(uriElevZones, Constants.FILE_ELEV_ZONE);

            //===========================
            //Zonal Statistics
            //===========================
            string strTable = "tblZones";
            IGPResult gpResult = await QueuedTask.Run(() =>
            {
                //string sMask = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_VECTOR;
                string sMask = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_RASTER;
                var environments = Geoprocessing.MakeEnvironmentArray(workspace: Module1.Current.Aoi.FilePath, snapRaster: Module1.Current.Aoi.SnapRasterPath,
                    mask: sMask);
                string strInZoneData = uriElevZones.LocalPath + "\\" + Constants.FILE_ELEV_ZONE;
                string strInValueRaster = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Surfaces, true) + Constants.FILE_DEM_FILLED;
                string strOutTable = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) + strTable;
                var parameters = Geoprocessing.MakeValueArray(strInZoneData, Constants.FIELD_VALUE, strInValueRaster, strOutTable);
                return Geoprocessing.ExecuteToolAsync("ZonalStatisticsAsTable", parameters, environments,
                            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            });
            if (gpResult.IsFailed)
            {
                success = BA_ReturnCode.UnknownError;
            }
            else
            {
                success = BA_ReturnCode.Success;
            }

            //=============================================
            // Create Field Titles
            //=============================================
            pworksheet.Cells[1, 1] = "VALUE";
            pworksheet.Cells[1, 2] = "COUNT";
            pworksheet.Cells[1, 3] = "AREA";
            pworksheet.Cells[1, 4] = "MIN";
            pworksheet.Cells[1, 5] = "MAX";
            pworksheet.Cells[1, 6] = "RANGE";
            pworksheet.Cells[1, 7] = "MEAN";
            pworksheet.Cells[1, 8] = "STD";
            pworksheet.Cells[1, 9] = "SUM";
            pworksheet.Cells[1, 10] = "%_AREA";
            pworksheet.Cells[1, 11] = "%_AREA_ELV";
            pworksheet.Cells[1, 12] = "LABEL";

            //============================================
            // Populate Elevation and Percent Area Rows
            //============================================
            Uri analysisUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false));
            await QueuedTask.Run(() => {
                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(analysisUri)))
            using (Table statisticsTable = geodatabase.OpenDataset<Table>(strTable))
            {
                long rasterValueCount = statisticsTable.GetCount();
                long sumOfCount = 0;
                using (RowCursor rowCursor = statisticsTable.Search(new QueryFilter(), false))
                {
                    while (rowCursor.MoveNext())
                    {
                        using (Row row = rowCursor.Current)
                        {
                            sumOfCount = sumOfCount + Convert.ToInt32(row["COUNT"]);
                        }
                    }
                }

                double percentArea = 0;
                int i = 0;
                using (RowCursor rowCursor = statisticsTable.Search(new QueryFilter(), false))
                {
                    while (rowCursor.MoveNext())
                    {
                        using (Row row = rowCursor.Current)
                        {
                            double count = Convert.ToDouble(row[Constants.FIELD_COUNT]);
                            percentArea = percentArea + (count / sumOfCount * 100);
                            // Populate Excel Table
                            double dblUpperBound = lstInterval[i].UpperBound;
                            if (!Module1.Current.Settings.m_demDisplayUnits.Equals(Module1.Current.Settings.m_demUnits))
                            {
                                if (Module1.Current.Settings.m_demDisplayUnits.Equals("Meters") &&
                                    Module1.Current.Settings.m_demUnits.Equals("Feet"))
                                    {
                                        dblUpperBound = LinearUnit.Feet.ConvertTo(dblUpperBound, LinearUnit.Meters);
                                    }
                                else if (Module1.Current.Settings.m_demDisplayUnits.Equals("Feet") &&
                                         Module1.Current.Settings.m_demUnits.Equals("Meters"))
                                    {
                                        dblUpperBound = LinearUnit.Meters.ConvertTo(dblUpperBound, LinearUnit.Feet);
                                    }
                            }
                            pworksheet.Cells[i + 3, 1] = dblUpperBound;
                            pworksheet.Cells[i + 3, 2] = row[Constants.FIELD_COUNT];
                            pworksheet.Cells[i + 3, 3] = Math.Round(Convert.ToDouble(row["AREA"]),0);
                            pworksheet.Cells[i + 3, 4] = row["MIN"];
                            pworksheet.Cells[i + 3, 5] = row["MAX"];
                            pworksheet.Cells[i + 3, 6] = row["RANGE"];
                            pworksheet.Cells[i + 3, 7] = row["MEAN"];
                            pworksheet.Cells[i + 3, 8] = row["STD"];
                            pworksheet.Cells[i + 3, 9] = row["SUM"];
                            pworksheet.Cells[i + 3, 10] = count / sumOfCount * 100;
                            pworksheet.Cells[i + 3, 11] = percentArea;
                            pworksheet.Cells[i + 3, 12] = lstInterval[i].Name; 
                            i++;
                        }
                    }
                }
            }
            });

            //aoiDemMin is always in meters
            //Make First Elevation Interval the Min of AOI DEM
            if (Module1.Current.Settings.m_demDisplayUnits.Equals("Feet"))
            {
                aoiDemMin = LinearUnit.Meters.ConvertTo(aoiDemMin, LinearUnit.Feet);
            }
            pworksheet.Cells[2, 1] = aoiDemMin;  //Value
            pworksheet.Cells[2, 11] = 0;         //PERCENT_AREA_ELEVATION

            success = BA_ReturnCode.Success;
            return success;
        }

        private static long CountRecords(Worksheet pWorksheet, int beginningRow)
        {
            long validRow = 0;
            if (pWorksheet != null)
            {
                long count = pWorksheet.UsedRange.Rows.Count;
                for (int i = beginningRow; i < count; i++)
                {
                    Range cell = pWorksheet.UsedRange.Cells[i, 1];
                    string strCell = cell.ToString();
                    if (! String.IsNullOrEmpty(strCell))
                    {
                        validRow = validRow++;
                    }
                }
            }
            return validRow;
        }

        public static async Task<BA_ReturnCode> CreateSnotelTableAsync(Worksheet pworksheet, Worksheet pElevWorkSheet)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            Uri uriSnotelZones = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false));
            IList<BA_Objects.Interval> lstInterval = await GeodatabaseTools.ReadReclassRasterAttribute(uriSnotelZones, Constants.FILE_SNOTEL_ZONE);

            //===========================
            //Zonal Statistics
            //===========================
            string strTable = "tblZones";
            IGPResult gpResult = await QueuedTask.Run(() =>
            {
                //string sMask = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_VECTOR;
                string sMask = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_RASTER;
                var environments = Geoprocessing.MakeEnvironmentArray(workspace: Module1.Current.Aoi.FilePath, snapRaster: Module1.Current.Aoi.SnapRasterPath,
                    mask: sMask);
                string strInZoneData = uriSnotelZones.LocalPath + "\\" + Constants.FILE_SNOTEL_ZONE;
                string strInValueRaster = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Surfaces, true) + Constants.FILE_DEM_FILLED;
                string strOutTable = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) + strTable;
                var parameters = Geoprocessing.MakeValueArray(strInZoneData, Constants.FIELD_VALUE, strInValueRaster, strOutTable);
                return Geoprocessing.ExecuteToolAsync("ZonalStatisticsAsTable", parameters, environments,
                            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            });
            if (gpResult.IsFailed)
            {
                success = BA_ReturnCode.UnknownError;
            }
            else
            {
                success = BA_ReturnCode.Success;
            }

            //=============================================
            // Create Field Titles
            //=============================================
            pworksheet.Cells[1, 1] = "VALUE";
            pworksheet.Cells[1, 2] = "COUNT";
            pworksheet.Cells[1, 3] = "AREA";
            pworksheet.Cells[1, 4] = "MIN";
            pworksheet.Cells[1, 5] = "MAX";
            pworksheet.Cells[1, 6] = "RANGE";
            pworksheet.Cells[1, 7] = "MEAN";
            pworksheet.Cells[1, 8] = "STD";
            pworksheet.Cells[1, 9] = "SUM";
            pworksheet.Cells[1, 10] = "%_AREA";
            pworksheet.Cells[1, 11] = "%_AREA_ELV";
            pworksheet.Cells[1, 12] = "LABEL";

            //============================================
            // Populate Elevation and Percent Area Rows
            //============================================
            Uri analysisUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false));
            await QueuedTask.Run(() => {
                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(analysisUri)))
                using (Table statisticsTable = geodatabase.OpenDataset<Table>(strTable))
                {
                    long rasterValueCount = statisticsTable.GetCount();
                    long sumOfCount = 0;
                    using (RowCursor rowCursor = statisticsTable.Search(new QueryFilter(), false))
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (Row row = rowCursor.Current)
                            {
                                sumOfCount = sumOfCount + Convert.ToInt32(row["COUNT"]);
                            }
                        }
                    }

                    double percentArea = 0;
                    int i = 0;
                    using (RowCursor rowCursor = statisticsTable.Search(new QueryFilter(), false))
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (Row row = rowCursor.Current)
                            {
                                double count = Convert.ToDouble(row[Constants.FIELD_COUNT]);
                                percentArea = percentArea + (count / sumOfCount * 100);
                                // Populate Excel Table
                                double dblUpperBound = lstInterval[i].UpperBound;
                                if (!Module1.Current.Settings.m_demDisplayUnits.Equals(Module1.Current.Settings.m_demUnits))
                                {
                                    if (Module1.Current.Settings.m_demDisplayUnits.Equals("Meters") &&
                                        Module1.Current.Settings.m_demUnits.Equals("Feet"))
                                    {
                                        dblUpperBound = LinearUnit.Feet.ConvertTo(dblUpperBound, LinearUnit.Meters);
                                    }
                                    else if (Module1.Current.Settings.m_demDisplayUnits.Equals("Feet") &&
                                             Module1.Current.Settings.m_demUnits.Equals("Meters"))
                                    {
                                        dblUpperBound = LinearUnit.Meters.ConvertTo(dblUpperBound, LinearUnit.Feet);
                                    }
                                }
                                pworksheet.Cells[i + 2, 1] = dblUpperBound;
                                pworksheet.Cells[i + 2, 2] = row[Constants.FIELD_COUNT];
                                pworksheet.Cells[i + 2, 3] = row["AREA"];
                                pworksheet.Cells[i + 2, 4] = row["MIN"];
                                pworksheet.Cells[i + 2, 5] = row["MAX"];
                                pworksheet.Cells[i + 2, 6] = row["RANGE"];
                                pworksheet.Cells[i + 2, 7] = row["MEAN"];
                                pworksheet.Cells[i + 2, 8] = row["STD"];
                                pworksheet.Cells[i + 2, 9] = row["SUM"];
                                pworksheet.Cells[i + 2, 10] = count / sumOfCount * 100;
                                pworksheet.Cells[i + 2, 11] = percentArea;
                                pworksheet.Cells[i + 2, 12] = lstInterval[i].Name;
                                i++;
                            }
                        }
                    }
                }
            });

            success = BA_ReturnCode.Success;
            return success;
        }

        public static async Task<double> CreatePrecipitationTableAsync(Worksheet pworksheet, string precipPath,
            double aoiDemMin)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            double MaxPRISMValue = 0.0F;
            Uri uriElevZones = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false));
            IList<BA_Objects.Interval> lstInterval = await GeodatabaseTools.ReadReclassRasterAttribute(uriElevZones, Constants.FILE_ELEV_ZONE);

            //===========================
            //Zonal Statistics
            //===========================
            string strTable = "tblZones";
            // We assume elevation zones is the smaller cell size. Could not find api to set cell size to minimum of layers
            double dblCellSize = await GeodatabaseTools.GetCellSize(uriElevZones, Constants.FILE_ELEV_ZONE);
            IGPResult gpResult = await QueuedTask.Run(() =>
            {
                string sMask = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_RASTER;
                var environments = Geoprocessing.MakeEnvironmentArray(workspace: Module1.Current.Aoi.FilePath, snapRaster: Module1.Current.Aoi.SnapRasterPath,
                    mask: sMask, cellSize: dblCellSize);
                string strInZoneData = uriElevZones.LocalPath + "\\" + Constants.FILE_ELEV_ZONE;
                string strOutTable = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) + strTable;
                var parameters = Geoprocessing.MakeValueArray(strInZoneData, Constants.FIELD_VALUE, precipPath, strOutTable);
                return Geoprocessing.ExecuteToolAsync("ZonalStatisticsAsTable", parameters, environments,
                            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            });
            if (gpResult.IsFailed)
            {
                success = BA_ReturnCode.UnknownError;
            }
            else
            {
                success = BA_ReturnCode.Success;
            }

            //=============================================
            // Create Field Titles
            //=============================================
            pworksheet.Cells[1, 1] = "VALUE";
            pworksheet.Cells[1, 2] = "COUNT";
            pworksheet.Cells[1, 3] = "AREA";
            pworksheet.Cells[1, 4] = "MIN";
            pworksheet.Cells[1, 5] = "MAX";
            pworksheet.Cells[1, 6] = "RANGE";
            pworksheet.Cells[1, 7] = "MEAN";
            pworksheet.Cells[1, 8] = "STD";
            pworksheet.Cells[1, 9] = "SUM";
            pworksheet.Cells[1, 10] = "%_AREA";
            pworksheet.Cells[1, 11] = "Label";
            pworksheet.Cells[1, 12] = "AREA_DEM";
            pworksheet.Cells[1, 13] = "%_AREA_DEM";
            pworksheet.Cells[1, 14] = "VOL_ACRE_FT";
            pworksheet.Cells[1, 15] = "%_VOL";

            //============================================
            // Populate Elevation and Percent Area Rows
            //============================================
            Uri analysisUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false));
            await QueuedTask.Run(() => {
                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(analysisUri)))
                using (Table statisticsTable = geodatabase.OpenDataset<Table>(strTable))
                {
                    long rasterValueCount = statisticsTable.GetCount();
                    long sumOfCount = 0;
                    using (RowCursor rowCursor = statisticsTable.Search(new QueryFilter(), false))
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (Row row = rowCursor.Current)
                            {
                                sumOfCount = sumOfCount + Convert.ToInt32(row["COUNT"]);
                            }
                        }
                    }

                    double percentArea = 0.0F;
                    double TempMaxPRISMValue = 0.0F;
                    int i = 0;
                    using (RowCursor rowCursor = statisticsTable.Search(new QueryFilter(), false))
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (Row row = rowCursor.Current)
                            {
                                double count = Convert.ToDouble(row[Constants.FIELD_COUNT]);
                                percentArea = percentArea + (count / sumOfCount * 100);
                                // Populate Excel Table
                                double dblUpperBound = lstInterval[i].UpperBound;
                                if (!Module1.Current.Settings.m_demDisplayUnits.Equals(Module1.Current.Settings.m_demUnits))
                                {
                                    if (Module1.Current.Settings.m_demDisplayUnits.Equals("Meters") &&
                                        Module1.Current.Settings.m_demUnits.Equals("Feet"))
                                    {
                                        dblUpperBound = LinearUnit.Feet.ConvertTo(dblUpperBound, LinearUnit.Meters);
                                    }
                                    else if (Module1.Current.Settings.m_demDisplayUnits.Equals("Feet") &&
                                             Module1.Current.Settings.m_demUnits.Equals("Meters"))
                                    {
                                        dblUpperBound = LinearUnit.Meters.ConvertTo(dblUpperBound, LinearUnit.Feet);
                                    }
                                }
                                pworksheet.Cells[i + 3, 1] = dblUpperBound;     //@ToDo: Not sure about this?
                                pworksheet.Cells[i + 3, 2] = row[Constants.FIELD_COUNT];
                                pworksheet.Cells[i + 3, 3] = Math.Round(Convert.ToDouble(row["AREA"]), 0);
                                pworksheet.Cells[i + 3, 4] = row["MIN"];
                                pworksheet.Cells[i + 3, 5] = row["MAX"];
                                pworksheet.Cells[i + 3, 6] = row["RANGE"];
                                pworksheet.Cells[i + 3, 7] = row["MEAN"];
                                pworksheet.Cells[i + 3, 8] = row["STD"];
                                pworksheet.Cells[i + 3, 9] = row["SUM"];
                                pworksheet.Cells[i + 3, 10] = count / sumOfCount * 100;         //PERCENT_AREA
                                pworksheet.Cells[i + 3, 11] = lstInterval[i].Name;              //label

                                // Determine Max PRISM Value (for Charting Purposes)
                                TempMaxPRISMValue = (Convert.ToDouble(row[Constants.FIELD_COUNT]) / sumOfCount) * 100;
                                /// find the largest % value
                                if (TempMaxPRISMValue > MaxPRISMValue)
                                {
                                    MaxPRISMValue = TempMaxPRISMValue;
                                }

                                i++;
                            }
                        }
                    }
                    // Make First PRISM Interval the Min of AOI DEM
                    // aoiDemMin is always in meters
                    if (Module1.Current.Settings.m_demDisplayUnits.Equals("Feet"))
                    {
                        aoiDemMin = LinearUnit.Meters.ConvertTo(aoiDemMin, LinearUnit.Feet);
                    }
                    pworksheet.Cells[2, 1] = aoiDemMin; //Value
                    pworksheet.Cells[2, 10] = 0;         // PERCENT_AREA_ELEVATION

                    // ===============================================
                    // Set MaxPRISMValue to an Even Whole Number
                    // ===============================================
                    MaxPRISMValue = Math.Round(MaxPRISMValue * 1.05 + 0.5); // this number is to set the x axis of the chart
                    if (MaxPRISMValue%2 > 0)
                    {
                        MaxPRISMValue = MaxPRISMValue + 1;
                    }
 

                }
            });

             if (success != BA_ReturnCode.Success)
            {
                MaxPRISMValue = -1;
            }
            return MaxPRISMValue;
        }

        public static BA_ReturnCode CopyCells(Worksheet pSourceWS, int SourceCol, 
                                              Worksheet pTargetWS, int TargetCol)
        {
            long row_index = 3;
            Range pRange = pSourceWS.Cells[row_index, SourceCol];

            while (! string.IsNullOrEmpty(pRange.Text.ToString()))
            {
                Range targetRange = pTargetWS.Cells[row_index, TargetCol];
                targetRange.Value = pRange.Value;
                row_index = row_index + 1;
                pRange = pSourceWS.Cells[row_index, SourceCol];
            }
            return BA_ReturnCode.Success;
        }

        public static BA_ReturnCode EstimatePrecipitationVolume(Worksheet pPRSIMWS, int AreaCol, int PrecipCol, 
                                                                int VolumeCol, int PercentCol)
        {
            double conversionfactor;
            int i;

            long row_index = 3;
            Range pRange = pPRSIMWS.Cells[row_index, 1];

            conversionfactor = 1 / (4046.8564224 * 12); // convert sq meter-inch to acre-foot
                                                        // 1 square meter = 2.471053814671653e-4 acre
                                                        // 1 inch = 1/12 feet

            double total_vol = 0;
            int intCount = 0;
            while (!string.IsNullOrEmpty(pRange.Text.ToString()))
            {
                Range areaRange = pPRSIMWS.Cells[row_index, AreaCol];
                Range precipRange = pPRSIMWS.Cells[row_index, PrecipCol];
                Range volumeRange = pPRSIMWS.Cells[row_index, VolumeCol];
                volumeRange.Value = System.Convert.ToDouble(areaRange.Value) * Convert.ToDouble(precipRange.Value) * conversionfactor;
                total_vol = total_vol + System.Convert.ToDouble(volumeRange.Value);
                row_index = row_index + 1;
                pRange = pPRSIMWS.Cells[row_index, 1];
                intCount++;
            }

            // calculate % volume
            for (i = 1; i <= intCount; i++)
            {
                Range pctRange = pPRSIMWS.Cells[i + 2, PercentCol];
                Range volumeRange = pPRSIMWS.Cells[i + 2, VolumeCol];
                pctRange.Value = volumeRange.Value * 100 / total_vol;
            }
            return BA_ReturnCode.Success;
        }
    }
}
