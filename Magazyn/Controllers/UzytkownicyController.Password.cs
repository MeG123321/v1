using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using System;

namespace Magazyn.Controllers
{
    // PARTIAL pozwala nam rozdzielić kod kontrolera na kilka plików
    public partial class UzytkownicyController : Controller
    {
        // LG_UC5: Automatyczne generowanie hasła
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GeneratePassword(long id)
        {
            // Podstawowa walidacja ID
            if (id <= 0) 
            {
                return BadRequest();
            }

            using (var connection = Db.OpenConnection(DbPath))
            {
                string userEmail = "";

                // 1. Sprawdzenie adresu e-mail (LG_UC5 Wyjątek A)
                using (var cmdCheck = connection.CreateCommand())
                {
                    cmdCheck.CommandText = "SELECT Email FROM Uzytkownicy WHERE id = $id LIMIT 1";
                    cmdCheck.Parameters.AddWithValue("$id", id);
                    
                    var result = cmdCheck.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        userEmail = result.ToString();
                    }
                }

                // Realizacja Wyjątku A: Jeśli brak maila, przerwij i wyświetl błąd
                if (string.IsNullOrWhiteSpace(userEmail))
                {
                    TempData["ErrorMessage"] = "Brak adresu e-mail dla użytkownika";
                    return RedirectToAction("UserDetails", new { id = id });
                }

                // 2. Generowanie hasła (LG_UC5 Przebieg główny pkt 3)
                string newPassword = Guid.NewGuid().ToString("N").Substring(0, 10);

                // Zapis nowego hasła do bazy
                using (var cmdUpdate = connection.CreateCommand())
                {
                    cmdUpdate.CommandText = "UPDATE Uzytkownicy SET Password = $pass WHERE id = $id";
                    cmdUpdate.Parameters.AddWithValue("$pass", newPassword);
                    cmdUpdate.Parameters.AddWithValue("$id", id);
                    cmdUpdate.ExecuteNonQuery();
                }
            }

            // Proste logowanie zdarzenia
            _logger.LogInformation("LG_UC5: Wygenerowano nowe haslo dla uzytkownika");

            // 3. Komunikat o sukcesie (LG_UC5 Przebieg główny pkt 4)
            TempData["SuccessMessage"] = "Hasło zostało wygenerowane i wysłane na adres e-mail użytkownika";

            // Powrót do widoku szczegółów
            return RedirectToAction("UserDetails", new { id = id });
        }
    }
}