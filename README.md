# Magazyn GiTA – System Zarządzania Pracownikami

Aplikacja webowa ASP.NET Core MVC do zarządzania personelem i uprawnieniami w firmie.

---

## System uprawnień (Role)

Aplikacja posiada **5 ról**, które określają zakres dostępu do funkcji systemu:

| Rola | Opis |
|------|------|
| **Administrator** | Pełny dostęp do wszystkich funkcji |
| **Kierownik magazynu** | Dostęp do zarządzania pracownikami (bez RODO i uprawnień) |
| **Kierownik sprzedazy** | Podgląd pracowników (tylko odczyt) |
| **Pracownik magazynu** | Dostęp tylko do własnego profilu i zmiany hasła |
| **Sprzedawca** | Dostęp tylko do własnego profilu i zmiany hasła |

---

## Tabela dostępu według ról

| Funkcja | Administrator | Kierownik magazynu | Kierownik sprzedazy | Pracownik magazynu | Sprzedawca |
|---------|:---:|:---:|:---:|:---:|:---:|
| Przegląd personelu (lista pracowników) | ✅ | ✅ | ✅ | ❌ | ❌ |
| Szczegóły pracownika | ✅ | ✅ | ✅ | ❌ | ❌ |
| Rejestracja nowego pracownika | ✅ | ✅ | ❌ | ❌ | ❌ |
| Edycja danych pracownika | ✅ | ✅ | ❌ | ❌ | ❌ |
| Generowanie hasła tymczasowego | ✅ | ✅ | ❌ | ❌ | ❌ |
| Użytkownicy zapomniani (RODO) | ✅ | ❌ | ❌ | ❌ | ❌ |
| Zapomnienie użytkownika (RODO) | ✅ | ❌ | ❌ | ❌ | ❌ |
| Lista Uprawnień | ✅ | ❌ | ❌ | ❌ | ❌ |
| Nadawanie/zmiana uprawnień | ✅ | ❌ | ❌ | ❌ | ❌ |
| Mój profil | ✅ | ✅ | ✅ | ✅ | ✅ |
| Zmiana własnego hasła | ✅ | ✅ | ✅ | ✅ | ✅ |
| Odzyskiwanie hasła (e-mail) | ✅ | ✅ | ✅ | ✅ | ✅ |

---

## Opis ról

### 🔴 Administrator
Posiada pełny dostęp do systemu:
- Zarządza wszystkimi pracownikami (dodawanie, edycja, podgląd)
- Może zapomnieć użytkownika zgodnie z RODO
- Może nadawać i odbierać uprawnienia (role) innym użytkownikom
- Widzi listę użytkowników zapomnianych (RODO)

### 🟠 Kierownik magazynu
Zarządza pracownikami na poziomie operacyjnym:
- Przegląda i edytuje dane pracowników
- Rejestruje nowych pracowników
- Generuje hasła tymczasowe
- **Nie może** zmieniać uprawnień ani wykonywać operacji RODO

### 🟡 Kierownik sprzedazy
Widok tylko do odczytu:
- Przegląda listę pracowników i ich szczegóły
- **Nie może** edytować danych, rejestrować, ani zarządzać uprawnieniami

### 🟢 Pracownik magazynu
Ograniczony dostęp:
- Ma dostęp wyłącznie do **własnego profilu**
- Może zmieniać własne hasło
- Nie widzi listy innych pracowników

### 🟢 Sprzedawca
Identyczny zakres jak Pracownik magazynu:
- Ma dostęp wyłącznie do **własnego profilu**
- Może zmieniać własne hasło

---

## Funkcje systemu

### Logowanie i bezpieczeństwo
- Logowanie przez formularz (`/Account/Login`)
- Blokada konta po 3 błędnych próbach logowania (15 minut)
- Hasła tymczasowe — wymagana zmiana przy pierwszym logowaniu
- Odzyskiwanie hasła przez e-mail (Gmail SMTP)

### Zarządzanie pracownikami
- Przeglądanie i wyszukiwanie pracowników (`/Uzytkownicy/AdminPanel`)
- Rejestracja nowych pracowników (`/Uzytkownicy/Rejestracja`)
- Edycja danych (`/Uzytkownicy/EditUser/{id}`)
- Szczegóły pracownika (`/Uzytkownicy/UserDetails/{id}`)

### RODO
- Anonimizacja użytkownika (`ForgetUser`) — usuwa dane osobowe
- Lista zapomnianych użytkowników (`/Uzytkownicy/ForgottenUsers`)

### Uprawnienia
- Lista uprawnień z filtrowaniem po rolach (`/Uprawnienia/Uprawnienia`)
- Nadawanie ról użytkownikom (`/Uprawnienia/SetRole`) — **tylko Administrator**

### Profil i hasło
- Mój profil (`/Account/MyProfile`) — widoczny dla wszystkich zalogowanych
- Zmiana hasła (`/Account/ChangePassword`) — dostępna dla wszystkich zalogowanych

---

## Konfiguracja e-mail

Aplikacja używa Gmail SMTP do wysyłania haseł tymczasowych:
- **Adres:** magazynfirma123321@gmail.com
- **Serwer:** smtp.gmail.com (port 587, TLS)

---

## Technologie

- ASP.NET Core MVC (.NET 10)
- SQLite (baza danych)
- Cookie Authentication
- HTML/CSS (własny dark-mode design)
