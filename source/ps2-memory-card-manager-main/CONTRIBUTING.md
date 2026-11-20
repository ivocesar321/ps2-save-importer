# Contributing

Thanks for helping improve mymc Web.

## Local Setup

```bash
python3 -m venv .venv
source .venv/bin/activate
python -m pip install -r requirements-dev.txt
```

Run the app:

```bash
uvicorn webapp:app --reload
```

Run checks:

```bash
python -m py_compile gui.py guires.py lzari.py mymc.py ps2mc.py ps2mc_dir.py ps2mc_ecc.py ps2save.py round.py sjistab.py verbuild.py webapp.py
pytest -q
```

## Pull Requests

- Keep changes focused.
- Add or update tests for card parsing, save import/export, and web API behavior.
- Do not include personal save files or memory card images in commits.
- Document user-facing behavior in `README.md` when it changes.

## Testing Notes

Tests must not write to a real PCSX2 memcards folder. Use temporary directories and patch `webapp.LOCAL_CARD_DIRS` when testing local-card behavior.
