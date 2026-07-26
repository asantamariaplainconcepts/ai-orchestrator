/**
 * The one HTTP entry point. Paths are relative because the SPA is served same-origin by the
 * host in every environment — there is no API base URL to configure, and no CORS.
 */
export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

async function request<TResponse>(path: string, init?: RequestInit): Promise<TResponse> {
  const response = await fetch(path, {
    ...init,
    headers: { "Content-Type": "application/json", ...init?.headers },
  });

  if (!response.ok) {
    throw new ApiError(`Request to ${path} failed`, response.status);
  }

  return response.status === 204
    ? (undefined as TResponse)
    : ((await response.json()) as TResponse);
}

export const api = {
  get: <TResponse>(path: string) => request<TResponse>(path),
  post: <TResponse>(path: string, body: unknown) =>
    request<TResponse>(path, { method: "POST", body: JSON.stringify(body) }),
  put: <TResponse>(path: string, body: unknown) =>
    request<TResponse>(path, { method: "PUT", body: JSON.stringify(body) }),
};
