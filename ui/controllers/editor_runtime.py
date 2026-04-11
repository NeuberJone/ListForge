from __future__ import annotations

import tkinter as tk


class EditorRuntime:
    def __init__(self, controller) -> None:
        self.controller = controller

    # ------------------------------------------------------------------
    # Bind principal dos widgets do editor
    # ------------------------------------------------------------------
    def bind_editor_widgets(
        self,
        *,
        txt_in,
        txt_out,
        txt_json,
        notebook,
        tab_list,
        tab_json,
        ent_find=None,
        ent_replace=None,
    ) -> None:
        self.controller.txt_in = txt_in
        self.controller.txt_out = txt_out
        self.controller.txt_json = txt_json
        self.controller.outputs_nb = notebook
        self.controller.tab_list = tab_list
        self.controller.tab_json = tab_json
        self.controller.ent_find = ent_find
        self.controller.ent_replace = ent_replace

        self.set_text_readonly(self.controller.txt_json, True)
        self.configure_tags()
        self.bind_editor_events()
        self.controller.update_settings_field_states()

        if not self.controller.txt_in.get("1.0", "end-1c").strip():
            self.controller.txt_in.insert(
                "1.0",
                "G,JÃO,10\n"
                "JOÃO,5,G,M\n"
                "MANEL,PP\n"
                "JUACA,JUSÉ,PP\n",
            )

    # ------------------------------------------------------------------
    # Preferências em runtime
    # ------------------------------------------------------------------
    def apply_runtime_preferences(self) -> None:
        editor_view = getattr(self.controller, "editor_view", None)
        if editor_view is not None:
            editor_view.apply_runtime_preferences()

        self.sync_json_output_visibility()

    def sync_json_output_visibility(self) -> None:
        outputs_nb = getattr(self.controller, "outputs_nb", None)
        tab_json = getattr(self.controller, "tab_json", None)
        tab_list = getattr(self.controller, "tab_list", None)
        show_json_var = getattr(self.controller, "show_json_tab_var", None)

        if outputs_nb is None or tab_json is None or show_json_var is None:
            return

        try:
            visible = bool(show_json_var.get())
        except Exception:
            visible = False

        try:
            current_tabs = outputs_nb.tabs()
        except Exception:
            current_tabs = ()

        json_tab_id = str(tab_json)
        json_is_present = json_tab_id in current_tabs

        if visible and not json_is_present:
            try:
                outputs_nb.add(tab_json, text="Prévia JSON")
            except Exception:
                pass

        if not visible and json_is_present:
            try:
                outputs_nb.forget(tab_json)
            except Exception:
                pass

            if tab_list is not None:
                try:
                    outputs_nb.select(tab_list)
                except Exception:
                    pass

    # ------------------------------------------------------------------
    # Utilidades visuais do editor
    # ------------------------------------------------------------------
    def set_text_readonly(self, widget: tk.Text | None, readonly: bool) -> None:
        if widget is None:
            return

        try:
            widget.configure(state="disabled" if readonly else "normal")
        except Exception:
            pass

    def set_json_readonly(self, readonly: bool = True) -> None:
        self.set_text_readonly(getattr(self.controller, "txt_json", None), readonly)

    def configure_tags(self) -> None:
        txt_in = getattr(self.controller, "txt_in", None)
        if txt_in is None:
            return

        try:
            txt_in.tag_configure(
                self.controller.SEARCH_TAG,
                background="#4A4412",
                foreground="#FFF2A8",
            )
            txt_in.tag_configure(
                self.controller.SEARCH_CURRENT_TAG,
                background="#8A5A19",
                foreground="#FFFFFF",
            )
        except Exception:
            pass

    def bind_editor_events(self) -> None:
        txt_in = getattr(self.controller, "txt_in", None)
        if txt_in is None:
            return

        txt_in.bind("<<Modified>>", self._on_editor_modified)
        txt_in.bind("<Control-f>", self._focus_find_entry)
        txt_in.bind("<Control-h>", self._focus_replace_entry)
        txt_in.bind("<Control-z>", self._handle_ctrl_z)
        txt_in.bind("<F3>", lambda _e: self.controller.find_next())
        txt_in.bind("<Shift-F3>", lambda _e: self.controller.find_previous())

        self.controller.find_var.trace_add("write", self._on_search_param_changed)
        self.controller.search_match_case_var.trace_add("write", self._on_search_param_changed)

    # ------------------------------------------------------------------
    # Eventos do editor
    # ------------------------------------------------------------------
    def _handle_ctrl_z(self, _event=None):
        self.controller.undo_last_change()
        return "break"

    def _focus_find_entry(self, _event=None):
        ent_find = getattr(self.controller, "ent_find", None)
        if ent_find is not None:
            ent_find.focus_set()
            ent_find.selection_range(0, "end")
        return "break"

    def _focus_replace_entry(self, _event=None):
        ent_replace = getattr(self.controller, "ent_replace", None)
        if ent_replace is not None:
            ent_replace.focus_set()
            ent_replace.selection_range(0, "end")
        return "break"

    def _on_editor_modified(self, _event=None) -> None:
        txt_in = getattr(self.controller, "txt_in", None)
        if txt_in is None:
            return

        try:
            if txt_in.edit_modified():
                self.controller._search_dirty = True
                txt_in.edit_modified(False)
        except Exception:
            pass

    def _on_search_param_changed(self, *_args) -> None:
        self.controller._search_dirty = True
        self.controller.clear_search_highlight(keep_status=True)