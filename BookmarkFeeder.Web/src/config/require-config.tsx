import type { ReactNode } from 'react'
import { Navigate } from 'react-router-dom'
import { useConfig } from './config-context'

/** Redirects to Settings until an API base URL + key are configured. */
export function RequireConfig({ children }: { children: ReactNode }) {
  const { isConfigured } = useConfig()
  if (!isConfigured) return <Navigate to="/settings" replace />
  return <>{children}</>
}
