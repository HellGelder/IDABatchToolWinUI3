# Graph Report - IDABatchTool  (2026-09-15)

## Corpus Check
- cluster-only mode — file stats not available

## Summary
- 5596 nodes · 12073 edges · 238 communities (196 shown, 33 thin omitted)
- Extraction: 92% EXTRACTED · 8% INFERRED · 0% AMBIGUOUS · INFERRED: 908 edges (avg confidence: 0.92)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `7adfd54b`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- token.py
- words
- RegexLexer
- include
- compiled.py
- chart.umd.min.js
- lexers/other.py
- templates.py
- text.py
- util.py
- default
- s
- an
- DiffWorker
- DelegatingLexer
- PythonLexer
- filters/__init__.py
- va
- marked.min.js
- ns
- CBinDiff
- javascript.py
- lexers/html.py
- no
- generator.py
- CIDABinDiff
- diaphora_ida.py
- get_bool_opt
- CKoretFuzzyHashing
- Formatter
- export_data.py
- loader.py
- scripting.py
- diaphora.py
- ImageFormatter
- haskell.py
- log
- DocCacheManager
- bo
- SfaReportGenerator
- .diff
- platform_classifier.py
- bn
- RegexLexerMeta
- ManPagesDatabase
- SfaFunctionIndex
- AnalysisPage
- SfaPage
- CIDAChooser
- RoffParser
- .getContext
- bt
- XmlLexer
- .db_cursor
- MainWindow
- XQueryLexer
- IDAAnalyzer
- .log
- updateElements
- zt
- basicutils_7x.py
- SettingsPage
- highlight
- Terminal256Formatter
- CDiaphoraTester
- .add_match
- diaphora_heuristics.py
- RubyLexer
- YamlLexer
- tn
- robotframework.py
- Style
- normalize_module_name
- DiffPage
- PhpLexer
- Page
- CVulnerabilityPatches
- latex.py
- Scanner
- CKoretKaramitasHash
- jkutils/factor.py
- CSourceFilesChooser
- basic.py
- TreemapWidget
- utils.py
- CssDjangoLexer
- Page
- diaphora_local.py
- HtmlFormatter
- markup.py
- .show_callgraph_context
- .register_menu
- FontManager
- MIMELexer
- VariableSplitter
- Page
- formatter.py
- CssGenshiLexer
- LassoLexer
- sql.py
- IDAMagicStrings.py
- CClassesGraph
- module_callbacks
- AnalysisWorker
- .get_tokens_unprocessed
- TestCaseTable
- MainWindow
- main.py
- jn
- .save_function
- .process_basic_block
- diaphora_plugin.py
- ValueError
- special.py
- TNTLexer
- Page
- NextFunction
- CDebuggingHelper
- CExampleDiaphoraHooks
- CCandidateFunctionNames
- .__init__
- Window
- CCallGraphViewer
- cc_main.py
- map_read.py
- CChooser
- JsonLexer
- .__init__
- EvoqueHtmlLexer
- style.py
- .__init__
- Perl6Lexer
- ShellSessionBaseLexer
- PostgresBase
- IDABatchToolWinUI.Pages
- PygmentsDoc
- format_lines
- .find_one_match_diffing
- CMyHooks
- CBaseTreeViewer
- CIDAMagicStringsAction
- .get_tokens
- RadioButton
- rs
- module.py
- rtf.py
- ShenLexer
- _Table
- lookahead
- StyleMeta
- _TokenType
- lfa.py
- CExcludeHeuristicHooks
- .get_style_defs
- _PseudoMatch
- CSharpAspxLexer
- Modula2Lexer
- slash.py
- export_cfg.py
- cc_base.py
- .get_sources_diff_data
- ._get_css_class
- ._format_lines
- oberon.py
- _postgres_builtins.py
- autumn.py
- default.py
- fruity.py
- solarized.py
- CFPSChecker
- IDABatchToolWinUI.csproj
- Win32DatabaseSync
- modnaming.py
- CryptolLexer
- MarkdownLexer
- _php_builtins.py
- qlik.py
- RobotFrameworkLexer
- VimLexer
- HttpLexer
- gruvbox.py
- nord.py
- CPrinter_t
- CHtmlViewer
- CFakeString
- EzhilLexer
- RstLexer
- RebolLexer
- _scilab_builtins.py
- UrbiscriptLexer
- _vim_builtins.py
- get_filetype_from_buffer
- add_sample.py
- clean_directory
- HaxeLexer
- CommonLispLexer
- SourcePawnLexer
- .analyse_text
- UcodeLexer
- arduino.py
- borland.py
- colorful.py
- friendly.py
- friendly_grayscale.py
- gh_dark.py
- styles/igor.py
- styles/lilypond.py
- lovelace.py
- material.py
- monokai.py
- murphy.py
- onedark.py
- pastie.py
- perldoc.py
- rainbow_dash.py
- rrt.py
- styles/sas.py
- staroffice.py
- tango.py
- trac.py
- vim.py
- xcode.py
- zenburn.py
- _inherit
- database/__init__.py
- _asy_builtins.py
- _cl_builtins.py
- _cocoa_builtins.py
- _lasso_builtins.py

## God Nodes (most connected - your core abstractions)
1. `RegexLexer` - 561 edges
2. `include` - 332 edges
3. `words` - 307 edges
4. `default` - 186 edges
5. `bygroups()` - 148 edges
6. `CBinDiff` - 110 edges
7. `CIDABinDiff` - 106 edges
8. `DelegatingLexer` - 92 edges
9. `Style` - 91 edges
10. `an()` - 61 edges

## Surprising Connections (you probably didn't know these)
- `Lexer` --uses--> `Filter`  [INFERRED]
  IDABatchTool/scripts/diaphora/pygments/lexer.py → IDABatchTool/scripts/diaphora/pygments/filter.py
- `IDLLexer` --uses--> `words`  [INFERRED]
  IDABatchTool/scripts/diaphora/pygments/lexers/idl.py → IDABatchTool/scripts/diaphora/pygments/lexer.py
- `OctaveLexer` --uses--> `words`  [INFERRED]
  IDABatchTool/scripts/diaphora/pygments/lexers/matlab.py → IDABatchTool/scripts/diaphora/pygments/lexer.py
- `ScilabLexer` --uses--> `words`  [INFERRED]
  IDABatchTool/scripts/diaphora/pygments/lexers/matlab.py → IDABatchTool/scripts/diaphora/pygments/lexer.py
- `Python2TracebackLexer` --uses--> `default`  [INFERRED]
  IDABatchTool/scripts/diaphora/pygments/lexers/python.py → IDABatchTool/scripts/diaphora/pygments/lexer.py

## Import Cycles
- None detected.

## Communities (238 total, 33 thin omitted)

### Community 0 - "token.py"
Cohesion: 0.02
Nodes (139): bygroups(), callback(), do_insertions(), Lexer, pygments.lexer ~~~~~~~~~~~~~~ Base lexer classes. :copyright: Copyright…, Has to return a float between ``0`` and ``1`` that indicates if a lexer wants…, Return an iterable of (index, tokentype, value) pairs where "index" is the…, Callback that yields multiple actions for each group in the match. (+131 more)

### Community 1 - "words"
Cohesion: 0.02
Nodes (114): Indicates a list of literal words that is transformed into an optimized regex…, words, MathematicaLexer, Lexer for Mathematica source code. .. versionadded:: 2.0, AmbientTalkLexer, Lexer for AmbientTalk source code. .. versionadded:: 2.0, AMDGPULexer, pygments.lexers.amdgpu ~~~~~~~~~~~~~~~~~~~~~~ Lexers for the AMDGPU ISA… (+106 more)

### Community 2 - "RegexLexer"
Cohesion: 0.02
Nodes (130): ProfilingRegexLexer, Base for simple stateful regular expression-based lexers. Simplifies the lexing…, Drop-in replacement for RegexLexer that does profiling of its regexes., RegexLexer, MxmlLexer, For MXML markup. Nested AS3 in <script> tags is highlighted by the appropriate…, BCLexer, GAPLexer (+122 more)

### Community 3 - "include"
Cohesion: 0.02
Nodes (99): include, str, Indicates that a state should include rules from another state., apdlexer, pygments.lexers.apdlexer ~~~~~~~~~~~~~~~~~~~~~~~~ Lexers for ANSYS Parametric…, For APDL source code. .. versionadded:: 2.9, AdlLexer, AtomsLexer (+91 more)

### Community 4 - "compiled.py"
Cohesion: 0.02
Nodes (111): ActionScriptLexer, This is only used to disambiguate between ActionScript and ActionScript3. We…, For ActionScript source code. .. versionadded:: 0.9, Ca65Lexer, Dasm16Lexer, GasLexer, HsailLexer, LlvmLexer (+103 more)

### Community 5 - "chart.umd.min.js"
Cohesion: 0.03
Nodes (58): Ae(), at(), average(), be(), beforeDatasetDraw(), beforeDatasetsDraw(), beforeLayout(), d() (+50 more)

### Community 6 - "lexers/other.py"
Cohesion: 0.02
Nodes (100): ABAPLexer, CobolFreeformatLexer, CobolLexer, GoodDataCLLexer, MaqlLexer, OpenEdgeLexer, pygments.lexers.business ~~~~~~~~~~~~~~~~~~~~~~~~ Lexers for "business-…, Lexer for Free format OpenCOBOL code. .. versionadded:: 1.6 (+92 more)

### Community 7 - "templates.py"
Cohesion: 0.03
Nodes (63): Lexer for Structured Query Language. Currently, this lexer does not recognize…, SqlLexer, Angular2HtmlLexer, Angular2Lexer, CheetahHtmlLexer, CheetahJavascriptLexer, CheetahLexer, CheetahPythonLexer (+55 more)

### Community 8 - "text.py"
Cohesion: 0.02
Nodes (78): ApacheConfLexer, AugeasLexer, Cfengine3Lexer, DockerLexer, IniLexer, KconfigLexer, LighttpdConfLexer, NestedTextLexer (+70 more)

### Community 9 - "util.py"
Cohesion: 0.05
Nodes (75): basename(), HelpFormatter, main(), main_inner(), _parse_filters(), _parse_options(), _print_help(), _print_list() (+67 more)

### Community 10 - "default"
Cohesion: 0.02
Nodes (71): default, Indicates a state or state action (e.g. #pop) to apply. For example…, AdaLexer, For Ada source code. .. versionadded:: 1.3, ArrowLexer, Lexer for Arrow .. versionadded:: 2.7, BibTeXLexer, BSTLexer (+63 more)

### Community 11 - "s"
Cohesion: 0.06
Nodes (62): a(), aa(), ai(), ao(), b(), beforeDraw(), cn(), ct() (+54 more)

### Community 12 - "an"
Cohesion: 0.06
Nodes (20): addBox(), afterDatasetsUpdate(), an(), ca(), configure(), generateLabels(), ke(), kn() (+12 more)

### Community 13 - "DiffWorker"
Cohesion: 0.06
Nodes (33): callable, Виджет страницы сравнения директорий с помощью BinDiff + Diaphora., _compute_hexdump_diff(), DiffWorker, _pulse(), _pulse(), _emit_progress(), _find_original_binary() (+25 more)

### Community 14 - "DelegatingLexer"
Cohesion: 0.04
Nodes (35): DelegatingLexer, This lexer takes two lexer as arguments. A root lexer and a language lexer.…, CObjdumpLexer, CppObjdumpLexer, DObjdumpLexer, For the output of ``objdump -Sr`` on compiled D files., AntlrActionScriptLexer, AntlrCppLexer (+27 more)

### Community 15 - "PythonLexer"
Cohesion: 0.04
Nodes (47): pygments.lexers.agile ~~~~~~~~~~~~~~~~~~~~~ Just export lexer classes…, CrocLexer, MiniDLexer, pygments.lexers.d ~~~~~~~~~~~~~~~~~ Lexers for D languages. :copyright:…, For MiniD source. MiniD is now known as Croc., FactorLexer, Lexer for the Factor language. .. versionadded:: 1.4, IoLexer (+39 more)

### Community 16 - "filters/__init__.py"
Cohesion: 0.05
Nodes (41): Filter, FunctionFilter, pygments.filter ~~~~~~~~~~~~~~~ Module that implements the default filter.…, Decorator that converts a function into a filter:: @simplefilter def…, Default filter. Subclass this class or use the `simplefilter` decorator to…, Abstract class used by `simplefilter` to create simple function filters on the…, simplefilter(), CodeTagFilter (+33 more)

### Community 17 - "va"
Cohesion: 0.08
Nodes (18): afterDraw(), afterEvent(), afterUpdate(), Ee(), f(), ki(), Le(), oa() (+10 more)

### Community 18 - "marked.min.js"
Cohesion: 0.06
Nodes (44): A(), blockTokens(), br(), checkbox(), code(), codespan(), constructor(), de() (+36 more)

### Community 19 - "ns"
Cohesion: 0.05
Nodes (13): As(), beforeUpdate(), buildTicks(), go(), ii(), initialize(), labelColor(), labelPointStyle() (+5 more)

### Community 20 - "CBinDiff"
Cohesion: 0.04
Nodes (30): CBinDiff, Try to get a valid structure definition by removing (yes) the invalid…, Get a prettified form of the given assembly source, Internal re.sub wrapper to replace things in pseudocodes and assembly, Convert the input assembly @asm to an easier format to text diff using lists, Convert the input pseudocode @pseudo to an easier format to text diff using…, Return a better string to diff assembly text for the given input @asm text, Compare the given basic blocks and calculate each basic block's colour. It's… (+22 more)

### Community 21 - "javascript.py"
Cohesion: 0.04
Nodes (46): combined, tuple, Indicates a state combined from multiple states., AutohotkeyLexer, AutoItLexer, pygments.lexers.automation ~~~~~~~~~~~~~~~~~~~~~~~~~~ Lexers for automation…, For autohotkey source code. .. versionadded:: 1.4, For AutoIt files. AutoIt is a freeware BASIC-like scripting language designed… (+38 more)

### Community 22 - "lexers/html.py"
Cohesion: 0.06
Nodes (46): ExtendedRegexLexer, A RegexLexer that uses a context object to store its state., ActionScript3Lexer, For ActionScript 3 source code. .. versionadded:: 0.11, Lexer for terraformi ``.tf`` files. .. versionadded:: 2.1, TerraformLexer, pygments.lexers._css_builtins ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ This file is…, CssLexer (+38 more)

### Community 23 - "no"
Cohesion: 0.06
Nodes (16): buildLookupTable(), ea(), En, Fo(), _generate(), getDecimalForValue(), _getTimestampsForTable(), ha (+8 more)

### Community 24 - "generator.py"
Cohesion: 0.08
Nodes (29): ABC, get_module_category_and_description(), Возвращает категорию и описание категории (используется в отчётах)., describe_section(), describe_segment(), _normalize(), Справочник назначений секций и сегментов ELF на русском языке. Используется в…, Приводит имя секции/сегмента к виду, пригодному для поиска в словаре. (+21 more)

### Community 25 - "CIDABinDiff"
Cohesion: 0.06
Nodes (14): CodeRefsTo(), CIDABinDiff, log_refresh(), Get the last inserted row before IDA or Diaphora crashed., Recalculate the primes assigned to a function., Internal use, export the database., Export the current database. Call script hooks if there is any., Print a message and refresh the UI. (+6 more)

### Community 26 - "diaphora_ida.py"
Cohesion: 0.06
Nodes (35): ctree_visitor_t, Form, BinDiffOptions, CAstVisitor, CAstVisitorInherits, CBinDiffExporterSetup, CExternalDiffingDialog, debug_refresh() (+27 more)

### Community 27 - "get_bool_opt"
Cohesion: 0.07
Nodes (19): BBCodeFormatter, pygments.formatters.bbcode ~~~~~~~~~~~~~~~~~~~~~~~~~~ BBcode formatter.…, Format tokens with BBcodes. These formatting codes are used by many bulletin…, GroffFormatter, pygments.formatters.groff ~~~~~~~~~~~~~~~~~~~~~~~~~ Formatter for groff output.…, Format tokens with groff escapes to change their color and font style. ..…, escape_html(), pygments.formatters.html ~~~~~~~~~~~~~~~~~~~~~~~~ Formatter for HTML output.… (+11 more)

### Community 28 - "CKoretFuzzyHashing"
Cohesion: 0.05
Nodes (27): CFileStr, CKoretFuzzyHashing, kdha, kfha, ksha, main(), modsum(), str (+19 more)

### Community 29 - "Formatter"
Cohesion: 0.06
Nodes (25): ansiformat(), colorize(), pygments.console ~~~~~~~~~~~~~~~~ Format colored console output. :copyright:…, Format ``text`` with a color and/or some attributes:: color normal color…, Formatter, Converts a token stream to text. Options accepted: ``style`` The style to use,…, Return the style definitions for the current style as a string. ``arg`` is an…, ircformat() (+17 more)

### Community 30 - "export_data.py"
Cohesion: 0.07
Nodes (43): _compute_file_hashes(), _decompile_function(), _elf_flag_names(), _elf_meta_template(), export_to_json(), callback(), _extract_framework_name(), _format_hexdump_with_ascii() (+35 more)

### Community 31 - "loader.py"
Cohesion: 0.10
Nodes (39): extract_archive(), find_7z(), Path, Обработка архивов APK, IPA, DMG. Распаковка в соседнюю папку., Возвращает путь к 7z, если он доступен в PATH., Извлекает архив в указанную папку (по умолчанию рядом с архивом, имя = stem…, _default_config(), _find_bindiff_in_common_locations() (+31 more)

### Community 32 - "scripting.py"
Cohesion: 0.05
Nodes (36): pygments.lexers._csound_builtins ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ :copyright:…, CsoundDocumentLexer, CsoundLexer, CsoundOrchestraLexer, CsoundScoreLexer, pygments.lexers.csound ~~~~~~~~~~~~~~~~~~~~~~ Lexers for Csound languages.…, For `Csound <https://csound.com>`_ scores. .. versionadded:: 2.1, For `Csound <https://csound.com>`_ orchestras. .. versionadded:: 2.1 (+28 more)

### Community 33 - "diaphora.py"
Cohesion: 0.06
Nodes (29): DataFrame, Diaphora database related support Copyright (c) 2023, Joxean Koret This program…, Diaphora database schema support Copyright (c) 2023, Joxean Koret This program…, ast_ratio(), CBytesEncoder, check_bufs(), Behold, Diaphora 3.X configuration options ahead! NOTES: This configuration…, # NOTE: Colors are specified in the format 0xBGR instead of RGB. (+21 more)

### Community 34 - "ImageFormatter"
Cohesion: 0.07
Nodes (25): BmpImageFormatter, GifImageFormatter, ImageFormatter, JpgImageFormatter, pygments.formatters.img ~~~~~~~~~~~~~~~~~~~~~~~ Formatter for Pixmap output.…, Create a PNG image from source code. This uses the Python Imaging Library to…, Get the height of a line., Get the Y coordinate of a line number. (+17 more)

### Community 35 - "haskell.py"
Cohesion: 0.05
Nodes (30): lex(), Pygments ~~~~~~~~ Pygments is a syntax highlighting package written in Python.…, Lex ``code`` with ``lexer`` and return an iterable of tokens., BooLexer, CSharpLexer, FSharpLexer, GenericAspxLexer, pygments.lexers.dotnet ~~~~~~~~~~~~~~~~~~~~~~ Lexers for .net languages.… (+22 more)

### Community 36 - "log"
Cohesion: 0.07
Nodes (13): log(), Import IDA's Type Libraries., Import structs, enums and unions, Reinitialize databases., Import only the definitions (TIL and structs/enums/unions)., Return the 3 different names that a function might have. Yeah, well..., Build topological relationships between basic blocks, Extract all features from the function (topological, assembly, pseudocode, etc.) (+5 more)

### Community 37 - "DocCacheManager"
Cohesion: 0.06
Nodes (20): DocCacheManager, Path, SQLite-кэш для документации Microsoft Learn по системным функциям. Содержит два…, Возвращает результаты для функции (или пустой список)., Сохраняет результаты поиска для функции (синхронно, не потокобезопасно). Args:…, Количество закешированных функций., Потокобезопасный менеджер кэша документации MS Learn. Чтение — напрямую из…, Задача на запись в очередь DocCacheManager. (+12 more)

### Community 38 - "bo"
Cohesion: 0.06
Nodes (14): bo, determineDataLimits(), getValueForPixel(), H(), is(), j(), ko, mo() (+6 more)

### Community 39 - "SfaReportGenerator"
Cohesion: 0.10
Nodes (24): is_system_module(), normalize_platform(), Определение системных библиотек по собственным словарям проекта. Единственная…, Возвращает набор нормализованных ключей системных модулей платформы., Приводит обозначение платформы к каноническому ключу. Неизвестное значение…, Проверяет, относится ли модуль к системным библиотекам платформы. Args:…, system_module_keys(), Индекс системных функций: предварительный проход по JSON-файлам. Строит SQLite-… (+16 more)

### Community 40 - ".diff"
Cohesion: 0.09
Nodes (19): Call the given event @func_name(@args) returning @default_ret if it doesn't…, Run a total of @total_cpus threads running SQL heuristics for category…, Check in all the matches for duplicates and bad matches and remove them., Find matches using all heuristics assigned to the 'partial' category., Brute force the unmatched functions. This is unreliable at best., Run heuristics labeled as experimental., Launch unreliable heuristics. Subject to be removed in the near future., Get a CChoser.Item object from the given list @item. (+11 more)

### Community 41 - "platform_classifier.py"
Cohesion: 0.09
Nodes (16): Словари для Android-модулей. Правила поддержки словарей: 1. Каждый модуль…, Логика классификации и группировка модулей по категориям., Классификатор импортированных модулей с детальными описаниями., Словари для Linux-модулей. Правила поддержки словарей: 1. Каждый модуль…, Словари для macOS / iOS модулей. Правила поддержки словарей: 1. Каждый модуль…, BasePlatformClassifier, classify_module(), CompositeClassifier (+8 more)

### Community 42 - "bn"
Cohesion: 0.08
Nodes (11): bn, ce(), de, dt(), ei(), he(), je(), pn() (+3 more)

### Community 43 - "RegexLexerMeta"
Cohesion: 0.06
Nodes (24): LexerMeta, ProfilingRegexLexerMeta, type, This metaclass automagically converts ``analyse_text`` methods into static…, Metaclass for RegexLexer, creates the self._tokens attribute from self.tokens…, Preprocess the regular expression component of a token definition., Preprocess the token component of a token definition., Preprocess the state transition action of a token definition. (+16 more)

### Community 44 - "ManPagesDatabase"
Cohesion: 0.07
Nodes (18): ManPagesDatabase, Connection, Path, Импортирует архив man-pages (``.tar.xz``) в новую БД. Args: db_path: путь к…, Сохраняет страницу и регистрирует все её имена функций., Количество имён функций в индексе., Есть ли документация для функции (с учётом алиасов)., Возвращает документацию функции в формате ``search_results``. Возвращаемый… (+10 more)

### Community 45 - "SfaFunctionIndex"
Cohesion: 0.07
Nodes (19): Path, Индекс системных функций, построенный из JSON-файлов экспорта. Двухфазное…, Сканирует JSON-файлы, собирает уникальные системные функции. Для каждого JSON…, Проверяет, есть ли функция в индексе. Быстрый lookup по PRIMARY KEY (O(log N)).…, Возвращает список импортов для указанного JSON-файла. Используется в reuse-…, Возвращает file_name для указанного JSON-файла., Возвращает размер исходного файла (или 0)., Возвращает все json_path из индекса. (+11 more)

### Community 46 - "AnalysisPage"
Cohesion: 0.10
Nodes (6): AnalysisPage, Path, QPushButton, QWidget, Удаляет временные файлы (.asm, .log, .id0, .id1, .nam, .til) в зависимости от…, Запускает экспорт в JSON в фоновом потоке (без блокировки UI).

### Community 47 - "SfaPage"
Cohesion: 0.10
Nodes (5): Path, QPushButton, QWidget, Возвращает ключ выбранной целевой платформы., SfaPage

### Community 48 - "CIDAChooser"
Cohesion: 0.07
Nodes (11): Choose, CBasicChooser, CDiaphoraChooser, CIDAChooser, command_handler_t, Wrapper class for IDA choosers, Sort items, add menu items and show the chooser., Return the functions rows for the diff database (+3 more)

### Community 49 - "RoffParser"
Cohesion: 0.09
Nodes (22): Офлайн-база документации man-pages для платформы Linux. Модуль скачивает…, find_alias_target(), ManPage, parse_man_page(), Разбор roff-исходников man-pages в Markdown. Модуль читает страницы руководства…, Разбирает roff-исходник и возвращает разобранную страницу., Нормализует заголовок секции (``RETURN VALUE`` и т.п.)., Форматирует шрифтовые макросы (``.B``, ``.BI``, ``.BR`` и др.). (+14 more)

### Community 50 - ".getContext"
Cohesion: 0.11
Nodes (7): Bi(), Ci(), Do(), eo(), Fi(), ls, Oe()

### Community 51 - "bt"
Cohesion: 0.10
Nodes (5): bt, Cs, nn(), os(), sn

### Community 52 - "XmlLexer"
Cohesion: 0.07
Nodes (19): Generic lexer for XML (eXtensible Markup Language)., A lexer for XSLT. .. versionadded:: 0.10, XmlLexer, XsltLexer, CheetahXmlLexer, JspLexer, MakoXmlLexer, Lexer for Java Server Pages. .. versionadded:: 0.7 (+11 more)

### Community 53 - ".db_cursor"
Cohesion: 0.07
Nodes (15): Delete the function at address @ea from the database, Find 100% equal matches in both databases, Find the functions that weren't matched after running all the selected…, Get the full table row for the given function with name @name in the database…, Get the list of unmatched functions in both databases., Search potentially renamed functions in a usual patch diffing session., After using a dirty heuristic doing patch diffing try to find the remaining…, Check if the processor of both databases is the same. (+7 more)

### Community 54 - "MainWindow"
Cohesion: 0.11
Nodes (17): MainWindow, Главное окно приложения., QPushButton, QWidget, Боковая панель с кнопками навигации., Sidebar, apply_theme(), Менеджер тем оформления: светлая и тёмная. (+9 more)

### Community 56 - "IDAAnalyzer"
Cohesion: 0.13
Nodes (12): Event, IDAAnalyzer, task(), task(), Path, Класс для пакетного анализа файлов в IDA Pro., Запускает ``task(file)`` для каждого файла в ThreadPoolExecutor. Сортирует…, ExportWorker (+4 more)

### Community 57 - ".log"
Cohesion: 0.09
Nodes (13): Get the percent of difference between the main and diff databases, Compare the call graphs of both databases and print out how different they are, Did we match already all the functions?, Determine if more rows should be read at the given stage, Wrapper for various functions that find matches based on SQL queries. Always…, Find matches using the query @sql and the usual rules., Find matches using the query @sql with a ratio >= @val., Find matches using the query @sql with a ratio >= @val and assign those with a… (+5 more)

### Community 58 - "updateElements"
Cohesion: 0.12
Nodes (15): _calculateBarIndexPixels(), _calculateBarValuePixels(), getBasePixel(), getLabelAndValue(), getLabelForValue(), getPixelForTick(), getPixelForValue(), _getRuler() (+7 more)

### Community 59 - "zt"
Cohesion: 0.11
Nodes (11): color(), Ft(), It(), kt(), mt(), qt(), _t(), te() (+3 more)

### Community 60 - "basicutils_7x.py"
Cohesion: 0.09
Nodes (15): CompileTextFromRange(), ForEveryFuncInSeg(), FuncXrefsFrom(), GetIdbFile(), GetInputFile(), GetRootName(), GetStrLitContents(), isCamelCase() (+7 more)

### Community 61 - "SettingsPage"
Cohesion: 0.11
Nodes (11): Path, QWidget, Проверяет наличие 7z и npx через запуск в терминале., Пытается запустить 7z и получить версию через терминал., Пытается запустить npx и получить версию через терминал., Выбранная папка для manpages.db., Путь к БД man-pages в выбранной пользователем папке., Выбор папки, куда будет загружена БД man-pages. (+3 more)

### Community 62 - "highlight"
Cohesion: 0.10
Nodes (13): CHtmlDiff, CHtmlViewer, Internal use, generate a HTML table with the assembly differences., I never understood why you'd want to have 13 spaces between instruction and args, format(), highlight(), Format a tokenlist ``tokens`` with the formatter ``formatter``. If ``outfile``…, Lex ``code`` with ``lexer`` and format it with the formatter ``formatter``. If… (+5 more)

### Community 63 - "Terminal256Formatter"
Cohesion: 0.11
Nodes (8): Format ``tokensource``, an iterable of ``(tokentype, tokenstring)`` tuples and…, EscapeSequence, pygments.formatters.terminal256 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ Formatter for…, # TODO:, r""" Format tokens with ANSI color sequences, for output in a true-color…, Format tokens with ANSI color sequences, for output in a 256-color terminal or…, Terminal256Formatter, TerminalTrueColorFormatter

### Community 64 - "CDiaphoraTester"
Cohesion: 0.15
Nodes (12): CDiaphoraBaseChecker, CDiaphoraDiffChecker, CDiaphoraExportChecker, CDiaphoraTester, debug(), launch_tests(), log(), main() (+4 more)

### Community 65 - ".add_match"
Cohesion: 0.10
Nodes (16): debug_refresh(), get_query_fields(), Get the list of fields used in any and all SQL heuristics queries., is_debug_enabled(), Get the graph representation of the function at address @ea1, Add a single match to the internal lists before really adding them to the…, An iterator that uses fetchmany to keep memory usage down., Check if we have a best match for the given two functions (not for the pair). (+8 more)

### Community 66 - "diaphora_heuristics.py"
Cohesion: 0.11
Nodes (18): check_categories(), check_dupes(), check_field_names(), check_heuristic_in_sql(), check_heuristics_ratio(), check_mandatory_fields(), Internal test, get all the internally set categories., Internal test, check for duplicated heuristics. (+10 more)

### Community 67 - "RubyLexer"
Cohesion: 0.10
Nodes (12): LexerContext, A helper object that holds lexer position data., Split ``text`` into (tokentype, text) pairs. If ``context`` is given, use this…, Lexer for Snowball source code. .. versionadded:: 2.2, SnowballLexer, For Ruby source code., RubyLexer, intp_regex_callback() (+4 more)

### Community 68 - "YamlLexer"
Cohesion: 0.10
Nodes (11): Set an explicit indentation level for a block scalar., Process an empty line in a block scalar., Process indentation spaces in a block scalar., Process indentation spaces in a plain scalar., Lexer for YAML, a human-friendly data serialization language. .. versionadded::…, Do not produce empty tokens., Reset the indentation levels., Save a possible indentation level. (+3 more)

### Community 70 - "robotframework.py"
Cohesion: 0.15
Nodes (12): Comment, ForLoop, GherkinTokenizer, ImportSetting, KeywordCall, KeywordSetting, pygments.lexers.robotframework ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ Lexer for Robot…, Setting (+4 more)

### Community 71 - "Style"
Cohesion: 0.11
Nodes (17): Style, AbapStyle, pygments.styles.abap ~~~~~~~~~~~~~~~~~~~~ ABAP workbench like style.…, AlgolStyle, Algol_NuStyle, pygments.styles.algol_nu ~~~~~~~~~~~~~~~~~~~~~~~~ Algol publication style…, pygments.styles.algol ~~~~~~~~~~~~~~~~~~~~~ Algol publication style. This style…, BlackWhiteStyle (+9 more)

### Community 72 - "normalize_module_name"
Cohesion: 0.11
Nodes (19): _build_category_key_map(), get_module_category(), Строит и кэширует отображение «ключ модуля → категория». Приоритет задаётся…, Возвращает короткое название категории модуля (или пустую строку). Быстрая…, module_name_aliases(), normalize_module_name(), Единая нормализация имён модулей (библиотек) для всего проекта. Одна и та же…, Приводит имя модуля к каноническому ключу. Убирает путь, префиксы… (+11 more)

### Community 73 - "DiffPage"
Cohesion: 0.12
Nodes (6): DiffPage, QWidget, Страница для запуска сравнения двух директорий с BinDiff и генерации отчёта., Обновляет колонку BinDiff (4) или Diaphora (5) для строки с rel_key., Сохраняем для обратной совместимости — используется _on_global_progress., QLineEdit

### Community 74 - "PhpLexer"
Cohesion: 0.10
Nodes (12): PhpLexer, PsyshConsoleLexer, For PHP source code. For PHP embedded in HTML, use the `HtmlPhpLexer`.…, For PsySH console output, such as: .. sourcecode:: psysh >>> $greeting =…, CssPhpLexer, HtmlPhpLexer, JavascriptPhpLexer, Subclass of `PhpLexer` that highlights unhandled data with the `HtmlLexer`.… (+4 more)

### Community 75 - "Page"
Cohesion: 0.13
Nodes (22): ProcessStatusText, ProgressLabel, AutoBindiffButton, AutoIdaButton, BindiffPathTextBox, BrowseBindiffButton, BrowseIdaButton, CheckUtilsButton (+14 more)

### Community 76 - "CVulnerabilityPatches"
Cohesion: 0.13
Nodes (9): log(), CVulnerabilityPatches, CVulnSearchResults, Try to guess if it looks like a newly added size check, Try to search for signedness fixed issues, Script to find patches that could be fixing vulnerabilities by simply doing…, Results from a heuristic to find vulnerabilities, Class used to find potentially fixed vulnerabilities by searching for patterns. (+1 more)

### Community 77 - "latex.py"
Cohesion: 0.12
Nodes (12): escape_tex(), _get_ttype_name(), LatexEmbeddedLexer, LatexFormatter, pygments.formatters.latex ~~~~~~~~~~~~~~~~~~~~~~~~~ Formatter for LaTeX…, r""" Format tokens as LaTeX code. This needs the `fancyvrb` and `color`…, Return the command sequences needed to define the commands used to format text…, # TODO: add support for background colors (+4 more)

### Community 78 - "Scanner"
Cohesion: 0.11
Nodes (13): DelphiLexer, For Delphi (Borland Object Pascal), Turbo Pascal and Free Pascal source code.…, EndOfText, pygments.scanner ~~~~~~~~~~~~~~~~ This library implements a regex based…, Raise if end of text is reached and the user tried to call a match function., Simple scanner All method patterns are regular expression strings (not compiled…, :param text: The text which should be scanned :param flags: default regular…, `True` if the scanner reached the end of text. (+5 more)

### Community 79 - "CKoretKaramitasHash"
Cohesion: 0.14
Nodes (11): CKoretKaramitasHash, main(), Yet another Control Flow Graph hash using small-primes-product. An…, # NOTE: In the current implementation (Nov-2018) all edges are considered as if, Return a set of prime numbers corresponding to the characteristics of the node., Tarjan's Algorithm (named for its discoverer, Robert Tarjan) is a graph theory…, Tarjan's algorithm and topological sorting implementation in Python by Paul…, First identify strongly connected components, then perform a topological sort… (+3 more)

### Community 80 - "jkutils/factor.py"
Cohesion: 0.14
Nodes (20): difference(), _difference(), difference_matrix(), difference_ratio(), factorization(), gcd(), isprime(), lcm() (+12 more)

### Community 81 - "CSourceFilesChooser"
Cohesion: 0.11
Nodes (3): CIDAMagicStringsChooser, command_handler_t, CSourceFilesChooser

### Community 82 - "basic.py"
Cohesion: 0.10
Nodes (16): BBCBasicLexer, BlitzBasicLexer, BlitzMaxLexer, CbmBasicV2Lexer, MonkeyLexer, QBasicLexer, pygments.lexers.basic ~~~~~~~~~~~~~~~~~~~~~ Lexers for BASIC like languages…, For BlitzBasic source code. .. versionadded:: 2.0 (+8 more)

### Community 83 - "TreemapWidget"
Cohesion: 0.16
Nodes (11): Enum, AnalysisStatus, DiffStatus, Константы интерфейса., Any, QWidget, Виджет Treemap для отображения статуса файлов (горизонтальная полоса)., Виджет, показывающий файлы в виде горизонтальной полосы равномерных блоков. (+3 more)

### Community 84 - "utils.py"
Cohesion: 0.14
Nodes (15): compute_executables_size(), _is_executable_image(), normalize_display_name(), Path, Вспомогательные функции для генерации отчётов., Каноническая нормализация имени модуля для отображения в отчётах. Универсальная…, Проверяет, что файл — исполняемый образ (PE/ELF/Mach-O), а не объектный.…, Суммирует размер только исполняемых модулей в директории. Учитываются файлы с… (+7 more)

### Community 85 - "CssDjangoLexer"
Cohesion: 0.11
Nodes (10): CssDjangoLexer, DjangoLexer, HtmlDjangoLexer, JavascriptDjangoLexer, Subclass of the `DjangoLexer` that highlights unlexed data with the…, Subclass of the `DjangoLexer` that highlights unlexed data with the `XmlLexer`., Subclass of the `DjangoLexer` that highlights unlexed data with the `CssLexer`., Generic `django <http://www.djangoproject.com/documentation/templates/>`_ and… (+2 more)

### Community 86 - "Page"
Cohesion: 0.14
Nodes (19): CleanupCheck, DeleteJsonCheck, PseudocodeCheck, TempCleanupCheck, Page, SfaBrowseDirButton, SfaCancelButton, SfaCleanupCheck (+11 more)

### Community 87 - "diaphora_local.py"
Cohesion: 0.19
Nodes (9): A replacement for difflib.HtmlDiff that tries to enforce a max width The main…, Diaphora, a binary diffing tool Copyright (c) 2015-2026 Joxean Koret This…, CHtmlDiff, CLocalDiffer, decompile_and_get(), do_decompile(), get_assembly(), get_disasm() (+1 more)

### Community 88 - "HtmlFormatter"
Cohesion: 0.16
Nodes (5): HtmlFormatter, r""" Format tokens as HTML 4 ``<span>`` tags within a ``<pre>`` tag, wrapped in…, Highlighted the lines specified in the `hl_lines` option by post-processing the…, Wrap the ``source``, which is a generator yielding individual lines, in custom…, The formatting process uses several nested generators; which of them are used…

### Community 89 - "markup.py"
Cohesion: 0.15
Nodes (13): MoinWikiLexer, MozPreprocCssLexer, MozPreprocHashLexer, MozPreprocJavascriptLexer, MozPreprocPercentLexer, MozPreprocXulLexer, pygments.lexers.markup ~~~~~~~~~~~~~~~~~~~~~~ Lexers for non-HTML markup…, Lexer for Mozilla Preprocessor files (with '#' as the marker). Other data is… (+5 more)

### Community 90 - ".show_callgraph_context"
Cohesion: 0.12
Nodes (9): Display two related graphs side-by-side and zoom-fit them., Get the call graph for the given function., Build the CCallGraphViewer objects., Show the callers and the callees for the given functions., Aditional right-click-menu commands handles., timeraction_t, uitimercallback_t, object (+1 more)

### Community 91 - ".register_menu"
Cohesion: 0.12
Nodes (6): CIdaMenuHandlerLoadResults, CIdaMenuHandlerSaveResults, CIdaMenuHandlerShowChoosers, Show all non empty choosers., Show the dialogue to save the diffing results., save_results()

### Community 92 - "FontManager"
Cohesion: 0.18
Nodes (8): FontManager, FontNotFound, Exception, Get the character size., Get the text size (width, height)., Get the font based on bold and italic flags., When there are no usable fonts specified, Manages a set of fonts: normal, italic, bold, etc...

### Community 93 - "MIMELexer"
Cohesion: 0.13
Nodes (7): EmailHeaderLexer, EmailLexer, pygments.lexers.email ~~~~~~~~~~~~~~~~~~~~~ Lexer for the raw E-mail.…, Lexer for raw E-mail. Additional options accepted: `highlight-X-header`…, Sub-lexer for raw E-mail. This lexer only process header part of e-mail. ..…, MIMELexer, Lexer for Multipurpose Internet Mail Extensions (MIME) data. This lexer is…

### Community 95 - "Page"
Cohesion: 0.14
Nodes (17): BrowseDirButton, CancelButton, ErrorLogTextBox, GenerateHtmlButton, InputDirTextBox, MaxIdaSlider, Page, ProcessProgress (+9 more)

### Community 96 - "formatter.py"
Cohesion: 0.15
Nodes (12): _lookup_style(), pygments.formatter ~~~~~~~~~~~~~~~~~~ Base formatter class. :copyright:…, escape_special_chars(), PangoMarkupFormatter, pygments.formatters.pangomarkup ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ Formatter for…, Escape & and < for Pango Markup., Format tokens as Pango Markup code. It can then be rendered to an SVG. ..…, find_plugin_styles() (+4 more)

### Community 97 - "CssGenshiLexer"
Cohesion: 0.12
Nodes (7): CssGenshiLexer, GenshiLexer, HtmlGenshiLexer, JavascriptGenshiLexer, A lexer that highlights `genshi <http://genshi.edgewall.org/>`_ and `kid…, A lexer that highlights javascript code in genshi text templates., A lexer that highlights CSS definitions in genshi text templates.

### Community 98 - "LassoLexer"
Cohesion: 0.17
Nodes (7): LassoLexer, For Lasso source code, covering both Lasso 9 syntax and LassoScript for Lasso…, LassoCssLexer, LassoHtmlLexer, LassoJavascriptLexer, LassoXmlLexer, Subclass of the `LassoLexer` which highlights unhandled data with the…

### Community 99 - "sql.py"
Cohesion: 0.12
Nodes (13): language_callback(), MySqlLexer, pygments.lexers.sql ~~~~~~~~~~~~~~~~~~~ Lexers for various SQL dialects and…, # TODO: better logging, # TODO: better handle multiline comments at the end with, # TODO: Backslash escapes?, Transact-SQL (T-SQL) is Microsoft's and Sybase's proprietary extension to SQL.…, The Oracle MySQL lexer. This lexer does not attempt to maintain strict… (+5 more)

### Community 100 - "IDAMagicStrings.py"
Cohesion: 0.24
Nodes (15): add_source_file_to(), collect_function_name_refs(), find_class_objects(), find_function_names(), find_source_files_in_debug_info(), find_source_files_in_strings(), get_lang(), get_source_strings() (+7 more)

### Community 101 - "CClassesGraph"
Cohesion: 0.16
Nodes (3): CClassesGraph, CClassXRefsChooser, get_string()

### Community 102 - "module_callbacks"
Cohesion: 0.17
Nodes (7): get_function_module(), get_lua_functions(), get_newest_version(), module_callbacks(), pygments.lexers._lua_builtins ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ This file contains…, regenerate(), run()

### Community 103 - "AnalysisWorker"
Cohesion: 0.13
Nodes (5): AnalysisWorker, Path, QThread, Фоновый поток для выполнения пакетного анализа и последующего JSON-экспорта., Установить флаг отмены и запросить остановку.

### Community 105 - "TestCaseTable"
Cohesion: 0.21
Nodes (3): KeywordTable, normalize(), TestCaseTable

### Community 106 - "MainWindow"
Cohesion: 0.15
Nodes (8): IDABatchToolWinUI, Application, App, MainWindow, LaunchActivatedEventArgs, NavigationView, NavigationViewSelectionChangedEventArgs, TitleBar

### Community 107 - "main.py"
Cohesion: 0.20
Nodes (12): default_filter(), find_executables(), is_executable(), is_macho(), Path, Поиск исполняемых файлов с фильтрацией по расширениям и сигнатурам., Проверяет, является ли файл Mach-O по сигнатуре., Проверяет, является ли файл исполняемым (PE, ELF или Mach-O) по сигнатуре. (+4 more)

### Community 109 - ".save_function"
Cohesion: 0.15
Nodes (7): Get a valid property to insert into the SQLite database. This is a hack for 64…, Save all the native assembly instructions in the basic block @bb_data to the…, Insert basic blocks information as well as the relationship between assembly…, Save all the microcode instructions in the basic block @bb_data to the database., Create a dictionary to be used with project specific hooks from a given tuple., Save a single function to the database., Save the function with the given properties @props to the database.

### Community 110 - ".process_basic_block"
Cohesion: 0.14
Nodes (7): diaphora_decode(), get_string_at(), Filter for certain constants/immediate values. Not all values should be taken…, Wrapper for IDA's decode_insn, Get the defined string at the given address., Process a single instruction and extract its features., Process one basic block and its instructions

### Community 111 - "diaphora_plugin.py"
Cohesion: 0.18
Nodes (7): CDiaphoraAction, CDiaphoraPlugin, local_diff(), PLUGIN_ENTRY(), Diaphora's IDA plugin Copyright (c) 2015-2026, Joxean Koret This program is…, Run the local-diff helper from extras/diaphora_local., main()

### Community 112 - "ValueError"
Cohesion: 0.24
Nodes (13): parse_item_create_functions(), parse_lex_functions(), parse_lex_keywords(), parse_lex_optimizer_hints(), pygments.lexers._mysql_builtins ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ Self-updating…, Parse keywords in lex.h., Parse optimizer hints in lex.h., Parse MySQL function names from lex.h. (+5 more)

### Community 113 - "special.py"
Cohesion: 0.15
Nodes (7): OutputLexer, pygments.lexers.special ~~~~~~~~~~~~~~~~~~~~~~~ Special lexers. :copyright:…, "Null" lexer, doesn't highlight anything., Simple lexer that highlights everything as ``Token.Generic.Output``. ..…, Recreate a token stream formatted with the `RawTokenFormatter`. Additional…, RawTokenLexer, TextLexer

### Community 114 - "TNTLexer"
Cohesion: 0.24
Nodes (5): Tokenize a line referral., Mark everything from ``start`` to the end of the line as Error., Returns a list of TNT tokens., Lexer for Typographic Number Theory, as described in the book Gödel, Escher,…, TNTLexer

### Community 115 - "Page"
Cohesion: 0.23
Nodes (13): BrowseLeftButton, BrowseOutputButton, BrowseRightButton, CancelDiffButton, DiffErrorTextBox, GenerateReportButton, LeftDirTextBox, OutputDirTextBox (+5 more)

### Community 116 - "NextFunction"
Cohesion: 0.21
Nodes (13): CanonicalizeRange(), CompileFuncNamesFromRangeAsText(), ForEveryFuncInDb(), NameCanonical(), NextFunction(), NFuncDown(), PrefixRange(), RenameFuncWithAddr() (+5 more)

### Community 117 - "CDebuggingHelper"
Cohesion: 0.15
Nodes (6): CDebuggingHelper, @diaphora_obj is the object with all the Diaphora APIs., @ea is the address of the function that is going to be read. Return True for…, Example Diaphora export hooks script for debugging problems. Joxean Koret,…, @ea is the address of the function where Diaphora crashed exporting it. Return…, @d is a dictionary with everything that Diaphora exports for the current…

### Community 118 - "CExampleDiaphoraHooks"
Cohesion: 0.15
Nodes (7): CExampleDiaphoraHooks, Enable/Disable a special @heuristic at the specified @iteration, Skeleton script to write project specific rules for Diaphora. Created by Joxean…, @category is the category for which heuristics are going to be executed by…, @name is the heuristic to be run and @sql is the SQL query that is going to be…, @category is the type of heuristics that are being launched, which can be…, @func1 and @func2 are dictionaries with data relative to the functions that are…

### Community 120 - ".__init__"
Cohesion: 0.15
Nodes (8): CSSUL4Lexer, HTMLUL4Lexer, JavascriptUL4Lexer, Lexer for UL4 embedded in HTML., Lexer for UL4 embedded in XML., Lexer for UL4 embedded in CSS., Lexer for UL4 embedded in Javascript., XMLUL4Lexer

### Community 121 - "Window"
Cohesion: 0.19
Nodes (12): AppTitleBar, NavAnalysis, NavDiff, NavFrame, NavSettings, NavSfa, NavView, Window (+4 more)

### Community 122 - "CCallGraphViewer"
Cohesion: 0.17
Nodes (4): GraphViewer, CCallGraphViewer, CDiffGraphViewer, Class used to show graphs.

### Community 123 - "cc_main.py"
Cohesion: 0.27
Nodes (8): analyze(), do_cutting(), func_list_annotate(), make_cut(), make_subgraph(), add_edge(), add_node(), create_snap_cg()

### Community 124 - "map_read.py"
Cohesion: 0.29
Nodes (9): bin_mod, final_score(), map_parse(), map_reconcile(), mod_collapse(), mod_print(), mod_underlap(), rec_list_print() (+1 more)

### Community 125 - "CChooser"
Cohesion: 0.17
Nodes (6): CChooser, Item, Our own chooser for displaying diffing results., A single chooser item., Return the highlighting colour for the current chooser., Fake method, it is only used when running from within IDA.

### Community 126 - "JsonLexer"
Cohesion: 0.17
Nodes (8): JsonBareObjectLexer, JsonLdLexer, JsonLexer, Indentation context for the YAML lexer., For JSON data structures. Javascript-style comments are supported (like ``/*…, For JSON data structures (with missing object curly braces). .. versionadded::…, For JSON-LD linked data. .. versionadded:: 2.0, YamlLexerContext

### Community 127 - ".__init__"
Cohesion: 0.20
Nodes (4): RowSplitter, RowTokenizer, UnknownTable, VariableTable

### Community 128 - "EvoqueHtmlLexer"
Cohesion: 0.17
Nodes (7): EvoqueHtmlLexer, EvoqueLexer, EvoqueXmlLexer, For files using the Evoque templating system. .. versionadded:: 1.1, Evoque templates use $evoque, which is unique., Subclass of the `EvoqueLexer` that highlights unlexed data with the…, Subclass of the `EvoqueLexer` that highlights unlexed data with the `XmlLexer`.…

### Community 129 - "style.py"
Cohesion: 0.17
Nodes (8): pygments.style ~~~~~~~~~~~~~~ Basic style object. :copyright: Copyright…, ParaisoLightStyle, pygments.styles.paraiso_light ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ Paraíso (Light) by…, pygments.styles.stata_dark ~~~~~~~~~~~~~~~~~~~~~~~~~~ Dark style inspired by…, StataDarkStyle, pygments.styles.stata_light ~~~~~~~~~~~~~~~~~~~~~~~~~~~ Light Style inspired by…, Light mode style inspired by Stata's do-file editor. This is not meant to be a…, StataLightStyle

### Community 130 - ".__init__"
Cohesion: 0.18
Nodes (8): AgdaLexer, IdrisLexer, LiterateAgdaLexer, LiterateIdrisLexer, A lexer for the dependently typed programming language Idris. Based on the…, For the Agda dependently typed functional programming language and proof…, For Literate Idris (Bird-style or LaTeX) source. Additional options accepted:…, For Literate Agda source. Additional options accepted: `litstyle` If given,…

### Community 132 - "ShellSessionBaseLexer"
Cohesion: 0.18
Nodes (10): BashSessionLexer, MSDOSSessionLexer, PowerShellSessionLexer, Base lexer for shell sessions. .. versionadded:: 2.1, Lexer for Bash shell sessions, i.e. command lines, including a prompt,…, Lexer for MS DOS shell sessions, i.e. command lines, including a prompt,…, Lexer for Tcsh sessions, i.e. command lines, including a prompt, interspersed…, Lexer for PowerShell sessions, i.e. command lines, including a prompt,… (+2 more)

### Community 133 - "PostgresBase"
Cohesion: 0.18
Nodes (8): PlPgsqlLexer, PostgresBase, PsqlRegexLexer, Base class for Postgres-related lexers. This is implemented as a mixin to avoid…, Handle the extra syntax in Pl/pgSQL language. .. versionadded:: 1.5, Extend the PostgresLexer adding support specific for psql commands. This is not…, Lexer for example sessions using sqlite3. .. versionadded:: 0.11, SqliteConsoleLexer

### Community 134 - "IDABatchToolWinUI.Pages"
Cohesion: 0.27
Nodes (6): IDABatchToolWinUI.Pages, AnalysisPage, DiffPage, SettingsPage, SfaPage, Page

### Community 135 - "PygmentsDoc"
Cohesion: 0.27
Nodes (4): Directive, PygmentsDoc, pygments.sphinxext ~~~~~~~~~~~~~~~~~~ Sphinx extension to generate automatic…, A directive to collect all lexers/formatters/filters and generate autoclass…

### Community 136 - "format_lines"
Cohesion: 0.29
Nodes (9): FancyURLopener, get_sm_functions(), get_version(), Opener, pygments.lexers._sourcemod_builtins ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ This…, regenerate(), run(), format_lines() (+1 more)

### Community 137 - ".find_one_match_diffing"
Cohesion: 0.22
Nodes (5): Compare the functions of one SQL match., Check if the given functions exist and return their respective rows., Call the "on_match" hook, if it exists., Diff the lines for the field @field_name and find matches of function names…, Find the 'bester' matches in the functions gap specified by the given ranges

### Community 138 - "CMyHooks"
Cohesion: 0.20
Nodes (4): CMyHooks, Example Diaphora export hooks script. In this example script the following fake…, @d is a dictionary with everything exported by Diaphora for the current…, @diaphora_obj is the CIDABinDiff object being used.

### Community 139 - "CBaseTreeViewer"
Cohesion: 0.22
Nodes (4): CBaseTreeViewer, CClassesTreeViewer, classes_handler(), handler()

### Community 140 - "CIDAMagicStringsAction"
Cohesion: 0.22
Nodes (4): CIDAMagicStringsAction, CIDAMagicStringsMod, IDAMagicStringsPlugin, PLUGIN_ENTRY()

### Community 141 - ".get_tokens"
Cohesion: 0.20
Nodes (7): apply_filters(), Use this method to apply an iterable of filters to a stream. If lexer is given…, PilNotAvailable, When Python imaging library is not available, streamer(), Return an iterable of (tokentype, value) pairs generated from `text`. If…, ImportError

### Community 142 - "RadioButton"
Cohesion: 0.20
Nodes (10): PlatformLinuxAndroid, PlatformMacIos, PlatformWindows, EngineBindiff, EngineBoth, EngineDiaphora, SfaPlatformLinuxAndroid, SfaPlatformMacIos (+2 more)

### Community 145 - "rtf.py"
Cohesion: 0.33
Nodes (5): pygments.formatters.rtf ~~~~~~~~~~~~~~~~~~~~~~~ A formatter that generates RTF…, Format tokens as RTF markup. This formatter automatically outputs full RTF…, RtfFormatter, Given a unicode character code with length greater than 16 bits, return the two…, surrogatepair()

### Community 148 - "lookahead"
Cohesion: 0.22
Nodes (4): lookahead, PostgresConsoleLexer, Wrap an iterator and allow pushing back an item., Lexer for psql sessions. .. versionadded:: 1.5

### Community 151 - "lfa.py"
Cohesion: 0.46
Nodes (7): analyze(), edge_detect(), func_call_weight(), func_callee_weight(), func_callers_weight(), get_last_three(), get_lfa_start()

### Community 152 - "CExcludeHeuristicHooks"
Cohesion: 0.25
Nodes (4): CExcludeHeuristicHooks, Build a new list with the SQL based heuristics that we want Diaphora to use., Script to exclude specific heuristics Created by Joxean Koret Public domain, We need to use this event too because some special heuristics not based on SQL…

### Community 155 - "CSharpAspxLexer"
Cohesion: 0.25
Nodes (4): CSharpAspxLexer, Lexer for highlighting C# within ASP.NET pages., Lexer for highlighting Visual Basic.net within ASP.NET pages., VbNetAspxLexer

### Community 156 - "Modula2Lexer"
Cohesion: 0.36
Nodes (3): Modula2Lexer, It's Pascal-like, but does not use FUNCTION -- uses PROCEDURE instead., For Modula-2 source code. The Modula-2 lexer supports several dialects. By…

### Community 157 - "slash.py"
Cohesion: 0.25
Nodes (4): pygments.lexers.slash ~~~~~~~~~~~~~~~~~~~~~ Lexer for the `Slash…, Lexer for the Slash programming language. .. versionadded:: 2.4, SlashLanguageLexer, SlashLexer

### Community 158 - "export_cfg.py"
Cohesion: 0.39
Nodes (7): export_all_cfgs(), export_cfg_svg(), _get_argv_param(), main(), IDAPython-скрипт для экспорта графа потока управления (CFG) функции как SVG.…, Экспортирует CFG функции как SVG., Экспортирует CFG всех функций.

### Community 159 - "cc_base.py"
Cohesion: 0.43
Nodes (4): escape_for_graphviz(), gen_mod_graph(), locate_module(), print_results()

### Community 161 - "._get_css_class"
Cohesion: 0.29
Nodes (4): _get_ttype_class(), Return the css class of this token type prefixed with the classprefix option., Return the CSS classes of this token type prefixed with the classprefix option., webify()

### Community 162 - "._format_lines"
Cohesion: 0.29
Nodes (3): Return the inline CSS styles for this token type., HTML-escape a value and split it by newlines., Just format the tokens, without any wrapping tags. Yield individual lines.

### Community 163 - "oberon.py"
Cohesion: 0.29
Nodes (5): ComponentPascalLexer, pygments.lexers.oberon ~~~~~~~~~~~~~~~~~~~~~~ Lexers for Oberon family…, The only other lexer using .cp is the C++ one, so we check if for a few common…, For Component Pascal source code. .. versionadded:: 2.1, # TODO: nested comments (* (* ... *) ... (* ... *) *) not supported!

### Community 164 - "_postgres_builtins.py"
Cohesion: 0.48
Nodes (6): parse_datatypes(), parse_keywords(), parse_pseudos(), pygments.lexers._postgres_builtins ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ Self-…, update_consts(), update_myself()

### Community 165 - "autumn.py"
Cohesion: 0.29
Nodes (5): AutumnStyle, pygments.styles.autumn ~~~~~~~~~~~~~~~~~~~~~~ A colorful style, inspired by the…, ManniStyle, pygments.styles.manni ~~~~~~~~~~~~~~~~~~~~~ A colorful style, inspired by the…, A colorful style, inspired by the terminal highlighting style.

### Community 166 - "default.py"
Cohesion: 0.29
Nodes (5): DefaultStyle, pygments.styles.default ~~~~~~~~~~~~~~~~~~~~~~~ The default highlighting style.…, EmacsStyle, pygments.styles.emacs ~~~~~~~~~~~~~~~~~~~~~ A highlighting style for Pygments,…, The default style (inspired by Emacs 22).

### Community 167 - "fruity.py"
Cohesion: 0.29
Nodes (5): FruityStyle, pygments.styles.fruity ~~~~~~~~~~~~~~~~~~~~~~ pygments version of my "fruity"…, Pygments version of the "native" vim theme., NativeStyle, pygments.styles.native ~~~~~~~~~~~~~~~~~~~~~~ pygments version of my "native"…

### Community 168 - "solarized.py"
Cohesion: 0.33
Nodes (5): pygments.styles.solarized ~~~~~~~~~~~~~~~~~~~~~~~~~ Solarized by Camil Staps A…, The solarized style, dark., The solarized style, light., SolarizedDarkStyle, SolarizedLightStyle

### Community 169 - "CFPSChecker"
Cohesion: 0.43
Nodes (3): CFPSChecker, main(), sqlite3_connect()

### Community 170 - "IDABatchToolWinUI.csproj"
Cohesion: 0.33
Nodes (5): net9.0-windows10.0.26100.0, Microsoft.Windows.SDK.BuildTools (10.0.28000.2705), Microsoft.Windows.SDK.BuildTools.WinApp (0.6.1), Microsoft.WindowsAppSDK (2.4.0), Microsoft.NET.Sdk

### Community 172 - "modnaming.py"
Cohesion: 0.60
Nodes (5): bracket_strings(), common_strings(), guess_module_names(), source_file_strings(), string_range_tokenize()

### Community 173 - "CryptolLexer"
Cohesion: 0.33
Nodes (4): CryptolLexer, LiterateCryptolLexer, FIXME: A Cryptol2 lexer based on the lexemes defined in the Haskell 98 Report.…, For Literate Cryptol (Bird-style or LaTeX) source. Additional options accepted:…

### Community 174 - "MarkdownLexer"
Cohesion: 0.33
Nodes (3): MarkdownLexer, For Markdown markup. .. versionadded:: 2.2, match args: 1:backticks, 2:lang_name, 3:newline, 4:code, 5:backticks

### Community 175 - "_php_builtins.py"
Cohesion: 0.53
Nodes (5): get_php_functions(), get_php_references(), pygments.lexers._php_builtins ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ This file loads the…, regenerate(), run()

### Community 176 - "qlik.py"
Cohesion: 0.33
Nodes (4): pygments.lexers._qlik_builtins ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ Qlik builtins.…, QlikLexer, pygments.lexers.qlik ~~~~~~~~~~~~~~~~~~~~ Lexer for the qlik scripting language…, Lexer for qlik code, including .qvs files .. versionadded:: 2.12

### Community 177 - "RobotFrameworkLexer"
Cohesion: 0.33
Nodes (3): For Robot Framework test data. Supports both space and pipe separated plain…, RobotFrameworkLexer, VariableTokenizer

### Community 178 - "VimLexer"
Cohesion: 0.40
Nodes (3): Lexer for VimL script files. .. versionadded:: 0.8, r""" It's kind of difficult to decide if something might be a keyword in VimL…, VimLexer

### Community 180 - "gruvbox.py"
Cohesion: 0.33
Nodes (5): GruvboxDarkStyle, GruvboxLightStyle, pygments.styles.gruvbox ~~~~~~~~~~~~~~~~~~~~~~~ pygments version of the…, Pygments version of the "gruvbox" dark vim theme., Pygments version of the "gruvbox" Light vim theme.

### Community 181 - "nord.py"
Cohesion: 0.33
Nodes (5): NordDarkerStyle, NordStyle, pygments.styles.nord ~~~~~~~~~~~~~~~~~~~~ pygments version of the "nord" theme…, Pygments version of the "nord" theme by Arctic Ice Studio, Pygments version of a darker "nord" theme by Arctic Ice Studio

### Community 185 - "EzhilLexer"
Cohesion: 0.40
Nodes (3): EzhilLexer, Lexer for Ezhil, a Tamil script-based programming language. .. versionadded::…, This language uses Tamil-script. We'll assume that if there's a decent amount…

### Community 187 - "RebolLexer"
Cohesion: 0.40
Nodes (3): A `REBOL <http://www.rebol.com/>`_ lexer. .. versionadded:: 1.1, Check if code contains REBOL header and so it probably not R code, RebolLexer

### Community 188 - "_scilab_builtins.py"
Cohesion: 0.40
Nodes (3): pygments.lexers._scilab_builtins ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ Builtin list…, duplicates_removed(), Returns a list with duplicates removed from the iterable `it`. Order is…

### Community 189 - "UrbiscriptLexer"
Cohesion: 0.40
Nodes (3): This is fairly similar to C and others, but freezeif and waituntil are unique…, For UrbiScript source code. .. versionadded:: 1.5, UrbiscriptLexer

### Community 191 - "get_filetype_from_buffer"
Cohesion: 0.50
Nodes (4): get_filetype_from_buffer(), get_filetype_from_line(), pygments.modeline ~~~~~~~~~~~~~~~~~ A simple modeline parser (based on…, Scan the buffer for modelines and return filetype if one is found.

### Community 192 - "add_sample.py"
Cohesion: 0.60
Nodes (4): add_sample(), is_excluded(), main(), Script used to create a skeleton .cfg file needed by the testing suite Diaphora…

### Community 193 - "clean_directory"
Cohesion: 0.50
Nodes (3): clean_directory(), Функции для очистки временных файлов после анализа., Рекурсивно удаляет файлы, соответствующие заданным шаблонам.

### Community 198 - "UcodeLexer"
Cohesion: 0.50
Nodes (3): Lexer for Icon ucode files. .. versionadded:: 2.4, endsuspend and endrepeat are unique to this language, and \\self, /self doesn't…, UcodeLexer

### Community 199 - "arduino.py"
Cohesion: 0.50
Nodes (3): ArduinoStyle, pygments.styles.arduino ~~~~~~~~~~~~~~~~~~~~~~~ Arduino® Syntax highlighting…, The Arduino® language style. This style is designed to highlight the Arduino…

### Community 200 - "borland.py"
Cohesion: 0.50
Nodes (3): BorlandStyle, pygments.styles.borland ~~~~~~~~~~~~~~~~~~~~~~~ Style similar to the style used…, Style similar to the style used in the borland IDEs.

### Community 201 - "colorful.py"
Cohesion: 0.50
Nodes (3): ColorfulStyle, pygments.styles.colorful ~~~~~~~~~~~~~~~~~~~~~~~~ A colorful style, inspired by…, A colorful style, inspired by CodeRay.

### Community 202 - "friendly.py"
Cohesion: 0.50
Nodes (3): FriendlyStyle, pygments.styles.friendly ~~~~~~~~~~~~~~~~~~~~~~~~ A modern style based on the…, A modern style based on the VIM pyte theme.

### Community 203 - "friendly_grayscale.py"
Cohesion: 0.50
Nodes (3): FriendlyGrayscaleStyle, pygments.styles.friendly_grayscale ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ A style…, A modern grayscale style based on the friendly style. .. versionadded:: 2.11

### Community 204 - "gh_dark.py"
Cohesion: 0.50
Nodes (3): GhDarkStyle, pygments.styles.gh_dark ~~~~~~~~~~~~~~~~~~~~~~~ Github's Dark-Colorscheme based…, Github's Dark-Colorscheme based theme for Pygments

### Community 205 - "styles/igor.py"
Cohesion: 0.50
Nodes (3): IgorStyle, pygments.styles.igor ~~~~~~~~~~~~~~~~~~~~ Igor Pro default style. :copyright:…, Pygments version of the official colors for Igor Pro procedures.

### Community 206 - "styles/lilypond.py"
Cohesion: 0.50
Nodes (3): LilyPondStyle, pygments.styles.lilypond ~~~~~~~~~~~~~~~~~~~~~~~~ LilyPond-specific style.…, Style for the LilyPond language. .. versionadded:: 2.11

### Community 207 - "lovelace.py"
Cohesion: 0.50
Nodes (3): LovelaceStyle, pygments.styles.lovelace ~~~~~~~~~~~~~~~~~~~~~~~~ Lovelace by Miikka Salminen…, The style used in Lovelace interactive learning environment. Tries to avoid the…

### Community 208 - "material.py"
Cohesion: 0.50
Nodes (3): MaterialStyle, pygments.styles.material ~~~~~~~~~~~~~~~~~~~~~~~~ Mimic the Material theme…, This style mimics the Material Theme color scheme.

### Community 209 - "monokai.py"
Cohesion: 0.50
Nodes (3): MonokaiStyle, pygments.styles.monokai ~~~~~~~~~~~~~~~~~~~~~~~ Mimic the Monokai color scheme.…, This style mimics the Monokai color scheme.

### Community 210 - "murphy.py"
Cohesion: 0.50
Nodes (3): MurphyStyle, pygments.styles.murphy ~~~~~~~~~~~~~~~~~~~~~~ Murphy's style from CodeRay.…, Murphy's style from CodeRay.

### Community 211 - "onedark.py"
Cohesion: 0.50
Nodes (3): OneDarkStyle, pygments.styles.onedark ~~~~~~~~~~~~~~~~~~~~~~~ One Dark Theme for Pygments by…, Theme inspired by One Dark Pro for Atom .. versionadded:: 2.11

### Community 212 - "pastie.py"
Cohesion: 0.50
Nodes (3): PastieStyle, pygments.styles.pastie ~~~~~~~~~~~~~~~~~~~~~~ Style similar to the `pastie`_…, Style similar to the pastie default style.

### Community 213 - "perldoc.py"
Cohesion: 0.50
Nodes (3): PerldocStyle, pygments.styles.perldoc ~~~~~~~~~~~~~~~~~~~~~~~ Style similar to the style used…, Style similar to the style used in the perldoc code blocks.

### Community 214 - "rainbow_dash.py"
Cohesion: 0.50
Nodes (3): RainbowDashStyle, pygments.styles.rainbow_dash ~~~~~~~~~~~~~~~~~~~~~~~~~~~~ A bright and colorful…, A bright and colorful syntax highlighting theme.

### Community 215 - "rrt.py"
Cohesion: 0.50
Nodes (3): pygments.styles.rrt ~~~~~~~~~~~~~~~~~~~ pygments "rrt" theme, based on Zap and…, Minimalistic "rrt" theme, based on Zap and Emacs defaults., RrtStyle

### Community 216 - "styles/sas.py"
Cohesion: 0.50
Nodes (3): pygments.styles.sas ~~~~~~~~~~~~~~~~~~~ Style inspired by SAS' enhanced program…, Style inspired by SAS' enhanced program editor. Note This is not meant to be a…, SasStyle

### Community 217 - "staroffice.py"
Cohesion: 0.50
Nodes (3): pygments.styles.staroffice ~~~~~~~~~~~~~~~~~~~~~~~~~~ Style similar to…, Style similar to StarOffice style, also in OpenOffice and LibreOffice., StarofficeStyle

### Community 218 - "tango.py"
Cohesion: 0.50
Nodes (3): pygments.styles.tango ~~~~~~~~~~~~~~~~~~~~~ The Crunchy default Style inspired…, The Crunchy default Style inspired from the color palette from the Tango Icon…, TangoStyle

### Community 219 - "trac.py"
Cohesion: 0.50
Nodes (3): pygments.styles.trac ~~~~~~~~~~~~~~~~~~~~ Port of the default trac highlighter…, Port of the default trac highlighter design., TracStyle

### Community 220 - "vim.py"
Cohesion: 0.50
Nodes (3): pygments.styles.vim ~~~~~~~~~~~~~~~~~~~ A highlighting style for Pygments,…, Styles somewhat like vim 7.0, VimStyle

### Community 221 - "xcode.py"
Cohesion: 0.50
Nodes (3): pygments.styles.xcode ~~~~~~~~~~~~~~~~~~~~~ Style similar to the `Xcode`…, Style similar to the Xcode default colouring theme., XcodeStyle

### Community 222 - "zenburn.py"
Cohesion: 0.50
Nodes (3): pygments.styles.zenburn ~~~~~~~~~~~~~~~~~~~~~~~ Low contrast color scheme…, Low contrast Zenburn style., ZenburnStyle

## Knowledge Gaps
- **11 isolated node(s):** `pd`, `Frame`, `NavigationView`, `TitleBar`, `Microsoft.Windows.SDK.BuildTools (10.0.28000.2705)` (+6 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 2128 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **33 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `RegexLexer` connect `RegexLexer` to `token.py`, `words`, `.__init__`, `include`, `compiled.py`, `PostgresBase`, `lexers/other.py`, `templates.py`, `text.py`, `util.py`, `default`, `EvoqueHtmlLexer`, `DelegatingLexer`, `PythonLexer`, `ShenLexer`, `javascript.py`, `lexers/html.py`, `Modula2Lexer`, `Formatter`, `scripting.py`, `haskell.py`, `oberon.py`, `CryptolLexer`, `MarkdownLexer`, `qlik.py`, `VimLexer`, `HttpLexer`, `XmlLexer`, `EzhilLexer`, `RstLexer`, `RebolLexer`, `highlight`, `CommonLispLexer`, `SourcePawnLexer`, `UcodeLexer`, `PhpLexer`, `basic.py`, `CssDjangoLexer`, `markup.py`, `MIMELexer`, `LassoLexer`, `sql.py`, `.get_tokens_unprocessed`?**
  _High betweenness centrality (0.168) - this node is a cross-community bridge._
- **Why does `get_platform_classifier()` connect `platform_classifier.py` to `ValueError`, `generator.py`?**
  _High betweenness centrality (0.152) - this node is a cross-community bridge._
- **Why does `ClassNotFound` connect `util.py` to `formatter.py`, `sql.py`, `MarkdownLexer`, `filters/__init__.py`, `ValueError`, `HttpLexer`, `markup.py`, `RstLexer`, `MIMELexer`?**
  _High betweenness centrality (0.120) - this node is a cross-community bridge._
- **Are the 202 inferred relationships involving `include` (e.g. with `AdaLexer` and `AmbientTalkLexer`) actually correct?**
  _`include` has 202 INFERRED edges - model-reasoned connections that need verification._
- **Are the 180 inferred relationships involving `words` (e.g. with `ActionScriptLexer` and `AdaLexer`) actually correct?**
  _`words` has 180 INFERRED edges - model-reasoned connections that need verification._
- **Are the 112 inferred relationships involving `default` (e.g. with `ActionScript3Lexer` and `AdaLexer`) actually correct?**
  _`default` has 112 INFERRED edges - model-reasoned connections that need verification._
- **What connects `pd`, `Frame`, `NavigationView` to the rest of the system?**
  _11 weakly-connected nodes found - possible documentation gaps or missing edges._