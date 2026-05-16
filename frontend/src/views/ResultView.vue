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
const renderedHtml = ref('')
const streamTail = ref<HTMLElement | null>(null)
let rafId: number | null = null
let markdownRafId: number | null = null
let scrollRafId: number | null = null
let lastTick = 0
let shouldFollowStream = true
let userScrollIntent = false
let userScrollIntentTimer: ReturnType<typeof setTimeout> | null = null

const hasActiveStream = computed(() => store.streamingText.length > 0 || store.loading || store.cardsReady)
const targetText = computed(() => store.streamingText || reading.value?.interpretation || '')

function distanceFromBottom() {
  const scroller = document.scrollingElement ?? document.documentElement
  return scroller.scrollHeight - (window.scrollY + window.innerHeight)
}

function onScroll() {
  if (!userScrollIntent) return
  shouldFollowStream = distanceFromBottom() <= 360
}

function refreshFollowAfterUserScroll() {
  shouldFollowStream = distanceFromBottom() <= 360
}

function markUserScrollIntent() {
  userScrollIntent = true
  if (userScrollIntentTimer) clearTimeout(userScrollIntentTimer)
  userScrollIntentTimer = setTimeout(() => {
    userScrollIntent = false
  }, 900)
  refreshFollowAfterUserScroll()
  if (typeof requestAnimationFrame !== 'undefined') {
    requestAnimationFrame(refreshFollowAfterUserScroll)
  }
}

function onKeydown(e: KeyboardEvent) {
  if (['ArrowUp', 'ArrowDown', 'PageUp', 'PageDown', 'Home', 'End', ' '].includes(e.key)) {
    markUserScrollIntent()
  }
}

function tick(now: number) {
  rafId = null
  const target = targetText.value
  if (displayed.value.length >= target.length) {
    lastTick = now
    if (hasActiveStream.value && !store.streamingDone) scheduleTick()
    return
  }

  const dt = lastTick === 0 ? 16 : Math.min(now - lastTick, 80)
  lastTick = now

  const behind = target.length - displayed.value.length
  const baseRate = 55
  const catchUp = behind * 0.12
  const charsPerSec = baseRate + catchUp
  const step = hasActiveStream.value ? Math.max(1, Math.round((charsPerSec * dt) / 1000)) : Math.max(1, Math.ceil(target.length / 3))
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

function renderMarkdown() {
  markdownRafId = null
  renderedHtml.value = marked.parse(displayed.value) as string
}

function scheduleMarkdownRender() {
  if (markdownRafId !== null) return
  if (typeof requestAnimationFrame === 'undefined') {
    renderMarkdown()
    return
  }
  markdownRafId = requestAnimationFrame(renderMarkdown)
}

function cancelMarkdownRender() {
  if (markdownRafId !== null && typeof cancelAnimationFrame !== 'undefined') {
    cancelAnimationFrame(markdownRafId)
  }
  markdownRafId = null
}

function scheduleAutoScroll() {
  if (!streaming.value || scrollRafId !== null || !shouldFollowStream) return
  if (typeof requestAnimationFrame === 'undefined') return
  scrollRafId = requestAnimationFrame(() => {
    scrollRafId = null
    const tail = streamTail.value
    if (tail && typeof tail.scrollIntoView === 'function') {
      tail.scrollIntoView({ block: 'end', inline: 'nearest', behavior: 'auto' })
      return
    }
    const scroller = document.scrollingElement ?? document.documentElement
    window.scrollTo({ top: scroller.scrollHeight, behavior: 'auto' })
  })
}

function cancelAutoScroll() {
  if (scrollRafId !== null && typeof cancelAnimationFrame !== 'undefined') {
    cancelAnimationFrame(scrollRafId)
  }
  scrollRafId = null
}

watch(
  targetText,
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

const streaming = computed(() => (hasActiveStream.value && !store.streamingDone) || displayed.value.length < targetText.value.length)

watch(displayed, () => {
  scheduleMarkdownRender()
  nextTick(() => scheduleAutoScroll())
}, { immediate: true })

onMounted(() => {
  shouldFollowStream = true
  window.addEventListener('scroll', onScroll, { passive: true })
  window.addEventListener('wheel', markUserScrollIntent, { passive: true })
  window.addEventListener('touchmove', markUserScrollIntent, { passive: true })
  window.addEventListener('keydown', onKeydown)
  if (!reading.value) {
    router.replace({ name: 'home' })
  }
})

onBeforeUnmount(() => {
  window.removeEventListener('scroll', onScroll)
  window.removeEventListener('wheel', markUserScrollIntent)
  window.removeEventListener('touchmove', markUserScrollIntent)
  window.removeEventListener('keydown', onKeydown)
  if (userScrollIntentTimer) clearTimeout(userScrollIntentTimer)
  cancelTick()
  cancelMarkdownRender()
  cancelAutoScroll()
})

function again() {
  store.reset()
  router.push({ name: 'home' })
}
</script>

<template>
  <main v-if="reading" class="result-page min-h-screen px-4 sm:px-6 py-12 sm:py-16 flex flex-col items-center">
    <header class="text-center mb-10">
      <div class="result-kicker text-mystic-accent text-xs tracking-[0.4em] mb-2">✦ {{ reading.spreadName.toUpperCase() }} ✦</div>
      <h1 class="font-display text-3xl sm:text-4xl md:text-5xl gold-text">Расклад раскрыт</h1>
      <p class="result-question text-mystic-silver/60 mt-2 italic">«{{ reading.question }}»</p>
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
      <div class="prose-mystic text-mystic-silver leading-relaxed" v-html="renderedHtml" /><span v-if="streaming" class="caret">▮</span><span ref="streamTail" class="stream-tail" aria-hidden="true"></span>
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
  max-width: 9rem;
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
  overflow-wrap: anywhere;
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
@media (max-width: 640px) {
  .result-page {
    padding-top: 2rem;
    padding-bottom: 2.5rem;
  }
  .result-kicker {
    letter-spacing: 0.14em;
    line-height: 1.45;
  }
  .result-question {
    overflow-wrap: anywhere;
    line-height: 1.45;
  }
  .cards-grid {
    gap: 1rem 0.65rem;
    margin-bottom: 2rem;
  }
  .card-entry {
    max-width: 7.2rem;
  }
}
</style>
