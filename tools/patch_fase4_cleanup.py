import io
import re

def read(p):
    return io.open(p, 'r', encoding='utf-8-sig').read()

def write(p, s):
    io.open(p, 'w', encoding='utf-8-sig', newline='\r\n').write(s)

p = 'src/XDLauncher/App.xaml.cs'
s = read(p)
s, count = re.subn(r'\n        if \(e\.Args\.Any\(arg => arg\.Equals\("--capture-settings".*?\n        \}\n', '\n', s, count=1, flags=re.S)
assert count == 1
s, count = re.subn(r'\n        if \(e\.Args\.Any\(arg => arg\.Equals\("--capture-about".*?\n        \}\n', '\n', s, count=1, flags=re.S)
assert count == 1
s, count = re.subn(r'\n    private static void CaptureSettings\(string category, string fileName\)\n    \{.*?\n    \}\n', '\n', s, count=1, flags=re.S)
assert count == 1
write(p, s)
