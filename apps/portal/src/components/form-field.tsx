import type { InputHTMLAttributes, SelectHTMLAttributes } from 'react';
import { cn } from '@vsite/ui';

interface FormFieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string;
  error?: string;
}

export function FormField({ label, error, id, className, ...props }: FormFieldProps) {
  return (
    <div>
      <label className="block text-sm font-medium" htmlFor={id}>
        {label}
      </label>
      <input
        id={id}
        className={cn('mt-1 w-full rounded-md border border-border px-3 py-2', className)}
        aria-invalid={Boolean(error)}
        {...props}
      />
      {error && (
        <p className="mt-1 text-sm text-destructive" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}

interface FormSelectProps extends SelectHTMLAttributes<HTMLSelectElement> {
  label: string;
  error?: string;
  options: { value: string; label: string }[];
}

export function FormSelect({ label, error, id, options, className, ...props }: FormSelectProps) {
  return (
    <div>
      <label className="block text-sm font-medium" htmlFor={id}>
        {label}
      </label>
      <select
        id={id}
        className={cn('mt-1 w-full rounded-md border border-border bg-background px-3 py-2', className)}
        aria-invalid={Boolean(error)}
        {...props}
      >
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
      {error && (
        <p className="mt-1 text-sm text-destructive" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}
