/**
 * GENERATED — DO NOT EDIT (pnpm gen:api)
 */
import type { HttpValidationProblemDetailsErrors } from './httpValidationProblemDetailsErrors';

export interface HttpValidationProblemDetails {
  /** @nullable */
  type?: string | null;
  /** @nullable */
  title?: string | null;
  /** @nullable */
  status?: number | null;
  /** @nullable */
  detail?: string | null;
  /** @nullable */
  instance?: string | null;
  errors?: HttpValidationProblemDetailsErrors;
  /** @nullable */
  error_code?: string | null;
}
