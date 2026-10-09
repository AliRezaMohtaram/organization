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
        refreshFilters: () => filterGroups.forEach(apply => apply())
    };

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
    else init();
})();
