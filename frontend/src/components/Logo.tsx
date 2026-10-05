import logo from '@/assets/logo.png'
import logoLight from '@/assets/logo-light.png'

/**
 * The Dixels logo in the current theme. The dark purple wordmark vanishes on a dark
 * background, so dark mode uses a copy with a light wordmark (the mark keeps its colours).
 * Both images are rendered and base.css hides one: the theme is either the OS setting or
 * the toggle's choice on <html data-theme>, and CSS already resolves both — a <picture>
 * media query would only see the OS setting.
 */
export function Logo({ className }: { className: string }) {
  return (
    <>
      <img className={`${className} logo-forlight`} src={logo} alt="Dixels" />
      <img className={`${className} logo-fordark`} src={logoLight} alt="Dixels" />
    </>
  )
}
