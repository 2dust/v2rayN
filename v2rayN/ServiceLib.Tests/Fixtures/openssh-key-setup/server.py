"""Disposable HTTP/HTTPS targets reachable only through the fixture's SSH server."""
import http.server
import ssl
import subprocess
import threading
import time


class Handler(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
        if self.path == "/slow":
            time.sleep(20)
        self.send_response(403 if self.path == "/denied" else 200)
        self.end_headers()
        try:
            self.wfile.write(b"ssh-key-setup-ok")
        except (BrokenPipeError, ConnectionResetError):
            pass

    def log_message(self, *_):
        pass


for port in (18080, 18443):
    server = http.server.ThreadingHTTPServer(("127.0.0.1", port), Handler)
    if port == 18443:
        context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
        context.load_cert_chain("/tmp/cert.pem", "/tmp/key.pem")
        server.socket = context.wrap_socket(server.socket, server_side=True)
    threading.Thread(target=server.serve_forever, daemon=True).start()
subprocess.run(["/usr/sbin/sshd", "-D", "-E", "/tmp/fixture-sshd.log"], check=True)
