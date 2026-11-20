"""Build a repeatable player ZIP from the verified DLL and install instructions."""
import hashlib
import pathlib
import re
import zipfile

root = pathlib.Path(__file__).resolve().parents[1]
source = (root / "src/AviassemblyFlaps.cs").read_text(encoding="utf-8")
version = re.search(r'\[BepInPlugin\("com\.andrewstack\.aviassembly\.flaps", "Flap Mod", "([0-9.]+)"\)\]', source)[1]
destination = root / "releases" / version
destination.mkdir(parents=True, exist_ok=True)
dll = (root / "AviassemblyFlaps.dll").read_bytes()
digest = hashlib.sha256(dll).hexdigest()
assert digest == (root / "SHA256.txt").read_text().split()[0].lower(), "Build and published checksum disagree"
path = "BepInEx/plugins/AviassemblyFlaps/AviassemblyFlaps.dll"
entries = {path: dll, "README.md": (root / "README.md").read_bytes()}
entries["SHA256SUMS.txt"] = "".join(hashlib.sha256(data).hexdigest()+"  "+name+"\n" for name,data in entries.items()).encode()
archive = destination / ("Flap-Mod-"+version+".zip")
with zipfile.ZipFile(archive,"w") as writer:
    for name,data in entries.items():
        info=zipfile.ZipInfo(name,(2020,1,1,0,0,0));info.external_attr=0o100644<<16
        writer.writestr(info,data,compress_type=zipfile.ZIP_DEFLATED,compresslevel=9)
with zipfile.ZipFile(archive) as reader:
    assert reader.testzip() is None and set(reader.namelist())==set(entries)
    assert all(reader.read(name)==data for name,data in entries.items())
(destination / "SHA256SUMS.txt").write_text(hashlib.sha256(archive.read_bytes()).hexdigest()+"  "+archive.name+"\n",encoding="ascii")
print(archive)
