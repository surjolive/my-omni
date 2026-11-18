export interface WorkspaceLocation {
  readonly id: string;
  readonly path: string;
  readonly displayName: string;
}

export interface WorkspaceEntry {
  readonly path: string;
  readonly name: string;
  readonly kind: "file" | "directory";
}

export interface WorkspaceService {
  chooseWorkspace(): Promise<WorkspaceLocation | undefined>;
  listDirectory(path: string, signal?: AbortSignal): Promise<readonly WorkspaceEntry[]>;
  readTextFile(path: string, maxBytes: number): Promise<string>;
  writeTextFile(path: string, content: string, expectedVersion?: string): Promise<void>;
  createDirectory(path: string): Promise<void>;
  deleteEntry(path: string, approvalId: string): Promise<void>;
}