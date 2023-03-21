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
        font-weight: bold;
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
    <div class ="style1">
              <table>
                <tr>
                  <td class="style3">
                    Data sources:
                  </td>
                </tr>
                <xsl:for-each select="data_sources/DataSource">
                  <tr>
                    <td class="style3">
                      <xsl:value-of select="heading" />
                    </td>
                  </tr>
                    <tr>
                      <td class="style4">
                        <xsl:value-of select="description" />
                        <br/>
                        Clipped from: <xsl:value-of select="uri" />
                        <xsl:if test="DateClippedText != 'January 1, 0001'">
                        <br/>
                        Clipped on: <xsl:value-of select="DateClippedText" />
                        </xsl:if>
                      </td>
                    </tr>
                </xsl:for-each>
                <tr>
                  <td class="style3">
                    Station Runoff Volume
                  </td>
                </tr>
                <tr>
                  <td class="style4">
                    <xsl:value-of select="annual_runoff_data_descr" />
                  </td>
                </tr>
              </table>
            </div>
</body></html>
  </xsl:template>

</xsl:stylesheet>