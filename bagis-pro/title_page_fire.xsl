<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

  <xsl:output
     method="html"
     indent="yes"
     encoding="ISO-8859-1"/>

  <xsl:template match="/ExportTitlePage">
    <html xmlns="http://www.w3.org/1999/xhtml">
      <head>
      <style type="text/css">
        .style1
        {
        height: 800px;
        }
        .style2
        {
        font-family: Arial, Helvetica, sans-serif;
        text-align: center;
        font-size: large;
        font-weight: bold;
        }
        .style3
        {
        font-family: Arial, Helvetica, sans-serif;
        padding: 1px 4px;
        font-size: 14px;
        }
        .style5
        {
        font-family: Arial, Helvetica, sans-serif;
        padding: 1px 4px;
        font-weight: bold;
        }
        .footer 
        {
        width: 100%;
        text-align: center;
        font-family: Arial, Helvetica, sans-serif;
        font-size: 14px;
        }
        <title>
          <xsl:value-of select="report_title"/>
        </title>
      </style>
  </head>
  <body>
    <div class="style2">
      <xsl:value-of select="report_title"/>
    </div>
    <div class="style2">
      <xsl:value-of select="aoi_name"/>
    </div>
            <div class ="style1">
                <table>
                    <tr>
                        <td class="style3"/>
                    </tr>
                    <tr>
                        <td class="style3">
                            Local file path: <xsl:value-of select="local_path"/>
                        </td>
                    </tr>
                    <tr>
                        <td class="style3">
                          Exported on <xsl:value-of select="DateCreatedText"/>
                        </td>
                    </tr>
                    <tr>
                      <td class="style3">
                        For more information see the Basin Analysis Reports Users Manual at 
                        <a href="https://nwcc-nrcs.hub.arcgis.com/documents/basin-analysis-reports-users-manual">https://nwcc-nrcs.hub.arcgis.com/documents/basin-analysis-reports-users-manual</a>
                      </td>
                    </tr>
                    <tr>
                    <td class="style3">
                      <xsl:text disable-output-escaping="yes"><![CDATA[&nbsp;]]></xsl:text>
                    </td>
                  </tr>
                </table>
                <table>
                <tr>
                  <td class="style3"/>
                </tr>
                <xsl:if test="streamgage_station != ''">
                  <tr>
                    <td class="style3">
                      The boundary of a basin is the watershed delineated upstream of the streamgage station listed below:
                      <br/>
                      Streamgage station: <xsl:value-of select="streamgage_station"/>
                    </td>
                  </tr>
                  <tr>
                    <td class="style3">
                      Streamgage station name: <xsl:value-of select="streamgage_station_name"/>
                    </td>
                  </tr>
                </xsl:if>
                <tr>
                  <td class="style3">
                    Delineated drainage area: <xsl:value-of select="drainage_area_sqmi"/> square miles
                  </td>
                </tr>
                  <tr>
                    <td class="style3">
                      Missing MTBS years: <xsl:value-of select="missing_mtbs_years"/>
                    </td>
                  </tr>
                </table>
            </div>
                   
            <div class="footer">
            Report generated using BAGIS Pro – A tool <br />
            maintained by the NRCS National Water and Climate Center (NWCC)<br />
            and Center for Spatial Analysis &#38; Research (CSAR), <br />
            Geography, Portland State University <br />
        </div>
</body></html>
  </xsl:template>

</xsl:stylesheet>