using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bagis_pro
{
    static class LookupTables
    {
        public static IReadOnlyDictionary<string, string> PrismText { get; } = new Dictionary<string, string>()
        {
            { "Jan", "January"},
            { "Feb", "February"},
            { "Mar", "March"},
            { "Apr", "April"},
            { "May", "May"},
            { "Jun", "June"},
            { "Jul", "July"},
            { "Aug", "August"},
            { "Sep", "September"},
            { "Oct", "October"},
            { "Nov", "November"},
            { "Dec", "December"},
            { "Q1", "Jan-Mar"},
            { "Q2", "Apr-Jun"},
            { "Q3", "Jul-Sep"},
            { "Q4", "Oct-Dec"},
            { "Annual", "Annual"}
        };

    }
}
