using System;
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
using Newtonsoft.Json.Linq;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace bagis_pro.Menus
{
    internal class MnuMaps_BtnSelectAoi : Button
    {
        protected override void OnClick()
        {
            try
            {
                OpenItemDialog selectAoiDialog = new OpenItemDialog()
                {
                    Title = "Select AOI Folder",
                    MultiSelect = false,
                    Filter = ItemFilters.folders
                };
                if (selectAoiDialog.ShowDialog() == true)
                {
                    Module1.DeactivateState("Aoi_Selected_State");
                    IEnumerable<Item> selectedItems = selectAoiDialog.Items;
                    var e = selectedItems.FirstOrDefault();
                    GeneralTools.SetAoi(e.Path);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("An error occurred while trying to set the AOI!! " + e.Message, "BAGIS PRO");
            }

        }
    }

    internal class MnuMaps_BtnMapLoad : Button
    {
        protected async override void OnClick()
        {
            string tempAoiPath = "C:\\Docs\\animas_AOI_prms";
            try
            {
                await MapTools.DisplayMaps(tempAoiPath);
                Module1.Current.DisplayedMap = Constants.FILE_EXPORT_MAP_ELEV_PDF;
                Module1.ActivateState("BtnMapLoad_State");          
            }
            catch (Exception e)
            {
                MessageBox.Show("An error occurred while trying to load the maps!! " + e.Message, "BAGIS PRO");
            }
        }
    }

}
