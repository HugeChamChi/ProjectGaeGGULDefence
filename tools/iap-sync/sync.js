#!/usr/bin/env node
'use strict';

const fs = require('fs');
const path = require('path');
const http = require('http');
const { google } = require('googleapis');

const PACKAGE_NAME = 'com.GaeGGUL';
const CURRENCY = 'USD';
const DEFAULT_LANGUAGE = 'ko-KR';
const REGIONS_VERSION = '2022/02';
const CATALOG_PATH = path.resolve(__dirname, '../../Assets/Resources/IAPProductCatalog.json');
const CLIENT_SECRET_PATH = process.env.IAP_SYNC_CLIENT_SECRET || path.join(__dirname, 'client_secret.json');
const TOKEN_PATH = path.join(__dirname, '.token.json');
const SCOPES = ['https://www.googleapis.com/auth/androidpublisher'];
const REDIRECT_PORT = 53682;
const REDIRECT_URI = `http://localhost:${REDIRECT_PORT}/oauth2callback`;

function loadClientSecret() {
  if (!fs.existsSync(CLIENT_SECRET_PATH)) {
    console.error(`[iap-sync] OAuth 클라이언트 파일을 찾을 수 없습니다: ${CLIENT_SECRET_PATH}`);
    console.error('Google Cloud Console에서 OAuth 클라이언트 ID(데스크톱 앱)를 만들고 다운로드한 JSON을 위 경로에 두거나, IAP_SYNC_CLIENT_SECRET 환경변수로 경로를 지정하세요.');
    process.exit(1);
  }
  const raw = JSON.parse(fs.readFileSync(CLIENT_SECRET_PATH, 'utf8'));
  return raw.installed || raw.web;
}

function loadCachedToken() {
  if (fs.existsSync(TOKEN_PATH)) {
    return JSON.parse(fs.readFileSync(TOKEN_PATH, 'utf8'));
  }
  return null;
}

function saveToken(token) {
  fs.writeFileSync(TOKEN_PATH, JSON.stringify(token, null, 2));
}

function authenticate(oAuth2Client) {
  return new Promise((resolve, reject) => {
    const authUrl = oAuth2Client.generateAuthUrl({
      access_type: 'offline',
      scope: SCOPES,
    });

    const server = http.createServer(async (req, res) => {
      if (!req.url.startsWith('/oauth2callback')) return;

      const url = new URL(req.url, REDIRECT_URI);
      const code = url.searchParams.get('code');
      res.end('로그인 완료. 이 창은 닫아도 됩니다.');
      server.close();

      try {
        const { tokens } = await oAuth2Client.getToken(code);
        oAuth2Client.setCredentials(tokens);
        saveToken(tokens);
        resolve();
      } catch (err) {
        reject(err);
      }
    });

    server.listen(REDIRECT_PORT, () => {
      console.log('[iap-sync] 브라우저에서 아래 주소를 열어 로그인하세요:');
      console.log(authUrl);
    });
  });
}

async function getAuthorizedClient() {
  const { client_id, client_secret } = loadClientSecret();
  const oAuth2Client = new google.auth.OAuth2(client_id, client_secret, REDIRECT_URI);

  const cached = loadCachedToken();
  if (cached) {
    oAuth2Client.setCredentials(cached);
  } else {
    await authenticate(oAuth2Client);
  }

  oAuth2Client.on('tokens', (tokens) => {
    saveToken({ ...oAuth2Client.credentials, ...tokens });
  });

  return oAuth2Client;
}

function priceToMicros(num) {
  return String(Math.round(num * 1000000));
}

// Unity's ProductCatalog escapes non-ASCII chars as literal \uXXXX text (see
// LocalizedProductDescription.EncodeNonLatinCharacters) and decodes them back at read time.
// JSON.parse alone won't reverse this, so mirror that decode step here.
function decodeUnityEscapes(str) {
  return str.replace(/\\u([0-9a-fA-F]{4})/g, (_, hex) => String.fromCharCode(parseInt(hex, 16)));
}

function loadProductsFromCatalog() {
  const catalog = JSON.parse(fs.readFileSync(CATALOG_PATH, 'utf8'));
  return catalog.products
    .filter((p) => p.id && p.id.trim().length > 0)
    .map((p) => {
      if (p.type === 2) {
        console.warn(`[iap-sync] ${p.id}: 구독 상품은 아직 지원하지 않아 건너뜁니다.`);
        return null;
      }

      const whole = Math.trunc(p.googlePrice.num);
      const nanos = Math.round((p.googlePrice.num - whole) * 1e9);

      return {
        allowMissing: true,
        updateMask: 'listings,purchaseOptions',
        latencyTolerance: 'PRODUCT_UPDATE_LATENCY_TOLERANCE_LATENCY_TOLERANT',
        regionsVersion: { version: REGIONS_VERSION },
        oneTimeProduct: {
          packageName: PACKAGE_NAME,
          productId: p.id,
          listings: [
            {
              languageCode: DEFAULT_LANGUAGE,
              title: decodeUnityEscapes(p.defaultDescription.title),
              description: decodeUnityEscapes(p.defaultDescription.description),
            },
          ],
          purchaseOptions: [
            {
              purchaseOptionId: 'buy',
              regionalPricingAndAvailabilityConfigs: [
                {
                  regionCode: 'US',
                  price: { currencyCode: CURRENCY, units: String(whole), nanos },
                  availability: 'AVAILABLE',
                },
              ],
              buyOption: { legacyCompatible: true, multiQuantityEnabled: false },
            },
          ],
        },
      };
    })
    .filter(Boolean);
}

async function main() {
  const requests = loadProductsFromCatalog();
  if (requests.length === 0) {
    console.log('[iap-sync] 동기화할 상품이 없습니다.');
    return;
  }

  const auth = await getAuthorizedClient();
  const androidpublisher = google.androidpublisher({ version: 'v3', auth });

  const res = await androidpublisher.monetization.onetimeproducts.batchUpdate({
    packageName: PACKAGE_NAME,
    requestBody: { requests },
  });

  const results = (res.data && res.data.oneTimeProducts) || [];
  console.log(`[iap-sync] ${results.length}개 상품 동기화 완료:`);
  for (const item of results) {
    const states = (item.purchaseOptions || []).map((po) => `${po.purchaseOptionId}:${po.state}`).join(', ');
    console.log(`  - ${item.productId} [${states}]`);
  }

  const activations = results.flatMap((item) =>
    (item.purchaseOptions || [])
      .filter((po) => po.state === 'DRAFT' || po.state === 'INACTIVE')
      .map((po) => ({
        activatePurchaseOptionRequest: {
          packageName: PACKAGE_NAME,
          productId: item.productId,
          purchaseOptionId: po.purchaseOptionId,
        },
      }))
  );

  if (activations.length > 0) {
    await androidpublisher.monetization.onetimeproducts.purchaseOptions.batchUpdateStates({
      packageName: PACKAGE_NAME,
      productId: '-',
      requestBody: { requests: activations },
    });
    console.log(`[iap-sync] 구매 옵션 ${activations.length}개 활성화(ACTIVE) 완료`);
  }
}

main().catch((err) => {
  console.error('[iap-sync] 실패:', (err.response && err.response.data) || err.message);
  process.exit(1);
});
