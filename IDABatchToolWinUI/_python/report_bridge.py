"""
Самодостаточный генератор HTML-отчётов исполнения 2 (IDABatchToolWinUI).

НЕ зависит от пакета исполнения 1: только стандартная библиотека Python.
Генерирует отчёты «Общий анализ», «Анализ СФ» и «Сравнение» по тем же
структурам JSON, что и исполнение 1, и с теми же визуальными шаблонами
(шаблоны лежат рядом: templates/*.html).

Протокол (строки stdout):
    PROGRESS <current> <total> <message>
    ERROR <message>
    RESULT <json>
"""
import argparse
import hashlib
import html
import json
import os
import re
import sqlite3
import sys
from pathlib import Path

TEMPLATES_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "templates")
VENDOR_DIR = os.path.normpath(os.path.join(TEMPLATES_DIR, "..", "vendor"))

# ─────────────────────────────────────────────────────────────────
#  Рендер шаблонов — готовый Jinja2 (внешняя зависимость, НЕ исполнение 1)
# ─────────────────────────────────────────────────────────────────

def inline_vendor(name):
    """Содержимое вендорного asset из vendor/ для инлайн-встраивания.

    Встраивание идёт через контекст шаблона, а не {% include %}: в
    минифицированных JS/CSS встречаются последовательности ``{{``/``%}``,
    которые Jinja приняла бы за собственный синтаксис.
    """
    path = os.path.join(VENDOR_DIR, name)
    try:
        with open(path, encoding="utf-8") as f:
            return f.read()
    except OSError:
        return ""


def render_template(name, **ctx):
    """Рендерит шаблон templates/<name> движком Jinja2 с авто-escape."""
    from jinja2 import Environment, FileSystemLoader, select_autoescape
    env = Environment(
        loader=FileSystemLoader(TEMPLATES_DIR),
        autoescape=select_autoescape(enabled_extensions=("html",), default=True),
        trim_blocks=True,
        lstrip_blocks=True,
    )
    env.globals["inline_vendor"] = inline_vendor
    template = env.get_template(name)
    return template.render(**ctx)


# ─────────────────────────────────────────────────────────────────
#  Утилиты
# ─────────────────────────────────────────────────────────────────

def emit_progress(cur, total, message=""):
    try:
        print(f"PROGRESS {cur} {total} {message}", flush=True)
    except Exception:
        pass


def emit_error(message):
    try:
        print(f"ERROR {message}", flush=True)
    except Exception:
        pass


def emit_result(obj):
    try:
        print("RESULT " + json.dumps(obj, ensure_ascii=False, default=str), flush=True)
    except Exception:
        pass


def normalize_display_name(name):
    """Нормализация имени модуля для классификации."""
    n = str(name or "").strip()
    n = re.sub(r"\.(dll|exe|sys|ocx|so|dylib|bundle)$", "", n, flags=re.IGNORECASE)
    n = re.sub(r"^lib", "", n)
    return n


# ─────────────────────────────────────────────────────────────────
#  Генератор «Общий анализ» (индивидуальные отчёты + индекс)
# ─────────────────────────────────────────────────────────────────

def _classify_module(mod_name):
    """Простая классификация модуля по имени (категория + описание)."""
    n = mod_name.lower()
    if n in ("kernel32", "kernelbase", "ntdll", "user32", "gdi32", "advapi32",
             "ole32", "oleaut32", "comdlg32", "shell32", "shlwapi", "winmm",
             "ws2_32", "wininet", "urlmon", "crypt32", "bcrypt", "ncrypt",
             "psapi", "dbghelp", "version", "setupapi", "cfgmgr32", "wintrust"):
        return "Системные библиотеки Windows", "Ядро Windows API."
    if n.startswith("api-ms-win") or n.startswith("ext-ms-win"):
        return "Системные библиотеки Windows", "API-наборы (API Sets)."
    if n.startswith("libc") or n.startswith("libm") or n.startswith("libpthread") \
            or n.startswith("libdl") or n.startswith("librt") or n.startswith("libutil") \
            or n.startswith("libgcc") or n in ("ld-linux", "linux-vdso"):
        return "Библиотеки C/C++ (Linux)", "Стандартная библиотека C и низкоуровневые библиотеки Linux."
    if n.startswith("libstdc++") or n.startswith("libc++") or n.startswith("libgomp"):
        return "Библиотеки C/C++ (Linux)", "Стандартная библиотека C++."
    if n.startswith("libz") or n in ("z", "libbz2", "liblzma", "liblz4", "libzstd"):
        return "Сжатие и архивы", "Библиотеки сжатия."
    if n.startswith("libssl") or n.startswith("libcrypto"):
        return "Криптография", "OpenSSL/TLS."
    if n.startswith("libcurl") or n.startswith("libhttp") or n.startswith("libxml") \
            or n.startswith("libjson") or n.startswith("libyaml"):
        return "Сетевые протоколы и данные", "Работа с сетью и форматами данных."
    if n.startswith("libsqlite") or n.startswith("sqlite") or n.startswith("libpq"):
        return "Базы данных", "Библиотеки БД."
    if n.startswith("libgtk") or n.startswith("libqt") or n.startswith("libx11") \
            or n.startswith("libwayland") or n.startswith("libglib"):
        return "Графика и GUI", "Библиотеки графического интерфейса."
    if n.startswith("libpcap") or n.startswith("pcap") or n.startswith("libnet"):
        return "Сетевые протоколы и данные", "Захват сетевых пакетов."
    if n.startswith("libudev") or n.startswith("libusb") or n.startswith("libpci"):
        return "Оборудование", "Работа с устройствами."
    if n in ("libandroid_runtime", "libandroid", "libbinder") or n.startswith("libandroid"):
        return "Библиотеки Android", "Системные библиотеки Android."
    if n.startswith("libfmj") or n.startswith("libiot") or n.startswith("liblog"):
        return "Библиотеки Android", "Служебные библиотеки Android."
    return "Прочие", "Дополнительные модули."


def _file_info_entries(data):
    """Информация о файле для отчёта анализа."""
    rows = []
    rows.append(("Имя файла", data.get("file_name", "")))
    rows.append(("Формат", data.get("format", "") or data.get("file_format", "")))
    rows.append(("Тип", data.get("type", "") or data.get("file_type", "")))
    rows.append(("Архитектура", data.get("arch", "") or data.get("processor", "")))
    rows.append(("Компилятор", data.get("compiler", "") or ""))
    rows.append(("Размер", str(data.get("file_size", ""))))
    for k, v in [("SHA-256", "sha256"), ("MD5", "md5"), ("CRC32", "crc32")]:
        if data.get(v):
            rows.append((k, data[v]))
    return rows


def generate_analysis_report(json_path, output_html, input_dir, internal_set):
    """Генерирует индивидуальный HTML-отчёт «Общий анализ»."""
    with open(json_path, "r", encoding="utf-8") as f:
        data = json.load(f)

    file_name = os.path.basename(data.get("file_name", str(json_path)))
    imports = data.get("imports", [])
    exports = data.get("exports", [])

    module_deps = []
    seen = set()
    if data.get("is_elf") or data.get("is_macho"):
        for needed in data.get("needed_libs", []):
            name = normalize_display_name(needed)
            if name in seen:
                continue
            seen.add(name)
            cat, desc = _classify_module(name)
            module_deps.append({"name": name, "count": 0, "category": cat,
                                "description": desc, "color": "#8b5cf6"})
    else:
        from collections import Counter
        cnt = Counter()
        for imp in imports:
            mod = imp.get("module", "")
            if not mod or mod.lower() == "unknown":
                continue
            if mod.startswith("."):
                continue
            cnt[normalize_display_name(mod)] += 1
        for name, c in cnt.most_common():
            is_internal = name.lower() in (internal_set or set())
            cat = "Внутренние модули" if is_internal else "Системные библиотеки"
            if not is_internal:
                cat, desc = _classify_module(name)
            module_deps.append({"name": name, "count": c, "category": cat,
                                "description": "" if is_internal else desc,
                                "color": "#8b5cf6" if not is_internal else "#10b981"})

    functions = []
    for func in data.get("functions", []):
        functions.append({
            "name": func.get("name", "<unnamed>"),
            "start_ea": func.get("start_ea", ""),
            "size": func.get("size", 0),
            "hexdump": func.get("hexdump", ""),
            "instructions_text": func.get("instructions_text", "") or func.get("instructions", ""),
            "pseudocode": func.get("pseudocode", ""),
        })

    ctx = {
        "file_name": file_name,
        "back_link": "index.html",
        "file_info": _file_info_entries(data),
        "module_deps": module_deps,
        "imports": imports,
        "exports": exports,
        "is_elf": data.get("is_elf", False),
        "elf_segment_rows": _elf_segments(data),
        "elf_section_rows": _elf_sections(data),
        "functions": functions,
    }
    text = render_template("report.html", **ctx)
    os.makedirs(os.path.dirname(output_html), exist_ok=True)
    with open(output_html, "w", encoding="utf-8") as f:
        f.write(text)


def _elf_segments(data):
    segs = []
    for seg in data.get("elf_segments", []) or []:
        segs.append({"type": seg.get("type", ""), "flags": seg.get("flags", ""),
                     "description": seg.get("description", "")})
    return segs


def _elf_sections(data):
    secs = []
    for s in data.get("elf_sections", []) or []:
        secs.append({"name": s.get("name", ""), "type": s.get("type", ""),
                     "flags": s.get("flags", ""), "description": s.get("description", "")})
    return secs


def generate_analysis_index(reports_dir, input_dir, report_links, global_modules,
                            ida_info, internal_set=None, total_files=0,
                            total_size_bytes=0, error_count=0, generation_time=""):
    """Сводный index.html для «Общего анализа»."""
    categories = {}
    for mod in global_modules:
        is_internal = internal_set and mod.lower() in internal_set
        if is_internal:
            key = "Внутренние модули"
        else:
            key, _ = _classify_module(mod)
        if key not in categories:
            categories[key] = {"name": key, "count": 0, "modules": [], "description": ""}
        categories[key]["count"] += 1
        categories[key]["modules"].append({"name": mod, "desc": ""})
    grouped = sorted(categories.values(), key=lambda c: -c["count"])

    ctx = {
        "input_dir": input_dir,
        "total_modules": len(report_links),
        "total_files": total_files,
        "total_size_bytes": total_size_bytes,
        "error_count": error_count,
        "generation_time": generation_time,
        "ida_info": ida_info or {},
        "grouped_categories": grouped,
        "reports": report_links,
    }
    index_path = os.path.join(reports_dir, "index.html")
    text = render_template("index.html", **ctx)
    with open(index_path, "w", encoding="utf-8") as f:
        f.write(text)
    return index_path


# ─────────────────────────────────────────────────────────────────
#  Генератор «Анализ СФ»
# ─────────────────────────────────────────────────────────────────

def _mslearn_lookup(func_name, cache_db):
    """Поиск документации в кэше MS Learn (SQLite, таблица search_cache)."""
    if not cache_db or not os.path.isfile(cache_db):
        return None
    try:
        conn = sqlite3.connect(cache_db)
        try:
            cur = conn.execute(
                "SELECT title, url, markdown FROM search_cache WHERE query = ? AND status = 'ok' LIMIT 1",
                (func_name,))
            row = cur.fetchone()
            if row:
                return {"title": row[0], "url": row[1], "markdown": row[2], "markdown_html": ""}
        finally:
            conn.close()
    except Exception:
        return None
    return None


def _lookup_doc(func_name, platform, cache_db, manpages_db):
    if platform in ("Linux", "Linux / Android"):
        if manpages_db and os.path.isfile(manpages_db):
            try:
                conn = sqlite3.connect(manpages_db)
                try:
                    cur = conn.execute(
                        "SELECT f.func_name, p.page_name, p.section, p.title, p.markdown "
                        "FROM function_index f JOIN pages p ON p.page_name = f.page_name "
                        "WHERE f.func_name = ? LIMIT 1", (func_name,))
                    row = cur.fetchone()
                    if row:
                        section = row[2]
                        page_name = row[1]
                        title = row[3]
                        markdown = row[4]
                        url = f"https://man7.org/linux/man-pages/man{section}/{page_name}.{section}.html"
                        return {"title": f"{page_name}({section})", "url": url,
                                "markdown": markdown, "markdown_html": ""}
                finally:
                    conn.close()
            except Exception:
                return None
        return None
    # Windows / прочие — MS Learn кэш
    return _mslearn_lookup(func_name, cache_db)


def _is_system_module(mod_name, platform):
    """Определяет, системный ли модуль."""
    n = (mod_name or "").lower()
    if platform in ("Linux", "Linux / Android"):
        return (n.startswith("lib") and any(x in n for x in
                ("libc", "libm", "libpthread", "libdl", "librt", "libgcc", "libstdc++",
                 "libz", "libssl", "libcrypto", "libcurl", "libxml", "libjson", "libsqlite",
                 "libpcap", "libudev", "libusb", "libgtk", "libqt", "libx11", "libglib"))) \
                or n in ("ld-linux", "linux-vdso")
    return True  # для Windows считаем всё системным (как в референсе)


def generate_sfa_report(json_path, output_html, reports_dir, input_dir,
                        platform, cache_db, manpages_db, list_all_imports,
                        data_override=None):
    """Индивидуальный HTML-отчёт «Анализ СФ».

    data_override — dict с данными (file_name, imports, needed_libs, file_size),
    используется в reuse-режиме, когда JSON уже удалён.
    """
    if data_override is not None:
        data = data_override
    else:
        with open(json_path, "r", encoding="utf-8") as f:
            data = json.load(f)

    file_name = os.path.basename(data.get("file_name", str(json_path)))
    imports = data.get("imports", [])
    needed_libs = data.get("needed_libs", []) or []

    docs_available = False
    if platform in ("Linux", "Linux / Android"):
        docs_available = manpages_db is not None and os.path.isfile(manpages_db)
    else:
        docs_available = cache_db is not None and os.path.isfile(cache_db)

    system_calls = []
    found_count = 0
    notfound_count = 0
    total_imports = len(imports)
    system_names = set()
    system_libs = set()
    system_notfound_names = set()

    if platform in ("Linux", "Linux / Android") and not data.get("is_elf"):
        # Не ELF — используем импорты
        pass

    for imp in imports:
        name = imp.get("name", "")
        dll = imp.get("module", "")
        if not name:
            continue
        is_system = _is_system_module(dll, platform)
        sr = _lookup_doc(name, platform, cache_db, manpages_db)
        found = sr is not None
        if found:
            found_count += 1
        else:
            notfound_count += 1
        if is_system:
            system_names.add(name)
            system_libs.add(dll)
            if not found:
                system_notfound_names.add(name)
        system_calls.append({
            "dll": dll or "—",
            "name": name,
            "is_system": is_system,
            "found": found,
            "search_results": [sr] if sr else [],
        })

    # ELF: нужные библиотеки (DT_NEEDED) — системные библиотеки, добавляем их функции
    if data.get("is_elf"):
        for lib in needed_libs:
            if _is_system_module(lib, platform):
                system_libs.add(lib)

    ctx = {
        "file_name": file_name,
        "back_link": "index.html",
        "platform": _platform_label(platform),
        "system_calls": system_calls,
        "list_all_imports": list_all_imports,
        "docs_available": docs_available,
        "marked_js": _marked_js(),
    }
    text = render_template("sfa_report.html", **ctx)

    # Считаем статистику для индекса
    stats = {
        "found_count": found_count,
        "notfound_count": notfound_count,
        "total_imports": total_imports,
        "system_count": len(system_names),
        "system_names": system_names,
        "system_libs": system_libs,
        "system_notfound_names": system_notfound_names,
    }

    os.makedirs(os.path.dirname(output_html), exist_ok=True)
    with open(output_html, "w", encoding="utf-8") as f:
        f.write(text)
    # Статистика сохраняется в sidecar-файл рядом (для индекса)
    stats_path = output_html + ".stats.json"
    with open(stats_path, "w", encoding="utf-8") as f:
        json.dump({k: (sorted(v) if isinstance(v, set) else v) for k, v in stats.items()},
                  f, ensure_ascii=False)
    return stats


def _platform_label(platform):
    return {"Windows": "Windows", "Linux": "Linux", "Linux / Android": "Linux / Android",
            "macOS / iOS": "macOS / iOS"}.get(platform, platform)


def _marked_js():
    """Встроенный marked.min.js (без внешних файлов)."""
    try:
        full = os.path.join(TEMPLATES_DIR, "..", "vendor", "marked.min.js")
        if os.path.isfile(full):
            with open(full, "r", encoding="utf-8") as f:
                return f.read()
    except Exception:
        pass
    return ""


def generate_sfa_index(reports_dir, input_dir, report_links, ida_info,
                       total_files, total_size_bytes, total_system_modules,
                       total_system_functions, total_system_notfound,
                       total_imports, generation_time, platform):
    ctx = {
        "input_dir": input_dir,
        "platform": _platform_label(platform),
        "generation_time": generation_time,
        "total_files": total_files,
        "total_size_bytes": total_size_bytes,
        "total_system_modules": total_system_modules,
        "total_system_functions": total_system_functions,
        "total_system_notfound": total_system_notfound,
        "total_imports": total_imports,
        "reports": report_links,
    }
    index_path = os.path.join(reports_dir, "index.html")
    text = render_template("sfa_index.html", **ctx)
    with open(index_path, "w", encoding="utf-8") as f:
        f.write(text)
    return index_path


# ─────────────────────────────────────────────────────────────────
#  Генератор «Сравнение» (diff)
# ─────────────────────────────────────────────────────────────────

def generate_diff_report(json_path, output_html, reports_dir, input_dir, internal_set):
    with open(json_path, "r", encoding="utf-8") as f:
        data = json.load(f)
    if not isinstance(data, dict):
        # diff.json может оказаться списком (доанализ/ошибка) — не падаем, рисуем пустой отчёт
        data = {}

    primary = data.get("real_primary") or data.get("primary", "")
    secondary = data.get("real_secondary") or data.get("secondary", "")

    ctx = {
        "primary": primary,
        "secondary": secondary,
        "real_primary": data.get("real_primary", ""),
        "real_secondary": data.get("real_secondary", ""),
        "back_link": "index.html",
        "error": data.get("error"),
        "file1": data.get("file1", {}),
        "file2": data.get("file2", {}),
        "total_functions1": data.get("total_functions1", 0),
        "total_functions2": data.get("total_functions2", 0),
        "global_hex_diff": data.get("global_hex_diff", []),
        "hexdump_similarity": data.get("hexdump_similarity", 0.0),
        "similarity": data.get("similarity", 0.0),
        "confidence": data.get("confidence", 0.0),
        "description": data.get("description", ""),
        "version": data.get("version", ""),
        "engine": data.get("engine", "bindiff"),
        "matched_summary": data.get("matched_summary", {}),
        "matched_diaphora_only": data.get("matched_diaphora_only", []),
        "imports_only_in_primary": data.get("imports_only_in_primary", []),
        "imports_only_in_secondary": data.get("imports_only_in_secondary", []),
        "matched_functions": data.get("matched_functions", []),
        "unmatched_functions1": data.get("unmatched_functions1", []),
        "unmatched_functions2": data.get("unmatched_functions2", []),
        "total_unmatched": data.get("total_unmatched", 0),
        "global_insn_diff": data.get("global_insn_diff", []),
    }
    text = render_template("diff_report.html", **ctx)
    os.makedirs(os.path.dirname(output_html), exist_ok=True)
    with open(output_html, "w", encoding="utf-8") as f:
        f.write(text)


def generate_diff_index(reports_dir, json_files, left_dir, right_dir,
                        generation_time, ida_version=""):
    pairs = []
    total_pairs = len(json_files)
    sims, confs = [], []
    has_bindiff = False
    has_diaphora = False

    for jf in json_files:
        stem = os.path.basename(jf).replace(".diff.json", "")
        try:
            with open(jf, "r", encoding="utf-8") as f:
                data = json.load(f)
        except Exception:
            data = {}
        if not isinstance(data, dict):
            data = {}
        matching = data.get("matched_summary", {})
        matched_count = data.get("total_matched") or (
            (matching.get("bindiff_only", 0) if isinstance(matching, dict) else 0)
            + (matching.get("diaphora_only", 0) if isinstance(matching, dict) else 0)
            + (matching.get("both", 0) if isinstance(matching, dict) else 0))
        sim = float(data.get("similarity", 0.0) or 0.0)
        hd_sim = float(data.get("hexdump_similarity", 0.0) or 0.0)
        conf = float(data.get("confidence", 0.0) or 0.0)
        eng = data.get("engine", "")
        if "bindiff" in eng:
            has_bindiff = True
        if "diaphora" in eng:
            has_diaphora = True
        if sim > 0:
            sims.append(sim)
            confs.append(conf)
        display_sim = hd_sim if hd_sim > 0 else sim
        pairs.append({
            "stem": stem,
            "similarity": sim,
            "display_similarity": display_sim,
            "hexdump_similarity": hd_sim,
            "confidence": conf,
            "matched_count": matched_count,
            "total_funcs1": data.get("total_functions1", 0),
            "hash1": data.get("file1", {}).get("hash", "") if isinstance(data.get("file1"), dict) else "",
            "hash2": data.get("file2", {}).get("hash", "") if isinstance(data.get("file2"), dict) else "",
            "diaphora_matched_count": data.get("diaphora_matched_count", 0),
            "report_filename": stem + ".html",
        })

    avg_sim = sum(sims) / len(sims) if sims else 0.0
    avg_conf = sum(confs) / len(confs) if confs else 0.0
    ctx = {
        "left_dir": left_dir,
        "right_dir": right_dir,
        "generation_time": generation_time,
        "ida_version": ida_version,
        "total_pairs": total_pairs,
        "avg_similarity": avg_sim,
        "avg_confidence": avg_conf,
        "has_bindiff": has_bindiff,
        "has_diaphora": has_diaphora,
        "pairs": pairs,
    }
    index_path = os.path.join(reports_dir, "index.html")
    with open(index_path, "w", encoding="utf-8") as f:
        f.write(render_template("diff_index.html", **ctx))
    return index_path


# ─────────────────────────────────────────────────────────────────
#  CLI
# ─────────────────────────────────────────────────────────────────

def _split(s):
    return [x for x in s.split(";") if x.strip()]


def _collect_export_jsons(json_dir, json_paths):
    if json_paths:
        return [Path(p) for p in _split(json_paths) if Path(p).is_file()]
    if os.path.isdir(json_dir):
        return sorted(Path(json_dir).glob("*.export.json"))
    return []


# ─────────────────────────────────────────────────────────────────
#  Индекс системных функций (sfa_function_index.db) — для reuse-режима
# ─────────────────────────────────────────────────────────────────

_SFA_INDEX_SCHEMA = """
CREATE TABLE IF NOT EXISTS system_functions (
    func_name TEXT PRIMARY KEY,
    module_name TEXT NOT NULL,
    module_key TEXT NOT NULL DEFAULT '',
    category TEXT NOT NULL DEFAULT ''
);
CREATE TABLE IF NOT EXISTS system_modules (
    module_key TEXT PRIMARY KEY,
    module_name TEXT NOT NULL DEFAULT '',
    category TEXT NOT NULL DEFAULT ''
);
CREATE TABLE IF NOT EXISTS file_imports (
    json_path TEXT NOT NULL,
    file_name TEXT NOT NULL DEFAULT '',
    func_name TEXT NOT NULL,
    module_name TEXT NOT NULL DEFAULT '',
    address TEXT NOT NULL DEFAULT '',
    file_size INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (json_path, func_name)
);
CREATE INDEX IF NOT EXISTS idx_fi_jp ON file_imports(json_path);
CREATE TABLE IF NOT EXISTS file_libs (
    json_path TEXT NOT NULL,
    lib_name TEXT NOT NULL,
    PRIMARY KEY (json_path, lib_name)
);
CREATE INDEX IF NOT EXISTS idx_fl_jp ON file_libs(json_path);
CREATE TABLE IF NOT EXISTS meta (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL DEFAULT ''
);
"""


def _normalize_module_key(module):
    """Канонический ключ модуля: без расширения, нижний регистр."""
    m = str(module or "").strip()
    m = re.sub(r"\.(dll|exe|sys|ocx|so|dylib|bundle)$", "", m, flags=re.IGNORECASE)
    m = re.sub(r"^lib", "", m)
    return m.lower()


def build_sfa_index(index_db, json_files, platform):
    """Строит index-БД системных функций из JSON-файлов (аналог SfaFunctionIndex.build_from_jsons)."""
    p = Path(index_db)
    p.parent.mkdir(parents=True, exist_ok=True)
    conn = sqlite3.connect(str(p))
    try:
        conn.executescript(_SFA_INDEX_SCHEMA)
        conn.execute("DELETE FROM system_functions")
        conn.execute("DELETE FROM system_modules")
        conn.execute("INSERT OR REPLACE INTO meta (key, value) VALUES ('platform', ?)", (platform,))

        seen = set()
        for jp in json_files:
            if not jp.exists():
                continue
            try:
                with open(jp, "r", encoding="utf-8") as f:
                    data = json.load(f)
            except Exception:
                continue
            jp_str = str(jp)
            file_name = data.get("file_name", "")
            imports = data.get("imports", [])
            file_size = int(data.get("file_size") or 0)
            src = Path(file_name)
            if not src.is_absolute():
                src = jp.parent.parent / src
            if not file_size:
                alongside = jp.parent / Path(file_name).name
                if alongside.exists():
                    file_size = alongside.stat().st_size
            if not file_size and src.exists():
                file_size = src.stat().st_size

            conn.execute("DELETE FROM file_imports WHERE json_path = ?", (jp_str,))
            conn.execute("DELETE FROM file_libs WHERE json_path = ?", (jp_str,))
            for lib in data.get("needed_libs") or []:
                if lib:
                    conn.execute(
                        "INSERT OR IGNORE INTO file_libs (json_path, lib_name) VALUES (?, ?)",
                        (jp_str, lib))

            for imp in imports:
                func_name = imp.get("name", "")
                module = imp.get("module", "") or ""
                address = imp.get("address", "")
                if not func_name or not module:
                    continue
                module_key = _normalize_module_key(module)
                if not module_key:
                    continue
                conn.execute(
                    "INSERT OR REPLACE INTO file_imports "
                    "(json_path, file_name, func_name, module_name, address, file_size) "
                    "VALUES (?, ?, ?, ?, ?, ?)",
                    (jp_str, file_name, func_name, module, address, file_size))
                if _is_system_module(module, platform):
                    conn.execute(
                        "INSERT OR IGNORE INTO system_modules (module_key, module_name, category) "
                        "VALUES (?, ?, ?)", (module_key, module, ""))
                    if func_name not in seen:
                        seen.add(func_name)
                        conn.execute(
                            "INSERT OR IGNORE INTO system_functions "
                            "(func_name, module_name, module_key, category) VALUES (?, ?, ?, ?)",
                            (func_name, module, module_key, ""))
        conn.commit()
    finally:
        conn.close()


def read_index_imports(index_db, json_path):
    """Читает импорты файла из index-БД для reuse-режима. Возвращает (imports, file_name, needed_libs)."""
    imports = []
    file_name = ""
    needed_libs = []
    try:
        conn = sqlite3.connect(str(index_db))
        try:
            rows = conn.execute(
                "SELECT func_name, module_name, address, file_name FROM file_imports WHERE json_path = ?",
                (str(json_path),)).fetchall()
            for func_name, module, address, fname in rows:
                imports.append({"name": func_name, "module": module, "address": address or ""})
                if fname:
                    file_name = fname
            libs = conn.execute(
                "SELECT lib_name FROM file_libs WHERE json_path = ?", (str(json_path),)).fetchall()
            needed_libs = [r[0] for r in libs]
        finally:
            conn.close()
    except Exception:
        pass
    return imports, file_name, needed_libs


def read_index_json_paths(index_db):
    """Список json_path из index-БД для reuse-режима."""
    paths = []
    try:
        conn = sqlite3.connect(str(index_db))
        try:
            rows = conn.execute(
                "SELECT DISTINCT json_path FROM file_imports WHERE json_path IS NOT NULL").fetchall()
            paths = [r[0] for r in rows if r[0]]
        finally:
            conn.close()
    except Exception:
        pass
    return paths


def run_generate(args):
    input_dir = Path(args.input_dir)
    reports_dir = Path(args.reports_dir)

    if args.kind == "analysis":
        json_files = _collect_export_jsons(args.json_dir, args.json_paths)
        if not json_files:
            emit_error("Нет JSON-файлов экспорта. Сначала выполните анализ.")
            return
        reports_dir.mkdir(parents=True, exist_ok=True)
        internal_set = set()
        # _build_internal_set аналог: внутренние модули — файлы в input_dir без расширения целевых
        total = len(json_files)
        done = 0
        report_links = []
        global_modules = set()
        ida_info = None
        total_files = 0
        total_size = 0

        for jp in sorted(json_files, key=lambda p: p.stat().st_size if p.exists() else 0, reverse=True):
            try:
                with open(jp, "r", encoding="utf-8") as f:
                    data = json.load(f)
            except Exception as e:
                emit_error(f"Ошибка чтения {jp.name}: {e}")
                done += 1
                emit_progress(done, total, "")
                continue

            local_ida = data.get("ida_info") if "ida_info" in data else None
            modules = set()
            if data.get("is_elf") or data.get("is_macho"):
                for needed in data.get("needed_libs", []):
                    modules.add(normalize_display_name(needed))
            else:
                for imp in data.get("imports", []):
                    mod = imp.get("module")
                    if not mod or str(mod).lower() == "unknown":
                        continue
                    if not str(mod).startswith("."):
                        modules.add(normalize_display_name(mod))

            original = os.path.basename(data.get("file_name", jp.name))
            source_full = Path(data["file_name"]) if data.get("file_name") else jp
            if not source_full.is_absolute():
                source_full = input_dir / source_full
            try:
                rel = source_full.relative_to(input_dir)
            except ValueError:
                rel = Path(original)
            out_rel = rel.with_suffix(rel.suffix + ".html")
            output_html = reports_dir / out_rel
            generate_analysis_report(str(jp), str(output_html), str(input_dir), internal_set)

            link = out_rel.as_posix()
            display = rel.as_posix()
            file_size = int(data.get("file_size") or 0)
            if not file_size:
                alongside = jp.parent / original
                if alongside.exists():
                    file_size = alongside.stat().st_size
                elif source_full.exists():
                    file_size = source_full.stat().st_size

            report_links.append({"filename": link, "display_name": display})
            global_modules.update(modules)
            if local_ida and ida_info is None:
                ida_info = local_ida
            total_files += 1
            total_size += file_size

            if args.delete_json:
                try:
                    jp.unlink()
                except OSError:
                    pass
            done += 1
            emit_progress(done, total, "")

        from datetime import datetime
        index_path = generate_analysis_index(
            str(reports_dir), str(input_dir), report_links, global_modules,
            ida_info or {}, internal_set=internal_set, total_files=total_files,
            total_size_bytes=total_size, error_count=0,
            generation_time=datetime.now().strftime("%Y-%m-%d %H:%M:%S"))
        emit_result({
            "reports_dir": str(reports_dir), "input_dir": str(input_dir),
            "index_path": str(index_path), "generated_count": len(report_links),
            "total_files": total_files, "total_size_bytes": total_size,
        })

    elif args.kind == "sfa":
        sfa_index_db = reports_dir / "sfa_function_index.db"
        platform = args.platform

        if args.reuse_cache:
            # Читаем пути из index-БД (JSON могут быть удалены)
            json_files = [Path(p) for p in read_index_json_paths(sfa_index_db)]
            if not json_files:
                emit_error("Индекс БД устарел или не содержит данных. Выполните полный анализ для перестроения индекса.")
                return
            # платформа из индекса
            try:
                conn = sqlite3.connect(str(sfa_index_db))
                row = conn.execute("SELECT value FROM meta WHERE key='platform'").fetchone()
                conn.close()
                if row and row[0]:
                    platform = row[0]
            except Exception:
                pass
        else:
            json_files = _collect_export_jsons(args.json_dir, args.json_paths)
            if not json_files:
                emit_error("Нет JSON-файлов экспорта.")
                return
            reports_dir.mkdir(parents=True, exist_ok=True)
            emit_progress(0, max(len(json_files), 1), "Сканирование системных функций…")
            try:
                build_sfa_index(sfa_index_db, json_files, platform)
            except Exception as e:
                emit_error(f"Ошибка сканирования системных функций: {e}")

        cache_db = reports_dir / "mslearn_cache.db" if (reports_dir / "mslearn_cache.db").is_file() else None
        manpages_db = Path(args.manpages_db) if args.manpages_db and Path(args.manpages_db).is_file() else None
        if args.manpages_db and not (Path(args.manpages_db).is_file()):
            p2 = Path(args.manpages_db) / "manpages.db"
            if p2.is_file():
                manpages_db = p2

        total = len(json_files)
        done = 0
        report_links = []
        ida_info = {}
        total_size = 0
        total_imports_all = 0
        global_found = 0
        global_notfound = 0
        gsf, gsl, gsn = set(), set(), set()

        list_all = platform in ("Linux", "Linux / Android")

        for jp in sorted(json_files, key=lambda p: p.stat().st_size if p.exists() else 0, reverse=True):
            if not jp.exists() and not args.reuse_cache:
                done += 1
                emit_progress(done, total, f"{os.path.basename(str(jp))} — ошибка")
                continue

            if args.reuse_cache and not jp.exists():
                # читаем импорты из index БД
                imports_data, local_file_name, needed_libs = read_index_imports(sfa_index_db, jp)
                if not imports_data:
                    emit_error(f"Нет импортов в индексе для {os.path.basename(str(jp))}")
                    done += 1
                    emit_progress(done, total, f"{os.path.basename(str(jp))} — ошибка")
                    continue
                data = {
                    "file_name": local_file_name,
                    "imports": imports_data,
                    "needed_libs": needed_libs,
                    "file_size": 0,
                    "is_elf": bool(needed_libs),
                    "ida_info": {},
                }
            else:
                try:
                    with open(jp, "r", encoding="utf-8") as f:
                        data = json.load(f)
                except Exception as e:
                    emit_error(f"Ошибка чтения {os.path.basename(str(jp))}: {e}")
                    done += 1
                    emit_progress(done, total, f"{os.path.basename(str(jp))} — ошибка")
                    continue

            original = os.path.basename(data.get("file_name", os.path.basename(str(jp))))
            source_full = Path(data.get("file_name", "") or "")
            if not source_full.is_absolute():
                source_full = input_dir / source_full
            try:
                rel = source_full.relative_to(input_dir)
            except ValueError:
                rel = Path(original)
            out_rel = rel.with_suffix(".sfa.html")
            output_html = reports_dir / out_rel

            stats = generate_sfa_report(
                str(jp), str(output_html), str(reports_dir), str(input_dir),
                platform, str(cache_db) if cache_db else None,
                str(manpages_db) if manpages_db else None, list_all,
                data_override=data if args.reuse_cache and not jp.exists() else None)

            file_size = int(data.get("file_size") or 0)
            if not file_size and source_full.exists():
                file_size = source_full.stat().st_size
            if not file_size:
                # для reuse — из index БД
                try:
                    conn = sqlite3.connect(str(sfa_index_db))
                    row = conn.execute(
                        "SELECT file_size FROM file_imports WHERE json_path = ? LIMIT 1",
                        (str(jp),)).fetchone()
                    conn.close()
                    if row and row[0]:
                        file_size = row[0]
                except Exception:
                    pass

            report_links.append({
                "filename": out_rel.as_posix(), "display_name": rel.as_posix(),
                "found_count": stats["found_count"], "notfound_count": stats["notfound_count"],
                "total_imports": stats["total_imports"], "system_count": stats["system_count"],
            })
            if data.get("ida_info") and not ida_info:
                ida_info = data["ida_info"]
            total_size += file_size
            total_imports_all += stats["total_imports"]
            global_found += stats["found_count"]
            global_notfound += stats["notfound_count"]
            gsf.update(stats["system_names"])
            gsl.update(stats["system_libs"])
            gsn.update(stats["system_notfound_names"])

            if args.delete_json and not args.reuse_cache:
                try:
                    jp.unlink()
                except OSError:
                    pass
            done += 1
            emit_progress(done, total, f"{os.path.basename(str(jp))} — готов")

        report_links.sort(key=lambda r: r["display_name"])
        from datetime import datetime
        index_path = generate_sfa_index(
            str(reports_dir), str(input_dir), report_links, ida_info,
            total_files=total, total_size_bytes=total_size,
            total_system_modules=len(gsl), total_system_functions=len(gsf),
            total_system_notfound=len(gsn), total_imports=total_imports_all,
            generation_time=datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
            platform=platform)
        emit_result({
            "reports_dir": str(reports_dir), "input_dir": str(input_dir),
            "index_path": str(index_path), "generated_count": len(report_links),
            "total_files": total, "total_size_bytes": total_size,
            "total_system_modules": len(gsl), "total_system_functions": len(gsf),
            "total_system_notfound": len(gsn), "total_imports": total_imports_all,
            "platform": platform,
        })

    elif args.kind == "diff":
        json_files = [Path(p) for p in _split(args.json_paths)] if args.json_paths else \
            list(Path(args.json_dir).glob("*.diff.json"))
        if not json_files:
            emit_error("Нет JSON-файлов с результатами сравнения.")
            return
        reports_dir.mkdir(parents=True, exist_ok=True)
        left = Path(args.left_dir) if args.left_dir else Path(args.json_dir)
        right = Path(args.right_dir) if args.right_dir else Path(args.json_dir)
        total = len(json_files)
        done = 0
        for jf in sorted(json_files):
            html_path = reports_dir / (jf.stem.replace(".diff", "") + ".html")
            try:
                generate_diff_report(str(jf), str(html_path), str(reports_dir), str(left), None)
            except Exception as e:
                emit_error(f"Ошибка генерации отчёта {jf.name}: {e}")
            done += 1
            emit_progress(done, total, jf.name)

        from datetime import datetime
        ida_version = ""
        try:
            cand = list(left.glob("*.export.json")) or list(Path(args.json_dir).glob("*.export.json"))
            if cand:
                with open(cand[0], "r", encoding="utf-8") as f:
                    d = json.load(f)
                ida_version = d.get("ida_info", {}).get("kernel_version", "")
        except Exception:
            pass
        index_path = generate_diff_index(
            str(reports_dir), [str(j) for j in json_files], str(left), str(right),
            generation_time=datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
            ida_version=ida_version)
        emit_result({
            "reports_dir": str(reports_dir), "input_dir": str(input_dir),
            "index_path": str(index_path), "generated_count": done,
        })


def main():
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest="command", required=True)
    p = sub.add_parser("generate")
    p.add_argument("--kind", required=True, choices=["analysis", "sfa", "diff"])
    p.add_argument("--input-dir", required=True)
    p.add_argument("--reports-dir", required=True)
    p.add_argument("--json-dir", required=True)
    p.add_argument("--left-dir", default="")
    p.add_argument("--right-dir", default="")
    p.add_argument("--platform", default="Windows")
    p.add_argument("--delete-json", action="store_true")
    p.add_argument("--reuse-cache", action="store_true")
    p.add_argument("--manpages-db", default="")
    p.add_argument("--json-paths", default="")
    args = parser.parse_args()

    try:
        if args.command == "generate":
            run_generate(args)
    except Exception as e:
        emit_error(f"{type(e).__name__}: {e}")
        sys.exit(1)


if __name__ == "__main__":
    main()