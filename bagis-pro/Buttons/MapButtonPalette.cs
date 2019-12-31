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
    internal class MapButtonPalette_BtnElevation : Button
    {
        protected override void OnClick()
        {
        }
    }

    internal class MapButtonPalette_BtnSlope : Button
    {
        protected override async void OnClick()
        {
            try
            {
                BA_Objects.MapDefinition thisMap = MapTools.LoadMapDefinition(BagisMapType.SLOPE);
                // toggle layers according to map definition
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

                Layout layout = await MapTools.GetDefaultLayoutAsync(Constants.MAPS_DEFAULT_LAYOUT_NAME);
                await MapTools.UpdateMapElementsAsync(layout, Module1.Current.Aoi.Name.ToUpper(), thisMap);
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display slope map!!" + e.Message, "BAGIS-PRO");
            }
        }
    }

    internal class MapButtonPalette_button3 : Button
    {
        protected override void OnClick()
        {
        }
    }

}
