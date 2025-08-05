using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using bagis_pro.BA_Objects;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace bagis_pro.Basin
{
    internal class WinClipDemModel : PropertyChangedBase
    {
        WinClipDem _view = null;
        bool _demExtentChecked = true;
        bool _filledDemChecked = true;
        bool _flowDirChecked = true;
        bool _flowAccChecked = true;
        bool _slopeChecked = true;
        bool _aspectChecked = true;
        bool _hillshadeChecked = true;
        double _zFactor = 1;
        private bool _smoothDemChecked;
        private int _filterCellHeight = 3;
        private int _filterCellWidth = 7;
        private string _basinFolder = "";

        public WinClipDemModel(WinClipDem view)
        {
            _view = view;
        }

        public bool FilledDemChecked
        {
            get => _filledDemChecked;
            set => SetProperty(ref _filledDemChecked, value);
        }
        public bool FlowDirChecked
        {
            get => _flowDirChecked;
            set => SetProperty(ref _flowDirChecked, value);
        }
        public bool FlowAccChecked
        {
            get => _flowAccChecked;
            set => SetProperty(ref _flowAccChecked, value);
        }
        public bool SlopeChecked
        {
            get => _slopeChecked;
            set => SetProperty(ref _slopeChecked, value);
        }
        public bool AspectChecked
        {
            get => _aspectChecked;
            set => SetProperty(ref _aspectChecked, value);
        }
        public bool HillshadeChecked
        {
            get => _hillshadeChecked;
            set => SetProperty(ref _hillshadeChecked, value);
        }
        public bool DemExtentChecked
        {
            get => _demExtentChecked;
            set => SetProperty(ref _demExtentChecked, value);
        }
        public double ZFactor
        {
            get => _zFactor;
            set => SetProperty(ref _zFactor, value);
        }
        public bool SmoothDemChecked
        {
            get => _smoothDemChecked;
            set => SetProperty(ref _smoothDemChecked, value);
        }
        public int FilterCellHeight
        {
            get => _filterCellHeight;
            set => SetProperty(ref _filterCellHeight, value);
        }
        public int FilterCellWidth
        {
            get => _filterCellWidth;
            set => SetProperty(ref _filterCellWidth, value);
        }
        public ICommand CmdAll => new RelayCommand(() =>
        {
            SetCheckedValues(true);
        });

        public ICommand CmdNone => new RelayCommand(() =>
        {
            SetCheckedValues(false);
        });

        protected void SetCheckedValues(bool checkAll)
        {
            DemExtentChecked = checkAll;
            FilledDemChecked = checkAll;
            FlowDirChecked = checkAll;
            FlowAccChecked = checkAll;
            SlopeChecked = checkAll;
            AspectChecked = checkAll;
            HillshadeChecked = checkAll;
        }

        private RelayCommand _runClipCommand;
        public ICommand CmdClip
        {
            get
            {
                if (_runClipCommand == null)
                    _runClipCommand = new RelayCommand(RunClipImplAsync, () => true);
                return _runClipCommand;
            }
        }

        private async void RunClipImplAsync(object param)
        {
            // verify filter size parameters
            if (SmoothDemChecked)
            {
                if (FilterCellHeight <= 0 || FilterCellWidth <= 0)
                {
                    MessageBox.Show("Invalid filter size! Please re-enter.", "BAGIS-Pro");
                    return;
                }
            }

            // verify dem is available
            string strSourceDem = Module1.Current.DataSources[DataSource.GetDemKey].uri;
            WorkspaceType wType = await GeneralTools.GetRasterWorkspaceType(strSourceDem);
            if (wType == WorkspaceType.None)
            {
                MessageBox.Show("Invalid DEM. AOI cannot be created!", "BAGIS-Pro", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Get output path from parent form
            var pane = (DockBasinToolViewModel)FrameworkApplication.DockPaneManager.Find("bagis_pro_Basin_DockBasinTool");;
            _basinFolder = pane.ParentFolder;


            uint nStep = 12;
            int intWait = 500;
            var progress = new ProgressDialog("Processing ...", "Cancel", 100, false);
            var status = new CancelableProgressorSource(progress);
            status.Max = 100;
            progress.Show();
            IList<string> lstExistingGdb = GeodatabaseTools.CheckForBasinGdb(_basinFolder);
            for (int i = 0; i < lstExistingGdb.Count; i++)
            {
                int layersRemoved = await MapTools.RemoveLayersInFolderAsync(lstExistingGdb[i]);
                System.IO.Directory.Delete(lstExistingGdb[i], true);
            }
            BA_ReturnCode success = await GeodatabaseTools.CreateGeodatabaseFoldersAsync(_basinFolder, FolderType.BASIN, status.Progressor);

            await QueuedTask.Run(() =>
            {
                status.Progressor.Value = 0;    // reset the progressor's value back to 0 between GP tasks
                status.Progressor.Message = $@"Clipping DEM to Basin Folder ... (step 1 of {nStep})";
                //block the CIM for a second
                Task.Delay(intWait).Wait();
            }, status.Progressor);



            // Clean-up step progressor
            progress.Hide();
            progress.Dispose();
        }
    }
}
