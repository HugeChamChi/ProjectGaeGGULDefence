// 뒤끝 콘솔용 브라우저. 로그인 세션은 PC 로컬 프로필에만 남는다(저장소 밖).
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright-core';

export const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
export const profileDir = path.join(process.env.LOCALAPPDATA ?? os.homedir(), 'GGD', 'backnd-console-profile');

export async function openConsole({ headless = false } = {}) {
  fs.mkdirSync(profileDir, { recursive: true });
  return chromium.launchPersistentContext(profileDir, {
    channel: 'chrome', headless, viewport: null, acceptDownloads: false,
  });
}
