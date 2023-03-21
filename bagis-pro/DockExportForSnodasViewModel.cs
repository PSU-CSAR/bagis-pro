using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace bagis_pro
{
    internal class DockExportForSnodasViewModel : DockPane, INotifyDataErrorInfo
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
        private string _outputPathLabel = "The name of the output file is ";
        private string _errorMessages = "";
        private readonly Dictionary<string, ICollection<string>>
            _validationErrors = new Dictionary<string, ICollection<string>>();
        private const string _keyStationTriplet = "StationTriplet";
        private const string _keyPointPath = "PointPath";
        private const string _keyStationName = "StationName";
        private const string _keyPolyPath = "PolyPath";
        private const string _keyOutputPath = "OutputPath";

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
        public string OutputPathLabel
        {
            get { return _outputPathLabel; }
            set
            {
                SetProperty(ref _outputPathLabel, value, () => OutputPathLabel);
            }
        }
        public string ErrorMessages
        {
            get { return _errorMessages; }
            set
            {
                SetProperty(ref _errorMessages, value, () => ErrorMessages);
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
                            long lngPoints = await GeodatabaseTools.CountFeaturesAsync(uriGdb, strFc);
                            if (lngPoints != 1)
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
                return new RelayCommand(() =>
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
                        PolyPath = item.Path;
                        //string strPolyErrorMsg = "";
                        //await QueuedTask.Run(async () =>
                        //{
                        //    string strGdbPath = System.IO.Path.GetDirectoryName(item.Path);
                        //    Uri uriGdb = new Uri(System.IO.Path.GetDirectoryName(item.Path));
                        //    string strFc = System.IO.Path.GetFileName(item.Path);
                        //    int intPoints = await GeodatabaseTools.CountFeaturesAsync(uriGdb, strFc);
                        //    if (intPoints != 1)
                        //    {
                        //        strPolyErrorMsg = "The polygon feature class must have 1 and only 1 feature!";
                        //    }
                        //    else
                        //    {
                        //        PolyPath = item.Path;
                        //    }
                        //});
                        //if (!String.IsNullOrEmpty(strPolyErrorMsg))
                        //{
                        //    MessageBox.Show(strPolyErrorMsg, "BAGIS-PRO", System.Windows.MessageBoxButton.OK);
                        //}
                    }
                });
            }
        }

        public ICommand CmdSelectOutput
        {
            get
            {
                return new RelayCommand(() =>
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
                return new RelayCommand( async () =>
                {
                    _validationErrors.Clear();
                    const string keyErrorMessages = "ErrorMessages";
                    int errors = ValidateRequiredFields();
                    errors = errors + ValidateStationTriplet(StationTriplet);
                    if (errors > 0)
                    {
                        List<string> lstAllErrors = new List<string>();
                        foreach (var strKey in _validationErrors.Keys)
                        {
                            IList<string> lstErrors = (IList<string>)_validationErrors[strKey];
                            lstAllErrors.AddRange(lstErrors);
                        }
                        _validationErrors[keyErrorMessages] = lstAllErrors;
                    }
                    /* Raise event to tell WPF to execute the GetErrors method */
                    RaiseErrorsChanged(keyErrorMessages);
                    if (errors > 0)
                    {
                        return; // Exit without processing
                    }

                    // generate the file(s)
                    string pointOutputPath = Path.GetTempPath() + "pourpoint.geojson";
                    string polygonOutputPath = Path.GetTempPath() + "polygon.geojson";
                    // Process the pourpoint
                    BA_ReturnCode success = await GeoprocessingTools.FeaturesToSnodasGeoJsonAsync(PointPath, pointOutputPath, true);
                    if (success.Equals(BA_ReturnCode.Success))
                    {
                        // Process the feature class
                        Uri uriGdb = new Uri(Path.GetDirectoryName(PolyPath));
                        string strFc = Path.GetFileName(PolyPath);
                        //int intFeatures = await GeodatabaseTools.CountFeaturesAsync(uriGdb, strFc);
                        string strFcPath = PolyPath;
                        //string strTempAoiPath = null;
                        //if (intFeatures > 1)
                        //{
                        //    strTempAoiPath = $@"{Path.GetDirectoryName(PolyPath)}\tmpAoi";
                        //    success = await GeoprocessingTools.BufferAsync(PolyPath, strTempAoiPath, "0.5 Meters", "ALL");
                        //    if (success == BA_ReturnCode.Success)
                        //    {
                        //        strFcPath = strTempAoiPath;
                        //    }
                        //}
                        success = await GeoprocessingTools.FeaturesToSnodasGeoJsonAsync(strFcPath, polygonOutputPath, true);
                        //if (success == BA_ReturnCode.Success && !String.IsNullOrEmpty(strTempAoiPath))
                        //{
                        //    // Clean up temp buffered FC
                        //    success = await GeoprocessingTools.DeleteDatasetAsync(strTempAoiPath);
                        //}
                    }

                    if (success.Equals(BA_ReturnCode.Success))
                    {
                        Webservices ws = new Webservices();
                        string errorMessage = ws.GenerateSnodasGeoJson(pointOutputPath, polygonOutputPath, OutputPath);
                        if (! string.IsNullOrEmpty(errorMessage))
                        {
                            MessageBox.Show(errorMessage, "BAGIS-PRO");
                        }
                    }
                    else
                    {
                        MessageBox.Show("An error occurred!!", "BAGIS-PRO");
                    }
                    MessageBox.Show("Export Successful!!", "BAGIS-PRO");
                });

            }
        }

        private int ValidateStationTriplet(string strValue)
        {
            ICollection<string> validationErrors = new List<string>();
            if (String.IsNullOrEmpty(strValue))
            {
                validationErrors.Add("Station triplet is required");
            }
            else
            {
                bool bValidFormat = false;
                string[] arrPieces = strValue.Split(':');
                if (arrPieces.Length == 3)
                {
                    if (arrPieces[0].Length == 8 && arrPieces[1].Length == 2)
                    {
                        if (arrPieces[2].Equals("USGS"))
                        {
                            bValidFormat = true;
                        }
                    }
                }
                if (!bValidFormat)
                {
                    validationErrors.Add("The station triplet value is not formatted correctly");
                }
            }
            /* Update the collection in the dictionary returned by the GetErrors method */
            if (validationErrors.Count > 0)
            {
                _validationErrors[_keyStationTriplet] = validationErrors;
            }
            else
            {
                _validationErrors.Remove(_keyStationTriplet);
            }
            /* Raise event to tell WPF to execute the GetErrors method */
            RaiseErrorsChanged(_keyStationTriplet);
            return validationErrors.Count;
        }

        private int ValidateRequiredFields()
        {
            int errorCount = 0;
            if (String.IsNullOrEmpty(PointPath))
            {
                ICollection<string> pointPathErrors = new List<string>
                    { "Point path is required"};
                _validationErrors[_keyPointPath] = pointPathErrors;
                errorCount++;
            }
            else
            {
                _validationErrors.Remove(_keyPointPath);
            }
            /* Raise event to tell WPF to execute the GetErrors method */
            RaiseErrorsChanged(_keyPointPath);
            if (String.IsNullOrEmpty(StationName))
            {
                ICollection<string> errors = new List<string>
                    { "Watershed name is required"};
                _validationErrors[_keyStationName] = errors;
                errorCount++;
            }
            else
            {
                _validationErrors.Remove(_keyStationName);
            }
            /* Raise event to tell WPF to execute the GetErrors method */
            RaiseErrorsChanged(_keyStationName);
            if (String.IsNullOrEmpty(PolyPath))
            {
                ICollection<string> errors = new List<string>
                    { "Polygon layer is required"};
                _validationErrors[_keyPolyPath] = errors;
                errorCount++;
            }
            else
            {
                _validationErrors.Remove(_keyPolyPath);
            }
            /* Raise event to tell WPF to execute the GetErrors method */
            RaiseErrorsChanged(_keyPolyPath);
            if (String.IsNullOrEmpty(OutputPath))
            {
                ICollection<string> errors = new List<string>
                    { "Output path is required"};
                _validationErrors[_keyOutputPath] = errors;
                errorCount++;
            }
            else
            {
                _validationErrors.Remove(_keyOutputPath);
            }
            /* Raise event to tell WPF to execute the GetErrors method */
            RaiseErrorsChanged(_keyOutputPath);

            return errorCount;
        }

        #region INotifyDataErrorInfo members
        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;
        private void RaiseErrorsChanged(string propertyName)
        {
            if (ErrorsChanged != null)
                ErrorsChanged(this, new DataErrorsChangedEventArgs(propertyName));
        }

        public System.Collections.IEnumerable GetErrors(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName)
                || !_validationErrors.ContainsKey(propertyName))
                return null;

            return _validationErrors[propertyName];
        }

        public bool HasErrors
        {
            get { return _validationErrors.Count > 0; }
        }
        #endregion
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
