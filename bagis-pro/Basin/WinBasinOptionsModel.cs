using ArcGIS.Core.Data;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        private string _selectedName = "";
        private string _selectedId = "";
        private ObservableCollection<string> _names = new ObservableCollection<string>();
        private ObservableCollection<string> _ids = new ObservableCollection<string>();
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
        public ObservableCollection<string> Ids
        {
            get => _ids;
            set => SetProperty(ref _ids, value); // Utilizes ViewModelBase.SetProperty
        }
        public WinBasinOptionsModel(WinBasinOptions view)
        {
            _view = view;
            dynamic oSettings = Module1.Current.AoiCreationSettings;
            if (oSettings == null)
            {
                GeneralTools.LoadBagisSettings();
                oSettings = Module1.Current.AoiCreationSettings;               
            }
            if (oSettings != null)
            {
                RefLayer = oSettings.ReferenceLayer;
                GaugeStation = oSettings.GaugeStationPath;
                DemPath = oSettings.DemPath;
                MetersChecked = true;
                SelectedName = oSettings.FieldStationName;
                SelectedId = oSettings.FieldStationId;
                string retVal = oSettings.DemUnits;
                if (retVal != null && retVal == Constants.UNITS_FEET)
                {
                    FeetChecked = true;
                }
            }
        }
        public async Task InitializeAsync()
        {
            WorkspaceType wType = await GeneralTools.GetFeatureWorkspaceType(GaugeStation);
            IReadOnlyList<Field> fields = null;
            Names.Clear();
            Ids.Clear();
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
                                FeatureClassDefinition fcDefinition = geodatabase.GetDefinition<FeatureClassDefinition>(arrReturnValues[1]);
                                fields = fcDefinition.GetFields();
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
                            FeatureClassDefinition fcDefinition = geodatabase.GetDefinition<FeatureClassDefinition>(Path.GetFileName(GaugeStation));
                            fields = fcDefinition.GetFields();
                        }
                    });
                    break;
                case WorkspaceType.Shapefile:
                    await QueuedTask.Run(() =>
                    {
                        FileSystemConnectionPath connectionPath =
                            new FileSystemConnectionPath(new Uri(Path.GetDirectoryName(GaugeStation)), FileSystemDatastoreType.Shapefile);
                        FileSystemDatastore dataStore = new FileSystemDatastore(connectionPath);
                        FeatureClassDefinition fcDefinition = dataStore.GetDefinition<FeatureClassDefinition>(Path.GetFileName(GaugeStation));
                        fields = fcDefinition.GetFields();
                    });
                    break;
            }
            ObservableCollection<string> tmpList = new ObservableCollection<string>();
            ObservableCollection<string> tmpListId = new ObservableCollection<string>();
            if (fields != null)
            {
                var validIdOptions = new[] { FieldType.String, FieldType.Integer, FieldType.SmallInteger, FieldType.BigInteger};
                await QueuedTask.Run(() =>
                {
                    foreach (var field in fields)
                    {
                        if (field.FieldType.Equals(FieldType.String))
                        {
                            tmpList.Add(field.Name);
                        }
                        if (validIdOptions.Contains(field.FieldType))
                        {
                            tmpListId.Add(field.Name);
                        }
                    }
                });
            }
            Names = tmpList;
            Ids = tmpListId;
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
        public ICommand CmdRefLayer
        {
            get
            {
                return new RelayCommand(async () => {
                    await MapTools.DisplayReferenceLayersAsync();
                });
            }
        }
        public ICommand CmdSelectRefLayer
        {
            get
            {
                return new RelayCommand(() =>
                {

                    //Display the filter in an Open Item dialog
                    OpenItemDialog aNewFilter = new OpenItemDialog
                    {
                        Title = "Select a layer file",
                        MultiSelect = false,
                        Filter = ItemFilters.Layers_AllFileTypes
                    };
                    bool? ok = aNewFilter.ShowDialog();
                    bool bOk = ok ?? false;
                    if (bOk)
                    {
                        RefLayer = "";
                        var arrFileNames = aNewFilter.Items;
                        foreach (var item in arrFileNames)
                        {
                            RefLayer = item.Path;
                        }
                    }

                    return Task.CompletedTask;
                });
            }
        }
        public ICommand CmdSelectDem
        {
            get
            {
                return new RelayCommand(() => {

                    // 1. Create the composite filter
                    BrowseProjectFilter multiFilter = new BrowseProjectFilter();

                    // 2. Add multiple predefined filters
                    multiFilter.AddFilter(BrowseProjectFilter.GetFilter("esri_browseDialogFilters_rasters"));
                    multiFilter.AddFilter(BrowseProjectFilter.GetFilter("esri_browseDialogFilters_services_image"));

                    //Display the filter in an Open Item dialog
                    OpenItemDialog aNewFilter = new OpenItemDialog
                    {
                        Title = "Select DEM",
                        MultiSelect = false,
                        BrowseFilter = multiFilter
                    };
                    bool? ok = aNewFilter.ShowDialog();
                    bool bOk = ok ?? false;
                    if (bOk)
                    {
                        DemPath = "";
                        var arrFileNames = aNewFilter.Items;
                        foreach (var item in arrFileNames)
                        {
                            DemPath = item.Path;
                        }
                    }
                });
            }
        }

        public ICommand CmdSetGaugeStations
        {
            get
            {
                return new RelayCommand( async () => {

                    // 1. Create the composite filter
                    BrowseProjectFilter multiFilter = new BrowseProjectFilter();

                    // 2. Add multiple predefined filters
                    multiFilter.AddFilter(BrowseProjectFilter.GetFilter("esri_browseDialogFilters_featureClasses_point"));
                    multiFilter.AddFilter(BrowseProjectFilter.GetFilter("esri_browseDialogFilters_shapefiles"));


                    //Display the filter in an Open Item dialog
                    OpenItemDialog aNewFilter = new OpenItemDialog
                    {
                        Title = "Select gauge stations",
                        MultiSelect = false,
                        BrowseFilter = multiFilter
                    };
                    bool? ok = aNewFilter.ShowDialog();
                    bool bOk = ok ?? false;
                    if (bOk)
                    {
                        GaugeStation = "";
                        var arrFileNames = aNewFilter.Items;
                        foreach (var item in arrFileNames)
                        {
                            GaugeStation = item.Path;
                        }
                        await InitializeAsync();
                    }
                });
            }
        }

        public ICommand CmdDefault
        {
            get
            {
                return new RelayCommand(() => {
                    IDictionary<string, dynamic> dictDataSources = Module1.Current.DataSources;
                    RefLayer = Constants.LAYER_FILE_REFERENCE_MAPS;
                    BA_Objects.DataSource dsDem = new BA_Objects.DataSource(dictDataSources[Constants.DATA_TYPE_DEM]);
                    if (dsDem != null)
                    {
                        DemPath = dsDem.uri;
                    }
                    dynamic oSettings = Module1.Current.BagisSettings;
                    if (oSettings != null)
                    {
                        GaugeStation = (string)oSettings.GaugeStationUri;
                        SelectedName = Constants.FIELD_NAME.ToLower();
                        SelectedId = Constants.FIELD_STATION_TRIPLET;
                        MetersChecked = true;
                        FeetChecked = false;
                    }
                    
                });
            }
        }
        private RelayCommand _runSaveCommand;
        public ICommand CmdSave
        {
            get
            {
                if (_runSaveCommand == null)
                    _runSaveCommand = new RelayCommand(RunSaveImpl, () => true);
                return _runSaveCommand;
            }
        }
        private void RunSaveImpl(object param)
        {
            dynamic oAoiSettings = Module1.Current.AoiCreationSettings;
            string strFullPath = GeneralTools.GetBagisSettingsPath() + @"\" + Constants.FOLDER_SETTINGS
                + @"\" + Constants.FILE_BAGIS_SETTINGS;
            dynamic oAllSettings = null;
            // read JSON directly from a file
            using (FileStream fs = File.OpenRead(strFullPath))
            {
                using (JsonTextReader reader = new JsonTextReader(new StreamReader(fs)))
                {
                    oAllSettings = (JObject)JToken.ReadFrom(reader);
                }
            }

            if (oAllSettings != null && oAoiSettings != null)
            {
                oAoiSettings.ReferenceLayer = RefLayer;
                oAoiSettings.DemPath = DemPath;
                oAoiSettings.GaugeStationPath = GaugeStation;
                oAoiSettings.DemPath = DemPath;
                oAoiSettings.DemUnits = Constants.UNITS_METERS;
                if (FeetChecked)
                {
                    oAoiSettings.DemUnits = Constants.UNITS_FEET;
                }
                oAoiSettings.FieldStationName = SelectedName;
                oAoiSettings.FieldStationId = SelectedId;
                try
                {
                    oAllSettings.AoiCreation = oAoiSettings;
                    // write JSON directly to a file
                    using (StreamWriter file = File.CreateText(strFullPath))
                    using (JsonTextWriter writer = new JsonTextWriter(file))
                    {
                        writer.Formatting = Formatting.Indented;
                        oAllSettings.WriteTo(writer);
                    }
                    // Save updated options to session variables
                    Module1.Current.AoiCreationSettings = oAoiSettings;
                    MessageBox.Show("Options have been successfully saved", "BAGIS-Pro");                    
                }
                catch (Exception e)
                {
                    MessageBox.Show("Unable to save updated options!", "BAGIS-Pro");
                }
            }

        }

        }


    }
