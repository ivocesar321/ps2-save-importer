const state = {
  sessionId: localStorage.getItem("ps2MemoryCardManager.sessionId") || null,
};

const els = {
  cardInput: document.querySelector("#cardInput"),
  saveInput: document.querySelector("#saveInput"),
  newCardBtn: document.querySelector("#newCardBtn"),
  downloadCardBtn: document.querySelector("#downloadCardBtn"),
  saveBackBtn: document.querySelector("#saveBackBtn"),
  refreshLocalBtn: document.querySelector("#refreshLocalBtn"),
  openLocalBtn: document.querySelector("#openLocalBtn"),
  localCardSelect: document.querySelector("#localCardSelect"),
  eccToggle: document.querySelector("#eccToggle"),
  overwriteToggle: document.querySelector("#overwriteToggle"),
  rows: document.querySelector("#saveRows"),
  status: document.querySelector("#status"),
  sessionLabel: document.querySelector("#sessionLabel"),
  usedLabel: document.querySelector("#usedLabel"),
  freeLabel: document.querySelector("#freeLabel"),
  totalLabel: document.querySelector("#totalLabel"),
  countLabel: document.querySelector("#countLabel"),
  usageBar: document.querySelector("#usageBar"),
};

function fmtKb(kb) {
  return `${new Intl.NumberFormat().format(kb)} KB`;
}

function setStatus(message, type = "") {
  els.status.textContent = message || "";
  els.status.className = `status ${type}`.trim();
}

async function api(path, options = {}) {
  const response = await fetch(path, options);
  if (!response.ok) {
    let message = response.statusText;
    try {
      const body = await response.json();
      message = body.detail || message;
    } catch (_error) {
      message = await response.text();
    }
    throw new Error(message);
  }
  return response.json();
}

function updateSummary(card) {
  state.sessionId = card.sessionId;
  localStorage.setItem("ps2MemoryCardManager.sessionId", card.sessionId);
  const format = card.hasEcc ? "ECC" : "No ECC";
  const source = card.sourcePath ? ` • ${card.sourcePath.split("/").pop()}` : "";
  els.sessionLabel.textContent = `${card.sessionId.slice(0, 8)} • ${format} • ${fmtKb(card.fileSizeKb)}${source}`;
  els.downloadCardBtn.disabled = false;
  els.saveBackBtn.disabled = !card.canSaveBack;
  els.usedLabel.textContent = fmtKb(Math.ceil(card.usedBytes / 1024));
  els.freeLabel.textContent = `${fmtKb(card.freeKb)} free`;
  els.totalLabel.textContent = `${fmtKb(card.totalKb)} total`;
  els.countLabel.textContent = `${card.saveCount} ${card.saveCount === 1 ? "save" : "saves"}`;
  const usedPct = card.totalBytes ? Math.min(100, Math.round((card.usedBytes / card.totalBytes) * 100)) : 0;
  els.usageBar.style.width = `${usedPct}%`;
  renderRows(card.saves);
}

function renderLocalCards(cards) {
  els.localCardSelect.innerHTML = "";
  if (!cards.length) {
    els.localCardSelect.innerHTML = `<option value="">No PCSX2 cards found</option>`;
    els.localCardSelect.disabled = true;
    els.openLocalBtn.disabled = true;
    return;
  }

  for (const card of cards) {
    const option = document.createElement("option");
    option.value = card.path;
    const format = card.hasEcc === null ? "Unreadable" : (card.hasEcc ? "ECC" : "No ECC");
    option.textContent = `${card.name} · ${format} · ${fmtKb(card.sizeKb)}`;
    option.disabled = !card.readable;
    els.localCardSelect.appendChild(option);
  }
  const firstReadable = cards.find((card) => card.readable);
  if (firstReadable) els.localCardSelect.value = firstReadable.path;
  els.localCardSelect.disabled = false;
  els.openLocalBtn.disabled = !els.localCardSelect.value;
}

function renderRows(saves) {
  els.rows.innerHTML = "";
  if (!saves.length) {
    const row = document.createElement("tr");
    row.className = "empty";
    row.innerHTML = `<td colspan="5">No saves</td>`;
    els.rows.appendChild(row);
    return;
  }

  for (const save of saves) {
    const row = document.createElement("tr");
    const title = save.title || "Corrupt";
    const subtitle = save.subtitle ? `<span>${escapeHtml(save.subtitle)}</span>` : "";
    row.innerHTML = `
      <td>${escapeHtml(save.dirname)}</td>
      <td class="titleCell"><strong>${escapeHtml(title)}</strong>${subtitle}</td>
      <td>${fmtKb(save.sizeKb)}</td>
      <td>${escapeHtml(save.protection)}</td>
      <td>
        <div class="rowActions">
          <button type="button" data-action="psu" data-dir="${encodeURIComponent(save.dirname)}">PSU</button>
          <button type="button" data-action="max" data-dir="${encodeURIComponent(save.dirname)}">MAX</button>
          <button class="danger" type="button" data-action="delete" data-dir="${encodeURIComponent(save.dirname)}">Delete</button>
        </div>
      </td>
    `;
    els.rows.appendChild(row);
  }
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

async function loadCurrent() {
  if (!state.sessionId) return;
  try {
    const card = await api(`/api/cards/${state.sessionId}`);
    updateSummary(card);
    setStatus("");
  } catch (error) {
    localStorage.removeItem("ps2MemoryCardManager.sessionId");
    state.sessionId = null;
    setStatus(error.message, "error");
  }
}

async function loadLocalCards() {
  try {
    const body = await api("/api/local/cards");
    renderLocalCards(body.cards);
  } catch (error) {
    setStatus(error.message, "error");
  }
}

els.cardInput.addEventListener("change", async () => {
  const file = els.cardInput.files[0];
  if (!file) return;
  const form = new FormData();
  form.append("card", file);
  try {
    setStatus("Opening card...");
    updateSummary(await api("/api/cards", { method: "POST", body: form }));
    setStatus("Card loaded");
  } catch (error) {
    setStatus(error.message, "error");
  } finally {
    els.cardInput.value = "";
  }
});

els.newCardBtn.addEventListener("click", async () => {
  const form = new FormData();
  form.append("with_ecc", els.eccToggle.checked ? "true" : "false");
  try {
    setStatus("Creating card...");
    updateSummary(await api("/api/cards/new", { method: "POST", body: form }));
    setStatus("Card created");
  } catch (error) {
    setStatus(error.message, "error");
  }
});

els.saveInput.addEventListener("change", async () => {
  const file = els.saveInput.files[0];
  if (!file || !state.sessionId) return;
  const form = new FormData();
  form.append("save", file);
  form.append("overwrite", els.overwriteToggle.checked ? "true" : "false");
  try {
    setStatus("Importing save...");
    updateSummary(await api(`/api/cards/${state.sessionId}/saves/import`, { method: "POST", body: form }));
    setStatus("Save imported");
  } catch (error) {
    setStatus(error.message, error.message.includes("exists") ? "warn" : "error");
  } finally {
    els.saveInput.value = "";
  }
});

els.downloadCardBtn.addEventListener("click", () => {
  if (!state.sessionId) return;
  window.location.href = `/api/cards/${state.sessionId}/download`;
});

els.saveBackBtn.addEventListener("click", async () => {
  if (!state.sessionId) return;
  if (!window.confirm("Save this edited card back to the PCSX2 memcards folder? A .bak backup will be created first.")) return;
  try {
    setStatus("Saving back to PCSX2 card...");
    const card = await api(`/api/cards/${state.sessionId}/save-back`, { method: "POST" });
    updateSummary(card);
    setStatus(`Saved back. Backup: ${card.backupPath}`);
  } catch (error) {
    setStatus(error.message, "error");
  }
});

els.refreshLocalBtn.addEventListener("click", async () => {
  setStatus("Loading PCSX2 cards...");
  await loadLocalCards();
  setStatus("");
});

els.openLocalBtn.addEventListener("click", async () => {
  const path = els.localCardSelect.value;
  if (!path) return;
  try {
    setStatus("Opening PCSX2 card...");
    const card = await api("/api/local/cards/open", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ path }),
    });
    updateSummary(card);
    setStatus("PCSX2 card opened as an editable copy");
  } catch (error) {
    setStatus(error.message, "error");
  }
});

els.rows.addEventListener("click", async (event) => {
  const button = event.target.closest("button[data-action]");
  if (!button || !state.sessionId) return;
  const dirname = button.dataset.dir;
  const action = button.dataset.action;
  if (action === "psu" || action === "max") {
    window.location.href = `/api/cards/${state.sessionId}/saves/${dirname}/export?format=${action}`;
    return;
  }
  if (action === "delete") {
    if (!window.confirm("Delete this save?")) return;
    try {
      setStatus("Deleting save...");
      updateSummary(await api(`/api/cards/${state.sessionId}/saves/${dirname}`, { method: "DELETE" }));
      setStatus("Save deleted");
    } catch (error) {
      setStatus(error.message, "error");
    }
  }
});

loadCurrent();
loadLocalCards();
