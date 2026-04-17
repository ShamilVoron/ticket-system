export interface EmojiDef {
  id: string
  name: string
  url: string
}

/** Tux-набор реакций. Файлы должны лежать в /public/emoji/tux/ (tux-001.webp … tux-200.webp). */
function buildPack(): EmojiDef[] {
  const known: Record<number, string> = {
    1: 'Tux',
    2: 'Wink',
    3: 'Like',
    4: 'Love',
    5: 'Laugh',
    6: 'Cool',
    7: 'Shh',
    8: 'Wow',
    9: 'Cry',
    10: 'Wave',
    11: 'Laptop',
    12: 'Read',
    13: 'Coffee',
    14: 'Party',
    15: 'Sleep',
    16: 'Angry',
    17: 'Heart Eyes',
    18: 'Hi',
    19: 'Confused',
    20: 'Pirate',
    21: 'Rain',
    22: 'Idea',
    23: 'Zen',
    24: 'Linux',
    25: 'Ice',
  }
  const arr: EmojiDef[] = []
  for (let i = 1; i <= 200; i++) {
    const num = i.toString().padStart(3, '0')
    arr.push({
      id: `tux${num}`,
      name: known[i] || `Tux ${num}`,
      url: `/emoji/tux/tux-${num}.webp`,
    })
  }
  return arr
}

export const TUX_EMOJI_PACK: EmojiDef[] = buildPack()
