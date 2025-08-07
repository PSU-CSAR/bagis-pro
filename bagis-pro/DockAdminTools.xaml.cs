using bagis_pro.Basin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;


namespace bagis_pro
{
    /// <summary>
    /// Interaction logic for DockAdminToolsView.xaml
    /// </summary>
    public partial class DockAdminToolsView : UserControl
    {
        public DockAdminToolsView()
        {
            InitializeComponent();
        }
        public void SelectedTime_Checked(object s, RoutedEventArgs e)
        {
            var radio = s as RadioButton;
            string strTag = Convert.ToString(radio.Tag);
            if (strTag.Equals("SelectedTimeChecked"))
            {
                bool bChecked = radio.IsChecked ?? false;
                ckAnnual.IsEnabled = bChecked;
                tbSelectMinYear.IsEnabled = bChecked;
                tbSelectMaxYear.IsEnabled = bChecked;
                ckPeriods.IsEnabled = bChecked;
                tbTimePeriodCount.IsEnabled = bChecked; 
            }

        }
        private void AnnualEndYear_textChangedEventHandler(object sender, TextChangedEventArgs args)
        {
            var textBox = sender as TextBox;
            if (tbSelectMaxYear != null)
            {
                tbSelectMaxYear.Text = textBox.Text;
            }
        }
        private void SelectedMaxYear_textChangedEventHandler(object sender, TextChangedEventArgs args)
        {
            // Set annual from value to annual to value - 29 (30 years)
            var textBox = sender as TextBox;
            int maxYear = -1;
            if (textBox != null && tbSelectMinYear != null)            {                
                bool bSuccess = int.TryParse(textBox.Text, out maxYear);
                if (bSuccess)
                {
                    tbSelectMinYear.Text = Convert.ToString(maxYear - 29);
                   
                }
            }
            // The model wasn't catching this value without this
            DockAdminToolsViewModel oModel = (DockAdminToolsViewModel)DataContext;
            if (textBox != null)
            {
                oModel.SelectedMaxYear = maxYear;
            }
        }
        public void Clip_Mtbs_Changed(object s, RoutedEventArgs e)
        {
            var ckBox = s as CheckBox;
            ckOverwriteMtbs.IsEnabled = (bool) ckBox.IsChecked;
        }


    }
}
