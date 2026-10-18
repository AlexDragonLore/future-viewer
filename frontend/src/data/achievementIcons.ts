import type { Component } from 'vue'
import {
  Scroll,
  Flame,
  CircleCheck,
  Medal,
  Trophy,
  Sparkles,
} from 'lucide-vue-next'

export const ACHIEVEMENT_ICONS: Record<string, Component> = {
  first_reading: Scroll,
  streak_3: Flame,
  streak_7: Flame,
  streak_30: Flame,
  total_10: CircleCheck,
  total_50: Medal,
  total_100: Trophy,
}

export function iconForAchievement(code: string): Component {
  return ACHIEVEMENT_ICONS[code] ?? Sparkles
}
