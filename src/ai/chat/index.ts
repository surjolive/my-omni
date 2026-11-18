export interface Conversation {
  readonly id: string;
  readonly title: string;
  readonly messages: readonly ChatMessage[];
}

export interface ChatMessage {
  readonly role: "user" | "assistant";
  readonly content: string;
  readonly createdAt: string;
}

export interface ChatService {
  listConversations(): Promise<readonly Conversation[]>;
  send(conversationId: string, text: string, signal?: AbortSignal): Promise<ChatMessage>;
  deleteConversation(conversationId: string, approvalId: string): Promise<void>;
}
