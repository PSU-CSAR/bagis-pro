using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.UtilityNetwork.Trace;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace bagis_pro.Buttons
{
    internal class SetBasinExtentTool : MapTool
    {
        public SetBasinExtentTool()
        {
            IsSketchTool = true;
            SketchType = SketchGeometryType.Rectangle;
            SketchOutputMode = SketchOutputMode.Map;
        }
        protected override Task OnToolActivateAsync(bool active)
        {
            return base.OnToolActivateAsync(active);
        }
        protected async override Task<bool> OnSketchCompleteAsync(Geometry geometry)
        {
            Map oMap = await MapTools.SetDefaultMapNameAsync(Constants.MAPS_DEFAULT_MAP_NAME);
            string glName = "Clip DEM";
            var gl_param = new GraphicsLayerCreationParams
            {
                Name = glName,
                IsVisible = true
            };
            GraphicElement oElement = null;
           await QueuedTask.Run(() =>
            {
                // Create a new graphics layer if one doesn't exist
                GraphicsLayer graphicsLayer = oMap.GetLayersAsFlattenedList().OfType<GraphicsLayer>().Where(f =>
                    f.Name == glName).FirstOrDefault();
                if (graphicsLayer == null)
                {
                    graphicsLayer = LayerFactory.Instance.CreateLayer<GraphicsLayer>(gl_param, oMap);
                }
                else
                {
                    graphicsLayer.RemoveElements();
                }

                //Set symbolology, create and add element to layout
                CIMStroke outline = SymbolFactory.Instance.ConstructStroke(
                    ColorFactory.Instance.GreenRGB, 3.0, SimpleLineStyle.Solid);
                CIMPolygonSymbol polySym = 
                    SymbolFactory.Instance.ConstructPolygonSymbol(null, outline); // Null fill for no fill.

                // Create the graphic using the geometry and symbol
                var cimGraphicElement = new CIMPolygonGraphic
                {
                    Polygon = (Polygon)geometry,
                    Symbol = polySym.MakeSymbolReference()
                };
                oElement = graphicsLayer.AddElement(cimGraphicElement);
            });

            if (oElement != null)
            {
                Module1.ActivateState("bagis_pro_Buttons_BtnCreateBasin_State");
                return true;
            }
            else
            {
                Module1.DeactivateState("bagis_pro_Buttons_BtnCreateBasin_State");
                return false;
            }
        }
    }
}
