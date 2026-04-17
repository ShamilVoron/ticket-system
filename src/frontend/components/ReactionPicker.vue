<template>
  <div
    v-if="open"
    ref="panelRef"
    class="z-[100] w-56 max-h-64 rounded-xl border border-gray-200 dark:border-zinc-600 bg-white dark:bg-zinc-800 shadow-xl p-2"
    :style="floatingStyle"
  >
    <div ref="scrollRef" class="max-h-56 overflow-y-auto pr-0.5">
      <div class="grid grid-cols-5 gap-1">
        <button
          v-for="e in TUX_EMOJI_PACK"
          :key="e.id"
          type="button"
          class="flex items-center justify-center h-9 rounded-lg hover:bg-gray-100 dark:hover:bg-zinc-700 transition-colors shrink-0"
          :title="e.name"
          @click="select(e.id)"
        >
          <TuxEmoji :url="e.url" :size="22" />
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount, computed, watch } from 'vue'
import { TUX_EMOJI_PACK } from '~/config/emojiPack'

const props = defineProps<{
  open: boolean
  anchorEl?: HTMLElement | null
}>()

const emit = defineEmits<{
  (e: 'select', emojiId: string): void
  (e: 'close'): void
}>()

const panelRef = ref<HTMLDivElement | null>(null)
const scrollRef = ref<HTMLDivElement | null>(null)
const pos = ref({ left: 0, top: 0 })

function updatePos() {
  if (!props.anchorEl) return
  const rect = props.anchorEl.getBoundingClientRect()
  const panelHeight = panelRef.value?.offsetHeight ?? 220
  // Prefer below anchor; if not enough room, place above
  let top = rect.bottom + 4
  if (top + panelHeight > window.innerHeight - 8) {
    top = rect.top - panelHeight - 4
  }
  pos.value = {
    left: rect.left,
    top,
  }
}

watch(() => props.open, (v) => {
  if (v) {
    nextTick(() => updatePos())
  }
})

const floatingStyle = computed(() => ({
  position: 'fixed' as const,
  left: `${pos.value.left}px`,
  top: `${pos.value.top}px`,
}))

function select(emojiId: string) {
  emit('select', emojiId)
  emit('close')
}

function onDocClick(ev: MouseEvent) {
  if (!props.open) return
  const target = ev.target as Node
  if (panelRef.value && !panelRef.value.contains(target)) {
    emit('close')
  }
}

function onWheel(ev: WheelEvent) {
  const el = scrollRef.value
  if (!el) return
  const isScrollingUp = ev.deltaY < 0
  const isScrollingDown = ev.deltaY > 0
  const atTop = el.scrollTop <= 0
  const atBottom = el.scrollTop + el.clientHeight >= el.scrollHeight
  if ((isScrollingUp && !atTop) || (isScrollingDown && !atBottom)) {
    ev.stopPropagation()
  }
}

onMounted(() => {
  document.addEventListener('click', onDocClick, true)
  scrollRef.value?.addEventListener('wheel', onWheel, { passive: false })
})

onBeforeUnmount(() => {
  document.removeEventListener('click', onDocClick, true)
  scrollRef.value?.removeEventListener('wheel', onWheel)
})
</script>
