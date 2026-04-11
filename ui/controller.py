from __future__ import annotations

import json
import os
import re
from pathlib import Path
import tkinter as tk
from tkinter import filedialog, messagebox, simpledialog, ttk
from urllib import error, request

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
    extract_list_text_from_json_data,
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
from ui import theme
from ui.controllers import (
    EditorRuntime,
    SearchRuntime,
    SettingsRuntime,
    ThemeRuntime,
)


CASE_LABEL_TO_VALUE = {
    "Original": "original",
    "Tudo maiúsculo": "upper",
    "Tudo minúsculo": "lower",
}
CASE_VALUE_TO_LABEL = {value: label for label, value in CASE_LABEL_TO_VALUE.items()}


class TexpadController:
    SEARCH_TAG = "search_match"
    SEARCH_CURRENT_TAG = "search_current"

    def __init__(self, root: tk.Misc) -> None:
        self.root = root

        self.cfg = load_config()
        self.size_cfg = load_size_config()

        self.current_file: Path | None = None
        self._rows = []
        self._last_orders = []
        self._last_json = ""
        self._search_matches: list[str] = []
        self._search_current_idx = -1
        self._search_dirty = True
        self._theme_switch_in_progress = False

        self.shell = None
        self.editor_view = None
        self.settings_view = None

        self.txt_in: tk.Text | None = None
        self.txt_out: tk.Text | None = None
        self.txt_json: tk.Text | None = None
        self.outputs_nb = None
        self.tab_list = None
        self.tab_json = None
        self.ent_find: ttk.Entry | None = None
        self.ent_replace: ttk.Entry | None = None

        self.ent_output_dir: ttk.Entry | None = None
        self.btn_pick_output_dir: ttk.Button | None = None
        self.ent_default_name: ttk.Entry | None = None

        self.current_file_var = tk.StringVar(value="Arquivo atual: (nova lista)")
        self.status_var = tk.StringVar(value="Pronto.")
        self.size_summary_var = tk.StringVar(value="")

        self.find_var = tk.StringVar(value="")
        self.replace_var = tk.StringVar(value="")
        self.search_match_case_var = tk.BooleanVar(value=False)

        self.editor_separator_var = tk.StringVar(value=self.cfg.get("default_input_separator", ","))
        self.editor_case_label_var = tk.StringVar(
            value=CASE_VALUE_TO_LABEL.get(self.cfg.get("default_case_mode", "original"), "Original")
        )

        self.show_json_tab_var = tk.BooleanVar(value=bool(self.cfg.get("show_json_tab", False)))
        self.show_generate_json_button_var = tk.BooleanVar(
            value=bool(self.cfg.get("show_generate_json_button", False))
        )
        self.show_copy_json_button_var = tk.BooleanVar(
            value=bool(self.cfg.get("show_copy_json_button", False))
        )

        self.use_default_output_dir_var = tk.BooleanVar(value=bool(self.cfg.get("use_default_output_dir", False)))
        self.output_dir_var = tk.StringVar(value=str(self.cfg.get("output_dir", "")))

        self.use_default_list_name_var = tk.BooleanVar(value=bool(self.cfg.get("use_default_list_name", False)))
        self.default_list_name_var = tk.StringVar(value=str(self.cfg.get("default_list_name", "lista")))

        self.default_case_label_var = tk.StringVar(
            value=CASE_VALUE_TO_LABEL.get(self.cfg.get("default_case_mode", "original"), "Original")
        )
        self.default_input_separator_var = tk.StringVar(value=self.cfg.get("default_input_separator", ","))

        self.theme_name_var = tk.StringVar(value=self.cfg.get("theme_name", theme.DEFAULT_THEME_NAME))

        self.size_group_vars: dict[str, dict[str, tk.StringVar]] = {}
        self._init_size_group_vars()
        self._load_last_opened_file_label()
        self._refresh_size_summary()

        theme.set_active_theme(self.theme_name_var.get())

        self.search_runtime = SearchRuntime(self)
        self.theme_runtime = ThemeRuntime(self)
        self.settings_runtime = SettingsRuntime(self)
        self.editor_runtime = EditorRuntime(self)

    # ------------------------------------------------------------------
    # Public helpers
    # ------------------------------------------------------------------
    @property
    def case_labels(self) -> list[str]:
        return list(CASE_LABEL_TO_VALUE.keys())

    @property
    def theme_names(self) -> list[str]:
        return theme.get_theme_names()

    def attach_shell(self, shell) -> None:
        self.shell = shell

    def attach_views(self, *, editor_view=None, settings_view=None) -> None:
        if editor_view is not None:
            self.editor_view = editor_view
        if settings_view is not None:
            self.settings_view = settings_view

    def bind_editor_widgets(
        self,
        *,
        txt_in: tk.Text,
        txt_out: tk.Text,
        txt_json: tk.Text,
        notebook,
        tab_list,
        tab_json,
        ent_find: ttk.Entry | None = None,
        ent_replace: ttk.Entry | None = None,
    ) -> None:
        self.editor_runtime.bind_editor_widgets(
            txt_in=txt_in,
            txt_out=txt_out,
            txt_json=txt_json,
            notebook=notebook,
            tab_list=tab_list,
            tab_json=tab_json,
            ent_find=ent_find,
            ent_replace=ent_replace,
        )
        self.search_runtime.configure_tags()

    def bind_settings_widgets(
        self,
        *,
        ent_output_dir: ttk.Entry,
        btn_pick_output_dir: ttk.Button,
        ent_default_name: ttk.Entry,
    ) -> None:
        self.settings_runtime.bind_settings_widgets(
            ent_output_dir=ent_output_dir,
            btn_pick_output_dir=btn_pick_output_dir,
            ent_default_name=ent_default_name,
        )

    # ------------------------------------------------------------------
    # Theme
    # ------------------------------------------------------------------
    def apply_theme(self, theme_name: str | None = None, *, persist: bool = False) -> None:
        self.theme_runtime.apply_theme(theme_name or self.theme_name_var.get(), persist=persist)

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

    # ------------------------------------------------------------------
    # Generic helpers
    # ------------------------------------------------------------------
    def _set_status(self, text: str) -> None:
        self.status_var.set(text)

    def _set_text_readonly(self, txt: tk.Text | None, readonly: bool) -> None:
        self.editor_runtime.set_text_readonly(txt, readonly)

    def _copy_to_clipboard(self, text: str) -> None:
        self.root.clipboard_clear()
        self.root.clipboard_append(text)
        self.root.update()

    def _focus_input_editor(self) -> None:
        if self.txt_in is not None:
            self.txt_in.focus_set()

    def _goto_input_line_if_present(self, message: str) -> None:
        if self.txt_in is None:
            return

        match = re.search(r"linha\s+(\d+)", message, flags=re.IGNORECASE)
        if not match:
            return

        try:
            line_no = int(match.group(1))
            idx = f"{line_no}.0"
            self.txt_in.mark_set("insert", idx)
            self.txt_in.see(idx)
            self.txt_in.tag_remove("sel", "1.0", "end")
            self.txt_in.tag_add("sel", idx, f"{idx} lineend")
        except Exception:
            pass

    def _handle_processing_error(self, exc: Exception) -> None:
        message = str(exc)
        self.show_screen("editor")
        self._goto_input_line_if_present(message)
        self._focus_input_editor()
        self._set_status(f"Erro: {message}")
        messagebox.showerror(APP_NAME, message)

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

    def _load_json_from_url(self, url: str):
        text = (url or "").strip()

        if not text:
            raise ValueError("Informe um link HTTP(S).")

        if not text.lower().startswith(("http://", "https://")):
            raise ValueError("O link precisa começar com http:// ou https://.")

        try:
            req = request.Request(
                text,
                headers={"User-Agent": f"{APP_NAME}/1.0 (+local)"},
            )
            with request.urlopen(req, timeout=20) as response:
                charset = response.headers.get_content_charset() or "utf-8"
                body = response.read().decode(charset, errors="replace")
            return json.loads(body)
        except error.URLError as exc:
            raise ValueError(f"Não foi possível acessar o link.\n{exc}") from exc
        except json.JSONDecodeError as exc:
            raise ValueError(f"O conteúdo retornado pelo link não é um JSON válido.\n{exc}") from exc

    # ------------------------------------------------------------------
    # Summary
    # ------------------------------------------------------------------
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

    # ------------------------------------------------------------------
    # Search
    # ------------------------------------------------------------------
    def clear_search_highlight(self, keep_status: bool = False) -> None:
        self.search_runtime.clear_search_highlight()
        if not keep_status:
            self._set_status("Destaque da busca removido.")

    def find_next_from_cursor(self) -> None:
        self.search_runtime.find_next_from_cursor()

    def find_next(self, _event=None):
        self.search_runtime.find_next()
        return "break"

    def find_previous(self, _event=None):
        self.search_runtime.find_previous()
        return "break"

    def replace_current(self) -> None:
        self.search_runtime.replace_current()

    def replace_all(self) -> None:
        self.search_runtime.replace_all()

    # ------------------------------------------------------------------
    # File actions
    # ------------------------------------------------------------------
    def open_input_file(self) -> None:
        if self.txt_in is None:
            return

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
        self._focus_input_editor()

    def save_input_file(self) -> None:
        if self.txt_in is None:
            return

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
        if self.txt_in is None:
            return

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
    # Editor actions
    # ------------------------------------------------------------------
    def undo_last_change(self) -> None:
        if self.txt_in is None:
            return
        try:
            self.txt_in.edit_undo()
            self._set_status("Última alteração desfeita.")
        except tk.TclError:
            self._set_status("Nada para desfazer.")

    def set_default_separator(self) -> None:
        self.editor_separator_var.set(",")
        self._set_status('Separador da entrada redefinido para ",".')

    def clean_unnecessary_spaces(self) -> None:
        if self.txt_in is None:
            return

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
        if self.txt_in is None or self.txt_out is None or self.txt_json is None:
            return

        self.txt_in.delete("1.0", "end")
        self.txt_out.delete("1.0", "end")

        self._set_text_readonly(self.txt_json, False)
        self.txt_json.delete("1.0", "end")
        self._set_text_readonly(self.txt_json, True)

        self._rows = []
        self._last_orders = []
        self._last_json = ""
        self._search_dirty = True
        self.current_file = None
        self.current_file_var.set("Arquivo atual: (nova lista)")
        self.clear_search_highlight(keep_status=True)
        self._set_status("Campos limpos.")
        self._focus_input_editor()

    def extract_list_from_link(self) -> None:
        if self.txt_in is None:
            return

        url = simpledialog.askstring(
            APP_NAME,
            "Cole o link do JSON para extrair a lista:",
            parent=self.root,
        )
        if url is None:
            return

        try:
            data = self._load_json_from_url(url)
            extracted = extract_list_text_from_json_data(data, output_separator=",")

            if not extracted.strip():
                raise ValueError("Nenhuma linha foi extraída do link informado.")

            self.txt_in.delete("1.0", "end")
            self.txt_in.insert("1.0", extracted)

            self.current_file = None
            self.current_file_var.set("Arquivo atual: (lista extraída do link)")
            self._search_dirty = True
            self.clear_search_highlight(keep_status=True)

            self._set_status("Lista extraída do link.")
            self.show_screen("editor")
            self._focus_input_editor()

            self.process_and_preview()

        except Exception as exc:
            self._set_status(f"Erro: {exc}")
            messagebox.showerror(APP_NAME, f"Falha ao extrair a lista do link.\n\n{exc}")

    # ------------------------------------------------------------------
    # Processing / exporting
    # ------------------------------------------------------------------
    def process_and_preview(self) -> None:
        if self.txt_in is None or self.txt_out is None or self.txt_json is None:
            return

        raw = self.txt_in.get("1.0", "end-1c")
        if not raw.strip():
            self.show_screen("editor")
            self._focus_input_editor()
            messagebox.showwarning(APP_NAME, "Cole ou abra uma lista na entrada.")
            return

        try:
            rows = process_text(
                raw,
                input_separator=self.editor_separator_var.get(),
                size_config=self.size_cfg,
            )
            if not rows:
                self.show_screen("editor")
                self._focus_input_editor()
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

            if self.outputs_nb is not None and self.tab_list is not None:
                self.outputs_nb.select(self.tab_list)

            self.show_screen("editor")
            self._set_status(
                f"Processado: {len(rows)} linha(s) | Separador: {separator_label(self.editor_separator_var.get())!r}."
            )
        except Exception as exc:
            self._handle_processing_error(exc)

    def copy_output(self) -> None:
        if self.txt_out is None:
            return
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
        if self.txt_out is None:
            return

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
            if self.show_json_tab_var.get() and self.outputs_nb is not None and self.tab_json is not None:
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
            parent=self.root,
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

    def update_settings_field_states(self) -> None:
        self.settings_runtime.update_settings_field_states()

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

        selected_theme = self.theme_name_var.get()
        if selected_theme not in theme.THEMES:
            selected_theme = theme.DEFAULT_THEME_NAME

        old_theme = self.cfg.get("theme_name", theme.DEFAULT_THEME_NAME)

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
            "theme_name": selected_theme,
            "last_opened_file": str(self.current_file) if self.current_file else "",
        }

        save_config(self.cfg)
        save_size_config(size_cfg)
        self.size_cfg = size_cfg

        self.editor_case_label_var.set(CASE_VALUE_TO_LABEL.get(self.cfg["default_case_mode"], "Original"))
        self.editor_separator_var.set(self.cfg["default_input_separator"])

        if old_theme != selected_theme:
            self.apply_theme(selected_theme, persist=False)
        else:
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
        self.theme_name_var.set(cfg.get("theme_name", theme.DEFAULT_THEME_NAME))

        self.editor_case_label_var.set(CASE_VALUE_TO_LABEL.get(cfg["default_case_mode"], "Original"))
        self.editor_separator_var.set(cfg["default_input_separator"])

        self.apply_theme(self.theme_name_var.get(), persist=False)
        self._apply_runtime_preferences()
        self.update_settings_field_states()
        self._set_status("Configurações gerais restauradas para o padrão.")

    def restore_default_size_settings(self) -> None:
        self.size_cfg = reset_size_config()
        self._load_size_config_into_vars()
        self._refresh_size_summary()
        self._set_status("Tamanhos restaurados para o padrão.")

    # ------------------------------------------------------------------
    # Runtime preferences
    # ------------------------------------------------------------------
    def _apply_runtime_preferences(self) -> None:
        self.editor_runtime.apply_runtime_preferences()
        self.settings_runtime.update_settings_field_states()

    # ------------------------------------------------------------------
    # Navigation helper
    # ------------------------------------------------------------------
    def show_screen(self, key: str) -> None:
        if self.shell is not None:
            self.shell.show_screen(key)