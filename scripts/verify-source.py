#!/usr/bin/env python3
"""Offline structural checks only. Does not compile C# or execute behavior tests."""
import json
import re
from pathlib import Path
from xml.etree import ElementTree as ET

root = Path(__file__).resolve().parent.parent
required = ["JitsiAdmin.OutlookPlugin.sln", "README.md", ".gitignore", ".github/workflows/windows-installer.yml",
            "scripts/ensure-office-tools.ps1", "scripts/package-offline.ps1",
            *["installer/" + name for name in ["Install.cmd", "Install.ps1", "Installer.Common.ps1", "Uninstall.cmd", "Uninstall.ps1", "LIESMICH.txt"]],
            *["docs/" + name + ".md" for name in ["api-contract", "configuration", "deployment", "manual-test-plan", "implementation-status"]]]
for name in required:
    assert (root / name).is_file(), name
for path in root.glob("config/*.json"):
    config = json.loads(path.read_text(encoding="utf-8"))
    assert config["serversHttpMethod"] == "GET" and config["createConferenceHttpMethod"] == "POST"
    assert isinstance(config["mockMode"], bool)
xml_files = [*root.glob("src/**/*.csproj"), *root.glob("tests/**/*.csproj"), *root.glob("src/**/*.resx"),
             *root.glob("src/**/*.Designer.xml"), *root.glob("src/**/App.config")]
ns = {"m": "http://schemas.microsoft.com/developer/msbuild/2003"}
for path in xml_files:
    tree = ET.parse(path)
    for node in tree.iter():
        name = node.tag.split("}")[-1]
        if name in {"Compile", "EmbeddedResource", "None", "ProjectReference"}:
            include = node.attrib.get("Include", "")
            if include and "*" not in include:
                assert (path.parent / include.replace("\\", "/")).is_file(), (path, include)
solution = (root / "JitsiAdmin.OutlookPlugin.sln").read_text()
for project in re.findall(r'"([^"\n]+\.csproj)"', solution):
    assert (root / project.replace("\\", "/")).is_file(), project
for path in root.glob("src/**/*.cs"):
    text = path.read_text()
    assert not re.search(r'\.\s*(?:Save|Send|Wait)\s*\(|\.Result\b|NotImplementedException|FinalReleaseComObject', text), path
assert "AppointmentItem.HTMLBody" not in "".join(p.read_text() for p in root.glob("src/**/*.cs"))
for path in root.glob("docs/*.md"):
    for target in re.findall(r'\]\(([^)#]+)', path.read_text()):
        if "://" not in target:
            assert (path.parent / target).exists(), (path, target)
# Ignore strings/comments before checking delimiters; this is intentionally not a C# parser.
literals = re.compile(r'//[^\n]*|/\*[\s\S]*?\*/|@"(?:[^"]|"")*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'')
csharp = [*root.glob("src/**/*.cs"), *root.glob("tests/**/*.cs")]
for path in csharp:
    stripped = literals.sub("", path.read_text())
    stack = []
    for char in stripped:
        if char in "([{":
            stack.append(char)
        elif char in ")]}":
            assert stack and stack.pop() == {")": "(", "]": "[", "}": "{"}[char], path
    assert not stack, path
for folder in ["src", "tests", "config", "docs", "scripts", "installer", ".github"]:
    for path in (root / folder).rglob("*"):
        if path.is_file() and not any(part in {"bin", "obj", "__pycache__"} for part in path.parts):
            assert all(line == line.rstrip() for line in path.read_text().splitlines()), path
# The installed offline host must not compile or reference the later API integration.
host = ET.parse(root / "src/JitsiAdmin.OutlookPlugin/JitsiAdmin.OutlookPlugin.csproj")
compiled = {n.attrib.get("Include", "") for n in host.findall(".//m:Compile", ns)}
assert "OutlookTarget.cs" not in compiled and "UiDispatcher.cs" not in compiled
assert not host.findall(".//m:ProjectReference", ns) and not host.findall(".//m:PackageReference", ns)
controller = (root / "src/JitsiAdmin.OutlookPlugin/AddInController.cs").read_text()
assert "application.CreateItem(Outlook.OlItemType.olAppointmentItem)" in controller
assert "appointment.Display(false)" in controller
assert "new MeetLinkApi" not in controller and "Configuration.Load" not in controller
for command in ["Install", "Uninstall"]:
    starter = (root / ("installer/" + command + ".cmd")).read_text()
    assert '-File "%~dp0' + command + '.ps1"' in starter
print(f"PASS: {len(xml_files)} XML-Dateien, 2 JSON-Konfigurationen, Solution-/Dateiverweise, Dokumentlinks und verbotene Quelltextaufrufe geprüft.")
print(f"PASS: Klammerbalance von {len(csharp)} C#-Dateien und nachgestellte Leerzeichen geprüft.")
print("PASS: Offline-Host ohne API-Abhängigkeiten und CMD-Installationsstarter geprüft.")
print("Dies ist KEIN C#-Build, KEIN xUnit-Testlauf und KEIN Outlook-Integrationstest.")
