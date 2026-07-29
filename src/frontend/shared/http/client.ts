/**
 * The one HTTP entry point. Paths are relative because the SPA is served same-origin by the
 * host in every environment — there is no API base URL to configure, and no CORS.
 */
export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    /**
     * The `detail` from the API's problem response, when it sent one (#124). Carried because
     * some refusals are the answer — "this deployment cannot store values, do X instead" is
     * useless if the screen replaces it with "something went wrong".
     */
    readonly detail?: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

/**
 * The API answers failures with RFC 7807; a body that is not one simply yields nothing.
 *
 * Two shapes, because the API emits two: a plain problem carries `detail`, while a validation
 * problem carries no detail at all and puts the messages under `errors`, keyed by code. Reading
 * only `detail` therefore threw away exactly the refusals that name what to fix.
 */
async function problemDetail(response: Response): Promise<string | undefined> {
  try {
    const body: unknown = await response.clone().json();
    if (!body || typeof body !== "object") return undefined;

    const { detail, errors } = body as { detail?: unknown; errors?: unknown };
    if (typeof detail === "string" && detail.length > 0) return detail;

    if (errors && typeof errors === "object") {
      const messages = Object.values(errors as Record<string, unknown>)
        .flatMap((value) => (Array.isArray(value) ? value : [value]))
        .filter((value): value is string => typeof value === "string" && value.length > 0);
      if (messages.length > 0) return messages.join(" ");
    }

    return undefined;
  } catch {
    return undefined;
  }
}

async function request<TResponse>(path: string, init?: RequestInit): Promise<TResponse> {
  // `pnpm dev:mock` only. MODE is replaced at build time, so production builds dead-code
  // eliminate the branch AND the dynamically imported module — and the build asserts that by
  // grepping the emitted bundle (#95).
  if (import.meta.env.MODE === "mock") {
    const { mockRequest } = await import("./mock");
    return mockRequest<TResponse>(path, init);
  }

  const response = await fetch(path, {
    ...init,
    headers: { "Content-Type": "application/json", ...init?.headers },
  });

  if (!response.ok) {
    throw new ApiError(`Request to ${path} failed`, response.status, await problemDetail(response));
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
  delete: <TResponse>(path: string) => request<TResponse>(path, { method: "DELETE" }),
};
