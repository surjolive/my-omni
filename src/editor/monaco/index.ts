export interface EditorOptions {
  readonly languageId: string;
  readonly readOnly: boolean;
  readonly fontSize: number;
}

export interface EditorHost {
  mount(containerId: string, options: EditorOptions): Promise<void>;
  setValue(text: string): void;
  getValue(): string;
  focus(): void;
  dispose(): void;
}
