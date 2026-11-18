export type ApplicationState = "starting" | "ready" | "shutting-down" | "stopped";

export interface ApplicationService {
  getState(): ApplicationState;
  start(): Promise<void>;
  requestShutdown(): Promise<boolean>;
}
