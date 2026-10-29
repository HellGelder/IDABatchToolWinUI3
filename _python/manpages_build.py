"""Сборка встроенной БД man-pages для исполнения 2 (IDABatchToolWinUI).

Собирает manpages.db из двух локальных корпусов (скачивание - на стороне
make-portable.ps1, см. scripts/build-manpages.ps1):

  * RU: manpages-l10n (пакет man-pages-ru, po4a-переводы, roff в *.gz);
  * EN: man-pages с kernel.org (roff, распакованный tarball) - fallback
    для непереведённых страниц.

Каждая страница рендерится в готовый HTML внешним рендерером (pandoc,
ман-ридер с поддержкой tbl). В БД складывается готовый HTML - приложение
при генерации отчётов сети не касается и roff не парсит.

Схема (schema=2, ключ страницы - (page_name, section, lang)):
    pages(page_name, section, lang, title, library, summary, html)
    function_index(func_name, page_name, section, lang)
    meta(key, value)

Приоритет выбора страницы при поиске документации - в report_bridge
(секция 3 > 2 > 7 > 1 > 5 > 4 > 8 > 6, внутри секции ru > en).
"""
from __future__ import annotations

import argparse
import gzip
import os
import re
import sqlite3
import subprocess
import sys
import time
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path

# Секция берётся из суффикса файла (open.2, open.2.gz, CPU_SET.3, stat.3type):
# подкаталоги man2const/man3type и суффиксы совпадают, суффикс авторитетнее.
_PAGE_RE = re.compile(r"^(?P<name>[^/\\]+?)\.(?P<sec>[1-8][a-z]*)(?:\.gz)?$", re.IGNORECASE)

# Раздел NAME по-русски в po4a-страницах - «НАИМЕНОВАНИЕ», LIBRARY - «БИБЛИОТЕКА».
_NAME_SECTIONS = ("NAME", "НАИМЕНОВАНИЕ", "ИМЯ")
_LIBRARY_SECTIONS = ("LIBRARY", "БИБЛИОТЕКА")

_ROFF_ESCAPES = (
    (r"\-", "-"),
    (r"\(aq", "'"),
    (r"\(dq", '"'),
    (r"\(oq", "`"),
    (r"\(cq", "'"),
    (r"\(em", "—"),
    (r"\(en", "–"),
    (r"\(bu", "•"),
    (r"\~", " "),
    (r"\&", ""),
    (r"\%", ""),
    (r"\|", ""),
    (r"\^", ""),
    ("\\ ", " "),
    (r"\e", "\\"),
)

# font-переключения снимаются регэкспом: в po4a-страницах LIBRARY-строка
# выглядит как «(\fIlibc\fP, \fI-lc\fP)», и без этого токен «libc» внутри
# \fI...\fP не находится (str.replace со строкой "\f[BIRP]" был бы литералом).
_FONT_RE = re.compile(r"\\f[A-Z]")


_LIB_TOKEN_RE = re.compile(r"\blib[a-z][A-Za-z0-9_+.\-]*")
_LIB_TOKEN_NOISE = frozenset({"library", "libraries"})


def _unescape_roff(text: str) -> str:
    for esc, rep in _ROFF_ESCAPES:
        text = text.replace(esc, rep)
    return _FONT_RE.sub("", text)


def _roff_sections(text: str) -> dict[str, str]:
    """Разбирает .SH-секции roff-источника (до рендеринга - для метаданных)."""
    sections: dict[str, list[str]] = {}
    current = None
    for line in text.splitlines():
        stripped = line.strip()
        m = re.match(r'^\.SH\s+"?(.*?)"?\s*$', stripped)
        if m:
            current = m.group(1).strip().upper()
            sections.setdefault(current, [])
            continue
        if current is not None:
            sections[current].append(line)
    return {k: "\n".join(v).strip() for k, v in sections.items()}


def _names_and_summary(roff: str) -> tuple[list[str], str]:
    """Имена функций и краткое описание из секции NAME/НАИМЕНОВАНИЕ."""
    for key in _NAME_SECTIONS:
        for sh, body in _roff_sections(roff).items():
            if sh == key:
                joined = " ".join(
                    ln.strip() for ln in body.splitlines() if ln.strip())
                joined = _unescape_roff(joined)
                if " - " in joined:
                    head, desc = joined.split(" - ", 1)
                    names = [n.strip() for n in head.split(",") if n.strip()]
                    return names, desc.strip()
                return [], ""
    return [], ""


def _library_tokens(roff: str) -> list[str]:
    """Короткие имена библиотек из секции LIBRARY/БИБЛИОТЕКА.

    Токены в скобках («(libc, -lc)») не зависят от языка перевода:
    «Стандартная библиотека языка C (\fIlibc\fP, \fI-lc\fP)» → ["libc"].
    """
    for key in _LIBRARY_SECTIONS:
        for sh, body in _roff_sections(roff).items():
            if sh == key:
                tokens: list[str] = []
                for group in re.findall(r"\(([^)]*)\)", body) or [body]:
                    for token in _LIB_TOKEN_RE.findall(_unescape_roff(group)):
                        token = token.strip(".,;()")
                        if token and token.lower() not in _LIB_TOKEN_NOISE and token not in tokens:
                            tokens.append(token)
                return tokens
    return []


class Renderer:
    """Рендер roff → HTML через pandoc (ман-ридер, stdin/stdout)."""

    def __init__(self, pandoc_path: str):
        self.path = pandoc_path
        self.version = "pandoc"
        try:
            out = subprocess.run(
                [pandoc_path, "--version"], capture_output=True, text=True,
                timeout=30, encoding="utf-8", errors="replace")
            if out.returncode == 0 and out.stdout:
                self.version = out.stdout.splitlines()[0].strip()
        except Exception:
            pass

    def render(self, roff_text: str) -> str:
        proc = subprocess.run(
            [self.path, "-f", "man", "-t", "html", "--wrap=none"],
            input=roff_text.encode("utf-8"), stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL, timeout=120)
        if proc.returncode != 0:
            raise RuntimeError(f"pandoc exit {proc.returncode}")
        return proc.stdout.decode("utf-8", errors="replace")


def _read_page(path: Path) -> str:
    data = path.read_bytes()
    if path.name.lower().endswith(".gz"):
        data = gzip.decompress(data)
    return data.decode("utf-8", errors="replace")


def _collect_pages(root: Path) -> list[tuple[Path, str, str]]:
    """(путь, page_name, section) всех man-страниц каталога с man*-подкаталогами."""
    result = []
    for man_dir in sorted(root.glob("man*")):
        if not man_dir.is_dir():
            continue
        for f in sorted(man_dir.iterdir()):
            if not f.is_file():
                continue
            m = _PAGE_RE.match(f.name)
            if not m:
                continue
            result.append((f, m.group("name"), m.group("sec").lower()))
    return result


def main() -> int:
    ap = argparse.ArgumentParser(description="Сборка manpages.db из корпусов RU+EN")
    ap.add_argument("--ru-dir", required=True,
                    help="каталог с man*/ и *.gz (usr/share/man/ru из пакета man-pages-ru)")
    ap.add_argument("--en-dir", required=True,
                    help="каталог man-pages-N.M с man*/ (распакованный tarball kernel.org)")
    ap.add_argument("--out", required=True, help="путь к manpages.db")
    ap.add_argument("--pandoc", required=True, help="путь к pandoc.exe")
    ap.add_argument("--ru-version", default="", help="версия корпуса RU (для meta)")
    ap.add_argument("--en-version", default="", help="версия корпуса EN (для meta)")
    ap.add_argument("--jobs", type=int, default=8, help="параллельных рендеров")
    args = ap.parse_args()

    ru_dir, en_dir = Path(args.ru_dir), Path(args.en_dir)
    out_path = Path(args.out)
    if not ru_dir.is_dir():
        print(f"ERROR RU-каталог не найден: {ru_dir}")
        return 2
    if not en_dir.is_dir():
        print(f"ERROR EN-каталог не найден: {en_dir}")
        return 2

    renderer = Renderer(args.pandoc)
    print(f"PROGRESS 5 Рендерер: {renderer.version}", flush=True)

    ru_pages = _collect_pages(ru_dir)
    en_pages = _collect_pages(en_dir)
    total = len(ru_pages) + len(en_pages)
    if total == 0:
        print("ERROR В корпусах не найдено ни одной man-страницы")
        return 2
    print(f"PROGRESS 8 Страниц: RU {len(ru_pages)} + EN {len(en_pages)} = {total}", flush=True)

    jobs = [(p, name, sec, "ru") for p, name, sec in ru_pages]
    jobs += [(p, name, sec, "en") for p, name, sec in en_pages]

    rows: list[tuple] = []
    failed = 0
    done = 0

    def render_one(job):
        path, name, sec, lang = job
        roff = _read_page(path)
        html = renderer.render(roff)
        names, summary = _names_and_summary(roff)
        libs = _library_tokens(roff)
        return (name, sec, lang, ", ".join(names), ",".join(libs), summary, html, names)

    with ThreadPoolExecutor(max_workers=max(args.jobs, 1)) as pool:
        futures = {pool.submit(render_one, j): j for j in jobs}
        for fut in as_completed(futures):
            job = futures[fut]
            done += 1
            if done % 25 == 0 or done == total:
                pct = 8 + int(80 * done / total)
                print(f"PROGRESS {pct} Рендер {done}/{total} ({job[3]}: {job[1]})", flush=True)
            try:
                rows.append(fut.result())
            except Exception as e:
                failed += 1
                print(f"WARN рендер не удался ({job[1]}.{job[2]} {job[3]}): {e}", flush=True)

    if failed > max(5, total // 20):
        print(f"ERROR Слишком много неудачных рендеров: {failed}/{total}")
        return 2

    # Порядок вставки детерминированный: (lang, section, page_name)
    rows.sort(key=lambda r: (r[2], r[1], r[0]))
    print(f"PROGRESS 90 Запись БД ({len(rows)} страниц, ошибок рендера: {failed})…", flush=True)

    out_path.parent.mkdir(parents=True, exist_ok=True)
    conn = sqlite3.connect(str(out_path))
    try:
        conn.executescript("""
DROP TABLE IF EXISTS pages;
DROP TABLE IF EXISTS function_index;
DROP TABLE IF EXISTS meta;
CREATE TABLE pages (
    page_name TEXT NOT NULL,
    section TEXT NOT NULL,
    lang TEXT NOT NULL,
    title TEXT NOT NULL DEFAULT '',
    library TEXT NOT NULL DEFAULT '',
    summary TEXT NOT NULL DEFAULT '',
    html TEXT NOT NULL DEFAULT '',
    PRIMARY KEY (page_name, section, lang)
);
CREATE TABLE function_index (
    func_name TEXT NOT NULL,
    page_name TEXT NOT NULL,
    section TEXT NOT NULL,
    lang TEXT NOT NULL,
    PRIMARY KEY (func_name, page_name, section, lang)
);
CREATE INDEX idx_fi_func ON function_index(func_name);
CREATE TABLE meta (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL DEFAULT ''
);
""")
        pages_rows = []
        index_rows = []
        name_re = re.compile(r"^[a-zA-Z_]\w*$")
        for name, sec, lang, title, library, summary, html, names in rows:
            pages_rows.append((name, sec, lang, title, library, summary, html))
            for fn in names:
                fn = fn.strip()
                if fn and name_re.match(fn):
                    index_rows.append((fn, name, sec, lang))
        conn.executemany(
            "INSERT OR REPLACE INTO pages "
            "(page_name, section, lang, title, library, summary, html) "
            "VALUES (?,?,?,?,?,?,?)", pages_rows)
        conn.executemany(
            "INSERT OR REPLACE INTO function_index "
            "(func_name, page_name, section, lang) VALUES (?,?,?,?)", index_rows)
        meta = {
            "schema": "2",
            "ru_version": args.ru_version,
            "en_version": args.en_version,
            "renderer": renderer.version,
            "generated_at": time.strftime("%Y-%m-%d %H:%M:%S"),
            "imported_pages": str(len(pages_rows)),
            "imported_functions": str(len(index_rows)),
            "failed_renders": str(failed),
        }
        conn.executemany(
            "INSERT OR REPLACE INTO meta (key, value) VALUES (?,?)", meta.items())
        conn.commit()
    finally:
        conn.close()

    print(f"PROGRESS 100 Готово: страниц {len(pages_rows)}, функций в индексе {len(index_rows)}", flush=True)
    print(f"DONE {out_path}", flush=True)
    return 0


if __name__ == "__main__":
    # PYTHONUTF8 влияет на вывод сообщений (см. manpages_bridge.py).
    for _stream in (sys.stdout, sys.stderr):
        if _stream is not None and hasattr(_stream, "reconfigure"):
            try:
                _stream.reconfigure(encoding="utf-8")
            except Exception:
                pass
    sys.exit(main())
