"""Таксономия man-pages - единственная таблица соответствия для классификации
функций и библиотек, документированных man-pages (Linux / Android).

Источник истины - секции LIBRARY man-страниц: на сборке manpages_build.py
выгружает токены библиотек («(libc, -lc)» - не зависят от языка перевода)
в manpages.db, а здесь задано, как короткое имя библиотеки превращается в
категорию, отображаемый soname и системность.

Таксономия категорий утверждена владельцем проекта: **строго по библиотекам** -
каждая библиотека man-pages является отдельной категорией, плюс два fallback:

  * «Системные библиотеки ОС» - страницы man-pages без секции LIBRARY
    (обзорные man7, форматы man5, спецфайлы man4 и т.п.);
  * «Системные библиотеки Android» - bionic/AOSP-библиотеки (liblog,
    libbinder, …), которых в man-pages нет; описывает их android.py.

Правка этой таблицы не требует трогать словари: добавил библиотеку - и новые
страницы корпуса классифицируются сами.
"""
from __future__ import annotations

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


def manpages_category(library: str) -> str:
    """Категория по короткому имени библиотеки из LIBRARY-страницы.

    Пустая строка библиотеки (страницы без LIBRARY) → fallback-категория.
    Неизвестная библиотека (нет в таблице) → тоже fallback: страница
    документирована man-pages, значит системная.
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
