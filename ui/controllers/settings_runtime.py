from __future__ import annotations


class SettingsRuntime:
    def __init__(self, controller) -> None:
        self.controller = controller

        self.ent_output_dir = None
        self.btn_pick_output_dir = None
        self.ent_default_name = None

    # ------------------------------------------------------------------
    # Bind dos widgets
    # ------------------------------------------------------------------
    def bind_settings_widgets(
        self,
        *,
        ent_output_dir=None,
        btn_pick_output_dir=None,
        ent_default_name=None,
    ) -> None:
        self.ent_output_dir = ent_output_dir
        self.btn_pick_output_dir = btn_pick_output_dir
        self.ent_default_name = ent_default_name

        self.update_settings_field_states()

    # ------------------------------------------------------------------
    # Estado visual dos campos
    # ------------------------------------------------------------------
    def update_settings_field_states(self) -> None:
        self._update_output_dir_state()
        self._update_default_name_state()

    def _update_output_dir_state(self) -> None:
        enabled = not self._bool_var("use_default_output_dir_var")

        if self.ent_output_dir is not None:
            try:
                self.ent_output_dir.configure(state="normal" if enabled else "disabled")
            except Exception:
                pass

        if self.btn_pick_output_dir is not None:
            try:
                self.btn_pick_output_dir.configure(state="normal" if enabled else "disabled")
            except Exception:
                pass

    def _update_default_name_state(self) -> None:
        enabled = not self._bool_var("use_default_list_name_var")

        if self.ent_default_name is not None:
            try:
                self.ent_default_name.configure(state="normal" if enabled else "disabled")
            except Exception:
                pass

    # ------------------------------------------------------------------
    # Helpers
    # ------------------------------------------------------------------
    def _bool_var(self, attr_name: str) -> bool:
        var = getattr(self.controller, attr_name, None)
        if var is None:
            return False

        try:
            return bool(var.get())
        except Exception:
            return False