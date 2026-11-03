'use strict';
const fs=require('node:fs'),{open}=require('./app/workbook.cjs');
function read(file){
 const book=open(fs.readFileSync(file));if(book.sheets.size!==1)throw Error('뒤끝 업로드용 단일 시트 엑셀을 선택하세요.');
 const sheet=[...book.sheets.keys()][0],cells=book.sheets.get(sheet).cells;
 const col=n=>{let s='';for(n++;n;n=Math.floor((n-1)/26))s=String.fromCharCode(65+(n-1)%26)+s;return s;};
 const value=(x,y)=>{const c=book.get(sheet,col(x)+y);if(c.formula!==null)throw Error('수식은 업로드할 수 없습니다.');return c.value;};
 const headers=[];for(let x=0;x<100&&value(x,1);x++)headers.push(value(x,1));
 if(!headers.includes('RecordType')||!headers.includes('BonusId'))throw Error('보너스 업로드 엑셀의 헤더가 아닙니다.');
 if(new Set(headers).size!==headers.length)throw Error('중복 헤더');
 const last=Math.max(...[...cells.keys()].map(k=>Number(k.match(/\d+$/)[0]))),rows=[];
 for(let y=2;y<=last;y++){const values=headers.map((_,x)=>value(x,y));if(values.every(v=>v===''))continue;rows.push(Object.fromEntries(headers.map((h,i)=>[h,values[i]])));}
 return rows;
}
module.exports={read};if(require.main===module){try{process.stdout.write(JSON.stringify(read(process.argv[2])));}catch(e){process.stderr.write(e.message);process.exitCode=1;}}
