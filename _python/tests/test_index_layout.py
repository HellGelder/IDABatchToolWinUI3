"""Тесты компоновки сводного индекса анализа ПО (решения владельца 2026-11-10).

Проверяют:
- Linux/Android: три колонки «Тег / Библиотека / Секции man-страниц»,
  правило тегов (системная / собственная / импортирована), отсутствие
  счётчика «секций: N»;
- Windows: отдельный шаблон без каких-либо меток и тегов секций;
- «Исследованные файлы» отсортированы по путям.

Запуск: python -m unittest discover -s _python/tests -v
"""
from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path

_PYTHON_DIR = Path(__file__).resolve().parents[1]
if str(_PYTHON_DIR) not in sys.path:
    sys.path.insert(0, str(_PYTHON_DIR))

import report_bridge as rb  # noqa: E402


class ModuleStatusTagsTest(unittest.TestCase):
    """Правило колонки «Тег»: системность и принадлежность папке."""

    def test_system_outside_folder(self) -> None:
        self.assertEqual(rb._module_status_tags(True, False), ["системная"])

    def test_system_inside_folder_keeps_both(self) -> None:
        self.assertEqual(rb._module_status_tags(True, True),
                         ["системная", "собственная"])

    def test_own_not_system(self) -> None:
        self.assertEqual(rb._module_status_tags(False, True), ["собственная"])

    def test_imported_not_system(self) -> None:
        self.assertEqual(rb._module_status_tags(False, False), ["импортирована"])


class AnalysisIndexLayoutTest(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)

    def tearDown(self) -> None:
        self._tmp.cleanup()

    def _index(self, platform: str, modules: set[str], internal: set[str],
               reports=None) -> str:
        reports_dir = self.root / platform.replace(" ", "_")
        reports_dir.mkdir(parents=True, exist_ok=True)
        path = rb.generate_analysis_index(
            str(reports_dir), "", reports or [], modules, {},
            internal_set=internal, platform=platform)
        return Path(path).read_text(encoding="utf-8")

    def _row(self, html: str, name: str) -> str:
        pos = html.index(name)
        return html[html.rfind("<tr>", 0, pos):html.index("</tr>", pos)]

    def test_linux_has_three_columns_and_no_section_counter(self) -> None:
        html = self._index(
            "Linux / Android",
            {"libc.so.6", "libfoo.so", "libpng.so.3"},
            {"libfoo.so"})

        for header in ("Тег", "Библиотека", "Секции man-страниц"):
            self.assertIn(header, html)
        self.assertNotIn("секций:", html)

        self.assertIn("системная", self._row(html, "libc.so.6"))
        self.assertIn("собственная", self._row(html, "libfoo.so"))
        self.assertIn("импортирована", self._row(html, "libpng.so.3"))

        # Системная библиотека вне папки не помечается «собственная».
        self.assertNotIn("собственная", self._row(html, "libc.so.6"))

    def test_windows_template_has_no_tags(self) -> None:
        html = self._index(
            "Windows",
            {"kernel32.dll", "libfoo.dll"},
            {"libfoo.dll"})

        # Отдельный шаблон: split-view по категориям, без меток и секций.
        self.assertIn("catList", html)
        self.assertIn("Сформировано", html)
        for word in ("системная", "собственная", "импортирована",
                     "секций:", "Секции man-страниц"):
            self.assertNotIn(word, html)

    def test_researched_files_sorted_by_path(self) -> None:
        html = self._index(
            "Linux / Android", {"libc.so.6"}, set(),
            reports=[
                {"filename": "z/z.html", "display_name": "z/z"},
                {"filename": "a/a.html", "display_name": "a/a"},
                {"filename": "m/m.html", "display_name": "m/m"},
            ])

        self.assertLess(html.index("a/a"), html.index("m/m"))
        self.assertLess(html.index("m/m"), html.index("z/z"))


if __name__ == "__main__":
    unittest.main()
