﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using bagis_pro.BA_Objects;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace bagis_pro.Menus
{
    internal class MnuMaps_BtnMapLoad : Button
    {
        protected async override void OnClick()
        {
            string tempAoiPath = "C:\\Docs\\animas_AOI_prms";
            try
            {
                Layout layout = await MapTools.GetDefaultLayoutAsync(Constants.MAPS_DEFAULT_LAYOUT_NAME);

                BA_ReturnCode success = await MapTools.DisplayMaps(tempAoiPath, layout, true);

                if (success == BA_ReturnCode.Success && layout != null)
                {
                    bool bFoundIt = false;
                    //A layout view may exist but it may not be active
                    //Iterate through each pane in the application and check to see if the layout is already open and if so, activate it
                    foreach (var pane in FrameworkApplication.Panes)
                    {
                        if (!(pane is ILayoutPane layoutPane))  //if not a layout view, continue to the next pane    
                            continue;
                        if (layoutPane.LayoutView != null &&
                            layoutPane.LayoutView.Layout == layout) //if there is a match, activate the view  
                        {
                            (layoutPane as Pane).Activate();
                            bFoundIt = true;
                        }
                        else
                        {
                            continue;
                        }
                    }
                    if (!bFoundIt)
                    {
                        ILayoutPane iNewLayoutPane = await FrameworkApplication.Panes.CreateLayoutPaneAsync(layout); //GUI thread
                    }
                }

                // Legend
                success = await MapTools.DisplayLegendAsync(Constants.MAPS_DEFAULT_MAP_FRAME_NAME, layout, "ArcGIS Colors", 
                    "1.5 Point", true);

                // update map elements for default map (elevation)
                if (FrameworkApplication.State.Contains("MapButtonPalette_BtnElevation_State"))
                {
                    BA_Objects.MapDefinition defaultMap = MapTools.LoadMapDefinition(BagisMapType.ELEVATION);
                    success = await MapTools.UpdateMapElementsAsync(Module1.Current.Aoi.StationName, Constants.MAPS_DEFAULT_LAYOUT_NAME, defaultMap);
                    success = await MapTools.UpdateLegendAsync(layout, defaultMap.LegendLayerList);
                }
                else
                {
                    MessageBox.Show("The default Elevation Zones map could not be loaded. Use " +
                                    "the Display Maps buttons to display other maps!!", "BAGIS-PRO");
                }
                Module1.Current.DisplayedMap = Constants.FILE_EXPORT_MAP_ELEV_PDF;
                Module1.ActivateState("BtnMapLoad_State");
                MessageBox.Show("The maps are loaded. Use the Toggle Maps buttons to view the maps.", "BAGIS-PRO");
            }
            catch (Exception e)
            {
                MessageBox.Show("An error occurred while trying to load the maps!! " + e.Message, "BAGIS PRO");
            }
        }
    }

}
