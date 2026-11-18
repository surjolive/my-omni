export interface DesktopBridge {
  invoke<T>(channel: string, ...args: readonly unknown[]): Promise<T>;
  subscribe(channel: string, listener: (...args: readonly unknown[]) => void): () => void;
}

export interface PreloadService {
  exposeBridge(name: string, bridge: DesktopBridge): void;
}
