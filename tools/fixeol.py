# usage: python tools/fixeol.py -> give every changed file the line endings it has in the index (i/lf or i/crlf)
import subprocess
files = subprocess.run(['git', 'diff', '--name-only'], capture_output=True, text=True).stdout.split()
for line in subprocess.run(['git', 'ls-files', '--eol'] + files, capture_output=True, text=True).stdout.splitlines():
    p = line.split(); i, w, f = p[0], p[1], p[-1]
    if i not in ('i/lf', 'i/crlf') or w == i.replace('i/', 'w/'): continue
    b = open(f, 'rb').read().replace(b'\r\n', b'\n')
    if i == 'i/crlf': b = b.replace(b'\n', b'\r\n')
    open(f, 'wb').write(b); print('fixed', f, i)
