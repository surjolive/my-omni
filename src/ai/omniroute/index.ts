export interface OmniRouteModel {
  readonly id: string;
  readonly label: string;
}

export interface OmniRouteService {
  checkConnection(credentialReference: string): Promise<boolean>;
  listModels(credentialReference: string): Promise<readonly OmniRouteModel[]>;
  sendChat(credentialReference: string, modelId: string, messages: readonly ChatMessage[], signal?: AbortSignal): Promise<string>;
}

export interface ChatMessage {
  readonly role: "system" | "user" | "assistant";
  readonly content: string;
}
