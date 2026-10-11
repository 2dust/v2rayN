FROM ubuntu@sha256:186072bba1b2f436cbb91ef2567abca677337cfc786c86e107d25b7072feef0c
RUN apt-get update && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends openssh-server python3 openssl \
    && rm -rf /var/lib/apt/lists/* \
    && useradd -m -s /bin/sh fixture \
    && printf 'fixture:fixture-only-password\n' | chpasswd \
    && mkdir /run/sshd \
    && rm -f /etc/ssh/ssh_host_* \
    && printf '%s\n' 'PasswordAuthentication yes' 'PubkeyAuthentication yes' 'KbdInteractiveAuthentication no' \
       'UsePAM no' 'PermitRootLogin no' 'StrictModes yes' 'AuthorizedKeysFile .ssh/authorized_keys' \
       'AllowTcpForwarding yes' 'HostKey /etc/ssh/ssh_host_ed25519_key' 'LogLevel ERROR' > /etc/ssh/sshd_config
COPY server.py /fixture-server.py
# Only disposable credentials and an ephemeral loopback-published SSH port; no host mounts.
CMD ["sh", "-c", "ssh-keygen -A >/dev/null && openssl req -x509 -newkey rsa:2048 -nodes -keyout /tmp/key.pem -out /tmp/cert.pem -subj /CN=localhost -days 1 >/dev/null 2>&1 && python3 /fixture-server.py & wait"]
