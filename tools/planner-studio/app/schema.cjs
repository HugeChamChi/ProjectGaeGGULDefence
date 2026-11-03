'use strict';

const domains = [
  { id: 'endless', name: '무한모드', subtitle: '전투 시간과 보스 성장', icon: '∞', file: 'EndlessBalance.xlsx',
    destination: '뒤끝 차트', target: 'EndlessBalance', color: 'green',
    instruction: '내보낸 EndlessBalance.xlsx를 뒤끝 콘솔의 같은 이름 차트에 업로드하고 적용 파일로 선택하세요. 연결된 빌드의 다음 인게임 진입부터 사용합니다.',
    fields: [
      { id: 'StartSeconds', key: 'StartSeconds', label: '시작 시간', unit: '초', min: 0, exclusiveMin: true, max: 3.4028234663852886e38,
        description: 'NORMAL 전투를 시작할 때 주어지는 타이머입니다. 전투 중 남아 있는 시간을 직접 바꾸는 값은 아닙니다.',
        example: '150을 입력하면 2분 30초로 시작합니다.', aliases: ['시간', '타이머', '제한시간', '카운트다운', '시작', '초반'], sheet: 'EndlessBalance', cell: 'C2' },
      { id: 'HpGrowthPercent', key: 'HpGrowthPercent', label: '보스 HP 성장률', unit: '%', min: 0, max: Number.MAX_VALUE,
        description: 'NORMAL에서 성장 구간에 들어간 뒤 보스 HP가 증가하는 비율입니다. 첫 성장 라운드와 간격은 게임의 별도 설정을 따릅니다.',
        example: '13은 13%입니다. 성장 1회마다 기준 HP에 1.13을 곱합니다. 0.13을 입력하는 칸이 아닙니다.', aliases: ['HP', '체력', '피통', '성장', '복리', '난이도', '보스'], sheet: 'EndlessBalance', cell: 'D2' },
      { id: 'BreatherSeconds', key: 'BreatherSeconds', label: '쉬운 보스 처치 보너스', unit: '초', min: 0, max: 3.4028234663852886e38,
        description: '쉬운 보스를 처치한 뒤 타이머에 더하는 시간입니다. 보스의 역할 분류가 쉬운 보스로 지정되어 있을 때 사용합니다.',
        example: '15를 입력하면 쉬운 보스를 처치할 때 15초를 회복합니다.', aliases: ['시간', '회복', '보상', '쉬어감', '처치', '보너스'], sheet: 'EndlessBalance', cell: 'E2' },
      { id: 'HardSeconds', key: 'HardSeconds', label: '어려운 보스 처치 보너스', unit: '초', min: 0, max: 3.4028234663852886e38,
        description: '어려운 보스를 처치한 뒤 타이머에 더하는 시간입니다. 보스의 역할 분류가 어려운 보스로 지정되어 있을 때 사용합니다.',
        example: '30을 입력하면 어려운 보스를 처치할 때 30초를 회복합니다.', aliases: ['시간', '회복', '보상', '어려움', '처치', '보너스'], sheet: 'EndlessBalance', cell: 'F2' }
    ] },
  { id: 'account', name: '계정 성장', subtitle: '레벨 곡선과 전투 경험치', icon: '↗', file: 'AccountProgression.xlsx',
    destination: 'Unity 반영', target: 'AccountProgressionConfig', color: 'purple',
    instruction: '내보낸 AccountProgression.xlsx를 개발자에게 전달하세요. 개발자가 Data/Balance/AccountProgression.xlsx로 반영한 뒤 Unity의 Tools → Balance → Import AccountProgression.xlsx로 검증·적용합니다. 뒤끝 차트에 올리는 표가 아닙니다.',
    fields: [
      { id: 'FirstRequiredExperience', key: 'RequiredExperience[0]', label: '첫 레벨업 필요 경험치', unit: 'EXP', min: 1, max: 2147483647, integer: true,
        description: '계정 레벨 1에서 2로 올라가는 데 필요한 경험치입니다. 이후 레벨은 이 값과 레벨당 증가량으로 계산합니다. 전투 중 선택지 레벨업 경험치와 다릅니다.',
        example: '500을 입력하면 첫 계정 레벨업에 500 EXP가 필요합니다.', aliases: ['경험치', 'EXP', '레벨업', '성장', '계정', '필요량'], sheet: '안내_계정설정', cell: 'B8' },
      { id: 'RequirementStep', key: 'RequiredExperience[]', label: '레벨당 필요량 증가', unit: 'EXP', min: 0, max: 2147483647, integer: true,
        description: '계정 레벨이 하나 높아질 때마다 다음 레벨의 필요 경험치에 더하는 양입니다. 이 입력을 계산하여 게임의 레벨별 RequiredExperience 배열을 만듭니다.',
        example: '첫 필요량 500, 증가량 100이면 500 → 600 → 700 순서입니다.', aliases: ['경험치', 'EXP', '레벨', '증가량', '곡선', '계정'], sheet: '안내_계정설정', cell: 'B13' },
      { id: 'BossExperience', key: 'WinBossExperience / LoseBossExperience', label: '보스 100% 기준 경험치', unit: 'EXP', min: 0, max: 2147483647, integer: true,
        description: '보스 HP를 100% 줄였을 때의 계정 경험치입니다. 보스마다 관측된 최대 HP 감소 비율을 합산하고 전투 합계에서 한 번 내립니다. 승리·패배의 지급 여부는 가져온 표의 정책을 유지합니다.',
        example: '100일 때 보스 3마리 + 다음 보스 50%면 합계 350 EXP입니다. 실제 플레이 결과가 아닌 계산 예시입니다.', aliases: ['경험치', 'EXP', '보스', 'HP', '체력', '지급', '보상', '계정'], sheet: '안내_계정설정', cell: 'B14' }
    ] }
];

const impacts = {
  StartSeconds: '늘리면 첫 전투에 쓸 수 있는 시간이 늘어납니다. 처치 보너스와 함께 비교하세요. 보스 HP 자체는 바꾸지 않습니다.',
  HpGrowthPercent: '늘리면 뒤 라운드의 HP가 복리로 더 빠르게 커집니다. 처치 시간과 회복 시간의 균형을 함께 확인하세요. 그래프는 전투 시뮬레이션이 아닌 HP 계산입니다.',
  BreatherSeconds: '늘리면 쉬운 보스 처치 후 시간 여유가 커집니다. 실제로 시간을 벌 수 있는지는 처치에 걸린 시간에 따라 다릅니다.',
  HardSeconds: '늘리면 어려운 보스 처치 후 시간 여유가 커집니다. HP 성장률과 함께 조정하며 실제 플레이로 확인하세요.',
  FirstRequiredExperience: '늘리면 전반적인 계정 성장 속도가 느려집니다. 보스 기준 경험치가 같을 때 첫 레벨업에 더 많은 전투 진행이 필요합니다.',
  RequirementStep: '늘리면 높은 계정 레벨일수록 성장이 더 느려집니다. 첫 필요량은 유지하며 이후 필요량의 증가 폭만 바꿉니다.',
  BossExperience: '늘리면 같은 보스 진행으로 더 많은 계정 경험치를 얻습니다. 필요 경험치와 함께 비교하세요. 0이면 지급량이 0입니다.'
};
const fields = domains.flatMap(domain => domain.fields.map(field => ({ ...field, impact: impacts[field.id], uid: `${domain.id}.${field.id}`, domain: domain.id, domainName: domain.name, destination: domain.destination, target: domain.target, file: domain.file })));

function numeric(value) {
  if (typeof value === 'number') return value;
  if (typeof value !== 'string' || !/^[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:e[+-]?\d+)?$/i.test(value.trim())) return NaN;
  return Number(value);
}

function validate(values, contexts) {
  const errors = [];
  const normalized = {};
  for (const field of fields) {
    const value = numeric(values[field.uid]);
    if (!Number.isFinite(value)) errors.push({ uid: field.uid, message: `${field.label}: 숫자를 입력하세요.` });
    else if ((field.integer && !Number.isInteger(value)) || value < field.min || value > field.max || (field.exclusiveMin && value <= field.min)) {
      errors.push({ uid: field.uid, message: `${field.label}: ${field.exclusiveMin ? '0보다 큰 수' : `${field.min} 이상`}${field.integer ? '인 정수' : '의 수'}를 입력하세요.` });
    } else normalized[field.uid] = value;
  }
  if (Object.keys(normalized).length === fields.length && contexts?.account) {
    const max = contexts.account.maxLevel;
    const first = normalized['account.FirstRequiredExperience'];
    const step = normalized['account.RequirementStep'];
    const total = (max - 1) * first + ((max - 2) * (max - 1) / 2) * step;
    if (!Number.isSafeInteger(total) || total > 2147483647) errors.push({ uid: 'account.RequirementStep', message: '계정 성장: 최대 레벨까지의 누적 경험치가 게임의 정수 범위를 초과합니다.' });
    const initialRequired = first + (contexts.account.initialLevel - 1) * step;
    if (contexts.account.initialLevel < max && contexts.account.initialExperience >= initialRequired) errors.push({ uid: 'account.FirstRequiredExperience', message: '시작 경험치가 시작 레벨의 필요량보다 작아야 합니다.' });
  }
  return { ok: errors.length === 0, errors, values: normalized };
}

function search(query, domain = 'all', definitions = fields) {
  const terms = query.normalize('NFKC').toLocaleLowerCase().trim().split(/\s+/).filter(Boolean);
  return definitions.filter(field => (domain === 'all' || field.domain === domain) && terms.every(term =>
    [field.label, field.key, field.id, field.description, field.example, field.domainName, field.file, field.sheet, field.cell, ...field.aliases]
      .join(' ').normalize('NFKC').toLocaleLowerCase().includes(term)));
}

function preview(domain, values, context) {
  if (domain === 'endless') {
    const percent = numeric(values['endless.HpGrowthPercent']);
    return Array.from({ length: 40 }, (_, i) => ({ x: i + 1, y: 100 * Math.pow(1 + percent / 100, Math.max(0, i + 1 - 10)) }));
  }
  const first = numeric(values['account.FirstRequiredExperience']);
  const step = numeric(values['account.RequirementStep']);
  return Array.from({ length: Math.min(context.maxLevel - 1, 100) }, (_, i) => ({ x: i + 1, y: first + i * step }));
}

module.exports = { domains, fields, validate, numeric, search, preview };
