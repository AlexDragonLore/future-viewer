import { defineStore } from 'pinia'
import { ref } from 'vue'
import { extractApiError } from '@/api/httpClient'
import { tarotPlusApi, type TarotPlusNextStep } from '@/api/tarotPlusApi'
import type { TarotPlusAnswer, TarotPlusSession, TarotPlusSessionListItem } from '@/types'

export const useTarotPlusStore = defineStore('tarot-plus', () => {
  const current = ref<TarotPlusSession | null>(null)
  const history = ref<TarotPlusSessionListItem[]>([])
  const loading = ref(false)
  const paymentLoading = ref(false)
  const reportLoading = ref(false)
  const followUpLoading = ref(false)
  const error = ref<string | null>(null)
  const answers = ref<TarotPlusAnswer[]>([])
  const nextQuestion = ref<string | null>(null)
  const nextStep = ref<TarotPlusNextStep | null>(null)
  const lastFollowUpAnswer = ref<string | null>(null)

  async function createPreview(payload: {
    coreRequest: string
    mainSphere: string
    desiredOutcome: string
  }) {
    loading.value = true
    error.value = null
    try {
      const result = await tarotPlusApi.createPreview(payload)
      current.value = result.session
      answers.value = result.session.answers
      return result
    } catch (e) {
      error.value = extractApiError(e, 'Не удалось создать preview')
      throw e
    } finally {
      loading.value = false
    }
  }

  async function createPayment(sessionId = current.value?.id) {
    if (!sessionId) return
    paymentLoading.value = true
    error.value = null
    try {
      const result = await tarotPlusApi.createPayment(sessionId)
      if (result.confirmationUrl) {
        window.location.assign(result.confirmationUrl)
      } else {
        throw new Error('Платёж не удалось инициализировать')
      }
    } catch (e) {
      error.value = extractApiError(e, 'Не удалось создать платёж')
      throw e
    } finally {
      paymentLoading.value = false
    }
  }

  async function load(sessionId: string) {
    loading.value = true
    error.value = null
    try {
      current.value = await tarotPlusApi.get(sessionId)
      answers.value = current.value.answers
      lastFollowUpAnswer.value = null
      return current.value
    } catch (e) {
      error.value = extractApiError(e, 'Не удалось загрузить Tarot+ сессию')
      throw e
    } finally {
      loading.value = false
    }
  }

  async function loadHistory() {
    loading.value = true
    error.value = null
    try {
      history.value = await tarotPlusApi.history()
      return history.value
    } catch (e) {
      error.value = extractApiError(e, 'Не удалось загрузить историю Tarot+')
      throw e
    } finally {
      loading.value = false
    }
  }

  async function addAnswer(sessionId: string, question: string, answer: string) {
    loading.value = true
    error.value = null
    try {
      current.value = await tarotPlusApi.addAnswer(sessionId, { question, answer })
      answers.value = current.value.answers
      await loadNextStep(sessionId)
      return current.value
    } catch (e) {
      error.value = extractApiError(e, 'Не удалось сохранить ответ')
      throw e
    } finally {
      loading.value = false
    }
  }

  async function loadNextStep(sessionId: string) {
    error.value = null
    try {
      nextStep.value = await tarotPlusApi.getNextStep(sessionId)
      nextQuestion.value = nextStep.value.question
      return nextStep.value
    } catch (e) {
      error.value = extractApiError(e, 'Не удалось получить следующий вопрос')
      throw e
    }
  }

  async function generateReport(sessionId: string) {
    reportLoading.value = true
    error.value = null
    try {
      const result = await tarotPlusApi.generateReport(sessionId)
      current.value = result.session
      answers.value = result.session.answers
      lastFollowUpAnswer.value = null
      return result
    } catch (e) {
      error.value = extractApiError(e, 'Не удалось собрать отчёт')
      throw e
    } finally {
      reportLoading.value = false
    }
  }

  async function askFollowUp(sessionId: string, question: string) {
    followUpLoading.value = true
    error.value = null
    try {
      const result = await tarotPlusApi.askFollowUp(sessionId, question)
      current.value = result.session
      lastFollowUpAnswer.value = result.answerMarkdown
      return result
    } catch (e) {
      error.value = extractApiError(e, 'Не удалось отправить уточняющий вопрос')
      throw e
    } finally {
      followUpLoading.value = false
    }
  }

  function reset() {
    current.value = null
    history.value = []
    loading.value = false
    paymentLoading.value = false
    reportLoading.value = false
    followUpLoading.value = false
    error.value = null
    answers.value = []
    nextQuestion.value = null
    nextStep.value = null
    lastFollowUpAnswer.value = null
  }

  return {
    current,
    history,
    loading,
    paymentLoading,
    reportLoading,
    followUpLoading,
    error,
    answers,
    nextQuestion,
    nextStep,
    lastFollowUpAnswer,
    createPreview,
    createPayment,
    load,
    loadHistory,
    addAnswer,
    loadNextStep,
    generateReport,
    askFollowUp,
    reset,
  }
})
