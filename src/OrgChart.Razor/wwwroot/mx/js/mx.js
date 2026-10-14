/* =========================================================
   MX.JS — UI behaviours for the MX design system
   ---------------------------------------------------------
   Modals (stacked, focus-trapped) · Toasts · Command palette
   Wizard · Filter chips · Tabs · Dropdowns · Sidebar
   Theme state lives in theme.js; this file only calls
   window.ThemeManager when it exists.

   Declarative hooks:
     data-open="modalId"        open a modal
     data-close                 close the nearest modal
     data-action="mode:light"   run a registered action
     data-dd                    toggle the closest .dd dropdown
     data-tabs / data-tab="id"  segmented tabs → panes
     data-filter-group          filter chips for a table
     data-wizard                multi-step flow inside a modal
     data-sidebar-toggle        collapse rail / open mobile nav
     data-modal                 open a link's server form in a modal
     data-mx-ajax               (on a form inside a remote modal) submit via fetch
     data-mx-init="name"        run MX.component(name) on this element
     data-mx-src="/js/x.js"     …loading that script first if needed
     data-jdate                 Jalali date picker on a text input; ="latin" writes Latin digits;
                                MX.jdate(input) for inputs a script adds later
   ========================================================= */

(() => {
    "use strict";

    const $ = (s, r = document) => r.querySelector(s);
    const $$ = (s, r = document) => Array.from(r.querySelectorAll(s));

    const FA_DIGITS = "۰۱۲۳۴۵۶۷۸۹";
    const fa = v => String(v).replace(/\d/g, d => FA_DIGITS[d]);
    const faNum = n => fa(Number(n).toLocaleString("en-US").replace(/,/g, "٬"));

    const FOCUSABLE =
        'a[href],button:not([disabled]),input:not([disabled]):not([type="hidden"]),' +
        'select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"])';

    const visibleFocusables = root =>
        $$(FOCUSABLE, root).filter(el => el.offsetParent !== null || el === document.activeElement);


    /* =====================================================
       MODALS
       ===================================================== */

    const stack = [];

    const resolve = target =>
        typeof target === "string" ? document.getElementById(target) : target;

    function openModal(target) {
        const el = resolve(target);
        if (!el || el.classList.contains("open")) return;

        stack.push({ el, returnTo: document.activeElement });
        el.style.zIndex = String(300 + stack.length * 2);
        el.classList.add("open");
        el.setAttribute("aria-hidden", "false");
        document.documentElement.classList.add("mx-lock");
        el.dispatchEvent(new CustomEvent("mx:open"));

        setTimeout(() => {
            const first = $("[autofocus]", el) || visibleFocusables($(".modal-body", el) || el)[0];
            first?.focus({ preventScroll: true });
        }, 90);
    }

    function closeModal(target) {
        const el = target ? resolve(target) : stack[stack.length - 1]?.el;
        const index = stack.findIndex(s => s.el === el);
        if (index === -1) return;

        const [{ returnTo }] = stack.splice(index, 1);
        el.classList.remove("open");
        el.setAttribute("aria-hidden", "true");
        if (!stack.length) document.documentElement.classList.remove("mx-lock");
        el.dispatchEvent(new CustomEvent("mx:close"));
        returnTo?.focus?.({ preventScroll: true });
    }

    const topModal = () => stack[stack.length - 1]?.el;


    /* =====================================================
       TOASTS
       ===================================================== */

    const TOAST_ICONS = { success: "i-check", danger: "i-alert", warning: "i-alert", info: "i-info", primary: "i-zap" };

    function toast(message, tone = "success", title = "") {
        let host = $(".toast-stack");
        if (!host) {
            host = document.createElement("div");
            host.className = "toast-stack";
            host.setAttribute("aria-live", "polite");
            document.body.appendChild(host);
        }

        const el = document.createElement("div");
        el.className = `toast tone-${tone}`;
        el.setAttribute("role", "status");
        el.innerHTML =
            `<svg class="ico toast-ico"><use href="#${TOAST_ICONS[tone] || "i-check"}"/></svg>` +
            `<div><div class="toast-title"></div><div class="toast-msg"></div></div>`;
        const titleEl = $(".toast-title", el);
        if (title) titleEl.textContent = title; else titleEl.remove();
        $(".toast-msg", el).textContent = message;
        host.appendChild(el);

        setTimeout(() => {
            el.classList.add("out");
            el.addEventListener("animationend", () => el.remove(), { once: true });
        }, 3800);
    }


    /* =====================================================
       ACTIONS  (data-action="name:arg")
       ===================================================== */

    const actions = {
        mode: v => window.ThemeManager?.set("mode", v),
        accent: v => window.ThemeManager?.set("accent", v),
        preset: v => window.ThemeManager?.set("preset", v),
        "theme-open": () => window.ThemeManager?.open(),
        "theme-toggle": () => {
            const tm = window.ThemeManager;
            if (!tm) return;
            const dark = tm.get().mode === "dark" ||
                (tm.get().mode === "system" && matchMedia("(prefers-color-scheme: dark)").matches);
            tm.set("mode", dark ? "light" : "dark");
        },
        sidebar: () => toggleSidebar()
    };

    function runAction(spec, el) {
        const i = spec.indexOf(":");
        const name = i === -1 ? spec : spec.slice(0, i);
        const arg = i === -1 ? undefined : spec.slice(i + 1);
        actions[name]?.(arg, el);
    }


    /* =====================================================
       COMMAND PALETTE  (#cmdk)
       ===================================================== */

    function initPalette() {
        const root = $("#cmdk");
        if (!root) return;

        const input = $("input", root);
        const items = $$(".cmdk-item", root);
        const empty = $(".cmdk-empty", root);
        let active = 0;

        const visible = () => items.filter(el => !el.hidden);

        function highlight(n) {
            const list = visible();
            if (!list.length) return;
            active = (n + list.length) % list.length;
            list.forEach((el, k) => el.classList.toggle("active", k === active));
            list[active].scrollIntoView({ block: "nearest" });
        }

        function filter() {
            const raw = input.value.trim();
            const q = raw.toLowerCase();
            items.forEach(el => {
                // "Search X for …" items appear only while typing and carry the query in their link
                if (el.dataset.searchHref !== undefined) {
                    el.hidden = !q;
                    el.href = el.dataset.searchHref + encodeURIComponent(raw);
                    $$("[data-q]", el).forEach(b => { b.textContent = raw; });
                    return;
                }
                const hay = (el.textContent + " " + (el.dataset.k || "")).toLowerCase();
                el.hidden = !!q && !hay.includes(q);
            });
            $$(".cmdk-group", root).forEach(group => {
                let n = group.nextElementSibling, any = false;
                while (n && !n.classList.contains("cmdk-group")) {
                    if (n.classList.contains("cmdk-item") && !n.hidden) any = true;
                    n = n.nextElementSibling;
                }
                group.hidden = !any;
            });
            if (empty) empty.hidden = visible().length > 0;
            highlight(0);
        }

        input.addEventListener("input", filter);
        input.addEventListener("keydown", e => {
            if (e.key === "ArrowDown") { e.preventDefault(); highlight(active + 1); }
            else if (e.key === "ArrowUp") { e.preventDefault(); highlight(active - 1); }
            else if (e.key === "Enter") { e.preventDefault(); visible()[active]?.click(); }
        });
        items.forEach(el => el.addEventListener("mousemove", () => {
            const k = visible().indexOf(el);
            if (k !== active) highlight(k);
        }));
        root.addEventListener("mx:open", () => { input.value = ""; filter(); });
    }


    /* =====================================================
       WIZARD  ([data-wizard] on the .modal)
       ===================================================== */

    function initWizard(root) {
        const panes = $$("[data-step-pane]", root);
        const steps = $$(".step", root);
        const prev = $("[data-wizard-prev]", root);
        const next = $("[data-wizard-next]", root);
        const label = next && $("[data-label]", next);
        const counter = $("[data-wizard-counter]", root);
        const body = $(".modal-body", root);
        const last = panes.length - 1;
        let current = 0;

        function go(n) {
            current = Math.max(0, Math.min(n, last));
            panes.forEach((p, k) => { p.hidden = k !== current; });
            steps.forEach((s, k) => {
                s.classList.toggle("done", k < current);
                s.classList.toggle("active", k === current);
                s.setAttribute("aria-current", k === current ? "step" : "false");
            });
            if (prev) prev.disabled = current === 0;
            if (label) label.textContent = current === last ? next.dataset.finish : next.dataset.next;
            if (counter) counter.textContent = `مرحله ${fa(current + 1)} از ${fa(last + 1)}`;
            body?.scrollTo(0, 0);
            root.dispatchEvent(new CustomEvent("mx:step", { detail: { step: current } }));
        }

        next?.addEventListener("click", () => {
            const ok = root.dispatchEvent(
                new CustomEvent("mx:wizard-validate", { cancelable: true, detail: { step: current } }));
            if (!ok) return;
            if (current < last) go(current + 1);
            else root.dispatchEvent(new CustomEvent("mx:wizard-finish", { bubbles: true }));
        });
        prev?.addEventListener("click", () => go(current - 1));
        steps.forEach((s, k) => s.addEventListener("click", () => { if (k < current) go(k); }));
        root.closest(".modal-overlay")?.addEventListener("mx:open", () => go(0));

        root.mxWizard = { go, get step() { return current; } };
        go(0);
    }


    /* =====================================================
       FILTER CHIPS  ([data-filter-group] → table rows[data-status])
       ===================================================== */

    const filterGroups = [];

    function initFilterGroup(group) {
        const table = $(group.dataset.target);
        const counter = group.dataset.count ? $(group.dataset.count) : null;
        if (!table) return;

        function apply() {
            const filter = $(".fchip.active", group)?.dataset.filter || "all";
            const rows = $$("tbody tr[data-status]", table);
            let shown = 0;

            rows.forEach(tr => {
                const match = filter === "all" || tr.dataset.status.split(" ").includes(filter);
                tr.hidden = !match;
                if (match) shown++;
            });
            $$(".fchip", group).forEach(chip => {
                const n = $(".n", chip);
                if (!n) return;
                const f = chip.dataset.filter;
                n.textContent = fa(f === "all" ? rows.length
                    : rows.filter(tr => tr.dataset.status.split(" ").includes(f)).length);
            });
            const emptyRow = $("[data-empty-row]", table);
            if (emptyRow) emptyRow.hidden = shown > 0;
            if (counter) counter.textContent = fa(shown);
        }

        group.addEventListener("click", e => {
            const chip = e.target.closest(".fchip");
            if (!chip) return;
            $$(".fchip", group).forEach(c => {
                c.classList.toggle("active", c === chip);
                c.setAttribute("aria-pressed", String(c === chip));
            });
            apply();
        });

        filterGroups.push(apply);
        apply();
    }


    /* =====================================================
       SIDEBAR
       ===================================================== */

    const SIDEBAR_KEY = "mx-sidebar";
    const mqTablet = matchMedia("(max-width: 1100px)");
    const mqMobile = matchMedia("(max-width: 760px)");
    let userCollapsed = false;
    let tabletExpanded = false;
    try { userCollapsed = localStorage.getItem(SIDEBAR_KEY) === "collapsed"; } catch { /* storage blocked */ }

    function syncSidebar() {
        const app = $(".app");
        if (!app) return;
        if (mqMobile.matches) {
            app.classList.remove("is-collapsed");
        } else {
            app.classList.remove("nav-open");
            app.classList.toggle("is-collapsed", mqTablet.matches ? !tabletExpanded : userCollapsed);
        }
    }

    function toggleSidebar() {
        const app = $(".app");
        if (!app) return;
        if (mqMobile.matches) {
            app.classList.toggle("nav-open");
            return;
        }
        if (mqTablet.matches) {
            tabletExpanded = !tabletExpanded;
        } else {
            userCollapsed = !userCollapsed;
            try { localStorage.setItem(SIDEBAR_KEY, userCollapsed ? "collapsed" : "expanded"); } catch { /* ignore */ }
        }
        syncSidebar();
    }



    /* =====================================================
       COMPONENTS  (data-mx-init="name" [data-mx-src="url"])
       Page scripts register with MX.component(name, root => {}).
       Runs on page load and on every HTML injected by a remote modal.
       ===================================================== */

    const components = {};
    const scriptLoads = {};

    function loadScript(src) {
        if (!scriptLoads[src]) {
            scriptLoads[src] = new Promise((resolve, reject) => {
                const el = document.createElement("script");
                el.src = src;
                el.onload = resolve;
                el.onerror = () => reject(new Error(`Could not load ${src}`));
                document.head.appendChild(el);
            });
        }
        return scriptLoads[src];
    }

    async function initComponents(scope = document) {
        enhanceDates(scope);
        const roots = $$("[data-mx-init]", scope);
        if (scope instanceof Element && scope.matches("[data-mx-init]")) roots.unshift(scope);

        for (const root of roots) {
            if (root.mxReady) continue;
            const name = root.dataset.mxInit;
            if (!components[name] && root.dataset.mxSrc) {
                try { await loadScript(root.dataset.mxSrc); }
                catch (err) { console.error(err); continue; }
            }
            if (!components[name] || root.mxReady) continue;
            root.mxReady = true;
            components[name](root);
        }
    }

    /* =====================================================
       FILE DROP  (partial _FileDrop → data-mx-init="file-drop")
       Click or drag a file onto the zone; type and size are checked
       before upload, the chosen file is shown as a card, and the form
       shows an "uploading" state while the browser posts it.
       ===================================================== */

    const formatSize = bytes => bytes >= 1048576
        ? `${fa((bytes / 1048576).toFixed(1).replace(/\.0$/, "").replace(".", "٫"))} مگابایت`
        : `${fa(Math.max(1, Math.round(bytes / 1024)))} کیلوبایت`;

    components["file-drop"] = root => {
        const input = $(".file-drop-input", root);
        const zone = $(".dropzone", root);
        const card = $("[data-file-card]", root);
        const error = $("[data-file-error]", root);
        const progress = $("[data-file-progress]", root);
        const form = root.closest("form");
        const maxBytes = Number(root.dataset.maxBytes) || Infinity;
        const allowed = (input.accept || "").split(",").map(x => x.trim().toLowerCase()).filter(Boolean);

        const extOf = name => (name.match(/\.[^.]+$/)?.[0] || "").toLowerCase();

        function showError(message) {
            error.textContent = message;
            root.classList.toggle("is-invalid", !!message);
        }

        function render() {
            const file = input.files[0];
            root.classList.toggle("has-file", !!file);
            card.hidden = !file;
            if (!file) return;
            const ext = extOf(file.name).slice(1).toUpperCase() || "FILE";
            const kind = $("[data-file-kind]", card);
            kind.textContent = ext;
            kind.classList.toggle("csv", ext === "CSV" || ext === "TSV");
            $("[data-file-name]", card).textContent = file.name;
            $("[data-file-meta]", card).textContent = formatSize(file.size);
        }

        function clear() {
            input.value = "";
            render();
        }

        function accept(files) {
            const file = files?.[0];
            if (!file) return;
            if (files.length > 1) toast("فقط یک فایل پذیرفته می‌شود؛ اولین فایل انتخاب شد.", "warning");

            if (allowed.length && !allowed.includes(extOf(file.name))) {
                clear();
                showError(`فرمت «${file.name}» مجاز نیست. فقط ${allowed.join("، ")}`);
                return;
            }
            if (file.size > maxBytes) {
                clear();
                showError(`حجم «${file.name}» (${formatSize(file.size)}) بیشتر از سقف ${formatSize(maxBytes)} است.`);
                return;
            }
            if (file.size === 0) {
                clear();
                showError(`«${file.name}» خالی است.`);
                return;
            }

            // Dropped files are not in the input yet — move them there so the form posts them
            if (input.files[0] !== file) {
                const dt = new DataTransfer();
                dt.items.add(file);
                input.files = dt.files;
            }
            showError("");
            render();
        }

        input.addEventListener("change", () => accept(input.files));
        $("[data-file-remove]", card).addEventListener("click", () => { clear(); input.focus(); });

        ["dragenter", "dragover"].forEach(ev => root.addEventListener(ev, e => {
            e.preventDefault();
            zone.classList.add("drag");
        }));
        ["dragleave", "drop"].forEach(ev => root.addEventListener(ev, e => {
            if (ev === "dragleave" && root.contains(e.relatedTarget)) return;
            e.preventDefault();
            zone.classList.remove("drag");
        }));
        root.addEventListener("drop", e => accept(e.dataTransfer.files));

        form?.addEventListener("submit", e => {
            if (input.required && !input.files.length) {
                e.preventDefault();
                showError("ابتدا یک فایل انتخاب کنید.");
                input.focus();
                return;
            }
            if (e.defaultPrevented) return;
            progress.hidden = false;
            root.classList.add("is-uploading");
            $$('button[type="submit"]', form).forEach(b => b.setAttribute("aria-busy", "true"));
        });

        // Back/forward cache: the page may come back still showing "uploading"
        window.addEventListener("pageshow", e => {
            if (!e.persisted) return;
            progress.hidden = true;
            root.classList.remove("is-uploading");
            $$('button[aria-busy]', form || root).forEach(b => b.removeAttribute("aria-busy"));
            render();
        });

        render();
    };

    /* =====================================================
       FILTER LIST  (data-mx-init="filter-list")
       An input[data-filter-input] hides [data-filter-item] elements whose
       text (plus data-k) does not contain the query; [data-filter-empty]
       shows when nothing matches.
       ===================================================== */

    components["filter-list"] = root => {
        const input = $("[data-filter-input]", root);
        const items = $$("[data-filter-item]", root);
        const empty = $("[data-filter-empty]", root);
        if (!input) return;
        input.addEventListener("input", () => {
            const q = input.value.trim().toLowerCase();
            let shown = 0;
            items.forEach(el => {
                const hit = !q || (el.textContent + " " + (el.dataset.k || "")).toLowerCase().includes(q);
                el.hidden = !hit;
                if (hit) shown++;
            });
            if (empty) empty.hidden = shown > 0;
        });
    };

    function registerComponent(name, init) {
        components[name] = init;
        initComponents();   // elements already in the page
    }


    /* =====================================================
       REMOTE MODAL
       A link with data-modal loads its URL with header X-MX-Modal: 1;
       the server answers with a partial whose root is a .modal element.
       Forms with data-mx-ajax inside it post via fetch:
         · JSON { redirect }  → navigate (server sets the TempData toast)
         · HTML               → replace the modal (validation errors)
       Without JS the same URLs work as full pages.
       ===================================================== */

    const MODAL_HEADER = { "X-MX-Modal": "1" };

    function remoteHost() {
        let host = $("#mxRemote");
        if (!host) {
            host = document.createElement("div");
            host.id = "mxRemote";
            host.className = "modal-overlay";
            host.setAttribute("role", "dialog");
            host.setAttribute("aria-modal", "true");
            host.setAttribute("aria-hidden", "true");
            host.addEventListener("mx:close", () => {
                setTimeout(() => { if (!host.classList.contains("open")) host.innerHTML = ""; }, 300);
            });
            document.body.appendChild(host);
        }
        return host;
    }

    const LOADING_HTML =
        `<div class="modal modal-sm modal-loading" aria-busy="true">` +
        `<div class="modal-body"><span class="spinner"></span><span>در حال بارگذاری…</span></div></div>`;

    async function renderRemote(host, response) {
        const type = response.headers.get("content-type") || "";

        if (type.includes("application/json")) {
            const data = await response.json();
            if (data.redirect) { window.location.href = data.redirect; return; }
            if (data.message) toast(data.message, data.ok === false ? "danger" : "success");
            closeModal(host);
            return;
        }

        const html = await response.text();
        // A full page means the server redirected instead (e.g. "not found") — follow it.
        if (/<html[\s>]/i.test(html)) { window.location.href = response.url; return; }

        host.innerHTML = html;
        const title = $(".modal-title", host);
        if (title) {
            if (!title.id) title.id = "mxRemoteTitle";
            host.setAttribute("aria-labelledby", title.id);
        }
        await initComponents(host);
        // The partial can carry a message for the action that produced it (e.g. "alias added")
        const flash = $("[data-mx-toast]", host);
        if (flash?.dataset.mxToast) toast(flash.dataset.mxToast, flash.dataset.mxToastTone || "success");
        const first = $("[autofocus]", host) ||
            $(".field-invalid :is(input, select, textarea)", host) ||
            visibleFocusables($(".modal-body", host) || host)[0];
        first?.focus({ preventScroll: true });
    }

    async function openRemote(url) {
        const host = remoteHost();
        host.innerHTML = LOADING_HTML;
        openModal(host);

        try {
            const response = await fetch(url, { headers: MODAL_HEADER, credentials: "same-origin" });
            if (!response.ok) throw new Error(`HTTP ${response.status}`);
            await renderRemote(host, response);
        } catch (err) {
            console.error(err);
            closeModal(host);
            toast("بارگذاری فرم انجام نشد. دوباره تلاش کنید.", "danger");
        }
    }

    async function submitRemote(form, submitter) {
        const host = form.closest(".modal-overlay");
        const buttons = $$('button[type="submit"], button:not([type])', form);
        if (submitter && !buttons.includes(submitter)) buttons.push(submitter);   // button outside the form (form="…")
        buttons.forEach(b => { b.disabled = true; });
        submitter?.setAttribute("aria-busy", "true");

        try {
            const response = await fetch(form.action, {
                method: (form.getAttribute("method") || "post").toUpperCase(),
                body: new FormData(form, submitter),
                headers: MODAL_HEADER,
                credentials: "same-origin"
            });
            if (!response.ok) throw new Error(`HTTP ${response.status}`);
            await renderRemote(host, response);
        } catch (err) {
            console.error(err);
            toast("ذخیره انجام نشد. دوباره تلاش کنید.", "danger");
            buttons.forEach(b => { b.disabled = false; });
            submitter?.removeAttribute("aria-busy");
        }
    }

    document.addEventListener("submit", e => {
        const form = e.target;
        if (!form.matches("[data-mx-ajax]") || !form.closest("#mxRemote")) return;
        // A component (e.g. file-drop) may already have rejected the submit
        if (e.defaultPrevented) return;
        e.preventDefault();
        submitRemote(form, e.submitter);
    });

    /* =====================================================
       GLOBAL EVENTS
       ===================================================== */

    let pressedOn = null;
    document.addEventListener("mousedown", e => { pressedOn = e.target; });

    document.addEventListener("click", e => {
        const t = e.target;

        // Palette item: close the palette, then let its own hook run below.
        const paletteItem = t.closest(".cmdk-item");
        if (paletteItem) closeModal("cmdk");

        const opener = t.closest("[data-open]");
        if (opener) { e.preventDefault(); openModal(opener.dataset.open); }

        const closer = t.closest("[data-close]");
        if (closer) {
            const host = closer.closest(".modal-overlay");
            // In a modal, "close" links (fallback hrefs of the full-page form) must not navigate
            if (host) { e.preventDefault(); closeModal(host); }
        }

        // Server form in a modal; modified clicks keep the browser's own behaviour (new tab…)
        const remoteLink = t.closest("a[data-modal]");
        if (remoteLink && e.button === 0 && !e.ctrlKey && !e.metaKey && !e.shiftKey && !e.altKey) {
            e.preventDefault();
            openRemote(remoteLink.href);
        }

        const action = t.closest("[data-action]");
        if (action) runAction(action.dataset.action, action);

        if (t.closest("[data-sidebar-toggle]") || t.classList.contains("scrim")) toggleSidebar();
        else if (t.closest(".app.nav-open .nav-item")) $(".app").classList.remove("nav-open");

        // Click on the dimmed backdrop (press and release both on it)
        if (t.classList.contains("modal-overlay") && pressedOn === t && !t.hasAttribute("data-static")) {
            closeModal(t);
        }

        // Tabs
        const tab = t.closest("[data-tab]");
        if (tab) {
            const group = tab.closest("[data-tabs]");
            $$("[data-tab]", group).forEach(b => {
                const on = b === tab;
                b.classList.toggle("active", on);
                b.setAttribute("aria-selected", String(on));
                const pane = document.getElementById(b.dataset.tab);
                if (pane) pane.hidden = !on;
            });
        }

        // Dropdowns
        const ddToggle = t.closest("[data-dd]");
        const ownDd = ddToggle?.closest(".dd");
        $$(".dd.open").forEach(dd => {
            if (dd !== ownDd && !dd.contains(t)) dd.classList.remove("open");
        });
        if (ownDd) {
            const open = ownDd.classList.toggle("open");
            ddToggle.setAttribute("aria-expanded", String(open));
        } else if (t.closest(".dd-item")) {
            t.closest(".dd")?.classList.remove("open");
        }
    });

    document.addEventListener("keydown", e => {
        const typing = /^(INPUT|TEXTAREA|SELECT)$/.test(document.activeElement?.tagName) ||
            document.activeElement?.isContentEditable;

        if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") {
            e.preventDefault();
            if ($("#cmdk")?.classList.contains("open")) closeModal("cmdk"); else openModal("cmdk");
            return;
        }
        if (e.key === "/" && !typing && !stack.length) {
            e.preventDefault();
            openModal("cmdk");
            return;
        }

        if (e.key === "Escape") {
            if ($("#themeDrawer.open")) return;           // theme.js closes the drawer
            const open = $$(".dd.open");
            if (open.length) { open.forEach(d => d.classList.remove("open")); return; }
            const top = topModal();
            if (top && !top.hasAttribute("data-static")) closeModal(top);
            return;
        }

        // Focus trap inside the top-most modal
        if (e.key === "Tab" && stack.length) {
            const list = visibleFocusables(topModal());
            if (!list.length) return;
            const first = list[0], lastEl = list[list.length - 1];
            if (e.shiftKey && document.activeElement === first) { e.preventDefault(); lastEl.focus(); }
            else if (!e.shiftKey && document.activeElement === lastEl) { e.preventDefault(); first.focus(); }
        }
    });


    /* =====================================================
       INIT
       ===================================================== */

    /* =====================================================
       JALALI DATE PICKER  (input[data-jdate])
       The input stays a text box (typing works); a calendar button next to it and a click on it open one shared
       popup. The popup keeps the focus on the input: arrows move the day (RTL), PageUp/PageDown the month,
       Enter picks, Escape closes. Picking writes "۱۴۰۵/۰۷/۲۰" and fires input + change.
       ===================================================== */

    const JD = (() => {
        const div = (a, b) => ~~(a / b);
        const mod = (a, b) => a - ~~(a / b) * b;
        const BREAKS = [-61, 9, 38, 199, 426, 686, 756, 818, 1111, 1181, 1210, 1635, 2060, 2097, 2192, 2262, 2324, 2394, 2456, 3178];
        function cal(jy) {
            const gy = jy + 621;
            let leapJ = -14, jp = BREAKS[0], jump = 0;
            for (let i = 1; i < BREAKS.length; i++) {
                const jm = BREAKS[i];
                jump = jm - jp;
                if (jy < jm) break;
                leapJ += div(jump, 33) * 8 + div(mod(jump, 33), 4);
                jp = jm;
            }
            let n = jy - jp;
            leapJ += div(n, 33) * 8 + div(mod(n, 33) + 3, 4);
            if (mod(jump, 33) === 4 && jump - n === 4) leapJ++;
            const march = 20 + leapJ - (div(gy, 4) - div((div(gy, 100) + 1) * 3, 4) - 150);
            if (jump - n < 6) n = n - jump + div(jump + 4, 33) * 33;
            let leap = mod(mod(n + 1, 33) - 1, 4);
            if (leap === -1) leap = 4;
            return { leap, gy, march };
        }
        const g2d = (gy, gm, gd) => div((gy + div(gm - 8, 6) + 100100) * 1461, 4) + div(153 * mod(gm + 9, 12) + 2, 5) + gd - 34840408
            - div(div(gy + 100100 + div(gm - 8, 6), 100) * 3, 4) + 752;
        function d2g(jdn) {
            let j = 4 * jdn + 139361631;
            j = j + div(div(4 * jdn + 183187720, 146097) * 3, 4) * 4 - 3908;
            const i = div(mod(j, 1461), 4) * 5 + 308;
            const gd = div(mod(i, 153), 5) + 1, gm = mod(div(i, 153), 12) + 1;
            return { gy: div(j, 1461) - 100100 + div(8 - gm, 6), gm, gd };
        }
        const j2d = (jy, jm, jd) => { const r = cal(jy); return g2d(r.gy, 3, r.march) + (jm - 1) * 31 - div(jm, 7) * (jm - 7) + jd - 1; };
        function d2j(jdn) {
            const gy = d2g(jdn).gy;
            let jy = gy - 621;
            const r = cal(jy);
            let k = jdn - g2d(gy, 3, r.march);
            if (k >= 0) {
                if (k <= 185) return { jy, jm: 1 + div(k, 31), jd: mod(k, 31) + 1 };
                k -= 186;
            } else { jy--; k += 179; if (r.leap === 1) k++; }
            return { jy, jm: 7 + div(k, 30), jd: mod(k, 30) + 1 };
        }
        const monthLength = (jy, jm) => jm <= 6 ? 31 : jm <= 11 ? 30 : (cal(jy).leap === 0 ? 30 : 29);
        // weekday of a day number with Saturday = 0, computed through the Gregorian date
        const weekday = jdn => { const g = d2g(jdn); return (new Date(g.gy, g.gm - 1, g.gd).getDay() + 1) % 7; };
        const today = () => { const d = new Date(); return g2d(d.getFullYear(), d.getMonth() + 1, d.getDate()); };
        return { j2d, d2j, monthLength, weekday, today };
    })();

    const JD_MONTHS = ["فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"];
    const JD_DAYS = ["ش", "ی", "د", "س", "چ", "پ", "ج"];
    const latin = s => String(s).replace(/[۰-۹]/g, d => d.charCodeAt(0) - 0x06F0).replace(/[٠-٩]/g, d => d.charCodeAt(0) - 0x0660);
    // month/year arrows: "previous" sits on the right in RTL and points right
    const CHEV = Object.fromEntries([["r1", "m9 6 6 6-6 6"], ["r2", "m6 6 6 6-6 6M12 6l6 6-6 6"], ["l1", "m15 6-6 6 6 6"], ["l2", "m18 6-6 6 6 6M12 6l-6 6 6 6"]]
        .map(([k, d]) => [k, `<svg class="ico" viewBox="0 0 24 24" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="${d}"/></svg>`]));
    const ICO_CAL = '<svg class="ico" viewBox="0 0 24 24" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><rect x="3.5" y="5" width="17" height="15.5" rx="2.5"/><path d="M3.5 10h17M8 3v4M16 3v4"/></svg>';

    /** "1405/7/20" (Persian or Latin digits, / - .) → day number, or null. */
    function parseJalali(text) {
        const m = /^\s*(\d{4})[\/\-.](\d{1,2})[\/\-.](\d{1,2})/.exec(latin(text || ""));
        if (!m) return null;
        const jy = +m[1], jm = +m[2], jd = +m[3];
        if (jy < 1200 || jy > 1600 || jm < 1 || jm > 12 || jd < 1 || jd > JD.monthLength(jy, jm)) return null;
        return JD.j2d(jy, jm, jd);
    }

    function formatJalali(jdn, latinDigits) {
        const j = JD.d2j(jdn);
        const text = `${j.jy}/${String(j.jm).padStart(2, "0")}/${String(j.jd).padStart(2, "0")}`;
        return latinDigits ? text : fa(text);
    }

    const picker = { el: null, input: null, view: null, active: null };

    function pickerEl() {
        if (picker.el) return picker.el;
        const el = document.createElement("div");
        el.className = "jdate-pop";
        el.setAttribute("role", "dialog");
        el.setAttribute("aria-label", "تقویم");
        el.hidden = true;
        // keep the focus on the input
        el.addEventListener("pointerdown", e => e.preventDefault());
        el.addEventListener("click", e => {
            const b = e.target.closest("button");
            if (!b) return;
            if (b.dataset.day) choose(+b.dataset.day);
            else if (b.dataset.move) moveMonth(+b.dataset.move);
            else if (b.hasAttribute("data-today")) choose(JD.today());
            else if (b.hasAttribute("data-clear")) choose(null);
        });
        document.body.appendChild(el);
        picker.el = el;
        return el;
    }

    function moveMonth(delta) {
        let { jy, jm } = picker.view;
        jm += delta;
        while (jm > 12) { jm -= 12; jy++; }
        while (jm < 1) { jm += 12; jy--; }
        picker.view = { jy, jm };
        picker.active = JD.j2d(jy, jm, Math.min(JD.d2j(picker.active).jd, JD.monthLength(jy, jm)));
        renderPicker();
    }

    function renderPicker() {
        const el = pickerEl();
        const { jy, jm } = picker.view;
        const first = JD.j2d(jy, jm, 1);
        const len = JD.monthLength(jy, jm);
        const chosen = parseJalali(picker.input.value);
        const today = JD.today();
        let html = `<div class="jdate-head">
            <button type="button" class="icon-btn sm plain" data-move="-12" aria-label="سال قبل">${CHEV.r2}</button>
            <button type="button" class="icon-btn sm plain" data-move="-1" aria-label="ماه قبل">${CHEV.r1}</button>
            <div class="jdate-title">${JD_MONTHS[jm - 1]} ${fa(jy)}</div>
            <button type="button" class="icon-btn sm plain" data-move="1" aria-label="ماه بعد">${CHEV.l1}</button>
            <button type="button" class="icon-btn sm plain" data-move="12" aria-label="سال بعد">${CHEV.l2}</button>
        </div><div class="jdate-grid">`;
        html += JD_DAYS.map((d, i) => `<span class="jdate-dow${i === 6 ? " is-off" : ""}">${d}</span>`).join("");
        for (let i = 0; i < JD.weekday(first); i++) html += "<span></span>";
        for (let d = 1; d <= len; d++) {
            const jdn = first + d - 1;
            const cls = ["jdate-day"];
            if (jdn === chosen) cls.push("is-chosen");
            if (jdn === today) cls.push("is-today");
            if (jdn === picker.active) cls.push("is-active");
            if (JD.weekday(jdn) === 6) cls.push("is-off");
            html += `<button type="button" tabindex="-1" class="${cls.join(" ")}" data-day="${jdn}" aria-label="${fa(d)} ${JD_MONTHS[jm - 1]} ${fa(jy)}">${fa(d)}</button>`;
        }
        html += `</div><div class="jdate-foot">
            <button type="button" class="btn btn-ghost btn-sm" data-today>امروز</button>
            <span class="spacer"></span>
            <button type="button" class="btn btn-quiet btn-sm" data-clear>پاک کردن</button>
        </div>`;
        el.innerHTML = html;
    }

    function placePicker() {
        const el = picker.el, r = picker.input.getBoundingClientRect();
        const w = el.offsetWidth, h = el.offsetHeight;
        let top = r.bottom + 6;
        if (top + h > window.innerHeight - 8 && r.top - h - 6 > 8) top = r.top - h - 6;
        // RTL: line up with the input's right edge
        const left = Math.max(8, Math.min(r.right - w, window.innerWidth - w - 8));
        el.style.top = `${Math.round(top)}px`;
        el.style.left = `${Math.round(left)}px`;
    }

    function openPicker(input) {
        if (input.disabled || input.readOnly) return;
        picker.input = input;
        const start = parseJalali(input.value) ?? JD.today();
        const j = JD.d2j(start);
        picker.view = { jy: j.jy, jm: j.jm };
        picker.active = start;
        renderPicker();
        picker.el.hidden = false;
        input.setAttribute("aria-expanded", "true");
        placePicker();
    }

    function closePicker() {
        if (!picker.el || picker.el.hidden) return;
        picker.el.hidden = true;
        picker.input?.setAttribute("aria-expanded", "false");
        picker.input = null;
    }

    const pickerOpenFor = input => picker.input === input && picker.el && !picker.el.hidden;

    function choose(jdn) {
        const input = picker.input;
        if (!input) return;
        input.value = jdn === null ? "" : formatJalali(jdn, input.dataset.jdate === "latin");
        closePicker();
        input.dispatchEvent(new Event("input", { bubbles: true }));
        input.dispatchEvent(new Event("change", { bubbles: true }));
        input.focus();
    }

    function enhanceDate(input) {
        if (!input || input.mxJdate || !input.parentNode) return;
        input.mxJdate = true;
        const wrap = document.createElement("span");
        wrap.className = "jdate";
        input.before(wrap);
        wrap.appendChild(input);
        const btn = document.createElement("button");
        btn.type = "button";
        btn.className = "jdate-btn";
        btn.tabIndex = -1;
        btn.setAttribute("aria-label", "انتخاب تاریخ از تقویم");
        btn.innerHTML = ICO_CAL;
        wrap.appendChild(btn);
        input.setAttribute("aria-haspopup", "dialog");
        input.setAttribute("aria-expanded", "false");
        input.setAttribute("autocomplete", "off");
        btn.addEventListener("pointerdown", e => e.preventDefault());
        btn.addEventListener("click", () => {
            if (pickerOpenFor(input)) closePicker();
            else { input.focus(); openPicker(input); }
        });
        input.addEventListener("click", () => { if (!pickerOpenFor(input)) openPicker(input); });
        input.addEventListener("input", () => {
            if (!pickerOpenFor(input)) return;
            const d = parseJalali(input.value);
            if (d === null) return;
            const j = JD.d2j(d);
            picker.view = { jy: j.jy, jm: j.jm };
            picker.active = d;
            renderPicker();
        });
        input.addEventListener("blur", () => setTimeout(() => { if (picker.input === input && document.activeElement !== input) closePicker(); }, 150));
        input.addEventListener("keydown", e => {
            if (!pickerOpenFor(input)) {
                if (e.altKey && e.key === "ArrowDown") { e.preventDefault(); openPicker(input); }
                return;
            }
            const rtl = getComputedStyle(input.closest("[dir]") || document.documentElement).direction === "rtl";
            const steps = { ArrowUp: -7, ArrowDown: 7, ArrowLeft: rtl ? 1 : -1, ArrowRight: rtl ? -1 : 1 };
            if (e.key in steps) {
                e.preventDefault();
                picker.active += steps[e.key];
                const j = JD.d2j(picker.active);
                picker.view = { jy: j.jy, jm: j.jm };
                renderPicker();
            } else if (e.key === "PageUp" || e.key === "PageDown") {
                e.preventDefault();
                moveMonth(e.key === "PageUp" ? -1 : 1);
            } else if (e.key === "Enter") {
                e.preventDefault();
                e.stopPropagation();
                choose(picker.active);
            } else if (e.key === "Escape") {
                e.preventDefault();
                e.stopPropagation(); // not the modal around it
                closePicker();
            } else if (e.key === "Tab") {
                closePicker();
            }
        });
    }

    function enhanceDates(scope = document) {
        if (scope instanceof Element && scope.matches("input[data-jdate]")) enhanceDate(scope);
        $$("input[data-jdate]", scope).forEach(enhanceDate);
    }

    document.addEventListener("pointerdown", e => {
        if (picker.el && !picker.el.hidden && !picker.el.contains(e.target) && !e.target.closest?.(".jdate")) closePicker();
    });
    window.addEventListener("resize", closePicker);
    window.addEventListener("scroll", () => { if (picker.el && !picker.el.hidden) placePicker(); }, true);

    function init() {
        syncSidebar();
        mqTablet.addEventListener("change", syncSidebar);
        mqMobile.addEventListener("change", syncSidebar);
        $$(".modal-overlay").forEach(m => m.setAttribute("aria-hidden", "true"));
        initPalette();
        $$("[data-wizard]").forEach(initWizard);
        $$("[data-filter-group]").forEach(initFilterGroup);
        initComponents();
    }

    window.MX = {
        open: openModal,
        close: closeModal,
        toast,
        fa,
        faNum,
        actions,
        openRemote,
        component: registerComponent,
        jdate: input => input ? enhanceDate(input) : enhanceDates(),
        refreshFilters: () => filterGroups.forEach(apply => apply())
    };

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
    else init();
})();
