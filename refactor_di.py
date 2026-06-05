import os
import re

directory = r"D:\UnityGit\ProjectGaeGGULDefence\Assets\WorkSpace\USW\Scripts"

def process_file(filepath):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    # If the file doesn't contain IObjectResolver, skip
    if "IObjectResolver" not in content and "_resolver" not in content:
        return

    print(f"Processing {filepath}...")

    # Regex to find lines like: if (_bossManager == null) _bossManager = _resolver.Resolve<BossManager>();
    # Group 1: variable name
    # Group 2: Type
    resolve_pattern = re.compile(r"if\s*\(\s*([_a-zA-Z0-9]+)\s*==\s*null\s*\)\s*\1\s*=\s*_resolver\.Resolve<([a-zA-Z0-9_]+)>\(\s*\)\s*;")

    resolved_vars = []
    
    lines = content.split('\n')
    new_lines = []
    for line in lines:
        # Check if line has [Inject] private IObjectResolver _resolver;
        if "IObjectResolver" in line and "_resolver" in line:
            continue # skip this line
        
        match = resolve_pattern.search(line)
        if match:
            var_name = match.group(1)
            type_name = match.group(2)
            resolved_vars.append(var_name)
            continue # skip this line
            
        new_lines.append(line)

    content = '\n'.join(new_lines)
    
    # Now add [Inject] to the declarations of the resolved_vars
    for var in resolved_vars:
        # Looking for `private Type _var;`
        # Using a regex replacement
        # E.g., `private BossManager _bossManager;` -> `[Inject] private BossManager _bossManager;`
        # We need to be careful about not duplicating [Inject]
        decl_pattern = re.compile(r"(\s*)((?:private|public|protected|internal)\s+[a-zA-Z0-9_<>\[\]]+\s+" + re.escape(var) + r"\s*;)")
        content = decl_pattern.sub(r"\1[Inject] \2", content)

    # Some manual edge cases:
    # `[VContainer.Inject] private VContainer.IObjectResolver _resolver;`
    content = re.sub(r"\s*\[VContainer\.Inject\]\s*private\s*VContainer\.IObjectResolver\s*_resolver\s*;", "", content)
    
    # Write back
    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(content)

for root, _, files in os.walk(directory):
    for file in files:
        if file.endswith(".cs"):
            process_file(os.path.join(root, file))

print("Refactoring complete.")
