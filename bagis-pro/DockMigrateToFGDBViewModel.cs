using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
            Names.CollectionChanged += NamesCollectionChanged;
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
        private bool _canRun = false;   // Flag that indicates if things are in a state that the process can successfully run; Enables the button on the form
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

        public ObservableCollection<BA_Objects.Aoi> Names { get; set; }

        public void NamesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Allows us to respond when properties of items in the collection change; ie: including the AOI in the list to migrate
            if (e.OldItems != null)
                foreach (BA_Objects.Aoi oldItem in e.OldItems)
                    oldItem.PropertyChanged -= NamesCollection_PropertyChanged;

            if (e.NewItems != null)
                foreach (BA_Objects.Aoi newItem in e.NewItems)
                    newItem.PropertyChanged += NamesCollection_PropertyChanged;
        }

        private void NamesCollection_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs args)
        {
            //This will get called when an item in the collection is changed to manage the run button
            int validAoiCount = 0;
            foreach (var item in Names)
            {
                if (item.AoiBatchIsSelected == true)
                {
                    validAoiCount++;
                }
            }
            if (validAoiCount > 0)
            {
                _canRun = true;
            }
            else
            {
                _canRun = false;
            }
        }

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
                                IList<string> missingReqLayersList = new List<string>();
                                if (existsLayersList.Count < reqRasterList.Count)
                                {
                                    foreach (var aLayer in reqRasterList)
                                    {
                                        if (!existsLayersList.Contains(aLayer))
                                        {
                                            missingReqLayersList.Add(aLayer);
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
                                        missingReqLayersList.Add(item + @"\aoibagis");
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
                                            missingReqLayersList.Add(aLayer);
                                        }
                                    }
                                }

                                if (missingReqLayersList.Count > 0)
                                {
                                    StringBuilder sb = new StringBuilder();
                                    sb.Append(item + " cannot be exported to File Geodatabase format. ");
                                    sb.Append("The data layers listed below are missing. These files ");
                                    sb.Append("must be present before attempting the conversion:\r\n");
                                    foreach (var missing in missingReqLayersList)
                                    {
                                        sb.Append(missing + "\r\n");
                                    }
                                    Module1.Current.ModuleLogManager.LogError(nameof(CmdAoiFolder), sb.ToString());
                                }
                                else
                                {
                                    IList<string> missingOptLayersList = new List<string>();
                                    IList<string> optRasterList = GetListOptWeaselRasters(item);
                                    IList<string> existsOptLayersList = await GeneralTools.RasterDatasetsExistAsync(optRasterList);
                                    if (existsOptLayersList.Count < optRasterList.Count)
                                    {
                                        foreach (var aLayer in optRasterList)
                                        {
                                            if (!existsOptLayersList.Contains(aLayer))
                                            {
                                                missingOptLayersList.Add(aLayer);
                                            }
                                        }
                                    }
                                    lstTest.Clear();
                                    lstTest.Add(item + @"\unsnappedpp.shp");
                                    existsOptLayersList = await GeneralTools.ShapefilesExistAsync(lstTest);
                                    if (existsLayersList.Count == 0)
                                    {
                                        missingOptLayersList.Add(item + @"\unsnappedpp.shp");
                                    }
                                    if (missingOptLayersList.Count > 0)
                                    {
                                        StringBuilder sb = new StringBuilder();
                                        sb.Append("The following files normally present in a Weasel AOI are ");
                                        sb.Append("missing from " + item + " and will not be converted: \r\n");
                                        foreach (var missing in missingOptLayersList)
                                        {
                                            sb.Append(missing + "\r\n");
                                        }
                                        Module1.Current.ModuleLogManager.LogError(nameof(CmdAoiFolder), sb.ToString());
                                    }

                                    BA_Objects.Aoi aoi = new BA_Objects.Aoi(Path.GetFileName(item), item);
                                    IList<string> lstExistingGdb = CheckForBagisGdb(item);
                                    if (lstExistingGdb.Count > 0)
                                    {
                                        StringBuilder sb = new StringBuilder();
                                        sb.Append("At least one geodatabase already exists in aoi " + aoi.Name);
                                        sb.Append(". \r\n");
                                        sb.Append("Do you wish to overwrite them? All existing data will be lost!\r\n");
                                        System.Windows.MessageBoxResult res = MessageBox.Show(sb.ToString(), "BAGIS-PRO",
                                            System.Windows.MessageBoxButton.YesNo);
                                        if (res != System.Windows.MessageBoxResult.Yes)
                                        {
                                            aoi.AoiBatchIsSelected = false;
                                        }
                                    }
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
                });
            }
        }


        private RelayCommand _runCommand;
        public ICommand CmdRun
        {
            get
            {
                if (_runCommand == null)
                    _runCommand = new RelayCommand(RunImplAsync, () => _canRun);
                return _runCommand;
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

        IList<string> GetListOptWeaselRasters(string aoiPath)
        {
            IList<string> lstRasters = new List<string>();
            string layerPath = aoiPath + @"\output\surfaces\dem\grid";
            lstRasters.Add(layerPath);
            layerPath = aoiPath + @"\output\surfaces\dem\filled\hillshade\grid";
            lstRasters.Add(layerPath);
            return lstRasters;
        }

        IList<string> CheckForBagisGdb(string aoiPath)
        {
            IList<string> lstBagisGdb = new List<string>();
            if (Directory.Exists(aoiPath + "\\" + GeodatabaseNames.Analysis.Value))
            {
                lstBagisGdb.Add(aoiPath + "\\" + GeodatabaseNames.Analysis.Value);
            }
            if (Directory.Exists(aoiPath + "\\" + GeodatabaseNames.Aoi.Value))
            {
                lstBagisGdb.Add(aoiPath + "\\" + GeodatabaseNames.Aoi.Value);
            }
            if (Directory.Exists(aoiPath + "\\" + GeodatabaseNames.Layers.Value))
            {
                lstBagisGdb.Add(aoiPath + "\\" + GeodatabaseNames.Layers.Value);
            }
            if (Directory.Exists(aoiPath + "\\" + GeodatabaseNames.Prism.Value))
            {
                lstBagisGdb.Add(aoiPath + "\\" + GeodatabaseNames.Prism.Value);
            }
            if (Directory.Exists(aoiPath + "\\" + GeodatabaseNames.Surfaces.Value))
            {
                lstBagisGdb.Add(aoiPath + "\\" + GeodatabaseNames.Surfaces.Value);
            }
            return lstBagisGdb;
        }

        private async void RunImplAsync(object param)
        {
            MessageBox.Show("Running");
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
