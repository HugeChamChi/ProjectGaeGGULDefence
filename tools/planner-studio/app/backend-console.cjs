'use strict';
const {BrowserWindow}=require('electron');

// The remote console has no preload or access to the planner's IPC bridge.
exports.register=({handler,parent})=>{
  let backendConsole;
  handler('backend-console', async () => {
    if (backendConsole && !backendConsole.isDestroyed()) { backendConsole.show(); backendConsole.focus(); return {opened:true}; }
    backendConsole = new BrowserWindow({width:1280,height:900,title:'뒤끝 공식 콘솔 · 차트 업로드/적용',
      autoHideMenuBar:true,webPreferences:{partition:'persist:backend-console',contextIsolation:true,nodeIntegration:false,sandbox:true}});
    backendConsole.webContents.session.setPermissionRequestHandler((_wc,_permission,callback)=>callback(false));
    backendConsole.webContents.session.setPermissionCheckHandler(()=>false);
    backendConsole.webContents.setWindowOpenHandler(()=>({action:'deny'}));
    try { await backendConsole.loadURL('https://console.thebackend.io/'); }
    catch(error) { backendConsole.destroy();backendConsole=null;throw error; }
    return {opened:true};
  });
  parent.on('closed',()=>{if(backendConsole && !backendConsole.isDestroyed())backendConsole.destroy();});
};
