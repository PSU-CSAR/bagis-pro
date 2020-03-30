using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
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
            //Dim ZRasterName As String, VRasterName As String
            //Dim ZInputPath As String, VInputPath As String
            //Dim i As Integer
            //Dim IntervalList() As BA_IntervalList = Nothing

            //ZRasterName = BA_EnumDescription(MapsFileName.ElevationZone)
            //ZInputPath = BA_GeodatabasePath(aoiPath, GeodatabaseNames.Analysis)
            //'read class definition for chart and table labelling
            //Dim success As BA_ReturnCode = BA_ReadReclassRasterAttributeGDB(IntervalList, ZInputPath, ZRasterName)
            //VRasterName = BA_EnumDescription(MapsFileName.filled_dem_gdb)
            //VInputPath = BA_GeodatabasePath(aoiPath, GeodatabaseNames.Surfaces)

            //Dim pZoneRaster As IGeoDataset = BA_OpenRasterFromGDB(ZInputPath, ZRasterName)
            //Dim pValueRaster As IGeoDataset = BA_OpenRasterFromGDB(VInputPath, VRasterName)

            //===========================
            //Zonal Statistics
            //===========================

            string strTable = "tblZones";
            IGPResult gpResult = await QueuedTask.Run(() =>
            {
                string sMask = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_VECTOR;
                var environments = Geoprocessing.MakeEnvironmentArray(workspace: Module1.Current.Aoi.FilePath, snapRaster: Module1.Current.Aoi.SnapRasterPath,
                    mask: sMask);
                string strInZoneData = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) + Constants.FILE_ELEV_ZONE;
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

            //Create Field Indexes
            //Dim MinIndex As Integer, MaxIndex As Integer, MeanIndex As Integer
            //Dim RangeIndex As Integer, SumIndex As Integer, STDIndex As Integer
            //Dim AreaIndex As Integer, CountIndex As Integer, ValueIndex As Integer

            //MinIndex = ZonalTable.FindField("MIN")
            //MaxIndex = ZonalTable.FindField("MAX")
            //MeanIndex = ZonalTable.FindField("MEAN")
            //RangeIndex = ZonalTable.FindField("RANGE")
            //SumIndex = ZonalTable.FindField("SUM")
            //STDIndex = ZonalTable.FindField("STD")
            //AreaIndex = ZonalTable.FindField("AREA")
            //CountIndex = ZonalTable.FindField("COUNT")
            //ValueIndex = ZonalTable.FindField("VALUE")

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
                int i = 2;
                using (RowCursor rowCursor = statisticsTable.Search(new QueryFilter(), false))
                {
                    while (rowCursor.MoveNext())
                    {
                        using (Row row = rowCursor.Current)
                        {
                            percentArea = percentArea + (Convert.ToInt32(row["COUNT"]) / sumOfCount * 100);
                            // Populate Excel Table
                            //    pworksheet.Cells(i + 3, 1) = IntervalList(pZonalRow.Value(ValueIndex)).UpperBound* conversionFactor      'Value
                            pworksheet.Cells[i + 3, 2] = row["COUNT"];
                            pworksheet.Cells[i + 3, 3] = row["AREA"];
                            pworksheet.Cells[i + 3, 4] = row["MIN"];
                            pworksheet.Cells[i + 3, 5] = row["MAX"];
                            pworksheet.Cells[i + 3, 6] = row["RANGE"];
                            pworksheet.Cells[i + 3, 7] = row["MEAN"];
                            pworksheet.Cells[i + 3, 8] = row["STD"];
                            pworksheet.Cells[i + 3, 9] = row["SUM"];
                            pworksheet.Cells[i + 3, 10] = (Convert.ToInt32(row["COUNT"]) / sumOfCount * 100);
                            pworksheet.Cells[i + 3, 11] = percentArea;
                            //    pworksheet.Cells(i + 3, 12) = IntervalList(pZonalRow.Value(ValueIndex)).Name 
                            i++;
                        }
                    }
                }
            }
            });
            //Dim pZonalRow As IRow
            //Dim PercentArea As Double
            //PercentArea = 0
            //For i = 0 To RasterValueCount - 1
            //    'Target Rows
            //    pZonalRow = ZonalTable.GetRow(i + 1)
            //    'Determine PercentArea
            //    PercentArea = PercentArea + ((pZonalRow.Value(CountIndex) / SumOfCount) * 100)
            //    'Populate Excel Table
            //    'Debug.Print(pZonalRow.Value(ValueIndex))
            //    'Debug.Print(IntervalList(pZonalRow.Value(ValueIndex)).UpperBound)
            //    pworksheet.Cells(i + 3, 1) = IntervalList(pZonalRow.Value(ValueIndex)).UpperBound* conversionFactor      'Value
            //    pworksheet.Cells(i + 3, 2) = pZonalRow.Value(CountIndex)                                'Count
            //    pworksheet.Cells(i + 3, 3) = pZonalRow.Value(AreaIndex)                                 'Area
            //    pworksheet.Cells(i + 3, 4) = pZonalRow.Value(MinIndex)                                  'MIN
            //    pworksheet.Cells(i + 3, 5) = pZonalRow.Value(MaxIndex)                                  'MAX
            //    pworksheet.Cells(i + 3, 6) = pZonalRow.Value(RangeIndex)                                'Range
            //    pworksheet.Cells(i + 3, 7) = pZonalRow.Value(MeanIndex)                                 'Mean
            //    pworksheet.Cells(i + 3, 8) = pZonalRow.Value(STDIndex)                                  'STD
            //    pworksheet.Cells(i + 3, 9) = pZonalRow.Value(SumIndex)                                  'Sum
            //    pworksheet.Cells(i + 3, 10) = (pZonalRow.Value(CountIndex) / SumOfCount) * 100          'PERCENT_AREA
            //    pworksheet.Cells(i + 3, 11) = PercentArea                                               'PERCENT_AREA_ELEVATION
            //    pworksheet.Cells(i + 3, 12) = IntervalList(pZonalRow.Value(ValueIndex)).Name            'Label
            //Next

            //'AOI_DEMMin is always in meters
            //'Make First Elevation Interval the Min of AOI DEM
            //pworksheet.Cells(2, 1) = aoiDemMin* BA_SetConversionFactor(optZMetersChecked, True)   'Value
            //pworksheet.Cells(2, 11) = 0 'PERCENT_AREA_ELEVATION

            //pZonalRow = Nothing
            //ZonalTable = Nothing

            return success;
        }
    }
}
