using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bagis_pro
{
    public class MapTools
    {
        public static async Task<Layout> SetDefaultLayoutNameAsync(string layoutName)
        {
            return await QueuedTask.Run( () =>
            {
                Layout layout = null;
                Project proj = Project.Current;

                //Finding the first project item with name matches with mapName
                LayoutProjectItem lytItem =
                    proj.GetItems<LayoutProjectItem>()
                        .FirstOrDefault(m => m.Name.Equals(layoutName, StringComparison.CurrentCultureIgnoreCase));
                if (lytItem != null)
                {
                    layout = lytItem.GetLayout();
                }
                else
                {
                    layout = LayoutFactory.Instance.CreateLayout(8.5, 11, LinearUnit.Inches);
                    layout.SetName(layoutName);
                }
                return layout;
            });
        }

        public static async Task SetDefaultMapFrameDimensionAsync(string mapFrameName, Layout oLayout, Map oMap, double xMin, 
                                                                  double yMin, double xMax, double yMax)
        {
            await QueuedTask.Run( () =>
            {
                //Finding the mapFrame with mapFrameName
                if (!(oLayout.FindElement(mapFrameName) is MapFrame mfElm))
                {
                    //Build 2D envelope geometry
                    Coordinate2D mf_ll = new Coordinate2D(xMin, yMin);
                    Coordinate2D mf_ur = new Coordinate2D(xMax, yMax);
                    Envelope mf_env = EnvelopeBuilder.CreateEnvelope(mf_ll, mf_ur);
                    mfElm = LayoutElementFactory.Instance.CreateMapFrame(oLayout, mf_env, oMap);
                    mfElm.SetName(mapFrameName);
                }
            });
        }

        public static async Task<Map> SetDefaultMapNameAsync(string mapName)
        {
            return await QueuedTask.Run(async () =>
            {
                Map map = null;
                Project proj = Project.Current;

                //Finding the first project item with name matches with mapName
                MapProjectItem mpi =
                    proj.GetItems<MapProjectItem>()
                        .FirstOrDefault(m => m.Name.Equals(mapName, StringComparison.CurrentCultureIgnoreCase));
                if (mpi != null)
                {
                    map = mpi.GetMap();
                }
                else
                {
                    map = MapFactory.Instance.CreateMap(mapName, basemap: Basemap.None);
                }
                await ProApp.Panes.CreateMapPaneAsync(map);
                return map;
            });
        }

        public static async Task AddAoiBoundaryToMapAsync(string aoiPath)
        {
            await QueuedTask.Run(() =>
            {
                string strPath = GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Aoi, true) +
                                 "aoi_v";
                Uri uri = new Uri(strPath);
                LayerFactory.Instance.CreateLayer(uri, MapView.Active.Map);
            });
        }
    }
}
