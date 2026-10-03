#!/usr/bin/env python3
import os
import json
import html

DAYS_INFO = [
    {"num": 1, "dir": "Day01-CSharp-Memory", "title": "C# Memory Model, Span & ReadOnlySpan", "category": "Memory & Performance"},
    {"num": 2, "dir": "Day02-RefStruct-Memory", "title": "ref struct & Memory<T> in Async Pipelines", "category": "Memory & Performance"},
    {"num": 3, "dir": "Day03-Async-Await", "title": "Async/Await & AsyncStateMachine Compiler Lowering", "category": "Async & Concurrency"},
    {"num": 4, "dir": "Day04-Garbage-Collection", "title": "Garbage Collection Laboratory & Tuning", "category": "Memory & Performance"},
    {"num": 5, "dir": "Day05-Middleware-Kestrel", "title": "ASP.NET Core Middleware & Kestrel Server", "category": "Web Architecture"},
    {"num": 6, "dir": "Day06-Dependency-Injection", "title": "Dependency Injection Lifetimes & Captive Dependencies", "category": "Architecture & Patterns"},
    {"num": 7, "dir": "Day07-Minimal-APIs", "title": "Minimal APIs, Endpoint Filters & TypedResults", "category": "Web Architecture"},
    {"num": 8, "dir": "Day08-SQL-Indexes", "title": "PostgreSQL B-Tree Indexing & 8KB Page Layout", "category": "SQL & Relational DB"},
    {"num": 9, "dir": "Day09-SQL-Execution-Plans", "title": "SQL Execution Plans & Index Tuning", "category": "SQL & Relational DB"},
    {"num": 10, "dir": "Day10-Transactions-ACID", "title": "SQL Transactions, ACID & MVCC Isolation Anomaly Simulator", "category": "SQL & Relational DB"},
    {"num": 11, "dir": "Day11-EFCore-ChangeTracking", "title": "EF Core Change Tracking Mechanics & Optimization", "category": "EF Core & ORM"},
    {"num": 12, "dir": "Day12-EFCore-SplitQueries-Interceptors", "title": "EF Core Split Queries & DbCommandInterceptor", "category": "EF Core & ORM"},
    {"num": 13, "dir": "Day13-Dapper-Performance", "title": "High-Performance Data Access with Dapper & Rotated Array Search", "category": "Dapper & Performance"},
]

HTML_TEMPLATE = """<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Day {day_num:02d}: {day_title} | 100-Day .NET + AI Track</title>
  <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/themes/prism-tomorrow.min.css">
  <script src="https://cdn.jsdelivr.net/npm/marked/marked.min.js"></script>
  <script src="https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/prism.min.js"></script>
  <script src="https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/components/prism-csharp.min.js"></script>
  <script src="https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/components/prism-sql.min.js"></script>
  <script src="https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/components/prism-json.min.js"></script>
  <style>
    :root {{
      --bg-body: #0b0f19;
      --bg-card: #111827;
      --bg-card-hover: #1f2937;
      --bg-code: #0f172a;
      --border: #1e293b;
      --border-accent: #3b82f6;
      --text-main: #f8fafc;
      --text-muted: #94a3b8;
      --text-dim: #64748b;
      --accent: #38bdf8;
      --accent-glow: rgba(56, 189, 248, 0.15);
      --success: #10b981;
      --warning: #f59e0b;
    }}
    * {{
      box-sizing: border-box;
      margin: 0;
      padding: 0;
    }}
    body {{
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
      background-color: var(--bg-body);
      color: var(--text-main);
      line-height: 1.6;
      padding-bottom: 80px;
    }}
    a {{
      color: var(--accent);
      text-decoration: none;
    }}
    a:hover {{
      text-decoration: underline;
    }}
    header {{
      background: rgba(17, 24, 39, 0.95);
      backdrop-filter: blur(12px);
      border-bottom: 1px solid var(--border);
      position: sticky;
      top: 0;
      z-index: 50;
      padding: 12px 24px;
    }}
    .header-content {{
      max-width: 1400px;
      margin: 0 auto;
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
    }}
    .logo-area {{
      display: flex;
      align-items: center;
      gap: 12px;
    }}
    .badge {{
      font-size: 11px;
      font-weight: 700;
      padding: 4px 10px;
      border-radius: 9999px;
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }}
    .badge-day {{
      background: #1e3a8a;
      color: #93c5fd;
      border: 1px solid #3b82f6;
    }}
    .badge-category {{
      background: #064e3b;
      color: #6ee7b7;
      border: 1px solid #10b981;
    }}
    .nav-controls {{
      display: flex;
      align-items: center;
      gap: 8px;
    }}
    .nav-btn {{
      background: var(--bg-card);
      border: 1px solid var(--border);
      color: var(--text-main);
      padding: 6px 12px;
      border-radius: 6px;
      font-size: 13px;
      font-weight: 500;
      cursor: pointer;
      display: inline-flex;
      align-items: center;
      gap: 6px;
      transition: all 0.2s;
    }}
    .nav-btn:hover {{
      background: var(--bg-card-hover);
      border-color: var(--border-accent);
      text-decoration: none;
    }}
    select.day-select {{
      background: var(--bg-card);
      border: 1px solid var(--border);
      color: var(--text-main);
      padding: 6px 12px;
      border-radius: 6px;
      font-size: 13px;
      cursor: pointer;
      outline: none;
    }}
    .container {{
      max-width: 1400px;
      margin: 24px auto;
      padding: 0 24px;
    }}
    .hero {{
      background: linear-gradient(135deg, rgba(30, 41, 59, 0.7) 0%, rgba(15, 23, 42, 0.9) 100%);
      border: 1px solid var(--border);
      border-radius: 12px;
      padding: 24px 32px;
      margin-bottom: 24px;
      box-shadow: 0 4px 20px rgba(0, 0, 0, 0.4);
    }}
    .hero h1 {{
      font-size: 26px;
      font-weight: 800;
      color: #ffffff;
      margin-bottom: 8px;
    }}
    .hero p {{
      color: var(--text-muted);
      font-size: 15px;
    }}
    .tabs-nav {{
      display: flex;
      border-bottom: 1px solid var(--border);
      gap: 8px;
      margin-bottom: 24px;
      overflow-x: auto;
      padding-bottom: 2px;
    }}
    .tab-btn {{
      background: transparent;
      border: none;
      color: var(--text-muted);
      padding: 10px 18px;
      font-size: 14px;
      font-weight: 600;
      cursor: pointer;
      border-bottom: 2px solid transparent;
      display: flex;
      align-items: center;
      gap: 8px;
      white-space: nowrap;
      transition: all 0.2s;
    }}
    .tab-btn:hover {{
      color: var(--text-main);
    }}
    .tab-btn.active {{
      color: var(--accent);
      border-bottom-color: var(--accent);
    }}
    .tab-pane {{
      display: none;
    }}
    .tab-pane.active {{
      display: block;
      animation: fadeIn 0.2s ease-in-out;
    }}
    @keyframes fadeIn {{
      from {{ opacity: 0; transform: translateY(4px); }}
      to {{ opacity: 1; transform: translateY(0); }}
    }}
    .markdown-content {{
      background: var(--bg-card);
      border: 1px solid var(--border);
      border-radius: 12px;
      padding: 32px;
      line-height: 1.7;
    }}
    .markdown-content h1 {{ font-size: 24px; color: #fff; margin: 24px 0 16px; border-bottom: 1px solid var(--border); padding-bottom: 8px; }}
    .markdown-content h2 {{ font-size: 20px; color: #38bdf8; margin: 24px 0 12px; border-bottom: 1px solid rgba(255,255,255,0.05); padding-bottom: 6px; }}
    .markdown-content h3 {{ font-size: 17px; color: #f1f5f9; margin: 20px 0 8px; }}
    .markdown-content p {{ margin-bottom: 16px; color: #cbd5e1; }}
    .markdown-content ul, .markdown-content ol {{ margin-bottom: 16px; padding-left: 24px; color: #cbd5e1; }}
    .markdown-content li {{ margin-bottom: 6px; }}
    .markdown-content code {{
      background: rgba(56, 189, 248, 0.1);
      color: #38bdf8;
      padding: 2px 6px;
      border-radius: 4px;
      font-size: 13px;
      font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
    }}
    .markdown-content pre {{
      background: var(--bg-code);
      padding: 16px;
      border-radius: 8px;
      overflow-x: auto;
      margin-bottom: 20px;
      border: 1px solid var(--border);
    }}
    .markdown-content pre code {{
      background: transparent;
      padding: 0;
      color: inherit;
      font-size: 13px;
    }}
    .markdown-content table {{
      width: 100%;
      border-collapse: collapse;
      margin-bottom: 20px;
      border: 1px solid var(--border);
      border-radius: 8px;
      overflow: hidden;
    }}
    .markdown-content th, .markdown-content td {{
      padding: 10px 14px;
      border: 1px solid var(--border);
      text-align: left;
    }}
    .markdown-content th {{
      background: rgba(30, 41, 59, 0.8);
      color: #fff;
      font-weight: 600;
    }}
    .markdown-content tr:nth-child(even) {{
      background: rgba(15, 23, 42, 0.4);
    }}
    .markdown-content blockquote {{
      border-left: 4px solid var(--accent);
      padding: 8px 16px;
      background: var(--accent-glow);
      border-radius: 0 8px 8px 0;
      margin-bottom: 16px;
      color: #e2e8f0;
    }}
    .file-card {{
      background: var(--bg-card);
      border: 1px solid var(--border);
      border-radius: 10px;
      margin-bottom: 20px;
      overflow: hidden;
    }}
    .file-header {{
      background: #1e293b;
      padding: 10px 18px;
      display: flex;
      align-items: center;
      justify-content: space-between;
      border-bottom: 1px solid var(--border);
    }}
    .file-title {{
      font-family: ui-monospace, SFMono-Regular, monospace;
      font-size: 13px;
      font-weight: 600;
      color: #38bdf8;
      display: flex;
      align-items: center;
      gap: 8px;
    }}
    .copy-btn {{
      background: #334155;
      border: none;
      color: #f1f5f9;
      padding: 4px 10px;
      border-radius: 4px;
      font-size: 12px;
      cursor: pointer;
      transition: background 0.2s;
    }}
    .copy-btn:hover {{
      background: #475569;
    }}
    pre[class*="language-"] {{
      margin: 0 !important;
      border-radius: 0 !important;
      background: var(--bg-code) !important;
      padding: 16px !important;
      font-size: 13px !important;
      line-height: 1.5 !important;
    }}
    .footer-bar {{
      text-align: center;
      margin-top: 40px;
      color: var(--text-dim);
      font-size: 13px;
    }}
  </style>
</head>
<body>

  <header>
    <div class="header-content">
      <div class="logo-area">
        <a href="../index.html" class="nav-btn">🏠 Home Hub</a>
        <span class="badge badge-day">Day {day_num:02d}</span>
        <span class="badge badge-category">{day_category}</span>
        <span style="font-weight: 700; font-size: 15px;">100-Day .NET + AI Engineering Track</span>
      </div>
      <div class="nav-controls">
        {prev_link}
        <select class="day-select" onchange="if(this.value) window.location.href=this.value">
          {select_options}
        </select>
        {next_link}
      </div>
    </div>
  </header>

  <div class="container">
    <div class="hero">
      <h1>Day {day_num:02d}: {day_title}</h1>
      <p>Practical Engineering Implementations, Deep Systems Mechanics, DSA & Production Benchmarks</p>
    </div>

    <div class="tabs-nav">
      <button class="tab-btn active" onclick="switchTab('readme')">📑 Module README</button>
      {notes_tab_btn}
      {interview_tab_btn}
      <button class="tab-btn" onclick="switchTab('source')">💻 Production Source ({source_files_count} files)</button>
      <button class="tab-btn" onclick="switchTab('tests')">🧪 Unit Tests ({test_files_count} files)</button>
    </div>

    <!-- Tab Panes -->
    <div id="pane-readme" class="tab-pane active">
      <div class="markdown-content" id="readme-container">
        <!-- Rendered by Marked.js -->
      </div>
    </div>

    {notes_tab_pane}
    {interview_tab_pane}

    <div id="pane-source" class="tab-pane">
      {source_files_html}
    </div>

    <div id="pane-tests" class="tab-pane">
      {test_files_html}
    </div>

    <div class="footer-bar">
      <p>100-Day .NET + AI Engineering Track • Authoritative Curriculum: <a href="https://learning-ttf6.onrender.com/" target="_blank">LearningOS</a> • Code Repository: <a href="https://github.com/shatru123/LearningTrack" target="_blank">GitHub</a></p>
    </div>
  </div>

  <!-- Raw Content Payloads for Client-side Rendering -->
  <script type="text/markdown" id="raw-readme">{raw_readme}</script>
  {raw_notes_script}
  {raw_interview_script}

  <script>
    // Tab switching
    function switchTab(tabId) {{
      document.querySelectorAll('.tab-btn').forEach(btn => btn.classList.remove('active'));
      document.querySelectorAll('.tab-pane').forEach(pane => pane.classList.remove('active'));

      const btn = Array.from(document.querySelectorAll('.tab-btn')).find(b => b.getAttribute('onclick') && b.getAttribute('onclick').includes(tabId));
      if (btn) btn.classList.add('active');

      const pane = document.getElementById('pane-' + tabId);
      if (pane) pane.classList.add('active');
    }}

    // Copy to clipboard
    function copyCode(btn, codeId) {{
      const codeEl = document.getElementById(codeId);
      if (!codeEl) return;
      navigator.clipboard.writeText(codeEl.innerText).then(() => {{
        const orig = btn.innerText;
        btn.innerText = 'Copied! ✓';
        btn.style.background = '#059669';
        setTimeout(() => {{
          btn.innerText = orig;
          btn.style.background = '#334155';
        }}, 2000);
      }});
    }}

    // Render Markdown when DOM is loaded
    document.addEventListener('DOMContentLoaded', () => {{
      if (window.marked) {{
        const rEl = document.getElementById('raw-readme');
        if (rEl) {{
          document.getElementById('readme-container').innerHTML = marked.parse(rEl.textContent);
        }}
        const nEl = document.getElementById('raw-notes');
        if (nEl) {{
          const nTarget = document.getElementById('notes-container');
          if (nTarget) nTarget.innerHTML = marked.parse(nEl.textContent);
        }}
        const iEl = document.getElementById('raw-interview');
        if (iEl) {{
          const iTarget = document.getElementById('interview-container');
          if (iTarget) iTarget.innerHTML = marked.parse(iEl.textContent);
        }}
      }}
      if (window.Prism) {{
        Prism.highlightAll();
      }}
    }});
  </script>
</body>
</html>
"""

INDEX_HUB_TEMPLATE = """<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>100-Day .NET + AI Engineering Track | Knowledge Hub</title>
  <style>
    :root {
      --bg-body: #0b0f19;
      --bg-card: #111827;
      --bg-card-hover: #1f2937;
      --border: #1e293b;
      --border-accent: #3b82f6;
      --text-main: #f8fafc;
      --text-muted: #94a3b8;
      --accent: #38bdf8;
      --success: #10b981;
    }
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body {
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
      background: var(--bg-body);
      color: var(--text-main);
      padding: 32px 24px 80px;
      line-height: 1.6;
    }
    .container { max-width: 1300px; margin: 0 auto; }
    header {
      text-align: center;
      margin-bottom: 40px;
    }
    h1 {
      font-size: 32px;
      font-weight: 800;
      color: #fff;
      margin-bottom: 12px;
      background: linear-gradient(135deg, #38bdf8, #818cf8);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
    }
    p.lead {
      color: var(--text-muted);
      font-size: 16px;
      max-width: 800px;
      margin: 0 auto 20px;
    }
    .stats-bar {
      display: flex;
      justify-content: center;
      gap: 16px;
      flex-wrap: wrap;
      margin-bottom: 36px;
    }
    .stat-pill {
      background: var(--bg-card);
      border: 1px solid var(--border);
      padding: 8px 18px;
      border-radius: 9999px;
      font-size: 13px;
      font-weight: 600;
      color: var(--text-main);
    }
    .stat-pill span { color: var(--accent); }
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(360px, 1fr));
      gap: 20px;
    }
    .day-card {
      background: var(--bg-card);
      border: 1px solid var(--border);
      border-radius: 12px;
      padding: 24px;
      transition: all 0.25s ease;
      display: flex;
      flex-direction: column;
      justify-content: space-between;
      text-decoration: none;
      color: inherit;
    }
    .day-card:hover {
      transform: translateY(-4px);
      border-color: var(--border-accent);
      box-shadow: 0 10px 25px rgba(0, 0, 0, 0.4);
      background: var(--bg-card-hover);
    }
    .card-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 12px;
    }
    .day-badge {
      font-size: 12px;
      font-weight: 700;
      background: #1e3a8a;
      color: #93c5fd;
      border: 1px solid #3b82f6;
      padding: 3px 10px;
      border-radius: 9999px;
    }
    .category-badge {
      font-size: 11px;
      font-weight: 600;
      background: #064e3b;
      color: #6ee7b7;
      padding: 3px 8px;
      border-radius: 6px;
    }
    h2.card-title {
      font-size: 18px;
      font-weight: 700;
      color: #f1f5f9;
      margin-bottom: 10px;
    }
    p.card-desc {
      color: var(--text-muted);
      font-size: 13px;
      margin-bottom: 18px;
      flex-grow: 1;
    }
    .card-footer {
      display: flex;
      justify-content: space-between;
      align-items: center;
      border-top: 1px solid var(--border);
      padding-top: 14px;
      font-size: 12px;
      color: var(--accent);
      font-weight: 600;
    }
    .view-btn {
      display: inline-flex;
      align-items: center;
      gap: 4px;
    }
  </style>
</head>
<body>
  <div class="container">
    <header>
      <h1>100-Day .NET + AI Engineering Track</h1>
      <p class="lead">Complete knowledge repository containing all daily architectural guides, technical notes, senior interview questions, C# source implementations, unit tests, and DSA problem solvers.</p>
      <div class="stats-bar">
        <div class="stat-pill">Completed: <span>13 / 100 Days</span></div>
        <div class="stat-pill">Test Suite: <span>149 / 149 Passed (100%)</span></div>
        <div class="stat-pill">Platform: <span>.NET 8 / C# 12</span></div>
        <div class="stat-pill">Streak: <span>13 Days Active</span></div>
      </div>
    </header>

    <div class="grid">
      <!--DAY_CARDS-->
    </div>
  </div>
</body>
</html>
"""

def read_file_safely(path):
    if os.path.exists(path):
        with open(path, "r", encoding="utf-8", errors="replace") as f:
            return f.read()
    return ""

def generate_file_cards(base_dir, sub_dir, prefix):
    folder_path = os.path.join(base_dir, sub_dir)
    if not os.path.exists(folder_path):
        return [], 0

    cards = []
    file_count = 0
    idx = 0

    for root, _, files in sorted(os.walk(folder_path)):
        if "/obj" in root or "/bin" in root or "\\obj" in root or "\\bin" in root:
            continue
        for file in sorted(files):
            if file.endswith(".cs"):
                file_count += 1
                full_path = os.path.join(root, file)
                rel_path = os.path.relpath(full_path, base_dir)
                code_content = read_file_safely(full_path)
                code_id = f"{prefix}-code-{idx}"
                escaped_code = html.escape(code_content)

                card_html = f"""
                <div class="file-card">
                  <div class="file-header">
                    <span class="file-title">📄 {rel_path}</span>
                    <button class="copy-btn" onclick="copyCode(this, '{code_id}')">Copy Code</button>
                  </div>
                  <pre class="language-csharp"><code id="{code_id}" class="language-csharp">{escaped_code}</code></pre>
                </div>
                """
                cards.append(card_html)
                idx += 1

    return cards, file_count

def build_days_html():
    root_dir = os.path.abspath(".")
    print(f"Generating HTML files for Days 1 to {len(DAYS_INFO)} in {root_dir}...")

    select_options = ""
    for d in DAYS_INFO:
        select_options += f'<option value="../{d["dir"]}/index.html">Day {d["num"]:02d}: {d["title"][:30]}...</option>\n'

    day_cards_html = ""

    for i, day in enumerate(DAYS_INFO):
        day_num = day["num"]
        day_dir = day["dir"]
        day_title = day["title"]
        day_category = day["category"]
        abs_day_dir = os.path.join(root_dir, day_dir)

        # Nav links
        prev_link = ""
        if i > 0:
            prev_day = DAYS_INFO[i - 1]
            prev_link = f'<a href="../{prev_day["dir"]}/index.html" class="nav-btn">← Day {prev_day["num"]:02d}</a>'
        else:
            prev_link = '<span class="nav-btn" style="opacity:0.4; cursor:default;">← Start</span>'

        next_link = ""
        if i < len(DAYS_INFO) - 1:
            next_day = DAYS_INFO[i + 1]
            next_link = f'<a href="../{next_day["dir"]}/index.html" class="nav-btn">Day {next_day["num"]:02d} →</a>'
        else:
            next_link = '<span class="nav-btn" style="opacity:0.4; cursor:default;">Next Day →</span>'

        readme_content = read_file_safely(os.path.join(abs_day_dir, "README.md"))
        notes_content = read_file_safely(os.path.join(abs_day_dir, "notes.md"))
        interview_content = read_file_safely(os.path.join(abs_day_dir, "interview-questions.md"))

        notes_tab_btn = ""
        notes_tab_pane = ""
        raw_notes_script = ""
        if notes_content:
            notes_tab_btn = '<button class="tab-btn" onclick="switchTab(\'notes\')">📝 Deep-Dive Notes</button>'
            notes_tab_pane = """
            <div id="pane-notes" class="tab-pane">
              <div class="markdown-content" id="notes-container"></div>
            </div>
            """
            raw_notes_script = f'<script type="text/markdown" id="raw-notes">{html.escape(notes_content)}</script>'

        interview_tab_btn = ""
        interview_tab_pane = ""
        raw_interview_script = ""
        if interview_content:
            interview_tab_btn = '<button class="tab-btn" onclick="switchTab(\'interview\')">💡 Senior Interview Q&A</button>'
            interview_tab_pane = """
            <div id="pane-interview" class="tab-pane">
              <div class="markdown-content" id="interview-container"></div>
            </div>
            """
            raw_interview_script = f'<script type="text/markdown" id="raw-interview">{html.escape(interview_content)}</script>'

        source_cards, source_count = generate_file_cards(abs_day_dir, "src", f"d{day_num}-src")
        test_cards, test_count = generate_file_cards(abs_day_dir, "tests", f"d{day_num}-tests")

        source_files_html = "\n".join(source_cards) if source_cards else "<p>No source files found.</p>"
        test_files_html = "\n".join(test_cards) if test_cards else "<p>No test files found.</p>"

        day_html = HTML_TEMPLATE.format(
            day_num=day_num,
            day_title=html.escape(day_title),
            day_category=html.escape(day_category),
            prev_link=prev_link,
            next_link=next_link,
            select_options=select_options,
            source_files_count=source_count,
            test_files_count=test_count,
            notes_tab_btn=notes_tab_btn,
            interview_tab_btn=interview_tab_btn,
            notes_tab_pane=notes_tab_pane,
            interview_tab_pane=interview_tab_pane,
            source_files_html=source_files_html,
            test_files_html=test_files_html,
            raw_readme=html.escape(readme_content),
            raw_notes_script=raw_notes_script,
            raw_interview_script=raw_interview_script
        )

        out_path = os.path.join(abs_day_dir, "index.html")
        with open(out_path, "w", encoding="utf-8") as f:
            f.write(day_html)
        print(f"  ✓ Generated: {day_dir}/index.html ({source_count} src, {test_count} tests)")

        summary_preview = ""
        if readme_content:
            lines = [l.strip() for l in readme_content.splitlines() if l.strip() and not l.startswith("#")]
            summary_preview = " ".join(lines[:2])[:140] + "..." if lines else ""

        day_cards_html += f"""
        <a href="{day_dir}/index.html" class="day-card">
          <div>
            <div class="card-header">
              <span class="day-badge">Day {day_num:02d}</span>
              <span class="category-badge">{day_category}</span>
            </div>
            <h2 class="card-title">{day_title}</h2>
            <p class="card-desc">{html.escape(summary_preview)}</p>
          </div>
          <div class="card-footer">
            <span>{source_count + test_count} C# Files • Markdown Included</span>
            <span class="view-btn">Explore Day {day_num:02d} →</span>
          </div>
        </a>
        """

    hub_html = INDEX_HUB_TEMPLATE.replace("<!--DAY_CARDS-->", day_cards_html)
    hub_path = os.path.join(root_dir, "index.html")
    with open(hub_path, "w", encoding="utf-8") as f:
        f.write(hub_html)
    print("\n✓ Generated master Knowledge Hub: index.html")

if __name__ == "__main__":
    build_days_html()
