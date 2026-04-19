// wwwroot/js/script.js

function goBackReal(element) {
    // Pobieramy domyślny URL z atrybutu data-default-url przypisanego do linku
    const defaultUrl = element.getAttribute('data-default-url') || '/';

    // Sprawdzamy, czy referrer istnieje i czy prowadzi do naszej domeny
    // (żeby nie cofnęło nas np. do Google, jeśli ktoś tamtędy trafił)
    if (document.referrer && document.referrer.indexOf(window.location.host) !== -1) {
        window.location.href = document.referrer;
    } else {
        window.location.href = defaultUrl;
    }
}