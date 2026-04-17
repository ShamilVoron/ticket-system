const _title = ref('')
const _showBack = ref(false)

export function usePageHeader() {
  function set(title: string, showBack = false) {
    _title.value = title
    _showBack.value = showBack
  }

  function clear() {
    _title.value = ''
    _showBack.value = false
  }

  return {
    title: readonly(_title),
    showBack: readonly(_showBack),
    set,
    clear,
  }
}
