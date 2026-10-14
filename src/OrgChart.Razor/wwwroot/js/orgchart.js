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
        const positions = $("[data-oc-positions]", panel);
        const empty = $("[data-oc-tree-empty]", panel);
        const count = $("[data-oc-count]", panel);
        const INACTIVE_KEY = "orgchart-show-inactive";
        const POSITIONS_KEY = "orgchart-show-positions";

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

        // Positions under their unit: on by default, remembered per browser.
        if (positions) {
            if (store.get(POSITIONS_KEY) === "0") positions.checked = false;
            const applyPositions = () => { root.classList.toggle("show-positions", positions.checked); filter(); };
            positions.addEventListener("change", () => { store.set(POSITIONS_KEY, positions.checked ? "1" : "0"); applyPositions(); });
            root.classList.toggle("show-positions", positions.checked);
        }

        // Search and type filter: a node stays when it matches or has a matching descendant; ancestors open up.
        function filter() {
            const q = norm(search?.value);
            const t = type?.value || "";
            const showInactive = root.classList.contains("show-inactive");
            const showPositions = root.classList.contains("show-positions");
            let visible = 0;

            const visit = node => {
                const isPosition = node.classList.contains("oc-pos");
                // A position follows its unit's type for the type filter.
                const self = (!q || norm(node.dataset.search).includes(q))
                    && (!t || (isPosition ? node.dataset.unitType : node.dataset.type) === t)
                    && (showInactive || !node.classList.contains("is-inactive"))
                    && (showPositions || !isPosition);
                const group = groupOf(node);
                let childMatch = false;
                if (group) $$(":scope > .oc-node", group).forEach(c => { if (visit(c)) childMatch = true; });
                const keep = self || childMatch;
                node.classList.toggle("is-filtered-out", !keep);
                node.classList.toggle("is-match", (q !== "" || t !== "") && self);
                if ((q || t) && childMatch) setOpen(node, true);
                if (self && !isPosition) visible++;
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
        const userId = $(`[name='${box.dataset.ocUserTarget || "Input.UserId"}']`, form);
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

    /* ---------------- unit form: types allowed under the chosen parent ---------------- */
    function unitForm(form) {
        const parent = $("[data-oc-parent]", form);
        const type = $("[data-oc-type-select]", form);
        const none = $("[data-oc-no-type]", form);
        if (!parent || !type) return;
        const sync = () => {
            const p = parent.selectedOptions[0];
            const isRoot = !parent.value;
            const parentLevel = p?.dataset.level ? +p.dataset.level : null;
            let firstAllowed = null;
            $$("option", type).forEach(o => {
                const level = o.dataset.level ? +o.dataset.level : null;
                const ok = isRoot ? o.dataset.root === "1" : (level === null || parentLevel === null || level > parentLevel);
                o.disabled = !ok;
                o.hidden = !ok;
                if (ok && !firstAllowed) firstAllowed = o;
            });
            if (type.selectedOptions[0]?.disabled) type.value = firstAllowed ? firstAllowed.value : "";
            if (none) none.hidden = !!firstAllowed;
        };
        parent.addEventListener("change", sync);
        sync();
    }

    /* ---------------- scope: the authority list only for a partial delegation ---------------- */
    function scopeFields(root) {
        $$("[data-oc-scope]", root).forEach(scope => {
            const full = $("[data-oc-scope-full]", scope);
            const list = $("[data-oc-scope-list]", scope);
            if (!full || !list) return;
            const sync = () => { list.hidden = full.checked; };
            $$("input[name='FullScope']", scope).forEach(r => r.addEventListener("change", sync));
            sync();
        });
    }

    /* ---------------- searchable selects: select[data-oc-select-search="placeholder"] ----------------
       A search box above the select; typing filters its options (and option groups), opens the list as a listbox and
       picks the first match; Enter or a click takes the choice and closes the list. */
    function selectSearch(root) {
        $$("select[data-oc-select-search]", root).forEach(select => {
            if (select.ocSearch) return;
            select.ocSearch = true;
            const input = document.createElement("input");
            input.type = "search";
            input.className = "input oc-select-search";
            input.placeholder = select.dataset.ocSelectSearch || "";
            input.setAttribute("aria-label", input.placeholder);
            input.autocomplete = "off";
            select.before(input);
            const options = Array.from(select.options);
            const close = () => { select.removeAttribute("size"); };

            input.addEventListener("input", () => {
                const q = norm(input.value);
                let shown = 0, first = null;
                options.forEach(o => {
                    const group = o.parentElement.tagName === "OPTGROUP" ? o.parentElement.label : "";
                    const hit = !q || (o.value && norm(o.text + " " + group).includes(q));
                    o.hidden = !hit;
                    if (hit && o.value) { shown++; first ??= o; }
                });
                $$("optgroup", select).forEach(g => { g.hidden = !Array.from(g.children).some(o => !o.hidden); });
                if (!q) { close(); return; }
                select.size = Math.max(2, Math.min(8, shown + $$("optgroup", select).filter(g => !g.hidden).length));
                if (first && (!select.selectedOptions[0] || select.selectedOptions[0].hidden)) {
                    select.value = first.value;
                    select.dispatchEvent(new Event("change", { bubbles: true }));
                }
            });
            input.addEventListener("keydown", e => {
                if (e.key === "Enter") { e.preventDefault(); close(); select.focus(); }
                else if (e.key === "ArrowDown" && select.size > 1) { e.preventDefault(); select.focus(); }
            });
            select.addEventListener("click", () => { if (select.size > 1) close(); });
            select.addEventListener("keydown", e => { if (e.key === "Enter" && select.size > 1) { e.preventDefault(); close(); } });
        });
    }


    /* ---------------- graphical chart (Diagram page) ---------------- */
    function diagram() {
        const root = $("[data-ocd]");
        if (!root) return;
        const scroller = $("[data-ocd-scroll]", root);
        const canvas = $("[data-ocd-canvas]", root);
        if (!canvas) return;
        const nodes = $$(".ocd-node", canvas);
        const search = $("[data-ocd-search]", root);
        const found = $("[data-ocd-found]", root);
        const zoomLabel = $("[data-ocd-zoom-label]", root);
        const ZOOMS = [0.3, 0.4, 0.5, 0.65, 0.8, 1, 1.15, 1.3, 1.5];
        let zoom = ZOOMS.includes(+store.get("oc.diagram.zoom")) ? +store.get("oc.diagram.zoom") : 1;
        let matches = [], current = -1;

        const setCollapsed = (li, collapsed) => {
            li.classList.toggle("is-collapsed", collapsed);
            const toggle = $(":scope > .ocd-box [data-ocd-toggle]", li);
            if (toggle) toggle.setAttribute("aria-expanded", collapsed ? "false" : "true");
        };
        const hasChildren = li => !!$(":scope > ul", li);

        function level(n) {
            for (const li of nodes) if (hasChildren(li)) setCollapsed(li, n > 0 && +li.dataset.depth >= n - 1);
            $$("[data-ocd-level]", root).forEach(b => b.classList.toggle("active", +b.dataset.ocdLevel === n));
            store.set("oc.diagram.level", String(n));
        }

        function setZoom(z) {
            zoom = z;
            canvas.style.zoom = String(z);
            if (zoomLabel) zoomLabel.textContent = fa(Math.round(z * 100)) + "٪";
            store.set("oc.diagram.zoom", String(z));
        }

        function centerOn(el) {
            el.scrollIntoView({ block: "nearest", inline: "center" });
        }

        // collapse / expand one branch
        canvas.addEventListener("click", e => {
            const toggle = e.target.closest("[data-ocd-toggle]");
            if (!toggle) return;
            const li = toggle.closest(".ocd-node");
            setCollapsed(li, !li.classList.contains("is-collapsed"));
        });
        canvas.addEventListener("keydown", e => {
            const box = e.target.closest(".ocd-box");
            if (!box || e.target !== box) return;
            if (e.key === "Enter" || e.key === " ") {
                e.preventDefault();
                const li = box.closest(".ocd-node");
                if (hasChildren(li)) setCollapsed(li, !li.classList.contains("is-collapsed"));
            }
        });

        // levels, positions, zoom, print
        $$("[data-ocd-level]", root).forEach(b => b.addEventListener("click", () => level(+b.dataset.ocdLevel)));
        const positions = $("[data-ocd-positions]", root);
        positions.checked = store.get("oc.diagram.positions") === "1";
        root.classList.toggle("show-positions", positions.checked);
        positions.addEventListener("change", () => {
            root.classList.toggle("show-positions", positions.checked);
            store.set("oc.diagram.positions", positions.checked ? "1" : "0");
        });
        $$("[data-ocd-zoom]", root).forEach(b => b.addEventListener("click", () => {
            const step = +b.dataset.ocdZoom, i = ZOOMS.indexOf(zoom);
            setZoom(step === 0 ? 1 : ZOOMS[Math.max(0, Math.min(ZOOMS.length - 1, i + step))]);
        }));
        scroller.addEventListener("wheel", e => {
            if (!e.ctrlKey) return;
            e.preventDefault();
            const i = ZOOMS.indexOf(zoom);
            setZoom(ZOOMS[Math.max(0, Math.min(ZOOMS.length - 1, i + (e.deltaY < 0 ? 1 : -1)))]);
        }, { passive: false });
        $("[data-ocd-print]", root)?.addEventListener("click", () => window.print());

        // pan: drag the chart (a click on a link or button still works)
        let pan = null, panned = false;
        scroller.addEventListener("pointerdown", e => {
            if (e.button !== 0 || e.target.closest("a, button, input, label")) return;
            pan = { x: e.clientX, y: e.clientY, left: scroller.scrollLeft, top: scroller.scrollTop };
            panned = false;
        });
        window.addEventListener("pointermove", e => {
            if (!pan) return;
            const dx = e.clientX - pan.x, dy = e.clientY - pan.y;
            if (!panned && Math.abs(dx) + Math.abs(dy) < 5) return;
            panned = true;
            scroller.classList.add("is-panning");
            scroller.scrollLeft = pan.left - dx;
            scroller.scrollTop = pan.top - dy;
        });
        window.addEventListener("pointerup", () => { pan = null; scroller.classList.remove("is-panning"); });
        scroller.addEventListener("click", e => { if (panned) { e.preventDefault(); e.stopPropagation(); panned = false; } }, true);

        // search: matching boxes highlighted, their branches opened; Enter = next match
        function find() {
            const q = norm(search.value);
            $$(".ocd-box.is-match", canvas).forEach(b => b.classList.remove("is-match", "is-current"));
            matches = [];
            current = -1;
            if (!q) { found.textContent = ""; return; }
            for (const li of nodes) {
                if (!norm(li.dataset.search).includes(q)) continue;
                matches.push(li);
                $(":scope > .ocd-box", li).classList.add("is-match");
                for (let up = li.parentElement.closest(".ocd-node"); up; up = up.parentElement.closest(".ocd-node")) setCollapsed(up, false);
            }
            found.textContent = matches.length ? (found.dataset.text || "{0}").replace("{0}", fa(matches.length)) : (found.dataset.none || "—");
            next();
        }
        function next() {
            if (!matches.length) return;
            $$(".ocd-box.is-current", canvas).forEach(b => b.classList.remove("is-current"));
            current = (current + 1) % matches.length;
            const box = $(":scope > .ocd-box", matches[current]);
            box.classList.add("is-current");
            centerOn(box);
        }
        let timer = 0;
        search.addEventListener("input", () => { clearTimeout(timer); timer = setTimeout(find, 200); });
        search.addEventListener("keydown", e => { if (e.key === "Enter") { e.preventDefault(); next(); } });

        // start: the saved level (big charts: two levels), the saved zoom, the top centred
        const saved = store.get("oc.diagram.level");
        level(saved !== null ? +saved : nodes.length > 40 ? 2 : 0);
        setZoom(zoom);
        const top = $(".ocd-box", canvas);
        if (top) requestAnimationFrame(() => centerOn(top));
    }

    function init() {
        flash();
        tree();
        diagram();
        selectSearch(document);
        const withSearch = fn => root => { fn(root); selectSearch(root); };
        if (window.MX?.component) {
            window.MX.component("oc-user-search", withSearch(userSearch));
            window.MX.component("oc-unit-form", withSearch(unitForm));
            window.MX.component("oc-delegation-form", withSearch(form => { userSearch(form); scopeFields(form); }));
            window.MX.component("oc-select-search", selectSearch);
        } else {
            $$("[data-mx-init='oc-user-search']").forEach(userSearch);
            $$("[data-mx-init='oc-unit-form']").forEach(unitForm);
            $$("[data-mx-init='oc-delegation-form']").forEach(form => { userSearch(form); scopeFields(form); });
        }
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
    else init();
})();
