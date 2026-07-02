const { spawn } = require('child_process');

const child = spawn('node', ['D:/UnityGit/ProjectGaeGGULDefence/Library/PackageCache/com.gamelovers.mcp-unity@cce8b57de9/Server~/build/index.js']);

let reqId = 1;
function sendRequest(method, params) {
    const req = {
        jsonrpc: "2.0",
        id: reqId++,
        method: method,
        params: params
    };
    child.stdin.write(JSON.stringify(req) + '\n');
}

let stage = 0;
let buffer = '';

child.stdout.on('data', (data) => {
    buffer += data.toString();
    
    // JSON-RPC 응답은 \n으로 구분되거나 하나의 큰 JSON일 수 있음.
    // 간단히 id로 체크
    if (stage === 0 && buffer.includes(`"id":1`)) {
        console.log("Stage 1 (create_prefab) completed.");
        buffer = '';
        stage++;
        // 컴파일 실행
        sendRequest("tools/call", { name: "recompile_scripts", arguments: {} });
    } else if (stage === 1 && buffer.includes(`"id":2`)) {
        console.log("Stage 2 (recompile_scripts) completed.");
        buffer = '';
        stage++;
        // 메뉴 실행
        sendRequest("tools/call", { name: "execute_menu_item", arguments: { menuPath: "Custom/Register AudioManager" } });
    } else if (stage === 2 && buffer.includes(`"id":3`)) {
        console.log("Stage 3 (execute_menu_item) completed.");
        console.log("Result:", buffer);
        process.exit(0);
    }
});

child.stderr.on('data', (data) => {
    console.error(`STDERR: ${data}`);
});

// 시작: create_prefab
sendRequest("tools/call", { name: "create_prefab", arguments: { prefabName: "AudioManager", componentName: "AudioManager" } });
