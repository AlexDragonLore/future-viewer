<script setup lang="ts">
import { computed, onMounted, toRef } from 'vue'
import { useRouter } from 'vue-router'
import { useReadingStore } from '@/stores/useReadingStore'
import CardFlip from '@/components/cards/CardFlip.vue'
import { useTypewriter } from '@/composables/useTypewriter'

const router = useRouter()
const store = useReadingStore()

const reading = computed(() => store.current)
const interpretationSource = toRef(() => reading.value?.interpretation ?? '')
const { output: typed } = useTypewriter(interpretationSource, 18)

onMounted(() => {
  if (!reading.value) {
    router.replace({ name: 'home' })
  }
})

function again() {
  store.reset()
  router.push({ name: 'home' })
}
</script>

<template>
  <main v-if="reading" class="min-h-screen px-6 py-16 flex flex-col items-center">
    <header class="text-center mb-10">
      <div class="text-mystic-accent text-xs tracking-[0.4em] mb-2">✦ {{ reading.spreadName.toUpperCase() }} ✦</div>
      <h1 class="font-display text-4xl md:text-5xl gold-text">Расклад раскрыт</h1>
      <p class="text-mystic-silver/60 mt-2 italic">«{{ reading.question }}»</p>
    </header>

    <section class="cards-grid mb-12">
      <div v-for="card in reading.cards" :key="card.position" class="card-entry">
        <div class="text-xs text-mystic-accent/80 uppercase tracking-widest mb-2 text-center">
          {{ card.positionName }}
        </div>
        <CardFlip :card="card" :face-up="true" :width="130" />
        <div class="text-xs text-mystic-silver/60 mt-2 text-center max-w-[140px]">
          {{ card.meaning }}
        </div>
      </div>
    </section>

    <section class="mystic-card max-w-2xl w-full p-8 mb-8">
      <div class="text-xs uppercase tracking-widest text-mystic-accent/80 mb-3">Интерпретация</div>
      <p class="text-mystic-silver leading-relaxed whitespace-pre-line">{{ typed }}<span class="caret">▮</span></p>
    </section>

    <button class="glow-button" @click="again">Новый расклад</button>
  </main>
</template>

<style scoped>
.cards-grid {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 2rem;
  max-width: 1000px;
}
.card-entry {
  display: flex;
  flex-direction: column;
  align-items: center;
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
