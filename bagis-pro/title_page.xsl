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
          height: 650px;
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
          .footer {
          width: 100%;
          text-align: center;
          font-family: Arial, Helvetica, sans-serif;
          }

        </style>
  <head/>
  <body>
        <div class="style2">
            AOI: <xsl:value-of select="aoi_name"/>
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
                  <xsl:if test="comments != ''">
                    <tr>
                        <td class="style3">
                           Comments: <xsl:value-of select="comments"/>
                        </td>
                    </tr>
                  </xsl:if>
                    <tr>
                        <td class="style3">
                            Local file path to AOI: <xsl:value-of select="local_path"/>
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
                </xsl:if>
                <tr>
                  <td class="style3">
                    Delineated drainage area: <xsl:value-of select="drainage_area_sqmi"/> square miles
                  </td>
                </tr>
                <tr>
                  <td class="style3">
                    Elevation range: <xsl:value-of select="elevation_min_feet"/> to <xsl:value-of select="elevation_max_feet"/> feet
                  </td>
                </tr>
                <tr>
                  <td class="style3">
                    SNOTEL Sites: within basin – <xsl:value-of select="snotel_sites_in_basin"/>, within <xsl:value-of select="snotel_sites_buffer_size"/> mile buffer – <xsl:value-of select="snotel_sites_in_buffer"/> (SNOTEL SITE REPRESENTATION)
                  </td>
                </tr>
                <tr>
                  <td class="style3">
                    Snow Courses: within basin – <xsl:value-of select="scos_sites_in_basin"/>, within <xsl:value-of select="scos_sites_buffer_size"/> mile buffer – <xsl:value-of select="scos_sites_in_buffer"/> (SNOW COURSE SITE REPRESENTATION)
                  </td>
                </tr>
              </table>
            </div>
                   
            <div class="footer">
            Maps and Charts generated using BAGIS V3 – A tool <br />
            maintained by the NRCS National Water and Climate Center (NWCC)<br />
            and Center for Spatial Analysis &#38; Research (CSAR), <br />
            Geography, Portland State University <br />
        </div>
</body></html>
  </xsl:template>

</xsl:stylesheet>