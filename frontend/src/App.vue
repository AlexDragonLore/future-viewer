<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import Starfield from '@/components/fx/Starfield.vue'
import MistLayer from '@/components/fx/MistLayer.vue'
import SiteHeader from '@/components/SiteHeader.vue'
import SiteFooter from '@/components/SiteFooter.vue'
import { useMotionPreferences } from '@/composables/useMotionPreferences'
import { unlockAudio } from '@/composables/useAudio'
import { usePublicConfigStore } from '@/stores/usePublicConfigStore'
import { useReadingStore } from '@/stores/useReadingStore'

const publicConfig = usePublicConfigStore()
const route = useRoute()
const readingStore = useReadingStore()
const { prefersReducedMotion } = useMotionPreferences()

const performanceMode = computed(() => {
  if (route.name === 'reading') return true
  if (route.name !== 'result') return false

  return readingStore.loading || (readingStore.cardsReady && !readingStore.streamingDone)
})

const ambientEffectsActive = computed(() => !performanceMode.value && !prefersReducedMotion.value)
const showFooter = computed(() => route.name !== 'reading')
const useRouteTransition = computed(() => route.name !== 'reading' && route.name !== 'result')

onMounted(() => {
  publicConfig.load()
  window.addEventListener('pointerdown', unlockAudio, { once: true, passive: true })
  window.addEventListener('keydown', unlockAudio, { once: true })
  window.addEventListener('touchstart', unlockAudio, { once: true, passive: true })
})

onBeforeUnmount(() => {
  window.removeEventListener('pointerdown', unlockAudio)
  window.removeEventListener('keydown', unlockAudio)
  window.removeEventListener('touchstart', unlockAudio)
})
</script>

<template>
  <div
    class="app-shell min-h-screen relative overflow-hidden bg-gradient-to-b from-mystic-deepest via-mystic-deep to-mystic-mid text-mystic-silver font-body"
    :class="{ 'performance-mode': performanceMode }"
  >
    <Starfield :active="ambientEffectsActive" />
    <MistLayer :active="ambientEffectsActive" />
    <SiteHeader />
    <div class="relative z-10 flex flex-col min-h-screen pt-16">
      <RouterView v-slot="{ Component }">
        <Transition v-if="useRouteTransition" name="fade" mode="out-in">
          <component :is="Component" />
        </Transition>
        <component v-else :is="Component" />
      </RouterView>
      <SiteFooter v-if="showFooter" />
    </div>
    <div class="screen-vignette pointer-events-none fixed inset-0 z-[5]"></div>
  </div>
</template>

<style>
.screen-vignette {
  box-shadow: inset 0 0 140px 36px rgba(0, 0, 0, 0.42);
}

.performance-mode .screen-vignette {
  box-shadow: inset 0 0 52px 10px rgba(0, 0, 0, 0.2);
}

.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.5s ease;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

@media (max-width: 640px) {
  .screen-vignette {
    box-shadow: inset 0 0 36px 6px rgba(0, 0, 0, 0.12);
  }
}

@media (prefers-reduced-motion: reduce) {
  .fade-enter-active,
  .fade-leave-active {
    transition: none;
  }
}
</style>
