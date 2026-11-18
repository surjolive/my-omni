export interface DesktopServices {
  readonly workspace: unknown;
  readonly configuration: unknown;
  readonly providers: unknown;
}

export interface DesktopServicesFactory {
  create(): Promise<DesktopServices>;
  dispose(): Promise<void>;
}
