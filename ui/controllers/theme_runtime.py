from __future__ import annotations


class ThemeRuntime:
    def __init__(self, controller) -> None:
        self.controller = controller

    def apply_theme(self, theme_name: str, *, persist: bool = True) -> None:
        self.controller.theme_name_var.set(theme_name)

        if persist:
            self._persist_theme(theme_name)

        shell = getattr(self.controller, "shell", None)
        if shell is not None:
            shell.rebuild_theme()

    # ------------------------------------------------------------------
    # Internos
    # ------------------------------------------------------------------
    def _persist_theme(self, theme_name: str) -> None:
        config = getattr(self.controller, "config", None)
        if config is None:
            return

        if hasattr(config, "theme_name"):
            config.theme_name = theme_name

        if hasattr(config, "save"):
            config.save()