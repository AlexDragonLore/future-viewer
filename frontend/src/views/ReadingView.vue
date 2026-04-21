<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useReadingStore } from '@/stores/useReadingStore'
import CardDeck from '@/components/cards/CardDeck.vue'
import CardFlip from '@/components/cards/CardFlip.vue'
import { computeSlots, computeCardWidth } from '@/composables/useSpread'
import { dealCards, shuffleDeck } from '@/composables/useCardAnimation'
import type { ReadingCard, SpreadType } from '@/types'
import { playShuffle, playDeal, playCardFlip, playReveal } from '@/composables/useAudio'

const router = useRouter()
const store = useReadingStore()

const stage = ref<'idle' | 'shuffling' | 'dealing' | 'flipping' | 'done'>('idle')
const board = ref<HTMLElement | null>(null)
const deckRef = ref<{ $el: HTMLElement } | null>(null)
const getDeckEl = () => deckRef.value?.$el ?? null
const cardRefs = ref<HTMLElement[]>([])
const flippedFlags = ref<boolean[]>([])
const placeholders = ref<ReadingCard[]>([])
const pendingSpread = ref<SpreadType | null>(null)
const pendingQuestion = ref('')
const boardWidth = ref(0)
const cardWidth = computed(() =>
  pendingSpread.value !== null ? computeCardWidth(pendingSpread.value, boardWidth.value) : 140,
)
const cardHeight = computed(() => Math.round(cardWidth.value * (230 / 140)))

let resizeObserver: ResizeObserver | null = null

function updateBoardWidth() {
  if (board.value) boardWidth.value = board.value.getBoundingClientRect().width
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
})

async function startReading() {
  if (stage.value !== 'idle') return
  const deckEl = getDeckEl()
  if (!deckEl || !pendingSpread.value || !board.value) return

  stage.value = 'shuffling'
  playShuffle()

  const { cardsPromise, donePromise } = store.createStream(pendingSpread.value, pendingQuestion.value)
  donePromise.catch(() => {})
  let cardsFailed = false
  cardsPromise.catch(() => {
    cardsFailed = true
    stage.value = 'idle'
  })

  await new Promise<void>((resolve) => {
    shuffleDeck(deckEl, () => resolve())
  })

  if (cardsFailed) return

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
  await nextTick()

  const targets = cardRefs.value.map((el, i) => ({
    el,
    from: { x: from.x - halfW, y: from.y - halfH },
    to: { x: slots[i].x - halfW, y: slots[i].y - halfH },
  }))

  dealCards(targets, async () => {
    let reading
    try {
      reading = await cardsPromise
    } catch {
      stage.value = 'idle'
      return
    }

    placeholders.value = reading.cards
    flippedFlags.value = reading.cards.map(() => false)
    await nextTick()

    stage.value = 'flipping'
    for (let i = 0; i < flippedFlags.value.length; i++) {
      await new Promise((r) => setTimeout(r, 520))
      playCardFlip()
      flippedFlags.value[i] = true
    }
    setTimeout(() => {
      playReveal()
      stage.value = 'done'
      router.push({ name: 'result' })
    }, 1800)
  })
}

function setCardRef(el: Element | null, idx: number) {
  if (el instanceof HTMLElement) cardRefs.value[idx] = el
}
</script>

<template>
  <main ref="board" class="reading-board min-h-screen relative px-3 sm:px-6 py-8 sm:py-12">
    <div class="text-center mb-6 sm:mb-8 relative z-10">
      <div class="text-mystic-accent text-xs tracking-[0.4em] mb-2 break-words px-2">✦ {{ pendingQuestion }}</div>
      <h2 class="font-display text-2xl sm:text-3xl gold-text">
        {{
          stage === 'idle' ? 'Коснись колоды' :
          stage === 'shuffling' ? 'Перемешиваю судьбу…' :
          stage === 'dealing' ? 'Раскладываю…' :
          stage === 'flipping' ? 'Раскрываю…' :
          'Готово'
        }}
      </h2>
    </div>

    <div class="cards-layer absolute inset-0 pointer-events-none">
      <div
        v-for="(card, i) in placeholders"
        :key="i"
        :ref="(el) => setCardRef(el as Element, i)"
        class="card-slot"
        style="position: absolute; top: 0; left: 0;"
      >
        <CardFlip :card="card" :face-up="flippedFlags[i]" :width="cardWidth" />
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

    <div class="fixed bottom-6 left-0 right-0 text-center text-xs text-mystic-silver/50 z-10">
      <span v-if="stage === 'idle' && !store.error">коснись, чтобы перемешать колоду</span>
      <span v-else-if="store.error" class="text-red-400/80">{{ store.error }}</span>
    </div>
  </main>
</template>

<style scoped>
.reading-board {
  overflow: hidden;
}
.cards-layer {
  z-index: 5;
}
</style>
