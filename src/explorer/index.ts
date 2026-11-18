import type { WorkspaceEntry } from "../core/workspace/index.js";

export interface ExplorerService {
  listChildren(directoryPath: string, signal?: AbortSignal): Promise<readonly WorkspaceEntry[]>;
  refresh(directoryPath: string): Promise<void>;
  reveal(path: string): void;
}
