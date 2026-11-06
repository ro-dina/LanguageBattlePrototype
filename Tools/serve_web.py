#!/usr/bin/env python3
"""Serve a Unity Web build locally, with the headers required by Brotli/gzip."""
import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

class UnityWebHandler(SimpleHTTPRequestHandler):
    def guess_type(self, path):
        if path.endswith(('.br', '.gz')):
            path = path.rsplit('.', 1)[0]
        if path.endswith('.wasm'):
            return 'application/wasm'
        if path.endswith('.data'):
            return 'application/octet-stream'
        return super().guess_type(path)

    def end_headers(self):
        path = self.path.split('?', 1)[0]
        if path.endswith('.br'):
            self.send_header('Content-Encoding', 'br')
        elif path.endswith('.gz'):
            self.send_header('Content-Encoding', 'gzip')
        self.send_header('Cache-Control', 'no-store')
        super().end_headers()

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path, help='Folder containing the built index.html')
    parser.add_argument('--port', type=int, default=8711)
    args = parser.parse_args()
    root = args.directory.resolve()
    if not (root / 'index.html').is_file():
        parser.error('The directory must contain a Unity Web index.html.')
    server = ThreadingHTTPServer(('127.0.0.1', args.port), partial(UnityWebHandler, directory=str(root)))
    print(f'Open http://127.0.0.1:{args.port}/ (keep the same address and port to reuse browser saves)', flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()
