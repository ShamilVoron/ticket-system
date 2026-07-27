export type SystemBranding = {
  logoUrl: string
  accentColor: string
  companyName: string
  onboardingCompleted: boolean
  loaded: boolean
}

const DEFAULTS: SystemBranding = {
  logoUrl: '',
  accentColor: '',
  companyName: '',
  onboardingCompleted: false,
  loaded: false,
}

function parseBool(raw: string | undefined): boolean {
  const v = (raw || '').trim().toLowerCase()
  return v === 'true' || v === '1' || v === 'yes'
}

/** Cached system branding / onboarding flags from SystemSettings. */
export function useSystemBranding() {
  const branding = useState<SystemBranding>('systemBranding', () => ({ ...DEFAULTS }))
  const api = useApi()

  function applyAccent(color: string) {
    if (!import.meta.client) return
    const el = document.documentElement
    const c = (color || '').trim()
    if (c) {
      el.style.setProperty('--brand-accent', c)
    } else {
      el.style.removeProperty('--brand-accent')
    }
  }

  async function load(force = false) {
    if (branding.value.loaded && !force) {
      applyAccent(branding.value.accentColor)
      return branding.value
    }
    try {
      const settings = await api.systemSettings.getSettings()
      branding.value = {
        logoUrl: (settings.brand_logo_url || '').trim(),
        accentColor: (settings.brand_accent_color || '').trim(),
        companyName: (settings.company_name || '').trim(),
        onboardingCompleted: parseBool(settings.onboarding_completed),
        loaded: true,
      }
      applyAccent(branding.value.accentColor)
    } catch {
      branding.value = { ...DEFAULTS, loaded: true }
      applyAccent('')
    }
    return branding.value
  }

  return { branding, load, applyAccent }
}
