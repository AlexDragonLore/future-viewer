<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useReadingStore } from '@/stores/useReadingStore'
import CardDeck from '@/components/cards/CardDeck.vue'
import CardFlip from '@/components/cards/CardFlip.vue'
import { computeSlots, computeCardWidth } from '@/composables/useSpread'
import { dealCards, shuffleDeck } from '@/composables/useCardAnimation'
import type { Reading, ReadingCard, SpreadType } from '@/types'
import { playShuffle, playCardFlip, playReveal, unlockAudio } from '@/composables/useAudio'
import type { ReadingApiError } from '@/api/readingApi'
import { versionedCardImage } from '@/utils/assets'
import { preloadImages } from '@/utils/preloadImages'

const router = useRouter()
const store = useReadingStore()

const stage = ref<'idle' | 'shuffling' | 'dealing' | 'flipping' | 'done'>('idle')
const board = ref<HTMLElement | null>(null)
const deckRef = ref<{ $el: HTMLElement } | null>(null)
const getDeckEl = () => deckRef.value?.$el ?? null
const cardRefs = ref<HTMLElement[]>([])
const flippedFlags = ref<boolean[]>([])
const placeholders = ref<ReadingCard[]>([])
const showRealCards = ref(false)
const pendingSpread = ref<SpreadType | null>(null)
const pendingQuestion = ref('')
const boardWidth = ref(0)
const cardWidth = computed(() =>
  pendingSpread.value !== null ? computeCardWidth(pendingSpread.value, boardWidth.value) : 140,
)
const cardHeight = computed(() => Math.round(cardWidth.value * (230 / 140)))

let resizeObserver: ResizeObserver | null = null
type AnimationTimeline = ReturnType<typeof shuffleDeck>
let shuffleTimeline: AnimationTimeline | null = null
let dealTimeline: AnimationTimeline | null = null
let streamAbort: AbortController | null = null
let keepStreamAfterUnmount = false
let runId = 0
const timers = new Set<ReturnType<typeof setTimeout>>()

function updateBoardWidth() {
  if (board.value) boardWidth.value = board.value.getBoundingClientRect().width
}

function clearTimers() {
  timers.forEach((timer) => clearTimeout(timer))
  timers.clear()
}

function sleep(ms: number) {
  return new Promise<void>((resolve) => {
    const timer = setTimeout(() => {
      timers.delete(timer)
      resolve()
    }, ms)
    timers.add(timer)
  })
}

function killTimeline(timeline: AnimationTimeline | null) {
  timeline?.kill()
}

function cleanupRun(abortStream: boolean) {
  runId += 1
  killTimeline(shuffleTimeline)
  killTimeline(dealTimeline)
  shuffleTimeline = null
  dealTimeline = null
  clearTimers()

  if (abortStream) {
    streamAbort?.abort()
  }
  streamAbort = null
}

function isAbortError(e: unknown) {
  return e instanceof Error && e.name === 'AbortError'
}

function normalizeReadingForSpread(reading: Reading, spreadType: SpreadType): Reading {
  const expectedCount = spreadType as number
  if (reading.cards.length <= expectedCount) return reading

  return {
    ...reading,
    spreadType,
    cards: reading.cards.slice(0, expectedCount),
  }
}

onMounted(async () => {
  const saved = sessionStorage.getItem('fv_pending')
  if (!saved) {
    router.replace({ name: 'home' })
    return
  }
  const parsed = JSON.parse(saved)
  pendingSpread.value = parsed.spreadType
  pendingQuestion.value = parsed.question

  const count = parsed.spreadType as number
  placeholders.value = Array.from({ length: count }, (_, i) => ({
    position: i,
    positionName: '',
    positionMeaning: '',
    cardId: 0,
    cardName: '',
    imagePath: '',
    isReversed: false,
    meaning: '',
  }))
  flippedFlags.value = Array.from({ length: count }, () => false)

  await nextTick()
  updateBoardWidth()
  if (typeof ResizeObserver !== 'undefined' && board.value) {
    resizeObserver = new ResizeObserver(() => updateBoardWidth())
    resizeObserver.observe(board.value)
  }
  startReading()
})

onBeforeUnmount(() => {
  resizeObserver?.disconnect()
  resizeObserver = null
  cleanupRun(!keepStreamAfterUnmount)
})

async function startReading() {
  if (stage.value !== 'idle') return
  const deckEl = getDeckEl()
  if (!deckEl || !pendingSpread.value || !board.value) return

  cleanupRun(true)
  const currentRun = runId
  keepStreamAfterUnmount = false
  showRealCards.value = false

  stage.value = 'shuffling'
  unlockAudio()
  playShuffle()

  streamAbort = new AbortController()
  const { cardsPromise, donePromise } = store.createStream(
    pendingSpread.value,
    pendingQuestion.value,
    streamAbort.signal,
  )
  donePromise.catch(() => {})
  let cardsFailed = false
  cardsPromise.catch((e) => {
    if (isAbortError(e) || currentRun !== runId) return
    const err = e as ReadingApiError
    if (err.code === 'question_needs_rewrite' || err.code === 'question_rejected') {
      cardsFailed = true
      sessionStorage.setItem('fv_question_validation', JSON.stringify({
        code: err.code,
        message: err.message,
        suggestedQuestion: err.suggestedQuestion ?? null,
      }))
      router.replace({ name: 'home' })
      return
    }
    cardsFailed = true
    sessionStorage.setItem('fv_reading_error', JSON.stringify({
      message: store.error ?? err.message ?? 'Не удалось создать расклад',
    }))
    router.replace({ name: 'home' })
  })

  await new Promise<void>((resolve) => {
    shuffleTimeline = shuffleDeck(deckEl, () => resolve())
  })

  if (cardsFailed || currentRun !== runId || !board.value || !pendingSpread.value) return

  const boardRect = board.value.getBoundingClientRect()
  const deckRect = deckEl.getBoundingClientRect()
  const slots = computeSlots(pendingSpread.value, boardRect.width, boardRect.height)
  const halfW = cardWidth.value / 2
  const halfH = cardHeight.value / 2

  const from = {
    x: deckRect.left - boardRect.left + deckRect.width / 2,
    y: deckRect.top - boardRect.top + deckRect.height / 2,
  }

  stage.value = 'dealing'
  cardRefs.value = []
  await nextTick()

  const targets = cardRefs.value.map((el, i) => ({
    el,
    from: { x: from.x - halfW, y: from.y - halfH },
    to: { x: slots[i].x - halfW, y: slots[i].y - halfH },
  }))

  await new Promise<void>((resolve) => {
    dealTimeline = dealCards(targets, () => resolve())
  })

  if (cardsFailed || currentRun !== runId) return

  let reading
  try {
    reading = await cardsPromise
  } catch (e) {
    if (!isAbortError(e) && currentRun === runId) stage.value = 'idle'
    return
  }

  if (currentRun !== runId) return

  reading = normalizeReadingForSpread(reading, pendingSpread.value)
  store.current = reading

  await preloadImages(reading.cards.map((card) => versionedCardImage(card.imagePath)))
  if (currentRun !== runId) return

  placeholders.value = reading.cards
  flippedFlags.value = reading.cards.map(() => false)
  showRealCards.value = true
  await nextTick()

  stage.value = 'flipping'
  for (let i = 0; i < flippedFlags.value.length; i++) {
    await sleep(260)
    if (currentRun !== runId) return
    playCardFlip()
    flippedFlags.value[i] = true
  }

  await sleep(950)
  if (currentRun !== runId) return

  playReveal()
  stage.value = 'done'
  keepStreamAfterUnmount = true
  router.push({ name: 'result' })
}

function setCardRef(el: Element | null, idx: number) {
  if (el instanceof HTMLElement) cardRefs.value[idx] = el
}
</script>

<template>
  <main ref="board" class="reading-board relative px-3 sm:px-6 py-8 sm:py-12">
    <div class="text-center mb-6 sm:mb-8 relative z-10">
      <div class="question-ribbon text-mystic-accent text-xs tracking-[0.4em] mb-2 break-words px-2">✦ {{ pendingQuestion }}</div>
      <h2 class="reading-title font-display text-2xl sm:text-3xl gold-text">
        {{
          stage === 'idle' ? 'Коснись колоды' :
          stage === 'shuffling' ? 'Перемешиваю судьбу…' :
          stage === 'dealing' ? 'Раскладываю…' :
          stage === 'flipping' ? 'Раскрываю…' :
          'Готово'
        }}
      </h2>
    </div>

    <div v-if="stage === 'dealing' || stage === 'flipping' || stage === 'done'" class="cards-layer absolute inset-0 pointer-events-none">
      <div
        v-for="(card, i) in placeholders"
        :key="i"
        :ref="(el) => setCardRef(el as Element, i)"
        class="card-slot"
        style="position: absolute; top: 0; left: 0;"
      >
        <CardFlip v-if="showRealCards" :card="card" :face-up="flippedFlags[i]" :width="cardWidth" />
        <div
          v-else
          class="deal-card-back"
          :style="{ width: cardWidth + 'px', height: cardHeight + 'px' }"
          aria-hidden="true"
        >
          <div class="deal-card-back-pattern"></div>
          <div class="deal-card-back-emblem">✦</div>
        </div>
      </div>
    </div>

    <div class="absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 z-10">
      <CardDeck
        v-if="stage === 'idle' || stage === 'shuffling'"
        ref="deckRef"
        :size="cardWidth"
        :disabled="stage !== 'idle'"
        @shuffle="startReading"
      />
    </div>

    <div class="reading-hint fixed bottom-6 left-0 right-0 text-center text-xs text-mystic-silver/50 z-10">
      <span v-if="stage === 'idle' && !store.error">коснись, чтобы перемешать колоду</span>
      <span v-else-if="store.error" class="text-red-400/80">{{ store.error }}</span>
    </div>
  </main>
</template>

<style scoped>
.reading-board {
  overflow: hidden;
  height: calc(100vh - 4rem);
  height: calc(100svh - 4rem);
  min-height: calc(100svh - 4rem);
  max-height: calc(100svh - 4rem);
  contain: layout paint;
}
.cards-layer {
  z-index: 5;
}
.card-slot {
  transform: translateZ(0);
}
.deal-card-back {
  position: relative;
  border-radius: 12px;
  border: 1px solid rgba(245, 194, 107, 0.5);
  background:
    radial-gradient(circle at center, rgba(245, 194, 107, 0.2), transparent 65%),
    linear-gradient(135deg, #1a0a2e 0%, #2a1248 50%, #13082a 100%);
  box-shadow:
    0 0 14px rgba(245, 194, 107, 0.16),
    inset 0 0 18px rgba(245, 194, 107, 0.1);
  contain: layout paint style;
  overflow: hidden;
  transform: translateZ(0);
}
.deal-card-back-pattern {
  position: absolute;
  inset: 6px;
  border: 1px solid rgba(245, 194, 107, 0.28);
  border-radius: 8px;
  background: repeating-linear-gradient(45deg, rgba(245, 194, 107, 0.045) 0 2px, transparent 2px 8px);
}
.deal-card-back-emblem {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 2.3rem;
  color: rgba(245, 194, 107, 0.78);
  text-shadow: 0 0 10px rgba(245, 194, 107, 0.55);
}
.question-ribbon {
  overflow-wrap: anywhere;
  line-height: 1.45;
}

@media (max-width: 640px) {
  .reading-board {
    padding-top: 1.25rem;
    padding-bottom: 4.75rem;
  }
  .question-ribbon {
    max-width: 100%;
    letter-spacing: 0.12em;
    font-size: 0.68rem;
  }
  .reading-title {
    font-size: 1.35rem;
    line-height: 1.25;
  }
  .reading-hint {
    bottom: 1.1rem;
    padding: 0 1rem;
  }
}
</style>
