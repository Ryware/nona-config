export class NonaClientError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly method: string,
    public readonly url: string,
    public readonly responseBody?: string,
    public readonly cause?: unknown
  ) {
    super(message);
    this.name = "NonaClientError";
  }
}
