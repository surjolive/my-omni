export interface OllamaModel {
  readonly name: string;
  readonly sizeBytes: number;
}

export interface OllamaService {
  checkConnection(baseUrl: string): Promise<boolean>;
  listModels(baseUrl: string): Promise<readonly OllamaModel[]>;
  sendChat(baseUrl: string, modelName: string, messages: readonly OllamaMessage[], signal?: AbortSignal): Promise<string>;
}

export interface OllamaMessage {
  readonly role: "system" | "user" | "assistant";
  readonly content: string;
}
