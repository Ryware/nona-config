#!/usr/bin/env python3
"""Deterministic loopback-only transport failures for emulator tests."""
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import time

class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        if self.path.startswith('/slow/'):
            time.sleep(0.5)
        body = b'{"flag":{"value":123}}'
        self.send_response(503 if self.path.startswith('/http503/') else 200)
        self.send_header('Content-Type', 'application/json')
        self.send_header('Content-Length', str(len(body)))
        self.end_headers()
        try:
            self.wfile.write(body)
        except (BrokenPipeError, ConnectionResetError):
            pass

ThreadingHTTPServer(('127.0.0.1', 18687), Handler).serve_forever()
