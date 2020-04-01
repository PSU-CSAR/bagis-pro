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
                return Geoprocessing.ExecuteToolAsync("ZonalStatisticsAsTable", parameters, null,
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
                            pworksheet.Cells[i + 3, 3] = row["AREA"];
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
                return Geoprocessing.ExecuteToolAsync("ZonalStatisticsAsTable", parameters, null,
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
    }
}
