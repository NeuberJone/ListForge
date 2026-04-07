from __future__ import annotations

import tkinter as tk

from ui import theme
from ui.widgets import build_info_row, make_card, make_title_label


class HomeView(tk.Frame):
    def __init__(self, parent: tk.Misc, controller) -> None:
        super().__init__(parent, bg=theme.active_theme().app_bg)
        self.controller = controller

        self.grid_rowconfigure(0, weight=1)
        self.grid_columnconfigure(0, weight=1)

        self._build()

    def _build(self) -> None:
        t = theme.active_theme()

        container = tk.Frame(self, bg=t.app_bg)
        container.grid(row=0, column=0, sticky="nsew")
        container.grid_columnconfigure(0, weight=1)
        container.grid_rowconfigure(0, weight=1)

        self._build_summary_card(container)

    def _build_summary_card(self, parent: tk.Misc) -> None:
        card = make_card(parent)
        card.grid(row=0, column=0, sticky="nsew")
        card.grid_columnconfigure(0, weight=1)

        make_title_label(card, "Resumo atual").pack(fill="x", padx=18, pady=(16, 10))

        build_info_row(
            card,
            label="Arquivo",
            value_var=self.controller.home_current_file_var,
        )
        build_info_row(
            card,
            label="Modo",
            value_var=self.controller.home_mode_var,
        )
        build_info_row(
            card,
            label="Separador",
            value_var=self.controller.home_separator_var,
        )
        build_info_row(
            card,
            label="Salvar saída",
            value_var=self.controller.home_output_policy_var,
        )
        build_info_row(
            card,
            label="JSON",
            value_var=self.controller.home_json_status_var,
        )
        build_info_row(
            card,
            label="Tema",
            value_var=self.controller.theme_name_var,
        )

        spacer = tk.Frame(card, bg=theme.active_theme().panel_bg, height=8)
        spacer.pack(fill="x")

        tk.Label(
            card,
            text="Tamanhos cadastrados",
            bg=theme.active_theme().panel_bg,
            fg=theme.active_theme().text_muted,
            font=(theme.FONT_FAMILY, 9, "bold"),
            anchor="w",
        ).pack(fill="x", padx=18, pady=(8, 4))

        tk.Label(
            card,
            textvariable=self.controller.size_summary_var,
            bg=theme.active_theme().panel_bg,
            fg=theme.active_theme().text,
            justify="left",
            anchor="w",
            font=(theme.FONT_FAMILY, 9),
        ).pack(fill="x", padx=18, pady=(0, 16))

    def refresh(self) -> None:
        self.controller.refresh_home_dashboard()