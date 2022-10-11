using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;


namespace bagis_pro.Buttons
{
    internal class ToggleMapDisplay
    {
        public static async Task ToggleAsync(BagisMapType mapType)
        {
            //Get map definition
            BA_Objects.MapDefinition thisMap = MapTools.LoadMapDefinition(mapType);
            Layout oLayout = await MapTools.GetDefaultLayoutAsync(Constants.MAPS_DEFAULT_LAYOUT_NAME);
            if (oLayout != null)
            {
                foreach (var pane in FrameworkApplication.Panes)
                {
                    if (!(pane is ILayoutPane layoutPane))  //if not a layout view, continue to the next pane    
                        continue;
                    if (layoutPane.LayoutView.Layout == oLayout) //if there is a match, activate the view  
                    {
                        (layoutPane as Pane).Activate();
                    }
                }
            }

            // toggle layers according to map definition
            Module1.Current.MapFinishedLoading = false;
            var allLayers = MapView.Active.Map.Layers.ToList();
            await QueuedTask.Run(() =>
            {
                foreach (var layer in allLayers)
                {
                    if (thisMap.LayerList.Contains(layer.Name))
                    {
                        layer.SetVisibility(true);
                    }
                    else
                    {
                        layer.SetVisibility(false);
                    }
                }
            });

            await MapTools.UpdateMapElementsAsync(Module1.Current.Aoi.NwccName.ToUpper(), thisMap);
            BA_ReturnCode success = await MapTools.UpdateLegendAsync(oLayout, thisMap.LegendLayerList);
            Module1.Current.MapFinishedLoading = true;
            Module1.Current.DisplayedMap = thisMap.PdfFileName;
        }
    }

    internal class MapButtonPalette_BtnElevation : Button
    {
        protected override async void OnClick()
        {
            try
            {               
                await ToggleMapDisplay.ToggleAsync(BagisMapType.ELEVATION);
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display elevation map!!" + e.Message, "BAGIS-PRO");
            }
        }
    }

    internal class MapButtonPalette_BtnSlope : Button
    {
        protected override async void OnClick()
        {
            try
            {
                await ToggleMapDisplay.ToggleAsync(BagisMapType.SLOPE);
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display slope map!!" + e.Message, "BAGIS-PRO");
            }
        }
    }

    internal class MapButtonPalette_BtnAspect : Button
    {
        protected override async void OnClick()
        {
            try
            {
                await ToggleMapDisplay.ToggleAsync(BagisMapType.ASPECT);
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display aspect map!!" + e.Message, "BAGIS-PRO");
            }
        }
    }

    internal class MapButtonPalette_BtnSnotel : Button
    {
        protected override async void OnClick()
        {
            try
            {
                await ToggleMapDisplay.ToggleAsync(BagisMapType.SNOTEL);
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display snotel map!!" + e.Message, "BAGIS-PRO");
            }
        }
    }

    internal class MapButtonPalette_BtnSnowCourse : Button
    {
        protected override async void OnClick()
        {
            try
            {
                await ToggleMapDisplay.ToggleAsync(BagisMapType.SCOS);
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display snow course map!!" + e.Message, "BAGIS-PRO");
            }
        }
    }

    internal class MapButtonPalette_BtnSitesAll : Button
    {
        protected override async void OnClick()
        {
            try
            {
                await ToggleMapDisplay.ToggleAsync(BagisMapType.SITES_ALL);
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display all sites map!!" + e.Message, "BAGIS-PRO");
            }
        }
    }

    internal class MapButtonPalette_BtnPublicLandZones : Button
    {
        protected override async void OnClick()
        {
            try
            {
                await ToggleMapDisplay.ToggleAsync(BagisMapType.LAND_ZONES);
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display land zones map!!" + e.Message, "BAGIS-PRO");
            }
        }
    }

    internal class MapButtonPalette_BtnLandOwnership : Button
    {
        protected override async void OnClick()
        {
            try
            {
                await ToggleMapDisplay.ToggleAsync(BagisMapType.LAND_OWNERSHIP);
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display land ownership map!!" + e.Message, "BAGIS-PRO");
            }
        }
    }

    internal class MapButtonPalette_BtnForestedArea : Button
    {
        protected override async void OnClick()
        {
            try
            {
                await ToggleMapDisplay.ToggleAsync(BagisMapType.FORESTED_AREA);
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display forested area map!!" + e.Message, "BAGIS-PRO");
            }
        }
    }

    internal class MapButtonPalette_BtnSitesLocationZone : Button
    {
        protected override async void OnClick()
        {
            try
            {
                await ToggleMapDisplay.ToggleAsync(BagisMapType.SITES_LOCATION);
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display potential site location map!!" + e.Message, "BAGIS-PRO");
            }
        }
    }

    internal class MapButtonPalette_BtnSitesLocationPrecip : Button
    {
        protected override async void OnClick()
        {
            try
            {
                await ToggleMapDisplay.ToggleAsync(BagisMapType.SITES_LOCATION_PRECIP);
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display potential site location map!!" + e.Message, "BAGIS-PRO");
            }
        }
    }

    internal class MapButtonPalette_BtnSitesLocationPrecipContrib : Button
    {
        protected override async void OnClick()
        {
            try
            {
                await ToggleMapDisplay.ToggleAsync(BagisMapType.SITES_LOCATION_PRECIP_CONTRIB);
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display potential site location map!!" + e.Message, "BAGIS-PRO");
            }
        }
    }

    internal class MapButtonPalette_BtnCriticalPrecipZone : Button
    {
        protected override async void OnClick()
        {
            try
            {
                await ToggleMapDisplay.ToggleAsync(BagisMapType.CRITICAL_PRECIP);
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display critical precipitation zone map!!" + e.Message, "BAGIS-PRO");
            }
        }
    }

    internal class MapButtonPalette_BtnSwe : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                Layout oLayout = await MapTools.GetDefaultLayoutAsync(Constants.MAPS_SNODAS_LAYOUT);
                if (oLayout != null)
                {
                    foreach (var pane in FrameworkApplication.Panes)
                    {
                        if (!(pane is ILayoutPane layoutPane))  //if not a layout view, continue to the next pane    
                            continue;
                        if (layoutPane.LayoutView.Layout == oLayout) //if there is a match, activate the view  
                        {
                            (layoutPane as Pane).Activate();
                        }
                    }
                }
                Module1.Current.MapFinishedLoading = true;
                Module1.Current.DisplayedMap = Constants.FILE_EXPORT_SNODAS_SWE_PDF;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display SNODAS SWE map!!" + e.Message, "BAGIS-PRO");
                Module1.Current.ModuleLogManager.LogError(nameof(MapButtonPalette_BtnSwe),
                    "Exception: " + e.Message);
            }
        }
    }

    internal class MapButtonPalette_BtnSweDelta : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                Layout oLayout = await MapTools.GetDefaultLayoutAsync(Constants.MAPS_SNODAS_DELTA_LAYOUT);
                if (oLayout != null)
                {
                    foreach (var pane in FrameworkApplication.Panes)
                    {
                        if (!(pane is ILayoutPane layoutPane))  //if not a layout view, continue to the next pane    
                            continue;
                        if (layoutPane.LayoutView.Layout == oLayout) //if there is a match, activate the view  
                        {
                            (layoutPane as Pane).Activate();
                        }
                    }
                }
                Module1.Current.MapFinishedLoading = true;
                Module1.Current.DisplayedMap = Constants.FILE_EXPORT_SNODAS_SWE_DELTA_PDF;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display SWE Delta map!!" + e.Message, "BAGIS-PRO");
                Module1.Current.ModuleLogManager.LogError(nameof(MapButtonPalette_BtnSweDelta),
                    "Exception: " + e.Message);
            }
        }
    }

    internal class MapButtonPalette_BtnPrism : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                await ToggleMapDisplay.ToggleAsync(BagisMapType.PRISM);
                Module1.Current.MapFinishedLoading = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display PRISM map!!" + e.Message, "BAGIS-PRO");
            }
        }
    }

    internal class MapButtonPalette_BtnPrecipContrib : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                await ToggleMapDisplay.ToggleAsync(BagisMapType.PRECIPITATION_CONTRIBUTION);
                Module1.Current.MapFinishedLoading = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display Precipitation Contribution map!!" + e.Message, "BAGIS-PRO");
            }
        }
    }

    internal class MapButtonPalette_BtnWinterPrecipitation : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                await ToggleMapDisplay.ToggleAsync(BagisMapType.WINTER_PRECIPITATION);
                Module1.Current.MapFinishedLoading = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display Winter Precipitation map!!" + e.Message, "BAGIS-PRO");
            }
        }
    }

    internal class MapButtonPalette_BtnSeasonalPrecipContrib : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                Layout oLayout = await MapTools.GetDefaultLayoutAsync(Constants.MAPS_SEASONAL_PRECIP_LAYOUT);
                if (oLayout != null)
                {
                    foreach (var pane in FrameworkApplication.Panes)
                    {
                        if (!(pane is ILayoutPane layoutPane))  //if not a layout view, continue to the next pane    
                            continue;
                        if (layoutPane.LayoutView.Layout == oLayout) //if there is a match, activate the view  
                        {
                            (layoutPane as Pane).Activate();
                        }
                    }
                }

                Module1.Current.MapFinishedLoading = true;
                Module1.Current.DisplayedMap = Constants.FILE_EXPORT_SEASONAL_PRECIP_DISTRIBUTION_PDF;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display Seasonal Precipitation map!!" + e.Message, "BAGIS-PRO");
            }
        }
    }

    internal class MapButtonPalette_BtnAoiLocation : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                Layout oLayout = await MapTools.GetDefaultLayoutAsync(Constants.MAPS_AOI_LOCATION_LAYOUT);
                if (oLayout != null)
                {
                    foreach (var pane in FrameworkApplication.Panes)
                    {
                        if (!(pane is ILayoutPane layoutPane))  //if not a layout view, continue to the next pane    
                            continue;
                        if (layoutPane.LayoutView.Layout == oLayout) //if there is a match, activate the view  
                        {
                            (layoutPane as Pane).Activate();
                        }
                    }
                }
                Module1.Current.MapFinishedLoading = true;
                Module1.Current.DisplayedMap = Constants.FILE_EXPORT_MAP_AOI_LOCATION_PDF;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display AOI location map!!" + e.Message, "BAGIS-PRO");
            }
        }
    }

    internal class MapButtonPalette_BtnLandCover : Button
    {
        protected override async void OnClick()
        {
            try
            {
                await ToggleMapDisplay.ToggleAsync(BagisMapType.LAND_COVER);
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display land cover map!!" + e.Message, "BAGIS-PRO");
            }
        }
    }

}
