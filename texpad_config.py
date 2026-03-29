from __future__ import annotations

import json
from pathlib import Path
from typing import Any, Dict, Optional


APP_NAME = "Texpad"


def _documents_dir() -> Path:
    home = Path.home()
    docs = home / "Documents"
    return docs if docs.exists() else home


APP_DIR = _documents_dir() / APP_NAME
APP_DIR.mkdir(parents=True, exist_ok=True)

BACKUP_DIR = APP_DIR / "backups"
BACKUP_DIR.mkdir(parents=True, exist_ok=True)

DEFAULT_OUTPUT_DIR = APP_DIR / "output"
DEFAULT_OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

CONFIG_PATH = APP_DIR / "config.json"

DEFAULT_CONFIG: Dict[str, Any] = {
    "show_json_tab": True,
    "show_generate_json_button": True,
    "show_copy_json_button": True,
    "use_default_output_dir": True,
    "output_dir": str(DEFAULT_OUTPUT_DIR),
    "use_default_list_name": True,
    "default_list_name": "lista",
    "default_case_mode": "original",        # original | upper | lower
    "default_input_separator": ",",         # aceita "," ";" "|" "\\t"
    "last_opened_file": "",
}


def ensure_dir(path: str | Path) -> Path:
    p = Path(path)
    p.mkdir(parents=True, exist_ok=True)
    return p


def load_config() -> Dict[str, Any]:
    if not CONFIG_PATH.exists():
        return dict(DEFAULT_CONFIG)

    try:
        raw = json.loads(CONFIG_PATH.read_text(encoding="utf-8"))
        if isinstance(raw, dict):
            cfg = {**DEFAULT_CONFIG, **raw}
            ensure_dir(cfg.get("output_dir", DEFAULT_OUTPUT_DIR))
            return cfg
    except Exception:
        pass

    return dict(DEFAULT_CONFIG)


def save_config(cfg: Dict[str, Any]) -> None:
    safe_cfg = {**DEFAULT_CONFIG, **cfg}
    ensure_dir(safe_cfg.get("output_dir", DEFAULT_OUTPUT_DIR))
    CONFIG_PATH.write_text(
        json.dumps(safe_cfg, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )


def reset_config() -> Dict[str, Any]:
    cfg = dict(DEFAULT_CONFIG)
    save_config(cfg)
    return cfg


def create_backup(path: Path) -> Optional[Path]:
    if not path.exists():
        return None

    from datetime import datetime

    stamp = datetime.now().strftime("%Y%m%d-%H%M%S")
    backup_name = f"{path.stem}__backup__{stamp}{path.suffix}"
    backup_path = BACKUP_DIR / backup_name
    backup_path.write_bytes(path.read_bytes())
    return backup_path