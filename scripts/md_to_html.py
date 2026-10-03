import re
import html

def render_inline(text):
    if not text:
        return ""

    # Replace inline code first with safe token without underscores
    code_spans = []
    def code_repl(m):
        code_spans.append(m.group(1))
        return f"XXCODESPAN{len(code_spans)-1}XX"
    
    text = re.sub(r'`([^`]+)`', code_repl, text)

    # Links: [text](url)
    text = re.sub(r'\[([^\]]+)\]\(([^)]+)\)', r'<a href="\2" target="_blank" rel="noopener noreferrer">\1</a>', text)

    # Bold: **text**
    text = re.sub(r'\*\*([^*]+)\*\*', r'<strong>\1</strong>', text)
    # Italic: *text*
    text = re.sub(r'(?<!\*)\*([^*]+)\*(?!\*)', r'<em>\1</em>', text)

    # Re-insert code spans with html escaping
    for idx, c in enumerate(code_spans):
        escaped_c = html.escape(c)
        text = text.replace(f"XXCODESPAN{idx}XX", f"<code>{escaped_c}</code>")

    return text

def render_markdown(md_text):
    if not md_text:
        return ""

    lines = md_text.splitlines()
    out = []

    in_code = False
    code_lang = ""
    code_lines = []

    in_list = False
    list_type = "ul"

    in_table = False
    table_header_done = False

    def close_list():
        nonlocal in_list, list_type
        if in_list:
            out.append(f"</{list_type}>")
            in_list = False

    def close_table():
        nonlocal in_table, table_header_done
        if in_table:
            out.append("</tbody></table></div>")
            in_table = False
            table_header_done = False

    for line in lines:
        stripped = line.strip()

        # Fenced code block
        if stripped.startswith("```"):
            if not in_code:
                close_list()
                close_table()
                in_code = True
                code_lang = stripped[3:].strip().lower()
                code_lines = []
            else:
                in_code = False
                escaped_code = html.escape("\n".join(code_lines))
                lang_cls = f' class="language-{code_lang}"' if code_lang else ' class="language-text"'
                out.append(f'<div class="code-block-container"><pre{lang_cls}><code{lang_cls}>{escaped_code}</code></pre></div>')
                code_lines = []
            continue

        if in_code:
            code_lines.append(line)
            continue

        # Blank line
        if not stripped:
            close_list()
            close_table()
            continue

        # Horizontal rule
        if stripped in ("---", "***", "___", "- - -", "* * *"):
            close_list()
            close_table()
            out.append("<hr/>")
            continue

        # Headers
        m_head = re.match(r'^(#{1,6})\s+(.*)$', stripped)
        if m_head:
            close_list()
            close_table()
            level = len(m_head.group(1))
            header_text = render_inline(m_head.group(2))
            out.append(f"<h{level}>{header_text}</h{level}>")
            continue

        # Blockquote
        if stripped.startswith(">"):
            close_list()
            close_table()
            quote_text = render_inline(stripped.lstrip(">").strip())
            out.append(f"<blockquote><p>{quote_text}</p></blockquote>")
            continue

        # Table row
        if stripped.startswith("|") and stripped.endswith("|"):
            close_list()
            cells = [c.strip() for c in stripped[1:-1].split("|")]
            # Check if separator row like |---|---|
            if all(re.match(r'^:?-+:?$', c) for c in cells if c):
                table_header_done = True
                continue

            if not in_table:
                in_table = True
                table_header_done = False
                out.append('<div class="table-container"><table><thead><tr>')
                for c in cells:
                    out.append(f"<th>{render_inline(c)}</th>")
                out.append("</tr></thead><tbody>")
            else:
                if not table_header_done:
                    out.append("<tr>")
                    for c in cells:
                        out.append(f"<th>{render_inline(c)}</th>")
                    out.append("</tr></thead><tbody>")
                    table_header_done = True
                else:
                    out.append("<tr>")
                    for c in cells:
                        out.append(f"<td>{render_inline(c)}</td>")
                    out.append("</tr>")
            continue
        else:
            close_table()

        # Unordered list item
        m_ul = re.match(r'^[*-]\s+(.*)$', stripped)
        if m_ul:
            close_table()
            if not in_list or list_type != "ul":
                close_list()
                in_list = True
                list_type = "ul"
                out.append("<ul>")
            item_text = render_inline(m_ul.group(1))
            out.append(f"<li>{item_text}</li>")
            continue

        # Ordered list item
        m_ol = re.match(r'^\d+\.\s+(.*)$', stripped)
        if m_ol:
            close_table()
            if not in_list or list_type != "ol":
                close_list()
                in_list = True
                list_type = "ol"
                out.append("<ol>")
            item_text = render_inline(m_ol.group(1))
            out.append(f"<li>{item_text}</li>")
            continue

        # Normal paragraph
        close_list()
        close_table()
        out.append(f"<p>{render_inline(stripped)}</p>")

    close_list()
    close_table()
    return "\n".join(out)
