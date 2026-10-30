'use strict';
const crypto = require('node:crypto');
function encodeConfigRows(config, releaseKey, encoding = 'Base64') {
  if (!['Base64','Json'].includes(encoding)) throw Error('Unknown payload encoding');
  if (!/^[A-Za-z0-9_-]{1,80}$/.test(releaseKey)) throw Error('Invalid release key');
  const bytes = Buffer.from(JSON.stringify(config));
  const payload = encoding === 'Json' ? bytes.toString('utf8') : bytes.toString('base64');
  const digest = crypto.createHash('sha256').update(bytes).digest('hex');
  const parts = payload.match(/[\s\S]{1,2000}/g);
  if (parts.length > 4096) throw Error('Configuration too large');
  return [['SchemaVersion','ReleaseKey','PartIndex','PartCount','Sha256','Payload','Encoding'],
    ...parts.map((part,index) => [1,releaseKey,index,parts.length,digest,part,encoding])];
}

function decodeConfigRows(rows) {
  if (!Array.isArray(rows) || !rows.length || rows.length>4096) throw Error('Invalid configuration rows');
  const first=rows[0],parts=new Array(rows.length);
  const encoding=first.Encoding||'Base64';
  if(!['Base64','Json'].includes(encoding))throw Error('Unknown payload encoding');
  if (!first.ReleaseKey || !/^[a-f0-9]{64}$/.test(first.Sha256)) throw Error('Invalid release/hash');
  for (const row of rows) {
    const index=Number(row.PartIndex);
    if (String(row.SchemaVersion)!=='1' || Number(row.PartCount)!==rows.length || row.ReleaseKey!==first.ReleaseKey || row.Sha256!==first.Sha256
      || !Number.isInteger(index) || index<0 || index>=rows.length || parts[index]!==undefined || typeof row.Payload!=='string'
      || (row.Encoding||'Base64')!==encoding || !row.Payload.length || row.Payload.length>2000 || (encoding==='Base64'&&!/^[A-Za-z0-9+/]*={0,2}$/.test(row.Payload))) throw Error('Invalid/mixed configuration parts');
    parts[index]=row.Payload;
  }
  const bytes=Buffer.from(parts.join(''),encoding==='Json'?'utf8':'base64');
  if (crypto.createHash('sha256').update(bytes).digest('hex')!==first.Sha256) throw Error('Configuration checksum mismatch');
  JSON.parse(bytes.toString('utf8'));
  return {releaseKey:first.ReleaseKey,sha256:first.Sha256,bytes};
}
module.exports = {encodeConfigRows,decodeConfigRows};
