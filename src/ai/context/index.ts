export interface ContextItem {
  readonly id: string;
  readonly source: string;
  readonly text: string;
  readonly tokenEstimate: number;
}

export interface ContextService {
  collect(references: readonly string[], maxTokens: number, signal?: AbortSignal): Promise<readonly ContextItem[]>;
  clear(): void;
}
