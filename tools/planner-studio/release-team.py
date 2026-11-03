"""Create a team ZIP from a verified runtime; omit backups and personal workbooks."""
import argparse
import hashlib
import json
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent
parser = argparse.ArgumentParser()
parser.add_argument('--runtime', type=Path, default=ROOT / 'GGD-Planner')
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args()
runtime = args.runtime.resolve()
if not (runtime / 'GGD-Planner.exe').is_file():
    raise SystemExit('Runtime missing')
if args.output.exists():
    raise SystemExit('Output already exists; choose a new release name')

files = {}
for item in runtime.iterdir():
    if item.is_file() and (item.suffix in {'.exe', '.dll', '.pak', '.dat', '.bin'} or item.name in {'LICENSE', 'LICENSES.chromium.html', 'version', 'vk_swiftshader_icd.json'}):
        files['GGD-Planner/' + item.name] = item
for file in (runtime / 'locales').glob('*.pak'):
    files['GGD-Planner/locales/' + file.name] = file
files['GGD-Planner/resources/app.asar'] = runtime / 'resources/app.asar'
# Use source snapshots, never somebody's editable runtime materials folder.
for file in (ROOT / 'app/seeds').glob('*.xlsx'):
    files['GGD-Planner/자료/' + file.name] = file
files['팀 사용 안내.md'] = ROOT / 'TEAM-GUIDE.md'
files['개발 유지보수 안내.md'] = ROOT / 'MAINTENANCE.md'
files['엑셀 작성 규약.md'] = ROOT / 'EXCEL-CONTRACT.md'
files['GGD-Planner/팀 사용 안내.md'] = ROOT / 'TEAM-GUIDE.md'
manifest = {
    'kind': 'team-validation',
    'automaticServerDeployment': False,
    'files': {name: hashlib.sha256(file.read_bytes()).hexdigest() for name, file in sorted(files.items())},
}
args.output.parent.mkdir(parents=True, exist_ok=True)
with zipfile.ZipFile(args.output, 'x', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
    for name, file in sorted(files.items()):
        archive.write(file, name)
    archive.writestr('manifest.json', json.dumps(manifest, ensure_ascii=False, indent=2))
with zipfile.ZipFile(args.output) as archive:
    for name, digest in manifest['files'].items():
        if hashlib.sha256(archive.read(name)).hexdigest() != digest:
            raise SystemExit('Archive verification failed: ' + name)
print(args.output.resolve())
