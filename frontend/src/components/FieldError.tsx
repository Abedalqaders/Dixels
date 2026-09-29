import { useEffect, useState } from 'react'

/**
 * One way to show a problem with a field, for every form: the message sits under the
 * field, the field is marked invalid (red border), the two are linked for screen readers,
 * and on submit focus jumps to the first field that needs fixing. Cross-field problems
 * (a time against a room's rules, a clash) don't belong here — they get a panel.
 *
 *   const f = useFieldErrors<'name' | 'capacity'>('add')
 *   <form {...f.form} onSubmit={…}>
 *     <label htmlFor={f.id('name')}>Name</label>
 *     <input {...f.field('name')} />
 *     {f.error('name')}
 *
 * On submit: `f.setErrors({ name: 'Name is required.' })` — or `f.clear()` when all is well.
 */
export function useFieldErrors<Name extends string>(prefix: string) {
  const [errors, setErrors] = useState<Partial<Record<Name, string>>>({})
  const formId = `${prefix}-form`

  // After a failed submit, land on the first field with a message — reading order, which
  // is the order the fields appear in the form.
  useEffect(() => {
    if (Object.keys(errors).length === 0) return
    document.getElementById(formId)?.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus()
  }, [errors, formId])

  const id = (name: Name) => `${prefix}-${name}`
  const errorId = (name: Name) => `${prefix}-${name}-error`

  return {
    errors,
    /** Spread onto the <form>: how the hook finds it when focus has to move. */
    form: { id: formId },
    hasErrors: Object.keys(errors).length > 0,
    setErrors,
    clear: () => setErrors({}),
    id,
    /** Spread onto the input: its id, and the invalid marking + link when it has a message. */
    field: (name: Name) => ({
      id: id(name),
      'aria-invalid': errors[name] ? true : undefined,
      'aria-describedby': errors[name] ? errorId(name) : undefined,
    }),
    /** The message under the field, or nothing. */
    error: (name: Name) => (errors[name] ? <FieldError id={errorId(name)} message={errors[name]!} /> : null),
  }
}

/** The line under a field. Use through useFieldErrors; on its own only for a one-off. */
export function FieldError({ id, message }: { id?: string; message: string }) {
  return (
    <p id={id} role="alert" className="mt-1 text-sm text-destructive">
      {message}
    </p>
  )
}
