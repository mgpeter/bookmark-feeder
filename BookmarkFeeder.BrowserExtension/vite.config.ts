import { defineConfig } from 'vite'
import { fileURLToPath, URL } from 'node:url'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { crx, type ManifestV3Export } from '@crxjs/vite-plugin'
import manifest from './manifest.json'

/**
 * Safari has no `bookmarks` API for extensions; the popup reads bookmarks through the wrapper
 * app's native handler instead (safari/), which needs `nativeMessaging`. Chrome and Edge must not
 * carry that permission, so `vite build --mode safari` derives a Safari manifest into dist-safari/.
 */
function safariManifest(): ManifestV3Export {
  return {
    ...manifest,
    permissions: [...manifest.permissions.filter((p) => p !== 'bookmarks'), 'nativeMessaging'],
  }
}

// manifest.json is the source of truth; @crxjs wires the popup/assets into the MV3 build.
export default defineConfig(({ mode }) => {
  const safari = mode === 'safari'
  return {
    plugins: [react(), tailwindcss(), crx({ manifest: safari ? safariManifest() : manifest })],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
      },
    },
    build: {
      outDir: safari ? 'dist-safari' : 'dist',
      emptyOutDir: true,
    },
  }
})
