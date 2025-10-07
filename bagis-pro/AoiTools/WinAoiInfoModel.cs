using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using bagis_pro.BA_Objects;
using ExtensionMethod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace bagis_pro.AoiTools
{
    internal class WinAoiInfoModel : PropertyChangedBase
    {
        WinAoiInfo _view = null;

        public WinAoiInfoModel(WinAoiInfo view)
        {
            _view = view;
        }

        private RelayCommand _setAoiCommand;
        public ICommand CmdSetAoi
        {
            get
            {
                if (_setAoiCommand == null)
                    _setAoiCommand = new RelayCommand(SetAoiImplAsync, () => true);
                return _setAoiCommand;
            }
        }
        private async void SetAoiImplAsync(object param)
        {
            try
            {
                OpenItemDialog selectAoiDialog = new OpenItemDialog()
                {
                    Title = "Select AOI Folder",
                    MultiSelect = false,
                    Filter = ItemFilters.Folders
                };
                if (selectAoiDialog.ShowDialog() == true)
                {
                    Module1.DeactivateState("Aoi_Selected_State");
                    IEnumerable<Item> selectedItems = selectAoiDialog.Items;
                    var e = selectedItems.FirstOrDefault();
                    Aoi oAoi = await GeneralTools.SetAoiAsync(e.Path, null);
                    if (oAoi != null)
                    {
                        Module1.Current.CboCurrentAoi.SetAoiName(oAoi.Name);
                        MessageBox.Show("AOI is set to " + oAoi.Name + "!", "BAGIS PRO");

                        if (!oAoi.ValidForecastData)
                        {
                            StringBuilder sb = new StringBuilder();
                            sb.Append("This AOI does not have the required forecast station information. ");
                            sb.Append("Use the Batch Tools menu to run the 'Forecast Station Data' tool to update ");
                            sb.Append("the forecast station data. Next use the Batch Tools menu to run the ");
                            sb.Append("'Generate AOI Reports' tool to create the analysis layers required by BAGIS-Pro.");
                            MessageBox.Show(sb.ToString(), "BAGIS PRO");
                        }
                        else
                        {
                            string[] arrButtonNames = { "bagis_pro_Menus_MnuMaps_BtnMapLoad", "bagis_pro_Buttons_BtnExcelTables",
                                "bagis_pro_WinExportPdf"};
                            int intButtonsDisabled = 0;
                            for (int i = 0; i < arrButtonNames.Length; i++)
                            {
                                var plugin = FrameworkApplication.GetPlugInWrapper(arrButtonNames[i]);
                                if (plugin == null)
                                {
                                    intButtonsDisabled++;
                                }
                                else if (!plugin.Enabled)
                                {
                                    intButtonsDisabled++;
                                }
                            }
                            if (intButtonsDisabled > 0)
                            {
                                StringBuilder sb = new StringBuilder();
                                sb.Append("This AOI is missing at least one required layer. Use the Batch Tools menu to run the ");
                                sb.Append("'Generate AOI Reports' tool to create the analysis layers required by BAGIS-Pro.");
                                MessageBox.Show(sb.ToString(), "BAGIS PRO");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("An error occurred while trying to set the AOI!! " + e.Message, "BAGIS PRO");
            }

        }

    }


    }
