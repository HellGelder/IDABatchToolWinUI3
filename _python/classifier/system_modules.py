"""Определение системных библиотек по собственным словарям проекта.

Единственная точка принятия решения «является ли библиотека системной».
Системными считаются только библиотеки категории «Системные библиотеки ОС»:
базовые компоненты платформы (kernel32, user32, libc, libpthread, …).
Криптография (libcrypto, advapi32), сеть (ws2_32, mpr), графика, работа с
данными и среды выполнения (msvcrt, libgcc_s) - не системные: они относятся
к своим категориям. Запись в объединённом словаре платформы сама по себе
системности не даёт - она нужна для описаний и канонизации имён.

Источник истины - словари классификатора:

* Windows - ``WINDOWS_MODULES``;
* Linux / Android - ``LINUX_MODULES`` + ``ANDROID_MODULES``;
* macOS / iOS - ``MACOS_MODULES``.

Сторонние библиотеки (``THIRD_PARTY_*``) системными не считаются: они
преднамеренно исключены, чтобы в отчёт попадали только компоненты ОС.

Нормализация имён выполняется общим ``normalize_module_name``, поэтому
записи словарей с расширениями ``.exe``/``.sys``/``.drv``/``.dylib``
распознаются наравне с ``.dll``. Дополнительно учитываются версионные
формы имён: импорт ``libz.so.1.2.11`` сопоставляется с ключом ``libz.so.1``.
"""
from __future__ import annotations

from typing import Iterable, Mapping

from .naming import module_name_aliases, normalize_module_name
from .windows import WINDOWS_MODULES
from .linux import LINUX_MODULES
from .android import ANDROID_MODULES
from .macos import MACOS_MODULES

# Контрактные DLL (API Sets) не перечислены полностью в словаре -
# они определяются по префиксу имени и относятся только к Windows.
_API_SET_PREFIXES: tuple[str, ...] = (
    "api-ms-win-",
    "ext-ms-win-",
)


def _collect_keys(dicts: Iterable[Mapping[str, str]]) -> frozenset[str]:
    """Собирает все варианты нормализованных ключей из словарей."""
    return frozenset(_collect_map(dicts))


def _collect_map(dicts: Iterable[Mapping[str, str]]) -> dict[str, str]:
    """Карта «вариант ключа → каноническое имя записи словаря».

    Порядок словарей важен: первая запись выигрывает, поэтому варианты,
    совпавшие с более приоритетным словарём, не перетираются.
    """
    result: dict[str, str] = {}
    for module_dict in dicts:
        for name in module_dict:
            for alias in module_name_aliases(name):
                result.setdefault(alias, name)
    return result


# Нормализованные ключи системных модулей по платформам - строятся лениво:
# module_name_aliases по всем словарям на import-е оплачивался даже прогонами,
# где is_system_module не вызывается ни разу.
_SYSTEM_MAP: dict[str, dict[str, str]] = {}


def _system_map(platform: str) -> dict[str, str]:
    """Карта «вариант ключа → имя записи словаря» для платформы (с кэшем)."""
    mapping = _SYSTEM_MAP.get(platform)
    if mapping is None:
        if platform == "Windows":
            mapping = _collect_map([WINDOWS_MODULES])
        elif platform == "Linux / Android":
            mapping = _collect_map([LINUX_MODULES, ANDROID_MODULES])
        else:
            mapping = _collect_map([MACOS_MODULES])
        _SYSTEM_MAP[platform] = mapping
    return mapping


def _system_keys(platform: str) -> frozenset[str]:
    """Нормализованные ключи системных модулей платформы (с кэшем)."""
    return frozenset(_system_map(platform))

# Допустимые нестрогие обозначения платформы.
_PLATFORM_ALIASES: dict[str, str] = {
    "windows": "Windows",
    "win": "Windows",
    "linux": "Linux / Android",
    "linux / android": "Linux / Android",
    "linux/android": "Linux / Android",
    "android": "Linux / Android",
    "macos": "macOS / iOS",
    "macos / ios": "macOS / iOS",
    "macos/ios": "macOS / iOS",
    "ios": "macOS / iOS",
    "macos / ios (darwin)": "macOS / iOS",
    "darwin": "macOS / iOS",
}


def normalize_platform(platform: str) -> str:
    """Приводит обозначение платформы к каноническому ключу.

    Неизвестное значение возвращается как есть - вызывающий код в этом
    случае проверяет модуль по всем словарям.
    """
    if not platform:
        return "Windows"
    return _PLATFORM_ALIASES.get(platform.strip().lower(), platform)


# Категория классификатора, по которой определяются системные библиотеки ОС:
# «системные» - это именно базовые библиотеки операционной системы (kernel32,
# user32, gdi32, libc, …). Криптография (libcrypto, advapi32), сеть (mpr,
# ws2_32), графика, данные и среды выполнения (msvcrt, libgcc_s) системными
# не считаются - они относятся к своим категориям и не входят в счётчики
# системных библиотек ни в индексе, ни в анализе СФ.
SYSTEM_OS_CATEGORY = "Системные библиотеки ОС"


def _is_os_library(module_name: str, platform: str = "Windows") -> bool:
    """Библиотека из перечня ОС (категория «Системные библиотеки ОС»)."""
    canon = get_system_module_name(module_name, platform)
    if not canon:
        return False
    from .categories import get_module_category

    return get_module_category(canon) == SYSTEM_OS_CATEGORY


def is_system_module(module_name: str, platform: str = "Windows") -> bool:
    """Относится ли модуль к системным библиотекам ОС (только категория ОС).

    Проверяется перечень библиотек операционной системы, а не объединённый
    словарь платформ: запись в LINUX_MODULES/WINDOWS_MODULES ещё не делает
    библиотеку системной (libcrypto - криптография, libgcc_s - среда
    выполнения, ws2_32 - сеть).

    Args:
        module_name: имя модуля из импорта (``kernel32.dll``, ``libc.so.6``,
            ``Foundation.framework`` и т.п.).
        platform: целевая платформа (ключ ``PLATFORM_EXTENSIONS`` или его
            нестрогий синоним). Для неизвестной платформы модуль проверяется
            по словарям всех платформ.

    Returns:
        True, если модуль входит в категорию «Системные библиотеки ОС».
    """
    return _is_os_library(module_name, platform)


def get_system_module_name(module_name: str, platform: str = "Windows") -> str:
    """Каноническое имя системной библиотеки из словаря платформы.

    Возвращает запись словаря (``libpthread.so.0``, ``kernel32.dll``), по
    которой нашёлся вариант имени, либо пустую строку. Нужно, чтобы по
    короткому имени из man-страницы (``libpthread``) получать полное имя для
    категории, описания и отчёта.
    """
    if not module_name:
        return ""
    aliases = module_name_aliases(module_name)
    if not aliases:
        return ""

    platform = normalize_platform(platform)

    # API Sets относятся только к Windows и задаются префиксом.
    if platform == "Windows":
        primary = normalize_module_name(module_name)
        if any(primary.startswith(prefix) for prefix in _API_SET_PREFIXES):
            return module_name

    if platform in ("Windows", "Linux / Android", "macOS / iOS"):
        mapping = _system_map(platform)
        for alias in aliases:
            name = mapping.get(alias)
            if name:
                return name
        return ""

    # Неизвестная платформа - ищем по всем системным словарям.
    for key in ("Windows", "Linux / Android", "macOS / iOS"):
        mapping = _system_map(key)
        for alias in aliases:
            name = mapping.get(alias)
            if name:
                return name
    return ""


# Категория классификатора, по которой формируются отчёты СФ, - та же
# «Системные библиотеки ОС» (см. SYSTEM_OS_CATEGORY выше). is_sfa_module
# оставлен как отдельное имя для читаемости вызывающего кода (СФ-отчёты):
# критерий совпадает с is_system_module, чтобы «Системных библиотек» в СФ и
# категория «Системные библиотеки ОС» в индексе не расходились.
def is_sfa_module(module_name: str, platform: str = "Windows") -> bool:
    """Модуль входит в отчёты СФ: системная библиотека категории
    «Системные библиотеки ОС» (критерий тот же, что у is_system_module).

    Криптография (libcrypto/libssl), сеть, графика и runtime (libgcc_s)
    системными не считаются - иначе «Системных библиотек» в СФ расходится
    с категорией «Системные библиотеки ОС» отчёта анализа ПО.
    """
    return _is_os_library(module_name, platform)
