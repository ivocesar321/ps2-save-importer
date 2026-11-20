import struct

from fastapi.testclient import TestClient

import ps2mc
import ps2save
import webapp
from ps2mc_dir import DF_0400, DF_DIR, DF_EXISTS, DF_FILE, DF_RWX, pack_tod


def create_card(path, with_ecc=True):
    params = (
        with_ecc,
        ps2mc.PS2MC_STANDARD_PAGE_SIZE,
        ps2mc.PS2MC_STANDARD_PAGES_PER_ERASE_BLOCK,
        ps2mc.PS2MC_STANDARD_PAGES_PER_CARD,
    )
    with open(path, "w+b") as f:
        ps2mc.ps2mc(f, True, params).close()


def configure_test_storage(monkeypatch, tmp_path):
    sessions = tmp_path / "sessions"
    sessions.mkdir()
    monkeypatch.setattr(webapp, "SESSION_DIR", sessions)
    local_cards = tmp_path / "memcards"
    local_cards.mkdir()
    monkeypatch.setattr(webapp, "LOCAL_CARD_DIRS", [local_cards])
    return local_cards


def fixed_name(value, length):
    raw = value.encode("latin-1")
    return raw + (b"\0" * (length - len(raw)))


def build_psv(dirname="BASLUS-00000PSV", filename="DATA.BIN", data=b"hello"):
    tod = pack_tod((0, 0, 12, 1, 1, 2026))
    header = bytearray(0x40)
    header[:4] = b"\0VSP"
    header[0x38:0x40] = struct.pack("<LL", 0x2C, 2)

    data_offset = 0x40 + 0x28 + 0x38 + 0x3C
    ps2_header = struct.pack("<10L", len(data), 0, 0, 0, 0, 0, 0, 0, 0, 1)
    main_dir = (
        tod
        + tod
        + struct.pack("<L", 3)
        + struct.pack("<L", DF_RWX | DF_DIR | DF_0400 | DF_EXISTS)
        + fixed_name(dirname, 32)
    )
    file_info = (
        tod
        + tod
        + struct.pack("<L", len(data))
        + struct.pack("<L", DF_RWX | DF_FILE | DF_EXISTS)
        + fixed_name(filename, 32)
        + struct.pack("<L", data_offset)
    )
    return bytes(header) + ps2_header + main_dir + file_info + data


def test_create_card_defaults_to_ecc(monkeypatch, tmp_path):
    configure_test_storage(monkeypatch, tmp_path)
    client = TestClient(webapp.app)

    response = client.post("/api/cards/new", data={})

    assert response.status_code == 200
    body = response.json()
    assert body["hasEcc"] is True
    assert body["fileSizeBytes"] == 8650752


def test_local_card_open_and_save_back_creates_unique_backup(monkeypatch, tmp_path):
    local_cards = configure_test_storage(monkeypatch, tmp_path)
    card_path = local_cards / "Mcd001.ps2"
    create_card(card_path)
    client = TestClient(webapp.app)

    listed = client.get("/api/local/cards")
    assert listed.status_code == 200
    assert listed.json()["cards"][0]["readable"] is True

    opened = client.post("/api/local/cards/open", json={"path": str(card_path)})
    assert opened.status_code == 200
    assert opened.json()["canSaveBack"] is True

    session_id = opened.json()["sessionId"]
    first = client.post(f"/api/cards/{session_id}/save-back")
    second = client.post(f"/api/cards/{session_id}/save-back")

    assert first.status_code == 200
    assert second.status_code == 200
    assert first.json()["backupPath"] != second.json()["backupPath"]
    assert len(list(local_cards.glob("Mcd001.ps2.*.bak"))) == 2


def test_import_ps2_psv_save(monkeypatch, tmp_path):
    configure_test_storage(monkeypatch, tmp_path)
    client = TestClient(webapp.app)
    created = client.post("/api/cards/new", data={"with_ecc": "true"})
    session_id = created.json()["sessionId"]

    response = client.post(
        f"/api/cards/{session_id}/saves/import",
        data={"overwrite": "false"},
        files={"save": ("save.psv", build_psv(), "application/octet-stream")},
    )

    assert response.status_code == 200
    saves = response.json()["saves"]
    assert [save["dirname"] for save in saves] == ["BASLUS-00000PSV"]


def test_ps1_psv_is_rejected(monkeypatch, tmp_path):
    configure_test_storage(monkeypatch, tmp_path)
    client = TestClient(webapp.app)
    created = client.post("/api/cards/new", data={"with_ecc": "true"})
    session_id = created.json()["sessionId"]

    header = bytearray(0x40)
    header[:4] = b"\0VSP"
    header[0x38:0x40] = struct.pack("<LL", 0x2C, 1)
    response = client.post(
        f"/api/cards/{session_id}/saves/import",
        files={"save": ("ps1.psv", bytes(header), "application/octet-stream")},
    )

    assert response.status_code == 400
    assert "Only PS2 .psv save files are supported" in response.json()["detail"]
