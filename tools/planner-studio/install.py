"""Install only the verified app archive, preserving the runtime and user data."""
from pathlib import Path
import hashlib
import os
import shutil
import argparse

ROOT = Path(__file__).resolve().parent
parser=argparse.ArgumentParser(description='Optional update of another Planner installation; the project copy runs directly.')
parser.add_argument('--target',type=Path,required=True,help='Existing Planner installation folder')
args=parser.parse_args()
resources = args.target.resolve() / 'resources'
target = resources / 'app.asar'
source = ROOT / 'GGD-Planner/resources/app.asar'
original = ROOT / 'original-app.asar'
digest = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
backup = resources / ('app-before-content-' + digest(original)[:12] + '.asar.bak')
record = ROOT / 'installed-sha256.txt'
known = [digest(original), digest(source)]
if record.exists(): known.append(record.read_text().strip())
if digest(target) not in known:
    raise SystemExit('Installed app changed since inspection; do not overwrite it.')
if not backup.exists():
    shutil.copy2(target, backup)
pending = resources / 'app-content-update.asar.tmp'
shutil.copy2(source, pending)
os.replace(pending, target)
assert digest(target) == digest(source)
record.write_text(digest(target))
print('Installed:', target)
print('Original backup:', backup)
print('Restart the existing Planner to use the content workspace.')
