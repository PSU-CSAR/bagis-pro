using ArcGIS.Core.Geometry;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bagis_pro.BA_Objects
{
    public class Aoi : INotifyPropertyChanged
    {
        string m_strName;
        string m_strFilePath;
        double m_dblMinElev = Constants.VALUE_NO_DATA_9999;
        double m_dblMaxElev = Constants.VALUE_NO_DATA_9999;
        LinearUnit m_elevationUnits;
        public bool HasSnowCourse;
        public bool HasSnotel;
        public bool HasSnolite;
        public bool HasCoopPillow;
        string m_aoiBatchState;
        bool m_aoiBatchIsSelected;
        string m_stationTriplet = "";
        string m_NwccName = "";
        string m_fileStationName = "";
        string m_stationNumber = "";
        string m_stationState = "";
        public string StationName = "";
        public int WinterStartMonth;
        public int WinterEndMonth;
        public string Huc;

        public Aoi()
        {

        }

        public Aoi(string strName, string strFilePath)
        {
            m_strName = strName;
            m_strFilePath = strFilePath;
            m_dblMinElev = Constants.VALUE_NO_DATA_9999;
            m_dblMaxElev = Constants.VALUE_NO_DATA_9999;
            m_elevationUnits = LinearUnit.Meters;
            m_aoiBatchState = AoiBatchState.Waiting.ToString();
            m_aoiBatchIsSelected = true;
        }

        public string Name
        {
            get { return m_strName; }
            set
            {
                m_strName = value;
            }
        }

        public string FilePath
        {
            get { return m_strFilePath; }
            set
            {
                m_strFilePath = value;
            }
        }

        public string ElevationUnits
        {
            set
            {
                if (value.Equals(Constants.UNITS_METERS))
                {
                    m_elevationUnits = LinearUnit.Meters;
                }
                else
                {
                    m_elevationUnits = LinearUnit.Feet;
                }
            }
        }

        public double MinElevMeters
        {
            get { return m_dblMinElev; }
            set
            {
                m_dblMinElev = value;
            }
        }

        public double MaxElevMeters
        {
            get { return m_dblMaxElev; }
            set
            {
                m_dblMaxElev = value;
            }
        }

        public static string SnapRasterPath(string aoiPath)
        {
            return GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Surfaces, true) 
                    + Constants.FILE_DEM_FILLED; 
        }

        public string AoiBatchStateText
        {
            get { return m_aoiBatchState; }
            set
            {
                if (value != this.m_aoiBatchState)
                {
                    m_aoiBatchState = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool AoiBatchIsSelected
        {
            get { return m_aoiBatchIsSelected; }
            set
            {
                if (value != this.m_aoiBatchIsSelected)
                {
                    this.m_aoiBatchIsSelected = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string StationTriplet
        {
            get { return m_stationTriplet; }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    m_stationTriplet = value;
                    // Example triplet: 09361500:CO:USGS
                    string[] strPieces = m_stationTriplet.Split(':');
                    if (strPieces.Length == 3)
                    {
                        m_stationNumber = strPieces[0];
                        m_stationState = strPieces[2];
                    }
                }
                else
                {
                    m_stationTriplet = "XXXXXXXX:XX:USGS";
                }
            }
        }

        public string NwccName
        {
            get { return m_NwccName; }
            set
            {
                m_NwccName = value;
                if (! string.IsNullOrEmpty(m_NwccName))
                {
                    m_fileStationName = m_NwccName.Replace(' ', '_');
                }                
            }
        }

        public string FileStationName
        {
            get { return m_fileStationName; }
        }

        public string StationNumber
        {
            get { return m_stationNumber; }
        }

        public string StationState
        {
            get { return m_stationState; }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        // This method is called by the Set accessor of each property.
        // The CallerMemberName attribute that is applied to the optional propertyName
        // parameter causes the property name of the caller to be substituted as an argument.
        private void NotifyPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

    }
}
