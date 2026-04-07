from __future__ import annotations

import json
import os
import shutil
from datetime import datetime
from pathlib import Path


APP_NAME = "Texpad"

APP_DIR = Path(os.environ.get("APPDATA") or str(Path.home())) / APP_NAME
APP_DIR.mkdir(parents=True, exist_ok=True)

CONFIG_PATH = APP_DIR / "config.json"
BACKUP_DIR = APP_DIR / "backups"
BACKUP_DIR.mkdir(parents=True, exist_ok=True)

DEFAULT_CONFIG = {
    "show_json_tab": False,
    "show_generate_json_button": False,
    "show_copy_json_button": False,
    "use_default_output_dir": False,
    "output_dir": "",
    "use_default_list_name": False,
    "default_list_name": "lista",
    "default_case_mode": "original",
    "default_input_separator": ",",
    "theme_name": "Texpad Dark",
    "last_opened_file": "",
}


def load_config() -> dict:
    if not CONFIG_PATH.exists():
        return dict(DEFAULT_CONFIG)

    try:
        data = json.loads(CONFIG_PATH.read_text(encoding="utf-8"))
        if not isinstance(data, dict):
            return dict(DEFAULT_CONFIG)
        return {**DEFAULT_CONFIG, **data}
    except Exception:
        return dict(DEFAULT_CONFIG)


def save_config(cfg: dict) -> None:
    merged = {**DEFAULT_CONFIG, **cfg}
    CONFIG_PATH.write_text(
        json.dumps(merged, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )


def reset_config() -> dict:
    cfg = dict(DEFAULT_CONFIG)
    save_config(cfg)
    return cfg


def create_backup(source_file: str | Path) -> Path:
    source = Path(source_file)
    if not source.exists():
        raise FileNotFoundError(f"Arquivo não encontrado para backup: {source}")

    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    backup_name = f"{source.stem}_{timestamp}{source.suffix}"
    backup_path = BACKUP_DIR / backup_name

    shutil.copy2(source, backup_path)
    return backup_path