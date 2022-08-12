using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
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


namespace bagis_pro
{
    internal class DockExportForSnodasViewModel : DockPane
    {
        private const string _dockPaneID = "bagis_pro_DockExportForSnodas";

        protected DockExportForSnodasViewModel() { }

        /// <summary>
        /// Show the DockPane.
        /// </summary>
        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            if (pane == null)
                return;

            pane.Activate();
        }

        /// <summary>
        /// Text shown near the top of the DockPane.
        /// </summary>
        private string _heading = "Export For Snodas";
        public string Heading
        {
            get { return _heading; }
            set
            {
                SetProperty(ref _heading, value, () => Heading);
            }
        }

        public ICommand CmdSelectPoint
        {
            get
            {
                return new RelayCommand(async () =>
                {
                    //Create an instance of BrowseProjectFilter class
                    BrowseProjectFilter bf = new BrowseProjectFilter
                    {
                        //Name the filter
                        Name = "Point feature"
                    };

                    //Add typeID for Point feature class
                    bf.AddCanBeTypeId("fgdb_fc_point");
                    bf.AddCanBeTypeId("shapefile_point");
                    //Allow only File GDBs
                    bf.AddDontBrowseIntoFlag(BrowseProjectFilter.FilterFlag.DontBrowseFiles);
                    bf.AddDoBrowseIntoTypeId("database_fgdb");
                    //Display only folders and GDB in the browse dialog
                    bf.Includes.Add("FolderConnection");
                    bf.Includes.Add("GDB");
                    //Does not display Online places in the browse dialog
                    bf.Excludes.Add("esri_browsePlaces_Online");

                    //Display the filter in an Open Item dialog
                    OpenItemDialog aNewFilter = new OpenItemDialog
                    {
                        Title = "Select a basin, aoi, or folder",
                        MultiSelect = false,
                        BrowseFilter = bf
                    };
                    bool? ok = aNewFilter.ShowDialog();
                    bool bOk = ok ?? false;
                    if (bOk)
                    {
                        var arrFileNames = aNewFilter.Items;
                        foreach (var item in arrFileNames)
                        {
                            string strPath = item.Path;
                        }
                    }


                });
            }
        }
    }

    /// <summary>
    /// Button implementation to show the DockPane.
    /// </summary>
    internal class DockExportForSnodas_ShowButton : Button
    {
        protected override void OnClick()
        {
            DockExportForSnodasViewModel.Show();
        }
    }

}
