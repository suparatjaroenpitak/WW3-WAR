#!/usr/bin/env python3
"""blender-mcp-setup.py — ตรวจและตั้งค่า Blender MCP ให้ครบทุก agent app ในเครื่อง

ทำงาน 3 อย่าง:
  1. ตรวจว่าแอพ agent แต่ละตัวตั้งค่า Blender MCP ไว้หรือยัง และชี้ไปที่แพ็กเกจไหน
  2. ตั้งค่า/ซ่อมให้ชี้ไปที่แพ็กเกจปัจจุบัน (mcp-for-blender) ด้วย --apply
  3. ตรวจว่ามี Blender MCP server กี่ตัวกำลังรันอยู่ และใครถือพอร์ต 9876

ตัวอย่าง:
  python blender-mcp-setup.py                 # ตรวจอย่างเดียว (อ่านล้วน ไม่แก้อะไร)
  python blender-mcp-setup.py --apply         # แสดงแผน แล้วถามยืนยันก่อนเขียน
  python blender-mcp-setup.py --apply --yes   # เขียนเลย (สำรองไฟล์เดิมเป็น .bak-<เวลา>)
  python blender-mcp-setup.py --apply --only codex,freebuff
  python blender-mcp-setup.py --json          # ผลลัพธ์เป็น JSON สำหรับเอาไปใช้ต่อ

รหัสออก: 0 = ไม่มีปัญหา, 2 = เจอปัญหาที่ต้องแก้, 1 = สคริปต์รันไม่ได้

ความปลอดภัย: ไม่แตะ entry ของผลิตภัณฑ์อื่น (blender-ai / BlenderAI sidecar)
เขียนไฟล์แบบ atomic และสำรองไฟล์เดิมทุกครั้งก่อนแก้
"""

from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path

PKG = "mcp-for-blender"
LEGACY_PKG = "blender-mcp"
DEFAULT_HOST = "localhost"
DEFAULT_PORT = 9876

# ชื่อ/คำที่บ่งบอกว่าเป็น entry ของ Blender MCP ตัวนี้
PKG_MARKERS = (PKG, LEGACY_PKG)
# ผลิตภัณฑ์คนละตัว (BlenderAI sidecar) — ห้ามแก้
OTHER_PRODUCT_MARKERS = ("blender_ai_sidecar", "blender-ai", "blender_ai", "blenderai")

# ชื่อ process ที่ถือว่าเป็น "ตัวห่อ" ของ MCP server ไม่ใช่แอพเจ้าภาพ
WRAPPER_NAMES = {
    "uv.exe", "uvx.exe", "uv", "uvx",
    "python.exe", "python", "python3", "pythonw.exe",
    "mcp-for-blender.exe", "mcp-for-blender",
    "blender-mcp.exe", "blender-mcp",
    "cmd.exe", "sh", "bash", "node.exe", "bun.exe",
}

CLIENTS = [
    {
        "id": "freebuff",
        "name": "Freebuff Desktop",
        "kind": "json",
        "path": "~/.agents/mcp.json",
        "note": "หลังเขียนแล้วต้องกดอนุมัติในหน้า Connectors ของแอพ",
    },
    {
        "id": "claude",
        "name": "Claude Code",
        "kind": "json",
        "path": "~/.claude.json",
    },
    {
        "id": "cursor",
        "name": "Cursor",
        "kind": "json",
        "path": "~/.cursor/mcp.json",
    },
    {
        "id": "gemini",
        "name": "Gemini CLI",
        "kind": "json",
        "path": "~/.gemini/config/mcp_config.json",
    },
    {
        "id": "opencode",
        "name": "opencode",
        "kind": "opencode",
        "path": "~/.config/opencode/opencode.json",
    },
    {
        "id": "codex",
        "name": "Codex",
        "kind": "codex",
        "path": "~/.codex/config.toml",
    },
]

# แอพที่พบว่ามี MCP แต่รูปแบบไฟล์เฉพาะตัว — รายงานอย่างเดียว ไม่เขียน
UNSUPPORTED = [
    ("grokbot", "Grokbot", "~/.grokbot/settings.json", "เก็บในคีย์ mcpBoxServers (รูปแบบเฉพาะของแอพ)"),
    ("copilot", "Copilot", "~/.copilot/config.json", "ไฟล์เป็น JSONC ที่แอพเขียนเอง (มีคอมเมนต์)"),
]


# --------------------------------------------------------------------------- #
# output helpers
# --------------------------------------------------------------------------- #

def _utf8_stdout() -> None:
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8", errors="replace")  # type: ignore[union-attr]
        except Exception:
            pass


class Style:
    def __init__(self, enabled: bool) -> None:
        self.enabled = enabled

    def _wrap(self, code: str, text: str) -> str:
        return f"\033[{code}m{text}\033[0m" if self.enabled else text

    def bold(self, t: str) -> str:
        return self._wrap("1", t)

    def dim(self, t: str) -> str:
        return self._wrap("2", t)

    def red(self, t: str) -> str:
        return self._wrap("31", t)

    def green(self, t: str) -> str:
        return self._wrap("32", t)

    def yellow(self, t: str) -> str:
        return self._wrap("33", t)

    def cyan(self, t: str) -> str:
        return self._wrap("36", t)


# --------------------------------------------------------------------------- #
# filesystem helpers
# --------------------------------------------------------------------------- #

def expand(path: str) -> Path:
    return Path(os.path.expanduser(path))


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def detect_indent(text: str) -> str:
    m = re.search(r"\n([ \t]+)\S", text)
    return m.group(1) if m else "  "


def backup_file(path: Path) -> Path | None:
    if not path.exists():
        return None
    stamp = time.strftime("%Y%m%d-%H%M%S")
    target = path.with_name(f"{path.name}.bak-{stamp}")
    shutil.copy2(path, target)
    return target


def write_atomic(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_name(f"{path.name}.tmp-{os.getpid()}")
    tmp.write_text(text, encoding="utf-8", newline="\n")
    os.replace(tmp, path)


def write_json(path: Path, data: dict, indent: str) -> Path | None:
    bak = backup_file(path)
    write_atomic(path, json.dumps(data, indent=indent, ensure_ascii=False) + "\n")
    return bak


# --------------------------------------------------------------------------- #
# uv / uvx resolution
# --------------------------------------------------------------------------- #

def find_uvx() -> Path | None:
    for candidate in (
        Path(os.path.expanduser("~/.local/bin/uvx.exe")),
        Path(os.path.expanduser("~/.local/bin/uvx")),
        Path(os.path.expanduser("~/.cargo/bin/uvx")),
    ):
        if candidate.exists():
            return candidate
    found = shutil.which("uvx")
    return Path(found) if found else None


def find_uv() -> Path | None:
    for candidate in (
        Path(os.path.expanduser("~/.local/bin/uv.exe")),
        Path(os.path.expanduser("~/.local/bin/uv")),
        Path(os.path.expanduser("~/.cargo/bin/uv")),
    ):
        if candidate.exists():
            return candidate
    found = shutil.which("uv")
    return Path(found) if found else None


def canonical_command(plain_uvx: bool) -> tuple[str, list[str]]:
    """คืน (command, args) ที่จะใช้เขียนลง config"""
    if plain_uvx:
        return "uvx", [PKG]
    uvx = find_uvx()
    return (str(uvx) if uvx else "uvx"), [PKG]


def canonical_env(with_env: bool) -> dict:
    if not with_env:
        return {}
    return {
        "BLENDER_HOST": DEFAULT_HOST,
        "BLENDER_PORT": str(DEFAULT_PORT),
        "DISABLE_TELEMETRY": "true",
    }


# --------------------------------------------------------------------------- #
# entry classification
# --------------------------------------------------------------------------- #

def blob_of(name: str, command: str, args: list[str]) -> str:
    return " ".join([name, command, *args]).lower()


def is_blender_related(name: str, command: str, args: list[str]) -> bool:
    """กรองเอาเฉพาะ entry ที่เกี่ยวกับ Blender (ตัด MCP server อื่นอย่าง node_repl ออก)"""
    return "blender" in blob_of(name, command, args)


def classify(name: str, command: str, args: list[str]) -> str:
    """คืนหนึ่งใน: other-product | current | legacy | unknown"""
    blob = blob_of(name, command, args)
    if any(marker in blob for marker in OTHER_PRODUCT_MARKERS):
        return "other-product"
    if PKG in blob:
        return "current"
    if LEGACY_PKG in blob:
        return "legacy"
    return "unknown"


def looks_like_uv_launch(command: str, args: list[str]) -> bool:
    blob = " ".join([command, *args]).lower()
    return "uvx" in blob or "uv tool run" in blob or "uv.exe" in blob


# --------------------------------------------------------------------------- #
# per-format readers
# --------------------------------------------------------------------------- #

class ServerEntry:
    """มุมมองกลางของ MCP server entry หนึ่งตัว"""

    def __init__(self, name: str, command: str, args: list[str], env: dict) -> None:
        self.name = name
        self.command = command
        self.args = args
        self.env = env

    @property
    def kind(self) -> str:
        return classify(self.name, self.command, self.args)

    def launch_line(self) -> str:
        parts = [self.command, *self.args]
        return " ".join(f'"{p}"' if " " in p else p for p in parts)


def read_json_servers(path: Path) -> tuple[dict, dict]:
    """คืน (data, servers) โดย servers คือ mcpServers"""
    if not path.exists():
        return {}, {}
    text = read_text(path)
    if not text.strip():
        return {}, {}
    data = json.loads(text)
    if not isinstance(data, dict):
        raise ValueError("root ของไฟล์ไม่ใช่ object")
    servers = data.get("mcpServers")
    return data, servers if isinstance(servers, dict) else {}


def entries_of_json(path: Path) -> list[ServerEntry]:
    _, servers = read_json_servers(path)
    out = []
    for name, raw in servers.items():
        if not isinstance(raw, dict):
            continue
        out.append(
            ServerEntry(
                name,
                str(raw.get("command", "")),
                [str(a) for a in raw.get("args", []) if isinstance(a, (str, int, float))],
                dict(raw.get("env", {}) or {}),
            )
        )
    return out


def entries_of_opencode(path: Path) -> list[ServerEntry]:
    if not path.exists():
        return []
    text = read_text(path)
    if not text.strip():
        return []
    data = json.loads(text)
    servers = ((data or {}).get("mcp") or {}).get("servers") or {}
    out = []
    for name, raw in servers.items():
        if not isinstance(raw, dict):
            continue
        cmd = raw.get("command")
        if isinstance(cmd, list):
            cmd_list = [str(c) for c in cmd]
            command, args = (cmd_list[0], cmd_list[1:]) if cmd_list else ("", [])
        else:
            command, args = str(cmd or ""), [str(a) for a in raw.get("args", [])]
        out.append(ServerEntry(name, command, args, dict(raw.get("environment", {}) or {})))
    return out


def parse_codex_toml(path: Path) -> dict:
    """อ่านเฉพาะ [mcp_servers.*] ด้วย tomllib (ไม่เขียนทับด้วยตัว parser)"""
    if not path.exists():
        return {}
    import tomllib

    with path.open("rb") as fh:
        data = tomllib.load(fh)
    servers = data.get("mcp_servers") or {}
    return servers if isinstance(servers, dict) else {}


def entries_of_codex(path: Path) -> list[ServerEntry]:
    out = []
    for name, raw in parse_codex_toml(path).items():
        if not isinstance(raw, dict):
            continue
        out.append(
            ServerEntry(
                name,
                str(raw.get("command", "")),
                [str(a) for a in raw.get("args", [])],
                dict(raw.get("env", {}) or {}),
            )
        )
    return out


def entries_for(spec: dict) -> list[ServerEntry]:
    path = expand(spec["path"])
    kind = spec["kind"]
    if kind == "json":
        return entries_of_json(path)
    if kind == "opencode":
        return entries_of_opencode(path)
    if kind == "codex":
        return entries_of_codex(path)
    raise ValueError(f"unknown kind {kind}")


# --------------------------------------------------------------------------- #
# per-format writers
# --------------------------------------------------------------------------- #

def toml_literal(value: str) -> str:
    """สตริงแบบ literal เพื่อไม่ต้อง escape แบ็กสแลชของพาธ Windows"""
    if "'" in value:
        return '"' + value.replace("\\", "\\\\").replace('"', '\\"') + '"'
    return f"'{value}'"


def codex_upsert_block(text: str, name: str, command: str, args: list[str]) -> str:
    table = f"mcp_servers.{name}"
    lines = text.splitlines()
    header_re = re.compile(r"^\s*\[" + re.escape(table) + r"\]\s*$")
    start = next((i for i, l in enumerate(lines) if header_re.match(l)), None)

    body = [f"[{table}]", f"command = {toml_literal(command)}",
            "args = [" + ", ".join(toml_literal(a) for a in args) + "]"]

    if start is None:
        out = lines[:]
        if out and out[-1].strip():
            out.append("")
        out.extend(body)
        return "\n".join(out).rstrip("\n") + "\n"

    end = len(lines)
    for j in range(start + 1, len(lines)):
        if lines[j].lstrip().startswith("["):
            end = j
            break
    trailing_blank = bool(lines[start + 1:end]) and lines[end - 1].strip() == ""
    replacement = body + ([""] if trailing_blank else [])
    lines[start:end] = replacement
    return "\n".join(lines).rstrip("\n") + "\n"


def apply_json(path: Path, name: str, command: str, args: list[str], env: dict) -> str | None:
    data = json.loads(read_text(path)) if path.exists() and read_text(path).strip() else {}
    servers = data.get("mcpServers")
    servers = dict(servers) if isinstance(servers, dict) else {}
    existing = servers.get(name)
    entry = dict(existing) if isinstance(existing, dict) else {}
    entry["command"] = command
    entry["args"] = args
    if env:
        entry["env"] = env
    servers[name] = entry
    data["mcpServers"] = servers
    indent = detect_indent(read_text(path)) if path.exists() else "  "
    bak = write_json(path, data, indent)
    return str(bak) if bak else None


def apply_opencode(path: Path, name: str, command: str, args: list[str], env: dict) -> str | None:
    data = json.loads(read_text(path)) if path.exists() and read_text(path).strip() else {}
    mcp = data.setdefault("mcp", {})
    servers = mcp.setdefault("servers", {})
    existing = servers.get(name)
    entry = dict(existing) if isinstance(existing, dict) else {}
    entry["type"] = "local"
    entry["command"] = [command, *args]
    if env:
        entry["environment"] = env
    servers[name] = entry
    indent = detect_indent(read_text(path)) if path.exists() else "  "
    bak = write_json(path, data, indent)
    return str(bak) if bak else None


def apply_codex(path: Path, name: str, command: str, args: list[str], env: dict) -> str | None:
    text = read_text(path) if path.exists() else ""
    new_text = codex_upsert_block(text, name, command, args)
    bak = backup_file(path)
    write_atomic(path, new_text)
    return str(bak) if bak else None


def apply_entry(spec: dict, command: str, args: list[str], env: dict) -> str | None:
    path = expand(spec["path"])
    kind = spec["kind"]
    if kind == "json":
        return apply_json(path, "blender", command, args, env)
    if kind == "opencode":
        return apply_opencode(path, "blender", command, args, env)
    if kind == "codex":
        return apply_codex(path, "blender", command, args, env)
    raise ValueError(f"unknown kind {kind}")


# --------------------------------------------------------------------------- #
# process / port audit
# --------------------------------------------------------------------------- #

def windows_processes() -> list[dict]:
    """อ่านรายชื่อ process ผ่านไฟล์ชั่วคราวเพื่อไม่ให้ชื่อไทยเพี้ยนจาก console encoding"""
    tmp = Path(tempfile.gettempdir()) / f"bmcp-procs-{os.getpid()}.json"
    script = (
        "$ErrorActionPreference='SilentlyContinue';"
        "Get-CimInstance Win32_Process | "
        "Select-Object ProcessId,ParentProcessId,Name,CommandLine | "
        f"ConvertTo-Json -Compress -Depth 3 | Out-File -LiteralPath '{tmp}' -Encoding utf8"
    )
    try:
        subprocess.run(
            ["powershell", "-NoProfile", "-NonInteractive", "-Command", script],
            capture_output=True, timeout=90,
        )
        raw = tmp.read_text(encoding="utf-8-sig")
    except Exception:
        return []
    finally:
        tmp.unlink(missing_ok=True)
    if not raw.strip():
        return []
    data = json.loads(raw)
    if isinstance(data, dict):
        data = [data]
    return [d for d in data if isinstance(d, dict)]


def posix_processes() -> list[dict]:
    try:
        res = subprocess.run(["ps", "-eo", "pid=,ppid=,comm=,args="],
                             capture_output=True, text=True, timeout=30)
    except Exception:
        return []
    out = []
    for line in res.stdout.splitlines():
        parts = line.split(None, 3)
        if len(parts) < 3:
            continue
        out.append({
            "ProcessId": int(parts[0]),
            "ParentProcessId": int(parts[1]),
            "Name": parts[2],
            "CommandLine": parts[3] if len(parts) > 3 else "",
        })
    return out


def list_processes() -> list[dict]:
    return windows_processes() if os.name == "nt" else posix_processes()


def port_listeners(port: int) -> list[int]:
    if os.name == "nt":
        try:
            res = subprocess.run(["netstat", "-ano", "-p", "TCP"],
                                 capture_output=True, text=True, errors="replace", timeout=60)
        except Exception:
            return []
        pids = []
        suffix = f":{port}"
        for line in res.stdout.splitlines():
            parts = line.split()
            if len(parts) >= 5 and parts[0].upper() == "TCP" \
                    and parts[1].endswith(suffix) and parts[3].upper() == "LISTENING":
                try:
                    pids.append(int(parts[4]))
                except ValueError:
                    pass
        return pids
    for cmd in (["lsof", "-nP", f"-iTCP:{port}", "-sTCP:LISTEN", "-t"],
                ["ss", "-ltnp", f"sport = :{port}"]):
        try:
            res = subprocess.run(cmd, capture_output=True, text=True, timeout=30)
        except Exception:
            continue
        if res.returncode == 0 and res.stdout.strip():
            found = [int(x) for x in re.findall(r"\b(\d+)\b", res.stdout)]
            if found:
                return sorted(set(found))
    return []


def is_blender_mcp_server(proc: dict) -> bool:
    name = (proc.get("Name") or "").lower()
    return name in {"mcp-for-blender.exe", "mcp-for-blender",
                    "blender-mcp.exe", "blender-mcp"}


def host_app(pid: int, by_id: dict[int, dict]) -> tuple[str, int]:
    """ไล่ขึ้นไปหาแอพเจ้าภาพที่ไม่ใช่ตัวห่อ"""
    seen = set()
    current = pid
    while True:
        proc = by_id.get(current)
        if proc is None:
            return ("(ไม่ทราบ)", current)
        name = (proc.get("Name") or "").lower()
        if name and name not in WRAPPER_NAMES:
            return (proc.get("Name") or "?", current)
        parent = int(proc.get("ParentProcessId") or 0)
        if parent == 0 or parent in seen:
            return (proc.get("Name") or "?", current)
        seen.add(parent)
        current = parent


def audit_port(port: int) -> dict:
    procs = list_processes()
    by_id = {int(p.get("ProcessId", 0)): p for p in procs}

    listeners = port_listeners(port)
    listener_info = []
    for pid in listeners:
        proc = by_id.get(pid)
        listener_info.append({
            "pid": pid,
            "name": proc.get("Name") if proc else None,
            "cmd": (proc.get("CommandLine") or "")[:160] if proc else None,
        })

    servers = []
    for proc in procs:
        if not is_blender_mcp_server(proc):
            continue
        pid = int(proc.get("ProcessId", 0))
        name, host_pid = host_app(pid, by_id)
        servers.append({
            "pid": pid,
            "exe": proc.get("Name"),
            "host": name,
            "host_pid": host_pid,
            "cmd": (proc.get("CommandLine") or "")[:200],
        })

    hosts = sorted({s["host"] for s in servers})
    return {
        "port": port,
        "listeners": listener_info,
        "servers": servers,
        "server_count": len(servers),
        "hosts": hosts,
        "duplicate": len(hosts) > 1,
    }


# --------------------------------------------------------------------------- #
# toolchain check
# --------------------------------------------------------------------------- #

def check_toolchain(probe: bool) -> dict:
    uvx = find_uvx()
    uv = find_uv()
    result = {
        "uvx": str(uvx) if uvx else None,
        "uv": str(uv) if uv else None,
        "uv_version": None,
        "package_ok": None,
        "package_error": None,
    }
    if uv:
        try:
            res = subprocess.run([str(uv), "--version"], capture_output=True, text=True, timeout=60)
            result["uv_version"] = res.stdout.strip() or res.stderr.strip()
        except Exception as exc:
            result["uv_version"] = f"(รันไม่ได้: {exc})"
    if probe and (uvx or uv):
        exe = str(uvx) if uvx else str(uv)
        args = [str(PKG), "--help"] if uvx else ["tool", "run", str(PKG), "--help"]
        try:
            res = subprocess.run([exe, *args], capture_output=True, text=True,
                                 timeout=180, stdin=subprocess.DEVNULL)
            result["package_ok"] = res.returncode == 0
            if res.returncode != 0:
                result["package_error"] = (res.stderr or res.stdout).strip()[:400]
        except Exception as exc:
            result["package_ok"] = False
            result["package_error"] = str(exc)
    return result


# --------------------------------------------------------------------------- #
# main report
# --------------------------------------------------------------------------- #

def plan_for_client(spec: dict) -> dict:
    path = expand(spec["path"])
    record = {
        "id": spec["id"],
        "name": spec["name"],
        "kind": spec["kind"],
        "path": str(path),
        "exists": path.exists(),
        "entries": [],
        "action": "none",
        "action_target": None,
    }
    if not path.exists():
        record["action"] = "create"
        return record
    try:
        entries = entries_for(spec)
    except Exception as exc:
        record["action"] = "error"
        record["error"] = f"อ่าน config ไม่ได้: {exc}"
        return record

    entries = [e for e in entries if is_blender_related(e.name, e.command, e.args)]
    for e in entries:
        record["entries"].append({
            "name": e.name,
            "kind": e.kind,
            "launch": e.launch_line(),
        })

    versions = [e for e in entries if e.kind == "current" and e.name in ("blender", "mcp-for-blender")]
    legacy = [e for e in entries if e.kind == "legacy"]
    other = [e for e in entries if e.kind == "other-product"]
    upstream_any = [e for e in entries if e.kind == "current"]

    if versions:
        record["action"] = "ok"
    elif legacy:
        record["action"] = "upgrade"
        record["action_target"] = legacy[0].name
    elif upstream_any:
        record["action"] = "ok"
    elif other:
        record["action"] = "add-alongside-other-product"
    else:
        record["action"] = "add"
    return record


def print_report(records: list[dict], port: dict, tool: dict, st: Style, command: str, args: list[str]) -> None:
    print(st.bold("== Blender MCP — สถานะแอพ agent =="))
    print(st.dim(f"เป้าหมาย: {command} {' '.join(args)}"))
    print()

    width = max((len(r["name"]) for r in records), default=10)
    for rec in records:
        print(f"  {rec['name']:<{width}}  {st.dim(rec['path'])}")
        if rec["action"] == "error":
            print(f"    {st.red('✗')} {rec.get('error')}")
            continue
        if rec["action"] == "create":
            print(f"    {st.yellow('•')} ยังไม่มีไฟล์ — --apply จะสร้างให้")
            continue
        if not rec["entries"]:
            print(f"    {st.yellow('•')} ไม่มี Blender MCP entry — --apply จะเพิ่มให้")
            continue
        for ent in rec["entries"]:
            mark, color = {
                "current": ("✓", st.green),
                "legacy": ("↑", st.yellow),
                "other-product": ("–", st.dim),
                "unknown": ("?", st.yellow),
            }[ent["kind"]]
            label = {
                "current": "ใช้แพ็กเกจปัจจุบัน",
                "legacy": "ใช้ชื่อแพ็กเกจเก่า (ควรเปลี่ยน)",
                "other-product": "ผลิตภัณฑ์อื่น — ไม่แตะ",
                "unknown": "ไม่รู้จัก — ตรวจด้วยคน",
            }[ent["kind"]]
            print(f"    {color(mark)} [{ent['name']}] {ent['launch']}")
            print(f"       {st.dim(label)}")
        if rec["action"] == "ok":
            print(f"    {st.green('= ไม่ต้องแก้')}")
        elif rec["action"] in ("add", "add-alongside-other-product"):
            print(f"    {st.yellow('= --apply จะเพิ่ม entry ชื่อ blender')}")
        elif rec["action"] == "upgrade":
            target = rec["action_target"]
            print(f"    {st.yellow(f'= --apply จะอัปเดต entry [{target}] ให้ชี้ไปที่ {PKG}')}")
        note = next((c.get("note") for c in CLIENTS if c["id"] == rec["id"]), None)
        if note:
            print(f"    {st.dim('หมายเหตุ: ' + note)}")
    print()

    print(st.bold("== พอร์ตและ process =="))
    if port["listeners"]:
        for li in port["listeners"]:
            who = f"{li['name']} (pid {li['pid']})" if li["name"] else f"pid {li['pid']}"
            print(f"  {st.green('✓')} พอร์ต {port['port']} ถูกถือโดย {who}")
    else:
        print(f"  {st.yellow('•')} ไม่มีใคร listen พอร์ต {port['port']} "
              f"— เปิด Blender แล้วกด Start MCP Server ในแท็บ MCP for Blender (N panel)")

    if port["servers"]:
        print(f"  Blender MCP server ที่กำลังรัน: {port['server_count']} ตัว")
        for s in port["servers"]:
            print(f"    - {s['exe']} (pid {s['pid']}) ← เปิดโดย {s['host']}")
    else:
        print(f"  {st.dim('• ยังไม่มี Blender MCP server รันอยู่')}")

    if port["duplicate"]:
        print(f"  {st.red('⚠ พบตัวซ้ำ:')} {', '.join(port['hosts'])} "
              f"({port['server_count']} server) — ทุกตัวต่อเข้า Blender ที่พอร์ตเดียวกัน "
              f"คำสั่งอาจสลับกัน แนะนำให้เหลือแอพที่ใช้จริงตัวเดียว")
    elif port["servers"]:
        print(f"  {st.green('✓')} ไม่มีตัวซ้ำ (เจ้าภาพเดียว: {port['hosts'][0]})")

    print()
    print(st.bold("== toolchain =="))
    print(f"  uvx: {tool['uvx'] or st.red('ไม่พบ')}")
    print(f"  uv : {tool['uv'] or st.red('ไม่พบ')}  {st.dim(tool['uv_version'] or '')}")
    if tool["package_ok"] is True:
        print(f"  {st.green('✓')} รัน `uvx {PKG}` ได้ (แพ็กเกจพร้อมใช้)")
    elif tool["package_ok"] is False:
        print(f"  {st.red('✗')} รัน `uvx {PKG}` ไม่ได้: {tool['package_error']}")

    unsupported = [(n, p, w) for _, n, p, w in UNSUPPORTED if expand(p).exists()]
    if unsupported:
        print()
        print(st.bold("== ตรวจด้วยคน =="))
        for name, path, why in unsupported:
            print(f"  {st.dim('•')} {name} — {path}  {st.dim('(' + why + ')')}")


def issue_count(records: list[dict], port: dict, tool: dict) -> int:
    issues = 0
    for rec in records:
        if rec["action"] in ("error", "create", "add", "add-alongside-other-product", "upgrade"):
            issues += 1
        for ent in rec["entries"]:
            if ent["kind"] in ("legacy", "unknown"):
                issues += 1
    if port["duplicate"]:
        issues += 1
    if not port["listeners"]:
        issues += 1
    if tool["package_ok"] is False:
        issues += 1
    return issues


def main(argv: list[str] | None = None) -> int:
    _utf8_stdout()
    parser = argparse.ArgumentParser(
        description="ตรวจและตั้งค่า Blender MCP ให้ครบทุก agent app ในเครื่อง",
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    parser.add_argument("--apply", action="store_true", help="เขียน/ซ่อม config (สำรองไฟล์เดิมก่อน)")
    parser.add_argument("--dry-run", action="store_true", help="แสดงแผนของ --apply แต่ไม่เขียน")
    parser.add_argument("--yes", action="store_true", help="ไม่ต้องถามยืนยันก่อนเขียน")
    parser.add_argument("--only", default="", help="จำกัดเฉพาะบางแอพ เช่น codex,freebuff")
    parser.add_argument("--plain-uvx", action="store_true", help="ใช้คำสั่ง `uvx` แทนพาธเต็ม")
    parser.add_argument("--no-env", action="store_true", help="ไม่ต้องใส่ BLENDER_HOST/PORT/DISABLE_TELEMETRY")
    parser.add_argument("--no-probe", action="store_true", help="ข้ามการทดสอบรันแพ็กเกจ")
    parser.add_argument("--json", action="store_true", help="พิมพ์ผลลัพธ์เป็น JSON")
    parser.add_argument("--no-color", action="store_true", help="ปิดสี")
    args = parser.parse_args(argv)

    st = Style(enabled=sys.stdout.isatty() and not args.no_color)

    only = {x.strip() for x in args.only.split(",") if x.strip()}
    known = {c["id"] for c in CLIENTS}
    unknown_ids = only - known
    if unknown_ids:
        print(f"ไม่รู้จัก id: {', '.join(sorted(unknown_ids))} (ที่มี: {', '.join(sorted(known))})",
              file=sys.stderr)
        return 1
    specs = [c for c in CLIENTS if not only or c["id"] in only]

    command, cmd_args = canonical_command(args.plain_uvx)
    env = canonical_env(not args.no_env)

    records = []
    for spec in specs:
        rec = plan_for_client(spec)
        records.append(rec)

    port = audit_port(DEFAULT_PORT)
    tool = check_toolchain(probe=not args.no_probe)

    changing = [r for r in records if r["action"] in
                ("create", "add", "add-alongside-other-product", "upgrade")]

    if args.json:
        print(json.dumps({
            "target": {"command": command, "args": cmd_args, "env": env},
            "clients": records,
            "port": port,
            "toolchain": tool,
            "issues": issue_count(records, port, tool),
        }, ensure_ascii=False, indent=2))
        return 0

    print_report(records, port, tool, st, command, cmd_args)

    if not (args.apply or args.dry_run):
        n = issue_count(records, port, tool)
        print()
        if n:
            print(st.yellow(f"พบ {n} จุดที่ควรจัดการ — ใช้ --apply --dry-run เพื่อดูแผนก่อน"))
            return 2
        print(st.green("ทุกอย่างเรียบร้อย"))
        return 0

    if not changing:
        print()
        print(st.green("ไม่มีอะไรต้องเขียน — config ทุกตัวชี้ไปที่แพ็กเกจปัจจุบันแล้ว"))
        return 0

    print()
    print(st.bold("แผนการแก้:"))
    planned = []
    for rec in changing:
        if rec["action"] == "create":
            what = "สร้างไฟล์และเพิ่ม entry `blender`"
        elif rec["action"] == "upgrade":
            what = f"อัปเดต entry `{rec['action_target']}` ให้ชี้ไปที่ {PKG}"
        elif rec["action"] == "add-alongside-other-product":
            what = "เพิ่ม entry `blender` (ของเดิมที่เป็นผลิตภัณฑ์อื่นจะไม่ถูกแตะ)"
        else:
            what = "เพิ่ม entry `blender`"
        planned.append(rec)
        print(f"  • {rec['name']}: {what}")
        print(f"    {st.dim(rec['path'])}")
    print(st.dim("  ทุกไฟล์จะถูกสำรองเป็น <ชื่อไฟล์>.bak-<เวลา> ก่อนเขียน"))

    if args.dry_run:
        print()
        print(st.dim("(dry-run — ไม่ได้เขียนอะไร)"))
        return 0

    if not args.yes:
        try:
            answer = input("\nยืนยันเขียนไฟล์? [y/N] ").strip().lower()
        except EOFError:
            answer = ""
        if answer not in ("y", "yes"):
            print("ยกเลิก — ไม่ได้แก้อะไร")
            return 0

    print()
    for rec in planned:
        path = expand(rec["path"])
        before = path.read_text(encoding="utf-8-sig") if path.exists() else None
        try:
            bak = apply_entry(rec, command, cmd_args, env)
        except Exception as exc:
            print(f"  {st.red('✗')} {rec['name']}: {exc}")
            continue
        after = path.read_text(encoding="utf-8-sig")
        if before == after:
            print(f"  {st.dim('=')} {rec['name']}: ไม่มีการเปลี่ยนแปลง")
        else:
            print(f"  {st.green('✓')} {rec['name']}: เขียนแล้ว"
                  + (f" (สำรอง: {Path(bak).name})" if bak else ""))
    print()
    print(st.dim("ถ้าแอพกำลังเปิดอยู่ ให้รีสตาร์ทหรือกด Refresh/Reload ของแอพนั้นเพื่ออ่านค่าใหม่"))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except KeyboardInterrupt:
        raise SystemExit(130)
