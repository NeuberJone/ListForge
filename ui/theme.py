from __future__ import annotations

import tkinter as tk
from dataclasses import dataclass
from tkinter import ttk


@dataclass(frozen=True)
class ThemePalette:
    name: str
    app_bg: str
    sidebar_bg: str
    sidebar_active: str
    sidebar_hover: str
    sidebar_border: str
    topbar_bg: str
    panel_bg: str
    panel_alt: str
    panel_hover: str
    border: str
    text: str
    text_muted: str
    primary: str
    primary_hover: str
    success: str
    warning: str
    danger: str
    editor_bg: str
    editor_alt: str
    selection: str
    alert_bg: str
    alert_border: str
    alert_text: str


DARK_THEME = ThemePalette(
    name="Texpad Dark",
    app_bg="#060B16",
    sidebar_bg="#0C1425",
    sidebar_active="#1E4478",
    sidebar_hover="#1A2B47",
    sidebar_border="#1C2942",
    topbar_bg="#111B2D",
    panel_bg="#0F1728",
    panel_alt="#172338",
    panel_hover="#1D2C46",
    border="#243654",
    text="#E8EEF9",
    text_muted="#8EA3C7",
    primary="#4C8DFF",
    primary_hover="#67A0FF",
    success="#35D07F",
    warning="#F4A62A",
    danger="#FF5C70",
    editor_bg="#0A1324",
    editor_alt="#0E1930",
    selection="#234B88",
    alert_bg="#5A2A00",
    alert_border="#F0B400",
    alert_text="#FFDFA3",
)

SISBOLT_THEME = ThemePalette(
    name="SisBolt Dark",
    app_bg="#2F2F32",
    sidebar_bg="#2A2A2D",
    sidebar_active="#3F73DA",
    sidebar_hover="#38383B",
    sidebar_border="#454549",
    topbar_bg="#343437",
    panel_bg="#3C3C40",
    panel_alt="#55565A",
    panel_hover="#626369",
    border="#4A73CF",
    text="#F2F2F2",
    text_muted="#CDCDCD",
    primary="#3F73DA",
    primary_hover="#5A88E6",
    success="#72E072",
    warning="#D9B256",
    danger="#FF6565",
    editor_bg="#343437",
    editor_alt="#44464B",
    selection="#4F7DE0",
    alert_bg="#5B411B",
    alert_border="#C89A41",
    alert_text="#FFE2A8",
)

THEMES: dict[str, ThemePalette] = {
    DARK_THEME.name: DARK_THEME,
    SISBOLT_THEME.name: SISBOLT_THEME,
}

DEFAULT_THEME_NAME = DARK_THEME.name

SIDEBAR_WIDTH = 210
TOPBAR_HEIGHT = 58
FOOTER_HEIGHT = 28

FONT_FAMILY = "Segoe UI"
FONT_MONO = "Consolas"

_active_theme: ThemePalette = DARK_THEME


def get_theme(theme_name: str | None = None) -> ThemePalette:
    if theme_name and theme_name in THEMES:
        return THEMES[theme_name]
    return _active_theme


def get_theme_names() -> list[str]:
    return list(THEMES.keys())


def set_active_theme(theme_name: str | None) -> ThemePalette:
    global _active_theme
    _active_theme = THEMES.get(theme_name or "", THEMES[DEFAULT_THEME_NAME])
    return _active_theme


def active_theme() -> ThemePalette:
    return _active_theme


def configure_root(root: tk.Tk | tk.Toplevel, theme_name: str | None = None) -> None:
    current = set_active_theme(theme_name)
    root.configure(bg=current.app_bg)
    root.title("Texpad")
    root.geometry("1540x920")
    root.minsize(1320, 780)
    root.rowconfigure(0, weight=1)
    root.columnconfigure(0, weight=1)


def apply_ttk_theme(style: ttk.Style | None = None, theme_name: str | None = None) -> ttk.Style:
    current = set_active_theme(theme_name)
    style = style or ttk.Style()

    try:
        style.theme_use("clam")
    except tk.TclError:
        pass

    style.configure("TFrame", background=current.app_bg)
    style.configure("App.TFrame", background=current.app_bg)
    style.configure("Card.TFrame", background=current.panel_bg)
    style.configure("Inner.TFrame", background=current.panel_alt)

    style.configure(
        "TLabel",
        background=current.app_bg,
        foreground=current.text,
        font=(FONT_FAMILY, 10),
    )
    style.configure(
        "Muted.TLabel",
        background=current.app_bg,
        foreground=current.text_muted,
        font=(FONT_FAMILY, 10),
    )
    style.configure(
        "CardTitle.TLabel",
        background=current.panel_bg,
        foreground=current.text,
        font=(FONT_FAMILY, 12, "bold"),
    )
    style.configure(
        "SectionTitle.TLabel",
        background=current.panel_bg,
        foreground=current.text_muted,
        font=(FONT_FAMILY, 9, "bold"),
    )
    style.configure(
        "MetricValue.TLabel",
        background=current.panel_bg,
        foreground=current.primary,
        font=(FONT_FAMILY, 22, "bold"),
    )

    style.configure(
        "Card.TLabelframe",
        background=current.panel_bg,
        bordercolor=current.border,
        relief="solid",
        borderwidth=1,
    )
    style.configure(
        "Card.TLabelframe.Label",
        background=current.panel_bg,
        foreground=current.text_muted,
        font=(FONT_FAMILY, 9, "bold"),
    )

    style.configure(
        "TButton",
        background=current.panel_alt,
        foreground=current.text,
        bordercolor=current.border,
        focuscolor=current.panel_alt,
        padding=(10, 8),
        relief="flat",
        font=(FONT_FAMILY, 10),
    )
    style.map(
        "TButton",
        background=[("active", current.panel_hover)],
        foreground=[("active", current.text)],
    )

    style.configure(
        "Accent.TButton",
        background=current.primary,
        foreground="#FFFFFF",
        bordercolor=current.primary,
        focuscolor=current.primary,
        padding=(12, 8),
        relief="flat",
        font=(FONT_FAMILY, 10, "bold"),
    )
    style.map(
        "Accent.TButton",
        background=[("active", current.primary_hover)],
        foreground=[("active", "#FFFFFF")],
    )

    style.configure(
        "Danger.TButton",
        background=current.danger,
        foreground="#FFFFFF",
        bordercolor=current.danger,
        focuscolor=current.danger,
        padding=(12, 8),
        relief="flat",
        font=(FONT_FAMILY, 10, "bold"),
    )

    style.configure(
        "TEntry",
        fieldbackground=current.editor_alt,
        foreground=current.text,
        insertcolor=current.text,
        bordercolor=current.border,
        lightcolor=current.border,
        darkcolor=current.border,
        padding=6,
    )

    style.configure(
        "TCombobox",
        fieldbackground=current.editor_alt,
        foreground=current.text,
        arrowcolor=current.text,
        bordercolor=current.border,
        lightcolor=current.border,
        darkcolor=current.border,
        padding=4,
    )
    style.map(
        "TCombobox",
        fieldbackground=[("readonly", current.editor_alt)],
        selectbackground=[("readonly", current.editor_alt)],
        selectforeground=[("readonly", current.text)],
        foreground=[("readonly", current.text)],
    )

    style.configure(
        "TCheckbutton",
        background=current.panel_bg,
        foreground=current.text,
        font=(FONT_FAMILY, 10),
    )
    style.map(
        "TCheckbutton",
        background=[("active", current.panel_bg)],
        foreground=[("active", current.text)],
    )

    style.configure(
        "TRadiobutton",
        background=current.panel_bg,
        foreground=current.text,
        font=(FONT_FAMILY, 10),
    )
    style.map(
        "TRadiobutton",
        background=[("active", current.panel_bg)],
        foreground=[("active", current.text)],
    )

    # Mantido neutro caso algum Notebook ainda exista em outra área.
    style.configure(
        "TNotebook",
        background=current.app_bg,
        borderwidth=0,
        tabmargins=(0, 0, 0, 0),
    )
    style.configure(
        "TNotebook.Tab",
        background=current.panel_alt,
        foreground=current.text_muted,
        bordercolor=current.border,
        lightcolor=current.border,
        darkcolor=current.border,
        relief="flat",
        padding=(14, 6),
        font=(FONT_FAMILY, 10),
    )
    style.map(
        "TNotebook.Tab",
        background=[
            ("selected", current.panel_bg),
            ("active", current.panel_hover),
            ("!selected", current.panel_alt),
        ],
        foreground=[
            ("selected", current.text),
            ("active", current.text),
            ("!selected", current.text_muted),
        ],
    )

    style.configure(
        "Treeview",
        background=current.panel_bg,
        fieldbackground=current.panel_bg,
        foreground=current.text,
        rowheight=30,
        bordercolor=current.border,
        lightcolor=current.border,
        darkcolor=current.border,
        font=(FONT_FAMILY, 10),
    )
    style.configure(
        "Treeview.Heading",
        background=current.topbar_bg,
        foreground=current.text_muted,
        relief="flat",
        borderwidth=0,
        font=(FONT_FAMILY, 9, "bold"),
    )
    style.map(
        "Treeview",
        background=[("selected", current.selection)],
        foreground=[("selected", "#FFFFFF")],
    )
    style.map(
        "Treeview.Heading",
        background=[("active", current.topbar_bg)],
        foreground=[("active", current.text)],
    )

    style.configure(
        "Vertical.TScrollbar",
        background=current.panel_alt,
        troughcolor=current.editor_bg,
        bordercolor=current.border,
    )
    style.configure(
        "Horizontal.TScrollbar",
        background=current.panel_alt,
        troughcolor=current.editor_bg,
        bordercolor=current.border,
    )

    style.configure("TSeparator", background=current.border)

    return style