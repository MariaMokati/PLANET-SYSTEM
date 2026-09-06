#!/usr/bin/env python3
# Static checker for the SuperQuantX cTrader C# indicator files.
# Rebuilt per HANDOVER_BRIEF section 4. Takes a file path argument.
import re, sys, collections

def strip_strings_and_comments(src):
    out = []
    i = 0
    n = len(src)
    while i < n:
        c = src[i]
        if c == '/' and i + 1 < n and src[i+1] == '/':
            j = src.find('\n', i)
            if j < 0: j = n
            out.append(' ' * (j - i)); i = j
        elif c == '/' and i + 1 < n and src[i+1] == '*':
            j = src.find('*/', i + 2)
            j = n if j < 0 else j + 2
            out.append(''.join(ch if ch == '\n' else ' ' for ch in src[i:j])); i = j
        elif c == '"':
            j = i + 1
            while j < n:
                if src[j] == '\\': j += 2; continue
                if src[j] == '"': j += 1; break
                j += 1
            out.append(' ' * (j - i)); i = j
        elif c == "'":
            j = i + 1
            while j < n:
                if src[j] == '\\': j += 2; continue
                if src[j] == "'": j += 1; break
                j += 1
            out.append(' ' * (j - i)); i = j
        else:
            out.append(c); i += 1
    return ''.join(out)

def main(path):
    raw = open(path, encoding='utf-8').read()
    code = strip_strings_and_comments(raw)
    lines = raw.split('\n')
    codelines = code.split('\n')
    fails = []
    warns = []
    info = []

    # ---- 1. brace / paren / bracket balance ----
    for ch_open, ch_close, name in (('{','}','brace'), ('(',')','paren'), ('[',']','bracket')):
        depth = 0; bad = None
        for ln, L in enumerate(codelines, 1):
            for c in L:
                if c == ch_open: depth += 1
                elif c == ch_close:
                    depth -= 1
                    if depth < 0 and bad is None: bad = ln
        if depth != 0: fails.append("%s balance off by %+d" % (name, depth))
        if bad: fails.append("%s closed before opened at line %d" % (name, bad))
    if not fails: info.append("brace, paren and bracket balance clean")

    # ---- 2. duplicate members (fields, properties, methods) ----
    prop = re.findall(r'public\s+[\w<>\[\],\.]+\s+(\w+)\s*\{\s*get;\s*set;\s*\}', code)
    dupp = [k for k,v in collections.Counter(prop).items() if v > 1]
    if dupp: fails.append("duplicate parameter property: " + ", ".join(dupp))

    fld = re.findall(r'private\s+(?:readonly\s+)?[\w<>\[\],\.]+\s+(\w+)\s*(?:=|;|,)', code)
    dupf = [k for k,v in collections.Counter(fld).items() if v > 1]
    if dupf: warns.append("field name declared more than once (check multi-declarations): " + ", ".join(dupf))

    meth = re.findall(r'(?:private|public|protected)\s+(?:static\s+)?(?:override\s+)?[\w<>\[\],\.]+\s+(\w+)\s*\(([^)]*)\)\s*\{', code)
    mnames = collections.Counter(m[0] for m in meth)
    dupm = [k for k,v in mnames.items() if v > 1]
    if dupm: warns.append("overloaded or duplicated method name: " + ", ".join(dupm))

    # ---- 3. [Parameter] display-name collisions inside the same Group ----
    pairs = re.findall(r'\[Parameter\(\s*"([^"]+)"\s*,\s*Group\s*=\s*"([^"]+)"', raw)
    seen = collections.Counter(pairs)
    for (nm, grp), c in seen.items():
        if c > 1: fails.append('duplicate parameter display name "%s" in group "%s"' % (nm, grp))

    # ---- 4. enum member resolution ----
    enums = {}
    for m in re.finditer(r'public\s+enum\s+(\w+)\s*\{([^}]*)\}', code):
        enums[m.group(1)] = set(x.strip() for x in m.group(2).split(',') if x.strip())
    for m in re.finditer(r'\b(' + '|'.join(enums) + r')\.(\w+)', code) if enums else []:
        if m.group(2) not in enums[m.group(1)]:
            fails.append("enum member %s.%s does not exist" % (m.group(1), m.group(2)))
    if enums: info.append("%d enums, all member references resolve" % len(enums))

    # ---- 5. DefaultValue must be inside MinValue..MaxValue ----
    for m in re.finditer(r'\[Parameter\(\s*"([^"]+)"[^\]]*?DefaultValue\s*=\s*([-\d\.]+)[^\]]*?MinValue\s*=\s*([-\d\.]+)\s*,\s*MaxValue\s*=\s*([-\d\.]+)', raw):
        nm, d, lo, hi = m.group(1), float(m.group(2)), float(m.group(3)), float(m.group(4))
        if not (lo <= d <= hi):
            fails.append('parameter "%s" default %g outside %g..%g' % (nm, d, lo, hi))

    # ---- 6. forward index reads: [i + n], [index + n] on a Bars/series ----
    fwd = []
    for ln, L in enumerate(codelines, 1):
        for m in re.finditer(r'(\w+)\s*\[\s*(\w+)\s*\+\s*(\w+)\s*\]', L):
            fwd.append((ln, m.group(0), lines[ln-1].strip()))
    if fwd:
        warns.append("%d forward-offset index reads to inspect by hand" % len(fwd))
        for ln, expr, txt in fwd: warns.append("    line %d: %s   |  %s" % (ln, expr, txt[:100]))
    else:
        info.append("no forward-offset index reads at all")

    # ---- 7. em dashes and non-ascii punctuation in comments ----
    for ln, L in enumerate(lines, 1):
        if '—' in L or '–' in L:
            fails.append("em or en dash at line %d: %s" % (ln, L.strip()[:80]))

    # ---- 8. [Indicator] attribute present ----
    if '[Indicator(' not in raw: fails.append("missing [Indicator] class attribute")
    else: info.append("[Indicator] attribute present")

    # ---- 9. panel row budget: count y++ against the Rows constant ----
    for cname, drawfn in (('PanelRows','DrawPanel'), ('Rows','DrawPanel'),
                          ('Panel2Rows','DrawConsoPanel'), ('Panel3Rows','DrawDayPanel'),
                          ('DayRows','DrawDayPanel')):
        cm = re.search(r'const\s+int\s+%s\s*=\s*(\d+)' % cname, code)
        if not cm: continue
        cap = int(cm.group(1))
        fm = re.search(r'private\s+void\s+%s\s*\([^)]*\)\s*\{' % drawfn, code)
        if not fm: continue
        # walk to the matching close brace
        j = code.index('{', fm.start()); depth = 0; k = j
        while k < len(code):
            if code[k] == '{': depth += 1
            elif code[k] == '}':
                depth -= 1
                if depth == 0: break
            k += 1
        body = code[j:k]
        incs = len(re.findall(r'\by\+\+', body))
        loops = re.findall(r'for\s*\(\s*int\s+src\s*=\s*0;\s*src\s*<\s*(\d+)', body)
        loopextra = sum(int(x) for x in loops)
        info.append("%s = %d, %s has %d unconditional y++ plus up to %d loop rows (worst case %d)"
                    % (cname, cap, drawfn, incs, loopextra, incs + loopextra))
        if incs + loopextra > cap:
            fails.append("PANEL OVERFLOW: %s=%d but %s can need %d rows; the surplus renders nothing"
                         % (cname, cap, drawfn, incs + loopextra))

    # ---- 10. unused private methods and fields ----
    decl_m = set(re.findall(r'private\s+(?:static\s+)?[\w<>\[\],\.]+\s+(\w+)\s*\([^)]*\)\s*\{', code))
    for nm in sorted(decl_m):
        if len(re.findall(r'\b%s\s*\(' % re.escape(nm), code)) <= 1:
            warns.append("private method never called: " + nm)
    decl_f = set(re.findall(r'private\s+(?:readonly\s+)?[\w<>\[\],\.]+\s+(_\w+)', code))
    for nm in sorted(decl_f):
        if len(re.findall(r'\b%s\b' % re.escape(nm), code)) <= 1:
            warns.append("private field never used: " + nm)

    # ---- 11. array index constants vs declared size ----
    for m in re.finditer(r'new\s+(\w+)\[(\d+)\]', code):
        pass

    print("=" * 74)
    print("CHECK  " + path.split('/')[-1] + "   (%d lines)" % len(lines))
    print("=" * 74)
    for s in info:  print("  ok    " + s)
    for s in warns: print("  warn  " + s)
    for s in fails: print("  FAIL  " + s)
    print("-" * 74)
    print("  %d fail, %d warn" % (len(fails), len(warns)))
    return 1 if fails else 0

if __name__ == '__main__':
    sys.exit(main(sys.argv[1]))
