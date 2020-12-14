using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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
using ArcGIS.Desktop.Mapping;


namespace bagis_pro
{
    internal class DockMigrateToFGDBViewModel : DockPane
    {
        private const string _dockPaneID = "bagis_pro_DockMigrateToFGDB";

        protected DockMigrateToFGDBViewModel()
        {
            Names = new ObservableCollection<BA_Objects.Aoi>();
        }

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
        private string _heading = "Migrate to File Geodatabase";
        private string _aoiFolder = "";
        private bool _cmdRunEnabled = false;
        public string Heading
        {
            get { return _heading; }
            set
            {
                SetProperty(ref _heading, value, () => Heading);
            }
        }
        public string AoiFolder
        {
            get { return _aoiFolder; }
            set
            {
                SetProperty(ref _aoiFolder, value, () => AoiFolder);
            }
        }
        public bool CmdRunEnabled
        {
            get { return _cmdRunEnabled; }
            set
            {
                SetProperty(ref _cmdRunEnabled, value, () => CmdRunEnabled);
            }
        }

        public ObservableCollection<BA_Objects.Aoi> Names { get; set; }

        public ICommand CmdAoiFolder
        {
            get
            {
                return new RelayCommand(async () =>
                {
                    //Display the filter in an Open Item dialog
                    OpenItemDialog aNewFilter = new OpenItemDialog
                    {
                        Title = "Select a basin, aoi, or folder",
                        MultiSelect = false,
                        Filter = ItemFilters.folders
                    };
                    bool? ok = aNewFilter.ShowDialog();
                    bool bOk = ok ?? false;
                    if (bOk)
                    {
                        AoiFolder = "";
                        var arrFileNames = aNewFilter.Items;
                        foreach (var item in arrFileNames)
                        {
                            AoiFolder = item.Path;
                        }
                    }

                    string strLogFolder = AoiFolder + "\\" + Constants.FOLDER_MAP_PACKAGE;
                    // Make sure the maps_publish folder exists under the selected folder
                    if (!Directory.Exists(Path.GetDirectoryName(strLogFolder)))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(strLogFolder));
                    }

                    // Set logger to parent folder directory
                    Module1.Current.ModuleLogManager.UpdateLogFileLocation(strLogFolder);

                    Names.Clear();
                    try
                    {
                        string[] folders = Directory.GetDirectories(AoiFolder, "*", SearchOption.AllDirectories);
                        foreach (var item in folders)
                        {
                            FolderType fType = await GeodatabaseTools.GetAoiFolderTypeAsync(item);
                            if (fType == FolderType.FOLDER)
                            {
                                // Do nothing; skip
                            }
                            else
                            {
                                IList<string> reqRasterList = GetListOfReqWeaselRasters(item);
                                IList<string> existsLayersList = await GeneralTools.RasterDatasetsExistAsync(reqRasterList);
                                IList<string> missingLayersList = new List<string>();
                                if (existsLayersList.Count < reqRasterList.Count)
                                {
                                    foreach (var aLayer in reqRasterList)
                                    {
                                        if (!existsLayersList.Contains(aLayer))
                                        {
                                            missingLayersList.Add(aLayer);
                                        }
                                    }
                                }
                                // Accomodate two possible names for raster aoi boundary layer (aoibagis or aoi)
                                IList<string> lstTest = new List<string>
                                {
                                    item + @"\aoibagis"
                                };
                                existsLayersList = await GeneralTools.RasterDatasetsExistAsync(lstTest);
                                if (existsLayersList.Count == 0)
                                {
                                    lstTest.Clear();
                                    string strLayer = item + @"\aoi";
                                    lstTest.Add(strLayer);
                                    existsLayersList = await GeneralTools.RasterDatasetsExistAsync(lstTest);
                                    if (existsLayersList.Count == 0)
                                    {
                                        missingLayersList.Add(item + @"\aoibagis");
                                    }
                                }
                                IList<string> reqVectorList = GetListOfReqWeaselVectors(item);
                                existsLayersList = await GeneralTools.ShapefilesExistAsync(reqVectorList);
                                if (reqVectorList.Count > existsLayersList.Count)
                                {
                                    foreach (var aLayer in reqVectorList)
                                    {
                                        if (!existsLayersList.Contains(aLayer))
                                        {
                                            missingLayersList.Add(aLayer);
                                        }
                                    }
                                }

                                if (missingLayersList.Count > 0)
                                {
                                    StringBuilder sb = new StringBuilder();
                                    sb.Append(AoiFolder + " cannot be exported to File Geodatabase format. ");
                                    sb.Append("The data layers listed below are missing. These files ");
                                    sb.Append("must be present before attempting the conversion:\r\n");
                                    foreach (var missing in missingLayersList)
                                    {
                                        sb.Append(missing + "\r\n");
                                    }
                                    Module1.Current.ModuleLogManager.LogError(nameof(CmdAoiFolder), sb.ToString());
                                }
                                else
                                {
                                    BA_Objects.Aoi aoi = new BA_Objects.Aoi(Path.GetFileName(item), item);
                                    Names.Add(aoi);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        string strLogEntry = "An error occurred while interrogating the subdirectories " + e.StackTrace + "\r\n";
                        Module1.Current.ModuleLogManager.LogError(nameof(CmdAoiFolder), strLogEntry);
                    }

                    if (Names.Count > 0)
                    {
                        CmdRunEnabled = true;
                    }
                    else
                    {
                        CmdRunEnabled = false;
                    }
                });
            }
        }

        IList<string> GetListOfReqWeaselRasters(string aoiPath)
        {
            IList<string> lstRasters = new List<string>();
            // surfaces layers
            string layerPath = aoiPath + @"\output\surfaces\dem\filled\grid";   // Filled DEM
            lstRasters.Add(layerPath);
            layerPath = aoiPath + @"\output\surfaces\dem\filled\aspect\grid";   // Aspect
            lstRasters.Add(layerPath);
            layerPath = aoiPath + @"\output\surfaces\dem\filled\slope\grid";   // Slope
            lstRasters.Add(layerPath);
            layerPath = aoiPath + @"\output\surfaces\dem\filled\flow-direction\flow-accumulation\grid";   // Flow accumulation
            lstRasters.Add(layerPath);
            layerPath = aoiPath + @"\output\surfaces\dem\filled\flow-direction\grid";   // Flow direction
            lstRasters.Add(layerPath);

            // prism layers
            string[] arrPrismLayers = {"jan","feb","mar","apr","may","jun","jul","aug","sep",
                                       "oct","nov","dec","q1","q2","q3","q3","q4","annual" };
            foreach (var month in arrPrismLayers)
            {
                layerPath = aoiPath + @"\layers\PRISM\" + month + @"\grid";
                lstRasters.Add(layerPath);
            }

            // A couple of aoi layers
            layerPath = aoiPath + @"\aoib";
            lstRasters.Add(layerPath);
            layerPath = aoiPath + @"\p_aoi";
            lstRasters.Add(layerPath);

            return lstRasters;
        }

        IList<string> GetListOfReqWeaselVectors(string aoiPath)
        {
            IList<string> lstVectors = new List<string>();
            string layerPath = aoiPath + @"\aoi_v.shp";
            lstVectors.Add(layerPath);
            layerPath = aoiPath + @"\aoib_v.shp";
            lstVectors.Add(layerPath);
            layerPath = aoiPath + @"\p_aoi_v.shp";
            lstVectors.Add(layerPath);
            layerPath = aoiPath + @"\pourpoint.shp";
            lstVectors.Add(layerPath);
            return lstVectors;
        }
    }

    /// <summary>
    /// Button implementation to show the DockPane.
    /// </summary>
    internal class DockMigrateToFGDB_ShowButton : Button
    {
        protected override void OnClick()
        {
            DockMigrateToFGDBViewModel.Show();
        }
    }
}
