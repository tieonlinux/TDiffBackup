import unittest
import random
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
import time
import json
import requests
from typing import Optional, Union, Collection, Set, Callable
from datetime import timedelta
from bootstrap import download_tshock
import tempfile
import asyncio
import hashlib
import bsdiff4
import threading

OptionalPath = Union[None, Path, str]

def download_map(url=r"https://github.com/TEdit/Terraria-Map-Editor/raw/5c4afae20b/tests/World%20Files%201.4.0.3/SM_Classic.wld", dest: OptionalPath=None):
    if dest is None:
        dest = "./TShock"
    dest: Path = Path(dest)
    target_file_name = f'world_{random.randint(0, 2 ** 32)}.wld'
    target = Path(dest, target_file_name)
    with urllib.request.urlopen(url) as response, target.open('wb') as f:
        shutil.copyfileobj(response, f)
    return target


def hash_bytes(b: bytes) -> int:
    m = hashlib.sha256()
    m.update(b)
    return int.from_bytes(m.digest(), "big")


class PopenBuffer(threading.Thread):
    def __init__(self, popen, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.event = threading.Event()
        self.popen: subprocess.Popen = popen
        self.buff = io.BytesIO()
        self.setDaemon(True)
        self.start()

    def run(self, *args, **kwargs):
        while not self.event.is_set():
            try:
                line = self.popen.stdout.readline()
            except:
                if self.popen.returncode is not None:
                    break
                else:
                    raise
            else:
                self.buff.write(line)

    def read(self) -> bytes:
        return self.buff.getvalue()

    def clear(self):
        self.buff = io.BytesIO()

    def stop(self):
        self.event.set()


def setup_tshock(world_path: Path, dest: OptionalPath=None):
    if dest is None:
        dest = "./TShock"
    dest: Path = Path(dest)
    start = time.monotonic()
    os.environ["TdiffOverwriteThrottleTimeSpan"] = "-1"
    if not Path(dest, "tshock", "config.json").exists():
        p: subprocess.Popen = subprocess.Popen(str(Path(dest, "TerrariaServer.exe").absolute()), cwd=dest, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, stdin=subprocess.PIPE)
        pbuff = PopenBuffer(p)
        try:
            while not Path(dest, "tshock", "config.json").exists():
                try:
                    p.wait(5)
                except (TimeoutError, subprocess.TimeoutExpired):
                    pass

                if time.monotonic() - start > timedelta(minutes=2).total_seconds():
                    raise TimeoutError("unable to get TShock ready")
        finally:
            pbuff.stop()
            p.kill()
    
            
    json_config_path = Path(dest, "tshock", "config.json")
    config = json.loads(json_config_path.read_text())
    assert "RestApiEnabled" in config
    config["RestApiEnabled"] = True
    config["EnableTokenEndpointAuthentication"] = True
    config["ApplicationRestTokens"] = {
        "TESTTOKEN": {
        "Username": "Server",
        "UserGroupName": "superadmin"
        }}
    json_config_path.write_text(json.dumps(config))
    shutil.copy2("DiffBackup/bin/Release/DiffBackup.dll", dest / "ServerPlugins")
    cmd = f'\"{(dest / "TerrariaServer.exe").absolute()}\" -ip 127.0.0.1 -port 17778 --stats-optout -world \"{world_path.absolute()}\"'
    stdout = subprocess.PIPE
    p = subprocess.Popen(shlex.split(cmd), bufsize=2**21, cwd=dest, stdout=stdout, stderr=subprocess.STDOUT, stdin=subprocess.PIPE, env=os.environ.copy())
    pbuff = PopenBuffer(p)
    while b"Server started" not in pbuff.read():
        time.sleep(1)
    else:
        pbuff.clear()
    assert p.returncode is None

    r = requests.get(f"http://127.0.0.1:{config['RestApiPort']}/tokentest?token=TESTTOKEN")
    r.raise_for_status()
    print(r.json())
    return config, p, pbuff

class IntegrationTest(unittest.TestCase):
    world_path: Path
    server_config: dict
    server_process: subprocess.Popen
    tmp_dir: tempfile.TemporaryDirectory
    pbuff: PopenBuffer

    def setUp(self):
        self.tmp_dir = tempfile.TemporaryDirectory()
        download_tshock(dest=self.tmp_dir.name)
        self.world_path = download_map(dest=self.tmp_dir.name)
        self.server_config, self.server_process, self.pbuff = setup_tshock(self.world_path, dest=self.tmp_dir.name)

    def tearDown(self):
        self.server_process.kill()
        self.server_process.wait(15)
        self.tmp_dir.cleanup()
        self.pbuff.stop()
        if self.backup_path.is_dir():
            shutil.rmtree(self.backup_path)

    
    @property
    def backup_path(self) -> Path:
        return Path(str(self.world_path) + ".backups")


    def wait_for_ouput(self, pattern: Union[bytes, Callable, re.Pattern], timeout=None, buff=b"", clear=True) -> bytes:
        def is_a_match(buff):
            if isinstance(pattern, re.Pattern):
                return re.match(pattern, buff)
            if callable(pattern):
                return pattern(buff)
            return pattern in buff

        print("wait_for_ouput")
        t = time.monotonic()
        while not is_a_match(buff):
            if time.monotonic() - t > timeout:
                raise TimeoutError()
            buff = self.pbuff.read()
        if clear:
            self.pbuff.clear()
        return buff
            

    def save_world_file(self):
        r = requests.get(f"http://127.0.0.1:{self.server_config['RestApiPort']}/v2/world/save?token=TESTTOKEN", timeout=15)
        r.raise_for_status()
        data = r.json()

        buff = self.wait_for_ouput(b'World saved', timeout=30)

        return data, buff
        
    def test_save_once_create_backup_file(self):
        self.assertFalse(self.backup_path.exists())
        self.save_world_file()
        time.sleep(10)
        self.assertTrue(self.backup_path.is_dir())
        names = list(p for p in self.backup_path.glob("**/*") if p.is_file())
        self.assertEqual(1, len(names))
        zb = names[0].read_bytes()
        b = self.world_path.read_bytes()
        self.assertEqual(b, zb)

    def test_save_twice(self):
        self.assertFalse(self.backup_path.exists())
        self.save_world_file()
        time.sleep(30)
        self.assertTrue(self.backup_path.exists())
        time.sleep(timedelta(minutes=1).total_seconds()) # this is sub optimal
        self.save_world_file()
        time.sleep(30)
        self.assertTrue(self.backup_path.is_dir())
        names = list(p for p in self.backup_path.glob("**/*") if p.is_file())
        self.assertEqual(len(names), 2)
        src_file_path, diff_file_path = sorted(names, key=lambda s: s.suffix == '.diff')
        self.assertTrue(str(src_file_path).endswith(Path(self.world_path.name).suffix), f"{src_file_path} not ends with {Path(self.world_path.name).suffix}")
        self.assertTrue(str(diff_file_path).endswith('.diff'), f"{diff_file_path} not endswith('.diff')")
        src = src_file_path.read_bytes()
        diff = diff_file_path.read_bytes()
        rebuild = bsdiff4.patch(src, diff)
        b = self.world_path.read_bytes()
        self.assertEqual(rebuild, b)
    


if __name__ == '__main__':
    unittest.main()