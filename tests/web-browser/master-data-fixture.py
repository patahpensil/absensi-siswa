"""Write a small XLSX with text cells for the browser import tests."""
import io
import json
import sys
import xml.etree.ElementTree as ET
import zipfile

sheets = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"
relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships"
package_relationships = "http://schemas.openxmlformats.org/package/2006/relationships"
content_types = "http://schemas.openxmlformats.org/package/2006/content-types"
rows = json.load(sys.stdin)
worksheet = ET.Element("worksheet", xmlns=sheets)
data = ET.SubElement(worksheet, "sheetData")
for row_index, cells in enumerate(rows, 1):
    row = ET.SubElement(data, "row", r=str(row_index))
    for column, value in enumerate(cells):
        if value is None:
            continue
        ref = f"{chr(ord('A') + column)}{row_index}"
        cell = ET.SubElement(row, "c", r=ref, t="inlineStr")
        ET.SubElement(ET.SubElement(cell, "is"), "t").text = value

buffer = io.BytesIO()
with zipfile.ZipFile(buffer, "w", zipfile.ZIP_DEFLATED) as book:
    book.writestr("[Content_Types].xml", f"""
        <Types xmlns="{content_types}">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
        </Types>
    """.strip())
    book.writestr("_rels/.rels", f"""
        <Relationships xmlns="{package_relationships}">
          <Relationship Id="rId1" Type="{relationships}/officeDocument" Target="xl/workbook.xml"/>
        </Relationships>
    """.strip())
    book.writestr("xl/workbook.xml", f"""
        <workbook xmlns="{sheets}" xmlns:r="{relationships}">
          <sheets><sheet name="Data" sheetId="1" r:id="rId1"/></sheets>
        </workbook>
    """.strip())
    book.writestr("xl/_rels/workbook.xml.rels", f"""
        <Relationships xmlns="{package_relationships}">
          <Relationship Id="rId1" Type="{relationships}/worksheet" Target="worksheets/sheet1.xml"/>
        </Relationships>
    """.strip())
    book.writestr("xl/worksheets/sheet1.xml", ET.tostring(worksheet))

sys.stdout.buffer.write(buffer.getvalue())
