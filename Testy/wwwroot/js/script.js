document.addEventListener("DOMContentLoaded", () => {
  const body = document.body;
  const adminUrl = body?.dataset?.adminUrl || null;
  const homeUrl = body?.dataset?.homeUrl || "/";
  const loginUrl = body?.dataset?.loginUrl || "/Account/Login";

  function otworzOkno(idOkna) {
    const okno = document.getElementById(idOkna);
    if (okno) okno.style.display = "flex";
  }

  function zamknijWszystkieOkna() {
    document.querySelectorAll(".tlo-okienka").forEach(okno => {
      okno.style.display = "none";
    });
  }

  // INDEX: przycisk "Zaloguj się"
  const btnLogin = document.getElementById("przycisk-zaloguj");
  if (btnLogin) {
    btnLogin.addEventListener("click", (e) => {
      e.preventDefault();
      otworzOkno("okno-logowania");
    });
  }

  // zamykanie X
  const closeX = document.getElementById("zamknij-logowanie");
  if (closeX) closeX.addEventListener("click", zamknijWszystkieOkna);

  // klik poza oknem
  window.addEventListener("click", (e) => {
    if (e.target && e.target.classList && e.target.classList.contains("tlo-okienka")) {
      zamknijWszystkieOkna();
    }
  });

  // LOGOWANIE (backend)
  const loginForm = document.getElementById("formularz-logowania");
  if (loginForm) {
    loginForm.addEventListener("submit", async (e) => {
      e.preventDefault();

      const user = (document.getElementById("input-login")?.value || "").trim();
      const pass = (document.getElementById("input-haslo")?.value || "").trim();
      const errorMsg = document.getElementById("error-msg");
      if (errorMsg) errorMsg.style.display = "none";

      const token = document.querySelector('#formularz-logowania input[name="__RequestVerificationToken"]')?.value || "";

      try {
        const res = await fetch(loginUrl, {
          method: "POST",
          headers: { "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8" },
          body: new URLSearchParams({ username: user, password: pass, __RequestVerificationToken: token }).toString()
        });

        if (!res.ok) {
          let msg = "Błędne dane!";
          try {
            const data = await res.json();
            if (data?.msg) msg = data.msg;
          } catch { }
          if (errorMsg) {
            errorMsg.textContent = msg;
            errorMsg.style.display = "block";
          } else {
            alert(msg);
          }
          return;
        }

        const data = await res.json();
        if (data?.ok) {
          localStorage.setItem("czyAdmin", "tak");
          localStorage.setItem("rola", data.role || "");
          zamknijWszystkieOkna();

          if (!adminUrl) {
            alert("Brak data-admin-url na <body> w Index.cshtml.");
            return;
          }
          window.location.href = adminUrl;
        } else {
          if (errorMsg) errorMsg.style.display = "block";
        }
      } catch (err) {
        console.error(err);
        if (errorMsg) errorMsg.style.display = "block";
      }
    });
  }

  // wyloguj (na stronach admina)
  const logoutBtn = document.getElementById("akcja-wyloguj");
  if (logoutBtn) {
    logoutBtn.addEventListener("click", (e) => {
      e.preventDefault();
      localStorage.removeItem("czyAdmin");
      localStorage.removeItem("rola");
      window.location.href = "/Account/Logout";
    });
  }

  // guard (AdminPanel/Rejestracja)
  const isAdminPage = !!document.getElementById("akcja-wyloguj");
  if (isAdminPage && localStorage.getItem("czyAdmin") !== "tak") {
    window.location.href = homeUrl;
  }
});