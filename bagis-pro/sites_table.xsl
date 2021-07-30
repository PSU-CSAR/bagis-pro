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
        border-style: solid;
        border-width: 1px;
        border-color:black;
        }
        .style4
        {
        font-family: Arial, Helvetica, sans-serif;
        padding: 1px 4px;
        border-style: solid;
        border-width: 1px;
        border-color:black;
        text-align: right;
        }

      </style>
  <head/>
      <body>
        <div class="style2">
          Active Sites in <xsl:value-of select="aoi_name"/>
        </div>
        <br/>
        <div class ="style1">
          <table style="border-collapse:collapse; width=700px">
            <tr>
              <td class="style3">
                Type
              </td>
              <td class="style3">
                Name
              </td>
              <td class="style3">
                Elevation (<xsl:value-of select="site_elev_range_units"/>)
              </td>
              <td class="style3">
                Slope %
              </td>
              <td class="style3">
                Aspect &#176;
              </td>
              <td class="style3">
                Direction
              </td>
              <td class="style3">
                Latitude &#176;
              </td>
              <td class="style3">
                Longitude &#176;
              </td>
            </tr>
            <xsl:for-each select="all_sites/Site">
              <tr>
                <td class="style3">
                  <xsl:value-of select="SiteTypeText" />
                </td>
                <td class="style3">
                  <xsl:value-of select="Name" />
                </td>
                <td class="style4">
                  <xsl:value-of select="ElevationText" />
                </td>
                <td class="style4">
                  <xsl:value-of select="SlopeText" />
                </td>
                <td class="style4">
                  <xsl:value-of select="AspectText" />
                </td>
                <td class="style4">
                  <xsl:value-of select="AspectDirection" />
                </td>
                <td class="style4">
                  <xsl:value-of select="LatitudeText" />
                </td>
                <td class="style4">
                  <xsl:value-of select="LongitudeText" />
                </td>
              </tr>
            </xsl:for-each>
          </table>
        </div>
      </body>
    </html>
  </xsl:template>

</xsl:stylesheet>