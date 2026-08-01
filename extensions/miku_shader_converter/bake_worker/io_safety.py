# SPDX-FileCopyrightText: 2026 Miku Project Authors
# SPDX-License-Identifier: GPL-2.0-or-later
"""Safe, deterministic file-output helpers for the Blender integration."""

from __future__ import annotations

import json
import os
import tempfile
from pathlib import Path
from typing import Any, Optional, Union


PathLike = Union[str, os.PathLike]


class UnsafeOutputPath(ValueError):
    def __init__(self, path: PathLike, output_root: PathLike):
        self.code = "unsafe_output_path"
        self.path = os.fspath(path)
        self.output_root = os.fspath(output_root)
        super().__init__(f"unsafe_output_path: {self.path!r} escapes {self.output_root!r}")


def resolve_output_path(path: PathLike, output_root: PathLike) -> Path:
    root = Path(output_root).expanduser().resolve(strict=False)
    candidate = Path(path).expanduser()
    if not candidate.is_absolute():
        candidate = root / candidate
    candidate = candidate.resolve(strict=False)
    try:
        candidate.relative_to(root)
    except ValueError as exc:
        raise UnsafeOutputPath(candidate, root) from exc
    if candidate == root:
        raise UnsafeOutputPath(candidate, root)
    return candidate


def atomic_write_text(path: PathLike, text: str, output_root: Optional[PathLike] = None) -> Path:
    target = Path(path).expanduser()
    root = Path(output_root).expanduser() if output_root is not None else target.parent
    target = resolve_output_path(target, root)
    target.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(prefix=f".{target.name}.", suffix=".tmp", dir=str(target.parent))
    temporary = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as handle:
            handle.write(text)
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(str(temporary), str(target))
    finally:
        if temporary.exists():
            temporary.unlink()
    return target


def atomic_write_json(
    path: PathLike,
    value: Any,
    *,
    pretty: bool = True,
    output_root: Optional[PathLike] = None,
) -> Path:
    text = json.dumps(
        value,
        ensure_ascii=False,
        indent=2 if pretty else None,
        sort_keys=False,
        allow_nan=False,
    )
    return atomic_write_text(path, text + "\n", output_root=output_root)
