'use strict';
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { execFile } = require('node:child_process');
const { promisify } = require('node:util');
const { decodeConfigRows } = require('./chart-payload.cjs');
const execute = promisify(execFile);

async function unityCommand(project, args) {
  let output;
  try {
    const result = await execute(process.env.GGD_UNITY_CLI || 'unity', [...args, '--format', 'json'], {
      cwd: project, windowsHide: true, timeout: 40000, maxBuffer: 8 * 1024 * 1024,
    });
    output = result.stdout;
  } catch (error) {
    if (!error.stdout) throw Error('Unity CLI 실행 실패. 게임 담당자의 PC에서 CLI 설치와 Editor/Pipeline 연결을 확인하세요.');
    output = error.stdout;
  }
  const result = JSON.parse(output);
  if (!result.success) throw Error('Unity 연결 실패. 선택한 프로젝트의 Editor/Pipeline 상태를 확인하세요.');
  return result.data;
}

async function verifyPrepared(record, project, workspace) {
  if (!record || !/^DEV_(MissionEvents|UnitBaseStats|UnitLevelBonuses)$/.test(record.targetChartName)) {
    throw Error('지원하지 않는 DEV 대상입니다.');
  }
  if (!project || !fs.existsSync(path.join(project, 'ProjectSettings/ProjectVersion.txt'))) {
    throw Error('게임 프로젝트를 먼저 선택하세요. 서버 확인에는 Unity Editor/Pipeline과 Unity CLI, 기존 뒤끝 로그인이 필요합니다.');
  }
  const fileHash = crypto.createHash('sha256').update(fs.readFileSync(record.file)).digest('hex');
  if (fileHash !== record.sha256) throw Error('준비 파일이 변경되었습니다. 다시 준비하세요.');

  const folder = path.join(workspace, 'backend-verification');
  fs.mkdirSync(folder, { recursive: true });
  const script = path.join(folder, 'BackndChartReader.cs');
  fs.writeFileSync(script, fs.readFileSync(path.join(__dirname, 'backend-chart-reader.cs')));

  await unityCommand(project, ['command', 'set_autotick', '--enable', 'true', '--project-path', project]);
  const data = await unityCommand(project, [
    'command', 'run_script', '--project-path', project, '--file', script, '--entry', 'BackndChartReader.Run',
    '--args', JSON.stringify(['read', record.targetChartName]), '--timeout_ms', '25000', '--timeout', '35',
  ]);
  if (!data.success || !data.result?.success || typeof data.result.result !== 'string') {
    throw Error('차트를 읽지 못했습니다. 뒤끝 로그인 및 ' + record.targetChartName + '의 파일 적용 여부를 확인하세요.');
  }
  return comparePrepared(record, JSON.parse(data.result.result));
}

function comparePrepared(record, server) {
  if (server.chartName !== record.targetChartName || !server.selectedChartFileId || String(server.selectedChartFileId) === '0') {
    throw Error('조회한 차트 또는 적용 파일 ID가 올바르지 않습니다.');
  }
  if(record.encoding==='Table'){
    const [headers,...expected]=record.expectedTable;
    const normalize=rows=>rows.map(row=>JSON.stringify(row)).sort();
    const actual=server.rows.map(row=>headers.map(key=>String(row[key]??'')));
    if(JSON.stringify(normalize(actual))!==JSON.stringify(normalize(expected.map(row=>row.map(String)))))throw Error('서버 일반 표가 준비한 파일과 다릅니다.');
    return {serverFileMatches:true,gameplayVerified:false,chartName:record.targetChartName,selectedChartFileId:server.selectedChartFileId,verifiedAt:new Date().toISOString()};
  }
  const actual = decodeConfigRows(server.rows);
  if (actual.releaseKey !== record.releaseKey || actual.sha256 !== record.payloadSha256) {
    throw Error('서버 적용 파일이 현재 준비한 버전과 다릅니다.');
  }
  return {
    serverFileMatches: true, gameplayVerified: false, chartName: record.targetChartName,
    selectedChartFileId: server.selectedChartFileId, releaseKey: actual.releaseKey,
    verifiedAt: new Date().toISOString(),
  };
}

function register({ handler, workspace, getPrepared, atomic }) {
  const setting = path.join(workspace, 'backend-verification-project.json');
  handler('backend-choose-project', async () => {
    const { dialog } = require('electron');
    const choice = await dialog.showOpenDialog({
      title: '서버 조회에 사용할 Unity 게임 프로젝트', properties: ['openDirectory'],
    });
    if (choice.canceled) return null;
    const project = choice.filePaths[0];
    if (!fs.existsSync(path.join(project, 'ProjectSettings/ProjectVersion.txt'))) {
      throw Error('Unity 프로젝트 폴더를 선택하세요.');
    }
    atomic(setting, JSON.stringify({ project }));
    return { project };
  });

  let verifying = false;
  handler('content-backend-verify', async payload => {
    if (verifying) throw Error('서버 파일 확인이 진행 중입니다.');
    const record = getPrepared(payload.releaseKey);
    const project = fs.existsSync(setting) ? JSON.parse(fs.readFileSync(setting, 'utf8')).project : null;
    verifying = true;
    try {
      const result = await verifyPrepared(record, project, workspace);
      atomic(path.join(path.dirname(record.file), 'server-verification.json'), JSON.stringify(result, null, 2));
      return result;
    } finally {
      verifying = false;
    }
  });
}

module.exports = { register, verifyPrepared, comparePrepared };
