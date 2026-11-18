export interface TerminalSession {
  readonly id: string;
  readonly state: "starting" | "running" | "exited";
  readonly exitCode?: number;
}

export interface TerminalService {
  create(cwd: string, approvalId: string): Promise<TerminalSession>;
  write(sessionId: string, input: string): Promise<void>;
  resize(sessionId: string, columns: number, rows: number): Promise<void>;
  terminate(sessionId: string): Promise<void>;
}
