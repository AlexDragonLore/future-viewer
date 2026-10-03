import { createHash } from 'node:crypto'
import { glob, readFile, realpath, stat } from 'node:fs/promises'
import path from 'node:path'

const hasText = value => typeof value === 'string' && value.trim().length > 0
const isRepoPath = value => hasText(value) && !path.isAbsolute(value)
  && !value.includes('\\') && !value.split('/').some(part => part === '..' || part === '.')
const isRussianLocation = value => /^(?:RU|RUS|Россия|Российская Федерация)$/i.test(value?.trim() ?? '')

// This project deliberately keeps primary data AND backups in Russia. This is
// a release policy; it is not a claim that every foreign processor is unlawful.
export function validatePrimaryLocations(providers) {
  const errors = []
  for (const name of ['Production Docker host', 'Primary PostgreSQL', 'Backup storage']) {
    const provider = providers.find(item => item.provider_name === name)
    if (!provider?.enabled) continue
    if (!isRussianLocation(provider.country)) errors.push(`${name}: primary data/backup location must be verified in Russia (RU)`)
    if (provider.cross_border_transfer !== false) errors.push(`${name}: primary data/backup location must not imply a cross-border transfer`)
  }
  return errors
}

async function repoFile(root, relative) {
  if (!isRepoPath(relative)) throw new Error('expected a repository-relative file path without traversal')
  const absolute = await realpath(path.join(root, relative))
  if (!absolute.startsWith(`${await realpath(root)}${path.sep}`)) throw new Error('file resolves outside the repository')
  if (!(await stat(absolute)).isFile()) throw new Error('expected a regular file')
  return absolute
}

// An approval is bound to exact bytes and to an archived evidence file, not
// merely to a non-empty path string in assets.json.
export async function validateAssetEvidence(root, assets) {
  const errors = []
  for (const asset of assets ?? []) {
    if (!asset?.shipped || !asset.manual_approved) continue
    const label = `asset ${asset.asset_id}`
    try {
      const patterns = asset.path_glob?.split(';').map(value => value.trim()) ?? []
      if (!patterns.length || patterns.some(pattern => !isRepoPath(pattern))) throw new Error('invalid path_glob')
      const actual = new Set()
      for await (const relative of glob(patterns, {
        cwd: root,
        exclude: ['**/node_modules/**', '**/bin/**', '**/obj/**', '**/dist/**', '.git/**'],
      })) {
        if ((await stat(path.join(root, relative))).isFile()) actual.add(relative.split(path.sep).join('/'))
      }
      if (!actual.size) throw new Error('path_glob matches no shipped files')
      const manifest = JSON.parse(await readFile(await repoFile(root, asset.sha256_manifest), 'utf8'))
      if (manifest.schema_version !== 1 || manifest.asset_id !== asset.asset_id || !Array.isArray(manifest.files)) {
        throw new Error('manifest must have schema_version=1, matching asset_id and files array')
      }
      const seen = new Set()
      for (const file of manifest.files) {
        if (!file || !isRepoPath(file.path) || seen.has(file.path)) throw new Error('manifest contains an invalid or duplicate file path')
        seen.add(file.path)
        if (!actual.has(file.path)) throw new Error(`manifest contains a file outside path_glob: ${file.path}`)
        for (const field of ['author', 'source_reference', 'license_spdx_or_public_domain_basis', 'evidence_path', 'approved_by', 'approved_at']) {
          if (!hasText(file[field])) throw new Error(`${file.path}: missing ${field}`)
        }
        if (file.commercial_use !== true) throw new Error(`${file.path}: commercial use is not approved`)
        const approvalTime = Date.parse(file.approved_at)
        if (!/^\d{4}-\d{2}-\d{2}(?:T.*)?$/.test(file.approved_at) || !Number.isFinite(approvalTime) || approvalTime > Date.now()) {
          throw new Error(`${file.path}: invalid or future approved_at`)
        }
        if (!/^[a-f0-9]{64}$/i.test(file.sha256 ?? '')) throw new Error(`${file.path}: invalid sha256`)
        const bytes = await readFile(await repoFile(root, file.path))
        if (createHash('sha256').update(bytes).digest('hex') !== file.sha256.toLowerCase()) throw new Error(`${file.path}: SHA-256 mismatch; new review required`)
        const evidence = await readFile(await repoFile(root, file.evidence_path))
        if (!evidence.length) throw new Error(`${file.path}: archived evidence is empty`)
      }
      const missing = [...actual].filter(file => !seen.has(file))
      if (missing.length) throw new Error(`manifest omits shipped files: ${missing.join(', ')}`)
    } catch (error) {
      errors.push(`${label}: ${error.message}`)
    }
  }
  return errors
}
