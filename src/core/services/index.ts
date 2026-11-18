export interface ServiceToken<T> {
  readonly name: string;
  readonly __type?: T;
}

export interface ServiceRegistry {
  register<T>(token: ServiceToken<T>, service: T): void;
  resolve<T>(token: ServiceToken<T>): T;
  has<T>(token: ServiceToken<T>): boolean;
}
