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
import json
import os
import re
import shutil
import sqlite3
import subprocess
import sys
import threading
import urllib.error
import urllib.request
from concurrent.futures import ThreadPoolExecutor, as_completed
from datetime import datetime, timedelta, timezone
from pathlib import Path

TEMPLATES_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "templates")
VENDOR_DIR = os.path.normpath(os.path.join(TEMPLATES_DIR, "..", "vendor"))

# Переносимая поставка: если мост запущен от Tools\Python рядом с приложением,
# подключаем соседний site-packages (jinja2, requests), иначе они не видны.
_SP = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                    "..", "..", "Tools", "Python", "site-packages"))
if os.path.isdir(_SP) and _SP not in sys.path:
    sys.path.insert(0, _SP)

# Протокол обмена с GUI (PROGRESS/ERROR/RESULT) — строго UTF-8: без этого
# pythonw пишет в канал в системной кодировке (cp1251), и русские сообщения
# в GUI превращаются в нечитаемые символы.
for _stream in (sys.stdout, sys.stderr):
    if _stream is not None and hasattr(_stream, "reconfigure"):
        try:
            _stream.reconfigure(encoding="utf-8")
        except Exception:
            pass

# ─────────────────────────────────────────────────────────────────
#  Рендер шаблонов — готовый Jinja2 (внешняя зависимость, НЕ исполнение 1)
# ─────────────────────────────────────────────────────────────────

def inline_vendor(name, _cache={}):
    """Содержимое вендорного asset из vendor/ для инлайн-встраивания.

    Встраивание идёт через контекст шаблона, а не {% include %}: в
    минифицированных JS/CSS встречаются последовательности ``{{``/``%}``,
    которые Jinja приняла бы за собственный синтаксис. Содержимое
    кэшируется: marked.min.js читается на диск для каждого SFA-отчёта.
    """
    if name in _cache:
        return _cache[name]
    path = os.path.join(VENDOR_DIR, name)
    try:
        with open(path, encoding="utf-8") as f:
            content = f.read()
    except OSError:
        content = ""
    _cache[name] = content
    return content


# Один Environment на процесс: пересоздание на каждый вызов render_template
# перекомпилировало шаблоны заново для каждого отчёта.
_JINJA_ENV = None


def _jinja_env():
    global _JINJA_ENV
    if _JINJA_ENV is None:
        from jinja2 import Environment, FileSystemLoader, select_autoescape
        env = Environment(
            loader=FileSystemLoader(TEMPLATES_DIR),
            autoescape=select_autoescape(enabled_extensions=("html",), default=True),
            trim_blocks=True,
            lstrip_blocks=True,
        )
        env.globals["inline_vendor"] = inline_vendor
        _JINJA_ENV = env
    return _JINJA_ENV


def render_template(name, **ctx):
    """Рендерит шаблон templates/<name> движком Jinja2 с авто-escape."""
    template = _jinja_env().get_template(name)
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


def _stat_size_safe(path):
    """Размер файла в байтах; 0, если файл исчез или недоступен.

    exists() + stat() в одном выражении — гонка: файл может пропасть
    между проверкой и stat(), и OSError при сортировке валил весь прогон.
    """
    try:
        return path.stat().st_size
    except OSError:
        return 0


def normalize_display_name(module_name):
    """Каноническая нормализация имени модуля для отображения в отчётах.

    Как в исполнении 1 (reporting/utils.py): убирает путь, специфичные
    суффиксы платформ (.dylib, .framework), префикс ``@rpath/``. Имя
    сохраняет расширение и версии (``libcrypto.so.1.1`` остаётся
    ``libcrypto.so.1.1``) — на такие ключи завязаны словари
    классификатора и определение внутренних модулей.
    """
    if not module_name:
        return ""
    module_name = str(module_name)
    # Берём только имя файла из пути
    if '\\' in module_name or '/' in module_name:
        module_name = module_name.replace('\\', '/').split('/')[-1]
    if module_name.endswith('.dylib'):
        module_name = module_name[:-6]
    elif module_name.endswith('.framework'):
        module_name = module_name[:-10]
    elif '.dylib' in module_name:
        module_name = module_name.split('.dylib')[0]
    if module_name.startswith('@rpath/'):
        module_name = module_name[7:]
    return module_name


# ─────────────────────────────────────────────────────────────────
#  Классификатор модулей — единый источник истины, как в исполнении 1
# ─────────────────────────────────────────────────────────────────

from classifier.platform_classifier import classify_module as _classifier_describe
from classifier.categories import get_module_category_and_description
from classifier.system_modules import is_sfa_module as _classifier_is_sfa
from classifier.system_modules import is_system_module as _classifier_is_system
from classifier.system_modules import normalize_platform as _normalize_platform

# Неизвестная платформа → проверка по всем словарям (как в исполнении 1).
_UNKNOWN_DESC = "Неопознанный модуль"


def _classify_module(mod_name):
    """Категория и описание модуля — семантика исполнения 1
    (generator.generate_index): композитный классификатор по всем
    платформенным словарям + словари категорий. Без эвристических
    категорий: модуль вне словарей — «Неопознанные модули»."""
    try:
        desc = _classifier_describe(mod_name)
        cat, cat_desc = get_module_category_and_description(mod_name)
        if desc and desc != _UNKNOWN_DESC:
            return cat, desc
        if cat and cat != "Неопознанные модули":
            return cat, cat_desc
    except Exception:
        pass
    return "Неопознанные модули", _UNKNOWN_DESC


def _api_to_release(api):
    """Сопоставляет уровень Android API кодовому имени релиза (как в исполнении 1)."""
    mapping = {
        21: "5.0 Lollipop", 22: "5.1 Lollipop", 23: "6.0 Marshmallow",
        24: "7.0 Nougat", 25: "7.1 Nougat", 26: "8.0 Oreo",
        27: "8.1 Oreo", 28: "9.0 Pie", 29: "10", 30: "11",
        31: "12", 32: "12L", 33: "13", 34: "14", 35: "15",
    }
    return mapping.get(api, "")


def _file_info_entries(data):
    """Карточка «Информация о файле» (аналог ELFReportGenerator._build_file_info
    исполнения 1). Для ELF: идентификация → хеши → ELF-заголовок → динамическая
    компоновка → notes. Пустые строки пропускаются. Для PE — прежний вариант."""
    if data.get("is_elf"):
        hashes = data.get("hashes") or {}
        header = data.get("elf_header") or {}
        rows = [
            ("Имя файла", data.get("file_name", "")),
            ("Формат", data.get("format") or ""),
            ("Компилятор", data.get("compiler") or ""),
            ("Input SHA256", hashes.get("sha256", "")),
            ("Input MD5", hashes.get("md5", "")),
            ("Input CRC32", hashes.get("crc32", "")),
        ]
        if header:
            rows.append(("Разрядность", header.get("class", "")))
            rows.append(("Порядок байт", header.get("endianness", "")))
            rows.append(("Тип файла", header.get("type", "")))
            rows.append(("Архитектура", header.get("machine", "")))
            rows.append(("Точка входа", header.get("entry", "")))
            rows.append(("Флаги (e_flags)", header.get("flags", "")))
            rows.append(("Сегментов", str(header.get("program_headers", ""))))
            rows.append(("Секций", str(header.get("sections", ""))))
        rows.append(("Shared Name (SONAME)", data.get("soname") or ""))
        rows.append(("Interpreter (PT_INTERP)", data.get("interpreter") or ""))
        rows.append(("Library RPATH", data.get("rpath") or ""))
        rows.append(("Library RUNPATH", data.get("runpath") or ""))
        rows.append(("Build ID (GNU)", data.get("build_id") or ""))
        abi = data.get("abi_tag")
        if abi:
            rows.append(("ABI Tag (GNU)", abi))
        api = data.get("android_api")
        if api:
            release = _api_to_release(api)
            rows.append(("Android API Level",
                         f"{api} (Android {release})" if release else str(api)))
        return [(label, value) for label, value in rows if value]

    rows = []
    rows.append(("Имя файла", data.get("file_name", "")))
    rows.append(("Формат", data.get("format", "") or data.get("file_format", "")))
    rows.append(("Тип", data.get("type", "") or data.get("file_type", "")))
    rows.append(("Архитектура", data.get("arch", "") or data.get("processor", "")))
    rows.append(("Компилятор", data.get("compiler", "") or ""))
    rows.append(("Размер", str(data.get("file_size", ""))))
    hashes = data.get("hashes") or {}
    for k, v in [("SHA-256", "sha256"), ("MD5", "md5"), ("CRC32", "crc32")]:
        val = data.get(v) or hashes.get(v)
        if val:
            rows.append((k, val))
    return rows


# ─────────────────────────────────────────────────────────────────
#  Генератор «Общий анализ» (индивидуальные отчёты + индекс)
# ─────────────────────────────────────────────────────────────────

def _build_internal_set(input_dir):
    """Внутренние модули проекта: имена всех файлов исследуемой директории
    (аналог _build_internal_set исполнения 1)."""
    internal = set()
    root = Path(input_dir) if input_dir else None
    if root is None or not root.is_dir():
        return internal
    try:
        for f in root.rglob('*'):
            if f.is_file():
                internal.add(f.name.lower())
                internal.add(f.stem.lower())
    except OSError:
        pass
    return internal


# Метки и цвета категорий зависимостей в индивидуальных отчётах —
# как в исполнении 1 (BaseReportGenerator.CATEGORY_LABELS / CATEGORY_COLORS).
_CATEGORY_LABELS = {
    "Системные библиотеки ОС": "System",
    "Криптография и безопасность": "Crypto",
    "Сеть и коммуникации": "Network",
    "Графика и мультимедиа": "Graphics",
    "Среды выполнения, научные и ML-библиотеки": "Runtime",
    "Работа с данными, архивация и XML": "Data",
    "Внутренние модули проекта": "Internal",
    "Неопознанные модули": "Unknown",
}

_CATEGORY_COLORS = {
    "System": "#4CAF50",
    "Crypto": "#FF9800",
    "Network": "#2196F3",
    "Graphics": "#9C27B0",
    "Runtime": "#00BCD4",
    "Data": "#795548",
    "Internal": "#607D8B",
    "Unknown": "#F44336",
}


def _is_internal_module(module_name, internal_set):
    """Проверка «свой/чужой»: полное имя или имя без расширения (как в исп1)."""
    if internal_set is None:
        return False
    name_lower = module_name.lower()
    stem = os.path.splitext(module_name)[0].lower()
    return name_lower in internal_set or stem in internal_set


def _classify_with_context(module_name, internal_set):
    """Описание модуля с учётом внутреннего набора (как _classify_with_context исп1)."""
    if _is_internal_module(module_name, internal_set):
        return "Собственный модуль проекта (внутренняя библиотека)"
    try:
        desc = _classifier_describe(module_name)
        if desc:
            return desc
    except Exception:
        pass
    return "Неопознанный модуль"


def _classify_full(module_name, internal_set):
    """(метка категории, описание) для карточки зависимостей (как в исп1)."""
    desc = _classify_with_context(module_name, internal_set)
    if "Собственный модуль" in desc:
        return "Internal", desc
    cat_ru, _ = get_module_category_and_description(module_name)
    return _CATEGORY_LABELS.get(cat_ru, "Unknown"), desc


def generate_analysis_report(json_path, output_html, internal_set, reports_dir=None):
    """Генерирует индивидуальный HTML-отчёт «Общий анализ»."""
    with open(json_path, "r", encoding="utf-8") as f:
        data = json.load(f)

    # Заголовок отчёта — полный исходный путь (как в исполнении 1).
    file_name = data.get("file_name") or os.path.basename(str(json_path))
    imports = data.get("imports", [])
    exports = data.get("exports", [])

    # Ссылка «Назад к сводному отчёту» — детерминированно от reports_dir
    # (аналог compute_back_link исполнения 1): IDAReports/bin/x.html ->
    # ../index.html, IDAReports/x.html -> index.html. Поиск готового
    # index.html в родителях здесь неприменим: индекс пишется ПОСЛЕ
    # частных отчётов, на первом прогоне цикл ушёл бы до корня диска.
    if reports_dir is None:
        reports_dir = Path(output_html).parent
    back_link = _back_link(output_html, reports_dir)

    module_deps = []
    seen = set()
    if data.get("is_elf") or data.get("is_macho"):
        for needed in data.get("needed_libs", []):
            name = normalize_display_name(needed)
            if name in seen:
                continue
            seen.add(name)
            cat_label, desc = _classify_full(name, internal_set)
            color = _CATEGORY_COLORS.get(cat_label, "#9E9E9E")
            module_deps.append({"name": name, "count": 0, "category": cat_label,
                                "description": desc, "color": color})
        module_deps = sorted(module_deps, key=lambda x: (x["category"], x["name"]))
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
            cat_label, desc = _classify_full(name, internal_set)
            color = _CATEGORY_COLORS.get(cat_label, "#9E9E9E")
            module_deps.append({"name": name, "count": c, "category": cat_label,
                                "description": desc, "color": color})
        module_deps = sorted(module_deps, key=lambda x: (x["category"], x["name"]))

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
        "back_link": back_link,
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
    """Сегменты ELF: тип, права и словесное назначение (как в исполнении 1)."""
    from elf_descriptions import describe_segment
    segs = []
    for seg in data.get("elf_segments", []) or []:
        segs.append({"type": seg.get("type", ""), "flags": seg.get("flags", ""),
                     "description": describe_segment(seg.get("type", ""))})
    return segs


def _elf_sections(data):
    """Секции ELF: имя, тип, флаги и словесное назначение (как в исполнении 1)."""
    from elf_descriptions import describe_section
    secs = []
    for s in data.get("elf_sections", []) or []:
        name = s.get("name", "")
        display_name = name if name and not _is_placeholder_section(name) else "—"
        secs.append({"name": display_name, "type": s.get("type", ""),
                     "flags": s.get("flags", ""),
                     "description": describe_section(name, s.get("type", ""))})
    return secs


def _is_placeholder_section(name):
    """Служебная заглушка секции (индекс 0): пустое имя или '<0>'."""
    stripped = (name or "").strip()
    return not stripped or (stripped.startswith("<") and stripped.endswith(">"))


def _category_description(cat):
    """Описание категории из словарей классификатора (пусто для эвристических категорий)."""
    try:
        from classifier.categories import get_category_description
        return get_category_description(cat)
    except Exception:
        pass
    return ""


def _sorted_category_groups(categories):
    """Порядок категорий сводного индекса — как в исполнении 1: внутренние первыми,
    затем по алфавиту, «Неопознанные модули» в конце; модули внутри категории по алфавиту."""
    grouped = []
    internal = categories.pop("Внутренние модули проекта", None)
    if internal:
        internal["modules"] = sorted(internal["modules"], key=lambda x: x["name"].lower())
        grouped.append(internal)
    ordered = sorted(c for c in categories if c != "Неопознанные модули")
    if "Неопознанные модули" in categories:
        ordered.append("Неопознанные модули")
    for cat in ordered:
        info = categories[cat]
        info["modules"] = sorted(info["modules"], key=lambda x: x["name"].lower())
        grouped.append(info)
    return grouped


def generate_analysis_index(reports_dir, input_dir, report_links, global_modules,
                            ida_info, internal_set=None, total_files=0,
                            total_size_bytes=0, error_count=0, generation_time=""):
    """Сводный index.html для «Общего анализа»: категория со словесным описанием
    и описание у каждого модуля (как в исполнении 1, generator.generate_index)."""
    categories = {}
    for mod in global_modules:
        if _is_internal_module(mod, internal_set):
            cat = "Внутренние модули проекта"
            desc = "Собственный модуль проекта (внутренняя библиотека)"
            cat_desc = "Библиотеки и исполняемые файлы, находящиеся внутри исследуемой директории."
        else:
            cat, desc = _classify_module(mod)
            cat_desc = _category_description(cat)
        if cat not in categories:
            categories[cat] = {"name": cat, "description": cat_desc, "count": 0, "modules": []}
        info = categories[cat]
        info["count"] += 1
        info["modules"].append({"name": mod, "desc": desc})
    grouped = _sorted_category_groups(categories)

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

def _decode_bytes(data):
    """Декодирует байты из subprocess с автоопределением кодировки.

    На Windows npx (Node) может выводить в UTF-8 или в OEM (cp866) кодировке.
    Пробуем UTF-8, затем системную кодовую страницу, затем latin-1 (не падает).
    """
    if not data:
        return ""
    for enc in ("utf-8", "cp866", "cp1251", "latin-1"):
        try:
            return data.decode(enc)
        except (UnicodeDecodeError, LookupError):
            continue
    return data.decode("latin-1", errors="replace")


def _normalize_func_name(func_name):
    """Нормализует имя функции для поиска в Microsoft Learn.

    Для C++ имён вида ``std::basic_streambuf<...>::sputc(char)``
    извлекает последний сегмент верхнего уровня: ``sputc``.
    """
    name = (func_name or "").strip()
    if not name:
        return name

    depth = 0
    last_top_level_sep = -1
    for i, ch in enumerate(name):
        if ch in ("<", "(", "{"):
            depth += 1
        elif ch in (">", ")", "}"):
            depth -= 1
        elif ch == ":" and depth == 0 and i + 1 < len(name) and name[i + 1] == ":":
            last_top_level_sep = i

    if last_top_level_sep >= 0:
        rest = name[last_top_level_sep + 2:].strip()
        paren_idx = rest.find("(")
        if paren_idx >= 0:
            rest = rest[:paren_idx].strip()
        if rest:
            return rest

    paren_idx = name.find("(")
    if paren_idx >= 0:
        name = name[:paren_idx].strip()
    return name


def _sanitize_for_shell(func_name):
    """Экранирует имя функции для передачи npx через cmd.exe.

    npx.cmd — это cmd.exe-скрипт, поэтому ``<< >> | & ;`` ломают парсинг.
    Удаляем их полностью — для поиска это несущественно.
    """
    result = (func_name or "").replace("<<", "").replace(">>", "")
    result = result.replace("<", "").replace(">", "")
    result = result.replace("|", "").replace("&", "").replace(";", "")
    return result.strip()


_NPX_PATH_CACHE = None
_NPX_MISSING_LOGGED = False


def _find_npx():
    """Путь к npx: PATH, затем типовые каталоги Windows. Результат кэшируется."""
    global _NPX_PATH_CACHE
    if _NPX_PATH_CACHE is not None:
        return _NPX_PATH_CACHE or None
    npx = shutil.which("npx")
    if not npx:
        for p in (r"C:\Program Files\nodejs\npx.cmd",
                  r"C:\Program Files\nodejs\npx.exe",
                  r"C:\ProgramData\chocolatey\bin\npx.exe"):
            if Path(p).exists():
                npx = p
                break
    _NPX_PATH_CACHE = npx or ""
    return npx


_NO_WINDOW = getattr(subprocess, "CREATE_NO_WINDOW", 0)


def _kill_process_tree(proc):
    """Убивает процесс и всё его дерево (cmd.exe → node.exe). Без окна."""
    try:
        subprocess.run(
            ["taskkill", "/F", "/T", "/PID", str(proc.pid)],
            capture_output=True, timeout=10, creationflags=_NO_WINDOW,
        )
    except Exception:
        try:
            proc.kill()
        except Exception:
            pass


def _parse_npx_results(stdout):
    """Разбирает вывод ``learn-cli search`` — только первый результат
    (как в SfaReportGenerator._search_function исполнения 1)."""
    results = []
    lines = stdout.splitlines()
    i = 0
    while i < len(lines):
        line = lines[i]
        if re.match(r"^\[\d+\]", line):
            title_match = re.match(r"^\[\d+\]\s+(.+)$", line)
            title = title_match.group(1).strip() if title_match else "Untitled"
            url = ""
            if i + 1 < len(lines) and (lines[i + 1].strip().startswith("http://")
                                       or lines[i + 1].strip().startswith("https://")):
                url = lines[i + 1].strip()
                i += 1
            i += 1
            while i < len(lines) and lines[i].strip() == "":
                i += 1
            md_lines = []
            while i < len(lines) and not re.match(r"^\[\d+\]", lines[i]):
                md_lines.append(lines[i])
                i += 1
            markdown_text = "\n".join(md_lines).strip()
            if markdown_text:
                results.append({"title": title, "url": url,
                                "markdown": markdown_text, "markdown_html": ""})
            break  # только первый результат
        i += 1
    return results


def _extract_dll(results):
    """Вытаскивает ``Dll: xxx.dll`` из markdown-документации (если есть)."""
    for r in results or []:
        m = re.search(r"[Dd][Ll][Ll]\s*:\s*(\S+\.dll)", r.get("markdown", ""))
        if m:
            return m.group(1)
    return ""


# ─────────────────────────────────────────────────────────────────
#  Прямой клиент Microsoft Learn MCP (вместо npx-обёртки)
# ─────────────────────────────────────────────────────────────────

_MCP_ENDPOINT = "https://learn.microsoft.com/api/mcp"
_MCP_REQUEST_TIMEOUT = 30  # сек, на один запрос (не на запуск процесса)


def _mcp_session_cache_path():
    """Путь к кэшу сессии learn-cli (%LOCALAPPDATA%\\mslearn\\Cache\\...)."""
    local = os.environ.get("LOCALAPPDATA")
    if not local:
        return None
    return Path(local) / "mslearn" / "Cache" / "learn-mcp-cache.json"


def _mcp_load_cached_session():
    """sessionId и имя docsSearch-тула из кэша learn-cli (TTL 24 ч там же).

    learn-cli пишет туда session и mapping тулов при любом npx-вызове —
    переиспользуем, чтобы не делать handshake (initialize/tools). Если кэша
    нет — выполняем handshake сами и записываем в тот же файл.
    """
    path = _mcp_session_cache_path()
    try:
        if path and path.exists():
            data = json.loads(path.read_text(encoding="utf-8"))
            entry = (data.get("entries") or {}).get(_MCP_ENDPOINT)
            if entry:
                exp = entry.get("expiresAt")
                # learn-cli пишет UTC с суффиксом «Z»; naive now сравнивать с
                # aware нельзя — приводим к UTC.
                exp_dt = datetime.fromisoformat(exp.replace("Z", "+00:00")) if exp else None
                if exp_dt is not None and exp_dt.tzinfo is None:
                    exp_dt = exp_dt.replace(tzinfo=timezone.utc)
                if exp_dt is None or exp_dt > datetime.now(timezone.utc):
                    tools = entry.get("tools") or []
                    search_tool = ""
                    for t in tools:
                        tname = (t.get("name") if isinstance(t, dict) else t) or ""
                        if tname.lower().replace("_", "").replace("-", "") in (
                                "microsoftdocssearch", "docssearch", "docs_search"):
                            search_tool = tname
                            break
                    if not search_tool and tools:
                        # тулов в кэше нет (запись от нашего сохранения) —
                        # берём дефолтное имя Microsoft Learn MCP
                        search_tool = "microsoft_docs_search"
                    return entry.get("sessionId") or "", search_tool
    except Exception:
        pass
    return "", ""


def _mcp_save_cached_session(session_id, tool_name):
    """Сохраняет сессию в формат кэша learn-cli (или обновляет существующую запись)."""
    path = _mcp_session_cache_path()
    if not path or not session_id:
        return
    try:
        data = {"entries": {}}
        if path.exists():
            data = json.loads(path.read_text(encoding="utf-8"))
        entries = data.setdefault("entries", {})
        old = entries.get(_MCP_ENDPOINT) or {}
        entries[_MCP_ENDPOINT] = {
            "endpoint": _MCP_ENDPOINT,
            "sessionId": session_id,
            "tools": old.get("tools") or [{"name": tool_name}],
            "updatedAt": datetime.now().isoformat(),
            "expiresAt": (datetime.now() + timedelta(days=1)).isoformat(),
        }
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(data), encoding="utf-8")
    except Exception:
        pass


class McpDocClient:
    """Клиент Microsoft Learn MCP: постоянное соединение, запрос на функцию.

    Замена запуска npx на каждый поиск: learn-cli под капотом — это HTTP-клиент
    к learn.microsoft.com/api/mcp (Streamable HTTP, JSON-RPC). Мы вызываем тот
    же тул ``microsoft_docs_search`` тем же протоколом, результат идентичен
    (title/url/markdown), но без 4–5 с на разрешение пакета и boot node.
    Сессия (mcp-session-id) берётся из кэша learn-cli или создаётся handshake'ом
    и сохраняется обратно — последующие прогоны переиспользуют её.
    """

    def __init__(self):
        self._session_id = ""
        self._tool = ""
        self._lock = threading.Lock()
        self._rpc_id = 0

    def _headers(self, with_session):
        h = {
            "content-type": "application/json",
            "accept": "application/json, text/event-stream",
            "protocol-version": "2025-06-18",
        }
        if with_session and self._session_id:
            h["mcp-session-id"] = self._session_id
        return h

    def _post(self, payload, with_session=True):
        req = urllib.request.Request(
            _MCP_ENDPOINT, data=json.dumps(payload).encode("utf-8"),
            headers=self._headers(with_session), method="POST")
        with urllib.request.urlopen(req, timeout=_MCP_REQUEST_TIMEOUT) as resp:
            body = resp.read().decode("utf-8", "replace")
            sid = resp.headers.get("mcp-session-id") or ""
        return resp.status, sid, body

    @staticmethod
    def _sse_text(body):
        """Достаёт result из SSE-ответа (``data: {json}``)."""
        for line in body.splitlines():
            if line.startswith("data:"):
                try:
                    data = json.loads(line[5:].strip())
                except json.JSONDecodeError:
                    continue  # битая SSE-строка — не валим поиск в npx-фолбэк
                if "error" in data:
                    raise RuntimeError(data["error"].get("message", "MCP error"))
                result = data.get("result") or {}
                content = result.get("content") or []
                if content and content[0].get("type") == "text":
                    return content[0].get("text", "")
        return ""

    def search(self, query):
        """Поиск документации; возвращает список {title, url, markdown} —
        тот же формат, что _parse_npx_results (и исполнение 1)."""
        with self._lock:
            if not self._session_id or not self._tool:
                self._session_id, self._tool = _mcp_load_cached_session()
            if not self._tool:
                self._tool = "microsoft_docs_search"
            return self._search_once(query)

    def _search_once(self, query):
        payload = {"jsonrpc": "2.0", "id": self._next_id(), "method": "tools/call",
                   "params": {"name": self._tool, "arguments": {"query": query}}}
        try:
            _, sid, body = self._post(payload)
        except urllib.error.HTTPError as e:
            # сессия умерла — полный handshake и повтор
            if e.code in (400, 404):
                return self._reconnect_search(query)
            raise
        if sid:
            if sid != self._session_id:
                self._session_id = sid
                _mcp_save_cached_session(sid, self._tool)
        return self._parse_payload(self._sse_text(body))

    def _next_id(self):
        """Уникальный id JSON-RPC-запроса внутри клиента."""
        self._rpc_id += 1
        return self._rpc_id

    def _reconnect_search(self, query):
        """Полный handshake и повтор поиска (кэш сессии протух)."""
        import http.client as _hc
        conn = _hc.HTTPSConnection("learn.microsoft.com", timeout=_MCP_REQUEST_TIMEOUT)
        try:
            conn.request("POST", "/api/mcp",
                         body=json.dumps({"jsonrpc": "2.0", "id": 1, "method": "initialize",
                                          "params": {"protocolVersion": "2025-06-18",
                                                     "capabilities": {},
                                                     "clientInfo": {"name": "learn-cli", "version": "1.0.0"}}}),
                         headers={"content-type": "application/json",
                                  "accept": "application/json, text/event-stream"})
            resp = conn.getresponse()
            resp.read()
            self._session_id = resp.getheader("mcp-session-id") or ""
            if self._session_id:
                _mcp_save_cached_session(self._session_id, self._tool or "microsoft_docs_search")
        finally:
            conn.close()
        payload = {"jsonrpc": "2.0", "id": 2, "method": "tools/call",
                   "params": {"name": self._tool or "microsoft_docs_search",
                              "arguments": {"query": query}}}
        _, sid, body = self._post(payload)
        if sid:
            self._session_id = sid
        return self._parse_payload(self._sse_text(body))

    @staticmethod
    def _parse_payload(text):
        """JSON ответа MCP → формат результатов поиска (первый результат,
        как в исполнении 1 и _parse_npx_results)."""
        if not text:
            return []
        try:
            inner = json.loads(text)
        except json.JSONDecodeError:
            return []
        results = []
        for r in (inner.get("results") or [])[:1]:
            md = r.get("content", "")
            if not md:
                continue
            results.append({"title": r.get("title", ""),
                            "url": r.get("contentUrl", ""),
                            "markdown": md, "markdown_html": ""})
            break  # только первый результат
        return results


_MCP_CLIENT = None  # общий клиент (потокобезопасен: _search_once атомарен по lock)


def _get_mcp_client():
    global _MCP_CLIENT
    if _MCP_CLIENT is None:
        _MCP_CLIENT = McpDocClient()
    return _MCP_CLIENT


def _mcp_ping():
    """Доступность Learn MCP (любой HTTP-ответ считается успехом —
    GET возвращает 405, но это значит, что endpoint жив; как в probeEndpoint
    learn-cli)."""
    try:
        req = urllib.request.Request(_MCP_ENDPOINT, method="GET", headers={"accept": "application/json"})
        with urllib.request.urlopen(req, timeout=10):
            return True
    except urllib.error.HTTPError:
        return True  # 405 и т.п. — сервер отвечает
    except Exception:
        return False


class DocSearchManager:
    """Менеджер параллельного поиска документации MS Learn.

    - Пул потоков (по умолчанию 4): несколько функций ищутся одновременно.
    - «Тихие» вызовы: npx запускается с CREATE_NO_WINDOW — консольные окна
      не появляются (pythonw без консоли, а cmd.exe/npx.cmd иначе открывают
      своё окно на каждый вызов).
    - Отмена: cancel() убивает все запущенные процессы npx/node (дерево,
      ``taskkill /F /T``) и отменяет невыполненные задачи пула; потоки,
      ожидающие завершения процессов, разблокируются немедленно.
    """

    def __init__(self, log, max_workers=4):
        self._log = log
        self._executor = ThreadPoolExecutor(max_workers=max_workers)
        self._lock = threading.Lock()
        self._procs = set()
        self._cancelled = threading.Event()

    @property
    def cancelled(self):
        return self._cancelled.is_set()

    def submit(self, func_name, timeout=45):
        """Ставит поиск функции в пул. Возвращает None, если менеджер отменён."""
        if self._cancelled.is_set():
            return None
        return self._executor.submit(self._search, func_name, timeout)

    def cancel(self):
        """Останавливает поиск: убивает запущенные процессы, отменяет очередь."""
        if self._cancelled.is_set():
            return
        self._cancelled.set()
        with self._lock:
            procs = list(self._procs)
            self._procs.clear()
        for p in procs:
            _kill_process_tree(p)
        try:
            self._executor.shutdown(wait=False, cancel_futures=True)
        except Exception:
            pass

    def shutdown(self):
        """Нормальное завершение пула (без отмены)."""
        try:
            self._executor.shutdown(wait=False)
        except Exception:
            pass

    def _search(self, func_name, timeout):
        """Рабочая функция пула: поиск документации для одной функции.

        Основной путь — прямой MCP-вызов (learn.microsoft.com/api/mcp) из
        общего клиента: 4 потока пула постоянные, процессы не пересоздаются,
        ~1 с на функцию. Фолбэк — npx @microsoft/learn-cli (старое поведение,
        4–5 с только на запуск), если MCP-вызов не удался.
        """
        global _NPX_MISSING_LOGGED
        if self._cancelled.is_set():
            return []

        search_name = _normalize_func_name(func_name)
        safe_name = _sanitize_for_shell(search_name)
        if not safe_name:
            self._log(f"[WARN] {func_name}: пустое имя после нормализации")
            return []

        self._log(f"[INFO] Searching: {func_name} → {safe_name}")

        # 1) Прямой MCP-клиент (общий для всех потоков пула)
        client = _get_mcp_client()
        try:
            results = client.search(safe_name)
            if self._cancelled.is_set():
                return []
            if results:
                self._log(f"[INFO] Fetched {len(results)} results for {func_name}")
            else:
                self._log(f"[ERROR] No results for {func_name}")
            return results
        except Exception as e:
            if self._cancelled.is_set():
                return []
            self._log(f"[WARN] MCP search failed for {func_name}: {e} — fallback to npx")

        # 2) Фолбэк: npx (прежний путь)
        npx = _find_npx()
        if not npx:
            if not _NPX_MISSING_LOGGED:
                _NPX_MISSING_LOGGED = True
                self._log("[ERROR] npx not found. Please install Node.js and ensure it's in PATH.")
            return []

        try:
            proc = subprocess.Popen(
                [npx, "@microsoft/learn-cli", "search", safe_name],
                stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                creationflags=_NO_WINDOW,
            )
        except Exception as e:
            self._log(f"[ERROR] Exception starting npx for {func_name}: {e}")
            return []

        with self._lock:
            if self._cancelled.is_set():
                # Отмена пришла между запуском и регистрацией — гасим сразу.
                _kill_process_tree(proc)
                try:
                    proc.communicate(timeout=2)
                except Exception:
                    pass
                return []
            self._procs.add(proc)

        out_b = err_b = b""
        try:
            out_b, err_b = proc.communicate(timeout=timeout)
        except subprocess.TimeoutExpired:
            self._log(f"[ERROR] npx search timed out ({timeout}s) for {func_name}")
            _kill_process_tree(proc)
            try:
                out_b, err_b = proc.communicate(timeout=5)
            except Exception:
                out_b, err_b = b"", b""
        except Exception as e:
            self._log(f"[ERROR] Exception searching {func_name}: {e}")
            _kill_process_tree(proc)
        finally:
            with self._lock:
                self._procs.discard(proc)

        if self._cancelled.is_set():
            return []

        stdout = _decode_bytes(out_b)
        stderr = _decode_bytes(err_b)
        if proc.returncode not in (0, None):
            # Даже при ошибке пытаемся распарсить stdout (npx может выдать
            # результат на stdout, а предупреждения — на stderr)
            self._log(f"[WARN] npx search returned {proc.returncode}: {stderr[:200]}")
            if not stdout.strip():
                return []

        results = _parse_npx_results(stdout)
        if results:
            self._log(f"[INFO] Fetched {len(results)} results for {func_name}")
        else:
            self._log(f"[ERROR] No results for {func_name}")
        return results


# Мягкая отмена генерации: GUI присылает строку CANCEL в stdin.
_CANCEL_EVENT = threading.Event()
_DOC_MANAGER = None  # активный DocSearchManager (для stdin-наблюдателя)


def _cancel_requested(doc_manager):
    """Отмена запрошена (через stdin или напрямую менеджеру)."""
    return _CANCEL_EVENT.is_set() or (doc_manager is not None and doc_manager.cancelled)


def _watch_cancel_stdin():
    """Наблюдатель stdin: строка CANCEL — остановить поиск документации
    (убить процессы npx) и завершить генерацию."""
    try:
        if sys.stdin is None:
            return
        for line in sys.stdin:
            if line.strip().upper().startswith("CANCEL"):
                _CANCEL_EVENT.set()
                manager = _DOC_MANAGER
                if manager is not None:
                    manager.cancel()
                return
    except Exception:
        pass


class _DocCache:
    """Кэш документации MS Learn (SQLite).

    Схема совместима с исполнением 1 (таблицы ``functions``/``results`` из
    sfa_doc_cache.py): кэш, сформированный любым из исполнений, читается
    другим. Генерация в мосте однопоточная — блокировки не нужны.
    """

    def __init__(self, db_path):
        self._conn = sqlite3.connect(str(db_path))
        self._conn.execute("PRAGMA journal_mode=WAL")
        self._conn.execute("PRAGMA busy_timeout=5000")
        self._conn.executescript("""
            CREATE TABLE IF NOT EXISTS functions (
                name TEXT PRIMARY KEY,
                dll_name TEXT NOT NULL DEFAULT '',
                fetched_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS results (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                function_name TEXT NOT NULL,
                result_idx INTEGER NOT NULL,
                title TEXT NOT NULL DEFAULT '',
                url TEXT NOT NULL DEFAULT '',
                markdown TEXT NOT NULL DEFAULT '',
                markdown_html TEXT NOT NULL DEFAULT '',
                FOREIGN KEY (function_name) REFERENCES functions(name),
                UNIQUE(function_name, result_idx)
            );
            CREATE INDEX IF NOT EXISTS idx_results_fn ON results(function_name);
        """)
        self._conn.commit()

    def has_function(self, func_name):
        try:
            return self._conn.execute(
                "SELECT 1 FROM functions WHERE name = ?", (func_name,)
            ).fetchone() is not None
        except sqlite3.Error:
            return False

    def get_dll_name(self, func_name):
        try:
            row = self._conn.execute(
                "SELECT dll_name FROM functions WHERE name = ?", (func_name,)
            ).fetchone()
            return row[0] if row else ""
        except sqlite3.Error:
            return ""

    def get_results(self, func_name):
        try:
            rows = self._conn.execute(
                "SELECT title, url, markdown FROM results "
                "WHERE function_name = ? ORDER BY result_idx", (func_name,)
            ).fetchall()
        except sqlite3.Error:
            return []
        return [{"title": t, "url": u, "markdown": m, "markdown_html": ""}
                for t, u, m in rows]

    def save_results(self, func_name, results, dll_name=""):
        try:
            cur = self._conn.cursor()
            cur.execute(
                "INSERT OR REPLACE INTO functions (name, dll_name, fetched_at) VALUES (?, ?, ?)",
                (func_name, dll_name, datetime.now().isoformat(timespec="seconds")))
            cur.execute("DELETE FROM results WHERE function_name = ?", (func_name,))
            for idx, r in enumerate(results):
                cur.execute(
                    "INSERT OR REPLACE INTO results "
                    "(function_name, result_idx, title, url, markdown, markdown_html) "
                    "VALUES (?, ?, ?, ?, ?, ?)",
                    (func_name, idx, r.get("title", ""), r.get("url", ""),
                     r.get("markdown", ""), r.get("markdown_html", "")))
            self._conn.commit()
        except sqlite3.Error:
            pass

    def count(self):
        try:
            return self._conn.execute("SELECT COUNT(*) FROM functions").fetchone()[0]
        except sqlite3.Error:
            return 0

    def close(self):
        try:
            self._conn.close()
        except Exception:
            pass


def _manpages_candidates(func_name):
    """Варианты имени для поиска man-pages: без версии символа (@GLIBC_...)
    и без ведущих подчёркиваний — как ManPagesDatabase._candidates исполнения 1."""
    if not func_name:
        return ()
    name = func_name.strip().split("@", 1)[0]
    candidates = [name]
    stripped = name.lstrip("_")
    if stripped and stripped != name:
        candidates.append(stripped)
    return tuple(dict.fromkeys(candidates))


def _manpages_open(candidates, log):
    """Открывает БД man-pages: первый читаемый файл из списка кандидатов."""
    searched = []
    for raw in candidates:
        if not raw:
            continue
        p = Path(raw)
        if p.suffix.lower() != ".db":
            p = p / "manpages.db"
        searched.append(str(p))
        if not p.is_file():
            continue
        try:
            conn = sqlite3.connect(str(p))
            try:
                count = conn.execute("SELECT COUNT(*) FROM function_index").fetchone()[0]
            except sqlite3.Error:
                conn.close()
                raise
        except sqlite3.Error as e:
            log(f"[WARN] БД man-pages не читается ({p}): {e}")
            continue
        version = ""
        try:
            row = conn.execute("SELECT value FROM meta WHERE key = 'manpages_version'").fetchone()
            version = row[0] if row else ""
        except sqlite3.Error:
            pass
        log(f"[INFO] man-pages DB: {p} (функций: {count}, версия: {version})")
        return conn
    log("[WARN] БД man-pages не найдена (искали: "
        + (", ".join(searched) or "пути не заданы")
        + "). Документация Linux недоступна. Укажите путь в настройках "
          "и выполните синхронизацию man-pages.")
    return None


def _manpages_get_page(conn, func_name):
    """Документация функции из БД man-pages в формате search_results."""
    for candidate in _manpages_candidates(func_name):
        try:
            row = conn.execute(
                "SELECT p.page_name, p.section, p.title, p.markdown "
                "FROM function_index f JOIN pages p ON p.page_name = f.page_name "
                "WHERE f.func_name = ?", (candidate,)).fetchone()
        except sqlite3.Error:
            return None
        if row:
            page_name, section, title, markdown = row
            url = f"https://man7.org/linux/man-pages/man{section}/{page_name}.{section}.html"
            header = f"**{title}** — man {section}" if title else f"man {section}"
            body = f"{header}\n\n{markdown}" if markdown else header
            return {"title": f"{page_name}({section})", "url": url,
                    "markdown": body, "markdown_html": ""}
    return None


def _sfa_index_open(index_db):
    """Открывает индекс системных функций. None, если файла нет или он битый."""
    p = Path(index_db)
    if not p.is_file():
        return None
    conn = sqlite3.connect(str(p))
    try:
        conn.execute("SELECT 1 FROM system_functions LIMIT 1").fetchone()
        return conn
    except sqlite3.Error:
        conn.close()
        return None


def _sfa_index_is_known(conn, func_name):
    """Есть ли функция в индексе системных функций (фильтр 2 исполнения 1)."""
    if conn is None:
        return False
    try:
        return conn.execute(
            "SELECT 1 FROM system_functions WHERE func_name = ?", (func_name,)
        ).fetchone() is not None
    except sqlite3.Error:
        return False


def _sfa_index_totals(conn):
    """(модулей, функций) из индекса — фолбэк для счётчиков сводного отчёта."""
    if conn is None:
        return 0, 0
    try:
        modules = conn.execute("SELECT COUNT(*) FROM system_modules").fetchone()[0]
        functions = conn.execute("SELECT COUNT(*) FROM system_functions").fetchone()[0]
        return modules, functions
    except sqlite3.Error:
        return 0, 0


class _SfaLog:
    """Журнал генерации СФ (sfa_debug.log рядом с отчётами) — аналог _log
    SfaReportGenerator. Поиск документации идёт из пула потоков —
    запись защищена блокировкой."""

    def __init__(self, reports_dir):
        self._fh = None
        self._lock = threading.Lock()
        try:
            path = Path(reports_dir) / "sfa_debug.log"
            path.parent.mkdir(parents=True, exist_ok=True)
            self._fh = open(path, "w", encoding="utf-8")
            self.log(f"=== SFA Debug Log started at {datetime.now().isoformat()} ===")
        except OSError:
            self._fh = None

    def log(self, message):
        if self._fh is not None:
            with self._lock:
                try:
                    self._fh.write(message + "\n")
                    self._fh.flush()
                except Exception:
                    pass

    def close(self):
        if self._fh is not None:
            try:
                self._fh.close()
            except Exception:
                pass
            self._fh = None


def _back_link(output_html, reports_dir):
    """Относительная ссылка на index.html из вложенного отчёта (как
    compute_back_link исполнения 1)."""
    try:
        rel = Path(output_html).relative_to(Path(reports_dir))
    except ValueError:
        return "index.html"
    depth = len(rel.parts) - 1
    return "../" * depth + "index.html" if depth > 0 else "index.html"


def _is_system_module(mod_name, platform):
    """Определяет, системный ли модуль — по словарям классификатора
    (classifier/system_modules.py; единая точка принятия решения,
    аналогичная исполнению 1: словари платформ + префиксы API Sets)."""
    return _classifier_is_system(mod_name or "", platform or "")


def _is_sfa_module(mod_name, platform):
    """Модуль входит в отчёты СФ: категория «Системные библиотеки ОС».
    Отчёты СФ формируются только по системным библиотекам Windows-класса
    (kernel32, user32, …); криптография, сеть и runtime-словари — вне СФ."""
    return _classifier_is_sfa(mod_name or "", platform or "")


def generate_sfa_report(json_path, output_html, reports_dir, input_dir,
                        platform, data_override=None, reuse_cache=False,
                        doc_cache=None, manpages_conn=None, sfa_index_conn=None,
                        log=None, progress=None, doc_manager=None,
                        doc_progress=None):
    """Индивидуальный HTML-отчёт «Анализ СФ» — перенос логики
    SfaReportGenerator.generate_report_from_json исполнения 1.

    Отбор функций ведётся по системным библиотекам платформы:
      * Windows — только импорты из системных библиотек, плюс проверка по
        индексу системных функций (sfa_function_index.db); документация —
        Microsoft Learn (npx @microsoft/learn-cli) с кэшем mslearn_cache.db;
      * Linux / Android — отчёт содержит полный перечень импортов;
        документация — локальная БД man-pages (без сети); системность
        функции псевдо-модуля (.dynsym) подтверждается наличием документации.

    Поиск документации (npx) идёт параллельно через DocSearchManager:
    сначала фильтрация и кэш/man-pages (быстро), затем пул запросов для
    функций без документации, затем сборка отчёта в исходном порядке.

    Args:
        data_override: данные (file_name, imports, needed_libs, ...) для
            reuse-режима, когда JSON уже удалён.
        reuse_cache: True — не вызывать npx, только кэш.
        doc_cache: открытый _DocCache (создаётся на весь прогон).
        manpages_conn: открытое соединение БД man-pages (для Linux/Android).
        sfa_index_conn: открытое соединение индекса системных функций.
        log: колбэк журнала (sfa_debug.log).
        progress: колбэк (func_name, idx, total_in_file) — проход по импортам.
        doc_manager: DocSearchManager для параллельного поиска (Windows).
        doc_progress: колбэк (func_name, done, total) — получение документации.
    Returns:
        dict статистики или None, если генерация отменена.
    """
    if log is None:
        log = lambda msg: None
    if data_override is not None:
        data = data_override
    else:
        with open(json_path, "r", encoding="utf-8") as f:
            data = json.load(f)

    platform = _normalize_platform(platform or "Windows")
    use_manpages = platform == "Linux / Android"
    list_all_imports = use_manpages
    docs_available = platform == "Windows"

    file_name = os.path.basename(data.get("file_name", str(json_path)))
    imports = data.get("imports", [])
    needed_libs = data.get("needed_libs", []) or []
    total_imports = len(imports)
    valid_imports = sum(1 for imp in imports if imp.get("name"))
    log(f"[INFO] Processing {json_path}")
    log(f"[INFO] Found {total_imports} imports in {file_name}")

    # ─── Фаза 1: фильтрация; кэш/man-pages сразу, npx — в пул менеджера ───
    rows = []       # строки после фильтров (в исходном порядке импортов)
    pending = {}    # func_name -> future поиска MS Learn (без дубликатов)
    skipped = 0
    skipped_not_in_index = 0

    for idx, imp in enumerate(imports):
        func_name = imp.get("name")
        if not func_name:
            continue
        module = imp.get("module", "") or ""

        # Псевдо-модуль ELF (.dynsym): библиотека неизвестна — системность
        # определяется по наличию документации.
        is_pseudo_module = module.strip().lower() in (".dynsym", ".dynsec", "unknown", "")
        module_is_system = _is_sfa_module(module, platform)

        # Фильтр 1 (Windows): несистемные библиотеки пропускаются. Для
        # псевдо-модулей проверка откладывается до поиска документации.
        # Linux/Android фильтр не применяется — список импортов выводится целиком.
        if not list_all_imports and not module_is_system and not is_pseudo_module:
            log(f"[DEBUG] {func_name} ({module}) — не системная библиотека ({platform})")
            skipped += 1
            continue

        # Фильтр 2 (Windows): функция должна быть в индексе системных функций.
        if (not list_all_imports and sfa_index_conn is not None
                and not is_pseudo_module
                and not _sfa_index_is_known(sfa_index_conn, func_name)):
            log(f"[DEBUG] {func_name} — нет в индексе системных функций")
            skipped_not_in_index += 1
            skipped += 1
            continue

        if progress is not None:
            progress(func_name, idx, total_imports)

        dll_name = module or "—"
        if dll_name.strip().lower() in (".dynsym", ".dynsec", "unknown"):
            dll_name = "—"

        results = None
        found = False
        needs_doc = False
        if doc_cache is not None and doc_cache.has_function(func_name):
            results = doc_cache.get_results(func_name)
            cached_dll = doc_cache.get_dll_name(func_name)
            if cached_dll:
                dll_name = cached_dll
            found = bool(results)
            log(f"[INFO] Using cached results for {func_name} (count: {len(results)})")
        elif use_manpages:
            if manpages_conn is not None:
                page = _manpages_get_page(manpages_conn, func_name)
                if page:
                    results = [page]
                    found = True
                    log(f"[INFO] man-pages: {func_name} → {page['title']}")
                else:
                    log(f"[INFO] man-pages: страница для {func_name} не найдена")
            else:
                log(f"[INFO] {func_name}: БД man-pages недоступна")
        elif reuse_cache and doc_manager is not None:
            # Reuse-режим с добором: кэш неполон (записи от прошлых прогонов,
            # поиск мог быть прерван) — недостающую документацию ищем в npx,
            # как в исполнении 1. Функция уходит в пул вместе с остальными.
            if func_name not in pending:
                fut = doc_manager.submit(func_name)
                if fut is None:
                    log(f"[INFO] {func_name}: поиск отменён")
                else:
                    pending[func_name] = fut
            needs_doc = True
        elif reuse_cache:
            # Reuse-режим без менеджера поиска (npx недоступен): функция
            # остаётся not-found.
            log(f"[INFO] {func_name} не в кэше (reuse_cache=True) — пропуск")
        elif not docs_available:
            log(f"[INFO] {func_name}: поиск документации недоступен для {platform}")
        elif doc_manager is not None:
            # Документации нет в кэше — поиск уйдёт в пул параллельно.
            # (На этой ветке results/found гарантированно не установлены,
            # так что needs_doc всегда True — как в reuse-ветке выше.)
            if func_name not in pending:
                fut = doc_manager.submit(func_name)
                if fut is None:
                    log(f"[INFO] {func_name}: поиск отменён")
                else:
                    pending[func_name] = fut
            needs_doc = True
        else:
            log(f"[INFO] {func_name}: поиск документации недоступен")

        rows.append({
            "func": func_name,
            "module": module,
            "address": imp.get("address", ""),
            "dll": dll_name,
            "is_pseudo": is_pseudo_module,
            "module_is_system": module_is_system,
            "results": results,
            "found": found,
            "needs_doc": needs_doc,
        })

    # ─── Фаза 2: параллельное ожидание поисков документации ───
    resolved = {}   # func_name -> (results, dll_from_markdown)
    if pending:
        fut_to_func = {fut: fn for fn, fut in pending.items()}
        done_docs = 0
        for fut in as_completed(list(fut_to_func)):
            if _cancel_requested(doc_manager):
                break
            func_name = fut_to_func[fut]
            try:
                results = fut.result()
            except Exception as e:
                log(f"[ERROR] Doc search failed for {func_name}: {e}")
                results = []
            done_docs += 1
            if doc_progress is not None:
                doc_progress(func_name, done_docs, len(pending))
            dll_from_md = ""
            if results:
                dll_from_md = _extract_dll(results)
                if doc_cache is not None:
                    try:
                        doc_cache.save_results(func_name, results, dll_name=dll_from_md)
                        log(f"[INFO] Fetched and cached {len(results)} results for {func_name}")
                    except Exception as e:
                        log(f"[WARN] Failed to save cache: {e}")
            resolved[func_name] = (results, dll_from_md)

        if _cancel_requested(doc_manager):
            log("[INFO] Cancelled: doc search stopped")
            return None

    # ─── Фаза 3: сборка строк отчёта в исходном порядке импортов ───
    entries = []
    for row in rows:
        func_name = row["func"]
        dll_name = row["dll"]
        results = row["results"]
        found = row["found"]
        if row["needs_doc"]:
            results, dll_from_md = resolved.get(func_name, ([], ""))
            found = bool(results)
            if dll_from_md:
                dll_name = dll_from_md

        # Системность: известная системная библиотека либо псевдо-модуль,
        # для которого нашлась документация.
        if row["module_is_system"]:
            is_system_call = True
        elif row["is_pseudo"]:
            is_system_call = found
        else:
            is_system_call = False

        # Псевдо-модуль без документации в Windows-режиме в отчёт не попадает.
        if not list_all_imports and row["is_pseudo"] and not found:
            log(f"[INFO] {func_name}: не подтверждена как системная (нет документации)")
            skipped += 1
            continue

        entries.append({
            "name": func_name,
            "dll": dll_name,
            "address": row["address"],
            "module": row["module"],
            "search_results": results or [],
            "found": found,
            "is_system": is_system_call,
        })

    log(f"[INFO] Generated {len(entries)} rows "
        f"({sum(1 for e in entries if e['is_system'])} system; "
        f"skipped {skipped} as non-system, {skipped_not_in_index} not in index)")

    # Счётчики для индексного отчёта ведутся по системным функциям (для
    # Linux/Android в частном отчёте перечислены все импорты — без этого
    # «документация не найдена» смешивало бы системные функции с чужими).
    system_entries = [e for e in entries if e["is_system"]]
    system_found = [e for e in system_entries if e["found"]]
    found_count = len(system_found)
    notfound_count = len(system_entries) - found_count
    notfound_names = {e["name"] for e in entries if not e["found"]}
    system_notfound_names = {e["name"] for e in system_entries if not e["found"]}
    # Системные библиотеки модуля: для ELF источник — зависимости (DT_NEEDED);
    # в list_all-режиме дополняем библиотеками подтверждённых системных строк.
    # Критерий — СФ-библиотеки («Системные библиотеки ОС»), тот же, что у индекса.
    system_libs = {lib for lib in needed_libs if _is_sfa_module(lib, platform)}
    if list_all_imports:
        for e in system_entries:
            dll = e["dll"]
            if dll and dll != "—" and _is_sfa_module(dll, platform):
                system_libs.add(dll)

    ctx = {
        "file_name": file_name,
        "back_link": _back_link(output_html, reports_dir),
        "platform": _platform_label(platform),
        "system_calls": entries,
        "list_all_imports": list_all_imports,
        "docs_available": docs_available,
        "marked_js": _marked_js(),
    }
    text = render_template("sfa_report.html", **ctx)
    os.makedirs(os.path.dirname(output_html), exist_ok=True)
    with open(output_html, "w", encoding="utf-8") as f:
        f.write(text)
    log(f"[INFO] Report saved to {output_html}")

    return {
        "found_count": found_count,
        "notfound_count": notfound_count,
        "total_count": len(entries),
        "notfound_names": notfound_names,
        "total_imports": valid_imports,
        "system_count": len(system_entries),
        "system_names": {e["name"] for e in system_entries},
        "system_libs": system_libs,
        "system_notfound_names": system_notfound_names,
    }


def _platform_label(platform):
    return {"Windows": "Windows", "Linux": "Linux", "Linux / Android": "Linux / Android",
            "macOS / iOS": "macOS / iOS"}.get(platform, platform)


def _marked_js():
    """Встроенный marked.min.js (без внешних файлов)."""
    return inline_vendor("marked.min.js")


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

def generate_diff_report(json_path, output_html, reports_dir):
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
        "back_link": _back_link(output_html, reports_dir),
        "error": data.get("error"),
        # `or {}` — ключ может присутствовать со значением null (generate_diff_index
        # защищается от того же случая isinstance-проверкой).
        "file1": data.get("file1") or {},
        "file2": data.get("file2") or {},
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
    """Сводный отчёт сравнения (перенос доработок исполнения 1: d35a4f2/ee9d761).

    Показатель совпадения: hexdump 100% — hexdump-схожесть; два движка с
    уникальными парами Diaphora — доля найденных функций; иначе sim BinDiff.
    Имя пары — реальный исполняемый файл (без .i64), среднее — по display_sim.
    """
    pairs = []
    total_similarity = 0.0
    total_confidence = 0.0
    count = 0
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

        real_prim = data.get("real_primary", "")
        # Имя реального исполняемого файла (без .i64)
        if real_prim:
            real_name = os.path.basename(real_prim)
        else:
            real_name = stem.replace("_i64", "")

        sim = float(data.get("similarity", 0.0) or 0.0)
        conf = float(data.get("confidence", 0.0) or 0.0)
        hd_sim = float(data.get("hexdump_similarity", 0.0) or 0.0)
        eng = str(data.get("engine", "bindiff") or "bindiff")
        total1 = int(data.get("total_functions1", 0) or 0)
        matching = data.get("matched_summary", {})
        matched = len(data.get("matched_functions", []) or [])
        if not matched and isinstance(matching, dict):
            matched = (int(matching.get("bindiff_only", 0) or 0)
                       + int(matching.get("diaphora_only", 0) or 0)
                       + int(matching.get("both", 0) or 0))
        diaphora_only = int(matching.get("diaphora_only", 0) or 0) if isinstance(matching, dict) else 0
        diaphora_confirmed = int(matching.get("both", 0) or 0) if isinstance(matching, dict) else 0

        if "bindiff" in eng:
            has_bindiff = True
        if "diaphora" in eng:
            has_diaphora = True

        # Если hexdump 100% — hexdump-схожесть. Если оба движка и Diaphora
        # добавила уникальные пары — доля функций, найденных в сумме. Иначе
        # (BinDiff-only или Diaphora не добавила нового) — sim BinDiff.
        if hd_sim >= 1.0 or total1 == 0:
            display_sim = hd_sim
        elif eng == "bindiff+diaphora" and diaphora_only > 0:
            display_sim = matched / total1 if total1 else 0.0
        else:
            display_sim = sim

        pairs.append({
            "stem": real_name,
            "similarity": sim,
            "display_similarity": display_sim,
            "hexdump_similarity": hd_sim,
            "confidence": conf,
            "matched_count": matched,
            "total_funcs1": total1,
            "hash1": data.get("file1", {}).get("hash", "") if isinstance(data.get("file1"), dict) else "",
            "hash2": data.get("file2", {}).get("hash", "") if isinstance(data.get("file2"), dict) else "",
            "engine": eng,
            "diaphora_matched_count": diaphora_only,
            "diaphora_confirmed_count": diaphora_confirmed,
            "report_filename": stem + ".html",
        })
        total_similarity += display_sim
        total_confidence += conf
        count += 1

    avg_sim = total_similarity / count if count else 0.0
    avg_conf = total_confidence / count if count else 0.0
    ctx = {
        "left_dir": left_dir,
        "right_dir": right_dir,
        "generation_time": generation_time,
        "ida_version": ida_version,
        "total_pairs": count,
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
    """Канонический ключ модуля — как в исполнении 1
    (classifier.naming.normalize_module_name): путь, одно платформенное
    расширение, нижний регистр; версии сохраняются (libc.so.6 → libc.so.6)."""
    from classifier.naming import normalize_module_name
    return normalize_module_name(module)


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
                    file_size = _stat_size_safe(alongside)
            if not file_size and src.exists():
                file_size = _stat_size_safe(src)

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
                # В СФ-индекс — только СФ-библиотеки (категория «Системные
                # библиотеки ОС»): криптография, сеть и runtime вне СФ.
                if _is_sfa_module(module, platform):
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
    global _DOC_MANAGER
    input_dir = Path(args.input_dir)
    reports_dir = Path(args.reports_dir)

    if args.kind == "analysis":
        json_files = _collect_export_jsons(args.json_dir, args.json_paths)
        if not json_files:
            emit_error("Нет JSON-файлов экспорта. Сначала выполните анализ.")
            return
        reports_dir.mkdir(parents=True, exist_ok=True)
        # Внутренние модули проекта — все файлы исследуемой директории (как в исполнении 1).
        internal_set = _build_internal_set(input_dir)
        total = len(json_files)
        done = 0
        report_links = []
        global_modules = set()
        ida_info = None
        total_files = 0
        total_size = 0

        for jp in sorted(json_files, key=_stat_size_safe, reverse=True):
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
            # Per-file guard (как в diff-ветке): один битый JSON/шаблон не должен
            # валить весь прогон — остальные отчёты генерируются дальше.
            try:
                generate_analysis_report(str(jp), str(output_html), internal_set,
                                         reports_dir=str(reports_dir))
            except Exception as e:
                emit_error(f"Ошибка генерации отчёта {jp.name}: {e}")
                done += 1
                emit_progress(done, total, "")
                continue

            link = out_rel.as_posix()
            display = rel.as_posix()
            file_size = int(data.get("file_size") or 0)
            if not file_size:
                alongside = jp.parent / original
                if alongside.exists():
                    file_size = _stat_size_safe(alongside)
                elif source_full.exists():
                    file_size = _stat_size_safe(source_full)

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
        platform = _normalize_platform(args.platform)

        if args.reuse_cache:
            # Читаем пути из index-БД (JSON могут быть удалены)
            json_files = [Path(p) for p in read_index_json_paths(sfa_index_db)]
            if not json_files:
                emit_error("Индекс БД устарел или не содержит данных. Выполните полный анализ для перестроения индекса.")
                return
            # Платформа берётся из индекса: при перегенерации должны
            # использоваться те же словари, что и в анализе.
            try:
                conn = sqlite3.connect(str(sfa_index_db))
                try:
                    row = conn.execute("SELECT value FROM meta WHERE key='platform'").fetchone()
                finally:
                    conn.close()
                if row and row[0]:
                    platform = _normalize_platform(row[0])
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

        reports_dir.mkdir(parents=True, exist_ok=True)
        log = _SfaLog(reports_dir)

        # Кэш документации MS Learn открывается/создаётся всегда (как в
        # исполнении 1): в него складываются результаты npx-поиска, из него
        # берётся документация в reuse-режиме.
        doc_cache = None
        try:
            doc_cache = _DocCache(reports_dir / "mslearn_cache.db")
            log.log(f"[INFO] MS Learn cache: {reports_dir / 'mslearn_cache.db'} ({doc_cache.count()} функций)")
        except Exception as e:
            log.log(f"[WARN] Failed to open cache DB: {e}")

        # Путь к man-pages: из настроек, затем рядом с папкой отчётов и выше.
        manpages_conn = _manpages_open(
            [args.manpages_db,
             reports_dir / "manpages.db",
             reports_dir.parent / "manpages.db"],
            log.log)

        sfa_index_conn = _sfa_index_open(sfa_index_db)

        # npx нужен только как фолбэк: основной путь — прямой MCP-вызов
        # (learn.microsoft.com/api/mcp), которому Node.js не требуется.
        npx_ready = _find_npx() is not None
        if platform == "Windows" and not npx_ready and not _mcp_ping():
            emit_error("npx (Node.js) не найден и Microsoft Learn MCP недоступен — "
                       "документация будет отсутствовать.")

        # Менеджер параллельного поиска документации (Windows): 4 постоянных
        # потока, прямой MCP-вызов (фолбэк — npx), отмена с прерыванием
        # запросов. Создаётся и в reuse-режиме — для добора отсутствующей
        # в кэше документации.
        doc_manager = None
        if platform == "Windows":
            doc_manager = DocSearchManager(log.log, max_workers=4)
            _DOC_MANAGER = doc_manager
            if args.reuse_cache:
                emit_progress(0, 0, "Перегенерация из кэша (недостающая документация будет найдена автоматически)…")

        if not args.reuse_cache:
            _, funcs_cnt = _sfa_index_totals(sfa_index_conn)
            emit_progress(0, max(len(json_files), 1),
                          f"Найдено {funcs_cnt} системных функций. Генерация HTML…")

        total = len(json_files)
        done = 0
        report_links = []
        ida_info = {}
        total_size = 0
        total_imports_all = 0
        gsf, gsl, gsn = set(), set(), set()

        for jp in sorted(json_files, key=_stat_size_safe, reverse=True):
            if _cancel_requested(doc_manager):
                break
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

            # Пофункционный прогресс внутри текущего файла (как в исполнении 1)
            def on_func(func_name, func_idx, total_in_file):
                emit_progress(done, total,
                              f"{rel.as_posix()} → {func_name} ({func_idx + 1}/{total_in_file})")

            # Прогресс получения документации (параллельный поиск MS Learn)
            def on_doc(func_name, done_docs, total_docs):
                emit_progress(done, total,
                              f"{rel.as_posix()} → документация: {func_name} ({done_docs}/{total_docs})")

            # Per-file guard: один битый JSON/шаблон не валит весь прогон СФ
            # (stats is None — отмена; исключение — продолжаем с другими файлами).
            try:
                stats = generate_sfa_report(
                    str(jp), str(output_html), str(reports_dir), str(input_dir),
                    platform,
                    data_override=data if args.reuse_cache and not jp.exists() else None,
                    reuse_cache=args.reuse_cache,
                    doc_cache=doc_cache, manpages_conn=manpages_conn,
                    sfa_index_conn=sfa_index_conn, log=log.log, progress=on_func,
                    doc_manager=doc_manager, doc_progress=on_doc)
            except Exception as e:
                emit_error(f"Ошибка генерации отчёта {os.path.basename(str(jp))}: {e}")
                done += 1
                emit_progress(done, total, "")
                continue

            if stats is None:
                # Отмена: без сводного отчёта и результата
                break

            file_size = int(data.get("file_size") or 0)
            if not file_size and source_full.exists():
                file_size = _stat_size_safe(source_full)
            if not file_size:
                # для reuse — из index БД
                try:
                    conn = sqlite3.connect(str(sfa_index_db))
                    try:
                        row = conn.execute(
                            "SELECT file_size FROM file_imports WHERE json_path = ? LIMIT 1",
                            (str(jp),)).fetchone()
                    finally:
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

        # Отмена во время генерации: гасим поиск и выходим без результата
        if _cancel_requested(doc_manager):
            if doc_manager is not None:
                doc_manager.cancel()
            if doc_cache is not None:
                doc_cache.close()
            if manpages_conn is not None:
                manpages_conn.close()
            if sfa_index_conn is not None:
                sfa_index_conn.close()
            log.close()
            emit_error("Генерация отчётов СФ отменена пользователем.")
            return

        # Счётчики сводного отчёта — по уникальным множествам; пустые
        # дополняются из индекса (аналог фолбэка исполнения 1).
        total_system_modules = len(gsl)
        total_system_functions = len(gsf)
        idx_modules, idx_functions = _sfa_index_totals(sfa_index_conn)
        if not total_system_modules:
            total_system_modules = idx_modules
        if not total_system_functions:
            total_system_functions = idx_functions

        report_links.sort(key=lambda r: r["display_name"])
        index_path = generate_sfa_index(
            str(reports_dir), str(input_dir), report_links, ida_info,
            total_files=total, total_size_bytes=total_size,
            total_system_modules=total_system_modules,
            total_system_functions=total_system_functions,
            total_system_notfound=len(gsn), total_imports=total_imports_all,
            generation_time=datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
            platform=platform)

        if doc_manager is not None:
            doc_manager.shutdown()
            _DOC_MANAGER = None
        if doc_cache is not None:
            doc_cache.close()
        if manpages_conn is not None:
            manpages_conn.close()
        if sfa_index_conn is not None:
            sfa_index_conn.close()
        log.close()
        emit_result({
            "reports_dir": str(reports_dir), "input_dir": str(input_dir),
            "index_path": str(index_path), "generated_count": len(report_links),
            "total_files": total, "total_size_bytes": total_size,
            "total_system_modules": total_system_modules,
            "total_system_functions": total_system_functions,
            "total_system_notfound": len(gsn), "total_imports": total_imports_all,
            "platform": platform,
        })

    elif args.kind == "sfa-docs":
        """Предварительный поиск документации MS Learn — выполняется по кнопке
        «Запустить анализ СФ» (после анализа и экспорта), чтобы генерация
        отчётов СФ шла только по кэшу, без вызовов npx и консольных окон.
        Прогресс — по функциям; отмена — через stdin (CANCEL)."""
        sfa_index_db = reports_dir / "sfa_function_index.db"
        platform = _normalize_platform(args.platform)
        json_files = [Path(p) for p in _split(args.json_paths)] if args.json_paths else \
            _collect_export_jsons(args.json_dir, args.json_paths)
        if not json_files:
            emit_error("Нет JSON-файлов экспорта. Сначала выполните анализ.")
            return
        reports_dir.mkdir(parents=True, exist_ok=True)
        log = _SfaLog(reports_dir)

        if platform != "Windows":
            # Linux/Android: документация локальная (man-pages), ищется
            # непосредственно при генерации отчёта — предварительный поиск не нужен.
            log.close()
            emit_result({"searched": 0, "found": 0, "cached_total": 0,
                         "total_funcs": 0, "platform": platform})
            return

        emit_progress(0, 1, "Сканирование системных функций…")
        try:
            build_sfa_index(sfa_index_db, json_files, platform)
        except Exception as e:
            emit_error(f"Ошибка сканирования системных функций: {e}")

        sfa_index_conn = _sfa_index_open(sfa_index_db)
        doc_cache = None
        try:
            doc_cache = _DocCache(reports_dir / "mslearn_cache.db")
            log.log(f"[INFO] MS Learn cache: {reports_dir / 'mslearn_cache.db'} ({doc_cache.count()} функций)")
        except Exception as e:
            log.log(f"[WARN] Failed to open cache DB: {e}")

        # npx — только фолбэк: основной путь поиска — прямой MCP-вызов
        if _find_npx() is None and not _mcp_ping():
            emit_error("npx (Node.js) не найден и Microsoft Learn MCP недоступен — "
                       "функции будут отмечены как «без документации».")

        # Уникальные функции, требующие документации: из системных модулей и
        # псевдо-модулей (.dynsym), за вычетом уже кэшированных.
        funcs = []
        seen = set()
        for jp in sorted(json_files, key=_stat_size_safe, reverse=True):
            if _cancel_requested(None):
                break
            try:
                with open(jp, "r", encoding="utf-8") as f:
                    data = json.load(f)
            except Exception:
                continue
            for imp in data.get("imports", []):
                fn = imp.get("name")
                if not fn or fn in seen:
                    continue
                module = (imp.get("module") or "").strip()
                is_pseudo = module.lower() in (".dynsym", ".dynsec", "unknown", "")
                if not is_pseudo and not _is_sfa_module(module, platform):
                    continue
                if (not is_pseudo and sfa_index_conn is not None
                        and not _sfa_index_is_known(sfa_index_conn, fn)):
                    continue
                if doc_cache is not None and doc_cache.has_function(fn):
                    continue
                seen.add(fn)
                funcs.append(fn)

        total_funcs = len(funcs)
        log.log(f"[INFO] Doc pre-search: {total_funcs} functions to search")
        emit_progress(0, max(total_funcs, 1), f"Функций к поиску: {total_funcs}")

        manager = DocSearchManager(log.log, max_workers=4)
        _DOC_MANAGER = manager
        done = 0
        found_cnt = 0
        futs = {}
        for fn in funcs:
            fut = manager.submit(fn)
            if fut is None:
                break
            futs[fut] = fn

        for fut in as_completed(list(futs)) if futs else []:
            if _cancel_requested(manager):
                break
            fn = futs[fut]
            try:
                results = fut.result()
            except Exception as e:
                log.log(f"[ERROR] Doc search failed for {fn}: {e}")
                results = []
            done += 1
            if results:
                found_cnt += 1
                if doc_cache is not None:
                    try:
                        doc_cache.save_results(fn, results, dll_name=_extract_dll(results))
                    except Exception:
                        pass
            emit_progress(done, max(total_funcs, 1), f"Документация: {fn}")

        cancelled = _cancel_requested(manager)
        if cancelled:
            manager.cancel()
        else:
            manager.shutdown()
        _DOC_MANAGER = None
        cached_total = doc_cache.count() if doc_cache is not None else 0
        if doc_cache is not None:
            doc_cache.close()
        if sfa_index_conn is not None:
            sfa_index_conn.close()
        log.close()
        if cancelled:
            emit_error("Поиск документации отменён пользователем.")
            return
        emit_result({
            "reports_dir": str(reports_dir), "input_dir": str(input_dir),
            "searched": done, "found": found_cnt, "cached_total": cached_total,
            "total_funcs": total_funcs, "platform": platform,
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
                generate_diff_report(str(jf), str(html_path), str(reports_dir))
            except Exception as e:
                emit_error(f"Ошибка генерации отчёта {jf.name}: {e}")
            done += 1
            emit_progress(done, total, jf.name)

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
    p.add_argument("--kind", required=True, choices=["analysis", "sfa", "sfa-docs", "diff"])
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

    # Слежение за stdin для мягкой отмены (GUI присылает CANCEL)
    try:
        threading.Thread(target=_watch_cancel_stdin, daemon=True).start()
    except Exception:
        pass

    try:
        if args.command == "generate":
            run_generate(args)
    except Exception as e:
        emit_error(f"{type(e).__name__}: {e}")
        sys.exit(1)


if __name__ == "__main__":
    main()