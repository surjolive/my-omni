export interface EditorTab {
  readonly id: string;
  readonly path: string;
  readonly dirty: boolean;
}

export interface EditorTabsService {
  list(): readonly EditorTab[];
  open(path: string): Promise<EditorTab>;
  activate(tabId: string): void;
  save(tabId: string): Promise<void>;
  close(tabId: string, discardChanges?: boolean): Promise<boolean>;
}
