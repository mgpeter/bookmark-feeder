import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider } from 'react-router-dom'
import { ConfigProvider } from '@/config/config-context'
import { Toaster } from '@/components/ui/sonner'
import { setUnauthorizedHandler } from '@/lib/api-client'
import { router } from '@/routes'
import './index.css'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { retry: 1, staleTime: 30_000, refetchOnWindowFocus: false },
  },
})

// On a 401 from the API, send the user to Settings to fix the key.
setUnauthorizedHandler(() => {
  if (window.location.pathname !== '/settings') {
    window.location.assign('/settings')
  }
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ConfigProvider>
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
        <Toaster richColors />
      </QueryClientProvider>
    </ConfigProvider>
  </StrictMode>,
)
