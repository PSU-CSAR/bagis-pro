using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bagis_pro.BA_Objects
{
    public class Analysis : IDisposable
    {
        public string AoiName;
        public string AoiFolderPath;
        public DateTime DateCreated;
        public double ShapeAreaKm;
        public string MinElevText;
        public string MaxElevText;
        public bool UseBufferDistance;
        public double BufferDistance;
        public string BufferUnitsText;
        public bool UseUpperRange;
        public double UpperRange;
        public string UpperRangeText;
        public bool UseLowerRange;
        public double LowerRange;
        public string LowerRangeText;
        public string ElevUnitsText;
        public string ReportTitle;
        public string Scenario1Title;
        public Site[] Scenario1Sites;
        public string Scenario2Title;
        public Site[] Scenario2Sites;
        public AreaStatistics AreaStatistics;
        public List<DataSource> DataSources;
        public short ElevationZonesInterval;
        public double PrecipZonesInterval;
        public string PrecipZonesBegin;
        public string PrecipZonesEnd;
        public double PrecipZonesMin;
        public double PrecipZonesMax;
        public double PrecipZonesIntervalCount;
        public List<string> PrecipZonesIntervals;
        public string DemDisplayUnits;
        public List<string> ElevZonesIntervals;
        public List<double> ElevZonesPctArea;
        public List<int> ElevZonesSnotelCount;
        public List<int> ElevZonesSnowCourseCount;
        public int ElevZonesCount;
        public int ElevSubdivisionCount;
        public bool SubrangeEnabled;
        public double SubrangeElevMin;
        public double SubrangeElevMax;
        public int AspectDirectionsCount;
        public double PrecipVolumeKaf = -1;
        public string WinterStartMonth;
        public string WinterEndMonth;
        public double SeasonalPrecipMax = 100;
        public double SeasonalPrecipMin = 0;
        public DateTime DateBagisSettingsConverted;
        public string DateCreatedText
        {
            get
            {
                TimeZoneInfo zone = System.TimeZoneInfo.Local;
                string strDate = DateCreated.ToString("d-MMM-yyyy h:m tt ");
                return strDate + zone.DisplayName;
            }
            set
            {
                // Do nothing; This is only for XML serialization
            }
        }

        public string ShapeAreaAcresText
        {
            get
            {
                double bufferAcres = AreaUnit.SquareKilometers.ConvertTo(ShapeAreaKm, AreaUnit.Acres);
                return Math.Round(bufferAcres, 2).ToString("#0.00") + " Acres";
            }
            set
            {
                // Do nothing; This is only for XML serialization
            }
        }

        public string ShapeAreaKmText
        {
            get
            {
                return Math.Round(ShapeAreaKm, 3).ToString("#0.000") + " " + Constants.UNITS_SQUARE_KM;
            }
            set
            {
                // Do nothing; This is only for XML serialization
            }
        }

        public string ShapeAreaHectaresText
        {
            get
            {
                double bufferHectares = AreaUnit.SquareKilometers.ConvertTo(ShapeAreaKm, AreaUnit.Hectares);
                return Math.Round(bufferHectares, 2).ToString("#0.00") + " Hectares";
            }
            set
            {
                // Do nothing; This is only for XML serialization
            }
        }

        public string ShapeAreaMiText
        {
            get
            {
                double bufferMiles = AreaUnit.SquareKilometers.ConvertTo(ShapeAreaKm, AreaUnit.SquareMiles);
                return Math.Round(bufferMiles, 2).ToString("#0.000") + " Square Miles";
            }
            set
            {
                // Do nothing; This is only for XML serialization
            }
        }

        public string BufferDistanceText
        {
            get
            {
                return Math.Round(BufferDistance, 2).ToString("#0.00");
            }
            set
            {
                // Do nothing; This is only for XML serialization
            }
        }

        // Flag: Has Dispose already been called?
        bool disposed = false;
        // Instantiate a SafeHandle instance.
        System.Runtime.InteropServices.SafeHandle handle = 
            new Microsoft.Win32.SafeHandles.SafeFileHandle(IntPtr.Zero, true);

        // Public implementation of Dispose pattern callable by consumers.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                handle.Dispose();
                // Free any other managed objects here.
                //
            }

            disposed = true;
        }
    }

    public class AreaStatistics
    {
        public double S1RepArea;
        public string S1AoiPctRep;
        public string S1RepAreaSqMi;
        public string S1RepAreaAcres;
        public string S1RepAreaSqKm;
        public string S1RepAreaHect;
        public double S1NonRepArea;
        public string S1AoiPctNonRep;
        public string S1NonRepAreaSqMi;
        public string S1NonRepAreaAcres;
        public string S1NonRepAreaSqKm;
        public string S1NonRepAreaHect;
        public double S2RepArea;
        public string S2AoiPctRep;
        public string S2RepAreaSqMi;
        public string S2RepAreaAcres;
        public string S2RepAreaSqKm;
        public string S2RepAreaHect;
        public double S2NonRepArea;
        public string S2AoiPctNonRep;
        public string S2NonRepAreaSqMi;
        public string S2NonRepAreaAcres;
        public string S2NonRepAreaSqKm;
        public string S2NonRepAreaHect;
        public double MapNotRep;
        public double MapRepS1Only;
        public double MapRepS2Only;
        public double MapRepBothScen;
        public string MapAoiPctNotRep;
        public string MapAoiPctRepS1Only;
        public string MapAoiPctRepS2Only;
        public string MapAoiPctRepBothScen;
        //@ToDo: Sync the properties of this class with BAGIS v3 before deployment
    }
}
