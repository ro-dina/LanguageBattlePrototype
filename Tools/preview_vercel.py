#!/usr/bin/env python3
"""Serve public/ on localhost using this package's static Vercel header rules."""
import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
import json
from pathlib import Path
from urllib.parse import unquote, urlsplit

class Handler(SimpleHTTPRequestHandler):
    def rules(self):
        config = json.loads((self.deployment / 'vercel.json').read_text())
        path = urlsplit(self.path).path
        headers = {}
        for rule in config['headers']:
            source = rule['source']
            if source == path or (source.endswith(':path*') and path.startswith(source[:-6])):
                headers.update({item['key']: item['value'] for item in rule['headers']})
        return headers
    def guess_type(self, path): return self.rules().get('Content-Type', super().guess_type(path))
    def end_headers(self):
        for key, value in self.rules().items():
            if key.lower() != 'content-type': self.send_header(key, value)
        super().end_headers()
    def send_head(self):
        requested = unquote(urlsplit(self.path).path)
        target = (self.deployment / 'public' / requested.lstrip('/')).resolve()
        if not target.is_relative_to((self.deployment / 'public').resolve()) or any(part.startswith('.') for part in Path(requested).parts):
            self.send_error(404); return None
        return super().send_head()
    def list_directory(self, path): self.send_error(404); return None
    def __init__(self, *args, deployment, **kwargs):
        self.deployment = deployment
        super().__init__(*args, directory=str(deployment / 'public'), **kwargs)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--deployment', type=Path, default=Path(__file__).resolve().parents[1] / 'Deploy/Vercel')
    parser.add_argument('--port', type=int, default=8713)
    args = parser.parse_args(); deployment = args.deployment.resolve()
    if not (deployment / 'vercel.json').is_file(): parser.error('Run prepare_vercel.py first.')
    server = ThreadingHTTPServer(('127.0.0.1', args.port), partial(Handler, deployment=deployment))
    print(f'Local delivery check: http://127.0.0.1:{args.port}/', flush=True)
    try: server.serve_forever()
    except KeyboardInterrupt: pass
    finally: server.server_close()

if __name__ == '__main__': main()
