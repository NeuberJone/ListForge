from __future__ import annotations

import tkinter as tk

from ui import theme
from ui.views.editor_view import EditorView
from ui.views.manual_view import ManualView
from ui.views.settings_view import SettingsView
from ui.widgets import make_sidebar_button, set_sidebar_button_active


class TexpadShell(tk.Frame):
    def __init__(self, parent: tk.Misc, controller) -> None:
        super().__init__(parent, bg=theme.active_theme().app_bg)
        self.controller = controller
        self.controller.shell = self

        self.current_screen = "editor"
        self.screens: dict[str, tk.Frame] = {}
        self.sidebar_buttons: dict[str, tk.Button] = {}

        self.grid_rowconfigure(1, weight=1)
        self.grid_columnconfigure(1, weight=1)

        self._build_layout()
        self._build_sidebar()
        self._build_topbar()
        self._build_views()
        self.show_screen("editor")

    # ------------------------------------------------------------------
    # Estrutura principal
    # ------------------------------------------------------------------
    def _build_layout(self) -> None:
        t = theme.active_theme()

        self.sidebar = tk.Frame(
            self,
            bg=t.sidebar_bg,
            width=theme.SIDEBAR_WIDTH,
            highlightbackground=t.sidebar_border,
            highlightthickness=1,
            bd=0,
        )
        self.sidebar.grid(row=0, column=0, rowspan=2, sticky="nsw")
        self.sidebar.grid_propagate(False)

        self.topbar = tk.Frame(
            self,
            bg=t.topbar_bg,
            height=theme.TOPBAR_HEIGHT,
            highlightbackground=t.border,
            highlightthickness=1,
            bd=0,
        )
        self.topbar.grid(row=0, column=1, sticky="ew")
        self.topbar.grid_propagate(False)

        self.content_host = tk.Frame(self, bg=t.app_bg)
        self.content_host.grid(row=1, column=1, sticky="nsew")
        self.content_host.grid_rowconfigure(0, weight=1)
        self.content_host.grid_columnconfigure(0, weight=1)

    # ------------------------------------------------------------------
    # Sidebar
    # ------------------------------------------------------------------
    def _build_sidebar(self) -> None:
        t = theme.active_theme()

        header = tk.Frame(self.sidebar, bg=t.sidebar_bg)
        header.pack(fill="x", padx=12, pady=(14, 18))

        tk.Label(
            header,
            text="Texpad",
            bg=t.sidebar_bg,
            fg=t.text,
            font=(theme.FONT_FAMILY, 14, "bold"),
            anchor="w",
        ).pack(fill="x")

        tk.Label(
            header,
            text="Organização e transformação de listas",
            bg=t.sidebar_bg,
            fg=t.text_muted,
            font=(theme.FONT_FAMILY, 9),
            justify="left",
            anchor="w",
            wraplength=theme.SIDEBAR_WIDTH - 28,
        ).pack(fill="x", pady=(4, 0))

        nav = tk.Frame(self.sidebar, bg=t.sidebar_bg)
        nav.pack(fill="x", padx=10)

        items = [
            ("editor", "Editor"),
            ("settings", "Configurações"),
            ("manual", "Manual"),
        ]

        self.sidebar_buttons.clear()

        for key, label in items:
            btn = make_sidebar_button(
                nav,
                text=label,
                command=lambda section=key: self.show_screen(section),
            )
            btn.pack(fill="x", pady=3)
            self.sidebar_buttons[key] = btn

        footer = tk.Frame(self.sidebar, bg=t.sidebar_bg)
        footer.pack(side="bottom", fill="x", padx=12, pady=12)

        tk.Label(
            footer,
            text="Tema ativo",
            bg=t.sidebar_bg,
            fg=t.text_muted,
            font=(theme.FONT_FAMILY, 9),
            anchor="w",
        ).pack(fill="x")

        tk.Label(
            footer,
            textvariable=self.controller.theme_name_var,
            bg=t.sidebar_bg,
            fg=t.text,
            font=(theme.FONT_FAMILY, 10, "bold"),
            anchor="w",
        ).pack(fill="x", pady=(2, 0))

    # ------------------------------------------------------------------
    # Topbar
    # ------------------------------------------------------------------
    def _build_topbar(self) -> None:
        t = theme.active_theme()

        self.topbar_left = tk.Frame(self.topbar, bg=t.topbar_bg)
        self.topbar_left.pack(side="left", fill="y", padx=14)

        self.title_var = tk.StringVar(value="Editor")

        tk.Label(
            self.topbar_left,
            textvariable=self.title_var,
            bg=t.topbar_bg,
            fg=t.text,
            font=(theme.FONT_FAMILY, 13, "bold"),
            anchor="w",
        ).pack(side="left", fill="y")

    # ------------------------------------------------------------------
    # Views
    # ------------------------------------------------------------------
    def _build_views(self) -> None:
        editor_view = EditorView(self.content_host, self.controller)
        settings_view = SettingsView(self.content_host, self.controller)
        manual_view = ManualView(self.content_host, self.controller)

        editor_view.grid(row=0, column=0, sticky="nsew")
        settings_view.grid(row=0, column=0, sticky="nsew")
        manual_view.grid(row=0, column=0, sticky="nsew")

        self.screens = {
            "editor": editor_view,
            "settings": settings_view,
            "manual": manual_view,
        }

        self.controller.attach_views(
            editor_view=editor_view,
            settings_view=settings_view,
        )

        settings_view.bind_runtime_widgets()
        editor_view.apply_runtime_preferences()
        self.controller.update_settings_field_states()

    # ------------------------------------------------------------------
    # Navegação
    # ------------------------------------------------------------------
    def show_screen(self, screen_key: str) -> None:
        if screen_key not in self.screens:
            return

        self.current_screen = screen_key
        self.screens[screen_key].tkraise()

        titles = {
            "editor": "Editor",
            "settings": "Configurações",
            "manual": "Manual",
        }
        self.title_var.set(titles.get(screen_key, "Texpad"))

        for key, button in self.sidebar_buttons.items():
            set_sidebar_button_active(button, key == screen_key)

    # ------------------------------------------------------------------
    # Tema
    # ------------------------------------------------------------------
    def rebuild_theme(self) -> None:
        current_screen = self.current_screen

        self.sidebar.destroy()
        self.topbar.destroy()
        self.content_host.destroy()

        self._build_layout()
        self._build_sidebar()
        self._build_topbar()
        self._build_views()

        if current_screen in self.screens:
            self.show_screen(current_screen)
        else:
            self.show_screen("editor")


def run_app() -> tuple[tk.Tk, TexpadShell]:
    from ui.controller import TexpadController

    root = tk.Tk()
    controller = TexpadController(root)

    theme.configure_root(root, controller.theme_name_var.get())
    theme.apply_ttk_theme(theme_name=controller.theme_name_var.get())

    shell = TexpadShell(root, controller)
    shell.pack(fill="both", expand=True)

    root.mainloop()
    return root, shell