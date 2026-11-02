"""Тесты системности Linux по man-страницам (решение владельца 2026-11-02).

Запуск: python -m unittest discover -s _python/tests -v
"""
from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

_PYTHON_DIR = Path(__file__).resolve().parents[1]
if str(_PYTHON_DIR) not in sys.path:
    sys.path.insert(0, str(_PYTHON_DIR))

import report_bridge as rb  # noqa: E402
from classifier.system_modules import is_system_module  # noqa: E402


class DictionarySystemnessTest(unittest.TestCase):
    """Linux-системность по словарям отключена; Windows/macOS не затронуты."""

    def test_linux_dictionaries_disabled(self) -> None:
        for name in ("libc.so.6", "libm.so.6", "libpthread.so.0", "libnuma.so.1"):
            with self.subTest(name=name):
                self.assertFalse(is_system_module(name, "Linux / Android"))

    def test_other_platforms_unchanged(self) -> None:
        self.assertTrue(is_system_module("kernel32.dll", "Windows"))
        self.assertFalse(is_system_module("msvcrt.dll", "Windows"))
        self.assertTrue(is_system_module("Foundation.framework", "macOS / iOS"))


class Man2LibrarySetTest(unittest.TestCase):
    """Критерий: библиотека системная, если упомянута в man2."""

    def test_set_matches_man2(self) -> None:
        libs = rb._manpages_man2_libraries()
        self.assertIn("libc", libs)
        self.assertIn("librt", libs)
        self.assertIn("libnuma", libs)
        for name in ("libm", "libpthread", "libdl", "libcrypto"):
            with self.subTest(name=name):
                self.assertNotIn(name, libs)


class LibraryMatchingTest(unittest.TestCase):
    """LIBRARY-токены сверяются с зависимостями файла."""

    def test_all_tokens_matched(self) -> None:
        matched, unmatched = rb._match_needed_libraries(
            ["libc", "librt"],
            ["libc.so.6", "libm.so.6", "librt.so.1"],
        )
        self.assertEqual(matched, ["libc.so.6", "librt.so.1"])
        self.assertEqual(unmatched, [])

    def test_unmatched_token_kept(self) -> None:
        matched, unmatched = rb._match_needed_libraries(
            ["libpthread"], ["libc.so.6"])
        self.assertEqual(matched, [])
        self.assertEqual(unmatched, ["libpthread"])

    def test_loader_matches_libdl(self) -> None:
        matched, _ = rb._match_needed_libraries(
            ["libdl"], ["ld-linux-x86-64.so.2"])
        self.assertEqual(matched, ["ld-linux-x86-64.so.2"])


def _write_export(root: Path) -> Path:
    """Синтетический ELF-экспорт: системные, библиотечные и сырые вызовы."""
    inp = root / "input"
    jsons = root / "json"
    inp.mkdir(parents=True)
    jsons.mkdir()
    (inp / "app").write_bytes(b"")
    (inp / "libnuma.so.1").write_bytes(b"")
    (inp / "libcrypto.so.1.1").write_bytes(b"")

    data = {
        "file_name": str(inp / "app"),
        "file_size": 10,
        "is_elf": True,
        "is_macho": False,
        "functions": [],
        "exports": [],
        "imports": [
            {"name": "open", "module": ".dynsym", "address": "0x1"},
            {"name": "creat", "module": ".dynsym", "address": "0x2"},
            {"name": "bpf", "module": ".dynsym", "address": "0x3"},
            {"name": "printf", "module": ".dynsym", "address": "0x4"},
            {"name": "malloc", "module": ".dynsym", "address": "0x5"},
            {"name": "clock_getres", "module": ".dynsym", "address": "0x6"},
            {"name": "pthread_create", "module": ".dynsym", "address": "0x7"},
        ],
        "needed_libs": [
            "libc.so.6", "libm.so.6", "librt.so.1",
            "libnuma.so.1", "libcrypto.so.1.1",
        ],
        "elf_header": {},
        "elf_sections": [],
        "elf_segments": [],
        "hashes": {},
    }
    path = jsons / "app.export.json"
    path.write_text(json.dumps(data), encoding="utf-8")
    return path


class LinuxReportsTest(unittest.TestCase):
    """Системность функций и теги модулей на синтетическом экспорте."""

    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        self.json_path = _write_export(self.root)

    def tearDown(self) -> None:
        self._tmp.cleanup()

    def test_sfa_systemness_and_libraries(self) -> None:
        reports = self.root / "sfa"
        reports.mkdir()
        conn = rb._manpages_open([str(rb._manpages_embedded_path())], lambda _m: None)
        try:
            stats = rb.generate_sfa_report(
                str(self.json_path), str(reports / "app.sfa.html"),
                str(reports), str(self.root / "input"), "Linux / Android",
                manpages_conn=conn, log=lambda _m: None,
            )
        finally:
            conn.close()

        self.assertEqual(
            stats["system_names"], {"open", "creat", "bpf", "clock_getres"})
        self.assertEqual(stats["system_libs"], {"libc.so.6", "librt.so.1"})

        html = (reports / "app.sfa.html").read_text(encoding="utf-8")
        self.assertIn("не в зависимостях", html)   # pthread_create
        self.assertIn(">—<", html)                 # bpf: библиотеки нет

        index_path = rb.generate_sfa_index(
            str(reports), str(self.root / "input"),
            [{"filename": "app.sfa.html", "display_name": "app",
              "found_count": stats["found_count"],
              "notfound_count": stats["notfound_count"],
              "total_imports": stats["total_imports"],
              "system_count": stats["system_count"]}],
            {}, total_files=1, total_size_bytes=10,
            total_system_modules=len(stats["system_libs"]),
            total_system_functions=len(stats["system_names"]),
            total_system_notfound=len(stats["system_notfound_names"]),
            total_imports=stats["total_imports"],
            generation_time="test", platform="Linux / Android",
            modules=stats["system_libs"])
        shtml = Path(index_path).read_text(encoding="utf-8")
        self.assertIn("Стандартная библиотека C", shtml)

    def test_analysis_index_marks_only_man2_libraries(self) -> None:
        reports = self.root / "analysis"
        reports.mkdir()
        internal = rb._build_internal_set(str(self.root / "input"))
        modules = set(rb._collect_own_system_libs(
            str(self.root / "input"), "Linux / Android"))
        modules.update({
            "libc.so.6", "librt.so.1", "libnuma.so.1",
            "libm.so.6", "libcrypto.so.1.1",
        })
        index_path = rb.generate_analysis_index(
            str(reports), str(self.root / "input"), [], modules, {},
            internal_set=internal, platform="Linux / Android")

        html = Path(index_path).read_text(encoding="utf-8")
        self.assertIn("Политики NUMA", html)  # описание из словаря man-pages
        for name, expected in (
            ("libc.so.6", True),
            ("librt.so.1", True),
            ("libnuma.so.1", True),
            ("libm.so.6", False),
            ("libcrypto.so.1.1", False),
        ):
            with self.subTest(name=name):
                around = html.split(name, 1)[1][:160]
                self.assertEqual("системная" in around, expected)


class DescriptionCoverageTest(unittest.TestCase):
    """У каждой библиотеки корпуса есть категория, имя и описание."""

    def test_every_corpus_library_is_described(self) -> None:
        from classifier.manpages_taxonomy import (
            MANPAGES_CATEGORY_DESCRIPTIONS,
            MANPAGES_CATEGORY_NAMES_RU,
            MANPAGES_LIB_CATEGORIES,
        )

        corpus = set(rb._manpages_library_sections())
        self.assertTrue(corpus)
        self.assertEqual(corpus - set(MANPAGES_LIB_CATEGORIES), set())
        self.assertEqual(corpus - set(MANPAGES_CATEGORY_DESCRIPTIONS), set())
        self.assertEqual(corpus - set(MANPAGES_CATEGORY_NAMES_RU), set())

    def test_classifier_describes_new_libraries(self) -> None:
        from classifier.platform_classifier import classify_module

        for soname, needle in (
            ("libnuma.so.1", "NUMA"),
            ("libkeyutils.so.1", "ключами"),
            ("libaio.so.1", "Асинхронный"),
        ):
            with self.subTest(soname=soname):
                self.assertIn(needle, classify_module(soname))


if __name__ == "__main__":
    unittest.main()
