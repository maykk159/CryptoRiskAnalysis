import type { RiskAnalysisResponse } from '../types';
import { isJsonObject, isRiskAnalysisResponse } from './riskAnalysisResponse';

const API_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5058/api';

export class ApiRequestError extends Error {
  readonly status?: number;
  readonly retryAt?: number;

  constructor(message: string, status?: number, retryAt?: number) {
    super(message);
    this.name = 'ApiRequestError';
    this.status = status;
    this.retryAt = retryAt;
  }
}

function readRetryAt(response: Response): number | undefined {
  const value = response.status === 429 ? response.headers.get('Retry-After')?.trim() : undefined;
  if (!value) return undefined;

  const now = Date.now();
  const retryAt = /^\d+$/.test(value)
    ? now + Number(value) * 1000
    : /^[a-z]{3,9}[, ]/i.test(value)
      ? Date.parse(value)
      : NaN;
  return Number.isSafeInteger(retryAt) && retryAt > now ? retryAt : undefined;
}

function readText(value: unknown): string | undefined {
  return typeof value === 'string' ? value.trim() || undefined : undefined;
}

function readErrorMessage(payload: unknown): string | undefined {
  if (!isJsonObject(payload)) return undefined;
  const { message, errors, detail, title } = payload;
  const envelopeMessage = readText(message);
  if (envelopeMessage) return envelopeMessage;

  if (isJsonObject(errors)) {
    const fieldMessages = Object.entries(errors).flatMap(([field, messages]) => {
      const description = Array.isArray(messages)
        ? messages.map(readText).filter(Boolean).join(' ')
        : '';
      if (!description) return [];
      return [field ? `${field} — ${description}` : description];
    });
    if (fieldMessages.length > 0) return fieldMessages.join(' ');
  }

  return readText(detail) ?? readText(title);
}

export const getRiskAnalysis = async (
  assetId: string,
  days: number = 30,
  signal?: AbortSignal,
  refresh = false
): Promise<RiskAnalysisResponse> => {
  const query = new URLSearchParams({ days: String(days) });
  if (refresh) query.set('refresh', 'true');
  const response = await fetch(`${API_URL}/RiskAnalysis/${encodeURIComponent(assetId)}?${query}`, {
    headers: { Accept: 'application/json' },
    signal,
  });
  const retryAt = readRetryAt(response);

  let payload: unknown;
  try {
    payload = await response.json();
  } catch {
    if (!response.ok) {
      throw new ApiRequestError(
        `Request failed with status ${response.status}`,
        response.status,
        retryAt
      );
    }
  }

  const envelope = isJsonObject(payload) ? payload : undefined;
  if (!response.ok || envelope?.succeeded === false) {
    throw new ApiRequestError(
      readErrorMessage(payload) ?? 'Failed to fetch risk analysis data',
      response.status,
      retryAt
    );
  }

  const data = envelope?.data;
  if (envelope?.succeeded !== true || !isRiskAnalysisResponse(data) || data.assetId !== assetId) {
    throw new ApiRequestError('The server returned invalid risk analysis data.', response.status);
  }

  return data;
};

export function getErrorMessage(err: unknown, assetName?: string): string {
  if (err instanceof ApiRequestError) {
    const { status } = err;

    if (status === 429)
      return err.retryAt
        ? 'API rate limit exceeded.'
        : 'API rate limit exceeded. Please wait and try again.';

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
