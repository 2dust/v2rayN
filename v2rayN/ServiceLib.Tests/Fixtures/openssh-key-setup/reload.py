"""Reload only the disposable fixture's listener and wait for its new bind."""
import os
import pathlib
import signal
import subprocess
import time

subprocess.run(["/usr/sbin/sshd", "-t"], check=True)
log = pathlib.Path("/tmp/fixture-sshd.log")
offset = log.stat().st_size
pid = int(pathlib.Path("/run/sshd.pid").read_text().strip())
os.kill(pid, signal.SIGHUP)
deadline = time.monotonic() + 10
while time.monotonic() < deadline:
    with log.open("rb") as stream:
        stream.seek(offset)
        # sshd emits this only after the replacement IPv4 listener is bound.
        if b"Server listening on 0.0.0.0 port 22." in stream.read():
            break
    time.sleep(0.05)
else:
    raise TimeoutError("Disposable SSH fixture did not finish reloading")
