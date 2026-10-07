import { Component } from 'react'
import type { ErrorInfo, ReactNode } from 'react'
import i18n from '@/i18n'

interface Props {
  /** 'page': the shell (sidebar) stays and only the page area shows the message.
   * 'app': nothing else survived; a full-screen message with a reload. */
  scope?: 'app' | 'page'
  children: ReactNode
}

interface State {
  error: Error | null
}

/**
 * The last line of defence: without a boundary, one exception while rendering (a malformed
 * booking, a date the grid can't place) unmounts the whole app to a white page. With it,
 * the person sees what happened and has a way back. Errors are logged to the console for
 * now; a reporting service plugs in at `componentDidCatch`.
 *
 * A class component because React only exposes error boundaries that way.
 */
export class AppErrorBoundary extends Component<Props, State> {
  state: State = { error: null }

  static getDerivedStateFromError(error: Error): State {
    return { error }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Unhandled error while rendering', error, info.componentStack)
  }

  render() {
    const { error } = this.state
    if (!error) return this.props.children

    return <ErrorMessage error={error} scope={this.props.scope ?? 'page'} onTryAgain={() => this.setState({ error: null })} />
  }
}

/** What the boundary shows; also the router's own error screen (app/RouteError). */
export function ErrorMessage({ error, scope, onTryAgain }: { error: Error; scope: 'app' | 'page'; onTryAgain?: () => void }) {
  const message = (
    <div role="alert" className="mx-auto max-w-md p-6 text-center">
      <h1 className="text-lg font-semibold">{i18n.t('Error:BoundaryTitle')}</h1>
      <p className="mt-2 text-sm text-muted-foreground">{i18n.t('Error:BoundaryDetail')}</p>
      <p className="mt-2 font-mono text-xs text-muted-foreground">{error.message}</p>
      <button
        type="button"
        className="mt-4 rounded-md border px-3 py-1.5 text-sm"
        onClick={() => (scope === 'app' ? globalThis.location.reload() : onTryAgain?.())}
      >
        {scope === 'app' ? i18n.t('Common:Reload') : i18n.t('Common:TryAgain')}
      </button>
    </div>
  )

  return scope === 'app' ? <div className="flex min-h-screen items-center justify-center">{message}</div> : message
}
