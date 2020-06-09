import subprocess
import os
import sys
from pathlib import Path
import urllib.request
import zipfile
import io
import shutil
import shlex
import re
from typing import Optional, Union, Collection, Set

OptionalPath = Union[None, Path, str]


def download_tshock(url=r"https://github.com/Pryaxis/TShock/releases/download/v4.4.0-pre11/TShock4.4.0_Pre11_Terraria1.4.0.5.zip", dest: OptionalPath=None):
    if dest is None:
        dest = "./TShock"
    dest: Path = Path(dest)
    with urllib.request.urlopen(url) as response:
        buff = io.BytesIO()
        shutil.copyfileobj(response, buff)
        buff.seek(0)
        with zipfile.ZipFile(buff) as zf:
            dest.mkdir(exist_ok=True)
            zf.extractall(dest)


def submodule_init(name: str, remote_url:str, cwd: OptionalPath='./', version='latest'):
    if cwd is None:
        cwd = './'
    cwd: Path = Path(cwd)
    
    cmd = f'''git.exe submodule add  --force -- "{remote_url}" "{name}"'''
    subprocess.check_call(shlex.split(cmd), cwd=str(cwd))
    if version != 'latest':
        cmd = f"git.exe checkout -f {version} --"
        subprocess.check_call(shlex.split(cmd), cwd=str(cwd / name))


def submodule_rename_namespace(name: str, namespaces: Collection[str], cwd: OptionalPath='./', suffix: str='tie'):
    if cwd is None:
        cwd = './'
    cwd: Path = Path(cwd)
    submodule: Path = Path(cwd, name)
    
    modified: Set[Path] = set()

    for path in submodule.glob("**/*.cs"):
        source: str = path.read_text()
        original = source
        for namespace in namespaces:
            for ns_prefix in ('namespace', 'using'):
                source = re.sub(rf'({re.escape(ns_prefix)}\s+{re.escape(namespace)})', rf'\1_{suffix}', source)
        if original != source:
            path.write_text(source)
            modified.add(path.relative_to(submodule))
    if modified:
        cmd = 'git update-index --assume-unchanged -- '
        cmd += " ".join(map(lambda path: str(path).replace(os.sep, '/'), modified))
        subprocess.check_call(shlex.split(cmd), cwd=str(submodule))
    return modified


def deltaq_init(name: str='deltaq', remote_url="https://github.com/jzebedee/deltaq.git", version='v1.1', namespaces=('deltaq', 'bz2core')):
    submodule_init(name=name, remote_url=remote_url, version=version)
    submodule_rename_namespace(name=name, namespaces=namespaces)

def human_date_parser_init(name: str='HumanDateParser', remote_url="https://github.com/ti-ka/human-date-parser.git", version='latest', namespaces=('HumanDateParser',)):
    submodule_init(name=name, remote_url=remote_url, version=version)
    submodule_rename_namespace(name=name, namespaces=namespaces)

if __name__ == "__main__":
    download_tshock()
    deltaq_init()
    human_date_parser_init()
