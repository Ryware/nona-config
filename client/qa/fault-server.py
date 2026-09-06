#!/usr/bin/env python3
"""Deterministic loopback-only transport failures for emulator tests."""
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import time
import threading

def redirect_target(host_header):
    host = host_header.split(':', 1)[0].lower()
    if host not in ('127.0.0.1', 'localhost', '10.0.2.2'):
        host = '127.0.0.1'
    return f'http://{host}:18688/capture'

class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        if self.path.startswith('/redirect/'):
            self.send_response(302)
            self.send_header('Location', redirect_target(self.headers.get('Host', '')))
            self.end_headers()
            return
        if self.path == '/capture':
            body = b'{"flag":{"value":"redirect-followed"}}'
            self.send_response(200)
            self.send_header('Content-Length', str(len(body)))
            self.end_headers()
            self.wfile.write(body)
            return
        if self.path.startswith(('/large/', '/large-no-length/')):
            body = b'{"flag":{"value":"' + b'x' * 1024 + b'"}}'
            self.send_response(200)
            if self.path.startswith('/large/'):
                self.send_header('Content-Length', str(len(body)))
            self.end_headers()
            self.wfile.write(body)
            return
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

if __name__ == '__main__':
    with ThreadingHTTPServer(('127.0.0.1', 18688), Handler) as capture:
        threading.Thread(target=capture.serve_forever, daemon=True).start()
        try:
            with ThreadingHTTPServer(('127.0.0.1', 18687), Handler) as server:
                server.serve_forever()
        finally:
            capture.shutdown()
