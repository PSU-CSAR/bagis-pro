using ArcGIS.Desktop.Framework.Contracts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public ObservableCollection<BA_Objects.Aoi> Names { get; set; }

        public WinBasinOptionsModel(WinBasinOptions view)
        {
            _view = view;

        }
    }


    }
