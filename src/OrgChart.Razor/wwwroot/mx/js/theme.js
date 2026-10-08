/* =========================================================
   THEME.JS
   ---------------------------------------------------------
   - Dark / Light / System
   - Ready Presets
   - Accent Color
   - UI Size
   - Border Radius
   - LocalStorage
   - Theme Settings Drawer
   - Reset
   - ESC to close
   ========================================================= */


(() => {

    "use strict";


    /* =====================================================
       CONFIG
       ===================================================== */

    const STORAGE_KEY = "app-theme";


    const DEFAULT_THEME = {
        mode: "dark",
        preset: "ocean-dark",
        accent: "cyan",
        size: "medium",
        radius: "medium"
    };


    /* =====================================================
       DOM
       ===================================================== */

    const root = document.documentElement;

    const drawer = document.getElementById("themeDrawer");

    const overlay = document.getElementById("themeOverlay");

    const openButton =
        document.getElementById("openThemeSettings");

    const closeButton =
        document.getElementById("closeThemeSettings");

    const resetButton =
        document.getElementById("resetTheme");


    /* =====================================================
       STATE
       ===================================================== */

    let theme = loadTheme();


    /* =====================================================
       STORAGE
       ===================================================== */

    function loadTheme() {

        try {

            const saved =
                localStorage.getItem(STORAGE_KEY);

            if (!saved) {

                return {
                    ...DEFAULT_THEME
                };

            }


            const parsed =
                JSON.parse(saved);


            return {
                ...DEFAULT_THEME,
                ...parsed
            };

        } catch (error) {

            console.warn(
                "Theme settings could not be loaded.",
                error
            );

            return {
                ...DEFAULT_THEME
            };

        }

    }


    function saveTheme() {

        try {

            localStorage.setItem(
                STORAGE_KEY,
                JSON.stringify(theme)
            );

        } catch (error) {

            console.warn(
                "Theme settings could not be saved.",
                error
            );

        }

    }


    /* =====================================================
       APPLY THEME
       ===================================================== */

    function applyTheme() {

        /*
         * Main theme mode
         *
         * data-theme="dark"
         * data-theme="light"
         * data-theme="system"
         */

        root.dataset.theme = theme.mode;


        /*
         * Ready preset
         */

        root.dataset.preset = theme.preset;


        /*
         * Accent color
         */

        root.dataset.accent = theme.accent;


        /*
         * UI size
         */

        root.dataset.size = theme.size;


        /*
         * Border radius
         */

        root.dataset.radius = theme.radius;


        /*
         * Save
         */

        saveTheme();


        /*
         * Update selected states
         */

        updateActiveStates();

    }


    /* =====================================================
       UPDATE ACTIVE BUTTONS
       ===================================================== */

    function updateActiveStates() {


        /* ---------------------------------------------
           MODE
           --------------------------------------------- */

        document
            .querySelectorAll("[data-mode]")
            .forEach(button => {

                const isActive =
                    button.dataset.mode === theme.mode;

                button.classList.toggle(
                    "active",
                    isActive
                );

                button.setAttribute(
                    "aria-pressed",
                    String(isActive)
                );

            });


        /* ---------------------------------------------
           PRESET
           --------------------------------------------- */

        document
            .querySelectorAll("[data-preset-select]")
            .forEach(button => {

                const isActive =
                    button.dataset.presetSelect ===
                    theme.preset;

                button.classList.toggle(
                    "active",
                    isActive
                );

                button.setAttribute(
                    "aria-pressed",
                    String(isActive)
                );

            });


        /* ---------------------------------------------
           ACCENT
           --------------------------------------------- */

        document
            .querySelectorAll("[data-accent-select]")
            .forEach(button => {

                const isActive =
                    button.dataset.accentSelect ===
                    theme.accent;

                button.classList.toggle(
                    "active",
                    isActive
                );

                button.setAttribute(
                    "aria-pressed",
                    String(isActive)
                );

            });


        /* ---------------------------------------------
           SIZE
           --------------------------------------------- */

        document
            .querySelectorAll("[data-size-select]")
            .forEach(button => {

                const isActive =
                    button.dataset.sizeSelect ===
                    theme.size;

                button.classList.toggle(
                    "active",
                    isActive
                );

                button.setAttribute(
                    "aria-pressed",
                    String(isActive)
                );

            });


        /* ---------------------------------------------
           RADIUS
           --------------------------------------------- */

        document
            .querySelectorAll("[data-radius-select]")
            .forEach(button => {

                const isActive =
                    button.dataset.radiusSelect ===
                    theme.radius;

                button.classList.toggle(
                    "active",
                    isActive
                );

                button.setAttribute(
                    "aria-pressed",
                    String(isActive)
                );

            });

    }


    /* =====================================================
       SET THEME VALUE
       ===================================================== */

    function setThemeValue(
        property,
        value
    ) {

        if (!property || !value) {
            return;
        }


        theme[property] = value;


        /*
         * اگر کاربر یک preset انتخاب کرد،
         * preset خودش رنگ اصلی دارد.
         *
         * بنابراین accent را دست نمی‌زنیم
         * تا کاربر بتواند رنگ Accent را مستقل تغییر دهد.
         */


        applyTheme();

    }


    /* =====================================================
       RESET
       ===================================================== */

    function resetTheme() {

        theme = {
            ...DEFAULT_THEME
        };


        applyTheme();

    }


    /* =====================================================
       DRAWER
       ===================================================== */

    function openDrawer() {

        if (!drawer || !overlay) {
            return;
        }


        drawer.classList.add("open");

        overlay.classList.add("open");


        drawer.setAttribute(
            "aria-hidden",
            "false"
        );


        /*
         * جلوگیری از Scroll صفحه اصلی
         */

        document.body.style.overflow =
            "hidden";


        /*
         * Focus روی دکمه بستن
         */

        if (closeButton) {

            setTimeout(() => {

                closeButton.focus();

            }, 100);

        }

    }


    function closeDrawer() {

        if (!drawer || !overlay) {
            return;
        }


        drawer.classList.remove("open");

        overlay.classList.remove("open");


        drawer.setAttribute(
            "aria-hidden",
            "true"
        );


        document.body.style.overflow = "";


        /*
         * برگرداندن Focus
         */

        if (openButton) {

            openButton.focus();

        }

    }


    function toggleDrawer() {

        if (
            drawer &&
            drawer.classList.contains("open")
        ) {

            closeDrawer();

        } else {

            openDrawer();

        }

    }


    /* =====================================================
       EVENT HANDLERS
       ===================================================== */

    function handleClick(event) {


        /* ---------------------------------------------
           OPEN DRAWER
           --------------------------------------------- */

        const openTarget =
            event.target.closest(
                "#openThemeSettings"
            );

        if (openTarget) {

            openDrawer();

            return;

        }


        /* ---------------------------------------------
           CLOSE DRAWER
           --------------------------------------------- */

        const closeTarget =
            event.target.closest(
                "#closeThemeSettings"
            );

        if (closeTarget) {

            closeDrawer();

            return;

        }


        /* ---------------------------------------------
           MODE
           --------------------------------------------- */

        const modeButton =
            event.target.closest(
                "[data-mode]"
            );

        if (modeButton) {

            setThemeValue(
                "mode",
                modeButton.dataset.mode
            );

            return;

        }


        /* ---------------------------------------------
           PRESET
           --------------------------------------------- */

        const presetButton =
            event.target.closest(
                "[data-preset-select]"
            );

        if (presetButton) {

            setThemeValue(
                "preset",
                presetButton.dataset.presetSelect
            );

            return;

        }


        /* ---------------------------------------------
           ACCENT
           --------------------------------------------- */

        const accentButton =
            event.target.closest(
                "[data-accent-select]"
            );

        if (accentButton) {

            setThemeValue(
                "accent",
                accentButton.dataset.accentSelect
            );

            return;

        }


        /* ---------------------------------------------
           SIZE
           --------------------------------------------- */

        const sizeButton =
            event.target.closest(
                "[data-size-select]"
            );

        if (sizeButton) {

            setThemeValue(
                "size",
                sizeButton.dataset.sizeSelect
            );

            return;

        }


        /* ---------------------------------------------
           RADIUS
           --------------------------------------------- */

        const radiusButton =
            event.target.closest(
                "[data-radius-select]"
            );

        if (radiusButton) {

            setThemeValue(
                "radius",
                radiusButton.dataset.radiusSelect
            );

            return;

        }


        /* ---------------------------------------------
           RESET
           --------------------------------------------- */

        const resetTarget =
            event.target.closest(
                "#resetTheme"
            );

        if (resetTarget) {

            resetTheme();

            return;

        }

    }


    /* =====================================================
       KEYBOARD
       ===================================================== */

    function handleKeyboard(event) {

        /*
         * ESC
         */

        if (
            event.key === "Escape" &&
            drawer &&
            drawer.classList.contains("open")
        ) {

            closeDrawer();

        }

    }


    /* =====================================================
       OVERLAY CLICK
       ===================================================== */

    function handleOverlayClick(event) {

        /*
         * فقط اگر خود Overlay کلیک شده باشد
         *
         * نه فرزندان احتمالی
         */

        if (
            event.target === overlay
        ) {

            closeDrawer();

        }

    }


    /* =====================================================
       SYSTEM COLOR SCHEME
       ===================================================== */

    function setupSystemThemeListener() {

        /*
         * اگر mode روی system باشد،
         * CSS با prefers-color-scheme
         * خودش رنگ‌ها را تغییر می‌دهد.
         *
         * بنابراین JS نیاز به تغییر اضافی ندارد.
         */

        const mediaQuery =
            window.matchMedia(
                "(prefers-color-scheme: dark)"
            );


        mediaQuery.addEventListener(
            "change",
            () => {

                if (theme.mode === "system") {

                    applyTheme();

                }

            }
        );

    }


    /* =====================================================
       INIT
       ===================================================== */

    function init() {

        /*
         * ابتدا Theme ذخیره شده را اعمال کن
         */

        applyTheme();


        /*
         * Global click
         */

        document.addEventListener(
            "click",
            handleClick
        );


        /*
         * Keyboard
         */

        document.addEventListener(
            "keydown",
            handleKeyboard
        );


        /*
         * Overlay
         */

        if (overlay) {

            overlay.addEventListener(
                "click",
                handleOverlayClick
            );

        }


        /*
         * System theme
         */

        setupSystemThemeListener();

    }


    /* =====================================================
       PUBLIC API
       ===================================================== */

    window.ThemeManager = {

        get() {

            return {
                ...theme
            };

        },


        set(
            property,
            value
        ) {

            setThemeValue(
                property,
                value
            );

        },


        reset() {

            resetTheme();

        },


        open() {

            openDrawer();

        },


        close() {

            closeDrawer();

        },


        toggle() {

            toggleDrawer();

        }

    };


    /* =====================================================
       START
       ===================================================== */

    if (
        document.readyState === "loading"
    ) {

        document.addEventListener(
            "DOMContentLoaded",
            init
        );

    } else {

        init();

    }


})();