const { spawn } = require('child_process');

const child = spawn('node', ['D:/UnityGit/ProjectGaeGGULDefence/Library/PackageCache/com.gamelovers.mcp-unity@cce8b57de9/Server~/build/index.js']);

const request = {
    jsonrpc: "2.0",
    id: 1,
    method: "tools/call",
    params: { name: "execute_menu_item", arguments: { menuPath: "Custom/Register AudioManager" } }
};

child.stdout.on('data', (data) => {
    console.log(`STDOUT: ${data}`);
    process.exit(0);
});

child.stderr.on('data', (data) => {
    console.error(`STDERR: ${data}`);
});

child.stdin.write(JSON.stringify(request) + '\n');
