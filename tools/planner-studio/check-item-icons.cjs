const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),os=require('node:os');
const icons=require('./app/item-icons.cjs'),excel=require('./app/content-workbook.cjs');
const png=Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP438HwHwAGmAKHfbWTmQAAAABJRU5ErkJggg==','base64');
const a=icons.prepare('potion.png',png),b=icons.prepare('potion.png',png);assert.equal(a.address,b.address);assert.match(a.address,/^ItemIcons\/potion_[a-f0-9]{8}$/);assert.throws(()=>icons.prepare('bad.png',Buffer.from('bad')));
const e=excel.engine(),p=e.Studio.create();p.tables.Item[0].IconAddress=a.address;p.tables.Item[0].IconPng=a.png;
const restored=e.Studio.shape(p);assert.equal(restored.tables.Item[0].IconPng,a.png);
const folder=fs.mkdtempSync(path.join(os.tmpdir(),'planner-icons-'));icons.exportIcons(restored,folder);const manifest=JSON.parse(fs.readFileSync(path.join(folder,'ItemIcons/item-icons.json')));assert.equal(manifest.icons[0].address,a.address);assert.deepEqual(fs.readFileSync(path.join(folder,'ItemIcons',manifest.icons[0].file)),png);
assert(!e.Studio.tables(p).Item[0].includes('IconPng'));assert(excel.read(excel.write(p,['Item']),p).project.tables.Item[0].IconAddress===a.address);
console.log('PASS PNG validation, deterministic address, project persistence, portable PNG manifest, Excel address');
