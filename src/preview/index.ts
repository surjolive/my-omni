export interface PreviewSession {
  readonly id: string;
  readonly sourcePath: string;
  readonly state: "loading" | "ready" | "failed";
}

export interface PreviewService {
  open(sourcePath: string): Promise<PreviewSession>;
  reload(sessionId: string): Promise<void>;
  close(sessionId: string): Promise<void>;
}
