export interface DesktopApplication {
  start(): Promise<void>;
  quit(): Promise<void>;
}

export interface DesktopApplicationFactory {
  create(): DesktopApplication;
}
