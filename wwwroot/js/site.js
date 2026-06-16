// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

(() => {
    const cookieName = 'cmiforge.timezone';
    const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
    if (!timeZone) {
        return;
    }

    const existing = document.cookie
        .split(';')
        .map((part) => part.trim())
        .find((part) => part.startsWith(`${cookieName}=`));
    const existingValue = existing ? decodeURIComponent(existing.substring(cookieName.length + 1)) : '';

    if (existingValue !== timeZone) {
        const maxAge = 60 * 60 * 24 * 365;
        document.cookie = `${cookieName}=${encodeURIComponent(timeZone)}; path=/; max-age=${maxAge}; samesite=lax`;
        if (!sessionStorage.getItem('cmiforge.timezoneReloaded')) {
            sessionStorage.setItem('cmiforge.timezoneReloaded', 'true');
            window.location.reload();
        }
    }
})();
