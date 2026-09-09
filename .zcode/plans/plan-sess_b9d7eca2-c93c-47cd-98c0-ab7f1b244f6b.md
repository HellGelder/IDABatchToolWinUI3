# План: прогресс, параллельная БД, промежуточные результаты

## 1. Прогресс — показывать модуль и функцию

### Проблема
`progress_updated.emit(completed, total, "")` — message всегда пустой. Пользователь видит только "Генерация HTML: 3/10 ".

### Решение
- Добавить в `generate_report_from_json()` опциональный параметр `progress_callback`
- `progress_callback` вызывается для каждой функции: `progress_callback(func_name, idx, total_in_file)`
- В `SfaHtmlGeneratorWorker.process_one()` передать callback, который эмитит сигнал с именем файла и функции
- Итоговый формат: `"test.exe → CreateFileW (3/15)"`

**Изменяемые файлы:**
- `sfa_generator.py` — добавить `progress_callback` в `generate_report_from_json()`
- `sfa_html_generation.py` — передавать callback с именем файла и функции
- `sfa_page.py` — обновить `_on_html_progress` для отображения

---

## 2. Параллельный доступ к БД кэша

### Проблема
`ThreadPoolExecutor(max_workers=4)` → 4 потока вызывают `generate_report_from_json()` → каждый создаёт свой `SfaDocCache` → каждый со своим `self._lock` → локи не координируются → `sqlite3.OperationalError: database is locked`.

### Решение: единый DocCacheManager
Создать класс `DocCacheManager`, который:
- Является singleton'ом (один на всю программу)
- Держит ОДНО соединение с SQLite (или пул из одного потока)
- Принимает запросы на запись через очередь (`queue.Queue`)
- Фоновый поток (worker) читает из очереди и последовательно пишет в БД
- `save_results()` → кладёт задачу в очередь, не блокируя вызывающий поток
- `has_function()` / `get_results()` → читает напрямую (WAL + busy_timeout=5000)

**Изменяемые файлы:**
- `database/sfa_doc_cache.py` — добавить `DocCacheManager` с фоновым потоком-писателем
- `sfa_generator.py` — использовать `DocCacheManager` вместо прямого `SfaDocCache`

---

## 3. Промежуточная папка результатов

### Текущее состояние
`SFAReports` уже создаётся в `_do_generate_html()`:
```python
sfa_reports = input_dir / "SFAReports"
sfa_reports.mkdir(parents=True, exist_ok=True)
```

`.sfa.html` файлы уже пишутся туда сразу по мере обработки — это и есть промежуточные результаты.

### Дополнительно
- Создавать `SFAReports` **раньше**, в самом начале, и сразу показывать пользователю, куда пишутся результаты
- Выводить путь в лейбле: `"Результаты: input_dir/SFAReports"`

---

## Итого файлы для изменения

| Файл | Изменения |
|------|-----------|
| `database/sfa_doc_cache.py` | + `DocCacheManager` (очередь + фоновый писатель) |
| `reporting/sfa_generator.py` | + `progress_callback`, использовать `DocCacheManager` |
| `ui/workers/sfa_html_generation.py` | передавать callback с именем файла+функции |
| `ui/pages/sfa_page.py` | улучшить `_on_html_progress`, показать путь к SFAReports |