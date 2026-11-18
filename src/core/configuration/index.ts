export interface ConfigurationService {
  get<T>(key: string): Promise<T | undefined>;
  set<T>(key: string, value: T): Promise<void>;
  delete(key: string): Promise<void>;
  getSecretReference(key: string): Promise<string | undefined>;
}
