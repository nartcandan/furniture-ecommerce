// Fills an empty store with the demo catalog in catalog.json, through the admin API.
//
//   ADMIN_EMAIL=... ADMIN_PASSWORD=... node seed/seed.mjs [apiUrl]
//
// apiUrl defaults to http://localhost:5068. Going through the API means every product passes the same checks
// as one created in the admin panel. Photos are downloaded from Unsplash and Pexels at 1600 px and uploaded,
// so none are kept in the repository. A category or product that already exists by name is left as it is,
// so the script can be run again after a failure.

import { readFileSync } from 'node:fs'

const apiUrl = (process.argv[2] ?? 'http://localhost:5068').replace(/\/$/, '')
const { ADMIN_EMAIL: email, ADMIN_PASSWORD: password } = process.env
if (!email || !password) {
  console.error('Set ADMIN_EMAIL and ADMIN_PASSWORD to the admin account of the API.')
  process.exit(1)
}

const catalog = JSON.parse(readFileSync(new URL('./catalog.json', import.meta.url), 'utf8'))
const PHOTO_WIDTH = 1600

/** Where a photo is downloaded from. Its page, with the photographer, is unsplash.com/photos/{id} or pexels.com/photo/{id}. */
function photoUrl(ref) {
  const [source, id] = [ref.slice(0, 1), ref.slice(2)]
  if (source === 'u') return `https://unsplash.com/photos/${id}/download?w=${PHOTO_WIDTH}`
  if (source === 'p') return `https://images.pexels.com/photos/${id}/pexels-photo-${id}.jpeg?auto=compress&cs=tinysrgb&w=${PHOTO_WIDTH}`
  throw new Error(`Unknown photo reference ${ref}`)
}

let token = ''

async function api(path, { method = 'GET', body } = {}) {
  const isForm = body instanceof FormData
  const response = await fetch(`${apiUrl}${path}`, {
    method,
    headers: {
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(body !== undefined && !isForm ? { 'Content-Type': 'application/json' } : {}),
    },
    body: body === undefined ? undefined : isForm ? body : JSON.stringify(body),
  })
  const text = await response.text()
  if (!response.ok) {
    throw new Error(`${method} ${path} answered ${response.status}: ${text}`)
  }
  return text ? JSON.parse(text) : undefined
}

async function download(url) {
  for (let attempt = 1; ; attempt++) {
    const response = await fetch(url, { redirect: 'follow' })
    const type = response.headers.get('content-type') ?? ''
    if (response.ok && type.startsWith('image/')) {
      return new Blob([await response.arrayBuffer()], { type })
    }
    if (attempt === 3) {
      throw new Error(`Download failed with ${response.status} ${type}: ${url}`)
    }
    await new Promise((resolve) => setTimeout(resolve, 2000 * attempt))
  }
}

async function findProduct(name) {
  const page = await api(`/api/admin/products?search=${encodeURIComponent(name)}&pageSize=100`)
  return page.items.find((product) => product.name === name)
}

const login = await api('/api/auth/login', { method: 'POST', body: { email, password } })
token = login.accessToken

const existingCategories = await api('/api/admin/categories')
let created = 0
let skipped = 0
let photos = 0

// Categories first, then products one category after another, from the last product of each category to the
// first. The store lists the newest first, so the first product of each category (its showpiece) comes on top,
// and the home page's "New arrivals" mixes the rooms instead of showing four of the same.
const categoryIds = new Map()
for (const category of catalog.categories) {
  const id =
    existingCategories.find((existing) => existing.name === category.name)?.id ??
    (await api('/api/categories', {
      method: 'POST',
      body: { name: category.name, description: category.description },
    })).id
  categoryIds.set(category.name, id)
}

const longest = Math.max(...catalog.categories.map((category) => category.products.length))
for (let position = longest - 1; position >= 0; position--) {
  for (const category of catalog.categories) {
    const product = category.products[position]
    if (!product) continue

    if (await findProduct(product.name)) {
      console.log(`  = ${product.name} (already there)`)
      skipped++
      continue
    }

    const saved = await api('/api/products', {
      method: 'POST',
      body: {
        categoryId: categoryIds.get(category.name),
        name: product.name,
        description: product.description,
        price: product.price,
        stockQuantity: product.stock,
        widthCm: product.widthCm,
        depthCm: product.depthCm,
        heightCm: product.heightCm,
        weightKg: product.weightKg,
        material: product.material,
        color: product.color,
      },
    })

    // In the listed order, so the first photo becomes the main image.
    for (const [index, ref] of product.photos.entries()) {
      const form = new FormData()
      form.append('file', await download(photoUrl(ref)), `${saved.slug}-${index + 1}.jpg`)
      await api(`/api/products/${saved.id}/images`, { method: 'POST', body: form })
      photos++
    }

    console.log(`  + ${category.name}: ${product.name}, ${product.photos.length} photos`)
    created++
  }
}

console.log(`\nDone: ${created} products created with ${photos} photos, ${skipped} already there.`)
