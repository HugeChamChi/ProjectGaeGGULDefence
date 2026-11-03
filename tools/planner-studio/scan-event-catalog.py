"""Export compiled game definitions as JSON; retained entry point for package.py."""
import argparse
import json
import os
import subprocess
from pathlib import Path

root = Path(__file__).resolve().parent
parser = argparse.ArgumentParser()
parser.add_argument('--check', action='store_true', help='Fail if generated catalog files are stale; do not modify them')
args = parser.parse_args()
project = root / 'catalog-export/CatalogExport.csproj'
build = subprocess.run(['dotnet', 'build', str(project), '--nologo', '--verbosity', 'quiet'], capture_output=True, encoding='utf-8', errors='replace')
if build.returncode:
    raise SystemExit(build.stdout + build.stderr)
exporter = root / 'catalog-export/bin/Debug/net9.0/CatalogExport.dll'
result = subprocess.run(['dotnet', str(exporter)], capture_output=True, check=True, encoding='utf-8')
catalog = json.loads(result.stdout)
assert catalog['version'] > 0 and catalog['data'] and catalog['hooks'], 'Compiled catalog must not be empty'

outputs = {
    root / 'app/ui/content/event-catalog.json': json.dumps(catalog, ensure_ascii=False, indent=2) + '\n',
    root / 'app/ui/content/event-catalog.js': 'globalThis.EVENT_CATALOG = ' + json.dumps(catalog, ensure_ascii=False) + ';\n',
}
for file, text in outputs.items():
    if args.check:
        if not file.exists() or file.read_text(encoding='utf-8') != text:
            raise SystemExit(f'Stale event catalog: {file}. Run scan-event-catalog.py before packaging.')
    else:
        temporary = file.with_suffix(file.suffix + '.tmp')
        temporary.write_text(text, encoding='utf-8')
        os.replace(temporary, file)
print(f'Event catalogue v{catalog["version"]}: {len(catalog["data"])} data / {len(catalog["hooks"])} execution points')
