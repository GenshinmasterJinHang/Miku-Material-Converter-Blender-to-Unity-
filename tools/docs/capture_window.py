#!/usr/bin/env python3
"""Capture the foreground application window for documentation evidence."""

from __future__ import annotations

import argparse
import ctypes
import ctypes.wintypes
import time
from pathlib import Path

from PIL import ImageGrab


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--escape", action="store_true")
    parser.add_argument("--n-panel", action="store_true")
    parser.add_argument("--right-panel", action="store_true", help="click the editor's right-edge sidebar tab")
    args = parser.parse_args()
    user32 = ctypes.windll.user32
    handle = user32.GetForegroundWindow()
    if not handle:
        raise SystemExit("MIKU_CAPTURE_FOREGROUND_WINDOW_MISSING")
    rect = ctypes.wintypes.RECT()
    if not user32.GetWindowRect(handle, ctypes.byref(rect)):
        raise SystemExit("MIKU_CAPTURE_WINDOW_RECT_MISSING")
    if args.escape:
        user32.keybd_event(0x1B, 0, 0, 0)
        user32.keybd_event(0x1B, 0, 2, 0)
        time.sleep(1.0)
        user32.GetWindowRect(handle, ctypes.byref(rect))
    if args.n_panel:
        user32.SetForegroundWindow(handle)
        user32.SetCursorPos(
            (rect.left + rect.right) // 2,
            (rect.top + rect.bottom) // 2,
        )
        user32.mouse_event(2, 0, 0, 0, 0)
        user32.mouse_event(4, 0, 0, 0, 0)
        user32.keybd_event(0x4E, 0, 0, 0)
        user32.keybd_event(0x4E, 0, 2, 0)
        time.sleep(1.0)
        user32.GetWindowRect(handle, ctypes.byref(rect))
    if args.right_panel:
        user32.SetForegroundWindow(handle)
        # Blender's collapsed N-panel exposes a narrow tab at the editor's
        # right edge.  Clicking it is more reliable than sending N when the
        # foreground window contains a text field or another active region.
        user32.SetCursorPos(rect.right - 8, rect.top + max(180, (rect.bottom - rect.top) // 3))
        user32.mouse_event(2, 0, 0, 0, 0)
        user32.mouse_event(4, 0, 0, 0, 0)
        time.sleep(1.0)
        user32.GetWindowRect(handle, ctypes.byref(rect))
    if rect.right <= rect.left or rect.bottom <= rect.top:
        raise SystemExit("MIKU_CAPTURE_WINDOW_RECT_INVALID")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    ImageGrab.grab(
        bbox=(rect.left, rect.top, rect.right, rect.bottom),
    ).save(args.output)


if __name__ == "__main__":
    main()
