"""Логика классификации и группировка модулей по категориям.

Linux / Android классифицируются по таксономии man-pages
(manpages_taxonomy): категории «строго по библиотекам» - каждая
библиотека man-pages является отдельной категорией, bionic/AOSP-словари
вынесены в «Системные библиотеки Android». Ручные групповые словари
linux.py ликвидированы: функции документируются по man-pages, и
классификация следует за корпусом, а не за словарём.
"""
from typing import Tuple

# Импортируем словари для категорий
from .windows import (
    _WINDOWS_HAL, _WINDOWS_NATIVE_API, _WINDOWS_KERNEL_SUBSYSTEM,
    _WINDOWS_USER_SUBSYSTEM, _WINDOWS_SYSTEM_SERVICES,
    _WINDOWS_USB_DEVICE, _WINDOWS_DOTNET,
    _WINDOWS_API_SETS, _WINDOWS_SECURITY_CRYPTO,
    _WINDOWS_NETWORK, _WINDOWS_GRAPHICS, _WINDOWS_MULTIMEDIA,
    _WINDOWS_RUNTIME, _WINDOWS_REMOTE, _WINDOWS_DATA_SERVICES,
    _WINDOWS_KERNEL_DRIVERS,
)
from .android import (
    _ANDROID_CORE, _ANDROID_BINDER, _ANDROID_HIDL,
    _ANDROID_HARDWARE,
    _ANDROID_NATIVE_WINDOW, _ANDROID_MEDIA, _ANDROID_GRAPHICS,
    _ANDROID_NETWORK, _ANDROID_BLUETOOTH, _ANDROID_RUNTIME,
    _ANDROID_CRYPTO, _ANDROID_SECURITY, _ANDROID_NDK,
    _ANDROID_ICU, _ANDROID_CAMERA, _ANDROID_AUDIO,
    _ANDROID_JNI, _ANDROID_COMPRESSION, _ANDROID_EXPAT,
)
from .macos import (
    _SYSTEM_CORE, _SECURITY_CRYPTO, _NETWORK_WEB, _UI_GRAPHICS,
    _MULTIMEDIA, _DATA_STORAGE, _ML_VISION,
    _SYSTEM_SERVICES, _DEVELOPER_TOOLS,
    _APP_SERVICES, _SWIFT_RUNTIME,
)
from .third_party import (
    _THIRD_PARTY_CRYPTO, _THIRD_PARTY_NETWORK, _THIRD_PARTY_BROWSER,
    _THIRD_PARTY_VPN, _THIRD_PARTY_RPC, _THIRD_PARTY_RPC_MORE,
    _THIRD_PARTY_JAVASCRIPT, _THIRD_PARTY_GRAPHICS,
    _THIRD_PARTY_IMAGE, _THIRD_PARTY_AUDIO, _THIRD_PARTY_CV,
    _THIRD_PARTY_GAME_MULTIMEDIA, _THIRD_PARTY_2D_GRAPHICS,
    _THIRD_PARTY_GUI_MORE, _THIRD_PARTY_TERMINAL,
    _THIRD_PARTY_ML, _THIRD_PARTY_ML_MORE,
    _THIRD_PARTY_COMPRESSION, _THIRD_PARTY_COMPRESSION_MORE,
    _THIRD_PARTY_DATABASE, _THIRD_PARTY_LOGGING,
    _THIRD_PARTY_XML, _THIRD_PARTY_CRYPTO_MORE,
    _THIRD_PARTY_VIRTUALIZATION, _THIRD_PARTY_QT,
)
from .manpages_taxonomy import (
    MANPAGES_LIB_CATEGORIES, MANPAGES_CATEGORY_DESCRIPTIONS,
    GLIBC_SONAMES, BIONIC_SONAMES, LOADER_SONAMES,
    CATEGORY_OS_FALLBACK as _MANPAGES_OS_FALLBACK,
    CATEGORY_ANDROID as _MANPAGES_ANDROID,
)


def _manpages_lib_dict(lib: str) -> dict[str, str]:
    """Записи категории одной библиотеки man-pages: glibc- и bionic-soname
    плюс короткая форма → описание. Для libdl добавляется загрузчик
    (страницы ld.so не имеют секции LIBRARY - утверждённая таксономия)."""
    desc = MANPAGES_CATEGORY_DESCRIPTIONS.get(lib, "")
    entries: dict[str, str] = {}
    for name in (GLIBC_SONAMES.get(lib), BIONIC_SONAMES.get(lib), lib):
        if name:
            entries.setdefault(name, desc)
    if lib == "libdl":
        for loader in LOADER_SONAMES:
            entries.setdefault(loader, desc)
    return entries


_CATEGORIES = {
    # Категории man-pages «строго по библиотекам» - ПЕРЕД fallback'ами:
    # get_module_category берёт первую категорию, и «libc.so» bionic должен
    # попасть в «libc», а не в «Системные библиотеки Android».
    **{
        lib: {
            "description": MANPAGES_CATEGORY_DESCRIPTIONS.get(lib, ""),
            "dicts": [_manpages_lib_dict(lib)],
        }
        for lib in MANPAGES_LIB_CATEGORIES
    },
    _MANPAGES_ANDROID: {
        "description": (
            "Системные библиотеки Android (bionic/AOSP): libc, libm, libdl, "
            "логирование (liblog), Binder IPC, HIDL, cutils, media, "
            "графика, сеть, Bluetooth, NDK, ICU, JNI, сжатие. "
            "В man-pages эти библиотеки не документированы - описывает "
            "словарь android.py."
        ),
        "dicts": [
            _ANDROID_CORE, _ANDROID_BINDER, _ANDROID_HIDL,
            _ANDROID_RUNTIME, _ANDROID_HARDWARE,
            _ANDROID_JNI, _ANDROID_COMPRESSION,
        ],
    },
    _MANPAGES_OS_FALLBACK: {
        "description": (
            "Базовые библиотеки операционной системы: "
            "управление памятью, процессами, файлами, системные вызовы, "
            "аппаратная абстракция, подсистемы совместимости (WOW64), "
            "динамическая загрузка, POSIX-функции, "
            "фреймворки macOS/iOS."
        ),
        "dicts": [
            _WINDOWS_HAL, _WINDOWS_NATIVE_API, _WINDOWS_KERNEL_SUBSYSTEM,
            _WINDOWS_USER_SUBSYSTEM, _WINDOWS_SYSTEM_SERVICES,
            _WINDOWS_USB_DEVICE,
            _WINDOWS_API_SETS, _WINDOWS_DOTNET,
            _WINDOWS_REMOTE, _WINDOWS_DATA_SERVICES,
            _WINDOWS_KERNEL_DRIVERS,
            _SYSTEM_CORE, _SYSTEM_SERVICES, _APP_SERVICES, _SWIFT_RUNTIME,
        ],
    },
    "Криптография и безопасность": {
        "description": (
            "Криптографические операции (шифрование, хеширование, цифровые подписи), "
            "протоколы SSL/TLS, управление сертификатами, "
            "системы аутентификации, контроль доступа (AppArmor, SELinux, PAM), "
            "хеширование паролей, генерация ключей."
        ),
        "dicts": [
            _WINDOWS_SECURITY_CRYPTO,
            _SECURITY_CRYPTO,
            _ANDROID_CRYPTO, _ANDROID_SECURITY,
            _THIRD_PARTY_CRYPTO, _THIRD_PARTY_CRYPTO_MORE,
        ],
    },
    "Сеть и коммуникации": {
        "description": (
            "Сетевые протоколы (TCP/UDP, HTTP, FTP, WebSocket), "
            "разрешение DNS, удалённый вызов процедур (RPC, gRPC, AMQP, Kafka), "
            "VPN-туннели, Bluetooth, браузерные движки (CEF, WebKit), "
            "JavaScript-движки (V8)."
        ),
        "dicts": [
            _WINDOWS_NETWORK,
            _NETWORK_WEB,
            _ANDROID_NETWORK, _ANDROID_BLUETOOTH,
            _THIRD_PARTY_NETWORK, _THIRD_PARTY_BROWSER,
            _THIRD_PARTY_VPN, _THIRD_PARTY_RPC, _THIRD_PARTY_RPC_MORE,
            _THIRD_PARTY_JAVASCRIPT,
        ],
    },
    "Графика и мультимедиа": {
        "description": (
            "2D/3D-графика, видео и аудио, рендеринг шрифтов, "
            "обработка изображений (JPEG, PNG, WebP, RAW), "
            "звуковые системы (PulseAudio, ALSA, OpenAL), "
            "компьютерное зрение (OpenCV), игровые движки (OGRE, Bullet Physics), "
            "медиа-фреймворки (FFmpeg, GStreamer, VLC), GUI-тулкиты (Qt, GTK, wxWidgets)."
        ),
        "dicts": [
            _WINDOWS_GRAPHICS, _WINDOWS_MULTIMEDIA,
            _UI_GRAPHICS, _MULTIMEDIA, _ML_VISION,
            _ANDROID_NATIVE_WINDOW, _ANDROID_MEDIA, _ANDROID_GRAPHICS,
            _ANDROID_CAMERA, _ANDROID_AUDIO,
            _THIRD_PARTY_GRAPHICS, _THIRD_PARTY_IMAGE, _THIRD_PARTY_AUDIO,
            _THIRD_PARTY_CV, _THIRD_PARTY_GAME_MULTIMEDIA,
            _THIRD_PARTY_2D_GRAPHICS, _THIRD_PARTY_GUI_MORE,
            _THIRD_PARTY_TERMINAL,
        ],
    },
    "Среды выполнения, научные и ML-библиотеки": {
        "description": (
            "Библиотеки времени выполнения (MSVCRT, libstdc++), "
            "машинное обучение (TensorFlow, PyTorch, ONNX Runtime, TensorRT), "
            "высокопроизводительные вычисления (OpenBLAS, Armadillo, GSL, FFTW), "
            "арифметика произвольной точности (GMP, MPFR), "
            "NDK-библиотеки Android, ICU для интернационализации."
        ),
        "dicts": [
            _WINDOWS_RUNTIME,
            _SWIFT_RUNTIME, _DEVELOPER_TOOLS,
            _ANDROID_NDK, _ANDROID_ICU,
            _THIRD_PARTY_ML, _THIRD_PARTY_ML_MORE,
        ],
    },
    "Работа с данными, архивация и XML": {
        "description": (
            "Сжатие и архивация (zlib, bzip2, LZMA, Brotli, Zstd, Snappy, LZ4), "
            "работа с архивами (ZIP, RAR, tar, 7z), "
            "базы данных (SQLite, MySQL, PostgreSQL), "
            "парсеры XML (libxml2, Expat), логирование (log4cplus, log4net), "
            "сериализация (Protocol Buffers, MessagePack, Avro, FlatBuffers), "
            "шрифты (Fontconfig)."
        ),
        "dicts": [
            _DATA_STORAGE,
            _ANDROID_EXPAT,
            _THIRD_PARTY_COMPRESSION, _THIRD_PARTY_COMPRESSION_MORE,
            _THIRD_PARTY_DATABASE, _THIRD_PARTY_LOGGING,
            _THIRD_PARTY_XML, _THIRD_PARTY_RPC_MORE,
            _THIRD_PARTY_VIRTUALIZATION,
            _THIRD_PARTY_QT,
        ],
    },
}


def get_module_category_and_description(module_name: str) -> Tuple[str, str]:
    """Возвращает категорию и описание категории (используется в отчётах).

    Поиск по кэшированной карте «нормализованный ключ → категория»:
    перестройка всех нормализованных ключей на каждый вызов давала O(n²)
    на сотнях модулей отчёта. Приоритет категорий и результат - те же,
    что при переборе словарей: первая категория из ``_CATEGORIES``.
    """
    from .naming import normalize_module_name

    category = _build_category_key_map().get(normalize_module_name(module_name))
    if category is not None:
        return category, _CATEGORIES[category]["description"]
    return "Неопознанные модули", ""


def get_category_description(category: str) -> str:
    """Словесное описание категории (пусто для неизвестной категории)."""
    info = _CATEGORIES.get(category)
    return info.get("description", "") if info else ""


def get_module_category(module_name: str) -> str:
    """Возвращает короткое название категории модуля (или пустую строку).

    Быстрая кэшированная альтернатива ``get_module_category_and_description``,
    когда нужно только название категории.
    """
    from .naming import normalize_module_name

    if not module_name:
        return ""
    return _build_category_key_map().get(normalize_module_name(module_name), "")


# Кэш: нормализованный ключ модуля → название категории.
_CATEGORY_KEY_MAP: dict[str, str] | None = None


def _build_category_key_map() -> dict[str, str]:
    """Строит и кэширует отображение «ключ модуля → категория».

    Приоритет задаётся порядком ``_CATEGORIES`` - то же поведение, что и
    у ``get_module_category_and_description``, но за один lookup вместо
    перебора всех словарей.
    """
    global _CATEGORY_KEY_MAP
    if _CATEGORY_KEY_MAP is not None:
        return _CATEGORY_KEY_MAP

    from .naming import normalize_module_name

    mapping: dict[str, str] = {}
    for category, info in _CATEGORIES.items():
        for d in info["dicts"]:
            for key in d:
                mapping.setdefault(normalize_module_name(key), category)
    _CATEGORY_KEY_MAP = mapping
    return mapping
