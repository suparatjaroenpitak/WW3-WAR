"""Blender utility for a future tile export. Not run in this session.

Blender MCP handshake failed, so this script must not be described as having
produced a blend or glb file.
"""

import bpy


def main():
    raise RuntimeError(
        "Export was not executed. Connect Blender MCP and pass a verified tile mesh before calling this script."
    )


if __name__ == "__main__":
    main()
