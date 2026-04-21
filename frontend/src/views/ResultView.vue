<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useReadingStore } from '@/stores/useReadingStore'
import CardFlip from '@/components/cards/CardFlip.vue'
import { marked } from 'marked'

const router = useRouter()
const store = useReadingStore()

const reading = computed(() => store.current)

const viewportWidth = ref(typeof window !== 'undefined' ? window.innerWidth : 1024)
function onResize() {
  viewportWidth.value = window.innerWidth
}
onMounted(() => window.addEventListener('resize', onResize))
onBeforeUnmount(() => window.removeEventListener('resize', onResize))

const cardWidth = computed(() => {
  if (viewportWidth.value <= 380) return 96
  if (viewportWidth.value <= 640) return 110
  return 130
})

const displayed = ref('')
let rafId: number | null = null
let lastTick = 0

function tick(now: number) {
  rafId = null
  const target = store.streamingText
  if (displayed.value.length >= target.length) {
    lastTick = now
    if (!store.streamingDone) scheduleTick()
    return
  }

  const dt = lastTick === 0 ? 16 : Math.min(now - lastTick, 80)
  lastTick = now

  const behind = target.length - displayed.value.length
  const baseRate = 55
  const catchUp = behind * 0.12
  const charsPerSec = baseRate + catchUp
  const step = Math.max(1, Math.round((charsPerSec * dt) / 1000))
  const next = Math.min(target.length, displayed.value.length + step)
  displayed.value = target.slice(0, next)

  if (displayed.value.length < target.length || !store.streamingDone) {
    scheduleTick()
  }
}

function scheduleTick() {
  if (rafId !== null) return
  rafId = requestAnimationFrame(tick)
}

function cancelTick() {
  if (rafId !== null) {
    cancelAnimationFrame(rafId)
    rafId = null
  }
}

watch(
  () => store.streamingText,
  (text) => {
    if (text.length < displayed.value.length) {
      displayed.value = ''
      lastTick = 0
    }
    scheduleTick()
  },
  { immediate: true },
)

watch(
  () => store.streamingDone,
  (done) => {
    if (done && displayed.value.length >= store.streamingText.length) cancelTick()
  },
)

const typedHtml = computed(() => marked.parse(displayed.value) as string)
const streaming = computed(() => !store.streamingDone || displayed.value.length < store.streamingText.length)

watch(displayed, async () => {
  if (!streaming.value) return
  await nextTick()
  const doc = document.documentElement
  const nearBottom = window.innerHeight + window.scrollY >= doc.scrollHeight - 240
  if (nearBottom) window.scrollTo({ top: doc.scrollHeight, behavior: 'auto' })
})

onMounted(() => {
  if (!reading.value) {
    router.replace({ name: 'home' })
  }
})

onBeforeUnmount(() => cancelTick())

function again() {
  store.reset()
  router.push({ name: 'home' })
}
</script>

<template>
  <main v-if="reading" class="min-h-screen px-4 sm:px-6 py-12 sm:py-16 flex flex-col items-center">
    <header class="text-center mb-10">
      <div class="text-mystic-accent text-xs tracking-[0.4em] mb-2">✦ {{ reading.spreadName.toUpperCase() }} ✦</div>
      <h1 class="font-display text-3xl sm:text-4xl md:text-5xl gold-text">Расклад раскрыт</h1>
      <p class="text-mystic-silver/60 mt-2 italic">«{{ reading.question }}»</p>
    </header>

    <section class="cards-grid mb-12">
      <div v-for="card in reading.cards" :key="card.position" class="card-entry">
        <div class="text-xs text-mystic-accent/80 uppercase tracking-widest mb-2 text-center">
          {{ card.positionName }}
        </div>
        <CardFlip :card="card" :face-up="true" :width="cardWidth" />
        <div class="text-xs text-mystic-silver/60 mt-2 text-center max-w-[140px]">
          {{ card.meaning }}
        </div>
      </div>
    </section>

    <section class="mystic-card max-w-2xl w-full p-5 sm:p-8 mb-8">
      <div class="text-xs uppercase tracking-widest text-mystic-accent/80 mb-3">Интерпретация</div>
      <div class="prose-mystic text-mystic-silver leading-relaxed" v-html="typedHtml" /><span v-if="streaming" class="caret">▮</span>
    </section>

    <button class="glow-button" @click="again">Новый расклад</button>
  </main>
</template>

<style scoped>
.cards-grid {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: clamp(0.5rem, 2vw, 2rem);
  max-width: 1000px;
  width: 100%;
}
.card-entry {
  display: flex;
  flex-direction: column;
  align-items: center;
}
.prose-mystic :deep(h2) {
  font-size: 1.1rem;
  font-weight: 600;
  color: #f5c26b;
  margin-top: 1.25rem;
  margin-bottom: 0.4rem;
  letter-spacing: 0.05em;
}
.prose-mystic :deep(strong) {
  color: #e8d5a3;
  font-weight: 600;
}
.prose-mystic :deep(ul) {
  list-style: disc;
  padding-left: 1.25rem;
  margin: 0.4rem 0;
}
.prose-mystic :deep(li) {
  margin-bottom: 0.2rem;
}
.prose-mystic :deep(p) {
  margin-bottom: 0.6rem;
}
.caret {
  display: inline-block;
  animation: blink 0.9s steps(2, start) infinite;
  color: #f5c26b;
  margin-left: 2px;
}
@keyframes blink {
  to {
    visibility: hidden;
  }
}
</style>
