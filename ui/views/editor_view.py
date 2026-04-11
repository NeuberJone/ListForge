from __future__ import annotations

import tkinter as tk
from tkinter import ttk

from ui import theme
from ui.widgets import SegmentedControl, create_text_area, make_card


class CollapsiblePanel(tk.Frame):
    def __init__(self, parent: tk.Misc, title: str, *, starts_open: bool = False) -> None:
        super().__init__(parent, bg=theme.active_theme().app_bg)
        self.title = title
        self.is_open = starts_open
        self._build()

    def _build(self) -> None:
        t = theme.active_theme()

        self.card = make_card(self)
        self.card.pack(fill="x", expand=False)

        self.header = tk.Frame(self.card, bg=t.panel_bg)
        self.header.pack(fill="x")

        self.toggle_var = tk.StringVar()
        self._refresh_toggle_text()

        self.btn_toggle = tk.Button(
            self.header,
            textvariable=self.toggle_var,
            command=self.toggle,
            bg=t.panel_bg,
            fg=t.text,
            activebackground=t.panel_hover,
            activeforeground=t.text,
            relief="flat",
            bd=0,
            cursor="hand2",
            anchor="w",
            font=(theme.FONT_FAMILY, 10, "bold"),
            padx=14,
            pady=10,
        )
        self.btn_toggle.pack(fill="x")

        self.content = tk.Frame(self.card, bg=t.panel_bg)
        if self.is_open:
            self.content.pack(fill="x", padx=12, pady=(0, 12))

    def _refresh_toggle_text(self) -> None:
        arrow = "▼" if self.is_open else "▶"
        self.toggle_var.set(f"{arrow} {self.title}")

    def toggle(self) -> None:
        self.is_open = not self.is_open
        self._refresh_toggle_text()

        if self.is_open:
            self.content.pack(fill="x", padx=12, pady=(0, 12))
        else:
            self.content.pack_forget()

    def refresh_theme(self) -> None:
        t = theme.active_theme()
        self.configure(bg=t.app_bg)
        self.header.configure(bg=t.panel_bg)
        self.content.configure(bg=t.panel_bg)
        self.btn_toggle.configure(
            bg=t.panel_bg,
            fg=t.text,
            activebackground=t.panel_hover,
            activeforeground=t.text,
        )


class OutputTabsAdapter:
    """
    Adaptador leve para manter compatibilidade com o controller,
    que ainda espera uma interface parecida com ttk.Notebook.
    """

    def __init__(self, view: "EditorView") -> None:
        self.view = view

    def tabs(self):
        tabs = [str(self.view.tab_list)]
        if self.view.json_panel_visible:
            tabs.append(str(self.view.tab_json))
        return tuple(tabs)

    def add(self, widget, text: str = "") -> None:
        if widget == self.view.tab_json:
            self.view._set_json_panel_visible(True)

    def forget(self, widget) -> None:
        if widget == self.view.tab_json:
            self.view._set_json_panel_visible(False)

    def select(self, widget=None):
        if widget is None:
            if self.view.current_output_section == "json":
                return str(self.view.tab_json)
            return str(self.view.tab_list)

        if widget == self.view.tab_list or widget == str(self.view.tab_list):
            self.view._select_output_section("list")
            return str(self.view.tab_list)

        if widget == self.view.tab_json or widget == str(self.view.tab_json):
            if self.view.json_panel_visible:
                self.view._select_output_section("json")
                return str(self.view.tab_json)
            self.view._select_output_section("list")
            return str(self.view.tab_list)

        return str(self.view.tab_list)


class EditorView(tk.Frame):
    def __init__(self, parent: tk.Misc, controller) -> None:
        super().__init__(parent, bg=theme.active_theme().app_bg)
        self.controller = controller

        self.grid_columnconfigure(0, weight=1)
        self.grid_rowconfigure(3, weight=1)

        self.current_output_section = "list"
        self.json_panel_visible = True

        self._build()

    def _build(self) -> None:
        self._build_file_bar()
        self._build_prepare_panel()
        self._build_search_panel()
        self._build_editor_body()
        self._build_footer_actions()

    # ------------------------------------------------------------------
    # Barra superior do editor
    # ------------------------------------------------------------------
    def _build_file_bar(self) -> None:
        t = theme.active_theme()

        row_files = make_card(self)
        row_files.grid(row=0, column=0, sticky="ew", pady=(0, 10))

        files_inner = tk.Frame(row_files, bg=t.panel_bg)
        files_inner.pack(fill="x", padx=16, pady=12)

        self.btn_open_input = tk.Button(
            files_inner,
            text="Abrir Lista",
            command=self.controller.open_input_file,
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
        self.btn_open_input.pack(side="left")

        self.btn_extract_link = tk.Button(
            files_inner,
            text="Extrair lista do link",
            command=self.controller.extract_list_from_link,
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
        self.btn_extract_link.pack(side="left", padx=(6, 0))

        self.btn_save_input = tk.Button(
            files_inner,
            text="Salvar Entrada",
            command=self.controller.save_input_file,
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
        self.btn_save_input.pack(side="left", padx=(6, 0))

        self.btn_save_input_as = tk.Button(
            files_inner,
            text="Salvar Entrada Como",
            command=self.controller.save_input_as_file,
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
        self.btn_save_input_as.pack(side="left", padx=(6, 0))

        self.btn_undo = tk.Button(
            files_inner,
            text="Desfazer",
            command=self.controller.undo_last_change,
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
        self.btn_undo.pack(side="left", padx=(14, 0))

        self.btn_open_backups = tk.Button(
            files_inner,
            text="Abrir pasta de backups",
            command=self.controller.open_backups_folder,
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
        self.btn_open_backups.pack(side="left", padx=(6, 0))

        tk.Label(
            files_inner,
            textvariable=self.controller.current_file_var,
            bg=t.panel_bg,
            fg=t.text_muted,
            font=(theme.FONT_FAMILY, 9),
            anchor="e",
        ).pack(side="right")

    # ------------------------------------------------------------------
    # Painéis retráteis
    # ------------------------------------------------------------------
    def _build_prepare_panel(self) -> None:
        self.prepare_panel = CollapsiblePanel(self, "Preparação da lista", starts_open=False)
        self.prepare_panel.grid(row=1, column=0, sticky="ew", pady=(0, 10))

        inner = self.prepare_panel.content

        ttk.Label(inner, text="Separador da entrada").grid(row=0, column=0, sticky="w", padx=(0, 6))

        self.ent_separator = ttk.Entry(inner, textvariable=self.controller.editor_separator_var, width=8)
        self.ent_separator.grid(row=0, column=1, sticky="w")

        ttk.Label(inner, text='Use "\\t" para tab').grid(row=0, column=2, sticky="w", padx=(8, 18))

        self.btn_reset_separator = ttk.Button(
            inner,
            text="Padrão (,)",
            command=self.controller.set_default_separator,
        )
        self.btn_reset_separator.grid(row=0, column=3, sticky="w", padx=(0, 8))

        self.btn_clean_spaces = ttk.Button(
            inner,
            text="Remover espaços desnecessários",
            command=self.controller.clean_unnecessary_spaces,
        )
        self.btn_clean_spaces.grid(row=0, column=4, sticky="w", padx=(0, 18))

        ttk.Label(inner, text="Maiúsculas / minúsculas").grid(row=0, column=5, sticky="w", padx=(0, 6))

        self.cmb_case_mode = ttk.Combobox(
            inner,
            textvariable=self.controller.editor_case_label_var,
            values=self.controller.case_labels,
            width=18,
            state="readonly",
        )
        self.cmb_case_mode.grid(row=0, column=6, sticky="w")

    def _build_search_panel(self) -> None:
        self.search_panel = CollapsiblePanel(self, "Localizar / Localizar e substituir", starts_open=False)
        self.search_panel.grid(row=2, column=0, sticky="ew", pady=(0, 10))

        inner = self.search_panel.content

        ttk.Label(inner, text="Localizar").grid(row=0, column=0, sticky="w", padx=(0, 6))
        self.ent_find = ttk.Entry(inner, textvariable=self.controller.find_var, width=24)
        self.ent_find.grid(row=0, column=1, sticky="w")

        ttk.Label(inner, text="Substituir por").grid(row=0, column=2, sticky="w", padx=(16, 6))
        self.ent_replace = ttk.Entry(inner, textvariable=self.controller.replace_var, width=24)
        self.ent_replace.grid(row=0, column=3, sticky="w")

        ttk.Checkbutton(
            inner,
            text="Diferenciar maiúsculas/minúsculas",
            variable=self.controller.search_match_case_var,
        ).grid(row=0, column=4, sticky="w", padx=(16, 10))

        btns_search = tk.Frame(inner, bg=theme.active_theme().panel_bg)
        btns_search.grid(row=1, column=0, columnspan=5, sticky="w", pady=(10, 0))

        ttk.Button(btns_search, text="Localizar", command=self.controller.find_next_from_cursor).pack(side="left")
        ttk.Button(btns_search, text="Anterior", command=self.controller.find_previous).pack(side="left", padx=(6, 0))
        ttk.Button(btns_search, text="Próximo", command=self.controller.find_next).pack(side="left", padx=(6, 0))
        ttk.Button(btns_search, text="Substituir", command=self.controller.replace_current).pack(side="left", padx=(12, 0))
        ttk.Button(btns_search, text="Substituir tudo", command=self.controller.replace_all).pack(side="left", padx=(6, 0))
        ttk.Button(btns_search, text="Limpar destaque", command=self.controller.clear_search_highlight).pack(
            side="left",
            padx=(12, 0),
        )

    # ------------------------------------------------------------------
    # Corpo principal
    # ------------------------------------------------------------------
    def _build_editor_body(self) -> None:
        t = theme.active_theme()

        body = tk.Frame(self, bg=t.app_bg)
        body.grid(row=3, column=0, sticky="nsew")
        body.grid_rowconfigure(0, weight=1)
        body.grid_columnconfigure(0, weight=1)
        body.grid_columnconfigure(1, weight=1)

        left = make_card(body)
        left.grid(row=0, column=0, sticky="nsew", padx=(0, 8))
        left.grid_rowconfigure(1, weight=1)
        left.grid_columnconfigure(0, weight=1)

        right = make_card(body)
        right.grid(row=0, column=1, sticky="nsew", padx=(8, 0))
        right.grid_rowconfigure(2, weight=1)
        right.grid_columnconfigure(0, weight=1)

        tk.Label(
            left,
            text="Entrada / edição",
            bg=t.panel_bg,
            fg=t.text,
            font=(theme.FONT_FAMILY, 11, "bold"),
            anchor="w",
        ).grid(row=0, column=0, sticky="ew", padx=14, pady=(14, 8))

        self.input_editor, self.txt_in = create_text_area(left, background=t.editor_bg)
        self.input_editor.grid(row=1, column=0, sticky="nsew", padx=14, pady=(0, 14))

        tk.Label(
            right,
            text="Saída",
            bg=t.panel_bg,
            fg=t.text,
            font=(theme.FONT_FAMILY, 11, "bold"),
            anchor="w",
        ).grid(row=0, column=0, sticky="ew", padx=14, pady=(14, 8))

        self.output_selector = SegmentedControl(
            right,
            items=[
                ("list", "Lista organizada"),
                ("json", "Prévia JSON"),
            ],
            command=self._select_output_section,
            selected_key="list",
        )
        self.output_selector.grid(row=1, column=0, sticky="ew", padx=14, pady=(0, 8))

        self.output_content = tk.Frame(right, bg=t.panel_bg)
        self.output_content.grid(row=2, column=0, sticky="nsew", padx=14, pady=(0, 14))
        self.output_content.grid_rowconfigure(0, weight=1)
        self.output_content.grid_columnconfigure(0, weight=1)

        self.tab_list = tk.Frame(self.output_content, bg=t.panel_bg)
        self.tab_json = tk.Frame(self.output_content, bg=t.panel_bg)

        self.output_editor, self.txt_out = create_text_area(self.tab_list, background=t.editor_alt)
        self.output_editor.pack(fill="both", expand=True)

        self.json_editor, self.txt_json = create_text_area(self.tab_json, background=t.editor_alt)
        self.json_editor.pack(fill="both", expand=True)

        self.outputs_nb = OutputTabsAdapter(self)

        self.controller.bind_editor_widgets(
            txt_in=self.txt_in,
            txt_out=self.txt_out,
            txt_json=self.txt_json,
            notebook=self.outputs_nb,
            tab_list=self.tab_list,
            tab_json=self.tab_json,
            ent_find=self.ent_find,
            ent_replace=self.ent_replace,
        )

        self._set_json_panel_visible(bool(self.controller.show_json_tab_var.get()))
        self._select_output_section("list")

    def _select_output_section(self, section_key: str) -> None:
        if section_key == "json" and not self.json_panel_visible:
            section_key = "list"

        self.current_output_section = section_key

        self.tab_list.grid_forget()
        self.tab_json.grid_forget()

        if section_key == "json" and self.json_panel_visible:
            self.tab_json.grid(row=0, column=0, sticky="nsew")
        else:
            self.tab_list.grid(row=0, column=0, sticky="nsew")

    def _set_json_panel_visible(self, visible: bool) -> None:
        self.json_panel_visible = visible

        items = [("list", "Lista organizada")]
        if visible:
            items.append(("json", "Prévia JSON"))

        current = self.current_output_section
        if current == "json" and not visible:
            current = "list"

        self.output_selector.set_items(items, selected_key=current, invoke=False)
        self._select_output_section(current)

    # ------------------------------------------------------------------
    # Rodapé de ações
    # ------------------------------------------------------------------
    def _build_footer_actions(self) -> None:
        t = theme.active_theme()

        footer = tk.Frame(self, bg=t.app_bg)
        footer.grid(row=4, column=0, sticky="ew", pady=(10, 0))

        left_actions = tk.Frame(footer, bg=t.app_bg)
        left_actions.pack(side="left")

        right_actions = tk.Frame(footer, bg=t.app_bg)
        right_actions.pack(side="right")

        self.btn_clear_all = ttk.Button(left_actions, text="Limpar tudo", command=self.controller.clear_all)
        self.btn_clear_all.pack(side="left")

        self.btn_copy_output = ttk.Button(left_actions, text="Copiar saída", command=self.controller.copy_output)
        self.btn_copy_output.pack(side="left", padx=(6, 0))

        self.btn_save_output = ttk.Button(left_actions, text="Salvar saída", command=self.controller.save_output)
        self.btn_save_output.pack(side="left", padx=(6, 0))

        self.btn_copy_json = ttk.Button(left_actions, text="Copiar JSON", command=self.controller.copy_json)
        self.btn_copy_json.pack(side="left", padx=(6, 0))

        self.btn_generate_json = ttk.Button(right_actions, text="Gerar JSON", command=self.controller.generate_json)
        self.btn_generate_json.pack(side="right")

        self.btn_process = ttk.Button(
            right_actions,
            text="Processar",
            style="Accent.TButton",
            command=self.controller.process_and_preview,
        )
        self.btn_process.pack(side="right", padx=(0, 6))

    # ------------------------------------------------------------------
    # Preferências em runtime
    # ------------------------------------------------------------------
    def apply_runtime_preferences(self) -> None:
        self._set_json_panel_visible(bool(self.controller.show_json_tab_var.get()))

        if self.controller.show_copy_json_button_var.get():
            if not self.btn_copy_json.winfo_manager():
                self.btn_copy_json.pack(side="left", padx=(6, 0))
        else:
            if self.btn_copy_json.winfo_manager():
                self.btn_copy_json.pack_forget()

        if self.controller.show_generate_json_button_var.get():
            if not self.btn_generate_json.winfo_manager():
                self.btn_generate_json.pack(side="right")
        else:
            if self.btn_generate_json.winfo_manager():
                self.btn_generate_json.pack_forget()

    def refresh_theme(self) -> None:
        t = theme.active_theme()
        self.configure(bg=t.app_bg)
        self.output_selector.refresh_theme()
        self.prepare_panel.refresh_theme()
        self.search_panel.refresh_theme()