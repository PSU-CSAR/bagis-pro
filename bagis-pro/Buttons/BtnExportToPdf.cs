using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace bagis_pro.Buttons
{
    internal class BtnExportToPdf : Button
    {
        protected async override void OnClick()
        {
            try
            {
                string outputDirectory = Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_MAP_PACKAGE;
                if (! System.IO.Directory.Exists(outputDirectory))
                {
                    System.IO.Directory.CreateDirectory(outputDirectory);
                }

                // This is the order the files will be assembled
                IList<string> lstFilesToAppend = new List<string> { Constants.FILE_TITLE_PAGE_PDF,  Constants.FILE_EXPORT_MAP_ELEV_PDF,
                    Constants.FILE_EXPORT_MAP_SNOTEL_PDF, Constants.FILE_EXPORT_MAP_SCOS_PDF, Constants.FILE_EXPORT_MAP_SNOTEL_AND_SCOS_PDF,
                    Constants.FILE_EXPORT_MAP_PRECIPITATION_PDF, Constants.FILE_EXPORT_MAPS_SWE[0], Constants.FILE_EXPORT_MAPS_SWE[1],
                    Constants.FILE_EXPORT_MAPS_SWE[2], Constants.FILE_EXPORT_MAPS_SWE[3], Constants.FILE_EXPORT_MAPS_SWE[4],
                    Constants.FILE_EXPORT_MAPS_SWE[5], Constants.FILE_EXPORT_MAPS_SWE[6], Constants.FILE_EXPORT_MAP_ASPECT_PDF,
                    Constants.FILE_EXPORT_MAP_SLOPE_PDF};
                foreach(string strButtonState in Constants.STATES_MAP_BUTTON)
                {
                    if (FrameworkApplication.State.Contains(strButtonState))
                    {
                        int foundS1 = strButtonState.IndexOf("_State");
                        string strMapButton = strButtonState.Remove(foundS1);
                        ICommand cmd = FrameworkApplication.GetPlugInWrapper(strMapButton) as ICommand;

                        if ((cmd != null))
                        {
                            do
                            {
                                await Task.Delay(TimeSpan.FromSeconds(0.5));  // build in delay until the command can execute
                            }
                            while (!cmd.CanExecute(null));
                            cmd.Execute(null);
                        }

                        do
                        {
                            await Task.Delay(TimeSpan.FromSeconds(0.5));  // build in delay so maps can load
                        }
                        while (Module1.Current.MapFinishedLoading == false);

                        BA_ReturnCode success2 = await GeneralTools.ExportMapToPdfAsync();    // export each map to pdf
                    }
                }

                // Export remaining SNODAS SWE maps
                if (Module1.Current.DisplayedMap.Equals(Constants.FILE_EXPORT_MAP_SWE_JANUARY_PDF))
                {
                    Uri snodasUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers));
                    Map map = MapView.Active.Map;
                    Layout layout = await MapTools.GetDefaultLayoutAsync(Constants.MAPS_DEFAULT_LAYOUT_NAME);
                    IList<string> lstSweFilesToAppend = await MapTools.PublishSnodasSweMapsAsync(snodasUri, 0, map, layout);
                    if (lstSweFilesToAppend.Count < 1)
                    {
                        Debug.WriteLine("BtnExportToPdf_onClick: No swe pdf files were created");
                    }
                }
                else
                {
                    Debug.WriteLine("BtnExportToPdf_onClick: January SWE Map must be displayed to export other months!!");
                }



                await GeneralTools.GenerateMapsTitlePage();

                // Initialize output document
                PdfDocument outputDocument = new PdfDocument();
                //Iterate through files
                foreach (string strFileName in lstFilesToAppend)
                {
                    string fullPath = Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_MAP_PACKAGE + "\\" +
                                      strFileName;
                    if (System.IO.File.Exists(fullPath))
                    {
                        PdfDocument inputDocument = PdfReader.Open(fullPath, PdfDocumentOpenMode.Import);
                        // Iterate pages
                        int count = inputDocument.PageCount;
                        for (int idx = 0;  idx < count; idx++)
                        {
                            // Get the page from the external document...
                            PdfPage page = inputDocument.Pages[idx];
                            outputDocument.AddPage(page);
                        }
                    }
                }

                // Save final document
                string outputPath = Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_MAP_PACKAGE + "\\" +
                                      Constants.FILE_EXPORT_MAPS_ALL_PDF;
                outputDocument.Save(outputPath);
                MessageBox.Show("Map package exported to " + outputPath + "!!", "BAGIS-PRO");
            }
            catch (Exception e)
            {
                MessageBox.Show("An error occurred while trying to export the maps!! " + e.Message, "BAGIS PRO");
            }
        }
    }

    internal class BtnExcelTables : Button
    {
        protected async override void OnClick()
        {
            try
            {
                BA_ReturnCode success = await GeneralTools.GenerateTablesAsync();
            }
            catch (Exception e)
            {
                MessageBox.Show("An error occurred while trying to export the tables!! " + e.Message, "BAGIS PRO");
            }
        }
    }
}
