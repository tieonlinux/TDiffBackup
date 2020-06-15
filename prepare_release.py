import os
import sys
import re
from pathlib import Path
import datetime

def update_assembly_info(path: Path, version: str, encoding='utf-8'):
    content = path.read_text(encoding=encoding)
    content = re.sub(r"""^s*\[\s*assembly\s*:\s*AssemblyVersion\s*\(*.*\)\s*\]\s*$""", f'''[assembly: AssemblyVersion("{version}")]''', content, flags=re.MULTILINE | re.IGNORECASE)
    content = re.sub(r"""^s*\[\s*assembly\s*:\s*AssemblyFileVersion\s*\(*.*\)\s*\]\s*$""", f'''[assembly: AssemblyFileVersion("{version}")]''', content, flags=re.MULTILINE | re.IGNORECASE)
    content = re.sub(r"""^s*\[\s*assembly\s*:\s*AssemblyCopyright\s*\(*.*\)\s*\]\s*$""", f'''[assembly: AssemblyCopyright("Copyright © Tieonlinux {datetime.datetime.now().year}")]''', content, flags=re.MULTILINE | re.IGNORECASE)
    path.write_text(content, encoding=encoding)


def update_assemblies(version: str):
    for path in Path().glob("**/AssemblyInfo.cs"):
        update_assembly_info(path, version)


if __name__ == "__main__":
    version = sys.argv[1]
    if version.startswith("refs/tags/"):
        version = version[len("refs/tags/"):]
    version = version.lstrip("v")
    version = tuple(int(n) for n in version.split("."))
    if len(version) < 4:
        version = version + (0,) * (4 - len(version))
    version = ".".join(map(str, version))
    update_assemblies(version)