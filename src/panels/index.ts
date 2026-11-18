export interface PanelDescriptor {
  readonly id: string;
  readonly title: string;
  readonly location: "sidebar" | "bottom" | "editor";
}

export interface PanelService {
  register(panel: PanelDescriptor): () => void;
  list(): readonly PanelDescriptor[];
  show(panelId: string): void;
  hide(panelId: string): void;
}
