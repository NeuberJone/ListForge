from __future__ import annotations

import json
import re
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Any, List

from texpad_sizes import (
    format_size_token,
    gender_from_size,
    is_valid_size,
    parse_qty_and_size,
    size_group_of,
)


BASE_JSON = {
    "title": "List",
    "order_number": 0,
    "client_name": "",
    "orders": [],
    "unique_name_chars": "",
    "unique_nickname_chars": "",
}

JSON_IMPORT_FIELD_ORDER = [
    "Name",
    "Number",
    "ShortSleeve",
    "LongSleeve",
    "Short",
    "Pants",
    "Tanktop",
    "Vest",
    "Nickname",
    "BloodType",
]
JSON_IMPORT_MANDATORY_FIELDS = {"Name", "Number"}

GROUP_RENDER_ORDER = ("male", "female", "child")


@dataclass(frozen=True)
class ParsedRow:
    name: str
    number: str
    tams: tuple[str, ...]
    s2: str
    s3: str


def normalize_separator(value: str) -> str:
    raw = (value or "").strip()
    if raw in {"\\t", "TAB", "tab"}:
        return "\t"
    return raw or ","


def separator_label(value: str) -> str:
    sep = normalize_separator(value)
    if sep == "\t":
        return "\\t"
    return sep


def sanitize_base_filename(name: str) -> str:
    text = (name or "").strip()
    if not text:
        text = datetime.now().strftime("lista-%Y%m%d-%H%M%S")
    bad = r'\/:*?"<>|'
    for ch in bad:
        text = text.replace(ch, "_")
    text = re.sub(r"\s+", " ", text).strip(" .")
    return text or datetime.now().strftime("lista-%Y%m%d-%H%M%S")


def versioned_path(directory: Path, base_name: str, suffix: str) -> Path:
    safe_base = sanitize_base_filename(base_name)
    path = directory / f"{safe_base}{suffix}"
    if not path.exists():
        return path

    idx = 2
    while True:
        candidate = directory / f"{safe_base}_v{idx}{suffix}"
        if not candidate.exists():
            return candidate
        idx += 1


def _clean_token(value: str) -> str:
    return (value or "").strip()


def _is_number(token: str) -> bool:
    return _clean_token(token).isdigit()


def _is_size(token: str, size_config: dict[str, Any]) -> bool:
    text = _clean_token(token)
    if not text:
        return False

    try:
        parse_qty_and_size(text, size_config)
        return True
    except Exception:
        return is_valid_size(text, size_config)


def apply_case_mode(text: str, case_mode: str) -> str:
    value = text or ""
    if case_mode == "upper":
        return value.upper()
    if case_mode == "lower":
        return value.lower()
    return value


def clean_text_by_separator(text: str, separator: str) -> str:
    sep = normalize_separator(separator)
    if not sep:
        raise ValueError("Separador inválido.")

    cleaned_lines: List[str] = []
    for line in text.splitlines():
        if not line.strip():
            cleaned_lines.append("")
            continue

        stripped = line.lstrip()
        parts = [part.strip() for part in stripped.split(sep)]
        cleaned_lines.append(sep.join(parts))

    return "\n".join(cleaned_lines)


def _normalize_json_import_value(value: Any) -> str:
    if value is None:
        return ""
    return str(value).replace("\r", "").replace("\n", " ").strip()


def _decide_effective_json_import_fields(orders: list[dict[str, Any]]) -> list[str]:
    present: set[str] = set()

    for entry in orders:
        for key in JSON_IMPORT_FIELD_ORDER:
            if key in JSON_IMPORT_MANDATORY_FIELDS:
                continue
            if _normalize_json_import_value(entry.get(key, "")):
                present.add(key)

    return [
        key
        for key in JSON_IMPORT_FIELD_ORDER
        if key in JSON_IMPORT_MANDATORY_FIELDS or key in present
    ]


def extract_list_text_from_json_data(data: Any, *, output_separator: str = ",") -> str:
    if isinstance(data, list):
        orders = data
    elif isinstance(data, dict):
        orders = data.get("orders", [])
    else:
        raise ValueError("A resposta precisa ser um objeto com 'orders' ou uma lista de pedidos.")

    if not isinstance(orders, list):
        raise ValueError("Campo 'orders' inválido (não é lista).")

    effective_fields = _decide_effective_json_import_fields(orders)
    lines: list[str] = []

    for entry in orders:
        if not isinstance(entry, dict):
            raise ValueError("Cada item de 'orders' precisa ser um objeto JSON.")

        row_values = [
            _normalize_json_import_value(entry.get(field, ""))
            for field in effective_fields
        ]

        expanded_rows: list[str] = []

        for idx, value in enumerate(row_values):
            match = re.fullmatch(r"(\d+)-(.+)", value)
            if match:
                qty = int(match.group(1))
                base_value = match.group(2).strip()

                for _ in range(qty):
                    new_row = row_values.copy()
                    new_row[idx] = base_value
                    expanded_rows.append(output_separator.join(new_row))
                break
        else:
            expanded_rows.append(output_separator.join(row_values))

        lines.extend(expanded_rows)

    return "\n".join(lines)


def parse_line(
    line: str,
    *,
    input_separator: str = ",",
    size_config: dict[str, Any],
) -> ParsedRow | None:
    raw = line.strip()
    if not raw:
        return None

    sep = normalize_separator(input_separator)
    parts = [_clean_token(part) for part in raw.split(sep)]

    name = ""
    number = ""
    tams: List[str] = []
    extra_strings: List[str] = []

    for token in parts:
        value = _clean_token(token)
        if not value:
            continue

        if _is_size(value, size_config):
            tams.append(value)
            continue

        if _is_number(value) and not number:
            number = value
            continue

        if not name:
            name = value
        else:
            extra_strings.append(value)

    if not tams:
        raise ValueError(f"Sem TAM reconhecido: {raw}")

    if len(tams) > 4:
        raise ValueError(f"Mais de 4 TAMs na linha: {raw}")

    s2 = extra_strings[0] if len(extra_strings) >= 1 else ""
    s3 = extra_strings[1] if len(extra_strings) >= 2 else ""

    return ParsedRow(
        name=name,
        number=number,
        tams=tuple(tams),
        s2=s2,
        s3=s3,
    )


def process_text(
    text: str,
    *,
    input_separator: str = ",",
    size_config: dict[str, Any],
) -> List[ParsedRow]:
    parsed: List[ParsedRow] = []

    for line_no, line in enumerate(text.splitlines(), start=1):
        if not line.strip():
            continue
        try:
            row = parse_line(
                line,
                input_separator=input_separator,
                size_config=size_config,
            )
            if row:
                parsed.append(row)
        except ValueError as exc:
            raise ValueError(f"Linha {line_no}: {exc}") from None

    parsed.sort(key=lambda row: (row.name.casefold(), row.number))
    return parsed


def _explode_row_fragments(
    row: ParsedRow,
    *,
    size_config: dict[str, Any],
) -> list[tuple[ParsedRow, str]]:
    grouped_single_sizes: dict[str, list[str]] = {group: [] for group in GROUP_RENDER_ORDER}
    exploded_fragments: list[tuple[ParsedRow, str]] = []

    for token in row.tams:
        qty, size = parse_qty_and_size(token, size_config)
        group = size_group_of(size, size_config)

        if qty > 1:
            for _ in range(qty):
                exploded_fragments.append(
                    (
                        ParsedRow(
                            name=row.name,
                            number=row.number,
                            tams=(size,),
                            s2=row.s2,
                            s3=row.s3,
                        ),
                        group,
                    )
                )
        else:
            grouped_single_sizes[group].append(size)

    for group in GROUP_RENDER_ORDER:
        sizes = grouped_single_sizes[group]
        if sizes:
            exploded_fragments.append(
                (
                    ParsedRow(
                        name=row.name,
                        number=row.number,
                        tams=tuple(sizes),
                        s2=row.s2,
                        s3=row.s3,
                    ),
                    group,
                )
            )

    return exploded_fragments


def _normalize_rows_for_output(
    rows: list[ParsedRow],
    *,
    size_config: dict[str, Any],
) -> list[tuple[ParsedRow, str]]:
    normalized: list[tuple[ParsedRow, str]] = []

    for row in rows:
        normalized.extend(_explode_row_fragments(row, size_config=size_config))

    return normalized


def _group_column_widths(
    normalized_rows: list[tuple[ParsedRow, str]],
) -> dict[str, int]:
    widths = {group: 0 for group in GROUP_RENDER_ORDER}

    for row, group in normalized_rows:
        widths[group] = max(widths[group], len(row.tams))

    return widths


def build_output(
    rows: List[ParsedRow],
    *,
    size_config: dict[str, Any],
    case_mode: str = "original",
    output_separator: str = ",",
) -> str:
    if not rows:
        return ""

    normalized_rows = _normalize_rows_for_output(rows, size_config=size_config)
    if not normalized_rows:
        return ""

    widths = _group_column_widths(normalized_rows)
    active_groups = [group for group in GROUP_RENDER_ORDER if widths[group] > 0]

    has_s2 = any(row.s2 for row, _ in normalized_rows)
    has_s3 = any(row.s3 for row, _ in normalized_rows)

    out_lines: List[str] = []

    for row, row_group in normalized_rows:
        cols: List[str] = [
            apply_case_mode(row.name, case_mode),
            row.number,
        ]

        for group in active_groups:
            if group == row_group:
                group_sizes = [format_size_token(size, size_config) for size in row.tams]
                group_sizes.extend([""] * (widths[group] - len(group_sizes)))
                cols.extend(group_sizes)
            else:
                cols.extend([""] * widths[group])

        if has_s2:
            cols.append(apply_case_mode(row.s2, case_mode))
        if has_s3:
            cols.append(apply_case_mode(row.s3, case_mode))

        out_lines.append(output_separator.join(cols))

    return "\n".join(out_lines)


def build_orders_from_orderlist(
    rows: List[ParsedRow],
    *,
    size_config: dict[str, Any],
    case_mode: str = "original",
) -> List[dict[str, str]]:
    orders: List[dict[str, str]] = []

    normalized_rows = _normalize_rows_for_output(rows, size_config=size_config)

    for row, _group in normalized_rows:
        for tam in row.tams:
            qty, size = parse_qty_and_size(tam, size_config)
            gender = gender_from_size(size, size_config)

            for _ in range(qty):
                orders.append(
                    {
                        "Name": apply_case_mode(row.name, case_mode),
                        "Nickname": apply_case_mode(row.s2, case_mode),
                        "Number": row.number,
                        "BloodType": apply_case_mode(row.s3, case_mode),
                        "Gender": gender,
                        "ShortSleeve": size,
                        "LongSleeve": "",
                        "Short": "",
                        "Pants": "",
                        "Tanktop": "",
                        "Vest": "",
                    }
                )

    return orders


def build_json_preview(orders: List[dict[str, str]]) -> str:
    data = dict(BASE_JSON)
    data["orders"] = orders
    return json.dumps(data, ensure_ascii=False, indent=4)


def export_output_text(text: str, output_dir: Path, base_name: str) -> Path:
    output_dir.mkdir(parents=True, exist_ok=True)
    path = versioned_path(output_dir, base_name, ".txt")
    path.write_text(text, encoding="utf-8", newline="\n")
    return path


def export_json(orders: List[dict[str, str]], output_dir: Path, base_name: str) -> Path:
    output_dir.mkdir(parents=True, exist_ok=True)
    path = versioned_path(output_dir, base_name, ".json")

    data = dict(BASE_JSON)
    data["orders"] = orders

    path.write_text(
        json.dumps(data, ensure_ascii=False, indent=4),
        encoding="utf-8",
    )
    return path