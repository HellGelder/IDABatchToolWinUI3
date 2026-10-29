"""Распаковка man-страниц из архива корпуса в плоский каталог man*/.

Поддержка: tar.xz/tar.gz (kernel.org man-pages), tar.zst (пакет
man-pages-ru Arch Linux, requires Python 3.14+ с compression.zstd).
Симлинки пропускаются: manpages_build.py читает только файлы вида
NAME.<секция>[.gz] в подкаталогах man*.

    python extract_corpus.py <archive> <dest> [<prefix>] [--all]

Без префикса путь определяется автоматически: всё, что начинается с
первого сегмента man<N> (…/usr/share/man/ru/man1/open.1.gz → man1/…).
С --all извлекаются все файлы под <prefix> (например, каталог LICENSES).
"""
import sys
import tarfile
from pathlib import Path


def _open_tar(path: Path):
    if path.name.endswith(".tar.zst") or path.name.endswith(".tzst"):
        from compression import zstd  # Python 3.14+ (PEP 784)
        # Файл НЕ закрываем после возврата: tarfile читает поток лениво,
        # with open(...) закрыл бы дескриптор до первого чтения.
        # Закрывается вместе с tarfile при завершении процесса.
        raw = open(path, "rb")
        return tarfile.open(fileobj=zstd.ZstdFile(raw), mode="r:")
    return tarfile.open(path, mode="r:*")


def main() -> int:
    args = [a for a in sys.argv[1:] if a != "--all"]
    all_files = "--all" in sys.argv
    if len(args) < 2:
        print("usage: extract_corpus.py <archive> <dest> [prefix] [--all]")
        return 2
    archive, dest = Path(args[0]), Path(args[1])
    strip = args[2] if len(args) > 2 else ""
    dest.mkdir(parents=True, exist_ok=True)

    extracted = 0
    with _open_tar(archive) as tar:
        for m in tar.getmembers():
            if not m.isfile():
                continue  # симлинки-алиасы на Windows не создаются; имена
                # алиасов и так попадают в индекс через NAME целевой страницы
            name = m.name.replace("\\", "/")
            if strip:
                if not name.startswith(strip):
                    continue
                name = name[len(strip):].lstrip("/")
            if all_files:
                rel = name
            else:
                parts = name.split("/")
                idx = next((i for i, p in enumerate(parts)
                            if p.startswith("man") and len(p) > 3 and p[3:4].isdigit()), None)
                if idx is None:
                    continue
                rel = "/".join(parts[idx:])
            if not rel:
                continue
            # Windows запрещает ':' в именах файлов (страницы вида
            # gnu::aligned.3attr) - санитизация; имя страницы в БД
            # остаётся узнаваемым, такие страницы импортами не встречаются.
            rel = rel.replace(":", "_")
            target = dest / rel
            if ".." in target.parts:
                continue
            target.parent.mkdir(parents=True, exist_ok=True)
            try:
                with tar.extractfile(m) as src, open(target, "wb") as out:
                    out.write(src.read())
            except OSError as e:
                print(f"WARN skip {name}: {e}", file=sys.stderr)
                continue
            extracted += 1
    print(f"extracted {extracted} files -> {dest}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
