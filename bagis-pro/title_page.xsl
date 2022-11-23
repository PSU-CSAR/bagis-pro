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
        height: 600px;
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
        }
        .style4
        {
        font-family: Arial, Helvetica, sans-serif;
        font-size: 95%;
        padding-top: 1px;
        padding-left: 10px;
        }
        .style5
        {
        font-family: Arial, Helvetica, sans-serif;
        padding: 1px 4px;
        font-weight: bold;
        }
        .footer {
        width: 100%;
        text-align: center;
        font-family: Arial, Helvetica, sans-serif;
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
                  <xsl:if test="publisher != ''">
                    <tr>
                        <td class="style3">
                            Publisher: <xsl:value-of select="publisher"/>
                        </td>
                    </tr>
                  </xsl:if>
                    <tr>
                        <td class="style3">
                           Comments: <xsl:value-of select="comments"/>
                        </td>
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
                      The boundary of a basin is the watershed delineated upstream of the stream gage station listed below:
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
                      Annual runoff ratio: <xsl:value-of select="annual_runoff_ratio"/> (Annual runoff ratio is calculated as the volume of potential annual runoff from
                      precipitation divided by the normal annual runoff observed at the stream gage.)
                    </td>
                  </tr>
                <tr>
                  <td class="style3">
                    Elevation range: <xsl:value-of select="elevation_min_feet"/> to <xsl:value-of select="elevation_max_feet"/> feet
                  </td>
                </tr>
                <tr>
                  <td class="style3">
                    Automated Sites: within basin – <xsl:value-of select="snotel_sites_in_basin"/>; within a <xsl:value-of select="snotel_sites_buffer_size"/> buffer distance outside 
                    of basin – <xsl:value-of select="snotel_sites_in_buffer"/>
                    <xsl:if test="has_snotel_sites = 'true'"> (See AUTOMATED SITE REPRESENTATION map)</xsl:if>
                  </td>
                </tr>
                <tr>
                  <td class="style3">
                    Snow Courses: within basin – <xsl:value-of select="scos_sites_in_basin"/>; within a <xsl:value-of select="scos_sites_buffer_size"/> buffer distance outside 
                    of basin – <xsl:value-of select="scos_sites_in_buffer"/> 
                    <xsl:if test="has_scos_sites = 'true'"> (See SNOW COURSE SITE REPRESENTATION map)</xsl:if>
                  </td>
                </tr>
              </table>
              <table>
                <tr>
                  <td class="style5">&#160;</td>
                </tr>
                <tr>
                  <td class="style5">&#160;</td>
                </tr>
                <tr>
                  <td class="style5">Network Representation Analysis</td>
                </tr>
                  <tr>
                    <td class="style3">
                      Site representation definition: within <xsl:value-of select="site_buffer_dist"/>&#160;<xsl:value-of select="site_buffer_dist_units"/> planar radius, 
                      with <xsl:value-of select="site_elev_range"/>&#160;<xsl:value-of select="site_elev_range_units"/> above and below site elevation.
                    </td>
                  </tr>
                <tr>
                  <td class="style3">
                    Percent of total basin represented by automated site(s): <xsl:value-of select="represented_snotel_percent"/>% 
                    <xsl:if test="has_snotel_sites = 'true'"> (See AUTOMATED SITE REPRESENTATION map)</xsl:if>
                  </td>
                </tr>
                <tr>
                  <td class="style3">
                    Percent of total basin area represented by snow course site(s): <xsl:value-of select="represented_snow_course_percent"/>%
                    <xsl:if test="has_scos_sites = 'true'"> (See SNOW COURSE SITE REPRESENTATION map)</xsl:if>
                  </td>
                </tr>                
                <tr>
                  <td class="style3">
                    Percent of total basin area represented by both automated and
                    snow course site(s): <xsl:value-of select="represented_all_sites_percent"/>%
                    <xsl:if test="has_scos_sites = 'true' and has_snotel_sites = 'true'"> (See ALL SITE REPRESENTATION map)</xsl:if>
                  </td>
                </tr>
                <tr>
                  <td class="style5">&#160;</td>
                </tr>
                <tr>
                  <td class="style5">&#160;</td>
                </tr>
                <tr>
                  <td class="style5">Potential Site Location Analysis</td>
                </tr>
                <tr>
                  <td class="style3">
                    Potential site location meets the following criteria:
                  </td>
                </tr>
                <tr>
                  <td class="style3" style="padding-left: 20px">
                      <li>on federal non-wilderness or tribal land, AND</li>
                      <li>on deciduous, evergreen, and mixed forested land, AND</li>
                      <li>
                        within <xsl:value-of select="roads_buffer"/> of access roads
                      </li>
                  </td>
                </tr>
              </table>
            </div>
                   
            <div class="footer">
            Report generated using BAGIS V3 – A tool <br />
            maintained by the NRCS National Water and Climate Center (NWCC)<br />
            and Center for Spatial Analysis &#38; Research (CSAR), <br />
            Geography, Portland State University <br />
        </div>
</body></html>
  </xsl:template>

</xsl:stylesheet>