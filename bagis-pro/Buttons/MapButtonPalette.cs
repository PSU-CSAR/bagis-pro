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

    internal class MapButtonPalette_BtnRoads : Button
    {
        protected override async void OnClick()
        {
            try
            {
                //await ToggleMapDisplay.ToggleAsync(BagisMapType.ROADS);
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
                int idxFeb = 3;
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
                int idxMar = 4;
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
                int idxApr = 5;
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
                int idxMay = 6;
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
                int idxJun = 7;
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
                    Constants.FILE_EXPORT_MAPS_SWE[idxJul]);

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
                    Constants.FILE_EXPORT_MAPS_SWE[idxNov]);

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

    internal class MapButtonPalette_BtnSweNovDelta : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                int idxNov = 0;
                await MapTools.LoadSweDeltaMapAsync(Constants.FILES_SWE_DELTA[idxNov],
                    Constants.LAYER_NAMES_SWE_DELTA[idxNov], Constants.MAP_TITLES_SWE_DELTA[idxNov],
                    Constants.FILE_EXPORT_MAPS_SWE_DELTA[idxNov]);

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
                await MapTools.LoadSweDeltaMapAsync(Constants.FILES_SWE_DELTA[idxDec],
                    Constants.LAYER_NAMES_SWE_DELTA[idxDec], Constants.MAP_TITLES_SWE_DELTA[idxDec],
                    Constants.FILE_EXPORT_MAPS_SWE_DELTA[idxDec]);

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
                await MapTools.LoadSweDeltaMapAsync(Constants.FILES_SWE_DELTA[idxJan],
                    Constants.LAYER_NAMES_SWE_DELTA[idxJan], Constants.MAP_TITLES_SWE_DELTA[idxJan],
                    Constants.FILE_EXPORT_MAPS_SWE_DELTA[idxJan]);

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
                await MapTools.LoadSweDeltaMapAsync(Constants.FILES_SWE_DELTA[idxFeb],
                    Constants.LAYER_NAMES_SWE_DELTA[idxFeb], Constants.MAP_TITLES_SWE_DELTA[idxFeb],
                    Constants.FILE_EXPORT_MAPS_SWE_DELTA[idxFeb]);

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
                await MapTools.LoadSweDeltaMapAsync(Constants.FILES_SWE_DELTA[idxMar],
                    Constants.LAYER_NAMES_SWE_DELTA[idxMar], Constants.MAP_TITLES_SWE_DELTA[idxMar],
                    Constants.FILE_EXPORT_MAPS_SWE_DELTA[idxMar]);

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
                await MapTools.LoadSweDeltaMapAsync(Constants.FILES_SWE_DELTA[idxApr],
                    Constants.LAYER_NAMES_SWE_DELTA[idxApr], Constants.MAP_TITLES_SWE_DELTA[idxApr],
                    Constants.FILE_EXPORT_MAPS_SWE_DELTA[idxApr]);

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
                await MapTools.LoadSweDeltaMapAsync(Constants.FILES_SWE_DELTA[idxMay],
                    Constants.LAYER_NAMES_SWE_DELTA[idxMay], Constants.MAP_TITLES_SWE_DELTA[idxMay],
                    Constants.FILE_EXPORT_MAPS_SWE_DELTA[idxMay]);

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
                await MapTools.LoadSweDeltaMapAsync(Constants.FILES_SWE_DELTA[idxJun],
                    Constants.LAYER_NAMES_SWE_DELTA[idxJun], Constants.MAP_TITLES_SWE_DELTA[idxJun],
                    Constants.FILE_EXPORT_MAPS_SWE_DELTA[idxJun]);

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

    internal class MapButtonPalette_BtnSeasonalPrecipContribSQ1 : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                int idxSQ1 = 0;
                string strAnalysisGdb = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis);
                await MapTools.UpdateMapAsync(strAnalysisGdb, Constants.FILES_SEASON_PRECIP_CONTRIB[idxSQ1], Module1.Current.DisplayedSeasonalPrecipContribMap,
                    Constants.LAYER_NAMES_SEASON_PRECIP_CONTRIB[idxSQ1], "SEASONAL PRECIP CONTRIBUTION: DEC, JAN, & FEB", "% Annual Precipitation", true, Constants.FILE_EXPORT_MAPS_SEASONAL_PRECIP_CONTRIB[idxSQ1]);
                Module1.Current.MapFinishedLoading = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display Seasonal Precipitation Q1 map!!" + e.Message, "BAGIS-PRO");
            }
        }
    }

    internal class MapButtonPalette_BtnSeasonalPrecipContribSQ2 : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                int idxSQ2 = 1;
                string strAnalysisGdb = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis);
                await MapTools.UpdateMapAsync(strAnalysisGdb, Constants.FILES_SEASON_PRECIP_CONTRIB[idxSQ2], Module1.Current.DisplayedSeasonalPrecipContribMap,
                    Constants.LAYER_NAMES_SEASON_PRECIP_CONTRIB[idxSQ2], "SEASONAL PRECIP CONTRIBUTION: MAR, APR, & MAY", "% Annual Precipitation", true, Constants.FILE_EXPORT_MAPS_SEASONAL_PRECIP_CONTRIB[idxSQ2]);
                Module1.Current.MapFinishedLoading = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display Seasonal Precipitation Q2 map!!" + e.Message, "BAGIS-PRO");
            }
        }
    }

    internal class MapButtonPalette_BtnSeasonalPrecipContribSQ3 : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                int idxSQ3 = 2;
                string strAnalysisGdb = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis);
                await MapTools.UpdateMapAsync(strAnalysisGdb, Constants.FILES_SEASON_PRECIP_CONTRIB[idxSQ3], Module1.Current.DisplayedSeasonalPrecipContribMap,
                    Constants.LAYER_NAMES_SEASON_PRECIP_CONTRIB[idxSQ3], "SEASONAL PRECIP CONTRIBUTION: JUN, JUL, & AUG", "% Annual Precipitation", true, Constants.FILE_EXPORT_MAPS_SEASONAL_PRECIP_CONTRIB[idxSQ3]);
                Module1.Current.MapFinishedLoading = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display Seasonal Precipitation Q3 map!!" + e.Message, "BAGIS-PRO");
            }
        }
    }

    internal class MapButtonPalette_BtnSeasonalPrecipContribSQ4 : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                int idxSQ4 = 3;
                string strAnalysisGdb = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis);
                await MapTools.UpdateMapAsync(strAnalysisGdb, Constants.FILES_SEASON_PRECIP_CONTRIB[idxSQ4], Module1.Current.DisplayedSeasonalPrecipContribMap,
                    Constants.LAYER_NAMES_SEASON_PRECIP_CONTRIB[idxSQ4], "SEASONAL PRECIP CONTRIBUTION: SEP, OCT, & NOV", "% Annual Precipitation", true, Constants.FILE_EXPORT_MAPS_SEASONAL_PRECIP_CONTRIB[idxSQ4]);
                Module1.Current.MapFinishedLoading = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display Seasonal Precipitation Q4 map!!" + e.Message, "BAGIS-PRO");
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
