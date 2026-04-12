document.addEventListener("DOMContentLoaded", () => {
  function openModal(id) {
    const el = document.getElementById(id);
    if (el) el.style.display = "flex";
  }
  function closeModal(id) {
    const el = document.getElementById(id);
    if (el) el.style.display = "none";
  }

  const statusEl = document.getElementById("status-wyszukiwania");
  function setStatus(msg) {
    if (!statusEl) return;
    statusEl.textContent = msg || "";
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

  async function fetchUser(id) {
    const res = await fetch(`/Home/ApiUser/${id}`);
    if (!res.ok) throw new Error("Nie można pobrać danych użytkownika");
    return await res.json();
  }

  function setEditMsg(text, kind) {
    const el = document.getElementById("edit-msg");
    if (!el) return;
    el.style.color = kind === "ok" ? "#57ab5a" : "#ff6b6b";
    el.textContent = text || "";
  }

  function renderDetails(user) {
    const tbody = document.getElementById("details-body");
    const loading = document.getElementById("details-loading");
    if (loading) loading.style.display = "none";
    if (!tbody) return;

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
      ["Nr lokalu", user.nrLokalu]
    ];

    tbody.innerHTML = "";
    for (const [k, v] of rows) {
      const tr = document.createElement("tr");
      const th = document.createElement("th");
      th.textContent = k;
      const td = document.createElement("td");
      td.textContent = (v === undefined || v === null || v === "") ? "-" : String(v);
      tr.appendChild(th);
      tr.appendChild(td);
      tbody.appendChild(tr);
    }
  }

  function fillEditForm(user) {
    document.getElementById("edit-id").value = user.id;
    document.getElementById("edit-username").value = user.username || "";
    document.getElementById("edit-password").value = user.password || "";
    document.getElementById("edit-firstName").value = user.firstName || "";
    document.getElementById("edit-lastName").value = user.lastName || "";
    document.getElementById("edit-email").value = user.email || "";
    document.getElementById("edit-pesel").value = user.pesel || "";
    document.getElementById("edit-dataUrodzenia").value = user.dataUrodzenia || "";
    document.getElementById("edit-phone").value = user.nrTelefonu || "";
    document.getElementById("edit-plec").value = user.plec || "Kobieta";
    document.getElementById("edit-status").value = user.status || "Aktywny";
    document.getElementById("edit-rola").value = user.rola || "Użytkownik";

    document.getElementById("edit-miejscowosc").value = user.miejscowosc || "";
    document.getElementById("edit-kod").value = user.kodPocztowy || "";
    document.getElementById("edit-ulica").value = user.ulica || "";
    document.getElementById("edit-nrposesji").value = user.nrPosesji || "";
    document.getElementById("edit-nrlokalu").value = user.nrLokalu || "";
  }

  async function loadUsers(params = {}) {
    const qs = new URLSearchParams();
    if (params.login) qs.set("login", params.login);
    if (params.name) qs.set("name", params.name);
    if (params.pesel) qs.set("pesel", params.pesel);

    const res = await fetch(`/Home/ApiUsers?${qs.toString()}`);
    const tbody = document.getElementById("bd-dane");
    if (!tbody) return;

    if (!res.ok) {
      tbody.innerHTML = "";
      setStatus("NIE ZNALEZIONO");
      return;
    }

    const data = await res.json();
    if (!Array.isArray(data) || data.length === 0) {
      tbody.innerHTML = "";
      setStatus("NIE ZNALEZIONO");
      return;
    }

    setStatus("");
    tbody.innerHTML = "";

    const cell = (val) => {
      const td = document.createElement("td");
      td.textContent = (val === undefined || val === null || val === "") ? "-" : String(val);
      return td;
    };

    for (const u of data) {
      const tr = document.createElement("tr");
      tr.appendChild(cell(u.username));
      tr.appendChild(cell(u.firstName));
      tr.appendChild(cell(u.lastName));
      tr.appendChild(cell(u.email));
      tr.appendChild(cell(u.pesel));

      const tdA = document.createElement("td");

      const btnEdit = document.createElement("button");
      btnEdit.type = "button";
      btnEdit.className = "btn-primary";
      btnEdit.textContent = "Edytuj";
      btnEdit.addEventListener("click", async () => {
        setEditMsg("", null);
        try {
          const user = await fetchUser(u.id);
          fillEditForm(user);
          openModal("modal-edit");
        } catch (e) {
          alert("Nie udało się pobrać danych do edycji.");
          console.error(e);
        }
      });

      const btnDetails = document.createElement("button");
      btnDetails.type = "button";
      btnDetails.className = "btn-secondary";
      btnDetails.textContent = "Szczegóły";
      btnDetails.style.marginLeft = "8px";
      btnDetails.addEventListener("click", async () => {
        const loading = document.getElementById("details-loading");
        if (loading) loading.style.display = "block";
        document.getElementById("details-body").innerHTML = "";

        try {
          const user = await fetchUser(u.id);
          renderDetails(user);
          openModal("modal-details");
        } catch (e) {
          alert("Nie udało się pobrać szczegółów użytkownika.");
          console.error(e);
        }
      });

      const btnForget = document.createElement("button");
      btnForget.type = "button";
      btnForget.className = "btn-secondary";
      btnForget.textContent = "Zapomnij";
      btnForget.style.marginLeft = "8px";
      btnForget.addEventListener("click", async () => {
        if (!confirm(`Na pewno zapomnieć użytkownika: ${u.username}?`)) return;

        // Na start adminId=1 (zmień jak będziesz mieć prawdziwe ID admina)
        const res2 = await fetch(`/Home/ForgetUser/${u.id}?adminId=1`, { method: "POST" });
        if (!res2.ok) {
          alert("Błąd podczas zapominania użytkownika.");
          return;
        }

        await loadUsers(params);
        alert("Użytkownik został zapomniany.");
      });

      tdA.appendChild(btnEdit);
      tdA.appendChild(btnDetails);
      tdA.appendChild(btnForget);

      tr.appendChild(tdA);
      tbody.appendChild(tr);
    }
  }

  loadUsers();

  const form = document.getElementById("emp-search");
  if (form) {
    form.addEventListener("submit", (e) => {
      e.preventDefault();
      loadUsers({
        login: document.getElementById("login")?.value || "",
        name: document.getElementById("name")?.value || "",
        pesel: document.getElementById("pesel")?.value || ""
      });
    });

    form.addEventListener("reset", () => setTimeout(() => loadUsers(), 0));
  }

  const editForm = document.getElementById("edit-form");
  if (editForm) {
    editForm.addEventListener("submit", async (e) => {
      e.preventDefault();
      setEditMsg("", null);

      const formData = new FormData(editForm);
      const body = new URLSearchParams();
      for (const [k, v] of formData.entries()) body.set(k, v.toString());

      const res = await fetch("/Home/UpdateUser", {
        method: "POST",
        headers: { "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8" },
        body: body.toString()
      });

      if (!res.ok) {
        let msg = "Nie udało się zapisać zmian.";
        try {
          const data = await res.json();
          if (data?.msg) msg = data.msg;
        } catch { }
        setEditMsg(msg, "err");
        return;
      }

      closeModal("modal-edit");
      await loadUsers({
        login: document.getElementById("login")?.value || "",
        name: document.getElementById("name")?.value || "",
        pesel: document.getElementById("pesel")?.value || ""
      });

      alert("Zapisano zmiany.");
    });
  }
});