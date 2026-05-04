function goBackReal(linkElement) {
    const defaultUrl = linkElement.getAttribute('data-default-url') || '/';

    if (document.referrer && document.referrer.indexOf(window.location.host) !== -1) {
        window.location.href = document.referrer;
    } else {
        window.location.href = defaultUrl;
    }
}
