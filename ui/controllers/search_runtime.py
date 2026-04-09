from __future__ import annotations

import tkinter as tk


class SearchRuntime:
    HIGHLIGHT_TAG = "search_highlight"
    CURRENT_TAG = "search_current"

    def __init__(self, controller) -> None:
        self.controller = controller
        self._matches: list[tuple[str, str]] = []
        self._current_index: int = -1

    # ------------------------------------------------------------------
    # Configuração
    # ------------------------------------------------------------------
    def configure_tags(self) -> None:
        text = self._get_text_widget()
        if text is None:
            return

        text.tag_configure(self.HIGHLIGHT_TAG, background="#3A4C6B")
        text.tag_configure(self.CURRENT_TAG, background="#4C8DFF", foreground="#FFFFFF")

    # ------------------------------------------------------------------
    # API pública
    # ------------------------------------------------------------------
    def find_next_from_cursor(self) -> None:
        text = self._get_text_widget()
        term = self._get_find_term()

        if text is None or not term:
            self.clear_search_highlight()
            return

        self._refresh_matches(start_index=text.index("insert"))
        if not self._matches:
            return

        self._current_index = 0
        self._apply_highlight()
        self._focus_current_match()

    def find_next(self) -> None:
        if not self._matches:
            self.find_next_from_cursor()
            return

        self._current_index = (self._current_index + 1) % len(self._matches)
        self._apply_highlight()
        self._focus_current_match()

    def find_previous(self) -> None:
        if not self._matches:
            self.find_next_from_cursor()
            return

        self._current_index = (self._current_index - 1) % len(self._matches)
        self._apply_highlight()
        self._focus_current_match()

    def replace_current(self) -> None:
        text = self._get_text_widget()
        replace_term = self._get_replace_term()

        if text is None:
            return

        if not self._matches:
            self.find_next_from_cursor()
            if not self._matches:
                return

        start, end = self._matches[self._current_index]

        text.delete(start, end)
        text.insert(start, replace_term)

        next_insert = f"{start}+{len(replace_term)}c"
        text.mark_set("insert", next_insert)

        self._refresh_matches(start_index=start)
        if not self._matches:
            self.clear_search_highlight()
            return

        self._current_index = min(self._current_index, len(self._matches) - 1)
        self._apply_highlight()
        self._focus_current_match()

    def replace_all(self) -> None:
        text = self._get_text_widget()
        term = self._get_find_term()
        replace_term = self._get_replace_term()

        if text is None or not term:
            self.clear_search_highlight()
            return

        self._refresh_matches(start_index="1.0")
        if not self._matches:
            return

        # Substitui de trás para frente para não deslocar índices ainda não processados
        for start, end in reversed(self._matches):
            text.delete(start, end)
            text.insert(start, replace_term)

        self._refresh_matches(start_index="1.0")
        if self._matches:
            self._current_index = 0
            self._apply_highlight()
            self._focus_current_match()
        else:
            self.clear_search_highlight()

    def clear_search_highlight(self) -> None:
        text = self._get_text_widget()
        if text is None:
            return

        text.tag_remove(self.HIGHLIGHT_TAG, "1.0", "end")
        text.tag_remove(self.CURRENT_TAG, "1.0", "end")
        self._matches.clear()
        self._current_index = -1

    # ------------------------------------------------------------------
    # Internos
    # ------------------------------------------------------------------
    def _get_text_widget(self) -> tk.Text | None:
        return getattr(self.controller, "txt_in", None)

    def _get_find_term(self) -> str:
        var = getattr(self.controller, "find_var", None)
        if var is None:
            return ""
        return (var.get() or "").strip()

    def _get_replace_term(self) -> str:
        var = getattr(self.controller, "replace_var", None)
        if var is None:
            return ""
        return var.get() or ""

    def _match_case_enabled(self) -> bool:
        var = getattr(self.controller, "search_match_case_var", None)
        if var is None:
            return False
        return bool(var.get())

    def _refresh_matches(self, start_index: str) -> None:
        text = self._get_text_widget()
        term = self._get_find_term()

        self.clear_search_highlight()

        if text is None or not term:
            return

        nocase = not self._match_case_enabled()
        index = start_index

        while True:
            pos = text.search(term, index, stopindex="end", nocase=nocase)
            if not pos:
                break

            end = f"{pos}+{len(term)}c"
            self._matches.append((pos, end))
            index = end

        if self._matches:
            self._current_index = 0

    def _apply_highlight(self) -> None:
        text = self._get_text_widget()
        if text is None:
            return

        text.tag_remove(self.HIGHLIGHT_TAG, "1.0", "end")
        text.tag_remove(self.CURRENT_TAG, "1.0", "end")

        for start, end in self._matches:
            text.tag_add(self.HIGHLIGHT_TAG, start, end)

        if 0 <= self._current_index < len(self._matches):
            start, end = self._matches[self._current_index]
            text.tag_add(self.CURRENT_TAG, start, end)

    def _focus_current_match(self) -> None:
        text = self._get_text_widget()
        if text is None:
            return

        if not (0 <= self._current_index < len(self._matches)):
            return

        start, end = self._matches[self._current_index]
        text.mark_set("insert", start)
        text.see(start)

        try:
            text.tag_remove("sel", "1.0", "end")
            text.tag_add("sel", start, end)
        except tk.TclError:
            pass