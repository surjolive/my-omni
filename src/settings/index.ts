export interface SettingsService {
  get<T>(key: string): T | undefined;
  update<T>(key: string, value: T): Promise<void>;
  reset(key: string): Promise<void>;
  onDidChange(listener: (key: string) => void): () => void;
}
