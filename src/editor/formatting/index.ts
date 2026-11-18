export interface Formatter {
  format(text: string, languageId: string, signal?: AbortSignal): Promise<string>;
}

export interface FormattingService {
  register(languageId: string, formatter: Formatter): () => void;
  formatDocument(text: string, languageId: string, signal?: AbortSignal): Promise<string>;
}
