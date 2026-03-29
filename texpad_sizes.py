from __future__ import annotations

import json
import re
from pathlib import Path
from typing import Any, Dict, List, Tuple

from texpad_config import APP_DIR


SIZE_CONFIG_PATH = APP_DIR / "sizes.json"

QTY_SIZE_RE = re.compile(r"^\s*(\d+)\s*-\s*([A-Za-z0-9]+)\s*$", re.IGNORECASE)

GROUP_MALE = "male"
GROUP_FEMALE = "female"
GROUP_CHILD = "child"

GROUP_LABELS = {
    GROUP_MALE: "Masculino",
    GROUP_FEMALE: "Feminino",
    GROUP_CHILD: "Infantil",
}


def default_size_config() -> Dict[str, Any]:
    return {
        "groups": {
            GROUP_MALE: {
                "label": GROUP_LABELS[GROUP_MALE],
                "base_sizes": ["PP", "P", "M", "G", "GG", "XG", "XGG", "XXGG", "XLGG"],
                "prefixes": [],
                "suffixes": [],
            },
            GROUP_FEMALE: {
                "label": GROUP_LABELS[GROUP_FEMALE],
                "base_sizes": ["PP", "P", "M", "G", "GG", "XG", "XGG", "XXGG"],
                "prefixes": ["BL"],
                "suffixes": [],
            },
            GROUP_CHILD: {
                "label": GROUP_LABELS[GROUP_CHILD],
                "base_sizes": ["2", "4", "6", "8", "10", "12", "14", "16"],
                "prefixes": [],
                "suffixes": ["A"],
            },
        }
    }


def _normalize_token(value: str) -> str:
    text = (value or "").strip().upper()
    return re.sub(r"\s+", "", text)


def _dedupe_keep_order(values: List[str]) -> List[str]:
    seen = set()
    out: List[str] = []
    for value in values:
        normalized = _normalize_token(value)
        if not normalized or normalized in seen:
            continue
        seen.add(normalized)
        out.append(normalized)
    return out


def _normalize_group(raw_group: Dict[str, Any], fallback_label: str) -> Dict[str, Any]:
    base_sizes = _dedupe_keep_order([str(v) for v in raw_group.get("base_sizes", [])])
    prefixes = _dedupe_keep_order([str(v) for v in raw_group.get("prefixes", [])])
    suffixes = _dedupe_keep_order([str(v) for v in raw_group.get("suffixes", [])])

    return {
        "label": str(raw_group.get("label", fallback_label)).strip() or fallback_label,
        "base_sizes": base_sizes,
        "prefixes": prefixes,
        "suffixes": suffixes,
    }


def normalize_size_config(config: Dict[str, Any]) -> Dict[str, Any]:
    default_cfg = default_size_config()
    raw_groups = config.get("groups", {}) if isinstance(config, dict) else {}

    groups: Dict[str, Dict[str, Any]] = {}
    for group_key, default_group in default_cfg["groups"].items():
        raw_group = raw_groups.get(group_key, {}) if isinstance(raw_groups, dict) else {}
        if not isinstance(raw_group, dict):
            raw_group = {}

        merged = {
            "label": raw_group.get("label", default_group["label"]),
            "base_sizes": raw_group.get("base_sizes", default_group["base_sizes"]),
            "prefixes": raw_group.get("prefixes", default_group["prefixes"]),
            "suffixes": raw_group.get("suffixes", default_group["suffixes"]),
        }
        groups[group_key] = _normalize_group(merged, default_group["label"])

    return {"groups": groups}


def load_size_config() -> Dict[str, Any]:
    if not SIZE_CONFIG_PATH.exists():
        cfg = default_size_config()
        save_size_config(cfg)
        return normalize_size_config(cfg)

    try:
        raw = json.loads(SIZE_CONFIG_PATH.read_text(encoding="utf-8"))
        cfg = normalize_size_config(raw if isinstance(raw, dict) else {})
        save_size_config(cfg)
        return cfg
    except Exception:
        cfg = default_size_config()
        save_size_config(cfg)
        return normalize_size_config(cfg)


def save_size_config(config: Dict[str, Any]) -> None:
    normalized = normalize_size_config(config)
    SIZE_CONFIG_PATH.write_text(
        json.dumps(normalized, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )


def reset_size_config() -> Dict[str, Any]:
    cfg = default_size_config()
    save_size_config(cfg)
    return normalize_size_config(cfg)


def parse_csv_tokens(text: str) -> List[str]:
    if not (text or "").strip():
        return []
    parts = [part.strip() for part in str(text).split(",")]
    return _dedupe_keep_order(parts)


def tokens_to_csv(values: List[str]) -> str:
    return ", ".join(_dedupe_keep_order(values))


def build_group_sizes(group: Dict[str, Any]) -> List[str]:
    base_sizes = _dedupe_keep_order([str(v) for v in group.get("base_sizes", [])])
    prefixes = _dedupe_keep_order([str(v) for v in group.get("prefixes", [])])
    suffixes = _dedupe_keep_order([str(v) for v in group.get("suffixes", [])])

    prefix_options = prefixes if prefixes else [""]
    suffix_options = suffixes if suffixes else [""]

    sizes: List[str] = []
    seen = set()

    for prefix in prefix_options:
        for base in base_sizes:
            for suffix in suffix_options:
                size = f"{prefix}{base}{suffix}"
                if size and size not in seen:
                    seen.add(size)
                    sizes.append(size)

    return sizes


def build_size_index(config: Dict[str, Any]) -> Dict[str, str]:
    cfg = normalize_size_config(config)
    index: Dict[str, str] = {}

    for group_key, group in cfg["groups"].items():
        for size in build_group_sizes(group):
            index[size] = group_key

    return index


def get_valid_sizes(config: Dict[str, Any]) -> List[str]:
    index = build_size_index(config)
    return list(index.keys())


def is_valid_size(token: str, config: Dict[str, Any]) -> bool:
    text = _normalize_token(token)
    if not text:
        return False
    return text in build_size_index(config)


def parse_qty_and_size(token: str, config: Dict[str, Any]) -> Tuple[int, str]:
    text = (token or "").strip()
    if not text:
        raise ValueError("Tamanho vazio (não permitido).")

    match = QTY_SIZE_RE.match(text)
    if match:
        qty = int(match.group(1))
        size = _normalize_token(match.group(2))
        if qty <= 0:
            raise ValueError("Quantidade inválida (<= 0).")
        if not is_valid_size(size, config):
            raise ValueError(f"Tamanho inválido: {size}")
        return qty, size

    size = _normalize_token(text)
    if not is_valid_size(size, config):
        raise ValueError(f"Tamanho inválido: {size}")
    return 1, size


def normalize_size_token(token: str, config: Dict[str, Any]) -> str:
    qty, size = parse_qty_and_size(token, config)
    return f"{qty}-{size}"


def size_group_of(size: str, config: Dict[str, Any]) -> str:
    normalized = _normalize_token(size)
    index = build_size_index(config)
    group = index.get(normalized)
    if not group:
        raise ValueError(f"Tamanho inválido: {normalized}")
    return group


def gender_from_size(size: str, config: Dict[str, Any]) -> str:
    group = size_group_of(size, config)

    if group == GROUP_CHILD:
        return "C"
    if group == GROUP_FEMALE:
        return "FE"
    return "MA"


def format_size_token(token: str, config: Dict[str, Any]) -> str:
    qty, size = parse_qty_and_size(token, config)
    return size if qty == 1 else f"{qty}-{size}"


def update_group_config(
    config: Dict[str, Any],
    group_key: str,
    *,
    base_sizes: List[str],
    prefixes: List[str],
    suffixes: List[str],
    label: str | None = None,
) -> Dict[str, Any]:
    cfg = normalize_size_config(config)

    if group_key not in cfg["groups"]:
        raise ValueError(f"Grupo de tamanhos inválido: {group_key}")

    current = cfg["groups"][group_key]
    cfg["groups"][group_key] = {
        "label": (label or current["label"]).strip() or current["label"],
        "base_sizes": _dedupe_keep_order(base_sizes),
        "prefixes": _dedupe_keep_order(prefixes),
        "suffixes": _dedupe_keep_order(suffixes),
    }
    return cfg