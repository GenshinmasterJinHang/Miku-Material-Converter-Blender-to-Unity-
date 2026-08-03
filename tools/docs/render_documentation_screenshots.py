"""Render deterministic, privacy-safe documentation UI captures.

The capture surfaces intentionally contain no project paths or game assets. The
layout mirrors the final Blender sidebar and Unity editor window so the public
docs remain reviewable even on a headless CI machine; the script is also the
reproducible source for all four checked-in PNGs.
"""

from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "docs/images"
W, H = 1600, 900
BG = (31, 31, 31)
PANEL = (48, 48, 48)
FIELD = (66, 66, 66)
TEXT = (224, 224, 224)
MUTED = (166, 166, 166)
BLUE = (54, 122, 190)
GREEN = (84, 160, 95)


def font(size: int, chinese: bool = False) -> ImageFont.FreeTypeFont:
    candidates = (
        (r"C:\Windows\Fonts\simsun.ttc", r"C:\Windows\Fonts\simhei.ttf", r"C:\Windows\Fonts\msyh.ttc")
        if chinese
        else (r"C:\Windows\Fonts\segoeui.ttf", r"C:\Windows\Fonts\arial.ttf")
    )
    for candidate in candidates:
        if Path(candidate).exists():
            return ImageFont.truetype(candidate, size)
    return ImageFont.load_default()


def text(draw: ImageDraw.ImageDraw, xy: tuple[int, int], value: str, size: int = 18,
         color: tuple[int, int, int] = TEXT, chinese: bool = False) -> None:
    draw.text(xy, value, font=font(size, chinese), fill=color)


def chrome(draw: ImageDraw.ImageDraw, title: str, chinese: bool) -> None:
    draw.rectangle((0, 0, W, H), fill=BG)
    draw.rectangle((0, 0, W, 36), fill=(22, 22, 22))
    text(draw, (18, 8), title, 16, TEXT, chinese)
    for x, label in ((120, "File"), (175, "Edit"), (230, "Window"), (315, "Help")):
        text(draw, (x, 8), label, 15, MUTED)


def blender_capture(chinese: bool, path: Path) -> None:
    image = Image.new("RGB", (W, H), BG)
    draw = ImageDraw.Draw(image)
    chrome(draw, "Blender 5.2.0 LTS — Shader Editor", chinese)
    draw.rectangle((0, 36, W, 82), fill=(38, 38, 38))
    text(draw, (18, 50), "Object", 16)
    text(draw, (102, 50), "View", 16, MUTED)
    text(draw, (160, 50), "Select", 16, MUTED)
    text(draw, (225, 50), "Add", 16, MUTED)
    text(draw, (278, 50), "Node", 16, MUTED)
    text(draw, (22, 98), "Miku Documentation Cube  ›  Documentation Standard PBR", 18, MUTED)
    draw.rounded_rectangle((100, 280, 440, 520), 8, fill=(62, 92, 62), outline=GREEN, width=2)
    text(draw, (122, 300), "Principled BSDF", 18)
    text(draw, (130, 352), "Base Color", 16)
    text(draw, (130, 395), "Metallic", 16)
    text(draw, (130, 438), "Roughness", 16)
    draw.rounded_rectangle((620, 350, 850, 460), 8, fill=(75, 45, 57), outline=(170, 82, 92), width=2)
    text(draw, (645, 370), "Material Output", 18)
    text(draw, (645, 415), "Surface", 16)
    right = 1180
    draw.rectangle((right, 36, W, H), fill=PANEL)
    draw.line((right, 36, right, H), fill=(100, 100, 100), width=2)
    draw.rectangle((right, 36, W, 74), fill=(57, 57, 57))
    text(draw, (right + 22, 48), "Miku", 20)
    text(draw, (right + 22, 92), "Material: Documentation Standard PBR", 15, MUTED, chinese)
    labels = [
        ("输出目录" if chinese else "Output Folder", "C:/Miku/Exports"),
        ("材质工作流" if chinese else "Material Workflow", "Standard PBR"),
        ("法线约定" if chinese else "Normal Convention", "OpenGL"),
        ("位移策略" if chinese else "Displacement Policy", "Auto"),
    ]
    y = 140
    for label, value in labels:
        text(draw, (right + 22, y), label, 15, MUTED, chinese)
        draw.rounded_rectangle((right + 22, y + 24, W - 22, y + 58), 4, fill=FIELD)
        text(draw, (right + 34, y + 32), value, 16)
        y += 82
    draw.rounded_rectangle((right + 22, y + 4, W - 22, y + 48), 4, fill=(62, 62, 62))
    text(draw, (right + 40, y + 14), ("高级" if chinese else "Advanced"), 16)
    draw.rounded_rectangle((right + 22, y + 70, W - 22, y + 120), 4, fill=BLUE)
    text(draw, (right + 84, y + 84), ("导出当前材质" if chinese else "Export Current Material"), 16)
    image.save(path)


def unity_capture(chinese: bool, path: Path) -> None:
    image = Image.new("RGB", (W, H), (35, 35, 35))
    draw = ImageDraw.Draw(image)
    chrome(draw, "Unity 6000.4.5f1 — Miku Material Creator", chinese)
    draw.rectangle((0, 36, W, 74), fill=(45, 45, 45))
    text(draw, (18, 48), "Miku", 15)
    text(draw, (78, 48), "Game Toon", 15)
    text(draw, (180, 48), "Materials", 15)
    text(draw, (290, 48), "Create Material", 15, TEXT)
    draw.rounded_rectangle((260, 100, 1340, 820), 10, fill=(53, 53, 53), outline=(103, 103, 103), width=2)
    text(draw, (300, 135), ("Miku 材质创建器" if chinese else "Miku Material Creator"), 28, TEXT, chinese)
    text(draw, (300, 192), ("工作流" if chinese else "Workflow"), 16, MUTED, chinese)
    draw.rounded_rectangle((560, 182, 1250, 222), 5, fill=FIELD)
    text(draw, (580, 192), ("原神卡通" if chinese else "Genshin Toon"), 17, TEXT, chinese)
    text(draw, (300, 250), ("材质部位" if chinese else "Material Part"), 16, MUTED, chinese)
    draw.rounded_rectangle((560, 240, 1250, 280), 5, fill=FIELD)
    text(draw, (580, 250), ("Body" if not chinese else "身体"), 17, TEXT, chinese)
    text(draw, (300, 320), ("纹理输入" if chinese else "Texture Inputs"), 20, TEXT, chinese)
    rows = [
        ("基础贴图" if chinese else "Base Map", "Body_Base.png", True),
        ("法线贴图" if chinese else "Normal Map", "Body_Normal.png", False),
        ("阴影 Ramp" if chinese else "Shadow Ramp Map", "Shadow_Ramp.png", False),
        ("金属贴图" if chinese else "Metal Map", "", False),
    ]
    y = 370
    for label, value, required in rows:
        text(draw, (300, y), label + (" *" if required else ""), 16, MUTED, chinese)
        draw.rounded_rectangle((560, y - 6, 1250, y + 34), 5, fill=FIELD)
        text(draw, (580, y + 3), value or ("未分配" if chinese else "None"), 16,
             TEXT if value else (220, 150, 100), chinese)
        y += 68
    text(draw, (300, 660), ("* 必填" if chinese else "* Required"), 15, (240, 190, 110), chinese)
    draw.rounded_rectangle((960, 720, 1250, 770), 5, fill=BLUE)
    text(draw, (1015, 735), ("创建材质" if chinese else "Create User-owned Material"), 16, TEXT, chinese)
    image.save(path)


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    blender_capture(False, OUT / "blender-standard-pbr-en.png")
    blender_capture(True, OUT / "blender-standard-pbr-zh-cn.png")
    unity_capture(False, OUT / "unity-game-material-wizard-en.png")
    unity_capture(True, OUT / "unity-game-material-wizard-zh-cn.png")


if __name__ == "__main__":
    main()
