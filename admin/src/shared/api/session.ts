// A generation changes even when the same account signs in again.
let generation = 0;
export const advanceSession = () => {
  generation++;
};
export const sessionToken = () =>
  localStorage.getItem("auth_token") || sessionStorage.getItem("auth_token");

export function captureSession() {
  const started = generation;
  const token = sessionToken();
  return () => {
    if (started !== generation || token !== sessionToken()) {
      throw new DOMException("The session changed while the request was running.", "AbortError");
    }
  };
}
