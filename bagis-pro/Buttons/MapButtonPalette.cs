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

            await MapTools.UpdateMapElementsAsync(Module1.Current.Aoi.Name.ToUpper(), thisMap);
            Layout oLayout = await MapTools.GetDefaultLayoutAsync(Constants.MAPS_DEFAULT_LAYOUT_NAME);
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

    internal class MapButtonPalette_BtnRoads : Button
    {
        protected override async void OnClick()
        {
            try
            {
                await ToggleMapDisplay.ToggleAsync(BagisMapType.ROADS);
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display roads map!!" + e.Message, "BAGIS-PRO");
            }
        }
    }

    internal class MapButtonPalette_BtnPublicLandZones : Button
    {
        protected override async void OnClick()
        {
            try
            {
                await ToggleMapDisplay.ToggleAsync(BagisMapType.PUBLIC_LAND_ZONES);
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display public land zones map!!" + e.Message, "BAGIS-PRO");
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

    internal class MapButtonPalette_BtnBelowTreeline : Button
    {
        protected override async void OnClick()
        {
            try
            {
                await ToggleMapDisplay.ToggleAsync(BagisMapType.BELOW_TREELINE);
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display area below treeline map!!" + e.Message, "BAGIS-PRO");
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

    internal class MapButtonPalette_BtnSweJan : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                int idxJan = 0;
                await MapTools.LoadSweMapAsync(Constants.FILES_SNODAS_SWE[idxJan],
                    Constants.LAYER_NAMES_SNODAS_SWE[idxJan], Constants.MAP_TITLES_SNODAS_SWE[idxJan],
                    Constants.FILE_EXPORT_MAPS_SWE[idxJan]);
                Module1.Current.MapFinishedLoading = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display SNODAS SWE map!!" + e.Message, "BAGIS-PRO");
                Module1.Current.ModuleLogManager.LogError(nameof(MapButtonPalette_BtnSweJan),
                    "Exception: " + e.Message);
            }
        }
    }

    internal class MapButtonPalette_BtnSweFeb : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                int idxFeb = 1;
                await MapTools.LoadSweMapAsync(Constants.FILES_SNODAS_SWE[idxFeb],
                    Constants.LAYER_NAMES_SNODAS_SWE[idxFeb], Constants.MAP_TITLES_SNODAS_SWE[idxFeb],
                    Constants.FILE_EXPORT_MAPS_SWE[idxFeb]);
                Module1.Current.MapFinishedLoading = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display SNODAS SWE map!!" + e.Message, "BAGIS-PRO");
                Module1.Current.ModuleLogManager.LogError(nameof(MapButtonPalette_BtnSweFeb),
                    "Exception: " + e.Message);
            }
        }
    }

    internal class MapButtonPalette_BtnSweMar : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                int idxMar = 2;
                await MapTools.LoadSweMapAsync(Constants.FILES_SNODAS_SWE[idxMar],
                    Constants.LAYER_NAMES_SNODAS_SWE[idxMar], Constants.MAP_TITLES_SNODAS_SWE[idxMar],
                    Constants.FILE_EXPORT_MAPS_SWE[idxMar]);
                Module1.Current.MapFinishedLoading = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display SNODAS SWE map!!" + e.Message, "BAGIS-PRO");
                Module1.Current.ModuleLogManager.LogError(nameof(MapButtonPalette_BtnSweMar),
                    "Exception: " + e.Message);
            }
        }
    }

    internal class MapButtonPalette_BtnSweApr : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                int idxApr = 3;
                await MapTools.LoadSweMapAsync(Constants.FILES_SNODAS_SWE[idxApr],
                    Constants.LAYER_NAMES_SNODAS_SWE[idxApr], Constants.MAP_TITLES_SNODAS_SWE[idxApr],
                    Constants.FILE_EXPORT_MAPS_SWE[idxApr]);
                Module1.Current.MapFinishedLoading = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display SNODAS SWE map!!" + e.Message, "BAGIS-PRO");
                Module1.Current.ModuleLogManager.LogError(nameof(MapButtonPalette_BtnSweApr),
                    "Exception: " + e.Message);

            }
        }
    }

    internal class MapButtonPalette_BtnSweMay : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                int idxMay = 4;
                await MapTools.LoadSweMapAsync(Constants.FILES_SNODAS_SWE[idxMay],
                    Constants.LAYER_NAMES_SNODAS_SWE[idxMay], Constants.MAP_TITLES_SNODAS_SWE[idxMay],
                    Constants.FILE_EXPORT_MAPS_SWE[idxMay]);

                Module1.Current.MapFinishedLoading = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display SNODAS SWE map!!" + e.Message, "BAGIS-PRO");
                Module1.Current.ModuleLogManager.LogError(nameof(MapButtonPalette_BtnSweMay),
                    "Exception: " + e.Message);
            }
        }
    }

    internal class MapButtonPalette_BtnSweJun : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                int idxJun = 5;
                await MapTools.LoadSweMapAsync(Constants.FILES_SNODAS_SWE[idxJun],
                    Constants.LAYER_NAMES_SNODAS_SWE[idxJun], Constants.MAP_TITLES_SNODAS_SWE[idxJun],
                    Constants.FILE_EXPORT_MAPS_SWE[idxJun]);

                Module1.Current.MapFinishedLoading = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display SNODAS SWE map!!" + e.Message, "BAGIS-PRO");
                Module1.Current.ModuleLogManager.LogError(nameof(MapButtonPalette_BtnSweJun),
                    "Exception: " + e.Message);
            }
        }
    }

    internal class MapButtonPalette_BtnSweDec : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                int idxDec = 6;
                await MapTools.LoadSweMapAsync(Constants.FILES_SNODAS_SWE[idxDec],
                    Constants.LAYER_NAMES_SNODAS_SWE[idxDec], Constants.MAP_TITLES_SNODAS_SWE[idxDec],
                    Constants.FILE_EXPORT_MAPS_SWE[idxDec]);

                Module1.Current.MapFinishedLoading = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display SNODAS SWE map!!" + e.Message, "BAGIS-PRO");
                Module1.Current.ModuleLogManager.LogError(nameof(MapButtonPalette_BtnSweDec),
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

}
