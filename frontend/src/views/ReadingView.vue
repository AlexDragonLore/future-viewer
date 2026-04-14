<script setup lang="ts">
import { nextTick, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useReadingStore } from '@/stores/useReadingStore'
import CardDeck from '@/components/cards/CardDeck.vue'
import CardFlip from '@/components/cards/CardFlip.vue'
import { computeSlots } from '@/composables/useSpread'
import { dealCards, shuffleDeck } from '@/composables/useCardAnimation'
import type { ReadingCard, SpreadType } from '@/types'

const router = useRouter()
const store = useReadingStore()

const stage = ref<'idle' | 'shuffling' | 'loading' | 'dealing' | 'flipping' | 'done'>('idle')
const board = ref<HTMLElement | null>(null)
const deckRef = ref<HTMLElement | null>(null)
const cardRefs = ref<HTMLElement[]>([])
const flippedFlags = ref<boolean[]>([])
const placeholders = ref<ReadingCard[]>([])
const pendingSpread = ref<SpreadType | null>(null)
const pendingQuestion = ref('')

onMounted(() => {
  const saved = sessionStorage.getItem('fv_pending')
  if (!saved) {
    router.replace({ name: 'home' })
    return
  }
  const parsed = JSON.parse(saved)
  pendingSpread.value = parsed.spreadType
  pendingQuestion.value = parsed.question

  const count =
    parsed.spreadType === 1 ? 1 : parsed.spreadType === 3 ? 3 : 10
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
})

async function startReading() {
  if (stage.value !== 'idle') return
  if (!deckRef.value || !pendingSpread.value) return

  stage.value = 'shuffling'
  await new Promise<void>((resolve) => {
    shuffleDeck(deckRef.value!, () => resolve())
  })

  stage.value = 'loading'
  try {
    await store.create(pendingSpread.value, pendingQuestion.value)
  } catch {
    stage.value = 'idle'
    return
  }

  stage.value = 'dealing'
  await nextTick()

  if (!store.current.value && !store.current) {
    // pinia: store.current is Ref, access via .value? Actually using setup store returns refs.
  }

  const reading = store.current
  if (!reading) {
    stage.value = 'idle'
    return
  }

  placeholders.value = reading.cards
  flippedFlags.value = reading.cards.map(() => false)
  await nextTick()

  const boardRect = board.value!.getBoundingClientRect()
  const deckRect = deckRef.value!.getBoundingClientRect()
  const slots = computeSlots(pendingSpread.value, boardRect.width, boardRect.height)

  const from = {
    x: deckRect.left - boardRect.left + deckRect.width / 2,
    y: deckRect.top - boardRect.top + deckRect.height / 2,
  }

  const targets = cardRefs.value.map((el, i) => ({
    el,
    from: { x: from.x - 70, y: from.y - 115 },
    to: { x: slots[i].x - 70, y: slots[i].y - 115 },
  }))

  const tl = dealCards(targets)
  tl.eventCallback('onComplete', async () => {
    stage.value = 'flipping'
    for (let i = 0; i < flippedFlags.value.length; i++) {
      await new Promise((r) => setTimeout(r, 320))
      flippedFlags.value[i] = true
    }
    setTimeout(() => {
      stage.value = 'done'
      router.push({ name: 'result' })
    }, 1200)
  })
}

function setCardRef(el: Element | null, idx: number) {
  if (el instanceof HTMLElement) cardRefs.value[idx] = el
}
</script>

<template>
  <main ref="board" class="reading-board min-h-screen relative px-6 py-12">
    <div class="text-center mb-8 relative z-10">
      <div class="text-mystic-accent text-xs tracking-[0.4em] mb-2">✦ {{ pendingQuestion }}</div>
      <h2 class="font-display text-3xl gold-text">
        {{
          stage === 'idle' ? 'Коснись колоды' :
          stage === 'shuffling' ? 'Перемешиваю судьбу…' :
          stage === 'loading' ? 'Карты зовут…' :
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
        <CardFlip :card="card" :face-up="flippedFlags[i]" :width="140" />
      </div>
    </div>

    <div class="absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 z-10">
      <CardDeck
        v-if="stage === 'idle' || stage === 'shuffling' || stage === 'loading'"
        ref="deckRef"
        :size="140"
        :disabled="stage !== 'idle'"
        @shuffle="startReading"
      />
    </div>

    <div class="fixed bottom-6 left-0 right-0 text-center text-xs text-mystic-silver/50 z-10">
      <span v-if="stage === 'idle'">коснись, чтобы перемешать колоду</span>
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
