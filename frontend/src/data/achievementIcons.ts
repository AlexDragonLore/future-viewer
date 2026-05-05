import type { Component } from 'vue'
import {
  Scroll,
  MessageSquareHeart,
  Send,
  Flame,
  CircleCheck,
  Medal,
  Trophy,
  Crown,
  Star,
  Award,
  Sparkles,
} from 'lucide-vue-next'

export const ACHIEVEMENT_ICONS: Record<string, Component> = {
  first_reading: Scroll,
  first_feedback: MessageSquareHeart,
  telegram_linked: Send,
  streak_3: Flame,
  streak_7: Flame,
  streak_30: Flame,
  total_10: CircleCheck,
  total_50: Medal,
  total_100: Trophy,
  score_master: Crown,
  perfect_10: Star,
  high_five: Award,
}

export function iconForAchievement(code: string): Component {
  return ACHIEVEMENT_ICONS[code] ?? Sparkles
}
