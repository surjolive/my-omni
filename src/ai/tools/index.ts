export interface ToolDescriptor {
  readonly id: string;
  readonly description: string;
  readonly requiresApproval: boolean;
}

export interface ToolService {
  list(): readonly ToolDescriptor[];
  invoke(toolId: string, input: unknown, approvalId?: string): Promise<unknown>;
}
