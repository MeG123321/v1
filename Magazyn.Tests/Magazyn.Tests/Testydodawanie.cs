using Magazyn.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Xunit;

namespace Magazyn.Tests.Scenariusze
{
    public class DodawanieUzytkownikaTests
    {
        private UserRegistrationDto UtworzPoprawnegoUzytkownika()
        {
            return new UserRegistrationDto
            {
                Username = "testprac01",
                Password = "Test123!",
                FirstName = "Marek",
                LastName = "Nowak",
                Email = "marek.nowak01@example.com",
                Pesel = "85021412345",
                NrTelefonu = "600700800",
                Plec = "Mężczyzna",
                Status = "Aktywny",

                Rola = "Użytkownik",
                DataUrodzenia = new DateOnly(1985, 2, 14),
                Miejscowosc = "Poznań",
                KodPocztowy = "60-101",
                Ulica = "Długa",
                NrPosesji = "12A",
                NrLokalu = "5"
            };
        }

        private (bool wynikWalidacji, List<ValidationResult> listaBledowWalidacji) WalidujModel(object model)
        {
            var listaBledowWalidacji = new List<ValidationResult>();
            var kontekstWalidacji = new ValidationContext(model);
            bool wynikWalidacji = Validator.TryValidateObject(model, kontekstWalidacji, listaBledowWalidacji, validateAllProperties: true);
            return (wynikWalidacji, listaBledowWalidacji);
        }

        private static void SprawdzCzyZawieraBladDlaPola(List<ValidationResult> listaBledowWalidacji, string nazwaPola)
        {
            Assert.Contains(listaBledowWalidacji, blad => blad.MemberNames.Contains(nazwaPola));
        }

      
        [Fact]
        public void TC_1_DodanieUzytkownika_Sukces()
        {
            UserRegistrationDto poprawnyUzytkownik = UtworzPoprawnegoUzytkownika();
            var (wynikWalidacji, listaBledowWalidacji) = WalidujModel(poprawnyUzytkownik);

            Assert.True(wynikWalidacji);
            Assert.Empty(listaBledowWalidacji);
        }

      
        [Fact]
        public void TC_2_DodanieUzytkownika_BezPolOpcjonalnych()
        {
            UserRegistrationDto poprawnyUzytkownik = UtworzPoprawnegoUzytkownika();
            poprawnyUzytkownik.Ulica = "";
            poprawnyUzytkownik.NrLokalu = "";

            var (wynikWalidacji, listaBledowWalidacji) = WalidujModel(poprawnyUzytkownik);

            Assert.True(wynikWalidacji);
            Assert.Empty(listaBledowWalidacji);
        }

      
        [Fact]
        public void TC_3_AnulowanieOperacji_WalidacjaPoprawna()
        {
            UserRegistrationDto poprawnyUzytkownik = UtworzPoprawnegoUzytkownika();
            var (wynikWalidacji, listaBledowWalidacji) = WalidujModel(poprawnyUzytkownik);

            Assert.True(wynikWalidacji);
            Assert.Empty(listaBledowWalidacji);
        }

      
        [Fact]
        public void TC_4_WszystkiePolaWymaganePuste()
        {
            UserRegistrationDto pustyUzytkownik = new UserRegistrationDto();

            var (wynikWalidacji, listaBledowWalidacji) = WalidujModel(pustyUzytkownik);

            Assert.False(wynikWalidacji);
            Assert.NotEmpty(listaBledowWalidacji);
        }

        
        [Fact]
        public void TC_5_PustyLogin()
        {
            UserRegistrationDto uzytkownik = UtworzPoprawnegoUzytkownika();
            uzytkownik.Username = "";

            var (wynikWalidacji, listaBledowWalidacji) = WalidujModel(uzytkownik);

            Assert.False(wynikWalidacji);
            SprawdzCzyZawieraBladDlaPola(listaBledowWalidacji, nameof(UserRegistrationDto.Username));
        }

        
        [Fact]
        public void TC_6_PustyEmail()
        {
            UserRegistrationDto uzytkownik = UtworzPoprawnegoUzytkownika();
            uzytkownik.Email = "";

            var (wynikWalidacji, listaBledowWalidacji) = WalidujModel(uzytkownik);

            Assert.False(wynikWalidacji);
            SprawdzCzyZawieraBladDlaPola(listaBledowWalidacji, nameof(UserRegistrationDto.Email));
        }

        
        [Fact]
        public void TC_7_PustyPesel()
        {
            UserRegistrationDto uzytkownik = UtworzPoprawnegoUzytkownika();
            uzytkownik.Pesel = "";

            var (wynikWalidacji, listaBledowWalidacji) = WalidujModel(uzytkownik);

            Assert.False(wynikWalidacji);
            SprawdzCzyZawieraBladDlaPola(listaBledowWalidacji, nameof(UserRegistrationDto.Pesel));
        }

       
        [Fact]
        public void TC_8_PustaDataUrodzenia()
        {
            UserRegistrationDto uzytkownik = UtworzPoprawnegoUzytkownika();
            uzytkownik.DataUrodzenia = null;

            var (wynikWalidacji, listaBledowWalidacji) = WalidujModel(uzytkownik);

            Assert.False(wynikWalidacji);
            SprawdzCzyZawieraBladDlaPola(listaBledowWalidacji, nameof(UserRegistrationDto.DataUrodzenia));
        }

        
        [Fact]
        public void TC_9_PustyTelefon()
        {
            UserRegistrationDto uzytkownik = UtworzPoprawnegoUzytkownika();
            uzytkownik.NrTelefonu = "";

            var (wynikWalidacji, listaBledowWalidacji) = WalidujModel(uzytkownik);

            Assert.False(wynikWalidacji);
            SprawdzCzyZawieraBladDlaPola(listaBledowWalidacji, nameof(UserRegistrationDto.NrTelefonu));
        }

        
        [Fact]
        public void TC_10_LoginZaKrotki()
        {
            UserRegistrationDto uzytkownik = UtworzPoprawnegoUzytkownika();
            uzytkownik.Username = "abc";

            var (wynikWalidacji, listaBledowWalidacji) = WalidujModel(uzytkownik);

            Assert.False(wynikWalidacji);
            SprawdzCzyZawieraBladDlaPola(listaBledowWalidacji, nameof(UserRegistrationDto.Username));
        }

      
        [Fact]
        public void TC_11_HasloZaKrotkie()
        {
            UserRegistrationDto uzytkownik = UtworzPoprawnegoUzytkownika();
            uzytkownik.Password = "Ab1!";

            var (wynikWalidacji, listaBledowWalidacji) = WalidujModel(uzytkownik);

            Assert.False(wynikWalidacji);
            SprawdzCzyZawieraBladDlaPola(listaBledowWalidacji, nameof(UserRegistrationDto.Password));
        }

        
        [Fact]
        public void TC_12_HasloBezZnakuSpecjalnego()
        {
            UserRegistrationDto uzytkownik = UtworzPoprawnegoUzytkownika();
            uzytkownik.Password = "Test1234";

            var (wynikWalidacji, listaBledowWalidacji) = WalidujModel(uzytkownik);

            Assert.False(wynikWalidacji);
            SprawdzCzyZawieraBladDlaPola(listaBledowWalidacji, nameof(UserRegistrationDto.Password));
        }

        
        [Fact]
        public void TC_13_EmailBezMalpy()
        {
            UserRegistrationDto uzytkownik = UtworzPoprawnegoUzytkownika();
            uzytkownik.Email = "marek.nowak01example.com";

            var (wynikWalidacji, listaBledowWalidacji) = WalidujModel(uzytkownik);

            Assert.False(wynikWalidacji);
            SprawdzCzyZawieraBladDlaPola(listaBledowWalidacji, nameof(UserRegistrationDto.Email));
        }

       
        [Fact]
        public void TC_14_PeselZLitera()
        {
            UserRegistrationDto uzytkownik = UtworzPoprawnegoUzytkownika();
            uzytkownik.Pesel = "85021412A4";

            var (wynikWalidacji, listaBledowWalidacji) = WalidujModel(uzytkownik);

            Assert.False(wynikWalidacji);
            SprawdzCzyZawieraBladDlaPola(listaBledowWalidacji, nameof(UserRegistrationDto.Pesel));
        }

        
        [Fact]
        public void TC_15_TelefonZaKrotki()
        {
            UserRegistrationDto uzytkownik = UtworzPoprawnegoUzytkownika();
            uzytkownik.NrTelefonu = "60070080";

            var (wynikWalidacji, listaBledowWalidacji) = WalidujModel(uzytkownik);

            Assert.False(wynikWalidacji);
            SprawdzCzyZawieraBladDlaPola(listaBledowWalidacji, nameof(UserRegistrationDto.NrTelefonu));
        }

      
        [Fact]
        public void TC_16_KodPocztowyZlyFormat()
        {
            UserRegistrationDto uzytkownik = UtworzPoprawnegoUzytkownika();
            uzytkownik.KodPocztowy = "60101";

            var (wynikWalidacji, listaBledowWalidacji) = WalidujModel(uzytkownik);

            Assert.False(wynikWalidacji);
            SprawdzCzyZawieraBladDlaPola(listaBledowWalidacji, nameof(UserRegistrationDto.KodPocztowy));
        }
        [Fact]
        public void TC_17_Login_ZawieraSpacje_Blad()
        {
            var uzytkownik = UtworzPoprawnegoUzytkownika();
            uzytkownik.Username = "test user";

            var (wynik, bledy) = WalidujModel(uzytkownik);

            Assert.False(wynik);
            SprawdzCzyZawieraBladDlaPola(bledy, nameof(UserRegistrationDto.Username));
        }

        [Fact]
        public void TC_18_Pesel_ZaDlugi_Blad()
        {
            var uzytkownik = UtworzPoprawnegoUzytkownika();
            uzytkownik.Pesel = "850101123456";

            var (wynik, bledy) = WalidujModel(uzytkownik);

            Assert.False(wynik);
            SprawdzCzyZawieraBladDlaPola(bledy, nameof(UserRegistrationDto.Pesel));
        }

        [Fact]
        public void TC_19_Telefon_ZaDlugi_Blad()
        {
            var uzytkownik = UtworzPoprawnegoUzytkownika();
            uzytkownik.NrTelefonu = "123456789123";

            var (wynik, bledy) = WalidujModel(uzytkownik);

            Assert.False(wynik);
            SprawdzCzyZawieraBladDlaPola(bledy, nameof(UserRegistrationDto.NrTelefonu));
        }

        [Fact]
        public void TC_20_KodPocztowy_Litery_Blad()
        {
            var uzytkownik = UtworzPoprawnegoUzytkownika();
            uzytkownik.KodPocztowy = "AA-AAA";

            var (wynik, bledy) = WalidujModel(uzytkownik);

            Assert.False(wynik);
            SprawdzCzyZawieraBladDlaPola(bledy, nameof(UserRegistrationDto.KodPocztowy));
        }

       

        [Fact]
        public void TC_21_Haslo_BrakMalejLitery_Blad()
        {
            var uzytkownik = UtworzPoprawnegoUzytkownika();
            uzytkownik.Password = "TEST123!";

            var (wynik, bledy) = WalidujModel(uzytkownik);

            Assert.False(wynik);
            SprawdzCzyZawieraBladDlaPola(bledy, nameof(UserRegistrationDto.Password));
        }

        [Fact]
        public void TC_22_Haslo_BrakWielkiejLitery_Blad()
        {
            var uzytkownik = UtworzPoprawnegoUzytkownika();
            uzytkownik.Password = "test123!";

            var (wynik, bledy) = WalidujModel(uzytkownik);

            Assert.False(wynik);
            SprawdzCzyZawieraBladDlaPola(bledy, nameof(UserRegistrationDto.Password));
        }

        [Fact]
        public void TC_23_Haslo_BrakZnakuSpecjalnego_Blad()
        {
            var uzytkownik = UtworzPoprawnegoUzytkownika();
            uzytkownik.Password = "Test1234";

            var (wynik, bledy) = WalidujModel(uzytkownik);

            Assert.False(wynik);
            SprawdzCzyZawieraBladDlaPola(bledy, nameof(UserRegistrationDto.Password));
        }
    }
}
