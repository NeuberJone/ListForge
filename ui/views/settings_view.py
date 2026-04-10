from __future__ import annotations

import tkinter as tk
from tkinter import ttk

from ui import theme
from ui.widgets import SegmentedControl


class SettingsView(tk.Frame):
    def __init__(self, parent: tk.Misc, controller) -> None:
        super().__init__(parent, bg=theme.active_theme().app_bg)
        self.controller = controller

        self.grid_rowconfigure(0, weight=1)
        self.grid_columnconfigure(0, weight=1)

        self.main_sections: dict[str, tk.Frame] = {}
        self.size_sections: dict[str, tk.Frame] = {}

        self.current_main_section = "general"
        self.current_size_section = "male"

        self._build()

    def _build(self) -> None:
        t = theme.active_theme()

        wrap = tk.Frame(self, bg=t.app_bg)
        wrap.grid(row=0, column=0, sticky="nsew")
        wrap.grid_rowconfigure(1, weight=1)
        wrap.grid_columnconfigure(0, weight=1)

        self.main_selector = SegmentedControl(
            wrap,
            items=[
                ("general", "Geral"),
                ("output", "Saída"),
                ("json", "JSON"),
                ("sizes", "Tamanhos"),
                ("appearance", "Aparência"),
            ],
            command=self._select_main_section,
            selected_key="general",
        )
        self.main_selector.grid(row=0, column=0, sticky="ew", pady=(0, 10))

        self.content_host = tk.Frame(wrap, bg=t.app_bg)
        self.content_host.grid(row=1, column=0, sticky="nsew")
        self.content_host.grid_rowconfigure(0, weight=1)
        self.content_host.grid_columnconfigure(0, weight=1)

        self.tab_general = tk.Frame(self.content_host, bg=t.app_bg)
        self.tab_output = tk.Frame(self.content_host, bg=t.app_bg)
        self.tab_json = tk.Frame(self.content_host, bg=t.app_bg)
        self.tab_sizes = tk.Frame(self.content_host, bg=t.app_bg)
        self.tab_appearance = tk.Frame(self.content_host, bg=t.app_bg)

        self.main_sections = {
            "general": self.tab_general,
            "output": self.tab_output,
            "json": self.tab_json,
            "sizes": self.tab_sizes,
            "appearance": self.tab_appearance,
        }

        self._build_general_tab()
        self._build_output_tab()
        self._build_json_tab()
        self._build_sizes_tab()
        self._build_appearance_tab()

        actions = tk.Frame(wrap, bg=t.app_bg)
        actions.grid(row=2, column=0, sticky="ew", pady=(10, 6))

        ttk.Button(
            actions,
            text="Restaurar padrões gerais",
            command=self.controller.restore_default_settings,
        ).pack(side="left")

        ttk.Button(
            actions,
            text="Restaurar tamanhos padrão",
            command=self.controller.restore_default_size_settings,
        ).pack(side="left", padx=(6, 0))

        ttk.Button(
            actions,
            text="Salvar configurações",
            style="Accent.TButton",
            command=self.controller.save_settings_from_ui,
        ).pack(side="right")

        self._show_main_section("general")

    # ------------------------------------------------------------------
    # Helpers
    # ------------------------------------------------------------------
    def _make_section(self, parent: tk.Misc, title: str) -> tuple[ttk.LabelFrame, tk.Frame]:
        box = ttk.LabelFrame(parent, text=title, style="Card.TLabelframe")
        box.pack(fill="both", expand=True, pady=(0, 10))

        inner = tk.Frame(box, bg=theme.active_theme().panel_bg)
        inner.pack(fill="both", expand=True, padx=12, pady=12)
        return box, inner

    def _select_main_section(self, section_key: str) -> None:
        self.current_main_section = section_key
        self._show_main_section(section_key)

    def _show_main_section(self, section_key: str) -> None:
        for panel in self.main_sections.values():
            panel.grid_forget()

        panel = self.main_sections[section_key]
        panel.grid(row=0, column=0, sticky="nsew")

    def _select_size_section(self, section_key: str) -> None:
        self.current_size_section = section_key
        self._show_size_section(section_key)

    def _show_size_section(self, section_key: str) -> None:
        for panel in self.size_sections.values():
            panel.pack_forget()

        panel = self.size_sections[section_key]
        panel.pack(fill="both", expand=True)

    # ------------------------------------------------------------------
    # Geral
    # ------------------------------------------------------------------
    def _build_general_tab(self) -> None:
        t = theme.active_theme()
        container = tk.Frame(self.tab_general, bg=t.app_bg)
        container.pack(fill="both", expand=True)

        _, inner_text = self._make_section(container, "Padrões de texto")

        ttk.Label(inner_text, text="Modo padrão de maiúsculas/minúsculas").grid(
            row=0, column=0, sticky="w", padx=(0, 6)
        )
        self.cmb_default_case = ttk.Combobox(
            inner_text,
            textvariable=self.controller.default_case_label_var,
            values=self.controller.case_labels,
            width=18,
            state="readonly",
        )
        self.cmb_default_case.grid(row=0, column=1, sticky="w")

        ttk.Label(inner_text, text="Separador padrão da entrada").grid(
            row=1,
            column=0,
            sticky="w",
            padx=(0, 6),
            pady=(10, 0),
        )
        self.ent_default_separator = ttk.Entry(
            inner_text,
            textvariable=self.controller.default_input_separator_var,
            width=8,
        )
        self.ent_default_separator.grid(row=1, column=1, sticky="w", pady=(10, 0))

        ttk.Label(inner_text, text='Use "\\t" para tab').grid(
            row=1,
            column=2,
            sticky="w",
            padx=(8, 0),
            pady=(10, 0),
        )

        _, inner_notes = self._make_section(container, "Observações")

        tk.Label(
            inner_notes,
            text=(
                "• O modo de maiúsculas/minúsculas afeta apenas strings comuns.\n"
                "• Campos de tamanho continuam sempre em maiúsculas.\n"
                "• O separador padrão afeta a leitura da entrada."
            ),
            bg=t.panel_bg,
            fg=t.text_muted,
            justify="left",
            anchor="w",
            font=(theme.FONT_FAMILY, 10),
        ).pack(anchor="w")

    # ------------------------------------------------------------------
    # Saída
    # ------------------------------------------------------------------
    def _build_output_tab(self) -> None:
        t = theme.active_theme()
        container = tk.Frame(self.tab_output, bg=t.app_bg)
        container.pack(fill="both", expand=True)

        _, inner_output = self._make_section(container, "Saída")
        inner_output.grid_columnconfigure(1, weight=1)

        ttk.Checkbutton(
            inner_output,
            text="Usar pasta padrão para salvar a saída",
            variable=self.controller.use_default_output_dir_var,
            command=self.controller.update_settings_field_states,
        ).grid(row=0, column=0, columnspan=3, sticky="w", pady=(0, 8))

        ttk.Label(inner_output, text="Pasta padrão").grid(
            row=1,
            column=0,
            sticky="w",
            padx=(0, 6),
        )
        self.ent_output_dir = ttk.Entry(
            inner_output,
            textvariable=self.controller.output_dir_var,
            width=60,
        )
        self.ent_output_dir.grid(row=1, column=1, sticky="ew")

        self.btn_pick_output_dir = ttk.Button(
            inner_output,
            text="Escolher...",
            command=self.controller.pick_default_output_folder,
        )
        self.btn_pick_output_dir.grid(row=1, column=2, sticky="w", padx=(6, 0))

        ttk.Checkbutton(
            inner_output,
            text="Usar nome padrão da lista",
            variable=self.controller.use_default_list_name_var,
            command=self.controller.update_settings_field_states,
        ).grid(row=2, column=0, columnspan=3, sticky="w", pady=(12, 8))

        ttk.Label(inner_output, text="Nome padrão").grid(
            row=3,
            column=0,
            sticky="w",
            padx=(0, 6),
        )
        self.ent_default_name = ttk.Entry(
            inner_output,
            textvariable=self.controller.default_list_name_var,
            width=30,
        )
        self.ent_default_name.grid(row=3, column=1, sticky="w")

    # ------------------------------------------------------------------
    # JSON
    # ------------------------------------------------------------------
    def _build_json_tab(self) -> None:
        t = theme.active_theme()
        container = tk.Frame(self.tab_json, bg=t.app_bg)
        container.pack(fill="both", expand=True)

        _, inner_json = self._make_section(container, "JSON")

        ttk.Checkbutton(
            inner_json,
            text="Mostrar área de JSON",
            variable=self.controller.show_json_tab_var,
        ).pack(anchor="w", pady=(0, 4))

        ttk.Checkbutton(
            inner_json,
            text="Mostrar botão Gerar JSON",
            variable=self.controller.show_generate_json_button_var,
        ).pack(anchor="w", pady=4)

        ttk.Checkbutton(
            inner_json,
            text="Mostrar botão Copiar JSON",
            variable=self.controller.show_copy_json_button_var,
        ).pack(anchor="w", pady=(4, 0))

    # ------------------------------------------------------------------
    # Tamanhos
    # ------------------------------------------------------------------
    def _build_sizes_tab(self) -> None:
        t = theme.active_theme()
        container = tk.Frame(self.tab_sizes, bg=t.app_bg)
        container.pack(fill="both", expand=True)

        outer, outer_inner = self._make_section(container, "Cadastro de tamanhos")
        outer_inner.pack_forget()

        self.size_selector = SegmentedControl(
            outer,
            items=[
                ("male", "Masculino"),
                ("female", "Feminino"),
                ("child", "Infantil"),
            ],
            command=self._select_size_section,
            selected_key="male",
        )
        self.size_selector.pack(fill="x", padx=12, pady=(12, 0))

        content_wrap = tk.Frame(outer, bg=t.panel_bg)
        content_wrap.pack(fill="both", expand=True, padx=12, pady=12)

        self.size_male = tk.Frame(content_wrap, bg=t.panel_bg)
        self.size_female = tk.Frame(content_wrap, bg=t.panel_bg)
        self.size_child = tk.Frame(content_wrap, bg=t.panel_bg)

        self.size_sections = {
            "male": self.size_male,
            "female": self.size_female,
            "child": self.size_child,
        }

        self._build_single_size_tab(self.size_male, "male")
        self._build_single_size_tab(self.size_female, "female")
        self._build_single_size_tab(self.size_child, "child")

        self._show_size_section("male")

        notes = tk.Frame(container, bg=t.app_bg)
        notes.pack(fill="x", pady=(10, 0))

        _, inner_notes = self._make_section(notes, "Observações")

        tk.Label(
            inner_notes,
            text=(
                "Base sizes = tamanhos-base\n"
                "Prefixes = prefixos\n"
                "Suffixes = sufixos\n"
                "Os campos de tamanho permanecem sempre em maiúsculas."
            ),
            bg=t.panel_bg,
            fg=t.text_muted,
            justify="left",
            anchor="w",
            font=(theme.FONT_FAMILY, 10),
        ).pack(anchor="w")

        tk.Label(
            inner_notes,
            textvariable=self.controller.size_summary_var,
            bg=t.panel_bg,
            fg=t.text,
            justify="left",
            anchor="w",
            font=(theme.FONT_FAMILY, 9),
        ).pack(anchor="w", pady=(10, 0))

    def _build_single_size_tab(self, parent: tk.Misc, group_key: str) -> None:
        vars_map = self.controller.size_group_vars[group_key]

        wrap = tk.Frame(parent, bg=theme.active_theme().panel_bg)
        wrap.pack(fill="both", expand=True, padx=16, pady=16)
        wrap.grid_columnconfigure(1, weight=1)

        ttk.Label(wrap, text="Tamanhos-base").grid(
            row=0,
            column=0,
            sticky="w",
            padx=(0, 8),
            pady=(0, 8),
        )
        ttk.Entry(
            wrap,
            textvariable=vars_map["base_sizes"],
            width=60,
        ).grid(row=0, column=1, sticky="ew", pady=(0, 8))

        ttk.Label(wrap, text="Prefixos").grid(
            row=1,
            column=0,
            sticky="w",
            padx=(0, 8),
            pady=(0, 8),
        )
        ttk.Entry(
            wrap,
            textvariable=vars_map["prefixes"],
            width=60,
        ).grid(row=1, column=1, sticky="ew", pady=(0, 8))

        ttk.Label(wrap, text="Sufixos").grid(
            row=2,
            column=0,
            sticky="w",
            padx=(0, 8),
        )
        ttk.Entry(
            wrap,
            textvariable=vars_map["suffixes"],
            width=60,
        ).grid(row=2, column=1, sticky="ew")

    # ------------------------------------------------------------------
    # Aparência
    # ------------------------------------------------------------------
    def _build_appearance_tab(self) -> None:
        t = theme.active_theme()
        container = tk.Frame(self.tab_appearance, bg=t.app_bg)
        container.pack(fill="both", expand=True)

        _, inner_appearance = self._make_section(container, "Tema da interface")

        ttk.Label(inner_appearance, text="Tema").grid(
            row=0,
            column=0,
            sticky="w",
            padx=(0, 8),
        )
        self.cmb_theme = ttk.Combobox(
            inner_appearance,
            textvariable=self.controller.theme_name_var,
            values=self.controller.theme_names,
            width=24,
            state="readonly",
        )
        self.cmb_theme.grid(row=0, column=1, sticky="w")
        self.cmb_theme.bind(
            "<<ComboboxSelected>>",
            lambda _e: self.controller.apply_theme(self.cmb_theme.get(), persist=True),
        )

        tk.Label(
            inner_appearance,
            text=(
                "Você pode manter o tema atual do programa ou usar um tema inspirado "
                "na interface do SisBolt."
            ),
            bg=t.panel_bg,
            fg=t.text_muted,
            justify="left",
            anchor="w",
            font=(theme.FONT_FAMILY, 10),
            wraplength=700,
        ).grid(row=1, column=0, columnspan=2, sticky="w", pady=(12, 0))

    # ------------------------------------------------------------------
    # Runtime bindings / refresh
    # ------------------------------------------------------------------
    def bind_runtime_widgets(self) -> None:
        self.controller.bind_settings_widgets(
            ent_output_dir=self.ent_output_dir,
            btn_pick_output_dir=self.btn_pick_output_dir,
            ent_default_name=self.ent_default_name,
        )

    def refresh(self) -> None:
        pass

    def refresh_theme(self) -> None:
        t = theme.active_theme()
        self.configure(bg=t.app_bg)
        self.main_selector.refresh_theme()
        self.size_selector.refresh_theme()