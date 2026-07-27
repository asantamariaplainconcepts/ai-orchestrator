import DOMPurify from "dompurify";
import { marked } from "marked";

/**
 * Renders a Story's description. The body is untrusted input from whatever repository a project
 * points at, so this is belt and braces (design D2): the parser is configured to emit no raw
 * HTML, AND the result goes through an allow-list sanitiser. Either alone is one config
 * regression away from an XSS, and the Mirror deliberately stores the body unsanitised (BR-008
 * — it must hold what the vendor holds), which makes render-time the only place this happens.
 */
export function renderStoryMarkdown(body: string): string {
  const html = marked.parse(body, { async: false, gfm: true, breaks: true });

  return DOMPurify.sanitize(html, {
    ALLOWED_TAGS: [
      "p",
      "br",
      "hr",
      "strong",
      "em",
      "del",
      "code",
      "pre",
      "blockquote",
      "ul",
      "ol",
      "li",
      "h1",
      "h2",
      "h3",
      "h4",
      "h5",
      "h6",
      "a",
      "table",
      "thead",
      "tbody",
      "tr",
      "th",
      "td",
    ],
    ALLOWED_ATTR: ["href", "title"],
    // http/https/mailto only: a javascript: or data: href is a script by another name.
    ALLOWED_URI_REGEXP: /^(?:https?|mailto):/i,
  });
}
