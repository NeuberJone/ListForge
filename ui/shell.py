from __future__ import annotations

import tkinter as tk

from ui import theme
from ui.controller import TexpadController
from ui.views.editor_view import EditorView
from ui.views.home_view import HomeView
from ui.views.manual_view import ManualView
from ui.views.settings_view import SettingsView
from ui.widgets import set_sidebar_button_active


class SidebarNavButton(tk.Button):
    def __init__(self, parent: tk.Misc, *, icon: str, text: str, command) -> None:
        self.icon = icon
        self.full_text = f"{icon}  {text}"
        super().__init__(
            parent,
            text=self.full_text,
            command=command,
            bg=theme.active_theme().sidebar_bg,
            fg=theme.active_theme().text_muted,
            activebackground=theme.active_theme().sidebar_hover,
            activeforeground=theme.active_theme().text,
            relief="flat",
            bd=0,
            highlightthickness=0,
            anchor="w",
            cursor="hand2",
            font=(theme.FONT_FAMILY, 10),
            padx=12,
            pady=10,
        )

    def apply_collapsed_state(self, collapsed: bool) -> None:
        if collapsed:
            self.configure(text=self.icon, anchor="center", padx=0)
        else:
            self.configure(text=self.full_text, anchor="w", padx=12)


class TexpadShell(tk.Frame):
    EXPANDED_WIDTH = 180
    COLLAPSED_WIDTH = 58

    def __init__(self, parent: tk.Misc) -> None:
        super().__init__(parent, bg=theme.active_theme().app_bg)

        self.parent = parent
        self.controller = TexpadController(parent)

        self.current_screen_key = "home"
        self.nav_buttons: dict[str, SidebarNavButton] = {}
        self.screens: dict[str, tk.Frame] = {}
        self._is_rebuilding = False
        self.sidebar_collapsed = False

        self.grid(row=0, column=0, sticky="nsew")
        self.grid_rowconfigure(0, weight=1)
        self.grid_columnconfigure(1, weight=1)

        self.controller.attach_shell(self)

        self._build_shell()
        self._build_views()

        self._show_screen_internal("home")
        self._tick_clock()

    # ------------------------------------------------------------------
    # Theme rebuild
    # ------------------------------------------------------------------
    def rebuild_theme(self) -> None:
        if self._is_rebuilding:
            return

        self._is_rebuilding = True
        current_screen = self.current_screen_key
        collapsed_state = self.sidebar_collapsed

        try:
            for child in self.winfo_children():
                child.destroy()

            self.configure(bg=theme.active_theme().app_bg)

            self.nav_buttons = {}
            self.screens = {}

            self._build_shell()
            self._build_views()

            self.sidebar_collapsed = collapsed_state
            self._apply_sidebar_state()

        finally:
            self._is_rebuilding = False

        self._show_screen_internal(current_screen)
        self.update_idletasks()

    # ------------------------------------------------------------------
    # Layout
    # ------------------------------------------------------------------
    def _build_shell(self) -> None:
        t = theme.active_theme()

        self.sidebar = tk.Frame(
            self,
            bg=t.sidebar_bg,
            width=self.EXPANDED_WIDTH,
            highlightbackground=t.sidebar_border,
            highlightthickness=1,
            bd=0,
        )
        self.sidebar.grid(row=0, column=0, sticky="nsw")
        self.sidebar.grid_propagate(False)
        self.sidebar.grid_rowconfigure(1, weight=1)

        self.shell = tk.Frame(self, bg=t.app_bg)
        self.shell.grid(row=0, column=1, sticky="nsew")
        self.shell.grid_rowconfigure(1, weight=1)
        self.shell.grid_columnconfigure(0, weight=1)

        self._build_sidebar()
        self._build_topbar()
        self._build_content_host()
        self._build_footer()

    def _build_sidebar(self) -> None:
        t = theme.active_theme()

        brand = tk.Frame(self.sidebar, bg=t.sidebar_bg, height=58)
        brand.grid(row=0, column=0, sticky="ew")
        brand.grid_propagate(False)
        brand.grid_columnconfigure(1, weight=1)

        self.btn_toggle_sidebar = tk.Button(
            brand,
            text="☰",
            command=self.toggle_sidebar,
            bg=t.sidebar_bg,
            fg=t.text,
            activebackground=t.sidebar_hover,
            activeforeground=t.text,
            relief="flat",
            bd=0,
            highlightthickness=0,
            cursor="hand2",
            font=(theme.FONT_FAMILY, 12, "bold"),
            padx=10,
            pady=10,
        )
        self.btn_toggle_sidebar.grid(row=0, column=0, sticky="w", padx=(4, 0), pady=4)

        self.lbl_brand = tk.Label(
            brand,
            text="Texpad",
            bg=t.sidebar_bg,
            fg=t.text,
            font=(theme.FONT_FAMILY, 13, "bold"),
            anchor="w",
        )
        self.lbl_brand.grid(row=0, column=1, sticky="w", padx=(4, 8))

        divider = tk.Frame(self.sidebar, bg=t.sidebar_border, height=1)
        divider.place(relx=0, rely=0, relwidth=1, y=58)

        nav = tk.Frame(self.sidebar, bg=t.sidebar_bg)
        nav.grid(row=1, column=0, sticky="nsew", padx=10, pady=12)
        nav.grid_columnconfigure(0, weight=1)

        items = [
            ("home", "⌂", "Home"),
            ("editor", "✎", "Editor"),
            ("settings", "⚙", "Configurações"),
            ("manual", "📘", "Manual"),
        ]

        for idx, (key, icon, label) in enumerate(items):
            btn = SidebarNavButton(
                nav,
                icon=icon,
                text=label,
                command=lambda k=key: self.show_screen(k),
            )
            btn.grid(row=idx, column=0, sticky="ew", pady=(0, 8))
            self.nav_buttons[key] = btn

        self._apply_sidebar_state()

    def _build_topbar(self) -> None:
        t = theme.active_theme()

        self.topbar = tk.Frame(
            self.shell,
            bg=t.topbar_bg,
            height=theme.TOPBAR_HEIGHT,
            highlightbackground=t.sidebar_border,
            highlightthickness=1,
            bd=0,
        )
        self.topbar.grid(row=0, column=0, sticky="ew")
        self.topbar.grid_propagate(False)
        self.topbar.grid_columnconfigure(1, weight=1)

        left = tk.Frame(self.topbar, bg=t.topbar_bg)
        left.grid(row=0, column=0, sticky="w", padx=18)

        tk.Label(
            left,
            textvariable=self.controller.page_title_var,
            bg=t.topbar_bg,
            fg=t.text,
            font=(theme.FONT_FAMILY, 13, "bold"),
        ).pack(side="left", pady=14)

        right = tk.Frame(self.topbar, bg=t.topbar_bg)
        right.grid(row=0, column=1, sticky="e", padx=16)

        tk.Label(
            right,
            textvariable=self.controller.clock_var,
            bg=t.topbar_bg,
            fg=t.text_muted,
            font=(theme.FONT_FAMILY, 9),
        ).pack(side="left", pady=14)

    def _build_content_host(self) -> None:
        t = theme.active_theme()

        body = tk.Frame(self.shell, bg=t.app_bg)
        body.grid(row=1, column=0, sticky="nsew")
        body.grid_rowconfigure(0, weight=1)
        body.grid_columnconfigure(0, weight=1)

        self.content_host = tk.Frame(body, bg=t.app_bg)
        self.content_host.grid(row=0, column=0, sticky="nsew", padx=12, pady=12)
        self.content_host.grid_rowconfigure(0, weight=1)
        self.content_host.grid_columnconfigure(0, weight=1)

    def _build_footer(self) -> None:
        t = theme.active_theme()

        footer = tk.Frame(
            self.shell,
            bg=t.topbar_bg,
            height=theme.FOOTER_HEIGHT,
            highlightbackground=t.sidebar_border,
            highlightthickness=1,
            bd=0,
        )
        footer.grid(row=2, column=0, sticky="ew")
        footer.grid_propagate(False)

        tk.Label(
            footer,
            textvariable=self.controller.status_var,
            bg=t.topbar_bg,
            fg=t.text_muted,
            font=(theme.FONT_FAMILY, 9),
            anchor="w",
        ).pack(fill="both", padx=14)

    # ------------------------------------------------------------------
    # Sidebar state
    # ------------------------------------------------------------------
    def toggle_sidebar(self) -> None:
        self.sidebar_collapsed = not self.sidebar_collapsed
        self._apply_sidebar_state()

    def _apply_sidebar_state(self) -> None:
        width = self.COLLAPSED_WIDTH if self.sidebar_collapsed else self.EXPANDED_WIDTH
        self.sidebar.configure(width=width)

        if self.sidebar_collapsed:
            self.lbl_brand.grid_remove()
        else:
            self.lbl_brand.grid()

        for btn in self.nav_buttons.values():
            btn.apply_collapsed_state(self.sidebar_collapsed)

        self.update_idletasks()

    # ------------------------------------------------------------------
    # Views
    # ------------------------------------------------------------------
    def _build_views(self) -> None:
        home_view = HomeView(self.content_host, self.controller)
        editor_view = EditorView(self.content_host, self.controller)
        settings_view = SettingsView(self.content_host, self.controller)
        manual_view = ManualView(self.content_host, self.controller)

        home_view.grid(row=0, column=0, sticky="nsew")
        editor_view.grid(row=0, column=0, sticky="nsew")
        settings_view.grid(row=0, column=0, sticky="nsew")
        manual_view.grid(row=0, column=0, sticky="nsew")

        settings_view.bind_runtime_widgets()

        self.screens = {
            "home": home_view,
            "editor": editor_view,
            "settings": settings_view,
            "manual": manual_view,
        }

        self.controller.attach_views(
            home_view=home_view,
            editor_view=editor_view,
            settings_view=settings_view,
        )

    # ------------------------------------------------------------------
    # Navigation
    # ------------------------------------------------------------------
    def show_screen(self, key: str) -> None:
        if key not in self.screens:
            return
        self._show_screen_internal(key)

    def _show_screen_internal(self, key: str) -> None:
        for screen in self.screens.values():
            screen.grid_remove()

        self.screens[key].grid()
        self.current_screen_key = key

        self.controller.update_top_action_for_screen(key)
        self._update_nav_state()

        screen = self.screens[key]
        if hasattr(screen, "refresh"):
            try:
                screen.refresh()
            except Exception:
                pass

    def _update_nav_state(self) -> None:
        for key, btn in self.nav_buttons.items():
            set_sidebar_button_active(btn, key == self.current_screen_key)

    # ------------------------------------------------------------------
    # Clock
    # ------------------------------------------------------------------
    def _tick_clock(self) -> None:
        from datetime import datetime

        self.controller.clock_var.set(datetime.now().strftime("%d/%m/%Y %H:%M:%S"))
        self.after(1000, self._tick_clock)


def run_app() -> None:
    root = tk.Tk()
    theme.configure_root(root)
    theme.apply_ttk_theme()
    TexpadShell(root)
    root.mainloop()