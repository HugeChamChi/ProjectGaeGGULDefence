'use strict';
const model=require('./ui/content/bonus-model.js');
module.exports={headers:['UnitKey','Level','PartIndex','EffectType','Stat','ValueMode','Value','CycleId','Every','DamageBonusPercent','ChargeReductionPercent'],modes:model.modes,validate:model.validateLegacy,validateModel:model.validate};
