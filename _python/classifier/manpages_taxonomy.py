"""Таксономия man-pages - единственная таблица соответствия для классификации
функций и библиотек, документированных man-pages (Linux / Android).

Две независимые оси:

  * **функция** (частный отчёт СФ) классифицируется по СЕКЦИИ своей man-страницы
    (man1..man9, русские названия - `section_category`): секция отвечает на
    вопрос «что это за документ», а не «из какой библиотеки»;
  * **модуль/библиотека** (индекс анализа ПО) - по секциям LIBRARY man-страниц:
    у модуля собирается набор секций, в которых он фигурирует (данные БД +
    `module_library_key`), а библиотечная категория остаётся
    `MANPAGES_LIB_CATEGORIES` с русскими названиями `category_name_ru`.

Источник истины - секции LIBRARY man-страниц: на сборке manpages_build.py
выгружает токены библиотек («(libc, -lc)» - не зависят от языка перевода)
в manpages.db, а здесь задано, как короткое имя библиотеки превращается в
категорию и отображаемый soname.

Таксономия библиотечных категорий утверждена владельцем проекта:
**строго по библиотекам** - каждая библиотека man-pages является отдельной
категорией, плюс два библиотечных fallback:

  * «Системные библиотеки ОС» - категория библиотечных словарей (Windows и
    прочие платформы); для функций man-pages НЕ применяется (до 1.0.0.36 она
    ошибочно доставалась любой странице без секции LIBRARY);
  * «Системные библиотеки Android» - bionic/AOSP-библиотеки (liblog,
    libbinder, …), которых в man-pages нет; описывает их android.py.

Правка этой таблицы не требует трогать словари: добавил библиотеку - и новые
страницы корпуса классифицируются сами.
"""
from __future__ import annotations

import re

# ── Категория = имя библиотеки man-pages ────────────────────────────────────
# Короткое имя из LIBRARY-токенов → название категории. Названия совпадают
# с именами библиотек (утверждённая таксономия «строго по библиотекам»).
MANPAGES_LIB_CATEGORIES: dict[str, str] = {
    "libc": "libc",
    "libm": "libm",
    "libpthread": "libpthread",
    "librt": "librt",
    "libdl": "libdl",
    "libresolv": "libresolv",
    "libanl": "libanl",
    "libnsl": "libnsl",
    "libcrypt": "libcrypt",
    "libutil": "libutil",
}

# Русские описания категорий (отчёт анализа ПО показывает их в таблице модулей).
MANPAGES_CATEGORY_DESCRIPTIONS: dict[str, str] = {
    "libc": (
        "Стандартная библиотека C (glibc): функции ISO C, интерфейс системных "
        "вызовов POSIX, NSS. man-pages: Standard C library (libc, -lc)."
    ),
    "libm": "Математическая библиотека: sin, cos, sqrt, pow и т.п. (libm, -lm).",
    "libpthread": "POSIX-потоки и синхронизация: pthread_*, семафоры (libpthread).",
    "librt": "POSIX-расширения реального времени: aio_*, mq_*, clock, shm (librt).",
    "libdl": "Динамическая загрузка: dlopen, dlsym (libdl) и страницы загрузчика ld.so.",
    "libresolv": "DNS-резолвер: resolver(3), dns_* (libresolv).",
    "libanl": "Асинхронное разрешение имён: getaddrinfo_a (libanl).",
    "libnsl": "Сетевые службы: NIS/YP, xdr_* (libnsl).",
    "libcrypt": "Хеширование паролей: crypt, crypt_r (libcrypt/libxcrypt).",
    "libutil": "Системные утилиты: openpty, forkpty, login_tty (libutil).",
}

# Fallback для страниц man-pages без секции LIBRARY: man7-обзорные страницы,
# man5-форматы, man4-спецфайлы. Считается системной категорией.
CATEGORY_OS_FALLBACK = "Системные библиотеки ОС"

# Категория bionic/AOSP-библиотек (android.py): в man-pages их нет.
CATEGORY_ANDROID = "Системные библиотеки Android"

# Русские названия категорий man-pages (левая панель индекса анализа ПО).
# Категорией остаётся короткое имя библиотеки («libc»), в интерфейсе оно
# показывается русским названием.
MANPAGES_CATEGORY_NAMES_RU: dict[str, str] = {
    "libc": "Стандартная библиотека C",
    "libm": "Математическая библиотека",
    "libpthread": "Потоки POSIX (pthread)",
    "librt": "Реальное время POSIX",
    "libdl": "Динамическая загрузка",
    "libresolv": "DNS-резолвер",
    "libanl": "Асинхронное разрешение имён",
    "libnsl": "Сетевые службы (NIS/YP)",
    "libcrypt": "Хеширование паролей",
    "libutil": "Системные утилиты",
}


def category_name_ru(category: str) -> str:
    """Русское название категории man-pages для интерфейса.

    Категории man-pages («libc», «libm», ...) показываются русскими
    названиями; не-manpages категории («Системные библиотеки ОС»,
    «Системные библиотеки Android», категории Windows-словарей) уже
    русские - возвращаются как есть.
    """
    return MANPAGES_CATEGORY_NAMES_RU.get(category, category)

# ── Приоритет секций man при поиске документации ────────────────────────────
# ELF-импорт - библиотечный вызов, поэтому секция 3 (функции libc) главнее
# секции 2 (сисколлы); при неоднозначности выигрывает 3. Утверждённое
# правило коллизий: секция важнее языка - RU-страница секции 2 не бьёт
# EN-страницу секции 3.
SECTION_PRIORITY: tuple[str, ...] = (
    "3", "3const", "3type", "3head", "3attr",
    "2", "2const", "2type",
    "7", "1", "5", "4", "8", "6",
)
_SECTION_RANK: dict[str, int] = {s: i for i, s in enumerate(SECTION_PRIORITY)}
_LANG_RANK = {"ru": 0, "en": 1}


def section_rank(section: str) -> int:
    """Порядок секции man для выбора страницы (неизвестные - в конец)."""
    return _SECTION_RANK.get((section or "").strip().lower(), 90)


def lang_rank(lang: str) -> int:
    """Порядок языка: русская страница предпочитается английской."""
    return _LANG_RANK.get((lang or "").strip().lower(), 5)


# ── Категория функции = секция man ──────────────────────────────────────────
# Секция man отвечает, ЧТО описывает страница (команда, сисколл, библиотечный
# вызов, формат файла…), а не из какой библиотеки функция. Поэтому в частном
# отчёте СФ категория строки - русское название секции её man-страницы
# (решение владельца 2026-10-30). Fallback «Системные библиотеки ОС» для
# страниц без LIBRARY здесь больше не применяется.
MANPAGES_SECTION_NAMES_RU: dict[str, str] = {
    "1": "Пользовательские команды",
    "2": "Системные вызовы",
    "3": "Библиотечные вызовы",
    "4": "Специальные файлы (устройства)",
    "5": "Форматы файлов и конфигурации",
    "6": "Игры",
    "7": "Обзоры, соглашения и прочее",
    "8": "Команды администрирования",
    "9": "Внутренние подпрограммы ядра (nonstandard)",
}

# Описания категорий-секций для левой панели индекса анализа ПО.
MANPAGES_SECTION_DESCRIPTIONS: dict[str, str] = {
    "1": ("Команды, которые пользователь запускает из оболочки (man1). "
          "Импортируемых библиотек здесь обычно нет."),
    "2": ("Функции-обёртки системных вызовов ядра (man2), в том числе "
          "2const/2type: open, read, clock_gettime, ioctl-команды."),
    "3": ("Библиотечные вызовы (man3), включая 3const/3type/3head/3attr: "
          "malloc, printf, pthread_*, sin, dlopen."),
    "4": "Специальные файлы устройств (man4). Импортируемых библиотек не содержит.",
    "5": "Форматы файлов и конфигураций (man5). Импортируемых библиотек не содержит.",
    "6": "Игры (man6).",
    "7": ("Обзоры, соглашения и прочие темы (man7): стандарты, раскладка ФС, "
          "протоколы, man-pages(7)."),
    "8": "Команды администрирования (man8). Импортируемых библиотек не содержит.",
    "9": "Внутренние подпрограммы ядра (man9, nonstandard; в Linux man-pages нет).",
}

# Цвета тегов/маркеров секций man (отчёты: индекс анализа ПО, карточки
# «Прямые зависимости»). Разные секции - разные цвета, чтобы тип документа
# читался сразу; модули без man-страниц и собственные - своими цветами.
MANPAGES_SECTION_COLORS: dict[str, str] = {
    "1": "#b07d2b",   # пользовательские команды
    "2": "#2e9e6b",   # системные вызовы
    "3": "#4a90d9",   # библиотечные вызовы
    "4": "#9a7bd0",   # специальные файлы
    "5": "#c2603f",   # форматы файлов
    "6": "#d05a8a",   # игры
    "7": "#7d8fa8",   # обзоры и соглашения
    "8": "#5f9ea0",   # администрирование
    "9": "#a0522d",   # подпрограммы ядра
}
CATEGORY_NO_MANPAGES_COLOR = "#9e9e9e"
INTERNAL_MODULE_COLOR = "#607D8B"

# Группа модулей, у которых нет man-страниц (bionic/AOSP, сторонние и
# собственные библиотеки) - помещается в конце списка категорий индекса.
CATEGORY_NO_MANPAGES = "Нет man-страниц"
NO_MANPAGES_DESCRIPTION = (
    "Модули без документации man-pages: bionic/AOSP (liblog, libbinder…), "
    "сторонние и собственные библиотеки, не описанные в man-pages.")

# Порядок тегов секций у модуля в индексе анализа ПО (правило владельца
# 2026-10-30): сначала системные вызовы, затем библиотечные вызовы, затем
# остальные секции без приоритета - по номеру.
_SECTION_TAG_ORDER: tuple[str, ...] = ("2", "3", "1", "4", "5", "6", "7", "8", "9")
_SECTION_BASE_RE = re.compile(r"^\s*(\d+)")


def section_base(section: str) -> str:
    """Базовая секция man: «2const»/«2type» → «2», «3type» → «3»."""
    m = _SECTION_BASE_RE.match(str(section or ""))
    return m.group(1) if m else ""


def section_category(section: str) -> str:
    """Русское название категории-секции man ('' - секция неизвестна)."""
    return MANPAGES_SECTION_NAMES_RU.get(section_base(section), "")


def section_tag_rank(section: str) -> int:
    """Порядок тега секции у модуля (системные → библиотечные → прочие)."""
    base = section_base(section)
    try:
        return _SECTION_TAG_ORDER.index(base)
    except ValueError:
        return len(_SECTION_TAG_ORDER)


def section_categories_ordered() -> list[str]:
    """Названия категорий-секций в порядке левой панели индекса."""
    return [MANPAGES_SECTION_NAMES_RU[b] for b in _SECTION_TAG_ORDER
            if b in MANPAGES_SECTION_NAMES_RU]


def section_description(section: str) -> str:
    """Описание группы-секции для левой панели индекса анализа ПО."""
    return MANPAGES_SECTION_DESCRIPTIONS.get(section_base(section), "")


def section_color(section: str) -> str:
    """Цвет тега/маркера секции man (неизвестная - цвет «нет man-страниц»)."""
    return MANPAGES_SECTION_COLORS.get(section_base(section), CATEGORY_NO_MANPAGES_COLOR)


def sort_sections(sections) -> list[str]:
    """Базовые секции без повторов в порядке тегов модуля."""
    bases = {section_base(s) for s in (sections or [])}
    bases.discard("")
    return sorted(bases, key=section_tag_rank)


# ── Модуль → короткое имя библиотеки man-pages ─────────────────────────────
# Для индекса анализа ПО: soname из DT_NEEDED/IDA приводится к токену LIBRARY
# («libc.so.6» → «libc», «libc.musl-x86_64.so.1» → «libc»), чтобы взять из
# manpages.db секции страниц этой библиотеки.
def module_library_key(module_name: str) -> str:
    """Короткое имя библиотеки man-pages для модуля ('' - определить нельзя)."""
    name = (module_name or "").strip().lower().replace("\\", "/").rsplit("/", 1)[-1]
    if not name:
        return ""
    # Загрузчик glibc документируется страницами ld.so, которые входят в libdl
    # (bionic linker/linker64 в man-pages не описан - у них тегов не будет).
    if name.startswith(("ld-", "ld.so")):
        return "libdl"
    if name.startswith("libc.musl"):
        return "libc"
    if ".so" in name:
        name = name.split(".so", 1)[0]
    return name


def manpages_category(library: str) -> str:
    """Категория по короткому имени библиотеки из LIBRARY-страницы.

    Пустая строка библиотеки (страницы без LIBRARY) → fallback-категория.
    Неизвестная библиотека (нет в таблице) → тоже fallback.
    Для функций частного отчёта СФ НЕ используется (там категория - секция,
    см. `section_category`); остаётся для библиотечных категорий индекса.
    """
    lib = (library or "").strip().lower()
    return MANPAGES_LIB_CATEGORIES.get(lib, CATEGORY_OS_FALLBACK)


# ── Отображаемый soname по варианту платформы ───────────────────────────────
# В отчёте модуль показывается soname'ом (однородно с DT_NEEDED-импортами).
# glibc: канонические soname библиотек; bionic (Android): pthread/dl/rt и все
# библиотеки без собственного soname фактически живут в libc.so (bionic слил
# их в libc) - утверждённое правило канонизации.

GLIBC_SONAMES: dict[str, str] = {
    "libc": "libc.so.6",
    "libm": "libm.so.6",
    "libmvec": "libmvec.so.1",
    "libpthread": "libpthread.so.0",
    "librt": "librt.so.1",
    "libdl": "libdl.so.2",
    "libresolv": "libresolv.so.2",
    "libanl": "libanl.so.1",
    "libnsl": "libnsl.so.1",
    "libutil": "libutil.so.1",
    "libcrypt": "libcrypt.so.1",
}

BIONIC_SONAMES: dict[str, str] = {
    "libc": "libc.so",
    "libm": "libm.so",
    "libdl": "libdl.so",
    # Без собственного soname в bionic - фактическое место libc.so:
    "libpthread": "libc.so",
    "librt": "libc.so",
    "libresolv": "libc.so",
    "libanl": "libc.so",
    "libnsl": "libc.so",
    "libcrypt": "libc.so",
    "libutil": "libc.so",
    "libmvec": "libm.so",
}


def library_display(library: str, variant: str) -> str:
    """Soname для колонки «Библиотека» в отчёте.

    variant: "glibc" | "android" (детект - по ELF-заметкам android_api).
    Пустая/неизвестная библиотека → пустая строка (модуль не назначается).
    """
    lib = (library or "").strip().lower()
    if not lib:
        return ""
    if variant == "android":
        return BIONIC_SONAMES.get(lib, "")
    return GLIBC_SONAMES.get(lib, "")


def is_syscall_section(section: str) -> bool:
    """Системность функции по секции её man-страницы.

    Системная функция - только из секции system calls: «2» и её варианты
    (2const, 2type - константы/типы syscall-домена, например ioctl-команды).
    Документированность в остальных секциях (3 - библиотечные вызовы,
    7 - разное, 1 - команды) системности НЕ даёт (правило владельца
    2026-10-29, взамен прежнего «любая документированная в man-pages -
    системная»).
    """
    return bool(section) and str(section).startswith("2")


# ── Системные soname при классификации модулей по имени (DT_NEEDED) ────────
# Импорты ELF обычно приходят псевдо-модулем .dynsym, но когда библиотека
# известна (DT_NEEDED/IDA), системность решается по этим наборам soname.

GLIBC_OS_SONAMES: frozenset[str] = frozenset({
    "libc.so.6", "libm.so.6", "libmvec.so.1", "libpthread.so.0",
    "librt.so.1", "libdl.so.2", "libresolv.so.2", "libanl.so.1",
    "libnsl.so.1", "libutil.so.1", "libcrypt.so.1", "libcrypt.so.2",
    "ld-linux.so.2", "ld-linux-x86-64.so.2", "ld-linux-aarch64.so.2",
    # musl (Alpine и др.) - та же документированная man-pages поверхность API:
    "libc.musl-x86_64.so.1", "libc.musl-aarch64.so.1", "ld-musl-x86_64.so.1",
})

BIONIC_OS_SONAMES: frozenset[str] = frozenset({
    "libc.so", "libm.so", "libdl.so", "linker", "linker64",
})

# Soname динамического загрузчика: страниц у ld.so нет секции LIBRARY,
# поэтому загрузчик относится к категории libdl (утверждённая таксономия).
LOADER_SONAMES: tuple[str, ...] = (
    "ld-linux.so.2", "ld-linux-x86-64.so.2", "ld-linux-aarch64.so.2",
    "ld-musl-x86_64.so.1", "ld.so",
)


def linux_soname_descriptions() -> dict[str, str]:
    """soname и короткая форма → описание библиотеки (анализ ПО).

    Ключи - glibc- и bionic-soname плюс короткая форма («libc»): классификатор
    модулей получает имена без суффиксов версий от man-страниц и DT_NEEDED.
    """
    result: dict[str, str] = {}
    for lib in MANPAGES_LIB_CATEGORIES:
        desc = MANPAGES_CATEGORY_DESCRIPTIONS.get(lib, "")
        for name in (GLIBC_SONAMES.get(lib), BIONIC_SONAMES.get(lib), lib):
            if name:
                result.setdefault(name, desc)
    return result
