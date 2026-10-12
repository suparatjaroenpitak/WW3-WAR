"""Read-only, bounded supplementary inspection of an installed ROME II copy.

This is an ordinary Python inspector, NOT an REA provider. It never launches
the game, extracts pack payloads, writes into the installation, or copies art.
The separate rea-*.json files are the actual REA CLI results.
"""
from __future__ import annotations

import argparse
import hashlib
import html
import json
import re
import struct
from pathlib import Path
from xml.etree import ElementTree


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def identity(path: Path) -> dict:
    data = path.read_bytes()
    return {"path": str(path), "bytes": len(data), "sha256": digest(data)}


def pe_observation(path: Path) -> dict:
    data = path.read_bytes()
    pe = struct.unpack_from("<I", data, 0x3C)[0]
    assert data[:2] == b"MZ" and data[pe:pe + 4] == b"PE\0\0"
    machine, count, timestamp = struct.unpack_from("<HHI", data, pe + 4)
    optional_size = struct.unpack_from("<H", data, pe + 20)[0]
    optional = pe + 24
    magic = struct.unpack_from("<H", data, optional)[0]
    assert magic in (0x10B, 0x20B)
    section_offset = optional + optional_size
    sections = []
    for index in range(count):
        offset = section_offset + 40 * index
        name = data[offset:offset + 8].split(b"\0")[0].decode("ascii", "replace")
        virtual_size, rva, raw_size, raw_offset = struct.unpack_from("<IIII", data, offset + 8)
        sections.append({"name": name, "rva": rva, "virtual_size": virtual_size,
                         "raw_offset": raw_offset, "raw_size": raw_size})

    def file_offset(rva: int) -> int:
        for section in sections:
            delta = rva - section["rva"]
            if 0 <= delta < section["raw_size"]:
                return section["raw_offset"] + delta
        raise ValueError(f"Unmapped file-backed RVA {rva:x}")

    def c_string(offset: int) -> str:
        end = data.index(b"\0", offset, min(len(data), offset + 4096))
        return data[offset:end].decode("ascii", "replace")

    directories = optional + (96 if magic == 0x10B else 112)
    import_rva, import_size = struct.unpack_from("<II", data, directories + 8)
    imported_libraries = []
    if import_rva:
        base = file_offset(import_rva)
        for index in range(min(import_size // 20, 4096)):
            fields = struct.unpack_from("<IIIII", data, base + index * 20)
            if not any(fields):
                break
            imported_libraries.append({"name": c_string(file_offset(fields[3])),
                                       "descriptor_file_offset": base + index * 20})
    cli_rva, cli_size = struct.unpack_from("<II", data, directories + 14 * 8)
    terms = ["campaign", "battle", "morale", "formation", "recruit", "diplomacy"]
    hits = {term: [] for term in terms}
    counts = {term: 0 for term in terms}
    for match in re.finditer(rb"[\x20-\x7e]{6,}", data):
        text = match.group().decode("ascii")
        lower = text.lower()
        for term in terms:
            if term in lower:
                counts[term] += 1
                if len(hits[term]) < 8:
                    hits[term].append({"file_offset": match.start(), "text": text[:240],
                                       "original_length": len(text), "truncated": len(text) > 240})
    return {"identity": identity(path), "machine": machine, "pe_magic": magic,
            "coff_timestamp_raw": timestamp, "sections": sections,
            "imported_libraries": imported_libraries,
            "cli_data_directory": {"rva": cli_rva, "size": cli_size},
            "ascii_string_search": {"minimum_length": 6, "terms": terms,
                                    "occurrence_counts": counts, "max_examples_per_term": 8,
                                    "examples": hits},
            "limitations": ["Custom Python PE/static-string inspection; not REA Evidence.",
                            "Import declarations do not prove runtime loading.",
                            "Strings are uninterpreted literals; no function ownership or execution established.",
                            "No disassembly, decompilation, unpacking or resource payload extraction."]}


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--install", type=Path, required=True)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    install = args.install.resolve()
    out = args.out.resolve()
    if out == install or install in out.parents:
        raise ValueError("Output must be outside the game installation")
    out.mkdir(parents=True, exist_ok=True)

    def save(name: str, value: dict) -> None:
        canonical = json.dumps(value, sort_keys=True, ensure_ascii=False).encode("utf-8")
        record = {"observation_id": "obs_" + digest(canonical),
                  "origin": "custom-read-only-python-inspector", "result": value}
        (out / name).write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    manifest_text = args.manifest.read_text(encoding="utf-8")
    allowed = ["appid", "name", "installdir", "buildid", "SizeOnDisk", "language"]
    manifest = {}
    for key in allowed:
        match = re.search(r'"' + re.escape(key) + r'"\s+"([^"]*)"', manifest_text)
        if match:
            manifest[key] = match.group(1)
    pack_headers = []
    for path in sorted((install / "data").glob("*.pack")):
        with path.open("rb") as stream:
            header = stream.read(32)
        pack_headers.append({"path": str(path.relative_to(install)), "bytes": path.stat().st_size,
                             "first_32_bytes_hex": header.hex(),
                             "magic_ascii": header[:4].decode("ascii", "replace"),
                             "header_sha256": digest(header)})
    save("local-install-inventory.json", {
        "install_path": str(install), "steam_manifest_identity": identity(args.manifest),
        "steam_manifest_selected_fields": manifest,
        "top_level_files": [{"name": p.name, "bytes": p.stat().st_size} for p in sorted(install.iterdir()) if p.is_file()],
        "pack_count": len(pack_headers), "pack_total_bytes": sum(p["bytes"] for p in pack_headers),
        "pack_headers": pack_headers,
        "campaign_startpos_files": [str(p.relative_to(install)) for p in sorted((install / "data/campaigns").rglob("startpos.esf"))],
        "limitations": ["Pack inventory only: names, sizes, raw header bytes; no member index decoded.",
                        "Large pack contents were not fully hashed; header hashes identify only first 32 bytes.",
                        "Manifest name/build are Steam declarations, not independent vendor verification."]})
    for name in ["Rome2.exe", "empire.retail.dll"]:
        save("local-pe-" + name.replace(".", "-") + ".json", pe_observation(install / name))
    keys_path = install / "data/text/default_keys.xml"
    keys = ElementTree.fromstring(keys_path.read_text(encoding="utf-8-sig"))
    selected = []
    for keyset in keys.findall("keyset"):
        for category in keyset.findall("category"):
            for group in category.findall("group"):
                for function in group.findall("func"):
                    name = function.attrib.get("name", "")
                    if re.search("select|move|attack|formation|pause|speed|end_turn|retreat|fire", name):
                        selected.append({"keyset": keyset.attrib.get("name"), "category": category.attrib.get("name"),
                                         "group": group.attrib.get("name"), "function": name,
                                         "keys": [{"text": key.text, **key.attrib} for key in function.findall("key")]})
    save("local-input-declarations.json", {"identity": identity(keys_path), "modes": [m.text for m in keys.findall("mode")],
                                          "selected_functions": selected,
                                          "limitations": ["Declared defaults only; active settings and runtime event bindings not observed."]})
    manual_dir = install / "data/encyclopedia/en/manual/single-player"
    prefixes = ["0012_", "0016_", "0017_", "0026_", "0038_", "0039_", "0044_", "0045_", "0075_", "0077_", "0081_", "0087_"]
    records = []
    for path in sorted(manual_dir.glob("*.html")):
        if not any(path.name.startswith(prefix) for prefix in prefixes):
            continue
        source = path.read_text(encoding="utf-8", errors="replace")
        headings, paragraphs = [], []
        for match in re.finditer(r"<(h1|h4|p)\b[^>]*>(.*?)</\1>", source, re.DOTALL | re.IGNORECASE):
            plain = re.sub(r"\s+", " ", html.unescape(re.sub(r"<[^>]+>", "", match.group(2)))).strip()
            if not plain:
                continue
            item = {"line": source[:match.start()].count("\n") + 1, "text": plain[:380],
                    "truncated": len(plain) > 380, "full_text_sha256": digest(plain.encode("utf-8"))}
            (paragraphs if match.group(1).lower() == "p" else headings).append(item)
        records.append({"identity": identity(path), "headings": headings, "paragraph_previews": paragraphs})
    save("local-manual-observations.json", {"files": records,
                                            "limitations": ["Installed vendor documentation statements, not live gameplay or engine internals.",
                                                            "Paragraph previews capped at 380 characters; complete source remains in installation.",
                                                            "Manual may describe features beyond this build/configuration; no balance constants inferred."]})
    print(json.dumps({"output_directory": str(out), "pack_count": len(pack_headers), "manual_files": len(records)}))


if __name__ == "__main__":
    main()
