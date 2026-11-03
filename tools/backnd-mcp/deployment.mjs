import path from 'node:path';
import { projectRoot, readJson, verifyBundle } from './balance.mjs';

export function chartTarget(environment = 'DEV') {
  if (!['DEV', 'LIVE'].includes(environment)) throw Error('environment는 DEV 또는 LIVE여야 합니다.');
  return { environment, chartPrefix: environment + '_' };
}

// Promotion describes existing bytes; it must not regenerate spreadsheets.
export function deploymentPlan(directory, environment = 'DEV', root = projectRoot) {
  const target = chartTarget(environment);
  verifyBundle(directory, root);
  const manifest = readJson(path.join(directory, 'manifest.json'), root);
  return {
    schemaVersion: 1, ...target, releaseKey: manifest.releaseKey,
    sourceSha256: manifest.sourceSha256,
    charts: manifest.charts.filter(c => c.chartName !== 'LevelBonuses').map(c => ({
      ...c, targetChartName: target.chartPrefix + c.chartName,
    })),
    deferredCharts: ['LevelBonuses'], remoteApplied: false, runtimeConnected: false,
    note: '기본 스탯·강화 수치 3개 차트의 업로드 계획입니다. 고유 보너스는 후순위이며 서버 업로드와 게임 연결은 수행하지 않습니다.',
  };
}
