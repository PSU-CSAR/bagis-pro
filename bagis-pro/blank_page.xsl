<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

  <xsl:output
     method="html"
     indent="yes"
     encoding="ISO-8859-1"/>

  <xsl:template match="/BlankPage">
    <html xmlns="http://www.w3.org/1999/xhtml">
      <head>
      <style type="text/css">
        .style1
        {
        height: 150px;
        }
        .style2
        {
        font-family: Arial, Helvetica, sans-serif;
        text-align: center;
        font-size: large;
        font-weight: bold;
        }

      </style>
        <title>Blank Page</title>
  </head>
      <body>
        <div class = "style1">&#160;</div>
        <div class="style2">
          <xsl:value-of select="page_content"/><br />
          Data are unavailable to render the content on this page <br />
          THIS PAGE INTENTIONALLY LEFT BLANK
        </div>
      </body>
    </html>
  </xsl:template>

</xsl:stylesheet>