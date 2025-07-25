using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace bagis_pro.Basin
{
    internal class WinClipDemModel : PropertyChangedBase
    {
        WinClipDem _view = null;
        bool _demExtentChecked = false;
        bool _filledDemChecked = false;
        bool _flowDirChecked = false;
        bool _flowAccChecked = false;
        bool _slopeChecked = false;
        bool _aspectChecked = false;
        bool _hillshadeChecked = false;
        double _zFactor = 1;
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
    }
}
