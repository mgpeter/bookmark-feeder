import {
  createContext,
  useCallback,
  useContext,
  useState,
  type ReactNode,
} from 'react'

export interface AppConfig {
  apiKey: string
}

interface ConfigContextValue extends AppConfig {
  isConfigured: boolean
  setConfig: (config: AppConfig) => void
  clear: () => void
}

const STORAGE_KEY = 'bookmarkfeeder.config'

const defaults: AppConfig = {
  apiKey: import.meta.env.VITE_API_KEY ?? '',
}

function load(): AppConfig {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (raw) return { ...defaults, ...(JSON.parse(raw) as Partial<AppConfig>) }
  } catch {
    /* ignore malformed storage */
  }
  return defaults
}

/** Read config outside React (used by the api-client). Source of truth is localStorage/env. */
export function getConfig(): AppConfig {
  return load()
}

const ConfigContext = createContext<ConfigContextValue | null>(null)

export function ConfigProvider({ children }: { children: ReactNode }) {
  const [config, setConfigState] = useState<AppConfig>(load)

  const setConfig = useCallback((next: AppConfig) => {
    setConfigState(next)
    localStorage.setItem(STORAGE_KEY, JSON.stringify(next))
  }, [])

  const clear = useCallback(() => {
    setConfigState({ apiKey: '' })
    localStorage.removeItem(STORAGE_KEY)
  }, [])

  const isConfigured = Boolean(config.apiKey)

  return (
    <ConfigContext.Provider value={{ ...config, isConfigured, setConfig, clear }}>
      {children}
    </ConfigContext.Provider>
  )
}

// eslint-disable-next-line react-refresh/only-export-components
export function useConfig(): ConfigContextValue {
  const ctx = useContext(ConfigContext)
  if (!ctx) throw new Error('useConfig must be used within a ConfigProvider')
  return ctx
}
