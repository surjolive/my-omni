export interface ThemeDescriptor {
  readonly id: string;
  readonly label: string;
  readonly source: "built-in" | "user";
}

export interface ThemeService {
  list(): readonly ThemeDescriptor[];
  getActiveThemeId(): string;
  activate(themeId: string): Promise<void>;
}
