export interface EditorGroup {
  readonly id: string;
  readonly tabIds: readonly string[];
}

export interface SplitEditorService {
  listGroups(): readonly EditorGroup[];
  split(groupId: string, direction: "horizontal" | "vertical"): EditorGroup;
  moveTab(tabId: string, targetGroupId: string): void;
  closeGroup(groupId: string): void;
}
