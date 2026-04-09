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

        self._set_text_readonly(self.controller.txt_json, True)
        self._configure_editor_tags()
        self._bind_editor_events()
        self.controller.update_settings_field_states()

        if not self.controller.txt_in.get("1.0", "end-1c").strip():
            self.controller.txt_in.insert(
                "1.0",
                "G,JÃO,10\n"
                "JOÃO,5,G,M\n"
                "MANEL,PP\n"
                "JUACA,JUSÉ,PP\n",
            )

        self.controller.refresh_home_dashboard()

    # ------------------------------------------------------------------
    # Preferências em runtime
    # ------------------------------------------------------------------
    def apply_runtime_preferences(self) -> None:
        editor_view = getattr(self.controller, "editor_view", None)
        if editor_view is not None:
            editor_view.apply_runtime_preferences()

        self._sync_json_output_visibility()

    def _sync_json_output_visibility(self) -> None:
        outputs_nb = getattr(self.controller, "outputs_nb", None)
        tab_json = getattr(self.controller, "tab_json", None)
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

            try:
                outputs_nb.select(self.controller.tab_list)
            except Exception:
                pass

    # ------------------------------------------------------------------
    # Utilidades visuais do editor
    # ------------------------------------------------------------------
    def _set_text_readonly(self, widget: tk.Text | None, readonly: bool) -> None:
        if widget is None:
            return

        try:
            widget.configure(state="disabled" if readonly else "normal")
        except Exception:
            pass

    def set_json_readonly(self, readonly: bool = True) -> None:
        self._set_text_readonly(getattr(self.controller, "txt_json", None), readonly)

    def _configure_editor_tags(self) -> None:
        txt_in = getattr(self.controller, "txt_in", None)
        txt_out = getattr(self.controller, "txt_out", None)
        txt_json = getattr(self.controller, "txt_json", None)

        widgets = [w for w in (txt_in, txt_out, txt_json) if w is not None]

        for widget in widgets:
            try:
                widget.tag_configure("highlight", background="#3A4C6B")
                widget.tag_configure("current_highlight", background="#4C8DFF", foreground="#FFFFFF")
            except Exception:
                pass

        search_runtime = getattr(self.controller, "search_runtime", None)
        if search_runtime is not None:
            try:
                search_runtime.configure_tags()
            except Exception:
                pass

    def _bind_editor_events(self) -> None:
        txt_in = getattr(self.controller, "txt_in", None)
        if txt_in is None:
            return

        txt_in.bind("<<Modified>>", self._on_input_modified, add="+")
        txt_in.bind("<KeyRelease>", self._on_input_key_release, add="+")
        txt_in.bind("<<Paste>>", self._schedule_home_refresh, add="+")
        txt_in.bind("<<Cut>>", self._schedule_home_refresh, add="+")
        txt_in.bind("<<Undo>>", self._schedule_home_refresh, add="+")
        txt_in.bind("<<Redo>>", self._schedule_home_refresh, add="+")

    def _on_input_modified(self, _event=None) -> None:
        txt_in = getattr(self.controller, "txt_in", None)
        if txt_in is None:
            return

        try:
            if txt_in.edit_modified():
                txt_in.edit_modified(False)
                self._schedule_home_refresh()
        except Exception:
            pass

    def _on_input_key_release(self, _event=None) -> None:
        self._schedule_home_refresh()

    def _schedule_home_refresh(self, _event=None) -> None:
        txt_in = getattr(self.controller, "txt_in", None)
        if txt_in is None:
            return

        try:
            txt_in.after_idle(self.controller.refresh_home_dashboard)
        except Exception:
            pass