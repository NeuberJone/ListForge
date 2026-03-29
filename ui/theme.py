from __future__ import annotations

import tkinter as tk
from tkinter import ttk

APP_BG = "#060B16"
SIDEBAR_BG = "#0C1425"
SIDEBAR_ACTIVE = "#1E4478"
SIDEBAR_HOVER = "#1A2B47"
SIDEBAR_BORDER = "#1C2942"

TOPBAR_BG = "#111B2D"

PANEL_BG = "#0F1728"
PANEL_ALT = "#172338"
PANEL_HOVER = "#1D2C46"

BORDER = "#243654"

TEXT = "#E8EEF9"
TEXT_MUTED = "#8EA3C7"

PRIMARY = "#4C8DFF"
PRIMARY_HOVER = "#67A0FF"

SUCCESS = "#35D07F"
WARNING = "#F4A62A"
DANGER = "#FF5C70"

EDITOR_BG = "#0A1324"
EDITOR_ALT = "#0E1930"
SELECTION = "#234B88"

SIDEBAR_WIDTH = 210
TOPBAR_HEIGHT = 58
FOOTER_HEIGHT = 28

FONT_FAMILY = "Segoe UI"
FONT_MONO = "Consolas"


def configure_root(root: tk.Tk | tk.Toplevel) -> None:
    root.configure(bg=APP_BG)
    root.title("Texpad")
    root.geometry("1540x920")
    root.minsize(1320, 780)
    root.rowconfigure(0, weight=1)
    root.columnconfigure(0, weight=1)


def apply_ttk_theme(style: ttk.Style | None = None) -> ttk.Style:
    style = style or ttk.Style()

    try:
        style.theme_use("clam")
    except tk.TclError:
        pass

    style.configure("TFrame", background=APP_BG)
    style.configure("App.TFrame", background=APP_BG)
    style.configure("Card.TFrame", background=PANEL_BG)
    style.configure("Inner.TFrame", background=PANEL_ALT)

    style.configure(
        "TLabel",
        background=APP_BG,
        foreground=TEXT,
        font=(FONT_FAMILY, 10),
    )
    style.configure(
        "Muted.TLabel",
        background=APP_BG,
        foreground=TEXT_MUTED,
        font=(FONT_FAMILY, 10),
    )
    style.configure(
        "CardTitle.TLabel",
        background=PANEL_BG,
        foreground=TEXT,
        font=(FONT_FAMILY, 12, "bold"),
    )
    style.configure(
        "SectionTitle.TLabel",
        background=PANEL_BG,
        foreground=TEXT_MUTED,
        font=(FONT_FAMILY, 9, "bold"),
    )
    style.configure(
        "MetricValue.TLabel",
        background=PANEL_BG,
        foreground=PRIMARY,
        font=(FONT_FAMILY, 22, "bold"),
    )

    style.configure(
        "Card.TLabelframe",
        background=PANEL_BG,
        bordercolor=BORDER,
        relief="solid",
        borderwidth=1,
    )
    style.configure(
        "Card.TLabelframe.Label",
        background=PANEL_BG,
        foreground=TEXT_MUTED,
        font=(FONT_FAMILY, 9, "bold"),
    )

    style.configure(
        "TButton",
        background=PANEL_ALT,
        foreground=TEXT,
        bordercolor=BORDER,
        focuscolor=PANEL_ALT,
        padding=(10, 8),
        relief="flat",
        font=(FONT_FAMILY, 10),
    )
    style.map(
        "TButton",
        background=[("active", PANEL_HOVER)],
        foreground=[("active", TEXT)],
    )

    style.configure(
        "Accent.TButton",
        background=PRIMARY,
        foreground="#FFFFFF",
        bordercolor=PRIMARY,
        focuscolor=PRIMARY,
        padding=(12, 8),
        relief="flat",
        font=(FONT_FAMILY, 10, "bold"),
    )
    style.map(
        "Accent.TButton",
        background=[("active", PRIMARY_HOVER)],
        foreground=[("active", "#FFFFFF")],
    )

    style.configure(
        "Danger.TButton",
        background=DANGER,
        foreground="#FFFFFF",
        bordercolor=DANGER,
        focuscolor=DANGER,
        padding=(12, 8),
        relief="flat",
        font=(FONT_FAMILY, 10, "bold"),
    )

    style.configure(
        "TEntry",
        fieldbackground=EDITOR_ALT,
        foreground=TEXT,
        insertcolor=TEXT,
        bordercolor=BORDER,
        lightcolor=BORDER,
        darkcolor=BORDER,
        padding=6,
    )

    style.configure(
        "TCombobox",
        fieldbackground=EDITOR_ALT,
        foreground=TEXT,
        arrowcolor=TEXT,
        bordercolor=BORDER,
        lightcolor=BORDER,
        darkcolor=BORDER,
        padding=4,
    )
    style.map(
        "TCombobox",
        fieldbackground=[("readonly", EDITOR_ALT)],
        selectbackground=[("readonly", EDITOR_ALT)],
        selectforeground=[("readonly", TEXT)],
        foreground=[("readonly", TEXT)],
    )

    style.configure(
        "TCheckbutton",
        background=PANEL_BG,
        foreground=TEXT,
        font=(FONT_FAMILY, 10),
    )
    style.map(
        "TCheckbutton",
        background=[("active", PANEL_BG)],
        foreground=[("active", TEXT)],
    )

    style.configure(
        "TRadiobutton",
        background=PANEL_BG,
        foreground=TEXT,
        font=(FONT_FAMILY, 10),
    )
    style.map(
        "TRadiobutton",
        background=[("active", PANEL_BG)],
        foreground=[("active", TEXT)],
    )

    style.configure(
        "TNotebook",
        background=APP_BG,
        borderwidth=0,
        tabmargins=(0, 0, 0, 0),
    )
    style.configure(
        "TNotebook.Tab",
        background=PANEL_ALT,
        foreground=TEXT_MUTED,
        borderwidth=0,
        padding=(12, 8),
        font=(FONT_FAMILY, 10),
    )
    style.map(
        "TNotebook.Tab",
        background=[("selected", PANEL_BG), ("active", PANEL_HOVER)],
        foreground=[("selected", TEXT), ("active", TEXT)],
    )

    style.configure(
        "Treeview",
        background=PANEL_BG,
        fieldbackground=PANEL_BG,
        foreground=TEXT,
        rowheight=30,
        bordercolor=BORDER,
        lightcolor=BORDER,
        darkcolor=BORDER,
        font=(FONT_FAMILY, 10),
    )
    style.configure(
        "Treeview.Heading",
        background=TOPBAR_BG,
        foreground=TEXT_MUTED,
        relief="flat",
        borderwidth=0,
        font=(FONT_FAMILY, 9, "bold"),
    )
    style.map(
        "Treeview",
        background=[("selected", SELECTION)],
        foreground=[("selected", "#FFFFFF")],
    )
    style.map(
        "Treeview.Heading",
        background=[("active", TOPBAR_BG)],
        foreground=[("active", TEXT)],
    )

    style.configure("Vertical.TScrollbar", background=PANEL_ALT, troughcolor=EDITOR_BG, bordercolor=BORDER)
    style.configure("Horizontal.TScrollbar", background=PANEL_ALT, troughcolor=EDITOR_BG, bordercolor=BORDER)

    style.configure("TSeparator", background=BORDER)

    return style