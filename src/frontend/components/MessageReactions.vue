<template>
  <div class="flex flex-wrap items-center gap-1.5 mt-1.5">
    <button
      v-for="group in groups"
      :key="group.emojiId"
      type="button"
      class="inline-flex items-center gap-1 px-1.5 py-0.5 rounded-full text-sm border transition-colors"
      :class="
        group.hasMe
          ? 'bg-indigo-50 dark:bg-indigo-500/20 border-indigo-200 dark:border-indigo-500/40'
          : 'bg-white dark:bg-zinc-800 border-gray-200 dark:border-zinc-600 hover:bg-gray-50 dark:hover:bg-zinc-700'
      "
      :title="group.names"
      @click="toggle(group.emojiId)"
    >
      <TuxEmoji :url="group.url" :size="16" />
      <span class="text-xs font-medium text-gray-700 dark:text-zinc-300">{{ group.count }}</span>
    </button>

    <div class="relative">
      <button
        v-if="canAdd"
        ref="addBtnRef"
        type="button"
        class="inline-flex items-center justify-center w-7 h-7 rounded-full border border-gray-200 dark:border-zinc-600 text-gray-400 dark:text-zinc-500 hover:bg-gray-50 dark:hover:bg-zinc-700 hover:text-gray-600 dark:hover:text-zinc-300 transition-colors"
        title="Добавить реакцию"
        @click.stop="pickerOpen = !pickerOpen"
      >
        <span class="text-lg leading-none">+</span>
      </button>
      <ReactionPicker
        :open="pickerOpen"
        :anchor-el="addBtnRef"
        @select="toggle"
        @close="pickerOpen = false"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { TUX_EMOJI_PACK } from '~/config/emojiPack'

interface ReactionItem {
  emoji: string // id из TUX_EMOJI_PACK
  userId: string
  userName: string
}

const props = defineProps<{
  reactions: ReactionItem[]
  currentUserId: string
  canAdd?: boolean
}>()

const emit = defineEmits<{
  (e: 'toggle', emojiId: string): void
}>()

const pickerOpen = ref(false)
const addBtnRef = ref<HTMLElement | null>(null)

const groups = computed(() => {
  const map = new Map<string, { count: number; hasMe: boolean; names: string; url: string }>()
  for (const r of props.reactions || []) {
    const def = TUX_EMOJI_PACK.find((e) => e.id === r.emoji)
    const g = map.get(r.emoji)
    if (g) {
      g.count++
      if (r.userId === props.currentUserId) g.hasMe = true
    } else {
      map.set(r.emoji, {
        count: 1,
        hasMe: r.userId === props.currentUserId,
        names: r.userName,
        url: def?.url ?? '',
      })
    }
  }
  for (const [emojiId, g] of map) {
    const names = (props.reactions || [])
      .filter((x) => x.emoji === emojiId)
      .map((x) => x.userName)
      .join(', ')
    g.names = names
  }
  return Array.from(map.entries()).map(([emojiId, data]) => ({ emojiId, ...data }))
})

function toggle(emojiId: string) {
  emit('toggle', emojiId)
}
</script>
