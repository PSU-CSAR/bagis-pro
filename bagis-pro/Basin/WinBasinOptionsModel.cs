using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace bagis_pro.Basin
{
    internal class WinBasinOptionsModel : ViewModelBase
    {
        WinBasinOptions _view = null;
        private string _refLayer;
        private string _demPath;
        private string _gaugeStation;
        private bool _metersChecked;
        private bool _feetChecked;
        private string _selectedName;
        private string _selectedId;
        private ObservableCollection<string> _names = new ObservableCollection<string>();
        public string RefLayer
        {
            get => _refLayer;
            set => SetProperty(ref _refLayer, value);
        }
        public string DemPath
        {
            get => _demPath;
            set => SetProperty(ref _demPath, value);
        }
        public string GaugeStation
        {
            get => _gaugeStation;
            set => SetProperty(ref _gaugeStation, value);
        }
        public bool MetersChecked
        {
            get => _metersChecked;
            set => SetProperty(ref _metersChecked, value);
        }
        public bool FeetChecked
        {
            get => _feetChecked;
            set => SetProperty(ref _feetChecked, value);
        }
        public string SelectedName
        {
            get => _selectedName;
            set => SetProperty(ref _selectedName, value);
        }
        public string SelectedId
        {
            get => _selectedId;
            set => SetProperty(ref _selectedId, value);
        }
        public ObservableCollection<string> Names
        {
            get => _names;
            set => SetProperty(ref _names, value); // Utilizes ViewModelBase.SetProperty
        }

        public WinBasinOptionsModel(WinBasinOptions view)
        {
            _view = view;
            dynamic oSettings = Module1.Current.AoiCreationSettings;
            if (oSettings == null)
            {
                GeneralTools.LoadBagisSettings();
                oSettings = Module1.Current.AoiCreationSettings;
                if (oSettings != null)
                {
                    RefLayer = oSettings.ReferenceLayer;
                    GaugeStation = oSettings.GaugeStationPath;
                    DemPath = oSettings.DemPath;
                    MetersChecked = true;
                    string retVal = oSettings.DemUnits;
                    if (retVal != null && retVal == Constants.UNITS_FEET)
                    {
                        FeetChecked = true;
                    }
                }               
            }
        }
        public async Task InitializeAsync()
        {
            WorkspaceType wType = await GeneralTools.GetFeatureWorkspaceType(GaugeStation);
            FeatureClassDefinition fcDefinition = null;
            Names.Clear();
            switch (wType)
            {
                case WorkspaceType.None:
                    MessageBox.Show("The gauge station layer is not valid. Please select a new layer!");
                    return;
                case WorkspaceType.FeatureServer:
                    string[] arrReturnValues = Webservices.ParseUriAndLayerNumber(GaugeStation);
                    if (arrReturnValues.Length == 2 && !string.IsNullOrEmpty(arrReturnValues[0]))
                    {
                        var serviceProps = new ServiceConnectionProperties(new Uri(arrReturnValues[0]));
                        await QueuedTask.Run(() =>
                        {
                            using (Geodatabase geodatabase = new Geodatabase(serviceProps))
                            {
                                fcDefinition = geodatabase.GetDefinition<FeatureClassDefinition>(arrReturnValues[1]);
                            }
                        });
                    }
                    break;
                case WorkspaceType.Geodatabase:
                    Uri uri = new Uri(Path.GetDirectoryName(GaugeStation));
                    await QueuedTask.Run(() =>
                    {
                        using (Geodatabase geodatabase =
                                new Geodatabase(new FileGeodatabaseConnectionPath(uri)))
                        {
                            fcDefinition = geodatabase.GetDefinition<FeatureClassDefinition>(Path.GetFileName(GaugeStation));
                        }
                    });
                    break;
                case WorkspaceType.Shapefile:
                    await QueuedTask.Run(() =>
                    {
                        FileSystemConnectionPath connectionPath =
                            new FileSystemConnectionPath(new Uri(Path.GetDirectoryName(GaugeStation)), FileSystemDatastoreType.Shapefile);
                        FileSystemDatastore dataStore = new FileSystemDatastore(connectionPath);
                        fcDefinition = dataStore.GetDefinition<FeatureClassDefinition>(Path.GetFileName(GaugeStation));
                    });
                    break;
            }
            ObservableCollection<string> tmpList = new ObservableCollection<string>();
            if (fcDefinition != null)
            {
                await QueuedTask.Run(() =>
                {
                    IReadOnlyList<Field> fields = fcDefinition.GetFields();
                    foreach (var field in fields)
                    {
                        tmpList.Add(field.Name);
                    }
                });
            }
            Names = tmpList;
        }

        public ICommand CmdClose
        {
            get
            {
                return new RelayCommand(() => {
                    _view.Close();
                });
            }
        }
    }


    }
