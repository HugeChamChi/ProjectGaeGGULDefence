import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { projectRoot } from './balance.mjs';

const execute = promisify(execFile);
export async function unity(args) {
  let stdout;
  try {
    ({ stdout } = await execute(process.env.GGD_UNITY_CLI || 'unity', [...args, '--format', 'json'], {
      cwd: projectRoot, windowsHide: true, timeout: 40000, maxBuffer: 8 * 1024 * 1024,
    }));
  } catch (error) {
    if (!error.stdout) throw Error('Unity CLI를 실행하지 못했습니다. CLI 설치 및 Editor/Pipeline 연결을 확인하세요.');
    stdout = error.stdout;
  }
  const envelope = JSON.parse(stdout);
  if (envelope.success !== true) throw Error('Unity 연결/실행 실패: ' + (envelope.errors?.map(e => `${e.code}: ${e.message || ''}`).join(', ') || 'UNKNOWN'));
  return envelope.data;
}

export function unwrapRead(data) {
  if (!data || data.success !== true || data.result?.success !== true) throw Error('차트 조회 스크립트가 실패했습니다. Editor 로그인·컴파일 상태를 확인하세요.');
  if (typeof data.result.result !== 'string') throw Error('예상하지 못한 Pipeline 응답입니다. run_script 결과 형식을 확인하세요.');
  return JSON.parse(data.result.result);
}

let pending = Promise.resolve();
export function readLive(operation, chartName = '') {
  // One bridge execution at a time; no shared response files and no code from tool inputs.
  const run = pending.then(async () => {
    await unity(['command', 'set_autotick', '--enable', 'true', '--project-path', projectRoot]);
    const data = await unity(['command', 'run_script', '--project-path', projectRoot,
      '--file', 'tools/backnd-mcp/BackndChartReader.cs', '--entry', 'BackndChartReader.Run',
      '--args', JSON.stringify([operation, chartName]), '--timeout_ms', '25000', '--timeout', '35']);
    return unwrapRead(data);
  });
  pending = run.catch(() => {});
  return run;
}
