import type { StructuredDataContext } from '../data/tarotSeoCatalog.js'

interface StructuredDataRoute {
  name: string
  path: string
  title: string
  description?: string
  contentKind?: string
  slug?: string
}

export function buildSeoStructuredData(route: StructuredDataRoute | undefined, context: StructuredDataContext): object[]
