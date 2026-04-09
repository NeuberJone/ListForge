from __future__ import annotations


class HomeRuntime:
    def __init__(self, controller) -> None:
        self.controller = controller

    # ------------------------------------------------------------------
    # API pública
    # ------------------------------------------------------------------
    def refresh_home_dashboard(self) -> None:
        self._ensure_home_vars()

        input_text = self._get_text_content("txt_in")
        output_text = self._get_text_content("txt_out")
        json_text = self._get_text_content("txt_json")

        input_lines = self._count_non_empty_lines(input_text)
        output_lines = self._count_non_empty_lines(output_text)

        json_enabled = self._json_enabled()
        json_has_content = bool(json_text.strip())

        self.controller.home_input_lines_var.set(str(input_lines))
        self.controller.home_output_lines_var.set(str(output_lines))
        self.controller.home_json_status_var.set(
            "Disponível" if json_enabled and json_has_content else
            "Habilitado" if json_enabled else
            "Oculto"
        )

        self.controller.home_separator_var.set(self._get_separator_label())
        self.controller.home_case_mode_var.set(self._get_case_mode_label())
        self.controller.home_alert_var.set(self._build_alert_text())
        self.controller.home_summary_var.set(
            self._build_summary_text(
                input_lines=input_lines,
                output_lines=output_lines,
                json_enabled=json_enabled,
                json_has_content=json_has_content,
            )
        )

    # ------------------------------------------------------------------
    # Internos
    # ------------------------------------------------------------------
    def _ensure_home_vars(self) -> None:
        import tkinter as tk

        if not hasattr(self.controller, "home_input_lines_var"):
            self.controller.home_input_lines_var = tk.StringVar(value="0")

        if not hasattr(self.controller, "home_output_lines_var"):
            self.controller.home_output_lines_var = tk.StringVar(value="0")

        if not hasattr(self.controller, "home_json_status_var"):
            self.controller.home_json_status_var = tk.StringVar(value="Oculto")

        if not hasattr(self.controller, "home_separator_var"):
            self.controller.home_separator_var = tk.StringVar(value=",")

        if not hasattr(self.controller, "home_case_mode_var"):
            self.controller.home_case_mode_var = tk.StringVar(value="-")

        if not hasattr(self.controller, "home_alert_var"):
            self.controller.home_alert_var = tk.StringVar(
                value="Nenhum alerta importante no momento."
            )

        if not hasattr(self.controller, "home_summary_var"):
            self.controller.home_summary_var = tk.StringVar(
                value="Nenhum conteúdo carregado ainda."
            )

    def _get_text_content(self, attr_name: str) -> str:
        widget = getattr(self.controller, attr_name, None)
        if widget is None:
            return ""

        try:
            return widget.get("1.0", "end-1c")
        except Exception:
            return ""

    def _count_non_empty_lines(self, text: str) -> int:
        return sum(1 for line in text.splitlines() if line.strip())

    def _json_enabled(self) -> bool:
        var = getattr(self.controller, "show_json_tab_var", None)
        if var is None:
            return False
        try:
            return bool(var.get())
        except Exception:
            return False

    def _get_separator_label(self) -> str:
        var = getattr(self.controller, "editor_separator_var", None)
        if var is None:
            return ","

        try:
            raw = var.get()
        except Exception:
            return ","

        if raw == "\t":
            return r"\t (tab)"
        if raw == "":
            return "(vazio)"
        return raw

    def _get_case_mode_label(self) -> str:
        candidates = [
            "editor_case_label_var",
            "default_case_label_var",
        ]

        for attr in candidates:
            var = getattr(self.controller, attr, None)
            if var is None:
                continue
            try:
                value = var.get()
            except Exception:
                continue
            if value:
                return value

        return "-"

    def _build_alert_text(self) -> str:
        alerts: list[str] = []

        input_text = self._get_text_content("txt_in")
        output_text = self._get_text_content("txt_out")

        if not input_text.strip():
            alerts.append("A entrada está vazia.")
        if input_text.strip() and not output_text.strip():
            alerts.append("Há conteúdo na entrada, mas a saída ainda não foi gerada.")

        if not self._json_enabled():
            alerts.append("A área de JSON está oculta nas configurações.")

        if not alerts:
            return "Nenhum alerta importante no momento."

        return "\n".join(f"• {item}" for item in alerts)

    def _build_summary_text(
        self,
        *,
        input_lines: int,
        output_lines: int,
        json_enabled: bool,
        json_has_content: bool,
    ) -> str:
        parts: list[str] = []

        if input_lines == 0:
            parts.append("Ainda não há linhas válidas na entrada.")
        else:
            parts.append(f"A entrada possui {input_lines} linha(s) preenchida(s).")

        if output_lines == 0:
            parts.append("A saída organizada ainda não foi gerada.")
        else:
            parts.append(f"A saída possui {output_lines} linha(s) no momento.")

        if json_enabled:
            if json_has_content:
                parts.append("A prévia JSON está disponível.")
            else:
                parts.append("A área JSON está habilitada, mas ainda sem conteúdo.")
        else:
            parts.append("A área JSON está oculta.")

        return " ".join(parts)