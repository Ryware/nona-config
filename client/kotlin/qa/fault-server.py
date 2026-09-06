#!/usr/bin/env python3
"""Deterministic loopback-only transport failures for emulator tests."""
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import time
import threading

class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        if self.path.startswith('/redirect/'):
            self.send_response(302)
            self.send_header('Location', 'http://10.0.2.2:18688/capture')
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

capture = ThreadingHTTPServer(('127.0.0.1', 18688), Handler)
threading.Thread(target=capture.serve_forever, daemon=True).start()
ThreadingHTTPServer(('127.0.0.1', 18687), Handler).serve_forever()
