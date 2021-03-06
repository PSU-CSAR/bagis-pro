﻿using System;
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

            await MapTools.UpdateMapElementsAsync(Module1.Current.Aoi.NwccName.ToUpper(), thisMap);
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

    internal class MapButtonPalette_BtnPublicLandOwnership : Button
    {
        protected override async void OnClick()
        {
            try
            {
                await ToggleMapDisplay.ToggleAsync(BagisMapType.PUBLIC_LAND_OWNERSHIP);
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
                int idxJan = 2;
                await MapTools.LoadSweMapAsync(Constants.FILES_SNODAS_SWE[idxJan],
                    Constants.LAYER_NAMES_SNODAS_SWE[idxJan], Constants.MAP_TITLES_SNODAS_SWE[idxJan],
                    Constants.FILE_EXPORT_MAPS_SWE[idxJan], false);
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
                int idxFeb = 3;
                await MapTools.LoadSweMapAsync(Constants.FILES_SNODAS_SWE[idxFeb],
                    Constants.LAYER_NAMES_SNODAS_SWE[idxFeb], Constants.MAP_TITLES_SNODAS_SWE[idxFeb],
                    Constants.FILE_EXPORT_MAPS_SWE[idxFeb], false);
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
                int idxMar = 4;
                await MapTools.LoadSweMapAsync(Constants.FILES_SNODAS_SWE[idxMar],
                    Constants.LAYER_NAMES_SNODAS_SWE[idxMar], Constants.MAP_TITLES_SNODAS_SWE[idxMar],
                    Constants.FILE_EXPORT_MAPS_SWE[idxMar], false);
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
                int idxApr = 5;
                await MapTools.LoadSweMapAsync(Constants.FILES_SNODAS_SWE[idxApr],
                    Constants.LAYER_NAMES_SNODAS_SWE[idxApr], Constants.MAP_TITLES_SNODAS_SWE[idxApr],
                    Constants.FILE_EXPORT_MAPS_SWE[idxApr], false);
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
                int idxMay = 6;
                await MapTools.LoadSweMapAsync(Constants.FILES_SNODAS_SWE[idxMay],
                    Constants.LAYER_NAMES_SNODAS_SWE[idxMay], Constants.MAP_TITLES_SNODAS_SWE[idxMay],
                    Constants.FILE_EXPORT_MAPS_SWE[idxMay], false);

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
                int idxJun = 7;
                await MapTools.LoadSweMapAsync(Constants.FILES_SNODAS_SWE[idxJun],
                    Constants.LAYER_NAMES_SNODAS_SWE[idxJun], Constants.MAP_TITLES_SNODAS_SWE[idxJun],
                    Constants.FILE_EXPORT_MAPS_SWE[idxJun], false);

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

    internal class MapButtonPalette_BtnSweJul : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                int idxJul = 8;
                await MapTools.LoadSweMapAsync(Constants.FILES_SNODAS_SWE[idxJul],
                    Constants.LAYER_NAMES_SNODAS_SWE[idxJul], Constants.MAP_TITLES_SNODAS_SWE[idxJul],
                    Constants.FILE_EXPORT_MAPS_SWE[idxJul], false);

                Module1.Current.MapFinishedLoading = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display SNODAS SWE map!!" + e.Message, "BAGIS-PRO");
                Module1.Current.ModuleLogManager.LogError(nameof(MapButtonPalette_BtnSweJul),
                    "Exception: " + e.Message);
            }
        }
    }

    internal class MapButtonPalette_BtnSweNov : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                int idxNov = 0;
                await MapTools.LoadSweMapAsync(Constants.FILES_SNODAS_SWE[idxNov],
                    Constants.LAYER_NAMES_SNODAS_SWE[idxNov], Constants.MAP_TITLES_SNODAS_SWE[idxNov],
                    Constants.FILE_EXPORT_MAPS_SWE[idxNov], false);

                Module1.Current.MapFinishedLoading = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display SNODAS SWE map!!" + e.Message, "BAGIS-PRO");
                Module1.Current.ModuleLogManager.LogError(nameof(MapButtonPalette_BtnSweNov),
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
                int idxDec = 1;
                await MapTools.LoadSweMapAsync(Constants.FILES_SNODAS_SWE[idxDec],
                    Constants.LAYER_NAMES_SNODAS_SWE[idxDec], Constants.MAP_TITLES_SNODAS_SWE[idxDec],
                    Constants.FILE_EXPORT_MAPS_SWE[idxDec], false);

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

    internal class MapButtonPalette_BtnSweNovDelta : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                int idxNov = 0;
                await MapTools.LoadSweMapAsync(Constants.FILES_SWE_DELTA[idxNov],
                    Constants.LAYER_NAMES_SWE_DELTA[idxNov], Constants.MAP_TITLES_SWE_DELTA[idxNov],
                    Constants.FILE_EXPORT_MAPS_SWE_DELTA[idxNov], true);

                Module1.Current.MapFinishedLoading = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display SWE Delta map!!" + e.Message, "BAGIS-PRO");
                Module1.Current.ModuleLogManager.LogError(nameof(MapButtonPalette_BtnSweNovDelta),
                    "Exception: " + e.Message);
            }
        }
    }

    internal class MapButtonPalette_BtnSweDecDelta : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                int idxDec = 1;
                await MapTools.LoadSweMapAsync(Constants.FILES_SWE_DELTA[idxDec],
                    Constants.LAYER_NAMES_SWE_DELTA[idxDec], Constants.MAP_TITLES_SWE_DELTA[idxDec],
                    Constants.FILE_EXPORT_MAPS_SWE_DELTA[idxDec], true);

                Module1.Current.MapFinishedLoading = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display SWE Delta map!!" + e.Message, "BAGIS-PRO");
                Module1.Current.ModuleLogManager.LogError(nameof(MapButtonPalette_BtnSweDecDelta),
                    "Exception: " + e.Message);
            }
        }
    }

    internal class MapButtonPalette_BtnSweJanDelta : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                int idxJan = 2;
                await MapTools.LoadSweMapAsync(Constants.FILES_SWE_DELTA[idxJan],
                    Constants.LAYER_NAMES_SWE_DELTA[idxJan], Constants.MAP_TITLES_SWE_DELTA[idxJan],
                    Constants.FILE_EXPORT_MAPS_SWE_DELTA[idxJan], true);

                Module1.Current.MapFinishedLoading = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display SWE Delta map!!" + e.Message, "BAGIS-PRO");
                Module1.Current.ModuleLogManager.LogError(nameof(MapButtonPalette_BtnSweJanDelta),
                    "Exception: " + e.Message);
            }
        }
    }

    internal class MapButtonPalette_BtnSweFebDelta : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                int idxFeb = 3;
                await MapTools.LoadSweMapAsync(Constants.FILES_SWE_DELTA[idxFeb],
                    Constants.LAYER_NAMES_SWE_DELTA[idxFeb], Constants.MAP_TITLES_SWE_DELTA[idxFeb],
                    Constants.FILE_EXPORT_MAPS_SWE_DELTA[idxFeb], true);

                Module1.Current.MapFinishedLoading = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display SWE Delta map!!" + e.Message, "BAGIS-PRO");
                Module1.Current.ModuleLogManager.LogError(nameof(MapButtonPalette_BtnSweFebDelta),
                    "Exception: " + e.Message);
            }
        }
    }

    internal class MapButtonPalette_BtnSweMarDelta : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                int idxMar = 4;
                await MapTools.LoadSweMapAsync(Constants.FILES_SWE_DELTA[idxMar],
                    Constants.LAYER_NAMES_SWE_DELTA[idxMar], Constants.MAP_TITLES_SWE_DELTA[idxMar],
                    Constants.FILE_EXPORT_MAPS_SWE_DELTA[idxMar], true);

                Module1.Current.MapFinishedLoading = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display SWE Delta map!!" + e.Message, "BAGIS-PRO");
                Module1.Current.ModuleLogManager.LogError(nameof(MapButtonPalette_BtnSweMarDelta),
                    "Exception: " + e.Message);
            }
        }
    }

    internal class MapButtonPalette_BtnSweAprDelta : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                int idxApr = 5;
                await MapTools.LoadSweMapAsync(Constants.FILES_SWE_DELTA[idxApr],
                    Constants.LAYER_NAMES_SWE_DELTA[idxApr], Constants.MAP_TITLES_SWE_DELTA[idxApr],
                    Constants.FILE_EXPORT_MAPS_SWE_DELTA[idxApr], true);

                Module1.Current.MapFinishedLoading = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display SWE Delta map!!" + e.Message, "BAGIS-PRO");
                Module1.Current.ModuleLogManager.LogError(nameof(MapButtonPalette_BtnSweAprDelta),
                    "Exception: " + e.Message);
            }
        }
    }

    internal class MapButtonPalette_BtnSweMayDelta : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                int idxMay = 6;
                await MapTools.LoadSweMapAsync(Constants.FILES_SWE_DELTA[idxMay],
                    Constants.LAYER_NAMES_SWE_DELTA[idxMay], Constants.MAP_TITLES_SWE_DELTA[idxMay],
                    Constants.FILE_EXPORT_MAPS_SWE_DELTA[idxMay], true);

                Module1.Current.MapFinishedLoading = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display SWE Delta map!!" + e.Message, "BAGIS-PRO");
                Module1.Current.ModuleLogManager.LogError(nameof(MapButtonPalette_BtnSweMayDelta),
                    "Exception: " + e.Message);
            }
        }
    }

    internal class MapButtonPalette_BtnSweJunDelta : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                int idxJun = 7;
                await MapTools.LoadSweMapAsync(Constants.FILES_SWE_DELTA[idxJun],
                    Constants.LAYER_NAMES_SWE_DELTA[idxJun], Constants.MAP_TITLES_SWE_DELTA[idxJun],
                    Constants.FILE_EXPORT_MAPS_SWE_DELTA[idxJun], true);

                Module1.Current.MapFinishedLoading = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display SWE Delta map!!" + e.Message, "BAGIS-PRO");
                Module1.Current.ModuleLogManager.LogError(nameof(MapButtonPalette_BtnSweJunDelta),
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

}
