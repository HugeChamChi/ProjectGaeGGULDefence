"""Snapshot actual per-tier unit stats; never modify Unity assets."""
import argparse
import json,re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
seed=json.loads((ROOT/'Tools/reward-editor/project-seed.json').read_text(encoding='utf-8'))
parser=argparse.ArgumentParser()
parser.add_argument('--check', action='store_true', help='Reject stale generated unit definitions without changing files')
args=parser.parse_args()
rows=[]
bonuses=[]
fields={'Attack':'atk','AttackInterval':'attackSpeed','SkillCooldown':'skillCooldown','FoodProduction':'foodProduction','MaxDroneCount':'maxDroneCount'}
for unit in seed['tables']['Unit']:
    if unit['Kind']!='Unit':continue
    if unit['UnitId'] not in {'UnitData_Betan','UnitData_Deltan','UnitData_Gamman','UnitData_Disigman','UnitData_Zeltan'}:continue
    data=(ROOT/unit['Source']).read_text(encoding='utf-8-sig')
    for tier in ['normal','rare','epic','legend']:
        row={'BaseStatKey':'base_'+unit['UnitId'],'Tier':tier}
        for column,field in fields.items():
            block=re.search(r'^  '+field+r':\s*\n((?:    [^\n]*\n?)+)',data,re.M)
            match=re.search(r'^    '+tier+r':\s*([-+\d.eE]+)',block[1],re.M) if block else None
            if not match:raise ValueError(f'Missing {unit["UnitId"]} {field}/{tier}')
            value=float(match[1]);row[column]=int(value) if column=='MaxDroneCount' else value
        rows.append(row)
    block=re.search(r'^  LevelBonuses:\s*\n(.*?)(?=^  [A-Za-z_]|\Z)',data,re.M|re.S)
    if block:
        for match in re.finditer(r'^  - Level: (\d+)\n    Description: ([^\n]*(?:\n      [^\n]*)*)',block[1],re.M):
            text=' '.join(line.strip() for line in match[2].splitlines())
            description=json.loads(text) if text.startswith('"') else text
            bonuses.append({'UnitId':unit['UnitId'],'Level':int(match[1]),'Description':description,'Source':unit['Source']})
target=Path(__file__).resolve().parent/'app/ui/content/unit-base-seed.js'
text=('globalThis.UNIT_BASE_STATS='+json.dumps(rows,ensure_ascii=False,separators=(',',':'))+';\n'
      +'globalThis.UNIT_AUTHORED_BONUSES='+json.dumps(bonuses,ensure_ascii=False,separators=(',',':'))+';\n')
if args.check:
    if not target.exists() or target.read_text(encoding='utf-8') != text:
        raise SystemExit('Stale unit definitions. Run scan-unit-stats.py before packaging.')
else:
    target.write_text(text,encoding='utf-8')
print(f'Actual base-stat rows: {len(rows)}; authored bonus descriptions: {len(bonuses)}')
