(() => {
  const SEARCH_PATH = "/Buscar";
  let browseRequest = null;
  let mapHeightObserver = null;
  let mapHeightResizeHandler = null;
  let lastMapSharedHeight = null;

  document.addEventListener("DOMContentLoaded", () => {
    bindBrowseAjax();
    setupMapHeightSync();
    scrollToSearchPanelIfRequested();
  });

  function bindBrowseAjax() {
    if (document.documentElement.dataset.browseAjaxBound === "true") return;
    document.documentElement.dataset.browseAjaxBound = "true";

    document.addEventListener("submit", event => {
      const form = event.target.closest("#api-page [data-browse-search-form]");
      if (!form) return;

      event.preventDefault();

      const params = new URLSearchParams(new FormData(form));
      params.set("page", "1");
      loadBrowseResults(params, true, "search-panel");
    });

    document.addEventListener("click", event => {
      const link = event.target.closest("#api-page a[href]");
      if (!link || link.classList.contains("is-disabled")) return;

      const url = new URL(link.href, window.location.origin);
      if (
        url.origin !== window.location.origin ||
        url.pathname.toLowerCase() !== SEARCH_PATH.toLowerCase()
      ) return;

      event.preventDefault();
      const scrollTarget = url.hash === "#search-panel" ? "search-panel" : "results";
      loadBrowseResults(url.searchParams, true, scrollTarget);
    });

    window.addEventListener("popstate", () => {
      if (window.location.pathname.toLowerCase() !== SEARCH_PATH.toLowerCase()) return;
      const scrollTarget = window.location.hash === "#search-panel" ? "search-panel" : "none";
      loadBrowseResults(new URLSearchParams(window.location.search), false, scrollTarget);
    });
  }

  async function loadBrowseResults(params, updateHistory, scrollTarget = "none") {
    const host = document.getElementById("api-page");
    if (!host) return;

    if (browseRequest) {
      browseRequest.abort();
    }

    browseRequest = new AbortController();
    const apiUrl = `/api/content/browse?${params.toString()}`;

    host.setAttribute("aria-busy", "true");
    host.classList.add("is-loading");

    try {
      const response = await fetch(apiUrl, {
        headers: { "X-Requested-With": "fetch" },
        signal: browseRequest.signal
      });

      if (!response.ok) {
        throw new Error(`Error ${response.status} al cargar resultados`);
      }

      host.innerHTML = await response.text();
      host.dataset.apiEndpoint = apiUrl;

      if (updateHistory) {
        const nextHash = scrollTarget === "search-panel" ? "#search-panel" : "";
        window.history.pushState({}, "", `${SEARCH_PATH}?${params.toString()}${nextHash}`);
      }

      // site.js tiene su inicialización encapsulada. Volvemos a disparar su
      // flujo de inicialización para mapa, galería, favoritos y modales.
      document.dispatchEvent(new Event("DOMContentLoaded"));

      setupMapHeightSync();

      if (scrollTarget === "results") {
        scrollToBrowseResults(host, params.get("mode"));
      } else if (scrollTarget === "search-panel") {
        scrollToSearchPanel();
      }
    } catch (error) {
      if (error?.name !== "AbortError") {
        console.error(error);
        showBrowseError(host);
      }
    } finally {
      host.removeAttribute("aria-busy");
      host.classList.remove("is-loading");
      browseRequest = null;
    }
  }

  function scrollToBrowseResults(host, mode) {
    const normalizedMode = normalizeMode(mode);
    let attempts = 0;

    const tryScroll = () => {
      let target;

      if (normalizedMode === "galeria") {
        target = document.getElementById("search-panel");
      } else if (normalizedMode === "mapa") {
        target = host.querySelector(".map-canvas-shell, [data-map-layout] #map, [data-map-layout]");
      } else if (normalizedMode === "texto") {
        target = host.querySelector(".gallery-shell, .classified-toolbar, .classified-list");
      } else {
        target = host.querySelector("#gallery-first, .gallery-feed, .gallery-shell");
      }

      if (target) {
        target.scrollIntoView({
          behavior: "smooth",
          block: "start"
        });
        return;
      }

      attempts += 1;
      if (attempts < 40) {
        window.setTimeout(tryScroll, 50);
      } else {
        host.querySelector("[data-browse-scroll-target]")?.scrollIntoView({
          behavior: "smooth",
          block: "start"
        });
      }
    };

    window.requestAnimationFrame(tryScroll);
  }

  function scrollToSearchPanelIfRequested() {
    if (window.location.hash !== "#search-panel") return;
    scrollToSearchPanel();
  }

  function scrollToSearchPanel() {
    const target = document.getElementById("search-panel");
    if (!target) return;

    window.requestAnimationFrame(() => {
      target.scrollIntoView({
        behavior: "smooth",
        block: "start"
      });
    });
  }

  function setupMapHeightSync() {
    mapHeightObserver?.disconnect?.();
    mapHeightObserver = null;
    if (mapHeightResizeHandler) {
      window.removeEventListener("resize", mapHeightResizeHandler);
      mapHeightResizeHandler = null;
    }

    const layout = document.querySelector("[data-map-layout]");
    const mapCanvasShell = layout?.querySelector(".map-canvas-shell");
    const mapCanvas = layout?.querySelector("#map");
    const panel = layout?.querySelector("[data-map-selection-panel]");
    const card = layout?.querySelector("[data-map-selection-card]");

    if (!layout || !mapCanvas || !panel) return;

    const sync = () => {
      if (window.matchMedia("(max-width: 780px)").matches) {
        layout.style.removeProperty("--map-desktop-shared-height");
        if (mapCanvasShell) {
          mapCanvasShell.style.height = "";
          mapCanvasShell.style.minHeight = "";
          mapCanvasShell.style.maxHeight = "";
        }
        mapCanvas.style.height = "";
        mapCanvas.style.minHeight = "";
        mapCanvas.style.maxHeight = "";
        panel.style.height = "";
        panel.style.maxHeight = "";
        panel.style.minHeight = "";
        lastMapSharedHeight = null;
        resizeMap();
        return;
      }

      const contentHeight = Math.max(
        420,
        Math.ceil(panel.scrollHeight),
        Math.ceil(card?.scrollHeight || 0)
      );
      const viewportMax = Math.max(
        420,
        Math.floor(window.innerHeight)
      );
      const sharedHeight = Math.max(contentHeight, viewportMax);
      if (lastMapSharedHeight === sharedHeight) {
        return;
      }

      layout.style.setProperty("--map-desktop-shared-height", `${sharedHeight}px`);
      if (mapCanvasShell) {
        mapCanvasShell.style.height = `${sharedHeight}px`;
        mapCanvasShell.style.minHeight = `${sharedHeight}px`;
        mapCanvasShell.style.maxHeight = `${sharedHeight}px`;
      }
      mapCanvas.style.height = `${sharedHeight}px`;
      mapCanvas.style.minHeight = `${sharedHeight}px`;
      mapCanvas.style.maxHeight = `${sharedHeight}px`;
      panel.style.height = `${sharedHeight}px`;
      panel.style.minHeight = `${sharedHeight}px`;
      panel.style.maxHeight = `${sharedHeight}px`;
      lastMapSharedHeight = sharedHeight;
      resizeMap();
    };

    mapHeightObserver = new ResizeObserver(sync);
    mapHeightObserver.observe(panel);

    if (card) {
      mapHeightObserver.observe(card);
    }

    mapHeightResizeHandler = () => sync();
    window.addEventListener("resize", mapHeightResizeHandler);

    window.requestAnimationFrame(sync);
    window.setTimeout(sync, 100);
    window.setTimeout(sync, 400);
  }

  function resizeMap() {
    const mapNode = document.getElementById("map");
    const mapInstance =
      mapNode?._map ||
      mapNode?.map ||
      window.map ||
      window.contentMap;

    mapInstance?.resize?.();
  }

  function showBrowseError(host) {
    const error = document.createElement("div");
    error.className = "status-banner";
    error.textContent = "No se pudieron actualizar los resultados. Intentá nuevamente.";
    host.prepend(error);
  }

  function normalizeMode(value) {
    return String(value || "Galeria")
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .toLowerCase();
  }
})();
