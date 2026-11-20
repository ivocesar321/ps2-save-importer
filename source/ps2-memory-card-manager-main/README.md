# PS2 Memory Card Manager

[![CI](https://github.com/lawlmoeaung/ps2-memory-card-manager/actions/workflows/ci.yml/badge.svg)](https://github.com/lawlmoeaung/ps2-memory-card-manager/actions/workflows/ci.yml)

PS2 Memory Card Manager is a Python 3 modernization of the original `mymc` PlayStation 2 memory card utility, with a local FastAPI web interface for PCSX2 users on macOS and other desktop platforms.

Use it as a PS2 memory card editor, PCSX2 save importer, PSV to PCSX2 workflow helper, and browser-based replacement for the legacy mymc GUI. The app is designed for local use. It can open PCSX2 memory card images, import common PS2 save formats, export saves, delete saves, create new memory cards, and save edits back to local PCSX2 cards with automatic backups.

## Features

- Local browser UI powered by FastAPI.
- Open `.ps2`, `.mcd`, and `.bin` memory card images.
- Create standard 8 MB PS2 memory cards with ECC data enabled by default for PCSX2 compatibility.
- Import `.psu`, `.max`, `.cbs`, `.sps`, and PS2 `.psv` saves.
- Export saves as `.psu` or `.max`.
- Browse macOS PCSX2 memory cards from `~/Library/Application Support/PCSX2/memcards`.
- Save edited local PCSX2 cards back to their original path after creating a unique `.bak` backup.
- Retains the original command-line `mymc.py` utility.

## Requirements

- Python 3.10 or newer.
- PCSX2 memory card images if you want to edit existing cards.

## Quick Start

```bash
python3 -m venv .venv
source .venv/bin/activate
python -m pip install -r requirements.txt
uvicorn webapp:app --reload
```

Open [http://127.0.0.1:8000](http://127.0.0.1:8000).

## PCSX2 Workflow

1. Close PCSX2 before editing a card.
2. Start PS2 Memory Card Manager and click `Refresh` in the PCSX2 Cards section.
3. Choose the card, then click `Open Selected`.
4. Import, export, or delete saves.
5. Click `Save Back` to write the edited copy back to PCSX2. A `.bak` backup is created first.

If PCSX2 asks to format a card, verify that the image is the ECC-included format expected by your PCSX2 install. New cards created by this app use ECC by default.

## Supported Save Formats

| Format | Import | Export | Notes |
| --- | --- | --- | --- |
| `.psu` | Yes | Yes | EMS format |
| `.max` | Yes | Yes | MAX Drive format |
| `.cbs` | Yes | No | CodeBreaker format |
| `.sps` | Yes | No | SharkPort/X-Port format |
| `.psv` | Yes | No | PS2 PSV only; PS1 PSV is not supported |

## Command Line

The original CLI is still available:

```bash
python mymc.py path/to/Mcd001.ps2 dir
python mymc.py path/to/Mcd001.ps2 import save.psu
python mymc.py path/to/Mcd001.ps2 export BASLUS-00000
```

Run `python mymc.py -h` for command help.

## Development

```bash
python3 -m venv .venv
source .venv/bin/activate
python -m pip install -r requirements-dev.txt
python -m py_compile gui.py guires.py lzari.py mymc.py ps2mc.py ps2mc_dir.py ps2mc_ecc.py ps2save.py round.py sjistab.py verbuild.py webapp.py
pytest -q
```

## Data Safety

Memory card editing is inherently risky. Keep PCSX2 closed while saving back to a card, and keep backups of important cards. The app creates a backup before `Save Back`, but you should also keep your own long-term backups.

## Credits

The original mymc utility was written by Ross Ridge and released into the public domain. This project keeps that foundation and adds a Python 3 web workflow for current PCSX2 users.

PSV import behavior was implemented with reference to public PSV format work from the community.

## License

This project is released under the Unlicense. See [LICENSE](LICENSE).
