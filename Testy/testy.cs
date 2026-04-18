using Xunit;
using Magazyn.Controllers;
using Magazyn.Models;
using Magazyn.Data;
using Magazyn.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Magazyn.Tests.Controllers
{
    public class RejestracjaUzytkownikaTests
    {
        private MagazynDbContext GetContext()
        {
            var options = new DbContextOptionsBuilder<MagazynDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new MagazynDbContext(options);
        }

        private UserRegistrationDto WypelnijModelPoprawnymiDanymi()
        {
            return new UserRegistrationDto
            {
                Username = "testprac01",
                Password = "Test123!", // Musi spełniać: mała, duża, cyfra, spec
                FirstName = "Marek",
                LastName = "Nowak",
                Email = "marek.nowak01@example.com",
                Pesel = "85021412345",
                NrTelefonu = "600700800",
                Plec = "Mężczyzna",
                Status = "Aktywny",
                Rola = "Użytkownik",
                DataUrodzenia = new DateOnly(1985, 2, 14), // Zmiana na DateOnly zgodnie z DTO
                Miejscowosc = "Poznań",
                KodPocztowy = "60-101",
                Ulica = "Długa",
                NrPosesji = "12A",
                NrLokalu = "5"
            };
        }

        [Fact]
        public void TC_01_DodanieUzytkownika_Sukces()
        {
            // Arrange
            using var context = GetContext();
            var controller = new UzytkownicyController(context);

            var httpContext = new DefaultHttpContext();
            controller.TempData = new TempDataDictionary(httpContext, new LocalFakeTempDataProvider());

            var model = WypelnijModelPoprawnymiDanymi();

            // Act
            var result = controller.Register(model) as RedirectToActionResult;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, context.Uzytkownicy.Count());
            Assert.Equal("testprac01", context.Uzytkownicy.First().Username);
        }

        [Fact]
        public void TC_02_Rejestracja_ZbytKrotkiLogin_Blad()
        {
            // Arrange
            using var context = GetContext();
            var controller = new UzytkownicyController(context);
            var model = WypelnijModelPoprawnymiDanymi();
            model.Username = "jan"; // < 5 znaków

            controller.ModelState.AddModelError("Username", "Nazwa użytkownika musi mieć od 5 do 20 znaków");

            // Act
            var result = controller.Register(model);

            // Assert
            Assert.False(controller.ModelState.IsValid);
            Assert.Equal("Nazwa użytkownika musi mieć od 5 do 20 znaków",
                controller.ModelState["Username"]!.Errors[0].ErrorMessage);
        }

        [Fact]
        public void TC_03_Haslo_BrakZnakuSpecjalnego_Blad()
        {
            // Arrange
            using var context = GetContext();
            var controller = new UzytkownicyController(context);
            var model = WypelnijModelPoprawnymiDanymi();
            model.Password = "Haslo123"; // Brak znaku specjalnego

            controller.ModelState.AddModelError("Password", "Hasło musi zawierać: małą literę, dużą literę, cyfrę oraz znak specjalny");

            // Act
            var result = controller.Register(model);

            // Assert
            Assert.False(controller.ModelState.IsValid);
            Assert.Contains("znak specjalny", controller.ModelState["Password"]!.Errors[0].ErrorMessage);
        }

        [Fact]
        public void TC_06_Pesel_ZlyFormat_Blad()
        {
            // Arrange
            using var context = GetContext();
            var controller = new UzytkownicyController(context);
            var model = WypelnijModelPoprawnymiDanymi();
            model.Pesel = "123"; // Za krótki

            controller.ModelState.AddModelError("Pesel", "PESEL musi składać się z 11 cyfr");

            // Act
            var result = controller.Register(model);

            // Assert
            Assert.False(controller.ModelState.IsValid);
            Assert.Equal("PESEL musi składać się z 11 cyfr", controller.ModelState["Pesel"]!.Errors[0].ErrorMessage);
        }

        [Fact]
        public void TC_09_KodPocztowy_ZlyFormat_Blad()
        {
            // Arrange
            using var context = GetContext();
            var controller = new UzytkownicyController(context);
            var model = WypelnijModelPoprawnymiDanymi();
            model.KodPocztowy = "60101"; // Brak myślnika

            controller.ModelState.AddModelError("KodPocztowy", "Kod pocztowy w formacie 00-000");

            // Act
            var result = controller.Register(model);

            // Assert
            Assert.False(controller.ModelState.IsValid);
            Assert.Equal("Kod pocztowy w formacie 00-000", controller.ModelState["KodPocztowy"]!.Errors[0].ErrorMessage);
        }

        // Pomocnicza klasa dla TempData
        private class LocalFakeTempDataProvider : ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
            public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
        }
    }
}