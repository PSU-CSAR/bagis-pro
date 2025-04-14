using System.Linq;
using ArcGIS.Desktop.Framework.Contracts;

namespace bagis_pro.Buttons
{
    /// <summary>
    /// Represents the ComboBox
    /// </summary>
    internal class CboCurrentBasin : ComboBox
    {

        private bool _isInitialized;
        private string _strNotSelected = "Not Selected";

        /// <summary>
        /// Combo Box constructor
        /// </summary>
        public CboCurrentBasin()
        {
            UpdateCombo();
            Module1.Current.CboCurrentBasin = this;
        }

        /// <summary>
        /// Updates the combo box with all the items.
        /// </summary>

        private void UpdateCombo()
        {
            // TODO – customize this method to populate the combobox with your desired items  
            if (_isInitialized)
                SelectedItem = ItemCollection.FirstOrDefault(); //set the default item in the comboBox

            if (!_isInitialized)
            {
                Clear();
                Add(new ComboBoxItem(_strNotSelected));
                _isInitialized = true;
            }

            Add(new ComboBoxItem(_strNotSelected));
            Enabled = true; //enables the ComboBox
            SelectedItem = ItemCollection.FirstOrDefault(); //set the default item in the comboBox
        }

        /// <summary>
        /// The on comboBox selection change event. 
        /// </summary>
        /// <param name="item">The newly selected combo box item</param>
        protected override void OnSelectionChange(ComboBoxItem item)
        {

            if (item == null)
                return;

            if (string.IsNullOrEmpty(item.Text))
                return;

            // TODO  Code behavior when selection changes.    
        }
        public void SetBasinName(string basinName)
        {
            Clear();
            Add(new ComboBoxItem(basinName));
            SelectedItem = ItemCollection.FirstOrDefault(); //set the default item in the comboBox
        }
        public void ResetBasinName()
        {
            Clear();
            Add(new ComboBoxItem(_strNotSelected));
            SelectedItem = ItemCollection.FirstOrDefault(); //set the default item in the comboBox
        }

    }
}
