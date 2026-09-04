import type { ApiResponse, RiskAnalysisResponse } from '../types/index';

// Reads from .env.local in development — prevents hardcoded localhost in production
const API_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5058/api';

export class ApiRequestError extends Error {
  readonly status?: number;

  constructor(message: string, status?: number) {
    super(message);
    this.name = 'ApiRequestError';
    this.status = status;
  }
}

export const getRiskAnalysis = async (
  assetId: string,
  days: number = 30,
  signal?: AbortSignal
): Promise<RiskAnalysisResponse> => {
  const query = new URLSearchParams({ days: String(days) });
  const response = await fetch(`${API_URL}/RiskAnalysis/${encodeURIComponent(assetId)}?${query}`, {
    headers: { Accept: 'application/json' },
    signal,
  });

  let payload: ApiResponse<RiskAnalysisResponse> | undefined;
  try {
    payload = (await response.json()) as ApiResponse<RiskAnalysisResponse>;
  } catch {
    if (!response.ok) {
      throw new ApiRequestError(`Request failed with status ${response.status}`, response.status);
    }
  }

  if (!response.ok || !payload?.succeeded || !payload.data) {
    throw new ApiRequestError(
      payload?.message ?? 'Failed to fetch risk analysis data',
      response.status
    );
  }

  return payload.data;
};

/**
 * Extracts a user-friendly error message from any error type.
 * Replaces the `catch (err: any)` anti-pattern with proper type narrowing
 * so TypeScript can actually check our error handling logic.
 */
export function getErrorMessage(err: unknown, assetName?: string): string {
  if (err instanceof ApiRequestError) {
    const { status } = err;

    if (status === 429) return 'API rate limit exceeded. Please wait a few seconds and try again.';

    if (status === 404)
      return `Crypto asset "${assetName ?? 'unknown'}" not found. Please select a different asset.`;

    return err.message;
  }

  if (err instanceof DOMException && err.name === 'AbortError') {
    return 'Request was cancelled.';
  }

  if (err instanceof TypeError || (err instanceof DOMException && err.name === 'NetworkError')) {
    return 'Failed to connect to the server. Please check your internet connection.';
  }

  if (err instanceof Error && err.message) {
    return err.message;
  }

  return 'Failed to connect to the server. Please check your internet connection.';
}
