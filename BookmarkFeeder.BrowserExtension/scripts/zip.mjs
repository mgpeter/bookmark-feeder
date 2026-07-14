// Packages dist/ into a store-uploadable zip named after the manifest version.
import { createWriteStream } from 'node:fs'
import { readFile, stat } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'
import archiver from 'archiver'

const root = new URL('../', import.meta.url)
const dist = new URL('dist/', root)

try {
  await stat(new URL('manifest.json', dist))
} catch {
  console.error('dist/ has no manifest.json — run `npm run build` first.')
  process.exit(1)
}

const { version, name } = JSON.parse(await readFile(new URL('manifest.json', dist), 'utf8'))
const zipName = `${name.toLowerCase().replace(/\s+/g, '-')}-${version}.zip`
const output = createWriteStream(fileURLToPath(new URL(zipName, root)))

const archive = archiver('zip', { zlib: { level: 9 } })
archive.on('warning', (err) => console.warn(err.message))
archive.on('error', (err) => {
  console.error(err.message)
  process.exit(1)
})

// Resolves once the bytes are actually flushed to disk, so the reported size is real.
const closed = new Promise((resolve, reject) => {
  output.on('close', resolve)
  output.on('error', reject)
})

archive.pipe(output)
// Zip the *contents* of dist/, not the folder: stores expect manifest.json at the root.
// fileURLToPath is required — URL.pathname yields "/D:/..." on Windows.
archive.directory(fileURLToPath(dist), false)
await archive.finalize()
await closed

console.log(`${zipName} (${(archive.pointer() / 1024).toFixed(1)} kB)`)
