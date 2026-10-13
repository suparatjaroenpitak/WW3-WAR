"""Tiny socket client for the Blender MCP addon (port 9876).

Protocol (from blender_mcp.py installed in Blender 5.2):
  request  = json {"type": <cmd>, "params": {...}}   (plain JSON, no delimiter)
  response = json {"status": "success"|"error", ...} (single JSON object)
"""
import json
import socket

HOST = "localhost"
PORT = 9876


def send_command(cmd_type, params=None, timeout=120.0):
    req = json.dumps({"type": cmd_type, "params": params or {}})
    data = req.encode("utf-8")
    buf = bytearray()
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
        sock.settimeout(timeout)
        sock.connect((HOST, PORT))
        sock.sendall(data)
        while True:
            chunk = sock.recv(65536)
            if not chunk:
                raise ConnectionError("connection closed before full response")
            buf.extend(chunk)
            try:
                return json.loads(buf.decode("utf-8"))
            except (json.JSONDecodeError, UnicodeDecodeError):
                if len(buf) > 100 * 1024 * 1024:
                    raise ConnectionError("response too large (>100MB)")
                continue


if __name__ == "__main__":
    import sys

    cmd = sys.argv[1] if len(sys.argv) > 1 else "ping"
    if cmd == "ping":
        print(send_command("ping"))
    elif cmd == "scene":
        info = send_command("get_scene_info")
        print(json.dumps(info, indent=1)[:3000])
    elif cmd == "addon":
        print(send_command("get_addon_info"))
