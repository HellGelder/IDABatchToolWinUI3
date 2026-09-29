"""Определение системных библиотек по собственным словарям проекта.

Единственная точка принятия решения «является ли библиотека системной».
Источник истины — словари классификатора:

* Windows — ``WINDOWS_MODULES``;
* Linux / Android — ``LINUX_MODULES`` + ``ANDROID_MODULES``;
* macOS / iOS — ``MACOS_MODULES``.

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

# Контрактные DLL (API Sets) не перечислены полностью в словаре —
# они определяются по префиксу имени и относятся только к Windows.
_API_SET_PREFIXES: tuple[str, ...] = (
    "api-ms-win-",
    "ext-ms-win-",
)


def _collect_keys(dicts: Iterable[Mapping[str, str]]) -> frozenset[str]:
    """Собирает все варианты нормализованных ключей из словарей."""
    keys: set[str] = set()
    for module_dict in dicts:
        for name in module_dict:
            keys.update(module_name_aliases(name))
    return frozenset(keys)


# Нормализованные ключи системных модулей по платформам — строятся лениво:
# module_name_aliases по всем словарям на import-е оплачивался даже прогонами,
# где is_system_module не вызывается ни разу.
_SYSTEM_KEYS: dict[str, frozenset[str]] = {}


def _system_keys(platform: str) -> frozenset[str]:
    """Нормализованные ключи системных модулей платформы (с кэшем)."""
    keys = _SYSTEM_KEYS.get(platform)
    if keys is None:
        if platform == "Windows":
            keys = _collect_keys([WINDOWS_MODULES])
        elif platform == "Linux / Android":
            keys = _collect_keys([LINUX_MODULES, ANDROID_MODULES])
        else:
            keys = _collect_keys([MACOS_MODULES])
        _SYSTEM_KEYS[platform] = keys
    return keys

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

    Неизвестное значение возвращается как есть — вызывающий код в этом
    случае проверяет модуль по всем словарям.
    """
    if not platform:
        return "Windows"
    return _PLATFORM_ALIASES.get(platform.strip().lower(), platform)


def is_system_module(module_name: str, platform: str = "Windows") -> bool:
    """Проверяет, относится ли модуль к системным библиотекам платформы.

    Args:
        module_name: имя модуля из импорта (``kernel32.dll``, ``libc.so.6``,
            ``Foundation.framework`` и т.п.).
        platform: целевая платформа (ключ ``PLATFORM_EXTENSIONS`` или его
            нестрогий синоним). Для неизвестной платформы модуль проверяется
            по словарям всех платформ.

    Returns:
        True, если модуль входит в системный словарь указанной платформы.
    """
    if not module_name:
        return False
    aliases = module_name_aliases(module_name)
    if not aliases:
        return False

    platform = normalize_platform(platform)

    if platform not in ("Windows", "Linux / Android", "macOS / iOS"):
        # Неизвестная платформа — ищем по всем системным словарям.
        win = _system_keys("Windows")
        lin = _system_keys("Linux / Android")
        mac = _system_keys("macOS / iOS")
        if any(a in win for a in aliases):
            return True
        if any(a in lin for a in aliases):
            return True
        return any(a in mac for a in aliases)

    # API Sets относятся только к Windows и задаются префиксом.
    if platform == "Windows":
        primary = normalize_module_name(module_name)
        if any(primary.startswith(prefix) for prefix in _API_SET_PREFIXES):
            return True

    return any(alias in _system_keys(platform) for alias in aliases)
