const fs = require('fs');
const path = require('path');

const directory = 'D:\\UnityGit\\ProjectGaeGGULDefence\\Assets\\WorkSpace\\USW\\Scripts';

function processFile(filepath) {
    let content = fs.readFileSync(filepath, 'utf8');

    if (!content.includes('IObjectResolver') && !content.includes('_resolver')) {
        return;
    }

    console.log(`Processing ${filepath}...`);

    const resolvePattern = /if\s*\(\s*([_a-zA-Z0-9]+)\s*==\s*null\s*\)\s*\1\s*=\s*_resolver\.Resolve<([a-zA-Z0-9_]+)>\(\s*\)\s*;/g;
    
    let resolvedVars = [];
    let match;
    while ((match = resolvePattern.exec(content)) !== null) {
        resolvedVars.push(match[1]);
    }

    // Remove resolve lines
    let lines = content.split('\n');
    let newLines = [];
    for (let line of lines) {
        if (line.includes('IObjectResolver') && line.includes('_resolver')) continue;
        if (line.includes('_resolver.Resolve<')) continue;
        newLines.push(line);
    }
    content = newLines.join('\n');

    // Add [Inject]
    for (let v of resolvedVars) {
        // E.g. private BossManager _bossManager;
        const declPattern = new RegExp(`(\\s*)((?:private|public|protected|internal)\\s+[a-zA-Z0-9_<>\\[\\]]+\\s+${v}\\s*;)`, 'g');
        content = content.replace(declPattern, '$1[Inject] $2');
    }

    // Edge cases
    content = content.replace(/\s*\[VContainer\.Inject\]\s*private\s*VContainer\.IObjectResolver\s*_resolver\s*;/g, '');

    fs.writeFileSync(filepath, content, 'utf8');
}

function walkDir(dir) {
    const files = fs.readdirSync(dir);
    for (const file of files) {
        const fullPath = path.join(dir, file);
        if (fs.statSync(fullPath).isDirectory()) {
            walkDir(fullPath);
        } else if (fullPath.endsWith('.cs')) {
            processFile(fullPath);
        }
    }
}

walkDir(directory);
console.log('Refactoring complete.');
