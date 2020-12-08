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
        string m_aoiBatchState;

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
        }

        public string Name
        {
            get { return m_strName; }
            set
            {
                m_strName = value;
                NotifyPropertyChanged();
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
                m_aoiBatchState = value;
                NotifyPropertyChanged();
            }
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
