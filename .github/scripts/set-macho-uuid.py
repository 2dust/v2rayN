#!/usr/bin/env python3

import argparse
import hashlib
import struct
import uuid
from pathlib import Path


LC_UUID = 0x1B
LC_CODE_SIGNATURE = 0x1D
MACH_HEADER_64_SIZE = 32


def byte_order(data: bytes) -> str:
    magic = data[:4]
    if magic == b"\xcf\xfa\xed\xfe":
        return "<"
    if magic == b"\xfe\xed\xfa\xcf":
        return ">"
    raise ValueError("expected a thin 64-bit Mach-O executable")


def load_commands(data: bytes, order: str):
    if len(data) < MACH_HEADER_64_SIZE:
        raise ValueError("Mach-O header is truncated")

    ncmds, sizeofcmds = struct.unpack_from(f"{order}II", data, 16)
    command_offset = MACH_HEADER_64_SIZE
    command_end = command_offset + sizeofcmds
    if command_end > len(data):
        raise ValueError("Mach-O load commands are truncated")

    for _ in range(ncmds):
        if command_offset + 8 > command_end:
            raise ValueError("Mach-O load command header is truncated")
        command, command_size = struct.unpack_from(f"{order}II", data, command_offset)
        if command_size < 8 or command_offset + command_size > command_end:
            raise ValueError("Mach-O load command has an invalid size")
        yield command_offset, command, command_size
        command_offset += command_size


def update_uuid(path: Path) -> uuid.UUID:
    data = bytearray(path.read_bytes())
    order = byte_order(data)
    uuid_offset = None
    signature_commands = []

    for offset, command, command_size in load_commands(data, order):
        if command == LC_UUID:
            if command_size < 24 or uuid_offset is not None:
                raise ValueError("Mach-O executable has an invalid LC_UUID command")
            uuid_offset = offset + 8
        elif command == LC_CODE_SIGNATURE:
            if command_size < 16:
                raise ValueError("Mach-O executable has an invalid LC_CODE_SIGNATURE command")
            signature_offset, signature_size = struct.unpack_from(f"{order}II", data, offset + 8)
            if signature_offset + signature_size > len(data):
                raise ValueError("Mach-O code signature is truncated")
            signature_commands.append((offset + 8, signature_offset, signature_size))

    if uuid_offset is None:
        raise ValueError("Mach-O executable has no LC_UUID command")

    normalized = bytearray(data)
    normalized[uuid_offset : uuid_offset + 16] = bytes(16)
    content_end = len(normalized)
    for command_data_offset, signature_offset, _ in signature_commands:
        normalized[command_data_offset : command_data_offset + 8] = bytes(8)
        content_end = min(content_end, signature_offset)

    uuid_bytes = bytearray(hashlib.sha256(normalized[:content_end]).digest()[:16])
    uuid_bytes[6] = (uuid_bytes[6] & 0x0F) | 0x50
    uuid_bytes[8] = (uuid_bytes[8] & 0x3F) | 0x80
    data[uuid_offset : uuid_offset + 16] = uuid_bytes
    path.write_bytes(data)
    return uuid.UUID(bytes=bytes(uuid_bytes))


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Set a deterministic, content-derived UUID on a thin 64-bit Mach-O executable."
    )
    parser.add_argument("executable", type=Path)
    args = parser.parse_args()

    try:
        new_uuid = update_uuid(args.executable)
    except (OSError, ValueError) as error:
        parser.error(str(error))

    print(new_uuid)


if __name__ == "__main__":
    main()
