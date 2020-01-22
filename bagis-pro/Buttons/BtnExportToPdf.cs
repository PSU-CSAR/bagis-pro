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

                IList<string> lstMapButtons = new List<string>{ "bagis_pro_Buttons_MapButtonPalette_BtnElevation",
                                                                "bagis_pro_Buttons_MapButtonPalette_BtnSnotel",
                                                                "bagis_pro_Buttons_MapButtonPalette_BtnSnowCourse",
                                                                "bagis_pro_Buttons_MapButtonPalette_BtnAspect",
                                                                "bagis_pro_Buttons_MapButtonPalette_BtnSlope"};
                foreach(string strMapButton in lstMapButtons)
                {
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

                    await GeneralTools.ExportMapToPdf();    // export each map to pdf
                }
                await GeneralTools.GenerateMapsTitlePage();

                IList<string> lstFilesToAppend = new List<string>{ Constants.FILE_TITLE_PAGE_PDF,
                                                                   Constants.FILE_EXPORT_MAP_ELEV_PDF,
                                                                   Constants.FILE_EXPORT_MAP_SNOTEL_PDF,
                                                                   Constants.FILE_EXPORT_MAP_SCOS_PDF,
                                                                   Constants.FILE_EXPORT_MAP_ASPECT_PDF,
                                                                   Constants.FILE_EXPORT_MAP_SLOPE_PDF};

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
            }
            catch (Exception e)
            {
                MessageBox.Show("An error occurred while trying to export the maps!! " + e.Message, "BAGIS PRO");
            }
        }
    }

 
}
