(function () {
    function init(root) {
        root = root || document;
        if (root.documentElement ? root.documentElement.dataset.lightboxInitialized : root.dataset.lightboxInitialized) return;
        const dialog = root.querySelector('#photo-lightbox');
        if (!dialog) return;
        (root.documentElement || root).dataset.lightboxInitialized = 'true';
        const triggers = Array.from(root.querySelectorAll('[data-lightbox-trigger]'));
        const image = dialog.querySelector('[data-lightbox-image]');
        const caption = dialog.querySelector('#lightbox-title');
        const close = dialog.querySelector('[data-lightbox-close]');
        const previous = dialog.querySelector('[data-lightbox-previous]');
        const next = dialog.querySelector('[data-lightbox-next]');
        const error = dialog.querySelector('[data-lightbox-error]');
        const retry = dialog.querySelector('[data-lightbox-retry]');
        let activeIndex = -1;
        let activeTrigger = null;

        function setState(state) { dialog.dataset.state = state; error.hidden = state !== 'error'; }
        function updateControls() { previous.disabled = activeIndex <= 0; next.disabled = activeIndex >= triggers.length - 1; }
        function load(trigger) {
            activeTrigger = trigger;
            activeIndex = triggers.indexOf(trigger);
            caption.textContent = trigger.dataset.alt;
            image.alt = trigger.dataset.alt;
            updateControls();
            setState('loading');
            image.removeAttribute('src');
            image.src = trigger.dataset.webSrc;
        }
        function open(trigger) { load(trigger); dialog.showModal(); close.focus(); }
        function closeDialog() { dialog.close(); }
        function navigate(offset) { const target = triggers[activeIndex + offset]; if (target) load(target); }

        triggers.forEach(function (trigger) {
            trigger.addEventListener('click', function () { open(trigger); });
            const grid = trigger.querySelector('img');
            if (grid) grid.addEventListener('error', function () {
                trigger.closest('[data-photo-tile]').dataset.state = 'error';
                grid.hidden = true;
                trigger.querySelector('[data-image-unavailable]').hidden = false;
            });
        });
        image.addEventListener('load', function () { setState('ready'); });
        image.addEventListener('error', function () { setState('error'); });
        close.addEventListener('click', closeDialog);
        previous.addEventListener('click', function () { navigate(-1); });
        next.addEventListener('click', function () { navigate(1); });
        retry.addEventListener('click', function () {
            const url = new URL(activeTrigger.dataset.webSrc, window.location.origin);
            url.searchParams.set('retry', Date.now().toString());
            image.removeAttribute('src');
            setState('loading');
            image.src = url.pathname + url.search;
        });
        dialog.addEventListener('close', function () { image.removeAttribute('src'); setState('idle'); if (activeTrigger) activeTrigger.focus(); });
        dialog.addEventListener('click', function (event) { if (event.target === dialog) closeDialog(); });
        dialog.addEventListener('keydown', function (event) {
            if (event.key === 'ArrowLeft') { event.preventDefault(); navigate(-1); }
            if (event.key === 'ArrowRight') { event.preventDefault(); navigate(1); }
            if (event.key === 'Escape') closeDialog();
        });
    }
    window.FolderFolioLightbox = { init: init };
    document.addEventListener('DOMContentLoaded', function () { init(); });
}());
