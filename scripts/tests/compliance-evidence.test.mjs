import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { mkdtemp, mkdir, rm, symlink, writeFile } from 'node:fs/promises'
import os from 'node:os'
import path from 'node:path'
import test from 'node:test'
import { validateAssetEvidence, validatePrimaryLocations } from '../lib/compliance-evidence.mjs'

async function fixture(t) {
  const root = await mkdtemp(path.join(os.tmpdir(), 'fv-evidence-'))
  t.after(() => rm(root, { recursive: true, force: true }))
  await mkdir(path.join(root, 'cards'))
  await writeFile(path.join(root, 'cards/a.svg'), '<svg/>')
  await writeFile(path.join(root, 'evidence.txt'), 'Archived author declaration — test fixture only')
  const entry = {
    path: 'cards/a.svg', sha256: createHash('sha256').update('<svg/>').digest('hex'),
    author: 'Fixture author', source_reference: 'Original source declaration',
    license_spdx_or_public_domain_basis: 'MIT', evidence_path: 'evidence.txt',
    commercial_use: true, approved_by: 'Fixture reviewer', approved_at: '2025-01-01',
  }
  const asset = { asset_id: 'cards', path_glob: 'cards/**/*.svg', shipped: true, manual_approved: true, sha256_manifest: 'manifest.json' }
  const manifest = { schema_version: 1, asset_id: 'cards', files: [entry] }
  const check = async () => {
    await writeFile(path.join(root, 'manifest.json'), JSON.stringify(manifest))
    return validateAssetEvidence(root, [asset])
  }
  return { root, asset, manifest, entry, check }
}

test('accepts exact approved files and real archived evidence', async t => {
  const { check } = await fixture(t)
  assert.deepEqual(await check(), [])
})

test('rejects content changed after approval', async t => {
  const { root, check } = await fixture(t)
  await writeFile(path.join(root, 'cards/a.svg'), '<svg>changed</svg>')
  assert.match((await check()).join(), /SHA-256 mismatch/)
})

test('rejects new shipped files omitted from approved manifest', async t => {
  const { root, check } = await fixture(t)
  await writeFile(path.join(root, 'cards/new.svg'), '<svg/>')
  assert.match((await check()).join(), /omits shipped files/)
})

test('rejects duplicate manifest paths', async t => {
  const { manifest, entry, check } = await fixture(t)
  manifest.files.push({ ...entry })
  assert.match((await check()).join(), /duplicate/)
})

test('rejects missing, empty or traversing evidence', async t => {
  const { root, entry, check } = await fixture(t)
  entry.evidence_path = '../outside.txt'
  assert.match((await check()).join(), /traversal/)
  entry.evidence_path = 'missing.txt'
  assert.match((await check()).join(), /ENOENT/)
  entry.evidence_path = 'evidence.txt'
  await writeFile(path.join(root, 'evidence.txt'), '')
  assert.match((await check()).join(), /empty/)
})

test('rejects symlink escapes even with a valid-looking evidence path', async t => {
  const { root, entry, check } = await fixture(t)
  await symlink(os.tmpdir(), path.join(root, 'external'))
  entry.evidence_path = `external/${path.basename(root)}/evidence.txt`
  // This resolves back inside root, so it is allowed. A sibling file is not.
  assert.deepEqual(await check(), [])
  const outside = `${root}-outside.txt`
  t.after(() => rm(outside, { force: true }))
  await writeFile(outside, 'not repository evidence')
  entry.evidence_path = `external/${path.basename(outside)}`
  assert.match((await check()).join(), /outside the repository/)
})

test('rejects empty groups, wrong asset identity, future approval and non-commercial rights', async t => {
  const { asset, manifest, entry, check } = await fixture(t)
  asset.path_glob = 'missing/**/*.svg'
  assert.match((await check()).join(), /matches no shipped files/)
  asset.path_glob = 'cards/**/*.svg'
  manifest.asset_id = 'other'
  assert.match((await check()).join(), /matching asset_id/)
  manifest.asset_id = 'cards'
  entry.approved_at = '2999-01-01'
  assert.match((await check()).join(), /future/)
  entry.approved_at = '2025-01-01'
  entry.commercial_use = false
  assert.match((await check()).join(), /commercial use/)
})

test('supports brace and semicolon patterns used by the asset registry', async t => {
  const { asset, check } = await fixture(t)
  asset.path_glob = 'cards/*.{png,svg};cards/**/*.svg'
  assert.deepEqual(await check(), [])
})

test('disabled/unapproved groups remain subject to the main approval gate', async () => {
  assert.deepEqual(await validateAssetEvidence('/unavailable', [{ shipped: true, manual_approved: false }]), [])
})

test('requires Russian primary and backup locations despite manual approval', () => {
  for (const provider_name of ['Production Docker host', 'Primary PostgreSQL', 'Backup storage']) {
    const provider = { provider_name, enabled: true, manual_approved: true, country: 'DE', cross_border_transfer: false }
    assert.equal(validatePrimaryLocations([provider]).length, 1)
    provider.country = 'RU'
    assert.deepEqual(validatePrimaryLocations([provider]), [])
    provider.cross_border_transfer = true
    assert.equal(validatePrimaryLocations([provider]).length, 1)
  }
  assert.deepEqual(validatePrimaryLocations([{ provider_name: 'OpenAI API', enabled: true, country: 'US', cross_border_transfer: true }]), [])
})
