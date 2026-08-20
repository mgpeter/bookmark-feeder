import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider } from 'react-router-dom'
import { AuthProvider } from '@/auth/auth-provider'
import { ApiTokenBridge } from '@/auth/api-token-bridge'
import { Toaster } from '@/components/ui/sonner'
import { ApiError } from '@/lib/api-client'
import { router } from '@/routes'
import './index.css'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // A 401 already has a recovery path: the unauthorized handler restarts the Auth0 login.
      // Retrying one would fire that handler twice and race two redirects, so 401 is excluded
      // from the single retry everything else gets.
      retry: (failureCount, error) =>
        !(error instanceof ApiError && error.status === 401) && failureCount < 1,
      staleTime: 30_000,
      refetchOnWindowFocus: false,
    },
  },
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <AuthProvider>
      <ApiTokenBridge>
        <QueryClientProvider client={queryClient}>
          <RouterProvider router={router} />
          <Toaster richColors />
        </QueryClientProvider>
      </ApiTokenBridge>
    </AuthProvider>
  </StrictMode>,
)
