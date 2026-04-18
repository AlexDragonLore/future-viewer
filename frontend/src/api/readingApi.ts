import { httpClient } from './httpClient'
import type { DeckType, Reading, SpreadType, SpreadInfo } from '@/types'

export interface ReadingStreamHandlers {
  onCards: (reading: Reading) => void
  onChunk: (delta: string) => void
  onDone: () => void
  onError?: (error: unknown) => void
}

type StreamEvent =
  | { type: 'cards'; reading: Reading }
  | { type: 'chunk'; delta: string }
  | { type: 'done' }

export const readingApi = {
  async create(spreadType: SpreadType, question: string, deckType: DeckType): Promise<Reading> {
    const { data } = await httpClient.post<Reading>('/api/readings', {
      spreadType,
      question,
      deckType,
    })
    return data
  },

  async createStream(
    spreadType: SpreadType,
    question: string,
    deckType: DeckType,
    handlers: ReadingStreamHandlers,
    signal?: AbortSignal,
  ): Promise<void> {
    const baseURL = (httpClient.defaults.baseURL ?? '').replace(/\/$/, '')
    const token = localStorage.getItem('fv_token')

    const headers: Record<string, string> = { 'Content-Type': 'application/json' }
    if (token) headers.Authorization = `Bearer ${token}`

    let response: Response
    try {
      response = await fetch(`${baseURL}/api/readings/stream`, {
        method: 'POST',
        headers,
        body: JSON.stringify({ spreadType, question, deckType }),
        signal,
      })
    } catch (e) {
      handlers.onError?.(e)
      throw e
    }

    if (!response.ok || !response.body) {
      const error = new Error(`Stream failed: ${response.status} ${response.statusText}`)
      handlers.onError?.(error)
      throw error
    }

    const reader = response.body.getReader()
    const decoder = new TextDecoder()
    let buffer = ''

    try {
      while (true) {
        const { value, done } = await reader.read()
        if (done) break
        buffer += decoder.decode(value, { stream: true })

        let newlineIdx: number
        while ((newlineIdx = buffer.indexOf('\n')) >= 0) {
          const line = buffer.slice(0, newlineIdx).trim()
          buffer = buffer.slice(newlineIdx + 1)
          if (!line) continue
          dispatch(line, handlers)
        }
      }

      const trailing = buffer.trim()
      if (trailing) dispatch(trailing, handlers)
    } catch (e) {
      handlers.onError?.(e)
      throw e
    }
  },

  async get(id: string): Promise<Reading> {
    const { data } = await httpClient.get<Reading>(`/api/readings/${id}`)
    return data
  },

  async history(): Promise<Reading[]> {
    const { data } = await httpClient.get<Reading[]>('/api/readings/history')
    return data
  },

  async spreads(): Promise<SpreadInfo[]> {
    const { data } = await httpClient.get<SpreadInfo[]>('/api/cards/spreads')
    return data
  },
}

function dispatch(line: string, handlers: ReadingStreamHandlers) {
  let evt: StreamEvent
  try {
    evt = JSON.parse(line) as StreamEvent
  } catch {
    return
  }
  switch (evt.type) {
    case 'cards':
      handlers.onCards(evt.reading)
      break
    case 'chunk':
      handlers.onChunk(evt.delta)
      break
    case 'done':
      handlers.onDone()
      break
  }
}
