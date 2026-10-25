'use strict';
const { contextBridge, ipcRenderer } = require('electron');
const channels = ['content-workbook-export','content-workbook-import','content-library-import','content-load', 'content-save', 'content-export', 'state', 'settings', 'library', 'import-library', 'use-bundled-folder', 'choose-excel-folder', 'open-excel-folder', 'save-guide', 'save', 'load', 'reset', 'validate', 'preview', 'search', 'import', 'export', 'share', 'open-exports', 'open-console', 'dirty'];
contextBridge.exposeInMainWorld('studio', Object.freeze(Object.fromEntries(channels.map(name => [name.replace(/-([a-z])/g, (_, c) => c.toUpperCase()), payload => ipcRenderer.invoke('studio:' + name, payload)]))));
