#!/usr/bin/env python3

from pathlib import Path
import shutil

ROOT = Path(".").resolve()

DIR_NAMES = {
    # Unity
    "Library",
    "Temp",
    "Obj",
    "Logs",
    "MemoryCaptures",
    "UserSettings",

    # IDEs
    ".vs",
    ".idea",

    # macOS
    "__MACOSX",
}

FILE_NAMES = {
    ".DS_Store",
    "Thumbs.db",
    "Desktop.ini",
}

EXTENSIONS = {
    ".zip",
    ".rar",
    ".7z",
    ".tar",
    ".gz",
    ".xz",
}

APP_DIR_SUFFIX = ".app"

def rm(p):
    try:
        if p.is_dir():
            print("DIR ", p)
            shutil.rmtree(p)
        else:
            print("FILE", p)
            p.unlink()
    except Exception as e:
        print("FAIL", p, e)

for p in ROOT.rglob("*"):
    name = p.name

    if p.is_dir():
        if name in DIR_NAMES or name.endswith(APP_DIR_SUFFIX):
            rm(p)
            continue

    if p.is_file():
        if name in FILE_NAMES:
            rm(p)
            continue

        if p.suffix.lower() in EXTENSIONS:
            rm(p)
            continue

        if name.startswith("._"):
            rm(p)
            continue
