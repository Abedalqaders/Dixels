// Wraps the first case-insensitive match of `query` inside `text` in a <mark> — search
// results should show *why* they matched, not just that they did.

export function HighlightedText({ text, query }: { text: string; query: string }) {
  if (!query) return <>{text}</>

  const index = text.toLowerCase().indexOf(query.toLowerCase())
  if (index === -1) return <>{text}</>

  return (
    <>
      {text.slice(0, index)}
      <mark className="hl">{text.slice(index, index + query.length)}</mark>
      {text.slice(index + query.length)}
    </>
  )
}
