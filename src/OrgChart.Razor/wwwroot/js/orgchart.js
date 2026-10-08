/* =========================================================
   ORGCHART.JS — behaviour of the OrgChart pages (MX design system)
   Tree: expand/collapse, search, type filter, inactive toggle, keyboard.
   Flash: shows the last change as an MX toast when mx.js is there.
   Component "oc-user-search": user search in the assignment form (also inside MX remote modals).
   Loaded with "defer", so mx.js (end of body) has already run.
   ========================================================= */
(() => {
    "use strict";

    const $ = (s, r = document) => r.querySelector(s);
    const $$ = (s, r = document) => Array.from(r.querySelectorAll(s));
    const FA = "۰۱۲۳۴۵۶۷۸۹";
    const fa = v => String(v).replace(/\d/g, d => FA[d]);
    const norm = s => (s || "").toLowerCase().replace(/[يى]/g, "ی").replace(/ك/g, "ک").trim();
    const store = {
        get(k) { try { return localStorage.getItem(k); } catch { return null; } },
        set(k, v) { try { localStorage.setItem(k, v); } catch { /* storage blocked */ } }
    };

    /* ---------------- flash → toast ---------------- */
    function flash() {
        const el = $("[data-oc-toast]");
        if (el && window.MX?.toast) {
            window.MX.toast(el.dataset.ocToast, "success");
            el.classList.add("is-toasted");
        }
    }

    /* ---------------- tree ---------------- */
    function tree() {
        const root = $("[data-oc-tree]");
        if (!root) return;
        const panel = root.closest("[data-oc-tree-panel]");
        const nodes = $$(".oc-node", root);
        const search = $("[data-oc-search]", panel);
        const type = $("[data-oc-type]", panel);
        const inactive = $("[data-oc-inactive]", panel);
        const empty = $("[data-oc-tree-empty]", panel);
        const count = $("[data-oc-count]", panel);
        const INACTIVE_KEY = "orgchart-show-inactive";

        const groupOf = node => $(":scope > ul", node);
        const setOpen = (node, open) => {
            const group = groupOf(node);
            if (!group) return;
            group.hidden = !open;
            node.setAttribute("aria-expanded", String(open));
        };

        root.addEventListener("click", e => {
            const toggle = e.target.closest(".oc-toggle:not(.is-leaf)");
            if (!toggle) return;
            const node = toggle.closest(".oc-node");
            setOpen(node, node.getAttribute("aria-expanded") !== "true");
        });
        $("[data-oc-expand]", panel)?.addEventListener("click", () => nodes.forEach(n => setOpen(n, true)));
        $("[data-oc-collapse]", panel)?.addEventListener("click", () => nodes.forEach(n => setOpen(n, false)));

        // Inactive units: remembered per browser; always shown when the selected unit is inactive.
        if (inactive) {
            if (!inactive.checked && store.get(INACTIVE_KEY) === "1") inactive.checked = true;
            const applyInactive = () => { root.classList.toggle("show-inactive", inactive.checked); filter(); };
            inactive.addEventListener("change", () => { store.set(INACTIVE_KEY, inactive.checked ? "1" : "0"); applyInactive(); });
            root.classList.toggle("show-inactive", inactive.checked);
        }

        // Search and type filter: a node stays when it matches or has a matching descendant; ancestors open up.
        function filter() {
            const q = norm(search?.value);
            const t = type?.value || "";
            const showInactive = root.classList.contains("show-inactive");
            let visible = 0;

            const visit = node => {
                const self = (!q || norm(node.dataset.search).includes(q)) && (!t || node.dataset.type === t)
                    && (showInactive || !node.classList.contains("is-inactive"));
                const group = groupOf(node);
                let childMatch = false;
                if (group) $$(":scope > .oc-node", group).forEach(c => { if (visit(c)) childMatch = true; });
                const keep = self || childMatch;
                node.classList.toggle("is-filtered-out", !keep);
                node.classList.toggle("is-match", (q !== "" || t !== "") && self);
                if ((q || t) && childMatch) setOpen(node, true);
                if (self) visible++;
                return keep;
            };
            $$(":scope > .oc-node", root).forEach(visit);
            if (empty) empty.hidden = visible > 0;
            if (count?.dataset.template) count.textContent = count.dataset.template.replace("{0}", fa(visible));
        }
        search?.addEventListener("input", filter);
        type?.addEventListener("change", filter);
        if (search?.value || type?.value) filter();

        // Keyboard (roving tabindex): ↑/↓ move, ← opens / goes in, → closes / goes up (RTL), Home/End.
        const links = () => $$(".oc-link", root).filter(a => a.offsetParent !== null);
        root.addEventListener("keydown", e => {
            const link = e.target.closest(".oc-link");
            if (!link) return;
            const node = link.closest(".oc-node");
            const list = links();
            const i = list.indexOf(link);
            const focus = a => { if (!a) return; list.forEach(x => x.tabIndex = -1); a.tabIndex = 0; a.focus(); };
            const open = node.getAttribute("aria-expanded");
            const rtl = getComputedStyle(root).direction === "rtl";
            const inKey = rtl ? "ArrowLeft" : "ArrowRight";
            const outKey = rtl ? "ArrowRight" : "ArrowLeft";

            switch (e.key) {
                case "ArrowDown": e.preventDefault(); focus(list[i + 1]); break;
                case "ArrowUp": e.preventDefault(); focus(list[i - 1]); break;
                case "Home": e.preventDefault(); focus(list[0]); break;
                case "End": e.preventDefault(); focus(list[list.length - 1]); break;
                case inKey:
                    e.preventDefault();
                    if (open === "false") setOpen(node, true);
                    else if (open === "true") focus($(":scope > ul > .oc-node:not(.is-filtered-out) .oc-link", node));
                    break;
                case outKey:
                    e.preventDefault();
                    if (open === "true") setOpen(node, false);
                    else focus($(":scope > .oc-row .oc-link", node.parentElement.closest(".oc-node") || node));
                    break;
            }
        });

        // Keep the selected unit in view.
        $(".oc-row.is-selected", root)?.scrollIntoView({ block: "nearest" });
    }

    /* ---------------- user search (assignment form) ---------------- */
    function userSearch(form) {
        const box = $("[data-oc-user-search]", form);
        if (!box) return;
        const query = $("[data-oc-user-query]", box);
        const results = $("[data-oc-user-results]", box);
        const userId = $("[name='Input.UserId']", form);
        const picked = $("[data-oc-user-picked]", form);
        const pickedName = $("[data-oc-user-name]", form);
        let timer = 0, seq = 0;

        const close = () => box.classList.remove("open");
        const esc = s => String(s ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#039;" }[c]));

        async function run() {
            const q = query.value.trim();
            if (q.length < 2) { close(); return; }
            const my = ++seq;
            try {
                const url = box.dataset.ocUserSearch + (box.dataset.ocUserSearch.includes("?") ? "&" : "?") + "q=" + encodeURIComponent(q);
                const response = await fetch(url, { credentials: "same-origin", headers: { Accept: "application/json" } });
                if (!response.ok || my !== seq) return;
                const users = await response.json();
                results.innerHTML = users.length
                    ? users.map(u => `<button type="button" class="dd-item" role="option" data-id="${esc(u.id)}" data-name="${esc(u.name)}">` +
                        `<span>${esc(u.name)}<br><small>${esc(u.detail || u.id)}</small></span></button>`).join("")
                    : `<div class="oc-user-empty">${esc(box.dataset.empty || "—")}</div>`;
                box.classList.add("open");
            } catch { close(); }
        }

        query.addEventListener("input", () => { clearTimeout(timer); timer = setTimeout(run, 250); });
        query.addEventListener("keydown", e => {
            if (e.key === "ArrowDown") { e.preventDefault(); $(".dd-item", results)?.focus(); }
            if (e.key === "Escape" && box.classList.contains("open")) { e.stopPropagation(); close(); }
            if (e.key === "Enter") e.preventDefault();
        });
        results.addEventListener("keydown", e => {
            const items = $$(".dd-item", results);
            const i = items.indexOf(document.activeElement);
            if (e.key === "ArrowDown") { e.preventDefault(); items[i + 1]?.focus(); }
            if (e.key === "ArrowUp") { e.preventDefault(); (items[i - 1] || query).focus(); }
        });
        results.addEventListener("click", e => {
            const item = e.target.closest(".dd-item");
            if (!item) return;
            userId.value = item.dataset.id;
            if (picked) { picked.hidden = false; pickedName.textContent = item.dataset.name; }
            query.value = "";
            close();
            userId.focus();
        });
        userId.addEventListener("input", () => { if (picked) picked.hidden = true; });
        document.addEventListener("click", e => { if (!box.contains(e.target)) close(); });
    }

    function init() {
        flash();
        tree();
        if (window.MX?.component) window.MX.component("oc-user-search", userSearch);
        else $$("[data-mx-init='oc-user-search']").forEach(userSearch);
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
    else init();
})();
