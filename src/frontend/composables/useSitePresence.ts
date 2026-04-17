/**
 * Пользователь сейчас взаимодействует с вкладкой приложения (видимость + фокус окна).
 * Если false — вкладка в фоне, другое окно в фокусе или другой монитор без фокуса.
 */
export function isUserActiveOnSite(): boolean {
  if (typeof document === 'undefined') return false
  return document.visibilityState === 'visible' && document.hasFocus()
}
