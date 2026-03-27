using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using bagis_pro.AoiTools;
using System.Linq;


namespace bagis_pro.Buttons
{
    internal class BtnCreateAoi : Button
    {

        private WinCreateAoi _wincreateaoi = null;
        protected async override void OnClick()
        {
            Map oMap = await MapTools.SetDefaultMapNameAsync(Constants.MAPS_DEFAULT_MAP_NAME);
            GraphicsLayer graphicsLayer = null;
            await QueuedTask.Run(() =>
            {
                // Create a new graphics layer if one doesn't exist
                graphicsLayer = oMap.GetLayersAsFlattenedList().OfType<GraphicsLayer>().Where(f =>
                    f.Name == Constants.MAPS_POURPOINT_LAYER).FirstOrDefault();

            });

            if (graphicsLayer == null)
            {
                MessageBox.Show("No defined pourpoint location in the active view!", "BAGIS-Pro");
            }
            else
            {
                MessageBox.Show("Please review and verify the Data Units on the next dialog!");
                // Show the Create AOI form

                //already open?
                if (_wincreateaoi != null)
                    return;
                _wincreateaoi = new WinCreateAoi();
                _wincreateaoi.Owner =
                    ArcGIS.Desktop.Framework.FrameworkApplication.Current.MainWindow;
                _wincreateaoi.Closed += (o, e) => { _wincreateaoi = null; };
                _wincreateaoi.Show();
                //uncomment for modal
                //_wincreateaoi.ShowDialog();            }
            }
        }
    }
}
