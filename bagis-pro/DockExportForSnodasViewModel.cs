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

        private string _pointPath = "";
        private string _polyPath = "";
        private string _stationTriplet = "";
        private string _stationName = "";
        private string _outputPath = "";

        public string PointPath
        {
            get { return _pointPath; }
            set
            {
                SetProperty(ref _pointPath, value, () => PointPath);
            }
        }
        public string PolyPath
        {
            get { return _polyPath; }
            set
            {
                SetProperty(ref _polyPath, value, () => PolyPath);
            }
        }
        public string StationTriplet
        {
            get { return _stationTriplet; }
            set
            {
                SetProperty(ref _stationTriplet, value, () => StationTriplet);
            }
        }
        public string StationName
        {
            get { return _stationName; }
            set
            {
                SetProperty(ref _stationName, value, () => StationName);
            }
        }
        public string OutputPath
        {
            get { return _outputPath; }
            set
            {
                SetProperty(ref _outputPath, value, () => OutputPath);
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
                    //bf.AddCanBeTypeId("shapefile_point");
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
                        Title = "Select a point feature class",
                        MultiSelect = false,
                        BrowseFilter = bf
                    };
                    bool? ok = aNewFilter.ShowDialog();
                    bool bOk = ok ?? false;
                    if (bOk)
                    {
                        // get the item
                        var item = aNewFilter.Items.First();
                        string strPointErrorMsg = "";
                        await QueuedTask.Run(async () =>
                        {
                            string strGdbPath = System.IO.Path.GetDirectoryName(item.Path);
                            Uri uriGdb = new Uri(System.IO.Path.GetDirectoryName(item.Path));
                            string strFc = System.IO.Path.GetFileName(item.Path);
                            int intPoints = await GeodatabaseTools.CountFeaturesAsync(uriGdb, strFc);
                            if (intPoints != 1)
                            {
                                strPointErrorMsg = "The point feature class must have 1 and only 1 feature!";
                            }
                            else
                            {
                                PointPath = item.Path;
                                string[] arrFields = new string[] { Constants.FIELD_STATION_TRIPLET, Constants.FIELD_STATION_NAME };
                                foreach (string strField in arrFields)
                                {
                                    // Check for the field, if it exists query the value
                                    if (await GeodatabaseTools.AttributeExistsAsync(uriGdb, strFc, strField))
                                    {
                                        QueryFilter queryFilter = new QueryFilter();
                                        string strValue = await GeodatabaseTools.QueryTableForSingleValueAsync(uriGdb, strFc,
                                            strField, queryFilter);
                                        switch (strField)
                                        {
                                            case Constants.FIELD_STATION_TRIPLET:
                                                StationTriplet = strValue;
                                                break;
                                            case Constants.FIELD_STATION_NAME:
                                                StationName = strValue;
                                                break;
                                        }
                                    }
                                }
                            }
                        });
                        if (!String.IsNullOrEmpty(strPointErrorMsg))
                        {
                            MessageBox.Show(strPointErrorMsg, "BAGIS-PRO", System.Windows.MessageBoxButton.OK);
                        }
                    }
                });
            }
        }

        public ICommand CmdSelectPoly
        {
            get
            {
                return new RelayCommand(async () =>
                {
                    //Create an instance of BrowseProjectFilter class
                    BrowseProjectFilter bf = new BrowseProjectFilter
                    {
                        //Name the filter
                        Name = "Polygon feature"
                    };

                    //Add typeID for Point feature class
                    bf.AddCanBeTypeId("fgdb_fc_polygon");
                    //bf.AddCanBeTypeId("shapefile_polygon");
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
                        Title = "Select a polygon feature",
                        MultiSelect = false,
                        BrowseFilter = bf
                    };
                    bool? ok = aNewFilter.ShowDialog();
                    bool bOk = ok ?? false;
                    if (bOk)
                    {
                        // get the item
                        var item = aNewFilter.Items.First();
                        string strPolyErrorMsg = "";
                        await QueuedTask.Run(async () =>
                        {
                            string strGdbPath = System.IO.Path.GetDirectoryName(item.Path);
                            Uri uriGdb = new Uri(System.IO.Path.GetDirectoryName(item.Path));
                            string strFc = System.IO.Path.GetFileName(item.Path);
                            int intPoints = await GeodatabaseTools.CountFeaturesAsync(uriGdb, strFc);
                            if (intPoints != 1)
                            {
                                strPolyErrorMsg = "The polygon feature class must have 1 and only 1 feature!";
                            }
                            else
                            {
                                PolyPath = item.Path;
                            }
                        });
                        if (!String.IsNullOrEmpty(strPolyErrorMsg))
                        {
                            MessageBox.Show(strPolyErrorMsg, "BAGIS-PRO", System.Windows.MessageBoxButton.OK);
                        }
                    }
                });
            }
        }

        public ICommand CmdSelectOutput
        {
            get
            {
                return new RelayCommand( () =>
                {
                    System.Windows.Forms.FolderBrowserDialog openFileDlg = new System.Windows.Forms.FolderBrowserDialog();
                    var result = openFileDlg.ShowDialog();
                    if (result.ToString() != string.Empty)
                    {
                        OutputPath = openFileDlg.SelectedPath;
                    }
                });
            }
        }

        public ICommand CmdExport
        {
            get
            {
                return new RelayCommand(() =>
                {
                    List<string> lstRequired = new List<string> { PointPath, PolyPath, OutputPath };
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
