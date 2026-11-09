const base = (import.meta.env.VITE_API_URL ?? '').replace(/\/$/, '');

export async function api<T>(path: string, options?: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`${base}/api${path}`, options);
  } catch {
    throw new Error('No se pudo conectar con la API. Verifica que el servidor esté ejecutándose.');
  }
  if (!response.ok) {
    const problem = await response.json().catch(() => null);
    throw new Error(
      problem?.detail ?? problem?.title ?? `La solicitud falló (${response.status}).`,
    );
  }
  return response.status === 204 ? (undefined as T) : response.json();
}

export const post = <T>(path: string, body: unknown) =>
  api<T>(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
