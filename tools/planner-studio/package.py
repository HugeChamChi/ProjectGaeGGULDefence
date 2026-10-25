"""Build a portable test copy with the original runtime and the extended source."""
import json
import shutil
import struct
import os
import argparse
import subprocess
import hashlib
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent
TARGET = ROOT / 'GGD-Planner'

def pack(source, target):
    header = {'files': {}}
    chunks = []
    offset = 0
    for file in sorted(source.rglob('*')):
        if not file.is_file():
            continue
        parts = file.relative_to(source).parts
        node = header
        for part in parts[:-1]:
            node = node['files'].setdefault(part, {'files': {}})
        data = file.read_bytes()
        node['files'][parts[-1]] = {'size': len(data), 'offset': str(offset)}
        chunks.append(data)
        offset += len(data)
    encoded = json.dumps(header, ensure_ascii=False, separators=(',', ':')).encode()
    payload = struct.pack('<I', len(encoded)) + encoded
    payload += b'\0' * (-len(payload) % 4)
    pickle = struct.pack('<I', len(payload)) + payload
    target.parent.mkdir(parents=True, exist_ok=True)
    temporary=target.with_suffix('.asar.tmp')
    temporary.write_bytes(struct.pack('<II', 4, len(pickle)) + pickle + b''.join(chunks))
    os.replace(temporary,target)

if __name__ == '__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--runtime',type=Path,help='Electron runtime folder, only needed on first setup')
    parser.add_argument('--output',type=Path,help='Optional isolated output folder for verification')
    args=parser.parse_args()
    if args.output: TARGET=args.output.resolve()
    runtime=args.runtime.resolve() if args.runtime else TARGET
    if not (runtime/'GGD-Planner.exe').exists():
        raise SystemExit('Runtime missing. Run package.py --runtime <existing GGD-Planner folder>.')
    TARGET.mkdir(exist_ok=True)
    for item in runtime.iterdir():
        if item.name in ('resources', '자료'):
            continue
        dest = TARGET / item.name
        if not dest.exists():
            if item.is_dir(): shutil.copytree(item, dest)
            else: shutil.copy2(item, dest)
    subprocess.run(['python',str(ROOT/'scan-unit-stats.py')],check=True)
    subprocess.run(['python',str(ROOT/'scan-event-catalog.py')],check=True)
    subprocess.run(['node',str(ROOT/'build-library.cjs')],check=True)
    manifest=json.loads((ROOT/'app/seeds/project-library-manifest.json').read_text())
    def fingerprint(file):
        digest=hashlib.sha256()
        with zipfile.ZipFile(file) as archive:
            for name in sorted(archive.namelist()):
                digest.update((name+'\0').encode());digest.update(archive.read(name))
        return digest.hexdigest()
    # Install the matching parser before refreshing workbook layouts.
    pack(ROOT / 'app', TARGET / 'resources/app.asar')
    # Preserve user edits; refresh only known generated workbook snapshots.
    (TARGET / '자료').mkdir(exist_ok=True)
    for file in (ROOT / 'app/seeds').glob('*.xlsx'):
        destination=TARGET/'자료'/file.name
        if not destination.exists(): shutil.copy2(file,destination)
        elif file.name in manifest['files']:
            info=manifest['files'][file.name]
            try: existing=fingerprint(destination)
            except zipfile.BadZipFile: existing=None
            if existing in [info['fingerprint'],*info['previousFingerprints']]:shutil.copy2(file,destination)
            else:
                alternative=destination.with_name(destination.stem+'-server.xlsx')
                if not alternative.exists():shutil.copy2(file,alternative)
    print(TARGET / 'GGD-Planner.exe')
