from __future__ import annotations

import base64
import zlib
from pathlib import Path
from urllib.parse import quote, unquote
from xml.etree import ElementTree as ET


DOCS = Path(__file__).resolve().parents[1]
SOURCE = DOCS / "AIVES-AWS-Style-Architecture.drawio"
OUTPUT = DOCS / "AIVES-System-Architecture-remake.drawio"


def geometry(cell: ET.Element) -> ET.Element:
    node = cell.find("mxGeometry")
    if node is None:
        node = ET.SubElement(cell, "mxGeometry", {"as": "geometry"})
    return node


def add_vertex(root: ET.Element, cell_id: str, value: str, style: str,
               x: int, y: int, width: int, height: int) -> ET.Element:
    cell = ET.SubElement(root, "mxCell", {
        "id": cell_id, "value": value, "style": style,
        "vertex": "1", "parent": "1",
    })
    ET.SubElement(cell, "mxGeometry", {
        "x": str(x), "y": str(y), "width": str(width), "height": str(height),
        "as": "geometry",
    })
    return cell


def add_edge(root: ET.Element, cell_id: str, source: str, target: str,
             label: str, *, response: bool = False, anchors: str = "") -> ET.Element:
    style = (
        "edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;"
        "html=1;strokeWidth=2;strokeColor=#333333;endArrow=classic;endFill=1;"
        "fontSize=13;fontColor=#222222;labelBackgroundColor=#FFFFFF;"
        "verticalAlign=bottom;"
    )
    if response:
        style += "dashed=1;dashPattern=8 5;"
    if anchors:
        style += anchors
    cell = ET.SubElement(root, "mxCell", {
        "id": cell_id, "value": label, "style": style,
        "edge": "1", "parent": "1", "source": source, "target": target,
    })
    ET.SubElement(cell, "mxGeometry", {"relative": "1", "as": "geometry"})
    return cell


def add_embedded_icon(root: ET.Element, cell_id: str, filename: str,
                      x: int, y: int, width: int, height: int) -> ET.Element:
    asset = DOCS / "architecture_icons" / filename
    if asset.suffix.lower() == ".png":
        png = asset.read_bytes()
        png_data = base64.b64encode(png).decode("ascii")
        svg = (
            '<svg xmlns="http://www.w3.org/2000/svg" '
            'xmlns:xlink="http://www.w3.org/1999/xlink" '
            'width="64" height="64" viewBox="0 0 64 64">'
            '<image x="0" y="0" width="64" height="64" '
            'preserveAspectRatio="xMidYMid meet" '
            f'href="data:image/png;base64,{png_data}" '
            f'xlink:href="data:image/png;base64,{png_data}"/>'
            '</svg>'
        )
        source = f"data:image/svg+xml,{quote(svg, safe='')}"
    else:
        svg = asset.read_text(encoding="utf-8")
        source = f"data:image/svg+xml,{quote(svg, safe='')}"
    style = f"shape=image;aspect=fixed;html=1;image={source};"
    return add_vertex(root, cell_id, "", style, x, y, width, height)


def main() -> None:
    old_mx = ET.parse(SOURCE).getroot()
    diagram = old_mx.find("diagram")
    if diagram is None or not diagram.text:
        raise ValueError(f"No diagram payload in {SOURCE}")

    compressed = base64.b64decode(diagram.text.strip())
    decoded = unquote(zlib.decompress(compressed, -15).decode("utf-8"))
    model = ET.fromstring(decoded)
    graph_root = model.find("root")
    if graph_root is None:
        raise ValueError("Source diagram has no mxGraphModel/root")
    cells = {cell.get("id"): cell for cell in graph_root.findall("mxCell")}

    # Retain the original icon artwork, containers, typography, and overall layout.
    # Expand the third-party boundary to add the speech service beside Gemini.
    cells["title"].set("value", "AIVES SYSTEM ARCHITECTURE")
    for cell in graph_root.findall("mxCell"):
        value = cell.get("value")
        if value:
            value = value.replace("\ufffd", " - ").replace("\u00b7", " | ")
            cell.set("value", value)
            style = cell.get("style", "")
            if "fontFamily=" not in style:
                cell.set("style", style + "fontFamily=Arial;")
    third_party = cells["gemini-box"]
    third_party.set("value", "")
    geo = geometry(third_party)
    geo.set("height", "420")
    geometry(cells["database"]).set("x", "1070")
    geometry(cells["sql-logo"]).set("x", "1160")
    geometry(cells["sql-label"]).set("x", "1100")
    geometry(cells["thirdparty-title"]).set("x", "1075")

    frontend = cells["frontend"]
    geometry(frontend).set("height", "280")
    frontend_card_style = (
        "rounded=1;whiteSpace=wrap;html=1;arcSize=10;strokeWidth=1;"
        "strokeColor=#999999;fillColor=#FFFFFF;"
    )
    add_vertex(graph_root, "axios-card", "", frontend_card_style, 48, 548, 127, 62)
    add_embedded_icon(graph_root, "axios-icon", "axios.svg", 54, 561, 34, 34)
    add_vertex(graph_root, "axios-label", "Axios REST<br>client",
               "text;html=1;align=center;verticalAlign=middle;fontSize=12;fontColor=#222222;fontFamily=Arial;",
               88, 556, 82, 46)
    add_vertex(graph_root, "router-card", "", frontend_card_style, 184, 548, 127, 62)
    add_embedded_icon(graph_root, "router-icon", "reactrouter.svg", 190, 561, 34, 34)
    add_vertex(graph_root, "router-label", "React<br>Router",
               "text;html=1;align=center;verticalAlign=middle;fontSize=12;fontColor=#222222;fontFamily=Arial;",
               224, 556, 82, 46)

    # Turn the existing validation/auth labels into icon cards and add the
    # remaining backend libraries as functional pictograms (no fake vendor logos).
    backend_card_style = (
        "rounded=1;whiteSpace=wrap;html=1;arcSize=10;strokeWidth=1;"
        "strokeColor=#999999;fillColor=#FFFFFF;"
    )
    backend_cards = [
        ("fluent-label", "FluentValidation", 450, 435, "fluent-icon", "fluentvalidation.png"),
        ("jwt-label", "JWT Bearer Auth", 700, 435, "jwt-icon", "jwt.svg"),
    ]
    for cell_id, label, x, y, icon_id, icon_file in backend_cards:
        cell = cells[cell_id]
        cell.set("value", "")
        cell.set("style", backend_card_style)
        geo = geometry(cell)
        geo.set("x", str(x)); geo.set("y", str(y)); geo.set("width", "230"); geo.set("height", "62")
        add_embedded_icon(graph_root, icon_id, icon_file, x + 10, y + 11, 40, 40)
        add_vertex(graph_root, icon_id + "-label", label,
                   "text;html=1;align=center;verticalAlign=middle;fontSize=13;fontColor=#222222;fontFamily=Arial;",
                   x + 55, y + 10, 165, 42)

    extra_backend = [
        ("automapper", "AutoMapper", 450, 512, "automapper.png"),
        ("signalr", "SignalR Hub (server)", 700, 512, "signalr.svg"),
    ]
    for key, label, x, y, icon_file in extra_backend:
        add_vertex(graph_root, key + "-card", "", backend_card_style, x, y, 230, 62)
        add_embedded_icon(graph_root, key + "-icon", icon_file, x + 10, y + 11, 40, 40)
        add_vertex(graph_root, key + "-label", label,
                   "text;html=1;align=center;verticalAlign=middle;fontSize=13;fontColor=#222222;fontFamily=Arial;",
                   x + 55, y + 10, 165, 42)

    add_vertex(
        graph_root, "gemini-card", "",
        "rounded=1;whiteSpace=wrap;html=1;arcSize=12;strokeWidth=1;strokeColor=#777777;fillColor=none;fillOpacity=0;",
        1080, 190, 230, 165,
    )
    # Move the existing Gemini icon/label inside its service card while retaining its SVG.
    geometry(cells["gemini-logo"]).set("y", "198")
    geometry(cells["gemini-label"]).set("x", "1090")
    geometry(cells["gemini-label"]).set("y", "274")
    geometry(cells["gemini-label"]).set("width", "210")
    add_vertex(
        graph_root, "gemini-purpose",
        "Question generation | Interview follow-up<br>AI grading",
        "text;html=1;whiteSpace=wrap;align=center;verticalAlign=middle;fontSize=11;fontColor=#444444;fontFamily=Arial;",
        1085, 309, 220, 38,
    )

    add_vertex(
        graph_root, "speech-card", "",
        "rounded=1;whiteSpace=wrap;html=1;arcSize=12;strokeWidth=1;strokeColor=#777777;fillColor=none;fillOpacity=0;",
        1080, 372, 230, 160,
    )
    add_embedded_icon(graph_root, "speech-icon", "azure_speech.svg", 1161, 382, 68, 68)
    add_vertex(
        graph_root, "speech-label", "Azure AI Speech",
        "text;html=1;align=center;verticalAlign=middle;fontSize=16;fontStyle=1;fontColor=#222222;",
        1090, 449, 210, 30,
    )
    add_vertex(
        graph_root, "speech-purpose", "Speech-to-Text · Text-to-Speech",
        "text;html=1;align=center;verticalAlign=middle;fontSize=12;fontColor=#444444;",
        1090, 480, 210, 30,
    )

    # Replace the old bidirectional single connectors with one labeled arrow per direction.
    for cell in list(graph_root.findall("mxCell")):
        if cell.get("id", "").startswith("edge-"):
            graph_root.remove(cell)

    add_edge(graph_root, "fe-be-request", "frontend", "backend", "Request",
             anchors="exitX=1;exitY=0.46;entryX=0;entryY=0.62;")
    add_edge(graph_root, "be-fe-response", "backend", "frontend", "Response", response=True,
             anchors="exitX=0;exitY=0.72;entryX=1;entryY=0.67;")
    add_edge(graph_root, "be-gemini-request", "backend", "gemini-card", "Request",
             anchors="exitX=1;exitY=0.26;entryX=0;entryY=0.42;")
    add_edge(graph_root, "gemini-be-response", "gemini-card", "backend", "Response", response=True,
             anchors="exitX=0;exitY=0.78;entryX=1;entryY=0.34;")
    add_edge(graph_root, "be-speech-request", "backend", "speech-card", "Request",
             anchors="exitX=1;exitY=0.58;entryX=0;entryY=0.30;")
    add_edge(graph_root, "speech-be-response", "speech-card", "backend", "Response", response=True,
             anchors="exitX=0;exitY=0.78;entryX=1;entryY=0.72;")
    add_edge(graph_root, "be-sql-request", "backend", "database", "SQL request",
             anchors="exitX=0.78;exitY=1;entryX=0;entryY=0.42;")
    add_edge(graph_root, "sql-be-response", "database", "backend", "DB response", response=True,
             anchors="exitX=0;exitY=0.72;entryX=0.62;entryY=1;")

    # Protocols are stated once, keeping the connection labels short and readable.
    add_vertex(
        graph_root, "request-response-legend",
        "Solid arrow: Request · Dashed arrow: Response    REST/HTTPS + JWT    Gemini/Speech: HTTPS    Database: SQL over TLS",
        "text;html=1;align=center;verticalAlign=middle;fontSize=12;fontColor=#333333;",
        35, 838, 1320, 28,
    )

    for cell in graph_root.findall("mxCell"):
        value = cell.get("value")
        if value:
            cell.set("value", value.replace("\ufffd", " - ").replace("\u00b7", " | "))
            style = cell.get("style", "")
            if "fontFamily=" not in style:
                cell.set("style", style + "fontFamily=Arial;")
    cells["title"].set("value", "AIVES SYSTEM ARCHITECTURE")

    model.set("dy", "900")
    model.set("pageHeight", "900")
    old_mx.attrib.pop("compressed", None)
    diagram.clear()
    diagram.attrib["id"] = "aives-system-architecture-remake"
    diagram.attrib["name"] = "AIVES System Architecture (remake)"
    diagram.append(model)
    old_mx.attrib["host"] = "app.diagrams.net"
    old_mx.attrib["modified"] = "2026-11-01T00:00:00.000Z"
    ET.ElementTree(old_mx).write(OUTPUT, encoding="utf-8", xml_declaration=True)
    print(f"Wrote {OUTPUT}")


if __name__ == "__main__":
    main()
