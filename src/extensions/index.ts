export interface ExtensionDescriptor {
  readonly id: string;
  readonly version: string;
  readonly publisher: string;
  readonly trusted: boolean;
}

export interface ExtensionService {
  listInstalled(): Promise<readonly ExtensionDescriptor[]>;
  install(packageReference: string, approvalId: string): Promise<ExtensionDescriptor>;
  enable(extensionId: string): Promise<void>;
  disable(extensionId: string): Promise<void>;
  uninstall(extensionId: string, approvalId: string): Promise<void>;
}
