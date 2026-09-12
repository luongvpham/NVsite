export interface PropertyPanelField {
  name: string;
  kind: string;
  control: string;
  label: string;
  group: string;
  order: number;
  editableInSystemPage: boolean;
  help?: string;
  maxLength?: number;
  multiline?: boolean;
  profile?: string;
  min?: number;
  max?: number;
  step?: number;
  options?: Array<{ value: string; label: string }>;
  preset?: string;
  allowKinds?: string[];
  sources?: string[];
  itemFields?: PropertyPanelField[];
  fields?: PropertyPanelField[];
}
