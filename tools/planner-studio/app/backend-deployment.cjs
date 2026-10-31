'use strict';
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const excel = require('./content-workbook.cjs');
const { encodeConfigRows } = require('./chart-payload.cjs');
const { unitRuntime } = require('./unit-runtime.cjs');

function serializeTarget(project, kind) {
  project=excel.engine().Studio.shape(project);
  switch (kind) {
    case 'missions':
      for(const mission of project.tables.Missions)for(const reward of mission.Rewards)if(!['Gold','Diamond','Stamina'].includes(reward.itemId))throw Error('현재 게임에 지급 처리가 연결되지 않은 아이템: '+reward.itemId+'. 아이템/기능 작성용 Excel로 먼저 관리하세요.');
      excel.write(project, excel.bundles.missions);
      return { chart: 'MissionEvents', authoring: 'MissionEvents', tables: excel.bundles.missions, config: excel.toRuntime(project, excel.bundles.missions) };
    case 'unitBaseStats':
      return { chart: 'UnitBaseStats', authoring: 'UnitBalance', tables: ['Unit'], config: unitRuntime(project) };
    case 'unitBonuses':
      const errors=excel.engine().Studio.validate(project).filter(x=>x.severity==='error');
      if(errors.length)throw Error(errors[0].message);
      require('./bonus-validation.cjs').validateModel(project);
      return {chart:'UnitLevelBonuses',authoring:'UnitLevelBonuses',tables:['BonusDefinition','BonusEffect'],config:project};
    default:
      throw Error('지원하지 않는 게임 연결 대상입니다.');
  }
}

exports.register = ({ handler, workspace, atomic, exportFile, getCurrentProject }) => {
  const prepared = new Map();
  const historyFile = path.join(workspace, 'deployment-history.json');
  const history = fs.existsSync(historyFile) ? JSON.parse(fs.readFileSync(historyFile, 'utf8')) : [];
  const digest = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
  function getPrepared(releaseKey) {
    const record = prepared.get(releaseKey) || history.find(r => r.releaseKey === releaseKey);
    if (!record) throw Error('먼저 적용 파일을 준비하세요.');
    for (const [file, hash] of [[record.file, record.sha256], [record.authoringFile, record.authoringSha256], [record.sourceFile, record.sourceSha256]]) {
      if (!file || !hash || digest(file) !== hash) throw Error('배포 원본 또는 준비 파일이 변경되었습니다. 다시 준비하세요.');
    }
    return record;
  }
  handler('content-backend-history', () => history.map(record => {
    try { getPrepared(record.releaseKey); return {...record, available:true}; }
    catch (error) { return {...record, available:false, error:error.message}; }
  }));
  handler('content-backend-restore', payload => getPrepared(payload.releaseKey));

  handler('content-backend-prepare', payload => {
    const project = excel.engine().Studio.shape(payload.project || getCurrentProject());
    const { chart, config, authoring, tables } = serializeTarget(project, payload.kind);
    const authoringBytes = excel.write(project, tables);
    const releaseKey = 'dev-' + crypto.randomBytes(12).toString('hex');
    const encoding = payload.base64 === true ? 'Base64' : 'Table';
    if(payload.kind==='unitBonuses'&&encoding!=='Table')throw Error('보너스 설명은 일반 표 형식만 지원합니다. Base64를 끄세요.');
    const rows = encoding === 'Table' ? require('./readable-chart.cjs').table(config,payload.kind) : encodeConfigRows(config, releaseKey, encoding);
    const bytes = Buffer.from(excel.engine().RewardCore.xlsx(chart, rows));
    const date=new Intl.DateTimeFormat('sv-SE',{timeZone:'Asia/Seoul'}).format(new Date());
    const { file } = exportFile(chart + '_' + date + '.backend.xlsx', bytes);
    const authoringFile = path.join(path.dirname(file), authoring + '.xlsx');
    const schemaFile = path.join(path.dirname(file), authoring + '.schema.json');
    atomic(authoringFile, authoringBytes);
    atomic(schemaFile, JSON.stringify(excel.contract(tables), null, 2));
    const sourceFile = path.join(path.dirname(file), 'source-project.json');
    atomic(sourceFile, JSON.stringify(project, null, 2));
    const record = {
      sourceFile, sourceSha256: digest(sourceFile), createdAt: new Date().toISOString(),
      file, releaseKey, encoding, targetChartName: 'DEV_' + chart, kind: payload.kind,
      ...(encoding==='Table'?{expectedTable:rows}:{}),
      authoringFile, schemaFile,
      authoringSha256: crypto.createHash('sha256').update(authoringBytes).digest('hex'),
      sha256: crypto.createHash('sha256').update(bytes).digest('hex'),
      payloadSha256: rows[1][4], status: 'prepared', remoteApplied: false,
    };
    atomic(path.join(path.dirname(file), 'deployment.json'), JSON.stringify(record, null, 2));
    prepared.set(releaseKey, record);
    history.unshift(record);
    atomic(historyFile, JSON.stringify(history, null, 2));
    return record;
  });

  handler('content-backend-file', payload => {
    const record = getPrepared(payload.releaseKey);
    const actual = crypto.createHash('sha256').update(fs.readFileSync(record.file)).digest('hex');
    if (actual !== record.sha256) throw Error('준비한 파일이 변경되었습니다. 다시 준비하세요.');
    require('electron').shell.showItemInFolder(record.file);
    return { opened: true };
  });

  require('./backend-verification.cjs').register({ handler, workspace, getPrepared, atomic });
};
