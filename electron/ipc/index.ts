export interface IpcRequest<T = unknown> {
  readonly channel: string;
  readonly payload: T;
}

export interface IpcRouter {
  handle<TRequest, TResponse>(channel: string, handler: (payload: TRequest) => Promise<TResponse>): () => void;
  send<T>(channel: string, payload: T): void;
}
