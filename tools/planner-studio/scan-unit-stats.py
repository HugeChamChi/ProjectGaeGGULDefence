"""Snapshot actual per-tier unit stats; never modify Unity assets."""
import json,re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
seed=json.loads((ROOT/'Tools/reward-editor/project-seed.json').read_text(encoding='utf-8'))
rows=[]
fields={'Attack':'atk','AttackInterval':'attackSpeed','SkillCooldown':'skillCooldown','FoodProduction':'foodProduction','MaxDroneCount':'maxDroneCount'}
for unit in seed['tables']['Unit']:
    if unit['Kind']!='Unit':continue
    data=(ROOT/unit['Source']).read_text(encoding='utf-8-sig')
    for tier in ['normal','rare','epic','legend']:
        row={'BaseStatKey':'base_'+unit['UnitId'],'Tier':tier}
        for column,field in fields.items():
            block=re.search(r'^  '+field+r':\s*\n((?:    [^\n]*\n?)+)',data,re.M)
            match=re.search(r'^    '+tier+r':\s*([-+\d.eE]+)',block[1],re.M) if block else None
            if not match:raise ValueError(f'Missing {unit["UnitId"]} {field}/{tier}')
            value=float(match[1]);row[column]=int(value) if column=='MaxDroneCount' else value
        rows.append(row)
target=Path(__file__).resolve().parent/'app/ui/content/unit-base-seed.js'
target.write_text('globalThis.UNIT_BASE_STATS='+json.dumps(rows,ensure_ascii=False,separators=(',',':'))+';\n',encoding='utf-8')
print(f'Actual base-stat rows: {len(rows)}')
