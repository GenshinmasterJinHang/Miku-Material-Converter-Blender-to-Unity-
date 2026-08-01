# SPDX-FileCopyrightText: 2026 Miku Project Authors
# SPDX-License-Identifier: MIT
"""Blender Extension entrypoint for the MIT Miku semantic exporter."""

from __future__ import annotations

from .miku_blender import register as _register_exporter
from .miku_blender import unregister as _unregister_exporter
from .bake_worker import register as _register_bake_worker
from .bake_worker import unregister as _unregister_bake_worker


def register() -> None:
    _register_bake_worker()
    _register_exporter()


def unregister() -> None:
    _unregister_exporter()
    _unregister_bake_worker()
