from __future__ import annotations

import tkinter as tk

from ui import theme
from ui.widgets import build_info_row, build_metric_card, make_card


class HomeView(tk.Frame):
    def __init__(self, parent: tk.Misc, controller) -> None:
        super().__init__(parent, bg=theme.active_theme().app_bg)
        self.controller = controller

        self.grid_rowconfigure(1, weight=1)
        self.grid_columnconfigure(0, weight=1)

        self._build()

    def _build(self) -> None:
        self._build_header()
        self._build_body()

    # ------------------------------------------------------------------
    # Header
    # ------------------------------------------------------------------
    def _build_header(self) -> None:
        t = theme.active_theme()

        header = make_card(self)
        header.grid(row=0, column=0, sticky="ew", pady=(0, 10))

        inner = tk.Frame(header, bg=t.panel_bg)
        inner.pack(fill="both", expand=True, padx=16, pady=14)

        tk.Label(
            inner,
            text="Visão geral",
            bg=t.panel_bg,
            fg=t.text,
            font=(theme.FONT_FAMILY, 13, "bold"),
            anchor="w",
        ).pack(fill="x")

        tk.Label(
            inner,
            text=(
                "Resumo rápido do editor, saída e configurações. "
                "Use esta tela para acompanhar o estado atual do trabalho."
            ),
            bg=t.panel_bg,
            fg=t.text_muted,
            font=(theme.FONT_FAMILY, 10),
            justify="left",
            anchor="w",
            wraplength=980,
        ).pack(fill="x", pady=(6, 0))

    # ------------------------------------------------------------------
    # Body
    # ------------------------------------------------------------------
    def _build_body(self) -> None:
        t = theme.active_theme()

        body = tk.Frame(self, bg=t.app_bg)
        body.grid(row=1, column=0, sticky="nsew")
        body.grid_rowconfigure(2, weight=1)
        body.grid_columnconfigure(0, weight=1)
        body.grid_columnconfigure(1, weight=1)
        body.grid_columnconfigure(2, weight=1)

        # Métricas
        cards = tk.Frame(body, bg=t.app_bg)
        cards.grid(row=0, column=0, columnspan=3, sticky="ew", pady=(0, 10))
        cards.grid_columnconfigure(0, weight=1)
        cards.grid_columnconfigure(1, weight=1)
        cards.grid_columnconfigure(2, weight=1)

        card_input = build_metric_card(
            cards,
            title="Linhas na entrada",
            value_var=self.controller.home_input_lines_var,
            subtitle="Total atual no editor",
            accent=t.primary,
        )
        card_input.grid(row=0, column=0, sticky="nsew", padx=(0, 6))

        card_output = build_metric_card(
            cards,
            title="Linhas na saída",
            value_var=self.controller.home_output_lines_var,
            subtitle="Resultado organizado",
            accent=t.success,
        )
        card_output.grid(row=0, column=1, sticky="nsew", padx=6)

        card_json = build_metric_card(
            cards,
            title="JSON",
            value_var=self.controller.home_json_status_var,
            subtitle="Disponibilidade da prévia JSON",
            accent=t.warning,
        )
        card_json.grid(row=0, column=2, sticky="nsew", padx=(6, 0))

        # Status rápido
        left = make_card(body)
        left.grid(row=1, column=0, columnspan=2, sticky="nsew", padx=(0, 8), pady=(0, 10))

        left_inner = tk.Frame(left, bg=t.panel_bg)
        left_inner.pack(fill="both", expand=True, padx=16, pady=14)

        tk.Label(
            left_inner,
            text="Status atual",
            bg=t.panel_bg,
            fg=t.text,
            font=(theme.FONT_FAMILY, 11, "bold"),
            anchor="w",
        ).pack(fill="x", pady=(0, 10))

        build_info_row(
            left_inner,
            label="Arquivo",
            value_var=self.controller.current_file_var,
        )
        build_info_row(
            left_inner,
            label="Tema",
            value_var=self.controller.theme_name_var,
        )
        build_info_row(
            left_inner,
            label="Separador",
            value_var=self.controller.home_separator_var,
        )
        build_info_row(
            left_inner,
            label="Modo de texto",
            value_var=self.controller.home_mode_var,
        )
        build_info_row(
            left_inner,
            label="Saída",
            value_var=self.controller.home_output_policy_var,
        )

        # Painel lateral
        right = make_card(body)
        right.grid(row=1, column=2, sticky="nsew", padx=(8, 0), pady=(0, 10))

        right_inner = tk.Frame(right, bg=t.panel_bg)
        right_inner.pack(fill="both", expand=True, padx=16, pady=14)

        tk.Label(
            right_inner,
            text="Status",
            bg=t.panel_bg,
            fg=t.text,
            font=(theme.FONT_FAMILY, 11, "bold"),
            anchor="w",
        ).pack(fill="x", pady=(0, 10))

        tk.Label(
            right_inner,
            textvariable=self.controller.status_var,
            bg=t.panel_bg,
            fg=t.text_muted,
            font=(theme.FONT_FAMILY, 10),
            justify="left",
            anchor="w",
            wraplength=320,
        ).pack(fill="x")

        # Resumo e ações
        bottom = make_card(body)
        bottom.grid(row=2, column=0, columnspan=3, sticky="nsew")

        bottom_inner = tk.Frame(bottom, bg=t.panel_bg)
        bottom_inner.pack(fill="both", expand=True, padx=16, pady=14)
        bottom_inner.grid_columnconfigure(0, weight=1)
        bottom_inner.grid_columnconfigure(1, weight=1)

        summary_box = tk.Frame(bottom_inner, bg=t.panel_bg)
        summary_box.grid(row=0, column=0, sticky="nsew", padx=(0, 10))

        tk.Label(
            summary_box,
            text="Resumo",
            bg=t.panel_bg,
            fg=t.text,
            font=(theme.FONT_FAMILY, 11, "bold"),
            anchor="w",
        ).pack(fill="x", pady=(0, 8))

        tk.Label(
            summary_box,
            text=(
                "A tela inicial mostra um resumo rápido da entrada, da saída, "
                "do estado do JSON e das configurações principais em uso."
            ),
            bg=t.panel_bg,
            fg=t.text_muted,
            font=(theme.FONT_FAMILY, 10),
            justify="left",
            anchor="w",
            wraplength=540,
        ).pack(fill="x")

        actions_box = tk.Frame(bottom_inner, bg=t.panel_bg)
        actions_box.grid(row=0, column=1, sticky="nsew", padx=(10, 0))

        tk.Label(
            actions_box,
            text="Ações rápidas",
            bg=t.panel_bg,
            fg=t.text,
            font=(theme.FONT_FAMILY, 11, "bold"),
            anchor="w",
        ).pack(fill="x", pady=(0, 8))

        buttons = tk.Frame(actions_box, bg=t.panel_bg)
        buttons.pack(fill="x")

        tk.Button(
            buttons,
            text="Ir para Editor",
            command=lambda: self.controller.shell.show_screen("editor"),
            bg=t.primary,
            fg="#FFFFFF",
            activebackground=t.primary_hover,
            activeforeground="#FFFFFF",
            relief="flat",
            bd=0,
            cursor="hand2",
            font=(theme.FONT_FAMILY, 10, "bold"),
            padx=14,
            pady=8,
        ).pack(side="left")

        tk.Button(
            buttons,
            text="Ir para Configurações",
            command=lambda: self.controller.shell.show_screen("settings"),
            bg=t.panel_alt,
            fg=t.text,
            activebackground=t.panel_hover,
            activeforeground=t.text,
            relief="flat",
            bd=0,
            cursor="hand2",
            font=(theme.FONT_FAMILY, 10),
            padx=12,
            pady=8,
        ).pack(side="left", padx=(8, 0))

        tk.Button(
            buttons,
            text="Abrir Manual",
            command=lambda: self.controller.shell.show_screen("manual"),
            bg=t.panel_alt,
            fg=t.text,
            activebackground=t.panel_hover,
            activeforeground=t.text,
            relief="flat",
            bd=0,
            cursor="hand2",
            font=(theme.FONT_FAMILY, 10),
            padx=12,
            pady=8,
        ).pack(side="left", padx=(8, 0))

    def refresh_theme(self) -> None:
        t = theme.active_theme()
        self.configure(bg=t.app_bg)