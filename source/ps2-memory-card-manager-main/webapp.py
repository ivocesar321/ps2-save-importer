from pathlib import Path
import shutil
import uuid

from fastapi import FastAPI, File, Form, HTTPException, UploadFile
from pydantic import BaseModel
from fastapi.responses import FileResponse
from fastapi.staticfiles import StaticFiles

import ps2mc
import ps2save
from ps2mc_dir import (
    DF_DIR,
    DF_EXISTS,
    DF_FILE,
    DF_POCKETSTN,
    DF_PROTECTED,
    DF_PSX,
    DF_WRITE,
    mode_is_dir,
)


BASE_DIR = Path(__file__).resolve().parent
STATIC_DIR = BASE_DIR / "static"
SESSION_DIR = BASE_DIR / "web_data" / "sessions"
SESSION_DIR.mkdir(parents=True, exist_ok=True)
LOCAL_CARD_DIRS = [
    Path.home() / "Library" / "Application Support" / "PCSX2" / "memcards",
]

app = FastAPI(title="PS2 Memory Card Manager")
app.mount("/static", StaticFiles(directory=STATIC_DIR), name="static")


class LocalCardOpenRequest(BaseModel):
    path: str


def _session_root(session_id):
    try:
        uuid.UUID(session_id)
    except ValueError:
        raise HTTPException(status_code=404, detail="Card session not found")
    return SESSION_DIR / session_id


def _session_path(session_id):
    path = _session_root(session_id) / "card.ps2"
    if not path.exists():
        raise HTTPException(status_code=404, detail="Card session not found")
    return path


def _source_path(session_id):
    source_file = _session_root(session_id) / "source.txt"
    if not source_file.exists():
        return None
    source = Path(source_file.read_text().strip()).expanduser()
    if not source.exists():
        return None
    return source


def _write_source_path(session_id, source):
    (_session_root(session_id) / "source.txt").write_text(str(source))


def _is_allowed_local_card(path):
    try:
        resolved = path.expanduser().resolve()
    except OSError:
        return False
    for base in LOCAL_CARD_DIRS:
        try:
            resolved.relative_to(base.expanduser().resolve())
            return resolved.suffix.lower() in {".ps2", ".mcd", ".bin"}
        except ValueError:
            continue
    return False


def _safe_dirname(dirname):
    if not dirname or dirname in {".", ".."} or "/" in dirname or "\0" in dirname:
        raise HTTPException(status_code=400, detail="Invalid save directory")
    return dirname


def _protection_label(mode):
    if mode & DF_PSX:
        return "PocketStation" if mode & DF_POCKETSTN else "PlayStation"
    protection = mode & (DF_PROTECTED | DF_WRITE)
    if protection == 0:
        return "Delete Protected"
    if protection == DF_WRITE:
        return "Not Protected"
    if protection == DF_PROTECTED:
        return "Copy & Delete Protected"
    return "Copy Protected"


def _title_for_save(mc):
    icon_sys = mc.get_icon_sys(".")
    if icon_sys is None:
        return ["Corrupt", ""]
    try:
        return list(ps2save.icon_sys_title(ps2save.unpack_icon_sys(icon_sys), "unicode"))
    except Exception:
        return ["Corrupt", ""]


def _summarize_card(path, session_id):
    try:
        with open(path, "rb") as f:
            mc = ps2mc.ps2mc(f, True)
            saves = []
            root = mc.dir_open("/")
            try:
                entries = list(root)[2:]
            finally:
                root.close()

            for ent in entries:
                mode = ent[0]
                if not (mode & DF_EXISTS) or not mode_is_dir(mode):
                    continue
                dirname = ent[8]
                try:
                    mc.chdir("/" + dirname)
                    size = mc.dir_size(".")
                    title = _title_for_save(mc)
                except Exception:
                    size = 0
                    title = ["Corrupt", ""]
                saves.append(
                    {
                        "dirname": dirname,
                        "title": title[0],
                        "subtitle": title[1],
                        "sizeBytes": size,
                        "sizeKb": (size + 1023) // 1024,
                        "protection": _protection_label(mode),
                        "psx": bool(mode & DF_PSX),
                    }
                )

            free = mc.get_free_space()
            total = mc.get_allocatable_space()
            has_ecc = mc.spare_size > 0
            mc.close()
    except Exception as exc:
        raise HTTPException(status_code=400, detail=str(exc))

    saves.sort(key=lambda item: item["dirname"])
    source = _source_path(session_id)
    file_size = path.stat().st_size
    return {
        "sessionId": session_id,
        "filename": path.name,
        "fileSizeBytes": file_size,
        "fileSizeKb": (file_size + 1023) // 1024,
        "hasEcc": has_ecc,
        "sourcePath": str(source) if source else None,
        "canSaveBack": source is not None,
        "freeBytes": free,
        "freeKb": free // 1024,
        "totalBytes": total,
        "totalKb": total // 1024,
        "usedBytes": max(total - free, 0),
        "saveCount": len(saves),
        "saves": saves,
    }


def _load_save_file(path):
    sf = ps2save.ps2_save_file()
    with open(path, "rb") as f:
        file_type = ps2save.detect_file_type(f)
        f.seek(0)
        if file_type == "psu":
            sf.load_ems(f)
        elif file_type == "max":
            sf.load_max_drive(f)
        elif file_type == "cbs":
            sf.load_codebreaker(f)
        elif file_type == "sps":
            sf.load_sharkport(f)
        elif file_type == "npo":
            raise HTTPException(status_code=400, detail="nPort saves are not supported")
        elif file_type == "psv":
            sf.load_psv(f)
        else:
            raise HTTPException(status_code=400, detail="Save file format not recognized")
    return sf, file_type


def _local_card_summary(path):
    try:
        with open(path, "rb") as f:
            mc = ps2mc.ps2mc(f, True)
            free = mc.get_free_space()
            total = mc.get_allocatable_space()
            has_ecc = mc.spare_size > 0
            mc.close()
        readable = True
        detail = ""
    except Exception as exc:
        free = total = 0
        has_ecc = None
        readable = False
        detail = str(exc)
    size = path.stat().st_size
    return {
        "path": str(path),
        "name": path.name,
        "sizeBytes": size,
        "sizeKb": (size + 1023) // 1024,
        "hasEcc": has_ecc,
        "freeKb": free // 1024,
        "totalKb": total // 1024,
        "readable": readable,
        "detail": detail,
    }


@app.get("/")
def index():
    return FileResponse(STATIC_DIR / "index.html")


@app.post("/api/cards")
def upload_card(card: UploadFile = File(...)):
    session_id = str(uuid.uuid4())
    session_root = SESSION_DIR / session_id
    session_root.mkdir(parents=True, exist_ok=True)
    path = session_root / "card.ps2"
    with open(path, "wb") as out:
        shutil.copyfileobj(card.file, out)
    return _summarize_card(path, session_id)


@app.get("/api/local/cards")
def list_local_cards():
    cards = []
    for base in LOCAL_CARD_DIRS:
        if not base.exists():
            continue
        for path in sorted(base.iterdir(), key=lambda item: item.name.lower()):
            if path.is_file() and path.suffix.lower() in {".ps2", ".mcd", ".bin"}:
                cards.append(_local_card_summary(path))
    return {"cards": cards}


@app.post("/api/local/cards/open")
def open_local_card(request: LocalCardOpenRequest):
    source = Path(request.path).expanduser()
    if not source.exists() or not _is_allowed_local_card(source):
        raise HTTPException(status_code=400, detail="Local card path is not allowed")
    session_id = str(uuid.uuid4())
    session_root = SESSION_DIR / session_id
    session_root.mkdir(parents=True, exist_ok=True)
    path = session_root / "card.ps2"
    shutil.copy2(source, path)
    _write_source_path(session_id, source.resolve())
    return _summarize_card(path, session_id)


@app.post("/api/cards/new")
def create_card(with_ecc: bool = Form(True)):
    session_id = str(uuid.uuid4())
    session_root = SESSION_DIR / session_id
    session_root.mkdir(parents=True, exist_ok=True)
    path = session_root / "card.ps2"
    params = (
        with_ecc,
        ps2mc.PS2MC_STANDARD_PAGE_SIZE,
        ps2mc.PS2MC_STANDARD_PAGES_PER_ERASE_BLOCK,
        ps2mc.PS2MC_STANDARD_PAGES_PER_CARD,
    )
    with open(path, "w+b") as f:
        ps2mc.ps2mc(f, True, params).close()
    return _summarize_card(path, session_id)


@app.get("/api/cards/{session_id}")
def get_card(session_id: str):
    path = _session_path(session_id)
    return _summarize_card(path, session_id)


@app.get("/api/cards/{session_id}/download")
def download_card(session_id: str):
    path = _session_path(session_id)
    return FileResponse(path, filename="card.ps2", media_type="application/octet-stream")


@app.post("/api/cards/{session_id}/save-back")
def save_back(session_id: str):
    card_path = _session_path(session_id)
    source = _source_path(session_id)
    if source is None:
        raise HTTPException(status_code=400, detail="This card was not opened from a local PCSX2 path")
    if not _is_allowed_local_card(source):
        raise HTTPException(status_code=400, detail="Local card path is not allowed")
    backup = source.with_name(f"{source.name}.{uuid.uuid4().hex[:8]}.bak")
    shutil.copy2(source, backup)
    shutil.copy2(card_path, source)
    summary = _summarize_card(card_path, session_id)
    summary["backupPath"] = str(backup)
    return summary


@app.post("/api/cards/{session_id}/saves/import")
def import_save(session_id: str, save: UploadFile = File(...), overwrite: bool = Form(False)):
    card_path = _session_path(session_id)
    upload_path = card_path.parent / ("upload-" + uuid.uuid4().hex)
    try:
        with open(upload_path, "wb") as out:
            shutil.copyfileobj(save.file, out)
        sf, _file_type = _load_save_file(upload_path)
        with open(card_path, "r+b") as f:
            mc = ps2mc.ps2mc(f, True)
            if overwrite:
                dirname = "/" + sf.get_directory()[8]
                if mc.get_mode(dirname) is not None:
                    mc.rmdir(dirname)
            imported = mc.import_save_file(sf, False)
            mc.close()
        if not imported:
            raise HTTPException(status_code=409, detail="Save already exists")
        return _summarize_card(card_path, session_id)
    except HTTPException:
        raise
    except Exception as exc:
        raise HTTPException(status_code=400, detail=str(exc))
    finally:
        upload_path.unlink(missing_ok=True)


@app.get("/api/cards/{session_id}/saves/{dirname}/export")
def export_save(session_id: str, dirname: str, format: str = "psu"):
    dirname = _safe_dirname(dirname)
    card_path = _session_path(session_id)
    if format not in {"psu", "max"}:
        raise HTTPException(status_code=400, detail="Export format must be psu or max")

    out_path = card_path.parent / f"{dirname}.{format}"
    try:
        with open(card_path, "rb") as f:
            mc = ps2mc.ps2mc(f, True)
            sf = mc.export_save_file("/" + dirname)
            with open(out_path, "wb") as out:
                if format == "max":
                    sf.save_max_drive(out)
                else:
                    sf.save_ems(out)
            mc.close()
    except Exception as exc:
        raise HTTPException(status_code=400, detail=str(exc))
    return FileResponse(out_path, filename=out_path.name, media_type="application/octet-stream")


@app.delete("/api/cards/{session_id}/saves/{dirname}")
def delete_save(session_id: str, dirname: str):
    dirname = _safe_dirname(dirname)
    card_path = _session_path(session_id)
    try:
        with open(card_path, "r+b") as f:
            mc = ps2mc.ps2mc(f, True)
            mc.rmdir("/" + dirname)
            mc.close()
    except Exception as exc:
        raise HTTPException(status_code=400, detail=str(exc))
    return _summarize_card(card_path, session_id)
