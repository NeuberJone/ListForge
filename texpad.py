from __future__ import annotations

import os
import re
from pathlib import Path
import tkinter as tk
from tkinter import filedialog, messagebox, simpledialog, ttk

from texpad_config import (
    APP_NAME,
    BACKUP_DIR,
    DEFAULT_CONFIG,
    create_backup,
    load_config,
    reset_config,
    save_config,
)
from texpad_core import (
    build_json_preview,
    build_orders_from_orderlist,
    build_output,
    clean_text_by_separator,
    export_json,
    export_output_text,
    normalize_separator,
    process_text,
    sanitize_base_filename,
    separator_label,
)
from texpad_sizes import (
    GROUP_CHILD,
    GROUP_FEMALE,
    GROUP_LABELS,
    GROUP_MALE,
    build_group_sizes,
    load_size_config,
    parse_csv_tokens,
    reset_size_config,
    save_size_config,
    tokens_to_csv,
    update_group_config,
)

CASE_LABEL_TO_VALUE = {
    "Original": "original",
    "Tudo maiúsculo": "upper",
    "Tudo minúsculo": "lower",
}
CASE_VALUE_TO_LABEL = {value: label for label, value in CASE_LABEL_TO_VALUE.items()}

# ----------------------------------------------------------------------
# Theme
# ----------------------------------------------------------------------
COLOR_APP_BG = "#060B16"
COLOR_SIDEBAR = "#0C1425"
COLOR_SIDEBAR_ACTIVE = "#1E4478"
COLOR_SIDEBAR_HOVER = "#1A2B47"
COLOR_SIDEBAR_BORDER = "#1C2942"
COLOR_TOPBAR = "#111B2D"
COLOR_PANEL = "#0F1728"
COLOR_PANEL_ALT = "#172338"
COLOR_PANEL_HOVER = "#1D2C46"
COLOR_BORDER = "#243654"
COLOR_TEXT = "#E8EEF9"
COLOR_TEXT_MUTED = "#8EA3C7"
COLOR_PRIMARY = "#4C8DFF"
COLOR_PRIMARY_HOVER = "#67A0FF"
COLOR_SUCCESS = "#35D07F"
COLOR_WARNING = "#F4A62A"
COLOR_DANGER = "#FF5C70"
COLOR_EDITOR_BG = "#0A1324"
COLOR_EDITOR_ALT = "#0E1930"
COLOR_SELECTION = "#234B88"

SIDEBAR_WIDTH = 210
TOPBAR_HEIGHT = 58
FOOTER_HEIGHT = 28


def create_text_area(parent: tk.Frame, *, background: str) -> tk.Text:
    holder = tk.Frame(parent, bg=background)
    holder.pack(fill="both", expand=True)

    text = tk.Text(
        holder,
        wrap="none",
        font=("Consolas", 10),
        undo=True,
        maxundo=-1,
        autoseparators=True,
        bg=background,
        fg=COLOR_TEXT,
        insertbackground=COLOR_TEXT,
        selectbackground=COLOR_SELECTION,
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

    return text


class TexpadFrame(tk.Frame):
    SEARCH_TAG = "search_match"
    SEARCH_CURRENT_TAG = "search_current"

    def __init__(self, parent) -> None:
        super().__init__(parent, bg=COLOR_APP_BG)

        self.cfg = load_config()
        self.size_cfg = load_size_config()

        self.current_file: Path | None = None
        self._rows = []
        self._last_orders = []
        self._last_json = ""
        self._search_matches: list[str] = []
        self._search_current_idx = -1
        self._search_dirty = True

        self.status_var = tk.StringVar(value="Pronto.")
        self.current_file_var = tk.StringVar(value="Arquivo atual: (nova lista)")
        self.page_title_var = tk.StringVar(value="Home")
        self.clock_var = tk.StringVar(value="")
        self.top_action_var = tk.StringVar(value="Abrir Lista")
        self.size_summary_var = tk.StringVar(value="")

        self.find_var = tk.StringVar(value="")
        self.replace_var = tk.StringVar(value="")
        self.search_match_case_var = tk.BooleanVar(value=False)

        self.editor_separator_var = tk.StringVar(value=self.cfg.get("default_input_separator", ","))
        self.editor_case_label_var = tk.StringVar(
            value=CASE_VALUE_TO_LABEL.get(self.cfg.get("default_case_mode", "original"), "Original")
        )

        self.show_json_tab_var = tk.BooleanVar(value=bool(self.cfg.get("show_json_tab", True)))
        self.show_generate_json_button_var = tk.BooleanVar(value=bool(self.cfg.get("show_generate_json_button", True)))
        self.show_copy_json_button_var = tk.BooleanVar(value=bool(self.cfg.get("show_copy_json_button", True)))

        self.use_default_output_dir_var = tk.BooleanVar(value=bool(self.cfg.get("use_default_output_dir", True)))
        self.output_dir_var = tk.StringVar(value=str(self.cfg.get("output_dir", "")))

        self.use_default_list_name_var = tk.BooleanVar(value=bool(self.cfg.get("use_default_list_name", True)))
        self.default_list_name_var = tk.StringVar(value=str(self.cfg.get("default_list_name", "lista")))

        self.default_case_label_var = tk.StringVar(
            value=CASE_VALUE_TO_LABEL.get(self.cfg.get("default_case_mode", "original"), "Original")
        )
        self.default_input_separator_var = tk.StringVar(value=self.cfg.get("default_input_separator", ","))

        # Dashboard vars
        self.home_input_lines_var = tk.StringVar(value="0")
        self.home_output_lines_var = tk.StringVar(value="0")
        self.home_sizes_total_var = tk.StringVar(value="0")
        self.home_json_status_var = tk.StringVar(value="Desativado")
        self.home_current_file_var = tk.StringVar(value="Nenhum arquivo aberto")
        self.home_mode_var = tk.StringVar(value="Original")
        self.home_separator_var = tk.StringVar(value=",")
        self.home_output_policy_var = tk.StringVar(value="Pasta padrão")
        self.home_alert_var = tk.StringVar(value="Tudo pronto para começar.")
        self.home_recent_a_var = tk.StringVar(value="Arquivo atual")
        self.home_recent_b_var = tk.StringVar(value="Saída")
        self.home_recent_c_var = tk.StringVar(value="JSON")
        self.home_recent_d_var = tk.StringVar(value="Backups")

        self.size_group_vars: dict[str, dict[str, tk.StringVar]] = {}
        self._init_size_group_vars()

        self.nav_buttons: dict[str, tk.Button] = {}
        self.screens: dict[str, tk.Frame] = {}
        self.current_screen_key = "home"

        self.ent_find: ttk.Entry | None = None
        self.ent_replace: ttk.Entry | None = None

        self._configure_root(parent)
        self._apply_ttk_theme()
        self._build_shell()
        self._build_home_screen()
        self._build_editor_screen()
        self._build_settings_screen()
        self._configure_tags()
        self._bind_events()
        self._load_last_opened_file_label()
        self._apply_runtime_preferences()
        self._refresh_size_summary()
        self._refresh_home_dashboard()
        self.show_screen("home")
        self._tick_clock()

        self.txt_in.insert(
            "1.0",
            "G,JÃO,10\n"
            "JOÃO,5,G,M\n"
            "MANEL,PP\n"
            "JUACA,JUSÉ,PP\n",
        )
        self._refresh_home_dashboard()

    # ------------------------------------------------------------------
    # Root / theme
    # ------------------------------------------------------------------
    def _configure_root(self, parent) -> None:
        if isinstance(parent, (tk.Tk, tk.Toplevel)):
            parent.title(APP_NAME)
            parent.geometry("1540x920")
            parent.minsize(1320, 780)
            parent.configure(bg=COLOR_APP_BG)
            parent.rowconfigure(0, weight=1)
            parent.columnconfigure(0, weight=1)

        self.grid(row=0, column=0, sticky="nsew")

    def _apply_ttk_theme(self) -> None:
        style = ttk.Style()
        try:
            style.theme_use("clam")
        except tk.TclError:
            pass

        style.configure("TFrame", background=COLOR_APP_BG)
        style.configure("Card.TFrame", background=COLOR_PANEL)
        style.configure("Inner.TFrame", background=COLOR_PANEL_ALT)

        style.configure(
            "Card.TLabelframe",
            background=COLOR_PANEL,
            bordercolor=COLOR_BORDER,
            relief="solid",
            borderwidth=1,
        )
        style.configure(
            "Card.TLabelframe.Label",
            background=COLOR_PANEL,
            foreground=COLOR_TEXT_MUTED,
            font=("Segoe UI", 9, "bold"),
        )

        style.configure(
            "TButton",
            background=COLOR_PANEL_ALT,
            foreground=COLOR_TEXT,
            bordercolor=COLOR_BORDER,
            focuscolor=COLOR_PANEL_ALT,
            padding=(10, 8),
            relief="flat",
        )
        style.map(
            "TButton",
            background=[("active", COLOR_PANEL_HOVER)],
            foreground=[("active", COLOR_TEXT)],
        )

        style.configure(
            "Accent.TButton",
            background=COLOR_PRIMARY,
            foreground="#FFFFFF",
            bordercolor=COLOR_PRIMARY,
            relief="flat",
            padding=(12, 8),
        )
        style.map(
            "Accent.TButton",
            background=[("active", COLOR_PRIMARY_HOVER)],
            foreground=[("active", "#FFFFFF")],
        )

        style.configure(
            "TEntry",
            fieldbackground=COLOR_EDITOR_ALT,
            foreground=COLOR_TEXT,
            insertcolor=COLOR_TEXT,
            bordercolor=COLOR_BORDER,
            lightcolor=COLOR_BORDER,
            darkcolor=COLOR_BORDER,
        )

        style.configure(
            "TCombobox",
            fieldbackground=COLOR_EDITOR_ALT,
            foreground=COLOR_TEXT,
            arrowcolor=COLOR_TEXT,
            bordercolor=COLOR_BORDER,
            lightcolor=COLOR_BORDER,
            darkcolor=COLOR_BORDER,
        )
        style.map(
            "TCombobox",
            fieldbackground=[("readonly", COLOR_EDITOR_ALT)],
            selectbackground=[("readonly", COLOR_EDITOR_ALT)],
            selectforeground=[("readonly", COLOR_TEXT)],
            foreground=[("readonly", COLOR_TEXT)],
        )

        style.configure("TCheckbutton", background=COLOR_PANEL, foreground=COLOR_TEXT)
        style.map("TCheckbutton", background=[("active", COLOR_PANEL)], foreground=[("active", COLOR_TEXT)])

        style.configure("TRadiobutton", background=COLOR_PANEL, foreground=COLOR_TEXT)
        style.map("TRadiobutton", background=[("active", COLOR_PANEL)], foreground=[("active", COLOR_TEXT)])

        style.configure(
            "TNotebook",
            background=COLOR_APP_BG,
            borderwidth=0,
            tabmargins=(0, 0, 0, 0),
        )
        style.configure(
            "TNotebook.Tab",
            background=COLOR_PANEL_ALT,
            foreground=COLOR_TEXT_MUTED,
            borderwidth=0,
            padding=(12, 8),
        )
        style.map(
            "TNotebook.Tab",
            background=[("selected", COLOR_PANEL), ("active", COLOR_PANEL_HOVER)],
            foreground=[("selected", COLOR_TEXT), ("active", COLOR_TEXT)],
        )

        style.configure(
            "Treeview",
            background=COLOR_PANEL,
            fieldbackground=COLOR_PANEL,
            foreground=COLOR_TEXT,
            rowheight=30,
            bordercolor=COLOR_BORDER,
            lightcolor=COLOR_BORDER,
            darkcolor=COLOR_BORDER,
        )
        style.configure(
            "Treeview.Heading",
            background=COLOR_TOPBAR,
            foreground=COLOR_TEXT_MUTED,
            relief="flat",
            borderwidth=0,
            font=("Segoe UI", 9, "bold"),
        )
        style.map(
            "Treeview",
            background=[("selected", COLOR_SELECTION)],
            foreground=[("selected", "#FFFFFF")],
        )
        style.map(
            "Treeview.Heading",
            background=[("active", COLOR_TOPBAR)],
            foreground=[("active", COLOR_TEXT)],
        )

    # ------------------------------------------------------------------
    # Shell
    # ------------------------------------------------------------------
    def _build_shell(self) -> None:
        self.columnconfigure(1, weight=1)
        self.rowconfigure(0, weight=1)

        self.sidebar = tk.Frame(
            self,
            bg=COLOR_SIDEBAR,
            width=SIDEBAR_WIDTH,
            highlightbackground=COLOR_SIDEBAR_BORDER,
            highlightthickness=1,
            bd=0,
        )
        self.sidebar.grid(row=0, column=0, sticky="nsw")
        self.sidebar.grid_propagate(False)
        self.sidebar.rowconfigure(2, weight=1)

        self.shell = tk.Frame(self, bg=COLOR_APP_BG)
        self.shell.grid(row=0, column=1, sticky="nsew")
        self.shell.rowconfigure(1, weight=1)
        self.shell.columnconfigure(0, weight=1)

        self._build_sidebar()
        self._build_topbar()
        self._build_content_shell()
        self._build_footer()

    def _build_sidebar(self) -> None:
        brand = tk.Frame(self.sidebar, bg=COLOR_SIDEBAR, height=58)
        brand.grid(row=0, column=0, sticky="ew")
        brand.grid_propagate(False)

        tk.Label(
            brand,
            text="✎  Texpad",
            bg=COLOR_SIDEBAR,
            fg=COLOR_TEXT,
            font=("Segoe UI", 14, "bold"),
            anchor="w",
        ).pack(fill="both", padx=16, pady=14)

        divider = tk.Frame(self.sidebar, bg=COLOR_SIDEBAR_BORDER, height=1)
        divider.grid(row=1, column=0, sticky="ew")

        nav = tk.Frame(self.sidebar, bg=COLOR_SIDEBAR)
        nav.grid(row=2, column=0, sticky="nsew", padx=10, pady=12)

        items = [
            ("home", "Home", "⌂"),
            ("editor", "Editor", "✎"),
            ("settings", "Configurações", "⚙"),
        ]

        for idx, (key, label, icon) in enumerate(items):
            btn = tk.Button(
                nav,
                text=f"{icon}  {label}",
                command=lambda k=key: self.show_screen(k),
                bg=COLOR_SIDEBAR,
                fg=COLOR_TEXT_MUTED,
                activebackground=COLOR_SIDEBAR_HOVER,
                activeforeground=COLOR_TEXT,
                relief="flat",
                bd=0,
                highlightthickness=0,
                anchor="w",
                cursor="hand2",
                font=("Segoe UI", 10),
                padx=12,
                pady=10,
            )
            btn.grid(row=idx, column=0, sticky="ew", pady=(0, 6))
            self.nav_buttons[key] = btn

        nav.columnconfigure(0, weight=1)

        footer_wrap = tk.Frame(
            self.sidebar,
            bg=COLOR_SIDEBAR,
            highlightbackground=COLOR_SIDEBAR_BORDER,
            highlightthickness=1,
            bd=0,
        )
        footer_wrap.grid(row=3, column=0, sticky="sew")

        footer = tk.Frame(footer_wrap, bg=COLOR_SIDEBAR)
        footer.pack(fill="x", padx=12, pady=12)

        row_status = tk.Frame(footer, bg=COLOR_SIDEBAR)
        row_status.pack(fill="x")

        tk.Label(
            row_status,
            text="●",
            bg=COLOR_SIDEBAR,
            fg=COLOR_SUCCESS,
            font=("Segoe UI", 9, "bold"),
        ).pack(side="left")

        tk.Label(
            row_status,
            text="Sistema ativo",
            bg=COLOR_SIDEBAR,
            fg=COLOR_TEXT_MUTED,
            font=("Segoe UI", 9),
        ).pack(side="left", padx=(6, 0))

        badge = tk.Frame(
            footer,
            bg=COLOR_PANEL_ALT,
            highlightbackground=COLOR_BORDER,
            highlightthickness=1,
            bd=0,
        )
        badge.pack(fill="x", pady=(10, 0))

        tk.Label(
            badge,
            text="Modo local",
            bg=COLOR_PANEL_ALT,
            fg=COLOR_TEXT_MUTED,
            font=("Segoe UI", 9),
            anchor="w",
        ).pack(fill="x", padx=10, pady=6)

    def _build_topbar(self) -> None:
        self.topbar = tk.Frame(
            self.shell,
            bg=COLOR_TOPBAR,
            height=TOPBAR_HEIGHT,
            highlightbackground=COLOR_SIDEBAR_BORDER,
            highlightthickness=1,
            bd=0,
        )
        self.topbar.grid(row=0, column=0, sticky="ew")
        self.topbar.grid_propagate(False)
        self.topbar.columnconfigure(1, weight=1)

        left = tk.Frame(self.topbar, bg=COLOR_TOPBAR)
        left.grid(row=0, column=0, sticky="w", padx=18)

        tk.Label(
            left,
            textvariable=self.page_title_var,
            bg=COLOR_TOPBAR,
            fg=COLOR_TEXT,
            font=("Segoe UI", 13, "bold"),
        ).pack(side="left", pady=14)

        right = tk.Frame(self.topbar, bg=COLOR_TOPBAR)
        right.grid(row=0, column=1, sticky="e", padx=16)

        tk.Label(
            right,
            textvariable=self.clock_var,
            bg=COLOR_TOPBAR,
            fg=COLOR_TEXT_MUTED,
            font=("Segoe UI", 9),
        ).pack(side="left", padx=(0, 14), pady=14)

        self.top_action_button = tk.Button(
            right,
            textvariable=self.top_action_var,
            command=self._run_primary_action,
            bg=COLOR_PRIMARY,
            fg="#FFFFFF",
            activebackground=COLOR_PRIMARY_HOVER,
            activeforeground="#FFFFFF",
            relief="flat",
            bd=0,
            cursor="hand2",
            font=("Segoe UI", 10, "bold"),
            padx=14,
            pady=7,
        )
        self.top_action_button.pack(side="left", pady=10)

    def _build_content_shell(self) -> None:
        body = tk.Frame(self.shell, bg=COLOR_APP_BG)
        body.grid(row=1, column=0, sticky="nsew")
        body.rowconfigure(0, weight=1)
        body.columnconfigure(0, weight=1)

        self.content_host = tk.Frame(body, bg=COLOR_APP_BG)
        self.content_host.grid(row=0, column=0, sticky="nsew", padx=12, pady=12)
        self.content_host.rowconfigure(0, weight=1)
        self.content_host.columnconfigure(0, weight=1)

    def _build_footer(self) -> None:
        footer = tk.Frame(
            self.shell,
            bg=COLOR_TOPBAR,
            height=FOOTER_HEIGHT,
            highlightbackground=COLOR_SIDEBAR_BORDER,
            highlightthickness=1,
            bd=0,
        )
        footer.grid(row=2, column=0, sticky="ew")
        footer.grid_propagate(False)

        tk.Label(
            footer,
            textvariable=self.status_var,
            bg=COLOR_TOPBAR,
            fg=COLOR_TEXT_MUTED,
            font=("Segoe UI", 9),
            anchor="w",
        ).pack(fill="both", padx=14)

    def _card(self, parent: tk.Widget) -> tk.Frame:
        return tk.Frame(
            parent,
            bg=COLOR_PANEL,
            highlightbackground=COLOR_BORDER,
            highlightthickness=1,
            bd=0,
        )

    # ------------------------------------------------------------------
    # Home
    # ------------------------------------------------------------------
    def _build_home_screen(self) -> None:
        screen = tk.Frame(self.content_host, bg=COLOR_APP_BG)
        screen.grid(row=0, column=0, sticky="nsew")
        screen.rowconfigure(3, weight=1)
        screen.columnconfigure(0, weight=1)
        screen.columnconfigure(1, weight=1)

        metrics = tk.Frame(screen, bg=COLOR_APP_BG)
        metrics.grid(row=0, column=0, columnspan=2, sticky="ew", pady=(0, 12))
        for i in range(4):
            metrics.columnconfigure(i, weight=1)

        self._build_metric_card(
            metrics,
            row=0,
            column=0,
            title="LINHAS NA ENTRADA",
            value_var=self.home_input_lines_var,
            subtitle="linhas preenchidas",
            accent=COLOR_PRIMARY,
        )
        self._build_metric_card(
            metrics,
            row=0,
            column=1,
            title="LINHAS NA SAÍDA",
            value_var=self.home_output_lines_var,
            subtitle="resultado organizado",
            accent=COLOR_SUCCESS,
        )
        self._build_metric_card(
            metrics,
            row=0,
            column=2,
            title="TAMANHOS VÁLIDOS",
            value_var=self.home_sizes_total_var,
            subtitle="cadastro atual",
            accent=COLOR_WARNING,
        )
        self._build_metric_card(
            metrics,
            row=0,
            column=3,
            title="JSON",
            value_var=self.home_json_status_var,
            subtitle="visualização e geração",
            accent=COLOR_DANGER,
        )

        left = tk.Frame(screen, bg=COLOR_APP_BG)
        left.grid(row=1, column=0, sticky="nsew", padx=(0, 8))
        left.columnconfigure(0, weight=1)

        right = tk.Frame(screen, bg=COLOR_APP_BG)
        right.grid(row=1, column=1, rowspan=3, sticky="nsew", padx=(8, 0))
        right.columnconfigure(0, weight=1)
        right.rowconfigure(1, weight=1)

        actions_card = self._card(left)
        actions_card.grid(row=0, column=0, sticky="ew", pady=(0, 12))

        tk.Label(
            actions_card,
            text="Ações rápidas",
            bg=COLOR_PANEL,
            fg=COLOR_TEXT,
            font=("Segoe UI", 12, "bold"),
            anchor="w",
        ).pack(fill="x", padx=18, pady=(16, 10))

        actions_wrap = tk.Frame(actions_card, bg=COLOR_PANEL)
        actions_wrap.pack(fill="x", padx=18, pady=(0, 16))
        for i in range(4):
            actions_wrap.columnconfigure(i, weight=1)

        self._build_quick_button(actions_wrap, 0, "Abrir Lista", self.open_input_file)
        self._build_quick_button(actions_wrap, 1, "Editor", lambda: self.show_screen("editor"))
        self._build_quick_button(actions_wrap, 2, "Processar", self.process_and_preview)
        self._build_quick_button(actions_wrap, 3, "Configurações", lambda: self.show_screen("settings"))

        alert_card = self._card(left)
        alert_card.grid(row=1, column=0, sticky="ew", pady=(0, 12))

        tk.Label(
            alert_card,
            text="Alertas",
            bg=COLOR_PANEL,
            fg=COLOR_TEXT,
            font=("Segoe UI", 12, "bold"),
            anchor="w",
        ).pack(fill="x", padx=18, pady=(16, 10))

        alert_box = tk.Frame(
            alert_card,
            bg="#5A2A00",
            highlightbackground="#F0B400",
            highlightthickness=1,
            bd=0,
        )
        alert_box.pack(fill="x", padx=18, pady=(0, 16))

        tk.Label(
            alert_box,
            textvariable=self.home_alert_var,
            bg="#5A2A00",
            fg="#FFDFA3",
            justify="left",
            wraplength=520,
            anchor="w",
            font=("Segoe UI", 10, "bold"),
        ).pack(fill="x", padx=12, pady=12)

        pref_card = self._card(left)
        pref_card.grid(row=2, column=0, sticky="ew")

        tk.Label(
            pref_card,
            text="Contexto atual",
            bg=COLOR_PANEL,
            fg=COLOR_TEXT,
            font=("Segoe UI", 12, "bold"),
            anchor="w",
        ).pack(fill="x", padx=18, pady=(16, 10))

        self._build_info_row(pref_card, "Arquivo", self.home_current_file_var)
        self._build_info_row(pref_card, "Modo de texto", self.home_mode_var)
        self._build_info_row(pref_card, "Separador", self.home_separator_var)
        self._build_info_row(pref_card, "Salvar saída", self.home_output_policy_var)

        recent_card = self._card(right)
        recent_card.grid(row=0, column=0, sticky="ew")

        tk.Label(
            recent_card,
            text="Resumo recente",
            bg=COLOR_PANEL,
            fg=COLOR_TEXT,
            font=("Segoe UI", 12, "bold"),
            anchor="w",
        ).pack(fill="x", padx=18, pady=(16, 10))

        self.home_recent_tree = ttk.Treeview(
            recent_card,
            columns=("item", "valor"),
            show="headings",
            height=5,
        )
        self.home_recent_tree.heading("item", text="ITEM")
        self.home_recent_tree.heading("valor", text="VALOR")
        self.home_recent_tree.column("item", width=190, anchor="w")
        self.home_recent_tree.column("valor", width=420, anchor="w")
        self.home_recent_tree.pack(fill="x", padx=18, pady=(0, 18))

        hero_card = self._card(right)
        hero_card.grid(row=1, column=0, sticky="nsew", pady=(12, 0))

        inner = tk.Frame(hero_card, bg=COLOR_PANEL)
        inner.pack(fill="both", expand=True, padx=22, pady=22)

        tk.Label(
            inner,
            text="Texpad agora segue a linguagem visual do shell do Nexor.",
            bg=COLOR_PANEL,
            fg=COLOR_TEXT,
            font=("Segoe UI", 15, "bold"),
            justify="left",
            anchor="w",
            wraplength=640,
        ).pack(anchor="w")

        tk.Label(
            inner,
            text=(
                "A diferença é que aqui o centro continua sendo o editor.\n\n"
                "Então a Home resume o estado atual, enquanto a tela Editor concentra "
                "entrada, localizar/substituir, processamento, saída organizada e JSON."
            ),
            bg=COLOR_PANEL,
            fg=COLOR_TEXT_MUTED,
            font=("Segoe UI", 10),
            justify="left",
            anchor="w",
            wraplength=680,
        ).pack(anchor="w", pady=(12, 0))

        tk.Button(
            inner,
            text="Ir para o Editor",
            command=lambda: self.show_screen("editor"),
            bg=COLOR_PRIMARY,
            fg="#FFFFFF",
            activebackground=COLOR_PRIMARY_HOVER,
            activeforeground="#FFFFFF",
            relief="flat",
            bd=0,
            cursor="hand2",
            font=("Segoe UI", 10, "bold"),
            padx=16,
            pady=8,
        ).pack(anchor="w", pady=(18, 0))

        self.screens["home"] = screen

    def _build_metric_card(
        self,
        parent: tk.Widget,
        *,
        row: int,
        column: int,
        title: str,
        value_var: tk.StringVar,
        subtitle: str,
        accent: str,
    ) -> None:
        card = self._card(parent)
        card.grid(row=row, column=column, sticky="ew", padx=(0 if column == 0 else 8, 0), pady=0)

        tk.Label(
            card,
            text=title,
            bg=COLOR_PANEL,
            fg=COLOR_TEXT_MUTED,
            font=("Segoe UI", 9, "bold"),
            anchor="w",
        ).pack(fill="x", padx=18, pady=(16, 6))

        tk.Label(
            card,
            textvariable=value_var,
            bg=COLOR_PANEL,
            fg=accent,
            font=("Segoe UI", 22, "bold"),
            anchor="w",
        ).pack(fill="x", padx=18)

        tk.Label(
            card,
            text=subtitle,
            bg=COLOR_PANEL,
            fg=COLOR_TEXT_MUTED,
            font=("Segoe UI", 10),
            anchor="w",
        ).pack(fill="x", padx=18, pady=(4, 16))

    def _build_quick_button(self, parent: tk.Widget, column: int, text: str, command) -> None:
        btn = tk.Button(
            parent,
            text=text,
            command=command,
            bg=COLOR_PANEL_ALT,
            fg=COLOR_TEXT,
            activebackground=COLOR_PANEL_HOVER,
            activeforeground=COLOR_TEXT,
            relief="flat",
            bd=0,
            cursor="hand2",
            font=("Segoe UI", 10),
            padx=12,
            pady=18,
        )
        btn.grid(row=0, column=column, sticky="ew", padx=(0 if column == 0 else 10, 0))

    def _build_info_row(self, parent: tk.Widget, label: str, value_var: tk.StringVar) -> None:
        row = tk.Frame(parent, bg=COLOR_PANEL)
        row.pack(fill="x", padx=18, pady=4)

        tk.Label(
            row,
            text=label,
            bg=COLOR_PANEL,
            fg=COLOR_TEXT_MUTED,
            font=("Segoe UI", 10),
            width=14,
            anchor="w",
        ).pack(side="left")

        tk.Label(
            row,
            textvariable=value_var,
            bg=COLOR_PANEL,
            fg=COLOR_TEXT,
            font=("Segoe UI", 10),
            anchor="w",
        ).pack(side="left", fill="x", expand=True)

    # ------------------------------------------------------------------
    # Editor
    # ------------------------------------------------------------------
    def _build_editor_screen(self) -> None:
        screen = tk.Frame(self.content_host, bg=COLOR_APP_BG)
        screen.grid(row=0, column=0, sticky="nsew")
        screen.rowconfigure(3, weight=1)
        screen.columnconfigure(0, weight=1)

        row_files = self._card(screen)
        row_files.grid(row=0, column=0, sticky="ew", pady=(0, 10))

        files_inner = tk.Frame(row_files, bg=COLOR_PANEL)
        files_inner.pack(fill="x", padx=16, pady=12)

        tk.Button(
            files_inner,
            text="Abrir Lista",
            command=self.open_input_file,
            bg=COLOR_PANEL_ALT,
            fg=COLOR_TEXT,
            activebackground=COLOR_PANEL_HOVER,
            activeforeground=COLOR_TEXT,
            relief="flat",
            bd=0,
            cursor="hand2",
            font=("Segoe UI", 10),
            padx=12,
            pady=7,
        ).pack(side="left")

        tk.Button(
            files_inner,
            text="Salvar Entrada",
            command=self.save_input_file,
            bg=COLOR_PANEL_ALT,
            fg=COLOR_TEXT,
            activebackground=COLOR_PANEL_HOVER,
            activeforeground=COLOR_TEXT,
            relief="flat",
            bd=0,
            cursor="hand2",
            font=("Segoe UI", 10),
            padx=12,
            pady=7,
        ).pack(side="left", padx=(6, 0))

        tk.Button(
            files_inner,
            text="Salvar Entrada Como",
            command=self.save_input_as_file,
            bg=COLOR_PANEL_ALT,
            fg=COLOR_TEXT,
            activebackground=COLOR_PANEL_HOVER,
            activeforeground=COLOR_TEXT,
            relief="flat",
            bd=0,
            cursor="hand2",
            font=("Segoe UI", 10),
            padx=12,
            pady=7,
        ).pack(side="left", padx=(6, 0))

        tk.Button(
            files_inner,
            text="Desfazer",
            command=self.undo_last_change,
            bg=COLOR_PANEL_ALT,
            fg=COLOR_TEXT,
            activebackground=COLOR_PANEL_HOVER,
            activeforeground=COLOR_TEXT,
            relief="flat",
            bd=0,
            cursor="hand2",
            font=("Segoe UI", 10),
            padx=12,
            pady=7,
        ).pack(side="left", padx=(14, 0))

        tk.Button(
            files_inner,
            text="Abrir pasta de backups",
            command=self.open_backups_folder,
            bg=COLOR_PANEL_ALT,
            fg=COLOR_TEXT,
            activebackground=COLOR_PANEL_HOVER,
            activeforeground=COLOR_TEXT,
            relief="flat",
            bd=0,
            cursor="hand2",
            font=("Segoe UI", 10),
            padx=12,
            pady=7,
        ).pack(side="left", padx=(6, 0))

        tk.Label(
            files_inner,
            textvariable=self.current_file_var,
            bg=COLOR_PANEL,
            fg=COLOR_TEXT_MUTED,
            font=("Segoe UI", 9),
            anchor="e",
        ).pack(side="right")

        prep_box = ttk.LabelFrame(screen, text="Preparação da lista", style="Card.TLabelframe")
        prep_box.grid(row=1, column=0, sticky="ew", pady=(0, 10))

        prep_inner = tk.Frame(prep_box, bg=COLOR_PANEL)
        prep_inner.pack(fill="x", padx=12, pady=12)

        ttk.Label(prep_inner, text="Separador da entrada").grid(row=0, column=0, sticky="w", padx=(0, 6))
        ttk.Entry(prep_inner, textvariable=self.editor_separator_var, width=8).grid(row=0, column=1, sticky="w")
        ttk.Label(prep_inner, text='Use "\\t" para tab').grid(row=0, column=2, sticky="w", padx=(8, 18))

        ttk.Button(prep_inner, text="Padrão (,)", command=self.set_default_separator).grid(
            row=0, column=3, sticky="w", padx=(0, 8)
        )
        ttk.Button(prep_inner, text="Remover espaços desnecessários", command=self.clean_unnecessary_spaces).grid(
            row=0, column=4, sticky="w", padx=(0, 18)
        )

        ttk.Label(prep_inner, text="Maiúsculas / minúsculas").grid(row=0, column=5, sticky="w", padx=(0, 6))
        ttk.Combobox(
            prep_inner,
            textvariable=self.editor_case_label_var,
            values=list(CASE_LABEL_TO_VALUE.keys()),
            width=18,
            state="readonly",
        ).grid(row=0, column=6, sticky="w")

        search_box = ttk.LabelFrame(screen, text="Localizar / Localizar e substituir", style="Card.TLabelframe")
        search_box.grid(row=2, column=0, sticky="ew", pady=(0, 10))

        search_inner = tk.Frame(search_box, bg=COLOR_PANEL)
        search_inner.pack(fill="x", padx=12, pady=12)

        ttk.Label(search_inner, text="Localizar").grid(row=0, column=0, sticky="w", padx=(0, 6))
        self.ent_find = ttk.Entry(search_inner, textvariable=self.find_var, width=28)
        self.ent_find.grid(row=0, column=1, sticky="w")

        ttk.Label(search_inner, text="Substituir por").grid(row=0, column=2, sticky="w", padx=(16, 6))
        self.ent_replace = ttk.Entry(search_inner, textvariable=self.replace_var, width=28)
        self.ent_replace.grid(row=0, column=3, sticky="w")

        ttk.Checkbutton(
            search_inner,
            text="Diferenciar maiúsculas/minúsculas",
            variable=self.search_match_case_var,
        ).grid(row=0, column=4, sticky="w", padx=(16, 10))

        btns_search = tk.Frame(search_inner, bg=COLOR_PANEL)
        btns_search.grid(row=0, column=5, sticky="e")
        ttk.Button(btns_search, text="Localizar", command=self.find_next_from_cursor).pack(side="left")
        ttk.Button(btns_search, text="Anterior", command=self.find_previous).pack(side="left", padx=(6, 0))
        ttk.Button(btns_search, text="Próximo", command=self.find_next).pack(side="left", padx=(6, 0))
        ttk.Button(btns_search, text="Substituir", command=self.replace_current).pack(side="left", padx=(12, 0))
        ttk.Button(btns_search, text="Substituir tudo", command=self.replace_all).pack(side="left", padx=(6, 0))
        ttk.Button(btns_search, text="Limpar destaque", command=self.clear_search_highlight).pack(side="left", padx=(12, 0))

        body = tk.Frame(screen, bg=COLOR_APP_BG)
        body.grid(row=3, column=0, sticky="nsew")
        body.rowconfigure(0, weight=1)
        body.columnconfigure(0, weight=1)
        body.columnconfigure(1, weight=1)

        left = self._card(body)
        left.grid(row=0, column=0, sticky="nsew", padx=(0, 8))
        left.rowconfigure(1, weight=1)
        left.columnconfigure(0, weight=1)

        right = self._card(body)
        right.grid(row=0, column=1, sticky="nsew", padx=(8, 0))
        right.rowconfigure(1, weight=1)
        right.columnconfigure(0, weight=1)

        tk.Label(
            left,
            text="Entrada / edição",
            bg=COLOR_PANEL,
            fg=COLOR_TEXT,
            font=("Segoe UI", 11, "bold"),
            anchor="w",
        ).grid(row=0, column=0, sticky="ew", padx=14, pady=(14, 8))
        self.txt_in = create_text_area(left, background=COLOR_EDITOR_BG)
        self.txt_in.grid(row=1, column=0, sticky="nsew", padx=14, pady=(0, 14))

        tk.Label(
            right,
            text="Saída",
            bg=COLOR_PANEL,
            fg=COLOR_TEXT,
            font=("Segoe UI", 11, "bold"),
            anchor="w",
        ).grid(row=0, column=0, sticky="ew", padx=14, pady=(14, 8))

        self.outputs_nb = ttk.Notebook(right)
        self.outputs_nb.grid(row=1, column=0, sticky="nsew", padx=14, pady=(0, 14))

        self.tab_list = tk.Frame(self.outputs_nb, bg=COLOR_PANEL)
        self.tab_json = tk.Frame(self.outputs_nb, bg=COLOR_PANEL)

        self.outputs_nb.add(self.tab_list, text="Lista organizada")
        self.outputs_nb.add(self.tab_json, text="Prévia JSON")

        self.txt_out = create_text_area(self.tab_list, background=COLOR_EDITOR_ALT)
        self.txt_json = create_text_area(self.tab_json, background=COLOR_EDITOR_ALT)
        self._set_text_readonly(self.txt_json, True)

        footer = tk.Frame(screen, bg=COLOR_APP_BG)
        footer.grid(row=4, column=0, sticky="ew", pady=(10, 0))

        left_actions = tk.Frame(footer, bg=COLOR_APP_BG)
        left_actions.pack(side="left")

        right_actions = tk.Frame(footer, bg=COLOR_APP_BG)
        right_actions.pack(side="right")

        ttk.Button(left_actions, text="Limpar tudo", command=self.clear_all).pack(side="left")
        ttk.Button(left_actions, text="Copiar saída", command=self.copy_output).pack(side="left", padx=(6, 0))
        ttk.Button(left_actions, text="Salvar saída", command=self.save_output).pack(side="left", padx=(6, 0))

        self.btn_copy_json = ttk.Button(left_actions, text="Copiar JSON", command=self.copy_json)
        self.btn_copy_json.pack(side="left", padx=(6, 0))

        self.btn_generate_json = ttk.Button(right_actions, text="Gerar JSON", command=self.generate_json)
        self.btn_generate_json.pack(side="right")
        ttk.Button(right_actions, text="Processar", style="Accent.TButton", command=self.process_and_preview).pack(
            side="right", padx=(0, 6)
        )

        self.screens["editor"] = screen

    # ------------------------------------------------------------------
    # Settings
    # ------------------------------------------------------------------
    def _build_settings_screen(self) -> None:
        screen = tk.Frame(self.content_host, bg=COLOR_APP_BG)
        screen.grid(row=0, column=0, sticky="nsew")
        screen.rowconfigure(0, weight=1)
        screen.columnconfigure(0, weight=1)

        wrap = tk.Frame(screen, bg=COLOR_APP_BG)
        wrap.grid(row=0, column=0, sticky="nsew")
        wrap.columnconfigure(0, weight=1)

        box_json = ttk.LabelFrame(wrap, text="JSON", style="Card.TLabelframe")
        box_json.pack(fill="x", pady=(0, 10))

        inner_json = tk.Frame(box_json, bg=COLOR_PANEL)
        inner_json.pack(fill="x", padx=12, pady=12)

        ttk.Checkbutton(inner_json, text="Mostrar aba de JSON", variable=self.show_json_tab_var).pack(
            anchor="w", pady=(0, 4)
        )
        ttk.Checkbutton(inner_json, text="Mostrar botão Gerar JSON", variable=self.show_generate_json_button_var).pack(
            anchor="w", pady=4
        )
        ttk.Checkbutton(inner_json, text="Mostrar botão Copiar JSON", variable=self.show_copy_json_button_var).pack(
            anchor="w", pady=(4, 0)
        )

        box_text = ttk.LabelFrame(wrap, text="Padrões de texto", style="Card.TLabelframe")
        box_text.pack(fill="x", pady=(0, 10))

        inner_text = tk.Frame(box_text, bg=COLOR_PANEL)
        inner_text.pack(fill="x", padx=12, pady=12)

        ttk.Label(inner_text, text="Modo padrão de maiúsculas/minúsculas").grid(row=0, column=0, sticky="w", padx=(0, 6))
        ttk.Combobox(
            inner_text,
            textvariable=self.default_case_label_var,
            values=list(CASE_LABEL_TO_VALUE.keys()),
            width=18,
            state="readonly",
        ).grid(row=0, column=1, sticky="w")

        ttk.Label(inner_text, text="Separador padrão da entrada").grid(row=1, column=0, sticky="w", padx=(0, 6), pady=(10, 0))
        ttk.Entry(inner_text, textvariable=self.default_input_separator_var, width=8).grid(
            row=1, column=1, sticky="w", pady=(10, 0)
        )
        ttk.Label(inner_text, text='Use "\\t" para tab').grid(row=1, column=2, sticky="w", padx=(8, 0), pady=(10, 0))

        box_output = ttk.LabelFrame(wrap, text="Saída", style="Card.TLabelframe")
        box_output.pack(fill="x", pady=(0, 10))

        inner_output = tk.Frame(box_output, bg=COLOR_PANEL)
        inner_output.pack(fill="x", padx=12, pady=12)
        inner_output.columnconfigure(1, weight=1)

        ttk.Checkbutton(
            inner_output,
            text="Usar pasta padrão para salvar a saída",
            variable=self.use_default_output_dir_var,
            command=self.update_settings_field_states,
        ).grid(row=0, column=0, columnspan=3, sticky="w", pady=(0, 8))

        ttk.Label(inner_output, text="Pasta padrão").grid(row=1, column=0, sticky="w", padx=(0, 6))
        self.ent_output_dir = ttk.Entry(inner_output, textvariable=self.output_dir_var, width=60)
        self.ent_output_dir.grid(row=1, column=1, sticky="ew")
        self.btn_pick_output_dir = ttk.Button(inner_output, text="Escolher...", command=self.pick_default_output_folder)
        self.btn_pick_output_dir.grid(row=1, column=2, sticky="w", padx=(6, 0))

        ttk.Checkbutton(
            inner_output,
            text="Usar nome padrão da lista",
            variable=self.use_default_list_name_var,
            command=self.update_settings_field_states,
        ).grid(row=2, column=0, columnspan=3, sticky="w", pady=(12, 8))

        ttk.Label(inner_output, text="Nome padrão").grid(row=3, column=0, sticky="w", padx=(0, 6))
        self.ent_default_name = ttk.Entry(inner_output, textvariable=self.default_list_name_var, width=30)
        self.ent_default_name.grid(row=3, column=1, sticky="w")

        box_sizes = ttk.LabelFrame(wrap, text="Cadastro de tamanhos", style="Card.TLabelframe")
        box_sizes.pack(fill="x", pady=(0, 10))

        inner_sizes = tk.Frame(box_sizes, bg=COLOR_PANEL)
        inner_sizes.pack(fill="x", padx=12, pady=12)
        inner_sizes.columnconfigure(2, weight=1)

        self._build_size_group_editor(inner_sizes, GROUP_MALE, row=0)
        self._build_size_group_editor(inner_sizes, GROUP_FEMALE, row=1)
        self._build_size_group_editor(inner_sizes, GROUP_CHILD, row=2)

        tk.Label(
            inner_sizes,
            text=(
                "Base sizes = tamanhos-base\n"
                "Prefixes = prefixos\n"
                "Suffixes = sufixos\n"
                "Os campos de tamanho continuam sempre em maiúsculas."
            ),
            bg=COLOR_PANEL,
            fg=COLOR_TEXT_MUTED,
            justify="left",
            anchor="w",
            font=("Segoe UI", 9),
        ).grid(row=9, column=0, columnspan=4, sticky="w", pady=(8, 6))

        tk.Label(
            inner_sizes,
            textvariable=self.size_summary_var,
            bg=COLOR_PANEL,
            fg=COLOR_TEXT,
            justify="left",
            anchor="w",
            font=("Segoe UI", 9),
        ).grid(row=10, column=0, columnspan=4, sticky="w")

        notes = ttk.LabelFrame(wrap, text="Observações", style="Card.TLabelframe")
        notes.pack(fill="x", pady=(0, 10))

        inner_notes = tk.Frame(notes, bg=COLOR_PANEL)
        inner_notes.pack(fill="x", padx=12, pady=12)

        tk.Label(
            inner_notes,
            text=(
                "• O shell visual foi adaptado do estilo da UI do Nexor.\n"
                "• No Texpad, o editor continua sendo o centro da experiência.\n"
                "• A saída organizada continua sendo gerada em vírgula."
            ),
            bg=COLOR_PANEL,
            fg=COLOR_TEXT_MUTED,
            justify="left",
            anchor="w",
            font=("Segoe UI", 10),
        ).pack(anchor="w")

        actions = tk.Frame(wrap, bg=COLOR_APP_BG)
        actions.pack(fill="x", pady=(0, 6))

        ttk.Button(actions, text="Restaurar padrões gerais", command=self.restore_default_settings).pack(side="left")
        ttk.Button(actions, text="Restaurar tamanhos padrão", command=self.restore_default_size_settings).pack(
            side="left", padx=(6, 0)
        )
        ttk.Button(actions, text="Salvar configurações", style="Accent.TButton", command=self.save_settings_from_ui).pack(
            side="right"
        )

        self.update_settings_field_states()
        self.screens["settings"] = screen

    def _build_size_group_editor(self, parent: tk.Widget, group_key: str, *, row: int) -> None:
        label = GROUP_LABELS[group_key]
        vars_map = self.size_group_vars[group_key]
        base_row = row * 3

        ttk.Label(parent, text=label).grid(row=base_row, column=0, sticky="w", padx=(0, 8), pady=(10, 2))

        ttk.Label(parent, text="Tamanhos-base").grid(row=base_row, column=1, sticky="w", padx=(0, 6), pady=(10, 2))
        ttk.Entry(parent, textvariable=vars_map["base_sizes"], width=50).grid(
            row=base_row, column=2, sticky="ew", pady=(10, 2)
        )

        ttk.Label(parent, text="Prefixos").grid(row=base_row + 1, column=1, sticky="w", padx=(0, 6), pady=2)
        ttk.Entry(parent, textvariable=vars_map["prefixes"], width=50).grid(
            row=base_row + 1, column=2, sticky="ew", pady=2
        )

        ttk.Label(parent, text="Sufixos").grid(row=base_row + 2, column=1, sticky="w", padx=(0, 6), pady=(2, 8))
        ttk.Entry(parent, textvariable=vars_map["suffixes"], width=50).grid(
            row=base_row + 2, column=2, sticky="ew", pady=(2, 8)
        )

    # ------------------------------------------------------------------
    # Navigation
    # ------------------------------------------------------------------
    def show_screen(self, key: str) -> None:
        if key not in self.screens:
            return

        for screen in self.screens.values():
            screen.grid_remove()

        self.screens[key].grid()
        self.current_screen_key = key

        titles = {
            "home": "Home",
            "editor": "Editor",
            "settings": "Configurações",
        }
        self.page_title_var.set(titles.get(key, APP_NAME))
        self._update_nav_state()
        self._update_top_action()
        self._refresh_home_dashboard()

    def _update_nav_state(self) -> None:
        for key, btn in self.nav_buttons.items():
            active = key == self.current_screen_key
            btn.configure(
                bg=(COLOR_SIDEBAR_ACTIVE if active else COLOR_SIDEBAR),
                fg=(COLOR_TEXT if active else COLOR_TEXT_MUTED),
                activebackground=(COLOR_SIDEBAR_HOVER if not active else COLOR_SIDEBAR_ACTIVE),
                activeforeground=COLOR_TEXT,
            )

    def _update_top_action(self) -> None:
        if self.current_screen_key == "home":
            self.top_action_var.set("Abrir Lista")
        elif self.current_screen_key == "editor":
            self.top_action_var.set("Processar Lista")
        else:
            self.top_action_var.set("Salvar Configurações")

    def _run_primary_action(self) -> None:
        if self.current_screen_key == "home":
            self.open_input_file()
        elif self.current_screen_key == "editor":
            self.process_and_preview()
        else:
            self.save_settings_from_ui()

    def _tick_clock(self) -> None:
        self.clock_var.set(Path().cwd().anchor and __import__("datetime").datetime.now().strftime("%d/%m/%Y %H:%M:%S"))
        self.after(1000, self._tick_clock)

    # ------------------------------------------------------------------
    # Init helpers
    # ------------------------------------------------------------------
    def _init_size_group_vars(self) -> None:
        self.size_group_vars = {}
        for group_key in (GROUP_MALE, GROUP_FEMALE, GROUP_CHILD):
            group = self.size_cfg["groups"][group_key]
            self.size_group_vars[group_key] = {
                "base_sizes": tk.StringVar(value=tokens_to_csv(group.get("base_sizes", []))),
                "prefixes": tk.StringVar(value=tokens_to_csv(group.get("prefixes", []))),
                "suffixes": tk.StringVar(value=tokens_to_csv(group.get("suffixes", []))),
            }

    def _load_size_config_into_vars(self) -> None:
        for group_key in (GROUP_MALE, GROUP_FEMALE, GROUP_CHILD):
            group = self.size_cfg["groups"][group_key]
            self.size_group_vars[group_key]["base_sizes"].set(tokens_to_csv(group.get("base_sizes", [])))
            self.size_group_vars[group_key]["prefixes"].set(tokens_to_csv(group.get("prefixes", [])))
            self.size_group_vars[group_key]["suffixes"].set(tokens_to_csv(group.get("suffixes", [])))

    def _load_last_opened_file_label(self) -> None:
        last_opened = (self.cfg.get("last_opened_file") or "").strip()
        if last_opened:
            self.current_file = Path(last_opened)
            self.current_file_var.set(f"Arquivo atual: {self.current_file}")

    def _configure_tags(self) -> None:
        self.txt_in.tag_configure(self.SEARCH_TAG, background="#4A4412", foreground="#FFF2A8")
        self.txt_in.tag_configure(self.SEARCH_CURRENT_TAG, background="#8A5A19", foreground="#FFFFFF")

    def _bind_events(self) -> None:
        self.txt_in.bind("<<Modified>>", self._on_editor_modified)
        self.txt_in.bind("<Control-f>", self._focus_find_entry)
        self.txt_in.bind("<Control-h>", self._focus_replace_entry)
        self.txt_in.bind("<Control-z>", self._handle_ctrl_z)
        self.txt_in.bind("<F3>", lambda _e: self.find_next())
        self.txt_in.bind("<Shift-F3>", lambda _e: self.find_previous())

        self.find_var.trace_add("write", self._on_search_param_changed)
        self.search_match_case_var.trace_add("write", self._on_search_param_changed)

    # ------------------------------------------------------------------
    # Generic helpers
    # ------------------------------------------------------------------
    def _set_status(self, text: str) -> None:
        self.status_var.set(text)
        self._refresh_home_dashboard()

    def _set_text_readonly(self, txt: tk.Text, readonly: bool) -> None:
        txt.configure(state=("disabled" if readonly else "normal"))

    def _copy_to_clipboard(self, text: str) -> None:
        root = self.winfo_toplevel()
        root.clipboard_clear()
        root.clipboard_append(text)
        root.update()

    def _open_path(self, path: Path) -> None:
        try:
            os.startfile(str(path))  # type: ignore[attr-defined]
        except AttributeError:
            messagebox.showinfo(APP_NAME, f"Caminho:\n{path}")
        except Exception as exc:
            messagebox.showerror(APP_NAME, f"Não foi possível abrir o caminho.\n\n{exc}")

    def _editor_case_mode(self) -> str:
        return CASE_LABEL_TO_VALUE.get(self.editor_case_label_var.get(), "original")

    def _default_case_mode(self) -> str:
        return CASE_LABEL_TO_VALUE.get(self.default_case_label_var.get(), "original")

    def _refresh_size_summary(self) -> None:
        male_sizes = build_group_sizes(self.size_cfg["groups"][GROUP_MALE])
        female_sizes = build_group_sizes(self.size_cfg["groups"][GROUP_FEMALE])
        child_sizes = build_group_sizes(self.size_cfg["groups"][GROUP_CHILD])

        total_sizes: list[str] = []
        seen: set[str] = set()
        for size in male_sizes + female_sizes + child_sizes:
            if size not in seen:
                seen.add(size)
                total_sizes.append(size)

        self.size_summary_var.set(
            "Tamanhos válidos atuais:\n"
            f"• Masculino: {', '.join(male_sizes) if male_sizes else '(nenhum)'}\n"
            f"• Feminino: {', '.join(female_sizes) if female_sizes else '(nenhum)'}\n"
            f"• Infantil: {', '.join(child_sizes) if child_sizes else '(nenhum)'}\n"
            f"• Total: {len(total_sizes)}"
        )

    def _refresh_home_dashboard(self) -> None:
        input_lines = 0
        output_lines = 0
        if hasattr(self, "txt_in"):
            input_lines = len([ln for ln in self.txt_in.get("1.0", "end-1c").splitlines() if ln.strip()])
        if hasattr(self, "txt_out"):
            output_lines = len([ln for ln in self.txt_out.get("1.0", "end-1c").splitlines() if ln.strip()])

        total_sizes = 0
        seen: set[str] = set()
        for group_key in (GROUP_MALE, GROUP_FEMALE, GROUP_CHILD):
            for size in build_group_sizes(self.size_cfg["groups"][group_key]):
                if size not in seen:
                    seen.add(size)
        total_sizes = len(seen)

        self.home_input_lines_var.set(str(input_lines))
        self.home_output_lines_var.set(str(output_lines))
        self.home_sizes_total_var.set(str(total_sizes))
        self.home_json_status_var.set("Ativo" if self.show_json_tab_var.get() else "Oculto")
        self.home_current_file_var.set(self.current_file.name if self.current_file else "Nenhum arquivo aberto")
        self.home_mode_var.set(self.editor_case_label_var.get())
        self.home_separator_var.set(separator_label(self.editor_separator_var.get()))
        self.home_output_policy_var.set(
            "Pasta padrão" if self.use_default_output_dir_var.get() else "Escolher ao salvar"
        )

        if not self.current_file and input_lines == 0:
            self.home_alert_var.set("Nenhuma lista carregada ainda.")
        elif not self.show_json_tab_var.get():
            self.home_alert_var.set("A visualização JSON está oculta nas configurações.")
        elif not self.use_default_output_dir_var.get():
            self.home_alert_var.set("A saída pedirá pasta manual a cada salvamento.")
        else:
            self.home_alert_var.set("Tudo pronto para editar e processar.")

        self.home_recent_a_var.set(self.current_file.name if self.current_file else "Nova lista")
        self.home_recent_b_var.set(f"{output_lines} linha(s) organizadas")
        self.home_recent_c_var.set("Prévia pronta" if self._last_json.strip() else "Aguardando processamento")
        self.home_recent_d_var.set(str(BACKUP_DIR))

        if hasattr(self, "home_recent_tree"):
            self.home_recent_tree.delete(*self.home_recent_tree.get_children())
            rows = [
                ("Arquivo atual", self.home_recent_a_var.get()),
                ("Saída", self.home_recent_b_var.get()),
                ("JSON", self.home_recent_c_var.get()),
                ("Backups", self.home_recent_d_var.get()),
            ]
            for row in rows:
                self.home_recent_tree.insert("", "end", values=row)

    def _apply_runtime_preferences(self) -> None:
        json_widget = str(self.tab_json)
        current_tabs = self.outputs_nb.tabs()

        if self.show_json_tab_var.get():
            if json_widget not in current_tabs:
                self.outputs_nb.add(self.tab_json, text="Prévia JSON")
        else:
            if json_widget in current_tabs:
                if self.outputs_nb.select() == json_widget:
                    self.outputs_nb.select(self.tab_list)
                self.outputs_nb.forget(self.tab_json)

        if self.show_copy_json_button_var.get():
            self.btn_copy_json.pack(side="left", padx=(6, 0))
        else:
            self.btn_copy_json.pack_forget()

        if self.show_generate_json_button_var.get():
            self.btn_generate_json.pack(side="right")
        else:
            self.btn_generate_json.pack_forget()

        self.update_settings_field_states()
        self._refresh_home_dashboard()

    def update_settings_field_states(self) -> None:
        output_state = "normal" if self.use_default_output_dir_var.get() else "disabled"
        self.ent_output_dir.configure(state=output_state)
        self.btn_pick_output_dir.configure(state=output_state)

        name_state = "normal" if self.use_default_list_name_var.get() else "disabled"
        self.ent_default_name.configure(state=name_state)

    def _build_size_config_from_ui(self) -> dict:
        cfg = load_size_config()

        for group_key in (GROUP_MALE, GROUP_FEMALE, GROUP_CHILD):
            vars_map = self.size_group_vars[group_key]
            base_sizes = parse_csv_tokens(vars_map["base_sizes"].get())
            prefixes = parse_csv_tokens(vars_map["prefixes"].get())
            suffixes = parse_csv_tokens(vars_map["suffixes"].get())

            if not base_sizes:
                raise ValueError(f"Informe ao menos um tamanho-base para {GROUP_LABELS[group_key]}.")

            cfg = update_group_config(
                cfg,
                group_key,
                base_sizes=base_sizes,
                prefixes=prefixes,
                suffixes=suffixes,
            )

        return cfg

    # ------------------------------------------------------------------
    # File actions
    # ------------------------------------------------------------------
    def open_input_file(self) -> None:
        filename = filedialog.askopenfilename(
            title=f"{APP_NAME} - Abrir lista",
            filetypes=[
                ("Arquivos de texto", "*.txt *.csv *.list"),
                ("Todos os arquivos", "*.*"),
            ],
        )
        if not filename:
            return

        path = Path(filename)

        try:
            content = self._read_text_file(path)
        except Exception as exc:
            messagebox.showerror(APP_NAME, str(exc))
            return

        self.txt_in.delete("1.0", "end")
        self.txt_in.insert("1.0", content)
        self.current_file = path
        self.current_file_var.set(f"Arquivo atual: {path}")
        self._search_dirty = True
        self.clear_search_highlight(keep_status=True)

        self.cfg["last_opened_file"] = str(path)
        save_config(self.cfg)

        self._set_status(f"Lista carregada: {path.name}")
        self.show_screen("editor")

    def save_input_file(self) -> None:
        if self.current_file is None:
            self.save_input_as_file()
            return

        text = self.txt_in.get("1.0", "end-1c")
        backup_path = None

        try:
            if self.current_file.exists():
                current_disk_text = self._read_text_file(self.current_file)
                if current_disk_text != text:
                    backup_path = create_backup(self.current_file)

            self._write_text_file(self.current_file, text)

            self.cfg["last_opened_file"] = str(self.current_file)
            save_config(self.cfg)

            if backup_path:
                self._set_status(f"Entrada salva: {self.current_file.name} | Backup: {backup_path.name}")
            else:
                self._set_status(f"Entrada salva: {self.current_file.name}")
        except Exception as exc:
            messagebox.showerror(APP_NAME, f"Falha ao salvar a lista.\n\n{exc}")

    def save_input_as_file(self) -> None:
        filename = filedialog.asksaveasfilename(
            title=f"{APP_NAME} - Salvar entrada como",
            defaultextension=".txt",
            filetypes=[
                ("Arquivos de texto", "*.txt"),
                ("CSV", "*.csv"),
                ("Todos os arquivos", "*.*"),
            ],
        )
        if not filename:
            return

        path = Path(filename)
        text = self.txt_in.get("1.0", "end-1c")

        try:
            if path.exists():
                create_backup(path)

            self._write_text_file(path, text)
            self.current_file = path
            self.current_file_var.set(f"Arquivo atual: {path}")
            self.cfg["last_opened_file"] = str(path)
            save_config(self.cfg)
            self._set_status(f"Entrada salva como: {path.name}")
        except Exception as exc:
            messagebox.showerror(APP_NAME, f"Falha ao salvar a entrada.\n\n{exc}")

    def open_backups_folder(self) -> None:
        self._open_path(BACKUP_DIR)

    def _read_text_file(self, path: Path) -> str:
        for enc in ("utf-8-sig", "utf-8", "cp1252", "latin-1"):
            try:
                return path.read_text(encoding=enc)
            except Exception:
                continue
        raise ValueError("Não foi possível ler o arquivo com as codificações suportadas.")

    def _write_text_file(self, path: Path, text: str) -> None:
        path.write_text(text, encoding="utf-8", newline="\n")

    # ------------------------------------------------------------------
    # Editor tools
    # ------------------------------------------------------------------
    def undo_last_change(self) -> None:
        try:
            self.txt_in.edit_undo()
            self._set_status("Última alteração desfeita.")
        except tk.TclError:
            self._set_status("Nada para desfazer.")

    def set_default_separator(self) -> None:
        self.editor_separator_var.set(",")
        self._set_status('Separador da entrada redefinido para ",".')

    def clean_unnecessary_spaces(self) -> None:
        text = self.txt_in.get("1.0", "end-1c")
        if not text.strip():
            messagebox.showwarning(APP_NAME, "Não há texto para limpar.")
            return

        try:
            cleaned = clean_text_by_separator(text, self.editor_separator_var.get())
        except Exception as exc:
            messagebox.showerror(APP_NAME, str(exc))
            return

        self.txt_in.delete("1.0", "end")
        self.txt_in.insert("1.0", cleaned)

        sep_label = separator_label(self.editor_separator_var.get())
        self._set_status(f"Espaços desnecessários removidos usando o separador {sep_label!r}.")

    def clear_all(self) -> None:
        self.txt_in.delete("1.0", "end")
        self.txt_out.delete("1.0", "end")

        self._set_text_readonly(self.txt_json, False)
        self.txt_json.delete("1.0", "end")
        self._set_text_readonly(self.txt_json, True)

        self._rows = []
        self._last_orders = []
        self._last_json = ""
        self._search_matches = []
        self._search_current_idx = -1
        self._search_dirty = True
        self.current_file = None
        self.current_file_var.set("Arquivo atual: (nova lista)")
        self.clear_search_highlight(keep_status=True)
        self._set_status("Campos limpos.")

    # ------------------------------------------------------------------
    # Processing / exporting
    # ------------------------------------------------------------------
    def process_and_preview(self) -> None:
        raw = self.txt_in.get("1.0", "end-1c")
        if not raw.strip():
            messagebox.showwarning(APP_NAME, "Cole ou abra uma lista na entrada.")
            return

        try:
            rows = process_text(
                raw,
                input_separator=self.editor_separator_var.get(),
                size_config=self.size_cfg,
            )
            if not rows:
                messagebox.showwarning(APP_NAME, "Nenhuma linha válida encontrada.")
                return

            case_mode = self._editor_case_mode()

            organized = build_output(
                rows,
                size_config=self.size_cfg,
                case_mode=case_mode,
                output_separator=",",
            )
            self.txt_out.delete("1.0", "end")
            self.txt_out.insert("1.0", organized)

            orders = build_orders_from_orderlist(
                rows,
                size_config=self.size_cfg,
                case_mode=case_mode,
            )
            preview = build_json_preview(orders)

            self._rows = rows
            self._last_orders = orders
            self._last_json = preview

            self._set_text_readonly(self.txt_json, False)
            self.txt_json.delete("1.0", "end")
            self.txt_json.insert("1.0", preview)
            self._set_text_readonly(self.txt_json, True)

            self.outputs_nb.select(self.tab_list)
            self.show_screen("editor")
            self._set_status(
                f"Processado: {len(rows)} linha(s) | Separador: {separator_label(self.editor_separator_var.get())!r}."
            )
        except Exception as exc:
            messagebox.showerror(APP_NAME, str(exc))
            self._set_status(f"Erro: {exc}")

    def copy_output(self) -> None:
        text = self.txt_out.get("1.0", "end").strip()
        if not text:
            messagebox.showwarning(APP_NAME, "Não há saída organizada para copiar.")
            return
        self._copy_to_clipboard(text)
        self._set_status("Saída organizada copiada.")

    def copy_json(self) -> None:
        if not self._last_json.strip():
            messagebox.showwarning(APP_NAME, "Ainda não há prévia do JSON. Clique em Processar.")
            return
        self._copy_to_clipboard(self._last_json)
        self._set_status("JSON copiado.")

    def save_output(self) -> None:
        text = self.txt_out.get("1.0", "end-1c")
        if not text.strip():
            self.process_and_preview()
            text = self.txt_out.get("1.0", "end-1c")
            if not text.strip():
                return

        output_dir = self._resolve_output_dir()
        if output_dir is None:
            return

        base_name = self._resolve_output_name()
        if base_name is None:
            return

        try:
            path = export_output_text(text, output_dir, base_name)
            self._set_status(f"Saída organizada salva em: {path.name}")
            messagebox.showinfo(APP_NAME, f"Saída organizada salva:\n{path}")
        except Exception as exc:
            messagebox.showerror(APP_NAME, f"Falha ao salvar a saída.\n\n{exc}")

    def generate_json(self) -> None:
        if not self._last_orders:
            self.process_and_preview()
            if not self._last_orders:
                return

        output_dir = self._resolve_output_dir()
        if output_dir is None:
            return

        base_name = self._resolve_output_name()
        if base_name is None:
            return

        try:
            path = export_json(self._last_orders, output_dir, base_name)
            if self.show_json_tab_var.get():
                self.outputs_nb.select(self.tab_json)
            self._set_status(f"JSON gerado: {path.name}")
            messagebox.showinfo(APP_NAME, f"JSON gerado:\n{path}\n\nRegistros: {len(self._last_orders)}")
        except Exception as exc:
            messagebox.showerror(APP_NAME, f"Falha ao gerar o JSON.\n\n{exc}")

    def _resolve_output_dir(self) -> Path | None:
        if self.use_default_output_dir_var.get():
            folder_text = self.output_dir_var.get().strip()
            if not folder_text:
                messagebox.showerror(APP_NAME, "Defina uma pasta padrão de saída nas configurações.")
                return None
            folder = Path(folder_text)
            folder.mkdir(parents=True, exist_ok=True)
            return folder

        chosen = filedialog.askdirectory(title=f"{APP_NAME} - Escolha a pasta de saída")
        if not chosen:
            return None
        folder = Path(chosen)
        folder.mkdir(parents=True, exist_ok=True)
        return folder

    def _resolve_output_name(self) -> str | None:
        suggested = self.default_list_name_var.get().strip() or "lista"
        if self.current_file:
            suggested = sanitize_base_filename(self.current_file.stem)

        if self.use_default_list_name_var.get():
            base = sanitize_base_filename(self.default_list_name_var.get().strip())
            if not base:
                messagebox.showerror(APP_NAME, "Defina um nome padrão válido nas configurações.")
                return None
            return base

        typed = simpledialog.askstring(
            APP_NAME,
            "Informe o nome da lista/arquivo:",
            initialvalue=suggested,
            parent=self.winfo_toplevel(),
        )
        if typed is None:
            return None

        base = sanitize_base_filename(typed)
        if not base:
            messagebox.showerror(APP_NAME, "Nome inválido.")
            return None
        return base

    # ------------------------------------------------------------------
    # Settings
    # ------------------------------------------------------------------
    def pick_default_output_folder(self) -> None:
        folder = filedialog.askdirectory(title=f"{APP_NAME} - Escolha a pasta padrão de saída")
        if folder:
            self.output_dir_var.set(folder)

    def save_settings_from_ui(self) -> None:
        if self.use_default_output_dir_var.get() and not self.output_dir_var.get().strip():
            messagebox.showerror(APP_NAME, "Informe uma pasta padrão de saída ou desative essa opção.")
            return

        if self.use_default_list_name_var.get() and not self.default_list_name_var.get().strip():
            messagebox.showerror(APP_NAME, "Informe um nome padrão de lista ou desative essa opção.")
            return

        try:
            normalize_separator(self.default_input_separator_var.get())
        except Exception:
            messagebox.showerror(APP_NAME, "Separador padrão inválido.")
            return

        try:
            size_cfg = self._build_size_config_from_ui()
        except Exception as exc:
            messagebox.showerror(APP_NAME, str(exc))
            return

        self.cfg = {
            **DEFAULT_CONFIG,
            **self.cfg,
            "show_json_tab": bool(self.show_json_tab_var.get()),
            "show_generate_json_button": bool(self.show_generate_json_button_var.get()),
            "show_copy_json_button": bool(self.show_copy_json_button_var.get()),
            "use_default_output_dir": bool(self.use_default_output_dir_var.get()),
            "output_dir": self.output_dir_var.get().strip(),
            "use_default_list_name": bool(self.use_default_list_name_var.get()),
            "default_list_name": self.default_list_name_var.get().strip(),
            "default_case_mode": self._default_case_mode(),
            "default_input_separator": self.default_input_separator_var.get().strip() or ",",
            "last_opened_file": str(self.current_file) if self.current_file else "",
        }

        save_config(self.cfg)
        save_size_config(size_cfg)
        self.size_cfg = size_cfg

        self.editor_case_label_var.set(CASE_VALUE_TO_LABEL.get(self.cfg["default_case_mode"], "Original"))
        self.editor_separator_var.set(self.cfg["default_input_separator"])

        self._apply_runtime_preferences()
        self._refresh_size_summary()
        self._set_status("Configurações salvas.")
        messagebox.showinfo(APP_NAME, "Configurações salvas com sucesso.")

    def restore_default_settings(self) -> None:
        cfg = reset_config()
        self.cfg = cfg

        self.show_json_tab_var.set(bool(cfg["show_json_tab"]))
        self.show_generate_json_button_var.set(bool(cfg["show_generate_json_button"]))
        self.show_copy_json_button_var.set(bool(cfg["show_copy_json_button"]))

        self.use_default_output_dir_var.set(bool(cfg["use_default_output_dir"]))
        self.output_dir_var.set(str(cfg["output_dir"]))

        self.use_default_list_name_var.set(bool(cfg["use_default_list_name"]))
        self.default_list_name_var.set(str(cfg["default_list_name"]))

        self.default_case_label_var.set(CASE_VALUE_TO_LABEL.get(cfg["default_case_mode"], "Original"))
        self.default_input_separator_var.set(cfg["default_input_separator"])

        self.editor_case_label_var.set(CASE_VALUE_TO_LABEL.get(cfg["default_case_mode"], "Original"))
        self.editor_separator_var.set(cfg["default_input_separator"])

        self._apply_runtime_preferences()
        self.update_settings_field_states()
        self._set_status("Configurações gerais restauradas para o padrão.")

    def restore_default_size_settings(self) -> None:
        self.size_cfg = reset_size_config()
        self._load_size_config_into_vars()
        self._refresh_size_summary()
        self._refresh_home_dashboard()
        self._set_status("Tamanhos restaurados para o padrão.")

    # ------------------------------------------------------------------
    # Search / replace
    # ------------------------------------------------------------------
    def _handle_ctrl_z(self, _event=None):
        self.undo_last_change()
        return "break"

    def _focus_find_entry(self, _event=None):
        if self.ent_find is not None:
            self.ent_find.focus_set()
            self.ent_find.selection_range(0, "end")
        return "break"

    def _focus_replace_entry(self, _event=None):
        if self.ent_replace is not None:
            self.ent_replace.focus_set()
            self.ent_replace.selection_range(0, "end")
        return "break"

    def _on_editor_modified(self, _event=None) -> None:
        if self.txt_in.edit_modified():
            self._search_dirty = True
            self.txt_in.edit_modified(False)
            self._refresh_home_dashboard()

    def _on_search_param_changed(self, *_args) -> None:
        self._search_dirty = True
        self.clear_search_highlight(keep_status=True)

    def clear_search_highlight(self, keep_status: bool = False) -> None:
        self.txt_in.tag_remove(self.SEARCH_TAG, "1.0", "end")
        self.txt_in.tag_remove(self.SEARCH_CURRENT_TAG, "1.0", "end")
        self._search_matches = []
        self._search_current_idx = -1
        if not keep_status:
            self._set_status("Destaques de busca limpos.")

    def _build_search_matches(self) -> list[str]:
        pattern = self.find_var.get()
        if not pattern:
            self.clear_search_highlight(keep_status=True)
            return []

        self.txt_in.tag_remove(self.SEARCH_TAG, "1.0", "end")
        self.txt_in.tag_remove(self.SEARCH_CURRENT_TAG, "1.0", "end")

        matches: list[str] = []
        start = "1.0"
        nocase = 0 if self.search_match_case_var.get() else 1

        while True:
            pos = self.txt_in.search(pattern, start, stopindex="end-1c", nocase=nocase)
            if not pos:
                break

            end = f"{pos}+{len(pattern)}c"
            matches.append(pos)
            self.txt_in.tag_add(self.SEARCH_TAG, pos, end)
            start = end

        self._search_matches = matches
        self._search_dirty = False
        self._search_current_idx = -1
        return matches

    def _ensure_search_matches(self) -> list[str]:
        if self._search_dirty:
            return self._build_search_matches()
        return self._search_matches

    def _set_current_match(self, idx: int) -> None:
        if not self._search_matches:
            self._search_current_idx = -1
            return

        idx = idx % len(self._search_matches)
        self._search_current_idx = idx

        self.txt_in.tag_remove(self.SEARCH_CURRENT_TAG, "1.0", "end")
        pos = self._search_matches[idx]
        end = f"{pos}+{len(self.find_var.get())}c"
        self.txt_in.tag_add(self.SEARCH_CURRENT_TAG, pos, end)
        self.txt_in.mark_set("insert", pos)
        self.txt_in.see(pos)
        self.txt_in.focus_set()

        self._set_status(f"Ocorrência {idx + 1} de {len(self._search_matches)}.")

    def find_next_from_cursor(self) -> None:
        matches = self._build_search_matches()
        if not matches:
            messagebox.showinfo(APP_NAME, "Texto não encontrado.")
            self._set_status("Texto não encontrado.")
            return

        cursor = self.txt_in.index("insert")
        chosen = 0
        for i, pos in enumerate(matches):
            if self.txt_in.compare(pos, ">=", cursor):
                chosen = i
                break

        self._set_current_match(chosen)

    def find_next(self, _event=None):
        matches = self._ensure_search_matches()
        if not matches:
            messagebox.showinfo(APP_NAME, "Texto não encontrado.")
            self._set_status("Texto não encontrado.")
            return "break"

        next_idx = 0 if self._search_current_idx < 0 else self._search_current_idx + 1
        self._set_current_match(next_idx)
        return "break"

    def find_previous(self, _event=None):
        matches = self._ensure_search_matches()
        if not matches:
            messagebox.showinfo(APP_NAME, "Texto não encontrado.")
            self._set_status("Texto não encontrado.")
            return "break"

        prev_idx = len(matches) - 1 if self._search_current_idx < 0 else self._search_current_idx - 1
        self._set_current_match(prev_idx)
        return "break"

    def replace_current(self) -> None:
        pattern = self.find_var.get()
        if not pattern:
            messagebox.showwarning(APP_NAME, "Informe o texto que deseja localizar.")
            return

        matches = self._ensure_search_matches()
        if not matches:
            messagebox.showinfo(APP_NAME, "Texto não encontrado.")
            self._set_status("Texto não encontrado.")
            return

        if self._search_current_idx < 0:
            self._set_current_match(0)

        pos = self._search_matches[self._search_current_idx]
        end = f"{pos}+{len(pattern)}c"

        self.txt_in.delete(pos, end)
        self.txt_in.insert(pos, self.replace_var.get())

        self._search_dirty = True
        new_matches = self._build_search_matches()
        if new_matches:
            self._set_current_match(min(self._search_current_idx, len(new_matches) - 1))
        else:
            self._set_status("Substituição concluída. Não restaram ocorrências.")

    def replace_all(self) -> None:
        pattern = self.find_var.get()
        if not pattern:
            messagebox.showwarning(APP_NAME, "Informe o texto que deseja localizar.")
            return

        source = self.txt_in.get("1.0", "end-1c")
        replacement = self.replace_var.get()

        if self.search_match_case_var.get():
            count = source.count(pattern)
            if count == 0:
                messagebox.showinfo(APP_NAME, "Texto não encontrado.")
                self._set_status("Texto não encontrado.")
                return
            result = source.replace(pattern, replacement)
        else:
            regex = re.compile(re.escape(pattern), re.IGNORECASE)
            result, count = regex.subn(replacement, source)
            if count == 0:
                messagebox.showinfo(APP_NAME, "Texto não encontrado.")
                self._set_status("Texto não encontrado.")
                return

        self.txt_in.delete("1.0", "end")
        self.txt_in.insert("1.0", result)
        self._search_dirty = True
        self._build_search_matches()
        self._set_status(f"Substituir tudo concluído: {count} ocorrência(s) alterada(s).")


def build_ui(parent):
    return TexpadFrame(parent)


def main() -> None:
    root = tk.Tk()
    TexpadFrame(root)
    root.mainloop()


if __name__ == "__main__":
    main()