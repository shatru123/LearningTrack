#!/usr/bin/env python3
import os
import json
import html
import re

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
    {"num": 14, "dir": "Day14-EFCore-Migrations-Production", "title": "EF Core Migrations & Production Strategy", "category": "EF Core & DevOps"},
    {"num": 15, "dir": "Day15-Channels-Concurrency", "title": "Channels & High-Throughput Concurrency in C#", "category": ".NET & Concurrency"},
    {"num": 16, "dir": "Day16-Synchronization-Locks", "title": "Distributed Locking & Concurrency Primitives", "category": "Architecture & Systems"},
    {"num": 17, "dir": "Day17-Redis-Cache-Patterns", "title": "Introduction to Redis & Cache Patterns", "category": "Redis & Distributed Systems"},
    {"num": 18, "dir": "Day18-Redis-DataStructures-Eviction", "title": "Redis Data Structures & Memory Eviction Policies", "category": "Redis & Distributed Systems"},
    {"num": 19, "dir": "Day19-Redis-RedLock-DistributedLocking", "title": "Redis Distributed Locking with RedLock", "category": "Redis & Distributed Systems"},
    {"num": 20, "dir": "Day20-Redis-Streams-PubSub", "title": "Redis Pub/Sub & Redis Streams", "category": "Redis & Distributed Systems"},
    {"num": 21, "dir": "Day21-Distributed-Cache-Architecture", "title": "System Design: Distributed Cache Architecture", "category": "System Design & DSA"},
    {"num": 22, "dir": "Day22-Rate-Limiter-Design", "title": "System Design: Rate Limiter Design", "category": "System Design & Distributed Systems"},
    {"num": 23, "dir": "Day23-Url-Shortener-TinyUrl", "title": "System Design: URL Shortener (TinyURL)", "category": "System Design & Distributed Systems"},
]

from diagram_helpers import get_day_svg
from md_to_html import render_markdown

HTML_TEMPLATE = """<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Day {day_num:02d}: {day_title} | 100-Day .NET + AI Track</title>
  <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/themes/prism-tomorrow.min.css">
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
      --border-accent: #38bdf8;
      --text-main: #f8fafc;
      --text-muted: #94a3b8;
      --text-dim: #64748b;
      --accent: #38bdf8;
      --accent-glow: rgba(56, 189, 248, 0.15);
      --success: #10b981;
      --warning: #f59e0b;
      --purple: #c084fc;
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
    .quick-highlight {{
      display: flex;
      flex-wrap: wrap;
      gap: 12px;
      margin-top: 14px;
    }}
    .highlight-pill {{
      background: rgba(56, 189, 248, 0.1);
      border: 1px solid rgba(56, 189, 248, 0.3);
      color: #38bdf8;
      font-size: 12px;
      font-weight: 600;
      padding: 4px 12px;
      border-radius: 9999px;
      display: inline-flex;
      align-items: center;
      gap: 6px;
    }}
    .diagram-card {{
      background: var(--bg-card);
      border: 1px solid var(--border);
      border-radius: 12px;
      padding: 20px;
      margin-bottom: 24px;
      text-align: center;
      box-shadow: 0 4px 15px rgba(0,0,0,0.3);
    }}
    .diagram-title {{
      font-size: 14px;
      font-weight: 700;
      color: var(--accent);
      margin-bottom: 14px;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 8px;
    }}
    .tabs-nav {{
      display: flex;
      border-bottom: 1px solid var(--border);
      gap: 6px;
      margin-bottom: 24px;
      overflow-x: auto;
      padding-bottom: 2px;
      background: rgba(17, 24, 39, 0.5);
      padding: 6px 12px;
      border-radius: 10px;
    }}
    .tab-btn {{
      background: transparent;
      border: none;
      color: var(--text-muted);
      padding: 10px 18px;
      font-size: 14px;
      font-weight: 600;
      cursor: pointer;
      border-radius: 8px;
      display: flex;
      align-items: center;
      gap: 8px;
      white-space: nowrap;
      transition: all 0.2s;
    }}
    .tab-btn:hover {{
      color: var(--text-main);
      background: rgba(255, 255, 255, 0.05);
    }}
    .tab-btn.active {{
      color: #ffffff;
      background: #1e293b;
      border: 1px solid var(--border-accent);
      box-shadow: 0 2px 8px rgba(0,0,0,0.3);
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
    .markdown-content h2 {{ font-size: 20px; color: #38bdf8; margin: 28px 0 12px; border-bottom: 1px solid rgba(255,255,255,0.05); padding-bottom: 6px; }}
    .markdown-content h3 {{ font-size: 17px; color: #f1f5f9; margin: 20px 0 8px; }}
    .markdown-content p {{ margin-bottom: 16px; color: #cbd5e1; font-size: 15px; }}
    .markdown-content ul, .markdown-content ol {{ margin-bottom: 16px; padding-left: 24px; color: #cbd5e1; }}
    .markdown-content li {{ margin-bottom: 6px; font-size: 14.5px; }}
    .markdown-content code {{
      background: rgba(56, 189, 248, 0.12);
      color: #38bdf8;
      padding: 2px 6px;
      border-radius: 4px;
      font-size: 13px;
      font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
    }}
    .code-block-container {{
      position: relative;
      margin: 16px 0 20px;
      border-radius: 8px;
      overflow: hidden;
      border: 1px solid var(--border);
    }}
    .markdown-content pre {{
      background: var(--bg-code);
      padding: 18px;
      overflow-x: auto;
      font-size: 13.5px;
      line-height: 1.5;
      font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
    }}
    .markdown-content pre code {{
      background: transparent;
      padding: 0;
      color: #e2e8f0;
      font-size: 13.5px;
    }}
    .table-container {{
      overflow-x: auto;
      margin: 18px 0;
      border-radius: 8px;
      border: 1px solid var(--border);
    }}
    .markdown-content table {{
      width: 100%;
      border-collapse: collapse;
      font-size: 14px;
      text-align: left;
    }}
    .markdown-content th, .markdown-content td {{
      padding: 10px 14px;
      border: 1px solid var(--border);
    }}
    .markdown-content th {{
      background: #1e293b;
      color: #f8fafc;
      font-weight: 600;
    }}
    .markdown-content tr:nth-child(even) {{
      background: rgba(15, 23, 42, 0.5);
    }}
    .markdown-content blockquote {{
      border-left: 4px solid var(--accent);
      padding: 10px 18px;
      background: var(--accent-glow);
      border-radius: 0 8px 8px 0;
      margin: 16px 0;
      color: #e2e8f0;
    }}
    .markdown-content hr {{
      border: none;
      border-top: 1px solid var(--border);
      margin: 28px 0;
    }}
    /* QA Native Details Styles */
    .qa-controls {{
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 16px;
      flex-wrap: wrap;
      gap: 12px;
    }}
    details.qa-card {{
      background: var(--bg-card);
      border: 1px solid var(--border);
      border-radius: 10px;
      margin-bottom: 16px;
      overflow: hidden;
      transition: border-color 0.2s;
    }}
    details.qa-card:hover {{
      border-color: var(--border-accent);
    }}
    summary.qa-header {{
      padding: 16px 20px;
      background: #1e293b;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: space-between;
      user-select: none;
      list-style: none;
    }}
    summary.qa-header::-webkit-details-marker {{
      display: none;
    }}
    details.qa-card[open] summary.qa-header {{
      border-bottom: 1px solid var(--border);
    }}
    .qa-badge {{
      background: #1e3a8a;
      color: #93c5fd;
      border: 1px solid #3b82f6;
      font-size: 11px;
      font-weight: 700;
      padding: 3px 8px;
      border-radius: 9999px;
      flex-shrink: 0;
    }}
    .qa-title {{
      font-size: 15px;
      font-weight: 700;
      color: #f8fafc;
    }}
    .qa-toggle-icon {{
      font-size: 12px;
      color: var(--accent);
      transition: transform 0.2s;
    }}
    details.qa-card[open] .qa-toggle-icon {{
      transform: rotate(180deg);
    }}
    .qa-body {{
      padding: 24px;
      background: #111827;
      line-height: 1.7;
    }}
    /* Code Viewer Styles */
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
    .section-divider-title {{
      font-size: 20px;
      font-weight: 700;
      color: #38bdf8;
      margin: 32px 0 16px;
      padding-bottom: 8px;
      border-bottom: 1px solid var(--border);
      display: flex;
      align-items: center;
      gap: 8px;
    }}
    .footer-bar {{
      text-align: center;
      margin-top: 40px;
      color: var(--text-dim);
      font-size: 13px;
    }}
    noscript .tab-pane {{
      display: block !important;
      margin-bottom: 40px;
    }}
    noscript .tabs-nav {{
      display: none !important;
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
        <span style="font-weight: 700; font-size: 15px;">100-Day .NET + AI Track</span>
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
      <div class="quick-highlight">
        <span class="highlight-pill">📝 Deep-Dive Engineering Notes Included</span>
        <span class="highlight-pill">💡 10 Senior Interview Questions with Answers</span>
        <span class="highlight-pill">🖼️ Architectural Visual Model</span>
        <span class="highlight-pill">💻 {source_files_count} Source & {test_files_count} Test Files</span>
      </div>
    </div>

    <!-- Visual Architecture / 3D Diagram Card (Always Visible on Top) -->
    <div class="diagram-card">
      <div class="diagram-title">🖼️ Core Architectural Mechanics & Systems Visual Model</div>
      {day_svg}
    </div>

    <div class="tabs-nav">
      <button class="tab-btn active" onclick="switchTab('notes')">📝 Deep-Dive Notes</button>
      <button class="tab-btn" onclick="switchTab('readme')">📑 Module README</button>
      <button class="tab-btn" onclick="switchTab('interview')">💡 Senior Interview Q&A (10 Questions)</button>
      <button class="tab-btn" onclick="switchTab('source')">💻 Production Source ({source_files_count} files)</button>
      <button class="tab-btn" onclick="switchTab('tests')">🧪 Unit Tests ({test_files_count} files)</button>
      <button class="tab-btn" onclick="switchTab('all')">📖 All-in-One Full Guide</button>
    </div>

    <!-- Tab Panes -->
    <div id="pane-notes" class="tab-pane active">
      <div class="markdown-content" id="notes-container">
        {rendered_notes_html}
      </div>
    </div>

    <div id="pane-readme" class="tab-pane">
      <div class="markdown-content" id="readme-container">
        {rendered_readme_html}
      </div>
    </div>

    <div id="pane-interview" class="tab-pane">
      <div class="qa-controls">
        <span style="font-size: 13px; color: var(--text-muted); font-weight: 600;">10 Senior / Staff-Level Interview Questions with In-Depth Answers:</span>
        <div style="display:flex; gap:8px;">
          <button class="nav-btn" onclick="toggleAllQa(true)">Expand All</button>
          <button class="nav-btn" onclick="toggleAllQa(false)">Collapse All</button>
        </div>
      </div>
      <div id="interview-cards-container">
        {qa_cards_html}
      </div>
    </div>

    <div id="pane-source" class="tab-pane">
      {source_files_html}
    </div>

    <div id="pane-tests" class="tab-pane">
      {test_files_html}
    </div>

    <div id="pane-all" class="tab-pane">
      <div class="all-in-one-wrapper">
        <h2 class="section-divider-title">📝 Part 1: Deep-Dive Architectural & Engineering Notes</h2>
        <div class="markdown-content" style="margin-bottom:32px;">
          {rendered_notes_html}
        </div>

        <h2 class="section-divider-title">📑 Part 2: Module Overview & Practical Walkthrough</h2>
        <div class="markdown-content" style="margin-bottom:32px;">
          {rendered_readme_html}
        </div>

        <h2 class="section-divider-title">💡 Part 3: Senior & Staff-Level Interview Scenarios (10 Questions)</h2>
        <div style="margin-bottom:32px;">
          {qa_cards_html}
        </div>

        <h2 class="section-divider-title">💻 Part 4: Production Source Code Implementations</h2>
        <div style="margin-bottom:32px;">
          {source_files_html}
        </div>

        <h2 class="section-divider-title">🧪 Part 5: Comprehensive Unit & Benchmark Tests</h2>
        <div style="margin-bottom:32px;">
          {test_files_html}
        </div>
      </div>
    </div>

    <div class="footer-bar">
      100-Day .NET + AI Engineering Track • Day {day_num:02d} Complete Reference Guide
    </div>
  </div>

  <noscript>
    <div style="padding:16px; background:#ef4444; color:white; border-radius:8px; margin:20px;">
      Note: JavaScript is disabled or blocked. All sections (Deep-Dive Notes, Module README, Interview Questions, and Code) are displayed sequentially above.
    </div>
  </noscript>

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

    // QA Accordion (using native details elements)
    function toggleAllQa(open) {{
      document.querySelectorAll('details.qa-card').forEach(d => {{
        d.open = open;
      }});
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

    document.addEventListener('DOMContentLoaded', () => {{
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
  <title>100-Day .NET + AI Engineering Track - Knowledge Hub</title>
  <style>
    :root {
      --bg-body: #0b0f19;
      --bg-card: #111827;
      --bg-card-hover: #1f2937;
      --border: #1e293b;
      --border-accent: #38bdf8;
      --text-main: #f8fafc;
      --text-muted: #94a3b8;
      --accent: #38bdf8;
      --accent-glow: rgba(56, 189, 248, 0.15);
      --success: #10b981;
    }
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body {
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Arial, sans-serif;
      background-color: var(--bg-body);
      color: var(--text-main);
      line-height: 1.6;
      padding-bottom: 60px;
    }
    .container {
      max-width: 1400px;
      margin: 0 auto;
      padding: 40px 24px;
    }
    header {
      margin-bottom: 40px;
      text-align: center;
    }
    h1 {
      font-size: 32px;
      font-weight: 800;
      color: #fff;
      margin-bottom: 12px;
      letter-spacing: -0.02em;
    }
    p.lead {
      color: var(--text-muted);
      font-size: 16px;
      max-width: 800px;
      margin: 0 auto 24px;
    }
    .stats-bar {
      display: flex;
      justify-content: center;
      flex-wrap: wrap;
      gap: 16px;
      margin-bottom: 36px;
    }
    .stat-pill {
      background: var(--bg-card);
      border: 1px solid var(--border);
      padding: 8px 18px;
      border-radius: 9999px;
      font-size: 13px;
      font-weight: 600;
      color: var(--text-muted);
    }
    .stat-pill span { color: var(--accent); }
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(380px, 1fr));
      gap: 24px;
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
    .notes-feature-tag {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      background: rgba(56, 189, 248, 0.1);
      border: 1px solid rgba(56, 189, 248, 0.25);
      color: #38bdf8;
      font-size: 11px;
      font-weight: 600;
      padding: 3px 8px;
      border-radius: 4px;
      margin-bottom: 14px;
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
      <p class="lead">Interactive Knowledge Hub containing all 23 daily architectural guides, deep notes, senior interview questions &amp; answers, C# implementations, unit tests, and 3D architectural diagrams.</p>
      <div class="stats-bar">
        <div class="stat-pill">Completed: <span>23 / 100 Days</span></div>
        <div class="stat-pill">Deep Notes: <span>23 Architectural Guides</span></div>
        <div class="stat-pill">Interview Q&amp;A: <span>230 Senior Scenarios &amp; Answers</span></div>
        <div class="stat-pill">Test Suite: <span>265 / 265 Passed (100%)</span></div>
        <div class="stat-pill">Platform: <span>.NET 8 / C# 12</span></div>
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

def format_interview_qa(interview_md):
    if not interview_md:
        return "<p>No interview questions available.</p>"
    
    pattern = re.compile(r'###\s+Q(\d+):\s*(.*?)(?=(?:###\s+Q\d+:|$))', re.DOTALL)
    matches = pattern.findall(interview_md)
    
    if not matches:
        return f'<div class="markdown-content">{render_markdown(interview_md)}</div>'
    
    qa_cards = []
    for q_num, q_body in matches:
        lines = q_body.strip().split("\n")
        q_title = lines[0].strip()
        body_content = "\n".join(lines[1:]).strip()
        rendered_body = render_markdown(body_content)
        
        # Make first 2 open by default for immediate preview
        is_open = "open" if int(q_num) <= 2 else ""
        
        qa_cards.append(f"""
        <details class="qa-card" {is_open}>
          <summary class="qa-header">
            <div style="display:flex; align-items:center; gap:10px;">
              <span class="qa-badge">Q{q_num}</span>
              <span class="qa-title">{html.escape(q_title)}</span>
            </div>
            <span class="qa-toggle-icon">▼</span>
          </summary>
          <div class="qa-body markdown-content">
            {rendered_body}
          </div>
        </details>
        """)
    
    return "\n".join(qa_cards)

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
    print(f"Generating full pre-rendered HTML files for Days 1 to {len(DAYS_INFO)}...")

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

        # Pre-render Markdown to HTML at build time
        rendered_readme = render_markdown(readme_content)
        rendered_notes = render_markdown(notes_content)
        qa_cards_html = format_interview_qa(interview_content)
        day_svg = get_day_svg(day_num)

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
            day_svg=day_svg,
            source_files_count=source_count,
            test_files_count=test_count,
            rendered_notes_html=rendered_notes,
            rendered_readme_html=rendered_readme,
            qa_cards_html=qa_cards_html,
            source_files_html=source_files_html,
            test_files_html=test_files_html
        )

        out_path = os.path.join(abs_day_dir, "index.html")
        with open(out_path, "w", encoding="utf-8") as f:
            f.write(day_html)
        print(f"  ✓ Generated: {day_dir}/index.html (Notes: {len(rendered_notes)} chars, {source_count} src, {test_count} tests, 10 Q&As)")

        summary_preview = ""
        if notes_content:
            lines = [l.strip() for l in notes_content.splitlines() if l.strip() and not l.startswith("#")]
            summary_preview = " ".join(lines[:2])[:140] + "..." if lines else ""
        elif readme_content:
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
            <div class="notes-feature-tag">📝 Deep Notes Included</div>
            <p class="card-desc">{html.escape(summary_preview)}</p>
          </div>
          <div class="card-footer">
            <span>🖼️ 3D Model • 📝 Notes • 💡 10 Q&As</span>
            <span class="view-btn">Deep Dive →</span>
          </div>
        </a>
        """

    hub_html = INDEX_HUB_TEMPLATE.replace("<!--DAY_CARDS-->", day_cards_html)
    hub_path = os.path.join(root_dir, "index.html")
    with open(hub_path, "w", encoding="utf-8") as f:
        f.write(hub_html)
    print(f"\n✓ Generated master Knowledge Hub: index.html with all {len(DAYS_INFO)} days!")

if __name__ == "__main__":
    build_days_html()
