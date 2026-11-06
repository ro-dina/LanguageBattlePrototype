#!/usr/bin/env python3
"""Verify actual response headers and raw bytes against the local release manifest."""
import argparse
import hashlib
import json
from pathlib import Path
import time
from urllib.error import HTTPError
from urllib.parse import urlsplit
from urllib.request import Request, urlopen

MIME = {'.wasm.br': 'application/wasm', '.data.br': 'application/octet-stream',
        '.js.br': 'application/javascript', '.js': 'application/javascript'}

def verify(base, deployment):
    parsed = urlsplit(base)
    if parsed.scheme != 'https' and not (parsed.scheme == 'http' and parsed.hostname in ('127.0.0.1', 'localhost')):
        raise ValueError('Use an HTTPS URL, or localhost for local checks.')
    if parsed.query or parsed.fragment or parsed.username or parsed.password: raise ValueError('Use a plain root URL without credentials or query strings.')
    base = base.rstrip('/') + '/'
    manifest = json.loads((deployment / 'release-manifest.json').read_text())
    results = []; total = 0; started = time.perf_counter()
    for name, item in manifest['files'].items():
        request = Request(base + ('' if name == 'index.html' else name), headers={'Accept-Encoding': 'br' if name.endswith('.br') else 'identity'})
        at = time.perf_counter()
        with urlopen(request, timeout=120) as response:
            data = response.read(); headers = response.headers; final = urlsplit(response.url)
            if (final.scheme, final.netloc) != (parsed.scheme, parsed.netloc): raise ValueError('Unexpected cross-origin redirect: ' + name)
            if response.status != 200: raise ValueError('Unexpected status: ' + name)
            if hashlib.sha256(data).hexdigest() != item['sha256']: raise ValueError('Response bytes do not match the release: ' + name)
            if name.endswith('.br') and headers.get('Content-Encoding', '').lower() != 'br': raise ValueError('Missing Brotli encoding: ' + name)
            expected = next((v for suffix, v in MIME.items() if name.endswith(suffix)), None)
            mime = headers.get('Content-Type', '').split(';')[0].lower()
            if expected and mime != expected: raise ValueError('Wrong Content-Type: ' + name)
            cache = headers.get('Cache-Control', '').lower()
            if name.startswith('Build/') and ('immutable' not in cache or 'max-age=31536000' not in cache): raise ValueError('Wrong immutable cache policy: ' + name)
            if name == 'index.html' and ('max-age=0' not in cache or 'must-revalidate' not in cache): raise ValueError('HTML must revalidate.')
            if name == 'index.html' and headers.get('X-Unity-Web-Release') != manifest['release']: raise ValueError('Wrong release header.')
            results.append({'path': name, 'status': response.status, 'bytes': len(data), 'contentType': mime, 'contentEncoding': headers.get('Content-Encoding'), 'cacheControl': cache, 'seconds': round(time.perf_counter()-at, 3)})
            total += len(data)
    # These files exist only locally or in the Unity source and must never be public.
    denied = ['README.md', 'vercel.json', 'release-manifest.json', '.vercel/project.json', '.git/config', 'Assets/Scripts/SaveFileStore.cs', 'LanguageBattle/save.json', 'LanguageBattleSettings/settings.json']
    for path in denied:
        try:
            with urlopen(Request(base + path), timeout=30) as response:
                raise ValueError('A private or operational path is public: ' + path)
        except HTTPError as error:
            if error.code != 404: raise ValueError('Private path did not return 404: ' + path)
    return {'result': 'PASS', 'url': base, 'release': manifest['release'], 'fileCount': len(results), 'bodyBytes': total, 'sequentialHTTPSeconds': round(time.perf_counter()-started, 3), 'files': results, 'privatePaths404': denied}

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('url')
    parser.add_argument('--deployment', type=Path, default=Path(__file__).resolve().parents[1] / 'Deploy/Vercel')
    parser.add_argument('--report', type=Path)
    args = parser.parse_args()
    try:
        report = verify(args.url, args.deployment)
        if args.report: args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n')
        print(json.dumps({k:v for k,v in report.items() if k != 'files'}, ensure_ascii=False, indent=2))
    except Exception as error: parser.exit(1, 'Delivery check failed: ' + str(error) + '\n')

if __name__ == '__main__': main()
