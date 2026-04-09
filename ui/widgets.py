from __future__ import annotations

import tkinter as tk
from collections.abc import Callable, Sequence
from tkinter import ttk

from ui import theme


def make_card(parent: tk.Misc) -> tk.Frame:
    t = theme.active_theme()
    return tk.Frame(
        parent,
        bg=t.panel_bg,
        highlightbackground=t.border,
        highlightthickness=1,
        bd=0,
    )


def make_inner(parent: tk.Misc, *, bg: str | None = None) -> tk.Frame:
    t = theme.active_theme()
    return tk.Frame(parent, bg=bg or t.panel_bg, bd=0, highlightthickness=0)


def make_sidebar_button(
    parent: tk.Misc,
    *,
    text: str,
    command,
) -> tk.Button:
    t = theme.active_theme()
    return tk.Button(
        parent,
        text=text,
        command=command,
        bg=t.sidebar_bg,
        fg=t.text_muted,
        activebackground=t.sidebar_hover,
        activeforeground=t.text,
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
    t = theme.active_theme()
    button.configure(
        bg=(t.sidebar_active if active else t.sidebar_bg),
        fg=(t.text if active else t.text_muted),
        activebackground=(t.sidebar_active if active else t.sidebar_hover),
        activeforeground=t.text,
    )


def make_primary_button(parent: tk.Misc, *, text: str, command) -> tk.Button:
    t = theme.active_theme()
    return tk.Button(
        parent,
        text=text,
        command=command,
        bg=t.primary,
        fg="#FFFFFF",
        activebackground=t.primary_hover,
        activeforeground="#FFFFFF",
        relief="flat",
        bd=0,
        cursor="hand2",
        font=(theme.FONT_FAMILY, 10, "bold"),
        padx=14,
        pady=7,
    )


def make_secondary_button(parent: tk.Misc, *, text: str, command) -> tk.Button:
    t = theme.active_theme()
    return tk.Button(
        parent,
        text=text,
        command=command,
        bg=t.panel_alt,
        fg=t.text,
        activebackground=t.panel_hover,
        activeforeground=t.text,
        relief="flat",
        bd=0,
        cursor="hand2",
        font=(theme.FONT_FAMILY, 10),
        padx=12,
        pady=7,
    )


def make_quick_button(parent: tk.Misc, *, text: str, command) -> tk.Button:
    t = theme.active_theme()
    return tk.Button(
        parent,
        text=text,
        command=command,
        bg=t.panel_alt,
        fg=t.text,
        activebackground=t.panel_hover,
        activeforeground=t.text,
        relief="flat",
        bd=0,
        cursor="hand2",
        font=(theme.FONT_FAMILY, 10),
        padx=12,
        pady=18,
    )


def make_title_label(parent: tk.Misc, text: str) -> tk.Label:
    t = theme.active_theme()
    return tk.Label(
        parent,
        text=text,
        bg=t.panel_bg,
        fg=t.text,
        font=(theme.FONT_FAMILY, 12, "bold"),
        anchor="w",
    )


def make_section_label(parent: tk.Misc, text: str) -> tk.Label:
    t = theme.active_theme()
    return tk.Label(
        parent,
        text=text,
        bg=t.panel_bg,
        fg=t.text_muted,
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
    t = theme.active_theme()
    kwargs = {}
    if wraplength is not None:
        kwargs["wraplength"] = wraplength

    return tk.Label(
        parent,
        text=text,
        bg=bg or t.panel_bg,
        fg=t.text_muted,
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
    t = theme.active_theme()
    card = make_card(parent)

    tk.Label(
        card,
        text=title,
        bg=t.panel_bg,
        fg=t.text_muted,
        font=(theme.FONT_FAMILY, 9, "bold"),
        anchor="w",
    ).pack(fill="x", padx=18, pady=(16, 6))

    tk.Label(
        card,
        textvariable=value_var,
        bg=t.panel_bg,
        fg=accent,
        font=(theme.FONT_FAMILY, 22, "bold"),
        anchor="w",
    ).pack(fill="x", padx=18)

    tk.Label(
        card,
        text=subtitle,
        bg=t.panel_bg,
        fg=t.text_muted,
        font=(theme.FONT_FAMILY, 10),
        anchor="w",
    ).pack(fill="x", padx=18, pady=(4, 16))

    return card


def build_info_row(parent: tk.Misc, *, label: str, value_var: tk.StringVar) -> tk.Frame:
    t = theme.active_theme()
    row = tk.Frame(parent, bg=t.panel_bg)
    row.pack(fill="x", padx=18, pady=4)

    tk.Label(
        row,
        text=label,
        bg=t.panel_bg,
        fg=t.text_muted,
        font=(theme.FONT_FAMILY, 10),
        width=14,
        anchor="w",
    ).pack(side="left")

    tk.Label(
        row,
        textvariable=value_var,
        bg=t.panel_bg,
        fg=t.text,
        font=(theme.FONT_FAMILY, 10),
        anchor="w",
    ).pack(side="left", fill="x", expand=True)

    return row


def build_alert_box(parent: tk.Misc, *, textvariable: tk.StringVar) -> tk.Frame:
    t = theme.active_theme()
    alert_box = tk.Frame(
        parent,
        bg=t.alert_bg,
        highlightbackground=t.alert_border,
        highlightthickness=1,
        bd=0,
    )

    tk.Label(
        alert_box,
        textvariable=textvariable,
        bg=t.alert_bg,
        fg=t.alert_text,
        justify="left",
        wraplength=520,
        anchor="w",
        font=(theme.FONT_FAMILY, 10, "bold"),
    ).pack(fill="x", padx=12, pady=12)

    return alert_box


def create_text_area(parent: tk.Misc, *, background: str | None = None) -> tuple[tk.Frame, tk.Text]:
    t = theme.active_theme()
    bg = background or t.editor_bg

    holder = tk.Frame(parent, bg=bg, bd=0, highlightthickness=0)

    linebar = tk.Text(
        holder,
        width=5,
        wrap="none",
        font=(theme.FONT_MONO, 10),
        bg=t.panel_bg,
        fg=t.text_muted,
        relief="flat",
        bd=0,
        padx=6,
        pady=8,
        state="disabled",
        takefocus=0,
        cursor="arrow",
    )

    text = tk.Text(
        holder,
        wrap="none",
        font=(theme.FONT_MONO, 10),
        undo=True,
        maxundo=-1,
        autoseparators=True,
        bg=bg,
        fg=t.text,
        insertbackground=t.text,
        selectbackground=t.selection,
        selectforeground="#FFFFFF",
        relief="flat",
        bd=0,
        padx=8,
        pady=8,
    )

    yscroll = ttk.Scrollbar(holder, orient="vertical")
    xscroll = ttk.Scrollbar(holder, orient="horizontal", command=text.xview)

    def refresh_line_numbers(*_args) -> None:
        try:
            last_line = int(text.index("end-1c").split(".")[0])
        except Exception:
            last_line = 1

        content = "\n".join(str(i) for i in range(1, last_line + 1))

        linebar.configure(state="normal")
        linebar.delete("1.0", "end")
        linebar.insert("1.0", content)
        linebar.configure(state="disabled")

        first, _last = text.yview()
        linebar.yview_moveto(first)

    def on_textscroll(first, last) -> None:
        yscroll.set(first, last)
        linebar.yview_moveto(first)

    def on_scrollbar(*args) -> None:
        text.yview(*args)
        linebar.yview(*args)

    text.configure(yscrollcommand=on_textscroll, xscrollcommand=xscroll.set)
    yscroll.configure(command=on_scrollbar)

    def schedule_refresh(_event=None) -> None:
        holder.after_idle(refresh_line_numbers)

    text.bind("<KeyRelease>", schedule_refresh, add="+")
    text.bind("<MouseWheel>", schedule_refresh, add="+")
    text.bind("<ButtonRelease-1>", schedule_refresh, add="+")
    text.bind("<Return>", schedule_refresh, add="+")
    text.bind("<<Paste>>", schedule_refresh, add="+")
    text.bind("<<Cut>>", schedule_refresh, add="+")
    text.bind("<<Undo>>", schedule_refresh, add="+")
    text.bind("<<Redo>>", schedule_refresh, add="+")

    holder.rowconfigure(0, weight=1)
    holder.columnconfigure(1, weight=1)

    linebar.grid(row=0, column=0, sticky="ns")
    text.grid(row=0, column=1, sticky="nsew")
    yscroll.grid(row=0, column=2, sticky="ns")
    xscroll.grid(row=1, column=1, sticky="ew")

    refresh_line_numbers()

    return holder, text


class SegmentedControl(tk.Frame):
    def __init__(
        self,
        parent: tk.Misc,
        *,
        items: Sequence[tuple[str, str]],
        command: Callable[[str], None] | None = None,
        selected_key: str | None = None,
        equal_width: bool = True,
        gap: int = 6,
        height_pad_x: int = 12,
        height_pad_y: int = 8,
    ) -> None:
        t = theme.active_theme()
        super().__init__(parent, bg=t.panel_bg, bd=0, highlightthickness=0)

        self._items = list(items)
        self._command = command
        self._equal_width = equal_width
        self._gap = gap
        self._pad_x = height_pad_x
        self._pad_y = height_pad_y

        self._buttons: dict[str, tk.Label] = {}
        self._selected_key: str | None = None

        self._build()

        if self._items:
            initial_key = selected_key if selected_key in dict(self._items) else self._items[0][0]
            self.select(initial_key, invoke=False)

    def _build(self) -> None:
        if self._equal_width:
            for col, _item in enumerate(self._items):
                self.grid_columnconfigure(col, weight=1)

        for col, (key, label) in enumerate(self._items):
            btn = self._create_button(label=label, key=key)
            padx = (0 if col == 0 else self._gap, 0)

            if self._equal_width:
                btn.grid(row=0, column=col, sticky="ew", padx=padx)
            else:
                btn.grid(row=0, column=col, sticky="w", padx=padx)

            self._buttons[key] = btn

    def _create_button(self, *, label: str, key: str) -> tk.Label:
        t = theme.active_theme()

        btn = tk.Label(
            self,
            text=label,
            bg=t.panel_alt,
            fg=t.text_muted,
            bd=1,
            relief="solid",
            cursor="hand2",
            font=(theme.FONT_FAMILY, 10),
            padx=self._pad_x,
            pady=self._pad_y,
        )
        btn.bind("<Button-1>", lambda _e, item_key=key: self.select(item_key))
        btn.bind("<Enter>", lambda _e, item_key=key: self._on_hover(item_key, True))
        btn.bind("<Leave>", lambda _e, item_key=key: self._on_hover(item_key, False))
        return btn

    def _on_hover(self, key: str, hovering: bool) -> None:
        t = theme.active_theme()

        if key == self._selected_key:
            return

        btn = self._buttons[key]
        btn.configure(
            bg=t.panel_hover if hovering else t.panel_alt,
            fg=t.text if hovering else t.text_muted,
        )

    def _refresh_styles(self) -> None:
        t = theme.active_theme()

        for key, btn in self._buttons.items():
            selected = key == self._selected_key
            btn.configure(
                bg=t.primary if selected else t.panel_alt,
                fg="#FFFFFF" if selected else t.text_muted,
                bd=1,
                relief="solid",
            )

    def select(self, key: str, *, invoke: bool = True) -> None:
        if key not in self._buttons:
            raise KeyError(f"SegmentedControl item not found: {key}")

        self._selected_key = key
        self._refresh_styles()

        if invoke and self._command is not None:
            self._command(key)

    def get(self) -> str | None:
        return self._selected_key

    def set_items(
        self,
        items: Sequence[tuple[str, str]],
        *,
        selected_key: str | None = None,
        invoke: bool = False,
    ) -> None:
        for btn in self._buttons.values():
            btn.destroy()

        self._items = list(items)
        self._buttons.clear()
        self._selected_key = None

        if self._equal_width:
            for col in range(max(len(self._items), 1)):
                self.grid_columnconfigure(col, weight=0)
            for col, _item in enumerate(self._items):
                self.grid_columnconfigure(col, weight=1)

        self._build()

        if self._items:
            initial_key = selected_key if selected_key in dict(self._items) else self._items[0][0]
            self.select(initial_key, invoke=invoke)

    def refresh_theme(self) -> None:
        t = theme.active_theme()
        self.configure(bg=t.panel_bg)
        self._refresh_styles()