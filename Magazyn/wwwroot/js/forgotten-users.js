document.addEventListener("DOMContentLoaded", () => {
  const tbody = document.getElementById("forgot-body");
  const statusEl = document.getElementById("forgot-status");

  function setStatus(msg) {
    if (statusEl) statusEl.textContent = msg || "";
  }

  function openModal(id) {
    const el = document.getElementById(id);
    if (el) el.style.display = "flex";
  }
  function closeModal(id) {
    const el = document.getElementById(id);
    if (el) el.style.display = "none";
  }

  document.addEventListener("click", (e) => {
    const t = e.target;
    if (!t) return;

    if (t.classList && t.classList.contains("tlo-okienka")) {
      t.style.display = "none";
      return;
    }

    const modalId = t.getAttribute?.("data-close-modal");
    if (modalId) closeModal(modalId);
  });

  async function fetchList(params = {}) {
    const qs = new URLSearchParams();
    if (params.name) qs.set("name", params.name);
    if (params.adminId) qs.set("adminId", params.adminId);

    const res = await fetch(`/Home/ApiForgottenUsers?${qs.toString()}`);
    if (!res.ok) return [];
    const data = await res.json();
    return Array.isArray(data) ? data : [];
  }

  async function fetchDetails(id) {
    const res = await fetch(`/Home/ApiUser/${id}`);
    if (!res.ok) throw new Error("Nie można pobrać szczegółów");
    return await res.json();
  }

  function renderDetails(user) {
    const body = document.getElementById("forgot-details-body");
    const loading = document.getElementById("forgot-details-loading");
    if (loading) loading.style.display = "none";
    if (!body) return;

    const rows = [
      ["ID", user.id],
      ["Login", user.username],
      ["Imię", user.firstName],
      ["Nazwisko", user.lastName],
      ["Email", user.email],
      ["PESEL", user.pesel],
      ["Data urodzenia", user.dataUrodzenia],
      ["Nr telefonu", user.nrTelefonu],
      ["Płeć", user.plec],
      ["Status", user.status],
      ["Rola", user.rola],
      ["Miejscowość", user.miejscowosc],
      ["Kod pocztowy", user.kodPocztowy],
      ["Ulica", user.ulica],
      ["Nr posesji", user.nrPosesji],
      ["Nr lokalu", user.nrLokalu],
      ["Zapomniany", user.zapomniany ? "TAK" : "NIE"]
    ];

    body.innerHTML = "";
    for (const [k, v] of rows) {
      const tr = document.createElement("tr");
      const th = document.createElement("th");
      th.textContent = k;
      const td = document.createElement("td");
      td.textContent = (v === undefined || v === null || v === "") ? "-" : String(v);
      tr.appendChild(th);
      tr.appendChild(td);
      body.appendChild(tr);
    }
  }

  async function load(params = {}) {
    if (!tbody) return;

    const data = await fetchList(params);
    if (data.length === 0) {
      tbody.innerHTML = "";
      setStatus("NIE ZNALEZIONO");
      return;
    }

    setStatus("");
    tbody.innerHTML = "";

    const cell = (v) => {
      const td = document.createElement("td");
      td.textContent = (v === undefined || v === null || v === "") ? "-" : String(v);
      return td;
    };

    for (const u of data) {
      const tr = document.createElement("tr");
      tr.appendChild(cell(u.id));
      tr.appendChild(cell(u.fullNameAfterForget));
      tr.appendChild(cell(u.dataZapomnienia));
      tr.appendChild(cell(u.zapomnialUserId));

      const tdA = document.createElement("td");
      const btn = document.createElement("button");
      btn.type = "button";
      btn.className = "btn-secondary";
      btn.textContent = "Szczegóły";
      btn.addEventListener("click", async () => {
        const loading = document.getElementById("forgot-details-loading");
        if (loading) loading.style.display = "block";
        const body = document.getElementById("forgot-details-body");
        if (body) body.innerHTML = "";

        try {
          const details = await fetchDetails(u.id);
          renderDetails(details);
          openModal("modal-forgot-details");
        } catch (e) {
          alert("Nie udało się pobrać szczegółów.");
          console.error(e);
        }
      });

      tdA.appendChild(btn);
      tr.appendChild(tdA);
      tbody.appendChild(tr);
    }
  }

  load();

  const form = document.getElementById("forgot-search");
  if (form) {
    form.addEventListener("submit", (e) => {
      e.preventDefault();
      load({
        name: document.getElementById("fname")?.value || "",
        adminId: document.getElementById("adminId")?.value || ""
      });
    });

    form.addEventListener("reset", () => setTimeout(() => load(), 0));
  }
});