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
                return Math.Round(ShapeAreaKm, 3).ToString("#0.000") + " Square KM";
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
        //@ToDo: Add other properties to this class if we migrate it to BAGIS-PRO
    }
}
