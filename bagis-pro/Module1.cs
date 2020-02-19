using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.Threading.Tasks;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace bagis_pro
{
    internal class Module1 : Module
    {
        private static Module1 _this = null;

        /// <summary>
        /// Retrieve the singleton instance to this module here
        /// </summary>
        public static Module1 Current
        {
            get
            {
                return _this ?? (_this = (Module1)FrameworkApplication.FindModule("Module1"));
            }
        }

        #region Overrides
        /// <summary>
        /// Called by Framework when ArcGIS Pro is closing
        /// </summary>
        /// <returns>False to prevent Pro from closing, otherwise True</returns>
        protected override bool CanUnload()
        {
            //TODO - add your business logic
            //return false to ~cancel~ Application close
            return true;
        }

        #endregion Overrides

        #region Toggle State
        /// <summary>
        /// Activate or Deactivate the specified state. State is identified via
        /// its name. Listen for state changes via the DAML <b>condition</b> attribute
        /// </summary>
        /// <param name="stateID"></param>
        public static void ToggleState(string stateID)
        {
            if (FrameworkApplication.State.Contains(stateID))
            {
                FrameworkApplication.State.Deactivate(stateID);
            }
            else
            {
                FrameworkApplication.State.Activate(stateID);
            }
        }
        #endregion Toggle State

        /// <summary>
        /// Activate the specified state if it is inactive. State is identified via
        /// its name. Listen for state changes via the DAML <b>condition</b> attribute
        /// </summary>
        /// <param name="stateID"></param>
        public static void ActivateState(string stateID)
        {
            if (!FrameworkApplication.State.Contains(stateID))
            {
                FrameworkApplication.State.Activate(stateID);
            }
        }

        /// <summary>
        /// Dectivate the specified state if it is active. State is identified via
        /// its name. Listen for state changes via the DAML <b>condition</b> attribute
        /// </summary>
        /// <param name="stateID"></param>
        public static void DeactivateState(string stateID)
        {
            if (FrameworkApplication.State.Contains(stateID))
            {
                FrameworkApplication.State.Deactivate(stateID);
            }
        }

        internal BA_Objects.Aoi Aoi { get; set; } = new BA_Objects.Aoi();
        internal BA_Objects.Settings Settings { get; set; } = new BA_Objects.Settings();
        internal bool AoiHasSnotel { get; set; } = false;
        internal bool AoiHasSnowCourse { get; set; } = false;
        internal bool MapDisplayElevationInMeters { get; } = false;
        internal string DisplayedMap { get; set; } = "";
        internal bool MapFinishedLoading { get; set; } = false;
        internal Buttons.CboCurrentAoi CboCurrentAoi { get; set; }
}
}
