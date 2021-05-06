<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

  <xsl:output
     method="html"
     indent="yes"
     encoding="ISO-8859-1"/>

  <xsl:template match="/ExportTitlePage">
    <html xmlns="http://www.w3.org/1999/xhtml">

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
        .footer {
        width: 100%;
        text-align: center;
        font-family: Arial, Helvetica, sans-serif;
        }

      </style>
  <head/>
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
                      Annual runoff ratio: <xsl:value-of select="annual_runoff_ratio"/>
                    </td>
                  </tr>
                <tr>
                  <td class="style3">
                    Elevation range: <xsl:value-of select="elevation_min_feet"/> to <xsl:value-of select="elevation_max_feet"/> feet
                  </td>
                </tr>
                <tr>
                  <td class="style3">
                    SNOTEL Sites: within basin – <xsl:value-of select="snotel_sites_in_basin"/>, outside basin – <xsl:value-of select="snotel_sites_in_buffer"/>
                    <xsl:if test="has_snotel_sites = 'true'"> (See SNOTEL SITES REPRESENTATION map)</xsl:if>
                  </td>
                </tr>
                <tr>
                  <td class="style3">
                    Snow Courses: within basin – <xsl:value-of select="scos_sites_in_basin"/>, outside basin – <xsl:value-of select="scos_sites_in_buffer"/> 
                    <xsl:if test="has_scos_sites = 'true'"> (See SNOW COURSE SITES REPRESENTATION map)</xsl:if>
                  </td>
                </tr>
              </table>
              <table>
                <tr>
                  <td class="style3"/>
                </tr>
                <tr>
                  <td class="style3">Network Representation And Location Analysis</td>
                </tr>
                  <tr>
                    <td class="style3">
                      Site representation definition: within <xsl:value-of select="site_buffer_dist"/>&#160;<xsl:value-of select="site_buffer_dist_units"/> planar radius, 
                      with <xsl:value-of select="site_elev_range"/>&#160;<xsl:value-of select="site_elev_range_units"/> above and below site elevation.
                    </td>
                  </tr>
                <tr>
                  <td class="style3">
                    Represented by SNOTEL site(s): <xsl:value-of select="represented_snotel_percent"/>% 
                    <xsl:if test="has_snotel_sites = 'true'"> (See SNOTEL SITES REPRESENTATION map)</xsl:if>
                  </td>
                </tr>
                <tr>
                  <td class="style3">
                    Represented by snow course site(s): <xsl:value-of select="represented_snow_course_percent"/>%
                    <xsl:if test="has_scos_sites = 'true'"> (See SNOW COURSE SITES REPRESENTATION map)</xsl:if>
                  </td>
                </tr>                
                <tr>
                  <td class="style3">
                    Represented by both SNOTEL and snow course site(s): <xsl:value-of select="represented_all_sites_percent"/>%
                    <xsl:if test="has_scos_sites = 'true' and has_snotel_sites = 'true'"> (See SNOTEL AND SNOW COURSE SITES REPRESENTATION map)</xsl:if>
                  </td>
                </tr>
              </table>
              <table>
                <tr>
                  <td class="style3">
                    Data sources:
                  </td>
                </tr>
                <xsl:for-each select="data_sources/DataSource">
                    <tr>
                      <td class="style4">
                        <xsl:value-of select="description" />
                        <br/>
                        Clipped from: <xsl:value-of select="uri" />
                        <br/>
                        Clipped on: <xsl:value-of select="DateClippedText" />
                      </td>
                    </tr>
                </xsl:for-each>
                <tr>
                  <td class="style4">
                    <xsl:value-of select="annual_runoff_data_descr" />
                    <br/>
                    Data provided by NWCC based on AWDB data in <xsl:value-of select="annual_runoff_data_year" />
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