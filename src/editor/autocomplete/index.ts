export interface CompletionItem {
  readonly label: string;
  readonly insertText: string;
  readonly detail?: string;
}

export interface CompletionProvider {
  provide(text: string, cursorOffset: number, signal?: AbortSignal): Promise<readonly CompletionItem[]>;
}

export interface AutocompleteService {
  register(languageId: string, provider: CompletionProvider): () => void;
  complete(languageId: string, text: string, cursorOffset: number, signal?: AbortSignal): Promise<readonly CompletionItem[]>;
}
