# Magazyn GiTA – System Zarządzania Pracownikami i Magazynem

Aplikacja webowa ASP.NET Core MVC do zarządzania personelem, uprawnieniami oraz gospodarką magazynową w firmie.

---

## System uprawnień (Role)

Aplikacja posiada **5 ról**, które określają zakres dostępu do funkcji systemu:

| Rola | Opis |
|------|------|
| **Administrator** | Pełny dostęp do wszystkich funkcji |
| **Kierownik magazynu** | Dostęp do funkcji magazynu oraz zarządzania pracownikami (bez RODO i uprawnień) |
| **Kierownik sprzedazy** | Podgląd pracowników (tylko odczyt) |
| **Pracownik magazynu** | Operacje magazynowe (rejestracja towaru, przegląd stanów); dostęp do własnego profilu |
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
| **MAG: Rejestracja towaru (MAG-UC1)** | ✅ | ✅ | ❌ | ✅ | ❌ |
| **MAG: Stany magazynowe / wyszukiwanie (MAG-UC2)** | ✅ | ✅ | ❌ | ✅ | ❌ |
| **MAG: Historia stanów (MAG-UC3)** | ✅ | ✅ | ❌ | ❌ | ❌ |
| **MAG: Szczegóły rejestracji (MAG-UC4)** | ✅ | ✅ | ❌ | ❌ | ❌ |
| **MAG: Zmiana stawki VAT (MAG-UC5)** | ✅ | ✅ | ❌ | ❌ | ❌ |
| **MAG: Słowniki – rodzaje towarów (MAG-UC6)** | ✅ | ✅ | ❌ | ❌ | ❌ |

---

## Opis ról

### Administrator
Posiada pełny dostęp do systemu:
- Zarządza wszystkimi pracownikami (dodawanie, edycja, podgląd)
- Może zapomnieć użytkownika zgodnie z RODO
- Może nadawać i odbierać uprawnienia (role) innym użytkownikom
- Widzi listę użytkowników zapomnianych (RODO)

### Kierownik magazynu
Zarządza pracownikami i funkcjami magazynowymi:
- Przegląda i edytuje dane pracowników
- Rejestruje nowych pracowników
- Generuje hasła tymczasowe
- Dostęp do: rejestracji towaru, podglądu stanów, historii, szczegółów rejestracji, zmiany VAT i słowników
- **Nie może** zmieniać uprawnień ani wykonywać operacji RODO

### Kierownik sprzedazy
Widok tylko do odczytu:
- Przegląda listę pracowników i ich szczegóły
- **Nie może** edytować danych, rejestrować, ani zarządzać uprawnieniami

### Pracownik magazynu
Ograniczony dostęp + operacje magazynowe:
- Rejestruje nowy towar (MAG-UC1)
- Przegląda i wyszukuje stany magazynowe (MAG-UC2)
- Ma dostęp do **własnego profilu** i może zmieniać hasło

### Sprzedawca
Identyczny zakres jak Pracownik magazynu w obszarze profilu:
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

## Moduł: Zarządzanie magazynem

### MAG-UC1 — Rejestracja towaru
**Aktor:** Pracownik magazynu

**Cel:** Zarejestrowanie nowego towaru w magazynie i aktualizacja stanów.

**Dane wejściowe:**
- Nazwa towaru (wymagana)
- Rodzaj towaru (wymagany, wybór z listy)
- Jednostka miary (wymagana, wybór z listy)
- Ilość (wymagana)
- Cena netto (wymagana)
- Stawka VAT (wymagana, wybór z listy)
- Opis (opcjonalny)
- Dostawca (nazwa firmy)
- Data dostawy

**Walidacje:**
- wszystkie pola wymagane muszą być uzupełnione
- ilość > 0
- cena netto > 0
- poprawne formaty liczbowe

**Efekt:**
- zapis rejestracji z automatyczną datą rejestracji
- automatyczne przypisanie pracownika (z sesji)
- aktualizacja stanu magazynowego o wprowadzoną ilość

Komunikaty:
- Sukces: „Towar został poprawnie zarejestrowany”
- Błąd: „Wszystkie pola oznaczone jako wymagane muszą zostać uzupełnione”
- Błąd: „Wprowadzone dane są niepoprawne”

### MAG-UC2 — Wyszukiwanie towaru i przegląd stanów
**Aktor:** Pracownik magazynu, Kierownik magazynu

**Cel:** Wyszukanie towarów i sprawdzenie stanów magazynowych.

**Kryteria wyszukiwania:**
- Nazwa towaru
- Rodzaj towaru
- Imię i nazwisko osoby rejestrującej
- Data stanu (tylko Kierownik)

Scenariusze:
- brak kryteriów → lista wszystkich towarów
- brak wyników → komunikat: „Nie znaleziono towarów spełniających podane kryteria”

**Wynik:** lista (nazwa, rodzaj, stan magazynowy, data stanu).

### MAG-UC3 — Przegląd historii stanów
**Aktor:** Kierownik magazynu

**Cel:** Przegląd historii zmian stanu dla wybranego towaru.

**Filtry:**
- zakres dat (od–do)
- pracownik (z listy)

Komunikaty:
- brak historii → „Brak historii dla wybranego towaru w podanym zakresie”

**Wynik:** lista operacji (data rejestracji, pracownik, ilość).

### MAG-UC4 — Podgląd szczegółów rejestracji
**Aktor:** Kierownik magazynu

**Cel:** Podgląd pełnych danych konkretnej rejestracji.

**Zakres danych:** nazwa, rodzaj, JM, ilość, cena netto, VAT, opis, dostawca, data dostawy, data rejestracji, pracownik.

Komunikat:
- brak danych → „Nie znaleziono szczegółowych danych dla wybranego towaru”

### MAG-UC5 — Zmiana stawki VAT
**Aktor:** Kierownik magazynu

**Cel:** Zaplanowanie zmiany VAT dla towaru lub dla całego rodzaju towaru.

**Dane wejściowe:**
- Nowa stawka VAT (wymagana)
- Data obowiązywania (wymagana; musi być w przyszłości)

Komunikaty:
- sukces: „Stawka VAT została zaktualizowana i będzie obowiązywać od dnia [Data]”
- błąd: „Data obowiązywania musi być datą przyszłą”

### MAG-UC6 — Zarządzanie listą rodzajów towarów
**Aktor:** Kierownik magazynu

**Cel:** Dodanie/edycja/usunięcie rodzaju towaru (słownik).

Walidacje:
- nazwa niepusta
- brak duplikatu

Komunikaty:
- sukces: „Nowy rodzaj towaru został dodany”
- duplikat: „Podany rodzaj towaru już znajduje się w systemie”

---

## Proponowany model danych (baza SQLite)

Poniżej propozycja tabel/kolumn do realizacji modułu magazynowego (MAG-UC1..UC6). Nazwy możesz dostosować do konwencji projektu.

### 1) Slowniki (tabele referencyjne)

#### `TowarRodzaje` (MAG-UC6)
- `Id` (PK)
- `Nazwa` (TEXT, UNIQUE, NOT NULL)
- `CzyAktywny` (BOOLEAN/INTEGER, NOT NULL, domyślnie 1)
- `CreatedAt` (DATETIME)

#### `JednostkiMiary`
- `Id` (PK)
- `Nazwa` (TEXT, UNIQUE, NOT NULL) — np. szt, kg, l
- `Skrot` (TEXT) — opcjonalnie
- `CzyAktywny` (BOOLEAN/INTEGER)

#### `StawkiVat`
- `Id` (PK)
- `Nazwa` (TEXT) — np. "23%"
- `Wartosc` (DECIMAL, NOT NULL) — np. 0.23
- `CzyAktywny` (BOOLEAN/INTEGER)

> Alternatywnie VAT możesz trzymać jako `INTEGER` (np. 23) zamiast `DECIMAL`.

### 2) Encje magazynowe

#### `Towary`
- `Id` (PK)
- `Nazwa` (TEXT, NOT NULL)
- `RodzajId` (FK -> `TowarRodzaje.Id`, NOT NULL)
- `JednostkaMiaryId` (FK -> `JednostkiMiary.Id`, NOT NULL)
- `AktualnaStawkaVatId` (FK -> `StawkiVat.Id`, NOT NULL)
- `AktualnaIlosc` (DECIMAL, NOT NULL, domyślnie 0)
- `Opis` (TEXT, NULL)
- `CzyAktywny` (BOOLEAN/INTEGER, NOT NULL, domyślnie 1)

> `AktualnaIlosc` umożliwia szybki podgląd bieżącego stanu (MAG-UC2). Historie trzymasz w tabeli ruchów.

#### `RejestracjeTowaru` (MAG-UC1, MAG-UC4)
Każde „Zapisz” w formularzu tworzy nowy rekord rejestracji.
- `Id` (PK)
- `TowarId` (FK -> `Towary.Id`, NOT NULL)
- `Ilosc` (DECIMAL, NOT NULL)
- `CenaNetto` (DECIMAL(18,2), NOT NULL)
- `StawkaVatId` (FK -> `StawkiVat.Id`, NOT NULL) — VAT użyty w tej rejestracji
- `Dostawca` (TEXT, NULL)
- `DataDostawy` (DATETIME, NULL)
- `DataRejestracji` (DATETIME, NOT NULL) — automatycznie
- `RejestrujacyUserId` (TEXT/UUID, FK -> tabela użytkowników, NOT NULL)

> Dodatkowo możesz przechować `RejestrujacyImieNazwisko` jako denormalizację, ale lepiej wyliczać z użytkownika.

#### `RuchyMagazynowe` / `HistoriaStanow` (MAG-UC3)
Jeżeli chcesz mieć uniwersalną historię zmian (przyjęcia/rozchody), dodaj tabelę ruchów.
- `Id` (PK)
- `TowarId` (FK -> `Towary.Id`, NOT NULL)
- `TypRuchu` (TEXT/INTEGER, NOT NULL) — np. "PRZYJECIE", "WYDANIE", "KOREKTA"
- `Ilosc` (DECIMAL, NOT NULL) — dodatnia dla przyjęć, ujemna dla wydań albo zawsze dodatnia + typ
- `DataOperacji` (DATETIME, NOT NULL)
- `UserId` (FK -> użytkownicy, NOT NULL)
- `RejestracjaTowaruId` (FK -> `RejestracjeTowaru.Id`, NULL) — jeśli ruch pochodzi z UC1
- `Komentarz` (TEXT, NULL)

> Jeśli Twoja aplikacja robi tylko przyjęcia (UC1), to historia może być budowana tylko z `RejestracjeTowaru`. Tabela `RuchyMagazynowe` daje większą elastyczność.

### 3) Planowanie zmiany VAT (MAG-UC5)

#### `PlanowaneZmianyVat`
- `Id` (PK)
- `Zakres` (TEXT/INTEGER, NOT NULL) — "TOWAR" lub "RODZAJ"
- `TowarId` (FK -> `Towary.Id`, NULL)
- `RodzajId` (FK -> `TowarRodzaje.Id`, NULL)
- `NowaStawkaVatId` (FK -> `StawkiVat.Id`, NOT NULL)
- `DataObowiazywania` (DATETIME, NOT NULL) — musi być w przyszłości
- `CreatedAt` (DATETIME, NOT NULL)
- `CreatedByUserId` (FK -> użytkownicy, NOT NULL)

Indeksy/ograniczenia (ważne):
- CHECK: dokładnie jedno z (`TowarId`, `RodzajId`) ustawione zgodnie z `Zakres`
- indeks na `DataObowiazywania`

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
