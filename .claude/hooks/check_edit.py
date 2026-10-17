"""PostToolUse hook: after Claude edits a file, catch breakage immediately.
.py  -> python -m py_compile (syntax only; Blender scripts import bpy, so they are not executed)
.cs  -> dotnet build (quiet); errors are fed back to Claude
Exit code 2 sends stderr back to Claude; 0 stays silent."""
import json, os, subprocess, sys

data = json.load(sys.stdin)
path = (data.get("tool_input") or {}).get("file_path") or ""
root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

if path.endswith(".py"):
    r = subprocess.run([sys.executable, "-m", "py_compile", path], capture_output=True, text=True)
    if r.returncode:
        print(f"Python syntax error after editing {path}:\n{r.stderr.strip()}", file=sys.stderr)
        sys.exit(2)
elif path.endswith(".cs") and os.path.normcase(os.path.abspath(path)).startswith(os.path.normcase(os.path.join(root, "src"))):
    r = subprocess.run(["dotnet", "build", os.path.join(root, "Starforge.csproj"), "-nologo", "-v", "q", "-clp:ErrorsOnly"],
                       capture_output=True, text=True, timeout=240)
    if r.returncode:
        errs = [l for l in r.stdout.splitlines() if ": error " in l]
        print("C# build failed:\n" + "\n".join(dict.fromkeys(errs[:20]) or [r.stdout[-2000:]]), file=sys.stderr)
        sys.exit(2)
