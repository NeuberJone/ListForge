from __future__ import annotations

import tkinter as tk
from tkinter import ttk

from ui import theme


def make_card(parent: tk.Misc) -> tk.Frame:
    return tk.Frame(
        parent,
        bg=theme.PANEL_BG,
        highlightbackground=theme.BORDER,
        highlightthickness=1,
        bd=0,
    )


def make_inner(parent: tk.Misc, *, bg: str | None = None) -> tk.Frame:
    return tk.Frame(parent, bg=bg or theme.PANEL_BG, bd=0, highlightthickness=0)


def make_sidebar_button(
    parent: tk.Misc,
    *,
    text: str,
    command,
) -> tk.Button:
    return tk.Button(
        parent,
        text=text,
        command=command,
        bg=theme.SIDEBAR_BG,
        fg=theme.TEXT_MUTED,
        activebackground=theme.SIDEBAR_HOVER,
        activeforeground=theme.TEXT,
        relief="flat",
        bd=0,
        highlightthickness=0,
        anchor="w",
        cursor="hand2",
        font=(theme.FONT_FAMILY, 10),
        padx=12,
        pady=10,
    )


def set_sidebar_button_active(button: tk.Button, active: bool) -> None:
    button.configure(
        bg=(theme.SIDEBAR_ACTIVE if active else theme.SIDEBAR_BG),
        fg=(theme.TEXT if active else theme.TEXT_MUTED),
        activebackground=(theme.SIDEBAR_ACTIVE if active else theme.SIDEBAR_HOVER),
        activeforeground=theme.TEXT,
    )


def make_primary_button(parent: tk.Misc, *, text: str, command) -> tk.Button:
    return tk.Button(
        parent,
        text=text,
        command=command,
        bg=theme.PRIMARY,
        fg="#FFFFFF",
        activebackground=theme.PRIMARY_HOVER,
        activeforeground="#FFFFFF",
        relief="flat",
        bd=0,
        cursor="hand2",
        font=(theme.FONT_FAMILY, 10, "bold"),
        padx=14,
        pady=7,
    )


def make_secondary_button(parent: tk.Misc, *, text: str, command) -> tk.Button:
    return tk.Button(
        parent,
        text=text,
        command=command,
        bg=theme.PANEL_ALT,
        fg=theme.TEXT,
        activebackground=theme.PANEL_HOVER,
        activeforeground=theme.TEXT,
        relief="flat",
        bd=0,
        cursor="hand2",
        font=(theme.FONT_FAMILY, 10),
        padx=12,
        pady=7,
    )


def make_quick_button(parent: tk.Misc, *, text: str, command) -> tk.Button:
    return tk.Button(
        parent,
        text=text,
        command=command,
        bg=theme.PANEL_ALT,
        fg=theme.TEXT,
        activebackground=theme.PANEL_HOVER,
        activeforeground=theme.TEXT,
        relief="flat",
        bd=0,
        cursor="hand2",
        font=(theme.FONT_FAMILY, 10),
        padx=12,
        pady=18,
    )


def make_title_label(parent: tk.Misc, text: str) -> tk.Label:
    return tk.Label(
        parent,
        text=text,
        bg=theme.PANEL_BG,
        fg=theme.TEXT,
        font=(theme.FONT_FAMILY, 12, "bold"),
        anchor="w",
    )


def make_section_label(parent: tk.Misc, text: str) -> tk.Label:
    return tk.Label(
        parent,
        text=text,
        bg=theme.PANEL_BG,
        fg=theme.TEXT_MUTED,
        font=(theme.FONT_FAMILY, 9, "bold"),
        anchor="w",
    )


def make_muted_label(
    parent: tk.Misc,
    text: str = "",
    *,
    bg: str | None = None,
    wraplength: int | None = None,
    justify: str = "left",
) -> tk.Label:
    kwargs = {}
    if wraplength is not None:
        kwargs["wraplength"] = wraplength

    return tk.Label(
        parent,
        text=text,
        bg=bg or theme.PANEL_BG,
        fg=theme.TEXT_MUTED,
        font=(theme.FONT_FAMILY, 10),
        justify=justify,
        anchor="w",
        **kwargs,
    )


def build_metric_card(
    parent: tk.Misc,
    *,
    title: str,
    value_var: tk.StringVar,
    subtitle: str,
    accent: str,
) -> tk.Frame:
    card = make_card(parent)

    tk.Label(
        card,
        text=title,
        bg=theme.PANEL_BG,
        fg=theme.TEXT_MUTED,
        font=(theme.FONT_FAMILY, 9, "bold"),
        anchor="w",
    ).pack(fill="x", padx=18, pady=(16, 6))

    tk.Label(
        card,
        textvariable=value_var,
        bg=theme.PANEL_BG,
        fg=accent,
        font=(theme.FONT_FAMILY, 22, "bold"),
        anchor="w",
    ).pack(fill="x", padx=18)

    tk.Label(
        card,
        text=subtitle,
        bg=theme.PANEL_BG,
        fg=theme.TEXT_MUTED,
        font=(theme.FONT_FAMILY, 10),
        anchor="w",
    ).pack(fill="x", padx=18, pady=(4, 16))

    return card


def build_info_row(parent: tk.Misc, *, label: str, value_var: tk.StringVar) -> tk.Frame:
    row = tk.Frame(parent, bg=theme.PANEL_BG)
    row.pack(fill="x", padx=18, pady=4)

    tk.Label(
        row,
        text=label,
        bg=theme.PANEL_BG,
        fg=theme.TEXT_MUTED,
        font=(theme.FONT_FAMILY, 10),
        width=14,
        anchor="w",
    ).pack(side="left")

    tk.Label(
        row,
        textvariable=value_var,
        bg=theme.PANEL_BG,
        fg=theme.TEXT,
        font=(theme.FONT_FAMILY, 10),
        anchor="w",
    ).pack(side="left", fill="x", expand=True)

    return row


def build_alert_box(parent: tk.Misc, *, textvariable: tk.StringVar) -> tk.Frame:
    alert_box = tk.Frame(
        parent,
        bg="#5A2A00",
        highlightbackground="#F0B400",
        highlightthickness=1,
        bd=0,
    )

    tk.Label(
        alert_box,
        textvariable=textvariable,
        bg="#5A2A00",
        fg="#FFDFA3",
        justify="left",
        wraplength=520,
        anchor="w",
        font=(theme.FONT_FAMILY, 10, "bold"),
    ).pack(fill="x", padx=12, pady=12)

    return alert_box


def create_text_area(parent: tk.Misc, *, background: str) -> tuple[tk.Frame, tk.Text]:
    holder = tk.Frame(parent, bg=background, bd=0, highlightthickness=0)

    text = tk.Text(
        holder,
        wrap="none",
        font=(theme.FONT_MONO, 10),
        undo=True,
        maxundo=-1,
        autoseparators=True,
        bg=background,
        fg=theme.TEXT,
        insertbackground=theme.TEXT,
        selectbackground=theme.SELECTION,
        selectforeground="#FFFFFF",
        relief="flat",
        bd=0,
        padx=8,
        pady=8,
    )

    yscroll = ttk.Scrollbar(holder, orient="vertical", command=text.yview)
    xscroll = ttk.Scrollbar(holder, orient="horizontal", command=text.xview)

    text.configure(yscrollcommand=yscroll.set, xscrollcommand=xscroll.set)

    holder.rowconfigure(0, weight=1)
    holder.columnconfigure(0, weight=1)

    text.grid(row=0, column=0, sticky="nsew")
    yscroll.grid(row=0, column=1, sticky="ns")
    xscroll.grid(row=1, column=0, sticky="ew")

    return holder, text