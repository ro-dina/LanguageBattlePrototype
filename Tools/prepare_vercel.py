#!/usr/bin/env python3
"""Package a Brotli Unity Web release without uploading or changing game sources."""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import subprocess
import tempfile
from html.parser import HTMLParser

PROJECT = Path(__file__).resolve().parents[1]
KINDS = {'.loader.js': 'application/javascript', '.framework.js.br': 'application/javascript',
         '.data.br': 'application/octet-stream', '.wasm.br': 'application/wasm'}
PRIVATE = [rb'/Users/[A-Za-z0-9_. -]+/', rb'/home/[A-Za-z0-9_. -]+/', rb'[A-Za-z]:\\Users\\',
           rb'/private/tmp/', rb'/tmp/LanguageBattle', rb'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----',
           rb'gh[pousr]_[A-Za-z0-9]{25,}', rb'"(?:instanceId|generationId)"\s*:\s*"(?:character-|progress-)[0-9a-f]{32}"']
READY = '''
                // Local-only load measurements; no tracking requests or save access.
                document.documentElement.dataset.unityReadyMs = String(Math.round(performance.now()));
                document.documentElement.dataset.unityTransferBytes = String(performance.getEntriesByType("resource").concat(performance.getEntriesByType("navigation")).reduce((n, e) => n + (e.transferSize || 0), 0));
'''

def sha(data): return hashlib.sha256(data).hexdigest()
def fail(message): raise ValueError(message)
def write_json(path, value): path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
def scan(path, data):
    if path.endswith('.br'):
        result = subprocess.run(['node', '-e', 'process.stdout.write(require("node:zlib").brotliDecompressSync(require("node:fs").readFileSync(process.argv[1])))', str(scan.source / path)], capture_output=True)
        if result.returncode: fail('Could not inspect compressed file: ' + path)
        data = result.stdout
    if any(re.search(pattern, data) for pattern in PRIVATE): fail('Private data or local absolute path found; content withheld: ' + path)
    if b'sourceMappingURL=' in data: fail('Source maps must not be included: ' + path)

class References(HTMLParser):
    def __init__(self): super().__init__(); self.paths = []
    def handle_starttag(self, tag, attrs):
        self.paths += [value for key, value in attrs if key in ('src', 'href') and value]

def template_files(source, html):
    parser = References(); parser.feed(html); pending = parser.paths[:]; found = set()
    while pending:
        raw = pending.pop()
        if raw.startswith(('data:', '#')): continue
        if '://' in raw or raw.startswith('/') or '?' in raw or '..' in PurePosixPath(raw).parts: fail('Unexpected external or unsafe template reference: ' + raw)
        if not raw.startswith('TemplateData/'): fail('Unexpected template reference: ' + raw)
        if raw in found: continue
        p = source / raw
        if not p.is_file() or p.is_symlink(): fail('Missing template asset: ' + raw)
        found.add(raw)
        if p.suffix == '.css':
            for match in re.findall(r'url\([\'\"]?([^\)\'\"]+)[\'\"]?\)', p.read_text()):
                pending.append(str(PurePosixPath(raw).parent / match))
    return found

def validate(destination):
    manifest = json.loads((destination / 'release-manifest.json').read_text())
    config = json.loads((destination / 'vercel.json').read_text())
    actual = {str(p.relative_to(destination / 'public')) for p in (destination / 'public').rglob('*') if p.is_file()}
    if actual != set(manifest['files']): fail('Public file list differs from the verified manifest.')
    if any(p.is_symlink() for p in (destination / 'public').rglob('*')): fail('Public symlinks are forbidden.')
    for name, item in manifest['files'].items():
        data = (destination / 'public' / name).read_bytes()
        if sha(data) != item['sha256']: fail('File digest differs: ' + name)
    html = (destination / 'public/index.html').read_text()
    enabled = re.search(r'(?m)^\s*config\.autoSyncPersistentDataPath\s*=\s*true\s*;', html)
    if not enabled or enabled.start() > html.index('createUnityInstance(canvas, config,'): fail('Automatic persistence is not enabled before startup.')
    for name in manifest['files']:
        if name.startswith('Build/'):
            if not re.fullmatch(r'Build/[0-9a-f]{64}\.(?:loader\.js|framework\.js\.br|data\.br|wasm\.br)', name): fail('A build filename is not content-hashed.')
            if Path(name).name not in html: fail('Build file is not referenced: ' + name)
            rule = next((r for r in config['headers'] if r['source'] == '/' + name), None)
            if not rule: fail('Missing build response headers: ' + name)
            headers = {h['key'].lower(): h['value'] for h in rule['headers']}
            if name.endswith('.br') and headers.get('content-encoding') != 'br': fail('Missing Brotli header.')
            if name.endswith('.wasm.br') and headers.get('content-type') != 'application/wasm': fail('Invalid WebAssembly MIME type.')
    if config.get('outputDirectory') != 'public' or config.get('framework') is not None: fail('Output must be static public/.')
    if config.get('rewrites') or config.get('functions'): fail('Unexpected server or route configuration.')
    return manifest

def package(source, destination, label):
    source = source.resolve(); destination = destination.resolve()
    if source == destination or source.is_relative_to(destination): fail('Source must be outside the deployment directory.')
    if not (source / 'index.html').is_file(): fail('Source must contain a built Unity index.html.')
    for p in source.rglob('*'):
        if p.is_symlink(): fail('Source symlinks are forbidden.')
        if p.is_file() and p.name != '.DS_Store' and p.relative_to(source).parts[0] not in ('index.html', 'Build', 'TemplateData'):
            fail('Not a release-only Web build: ' + str(p.relative_to(source)))
    html = (source / 'index.html').read_text(encoding='utf-8')
    if 'diagnostics.js' in html or 'Phase110Probe' in html or 'symbolsUrl:' in html: fail('A validation or debug build cannot be published.')
    if 'var buildUrl = "Build";' not in html: fail('Expected the Unity Default template and relative Build/ URLs.')
    if not re.search(r'(?m)^\s*config\.autoSyncPersistentDataPath\s*=\s*true\s*;', html): fail('Automatic persistence is disabled.')
    refs = re.findall(r'(?:loaderUrl|dataUrl|frameworkUrl|codeUrl)\s*[:=]\s*buildUrl\s*\+\s*"/([^"/]+)"', html)
    build_files = {p.name for p in (source / 'Build').iterdir() if p.is_file()}
    if len(refs) != 4 or build_files != set(refs): fail('Expected exactly the four release build files referenced by index.html.')
    selected = {'index.html'} | {'Build/' + p for p in refs} | template_files(source, html)
    scan.source = source
    for name in selected: scan(name, (source / name).read_bytes())
    destination.mkdir(parents=True, exist_ok=True)
    if (destination / 'public').exists() and not (destination / 'release-manifest.json').exists(): fail('Refusing to replace an unrecognized public directory.')
    with tempfile.TemporaryDirectory(prefix='.package-', dir=destination) as temporary:
        stage = Path(temporary); public = stage / 'public'; public.mkdir()
        kinds = {}; origins = {}
        for name in sorted(selected - {'index.html'}):
            data = (source / name).read_bytes(); target = name
            if name.startswith('Build/'):
                suffix = next((s for s in KINDS if name.endswith(s)), None)
                if suffix is None: fail('Unsupported build asset: ' + name)
                target = 'Build/' + sha(data) + suffix; kinds[target] = KINDS[suffix]
                html = html.replace(Path(name).name, Path(target).name)
            target_file = public / target; target_file.parent.mkdir(parents=True, exist_ok=True); target_file.write_bytes(data)
            origins[target] = name
        marker = '.then((unityInstance) => {'
        if html.count(marker) != 1: fail('Could not attach startup measurement to the known Unity template.')
        html = html.replace(marker, marker + READY)
        # A content hash changes whenever the data bytes change, so Unity's own
        # IndexedDB download cache can safely reuse this exact URL as immutable.
        data_filename = next(Path(name).name for name in kinds if name.endswith('.data.br'))
        cache = '\n      config.cacheControl = function (url) {\n        var path = url.split(/[?#]/)[0];\n        var asset = buildUrl + "/" + ' + json.dumps(data_filename) + ';\n        return path === asset || path.endsWith("/" + asset) ? "immutable" : "no-store";\n      };\n'
        html = html.replace('config.autoSyncPersistentDataPath = true;', 'config.autoSyncPersistentDataPath = true;' + cache, 1)
        release = sha((label + '\n' + html).encode())[:24]
        html = html.replace('<head>', '<head>\n    <meta name="unity-web-release" content="' + release + '">', 1)
        (public / 'index.html').write_text(html, encoding='utf-8'); origins['index.html'] = 'index.html'
        headers = []
        for path in ['/', '/index.html']:
            headers.append({'source': path, 'headers': [{'key': 'Cache-Control', 'value': 'public, max-age=0, must-revalidate'}, {'key': 'X-Unity-Web-Release', 'value': release}]})
        headers.append({'source': '/TemplateData/:path*', 'headers': [{'key': 'Cache-Control', 'value': 'public, max-age=0, must-revalidate'}]})
        for name, mime in sorted(kinds.items()):
            values = [{'key': 'Content-Type', 'value': mime}, {'key': 'Cache-Control', 'value': 'public, max-age=31536000, immutable'}]
            if name.endswith('.br'): values.append({'key': 'Content-Encoding', 'value': 'br'})
            headers.append({'source': '/' + name, 'headers': values})
        config = {'$schema': 'https://openapi.vercel.sh/vercel.json', 'framework': None, 'buildCommand': '', 'installCommand': '', 'outputDirectory': 'public', 'cleanUrls': False, 'headers': headers}
        write_json(stage / 'vercel.json', config)
        files = {str(p.relative_to(public)): {'sha256': sha(p.read_bytes()), 'bytes': p.stat().st_size, 'source': origins[str(p.relative_to(public))]} for p in sorted(public.rglob('*')) if p.is_file()}
        manifest = {'version': 1, 'release': release, 'label': label, 'files': files, 'totalPublicBytes': sum(v['bytes'] for v in files.values()), 'sourceIndexSha256': sha((source / 'index.html').read_bytes())}
        if manifest['totalPublicBytes'] + (stage / 'vercel.json').stat().st_size >= 100_000_000: fail('Package exceeds the conservative 100 MB Hobby CLI budget.')
        write_json(stage / 'release-manifest.json', manifest); validate(stage)
        backup = destination / '.previous-public'
        if backup.exists(): fail('An interrupted package is present; inspect .previous-public first.')
        if (destination / 'public').exists(): (destination / 'public').rename(backup)
        try:
            (stage / 'public').rename(destination / 'public')
            for name in ['vercel.json', 'release-manifest.json']: os.replace(stage / name, destination / name)
        except Exception:
            if backup.exists() and not (destination / 'public').exists(): backup.rename(destination / 'public')
            raise
        if backup.exists(): shutil.rmtree(backup)
    return validate(destination)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, help='Unity Web build folder; never the Unity project root')
    parser.add_argument('--destination', type=Path, default=PROJECT / 'Deploy/Vercel')
    parser.add_argument('--label', default='phase11.1-trial')
    parser.add_argument('--check', action='store_true', help='Validate the existing local package without uploading')
    args = parser.parse_args()
    try:
        if args.check: result = validate(args.destination.resolve())
        elif args.source: result = package(args.source, args.destination, args.label)
        else: parser.error('--source or --check is required')
        print(json.dumps({'result': 'PASS', 'release': result['release'], 'publicFiles': len(result['files']), 'publicBytes': result['totalPublicBytes'], 'uploaded': False}, indent=2))
    except (ValueError, OSError, KeyError) as error: parser.exit(1, 'Packaging refused: ' + str(error) + '\n')

if __name__ == '__main__': main()
