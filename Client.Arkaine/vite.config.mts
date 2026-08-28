import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

/*
 * Every root path the API owns. The SPA uses hash routing, so the dev server only ever
 * serves '/' for navigation and none of these can collide with a client route. The
 * trailing ([/?]|$) keeps public assets such as /favourite.png out of the proxy while
 * still matching query strings like /login?ReturnUrl=%2Floggedin.
 */
const apiPaths = [
    'error',
    'favourite',
    'favourites',
    'files',
    'forbidden',
    'loggedin',
    'login',
    'logout',
    'passkeys',
    'preview',
    'profile',
    'progress',
    'admin',
    'status',
    'stream',
    'tags',
    'twofactorauth',
    'updates'
]

/*
 * In development Vite serves the client from its own origin while the API runs as a
 * separate process, so calling the API directly makes every request cross-origin. That
 * needs CORS, SameSite=None cookies and a trusted TLS certificate. Firefox keeps its own
 * certificate store and never trusts the ASP.NET Core development certificate, so the
 * handshake fails before any CORS header is read and the browser reports it as a CORS
 * error. Proxying keeps the browser on a single origin, matching production where the
 * server hosts the built client.
 *
 * Aspire supplies ARKAINE_SERVER_URL; the fallback is the API's default dev launch URL
 * for anyone running `yarn dev` on its own.
 */
const serverUrl = process.env.ARKAINE_SERVER_URL || 'http://localhost:53613'

export default defineConfig({
    plugins: [vue()],
    server: {
        host: '0.0.0.0',
        port: Number(process.env.PORT) || 8081,
        strictPort: true,
        proxy: {
            [`^/(${apiPaths.join('|')})([/?]|$)`]: {
                target: serverUrl,
                changeOrigin: false,
                // Tolerate the self-signed dev certificate if the target is https.
                secure: false,
                ws: true
            }
        }
    },
    resolve: {
        alias: {
            '@': fileURLToPath(new URL('./src', import.meta.url))
        }
    }
})
