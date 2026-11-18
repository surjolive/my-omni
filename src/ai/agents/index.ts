export interface AgentPlan {
  readonly id: string;
  readonly summary: string;
  readonly proposedActions: readonly string[];
}

export interface AgentService {
  createPlan(goal: string, contextIds: readonly string[]): Promise<AgentPlan>;
  executePlan(planId: string, approvalId: string): Promise<void>;
  cancel(planId: string): Promise<void>;
}
