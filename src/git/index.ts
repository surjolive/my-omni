export interface GitChange {
  readonly path: string;
  readonly indexStatus: string;
  readonly worktreeStatus: string;
}

export interface GitService {
  getChanges(): Promise<readonly GitChange[]>;
  stage(paths: readonly string[]): Promise<void>;
  unstage(paths: readonly string[]): Promise<void>;
  commit(message: string, approvalId: string): Promise<string>;
  checkout(reference: string, approvalId: string): Promise<void>;
}
