export interface ViewHandle {
  readonly id: string;
  dispose(): void;
}

export interface UiService {
  mount(viewId: string, containerId: string): Promise<ViewHandle>;
  notify(message: string, severity: "info" | "warning" | "error"): void;
  requestConfirmation(title: string, details: string): Promise<string | undefined>;
}
