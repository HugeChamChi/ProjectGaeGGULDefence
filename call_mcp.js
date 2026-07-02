const { spawn } = require('child_process');
const fs = require('fs');
const child = spawn('node', ['D:/UnityGit/ProjectGaeGGULDefence/Library/PackageCache/com.gamelovers.mcp-unity@cce8b57de9/Server~/build/index.js']);

const request = {
    jsonrpc: "2.0",
    id: 1,
    method: "tools/list",
    params: {}
};

let output = '';
child.stdout.on('data', (data) => {
    output += data.toString();
    if (output.includes('"id":1')) {
        fs.writeFileSync('mcp_tools.json', output);
        process.exit(0);
    }
});

child.stderr.on('data', (data) => {
    console.error(`STDERR: ${data}`);
});

child.stdin.write(JSON.stringify(request) + '\n');
