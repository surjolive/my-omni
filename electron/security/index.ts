export interface SecurityPolicy {
  validateRendererOrigin(origin: string): boolean;
  validateWorkspacePath(path: string, workspaceRoot: string): Promise<boolean>;
  authorizeOperation(operation: string, approvalId?: string): Promise<boolean>;
}

export interface SecurityService {
  getPolicy(): SecurityPolicy;
}
