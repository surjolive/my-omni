export interface DebugSession {
  readonly id: string;
  readonly state: "launching" | "paused" | "running" | "ended";
}

export interface DebuggerService {
  launch(configurationId: string, approvalId: string): Promise<DebugSession>;
  continue(sessionId: string): Promise<void>;
  step(sessionId: string, kind: "into" | "over" | "out"): Promise<void>;
  stop(sessionId: string): Promise<void>;
}
