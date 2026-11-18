export interface SearchMatch {
  readonly path: string;
  readonly line: number;
  readonly column: number;
  readonly preview: string;
}

export interface ReplacementPreview {
  readonly id: string;
  readonly matchCount: number;
  readonly affectedPaths: readonly string[];
}

export interface SearchService {
  find(query: string, paths: readonly string[], signal?: AbortSignal): Promise<readonly SearchMatch[]>;
  previewReplace(query: string, replacement: string, paths: readonly string[]): Promise<ReplacementPreview>;
  applyReplace(previewId: string, approvalId: string): Promise<number>;
}
