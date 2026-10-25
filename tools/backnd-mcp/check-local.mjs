import path from 'node:path';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';
import { projectRoot } from './balance.mjs';

// Also useful after installation: performs a real MCP handshake, not a direct function call.
const client = new Client({ name: 'ggd-backnd-check', version: '1.0.0' });
try {
  await client.connect(new StdioClientTransport({ command: process.execPath, args: [path.join(projectRoot, 'tools/backnd-mcp/server.mjs')], stderr: 'inherit' }));
  const tools = await client.listTools();
  console.log(JSON.stringify({ tools: tools.tools.map(tool => tool.name) }));
  const result = await client.callTool({ name: 'backnd_status', arguments: {} });
  console.log(result.content[0].text);
  if (result.isError) process.exitCode = 1;
} finally {
  await client.close();
}
