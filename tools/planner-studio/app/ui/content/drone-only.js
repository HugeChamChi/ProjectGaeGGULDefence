'use strict';
globalThis.DroneOnlyFeature=({studio})=>{
 const allowed=new Set(['UnitData_Betan','UnitData_Deltan','UnitData_Gamman','UnitData_Disigman','UnitData_Zeltan']);
 function trim(p){
  p.tables.Unit=p.tables.Unit.filter(u=>allowed.has(u.UnitId)||allowed.has(u.UnitKey)||allowed.has(String(u.Source||'').split(/[\\/]/).pop().replace(/\.asset$/,''))||u.UnitId==='AlphanChief');
  const ids=new Set(p.tables.Unit.map(u=>u.UnitId)),bases=new Set(p.tables.Unit.map(u=>u.BaseStatKey));
  for(const name of ['UnitUpgrade','UnitMilestone'])if(p.tables[name])p.tables[name]=p.tables[name].filter(r=>ids.has(r.UnitId));
  if(p.tables.UnitBaseStat)p.tables.UnitBaseStat=p.tables.UnitBaseStat.filter(r=>bases.has(r.BaseStatKey));
  if(p.tables.BonusDefinition){const excluded=new Set(p.tables.BonusDefinition.filter(r=>!allowed.has(r.UnitKey)&&r.UnitKey!=='AlphanChief').map(r=>r.BonusId));p.tables.BonusDefinition=p.tables.BonusDefinition.filter(r=>!excluded.has(r.BonusId));p.tables.BonusEffect=p.tables.BonusEffect.filter(r=>!excluded.has(r.BonusId));}
  else if(p.tables.BonusEffect)p.tables.BonusEffect=p.tables.BonusEffect.filter(r=>allowed.has(r.UnitKey)||r.UnitKey==='AlphanChief');
  if(p.tables.BonusDescription)p.tables.BonusDescription=p.tables.BonusDescription.filter(r=>allowed.has(r.UnitKey)||r.UnitKey==='AlphanChief');
  for(const name of ['UnitMilestone','BonusDescription'])for(const row of p.tables[name]||[])if(typeof row.Description==='string')row.Description=row.Description.replaceAll('%p','%');
  p.tables.Party=p.tables.Party.filter(r=>r.PartyId==='Dron_Party').map(r=>({...r,Units:r.Units.filter(id=>ids.has(id))}));
  return p;
 }
 trim(STUDIO_SEED);
 return {prepare(p){return trim(studio.clone(p));},decorate:trim};
};
