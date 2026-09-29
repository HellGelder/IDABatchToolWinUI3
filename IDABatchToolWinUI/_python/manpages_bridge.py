"""
Самодостаточная синхронизация man-pages для исполнения 2 (IDABatchToolWinUI).

Скачивает официальный архив man-pages с kernel.org, разбирает секции 2 и 3
(системные вызовы и библиотечные функции), импортирует в SQLite-БД со схемой,
совместимой с поиском документации в отчётах СФ:
    pages(page_name, section, title, library, summary, markdown)
    function_index(func_name, page_name, section)
    meta(key, value)

Используется ТОЛЬКО стандартная библиотека + requests (внешняя зависимость,
не исполнение 1).

Протокол:
    PROGRESS <pct> <message>
    ERROR <message>
    DONE <path>
"""
import io
import os
import re
import sqlite3
import sys
import tarfile
from pathlib import Path

# Переносимая поставка: если мост запущен от Tools\Python рядом с приложением,
# подключаем соседний site-packages (requests), иначе он не виден.
# Вставка пути — ДО import requests, иначе фолбэк никогда не срабатывает.
_SP = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                    "..", "..", "Tools", "Python", "site-packages"))
if os.path.isdir(_SP) and _SP not in sys.path:
    sys.path.insert(0, _SP)

try:
    import requests
except ImportError:
    requests = None

MANPAGES_VERSION = "6.9"
MANPAGES_ARCHIVE_URL = f"https://www.kernel.org/pub/linux/docs/man-pages/man-pages-{MANPAGES_VERSION}.tar.xz"

_SECTION_RE = re.compile(r"man/man([23])/(.+)\.([23][a-z]*)$")

# Протокол обмена с GUI (PROGRESS/ERROR/DONE) — строго UTF-8: без этого
# pythonw пишет в канал в системной кодировке (cp1251), и русские сообщения
# в GUI превращаются в нечитаемые символы.
for _stream in (sys.stdout, sys.stderr):
    if _stream is not None and hasattr(_stream, "reconfigure"):
        try:
            _stream.reconfigure(encoding="utf-8")
        except Exception:
            pass

_SCHEMA_SQL = """
CREATE TABLE IF NOT EXISTS pages (
    page_name TEXT PRIMARY KEY,
    section TEXT NOT NULL DEFAULT '',
    title TEXT NOT NULL DEFAULT '',
    library TEXT NOT NULL DEFAULT '',
    summary TEXT NOT NULL DEFAULT '',
    markdown TEXT NOT NULL DEFAULT ''
);
CREATE TABLE IF NOT EXISTS function_index (
    func_name TEXT PRIMARY KEY,
    page_name TEXT NOT NULL,
    section TEXT NOT NULL DEFAULT ''
);
CREATE INDEX IF NOT EXISTS idx_fi_page ON function_index(page_name);
CREATE TABLE IF NOT EXISTS meta (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL DEFAULT ''
);
"""


def prog(pct, msg):
    try:
        print(f"PROGRESS {pct} {msg}", flush=True)
    except Exception:
        pass


def err(msg):
    try:
        print(f"ERROR {msg}", flush=True)
    except Exception:
        pass


def done(msg):
    try:
        print(f"DONE {msg}", flush=True)
    except Exception:
        pass


def _parse_roff_text(text):
    """Упрощённый парсер roff в markdown-подобный текст для man-страниц."""
    lines = []
    in_verbatim = False
    for raw in text.splitlines():
        line = raw.rstrip()
        stripped = line.strip()

        if stripped.startswith(".\\\""):
            continue  # комментарий
        if stripped.startswith(".TH"):
            continue  # TH обрабатывается отдельно
        if stripped.startswith(".SH"):
            # заголовок секции — вставляем отдельной строкой для _extract_sections
            m2 = re.match(r"^\.SH\s+(.+)$", stripped)
            if m2:
                lines.append(m2.group(1).strip())
            continue
        if stripped.startswith(".PP") or stripped.startswith(".P"):
            lines.append("")
            continue
        if stripped.startswith(".br"):
            continue

        # Вербатим-блоки (.nf/.fi)
        if stripped == ".nf":
            in_verbatim = True
            continue
        if stripped == ".fi":
            in_verbatim = False
            continue

        m = re.match(r"^\.(?:B|I|BI|IB|BR|IR|RB|RI)\s+(.*)$", stripped)
        if m:
            content = m.group(1)
            content = re.sub(r"\\f[BPIR]", "", content)
            content = content.replace("\\e", "\\")
            lines.append(content)
            continue

        # ман-ссылки: \fBfoo\fP(3)
        text_line = stripped
        text_line = re.sub(r"\\f[BPIR]", "", text_line)
        text_line = text_line.replace("\\e", "\\")
        # "foo(2)" в ссылки
        text_line = re.sub(r"\b([a-zA-Z0-9_]+)\(([23][a-z]*)\)", r"`\1(\2)'_", text_line)
        if text_line:
            lines.append(text_line)

    # схлопываем повторные пустые строки
    result = []
    prev_empty = False
    for l in lines:
        empty = l == ""
        if empty and prev_empty:
            continue
        result.append(l)
        prev_empty = empty
    return "\n".join(result).strip()


def _extract_sections(markdown_text):
    """Возвращает dict секций из текста (NAME, SYNOPSIS, LIBRARY, ...)."""
    sections = {}
    current = None
    for line in markdown_text.splitlines():
        m = re.match(r"^([A-Z][A-Z ]{3,})\s*$", line.strip())
        if m:
            current = m.group(1).strip()
            sections.setdefault(current, [])
            continue
        if current:
            sections[current].append(line)
    return {k: "\n".join(v).strip() for k, v in sections.items()}


def _title_from_name(name_text):
    """Из секции NAME: 'foo, bar - описание' возвращает (список имён, описание)."""
    if not name_text:
        return [], ""
    first_line = name_text.splitlines()[0] if name_text else ""
    if " - " in first_line:
        head, desc = first_line.split(" - ", 1)
        names = [n.strip() for n in head.split(",") if n.strip()]
        return names, desc.strip()
    return [], ""


def import_archive(db_path, payload, progress_callback=None):
    """Импортирует содержимое tar-архива man-pages в SQLite."""
    p = Path(db_path)
    if p.parent:
        p.parent.mkdir(parents=True, exist_ok=True)

    conn = sqlite3.connect(str(p))
    try:
        conn.executescript(_SCHEMA_SQL)

        inserts_pages = 0
        inserts_funcs = 0
        # Архив целиком в памяти — без временного файла на диске
        # (класс ошибок «забытый tmp-файл» исчезает).
        with tarfile.open(fileobj=io.BytesIO(payload), mode="r:xz") as tar:
            members = [m for m in tar.getmembers() if m.isfile()]
            total = len(members)
            for idx, m in enumerate(members):
                name = m.name.replace("\\", "/")
                rel = _SECTION_RE.search(name)
                if not rel:
                    continue
                section = rel.group(1)
                fname = rel.group(2)

                # читаем содержимое и извлекаем NAME / LIBRARY
                try:
                    f = tar.extractfile(m)
                    if f is None:
                        continue
                    content = f.read().decode("utf-8", errors="replace")
                except Exception:
                    continue

                md = _parse_roff_text(content)
                secs = _extract_sections(md)
                title, summary = _title_from_name(secs.get("NAME", ""))
                library = secs.get("LIBRARY", "")[:200]

                page_name = fname
                conn.execute(
                    "INSERT OR REPLACE INTO pages "
                    "(page_name, section, title, library, summary, markdown) "
                    "VALUES (?,?,?,?,?,?)",
                    (page_name, section, ", ".join(title), library, summary, md))
                inserts_pages += 1

                for fn in title:
                    fn = fn.strip()
                    if not fn or not re.match(r"^[a-zA-Z_][\w]*$", fn):
                        continue
                    conn.execute(
                        "INSERT OR REPLACE INTO function_index (func_name, page_name, section) "
                        "VALUES (?,?,?)", (fn, page_name, section))
                    inserts_funcs += 1

                if progress_callback and (idx % 50 == 0 or idx == total - 1):
                    progress_callback(idx + 1, total, f"Разбор {page_name}")

        conn.execute(
            "INSERT OR REPLACE INTO meta (key, value) VALUES ('manpages_version', ?)",
            (MANPAGES_VERSION,))
        conn.execute(
            "INSERT OR REPLACE INTO meta (key, value) VALUES ('imported_functions', ?)",
            (str(inserts_funcs),))
        conn.execute(
            "INSERT OR REPLACE INTO meta (key, value) VALUES ('imported_pages', ?)",
            (str(inserts_pages),))
        conn.commit()
    finally:
        conn.close()
    return inserts_funcs


def main():
    if len(sys.argv) < 2:
        err("Не указан путь к БД")
        return
    db_path = sys.argv[1]

    try:
        if requests is None:
            raise RuntimeError("Библиотека requests не установлена. Выполните: pip install requests")

        prog(5, f"Загрузка man-pages {MANPAGES_VERSION}…")
        try:
            response = requests.get(MANPAGES_ARCHIVE_URL, timeout=180)
            response.raise_for_status()
            payload = response.content
        except Exception as e:
            raise RuntimeError(f"Не удалось скачать архив man-pages: {e}")

        size_mb = len(payload) / (1024 * 1024)
        prog(30, f"Архив получен ({size_mb:.1f} МБ). Разбор…")

        def on_progress(current, total, message):
            pct = 30 + int(60 * current / max(total, 1))
            prog(min(pct, 95), message)

        count = import_archive(db_path, payload, progress_callback=on_progress)

        # проверка
        try:
            conn = sqlite3.connect(db_path)
            try:
                c = conn.execute("SELECT COUNT(*) FROM function_index").fetchone()[0]
            finally:
                conn.close()
        except Exception:
            c = count

        prog(100, f"Импортировано функций: {c}")
        done(str(db_path))
    except Exception as e:
        err(f"{type(e).__name__}: {e}")


if __name__ == "__main__":
    main()