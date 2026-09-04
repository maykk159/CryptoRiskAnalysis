import React from 'react';
import ReactDOM from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import App from './App.tsx';
import { ApiRequestError } from './services/api.ts';
import './index.css';

const shouldRetryQuery = (failureCount: number, error: unknown) => {
  if (failureCount >= 1) return false;
  if (error instanceof DOMException && error.name === 'AbortError') return false;

  // Client errors require user action or server-directed backoff. The API already
  // applies its own provider retries, so repeating 4xx responses only adds traffic.
  if (error instanceof ApiRequestError && error.status !== undefined) {
    return error.status >= 500;
  }

  return true;
};

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: shouldRetryQuery,
      staleTime: 60_000, // 1 minute — matches backend cache duration
      refetchOnWindowFocus: false, // don't re-fetch when switching tabs
    },
  },
});

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <QueryClientProvider client={queryClient}>
      <App />
    </QueryClientProvider>
  </React.StrictMode>
);
