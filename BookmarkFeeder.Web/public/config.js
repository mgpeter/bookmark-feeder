// Overwritten at container start by docker-entrypoint.d/40-runtime-config.sh.
//
// Empty in development, where import.meta.env (.env.development) supplies the values instead.
// It exists as a committed file so a `vite build` always emits it and the <script> tag in
// index.html never 404s.
window.__BOOKMARKFEEDER_CONFIG__ = {}
