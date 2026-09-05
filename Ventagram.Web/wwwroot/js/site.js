(() => {
  let ventagramFlashMessage = "";
  let mapLibreSdkPromise = null;
  let mapWarmupPromise = null;
  let openPublicationPreview = null;
  let mapSelectionLayoutObserver = null;
  let mapSelectionLayoutResizeHandler = null;
  let systemLoadingCounter = 0;
  let systemLoadingDelayTimer = null;
  const systemLoadingStorageKey = "ventagram:system-loading";
  let suppressNextBeforeUnloadSystemLoading = false;
  const lastClickedGalleryPublicationStorageKey = "ventagram:last-clicked-gallery-publication";
  const pendingAuthActionStorageKey = "ventagram:pending-auth-action";
  const favoriteLastListStorageKey = "ventagram:last-favorite-list-id";
  const likedPublicationsStorageKey = "ventagram:liked-publications";
  const publicationOpenModeStorageKey = "ventagram:publication-open-mode";
  const NAVIGATION_LOCALITY_COOKIE = "ventagram_nav_locality_id";
  const HEADER_PUBLICATION_GROUPS_COOKIE = "ventagram_header_groups";
  const MEDIA_PRELOAD_CONFIG = {
    mobilePreloadAds: 10,
    desktopPreloadRows: 5,
    galleryInitialItems: 2,
    maxConcurrentVideoPreloads: 3
  };
  const chatConfig = window.__VENTAGRAM_CHAT_CONFIG || {};
  const supportedMapBounds = [[-73.6, -56.5], [-52.0, -19.0]];
  const supportedMapCenter = [-60.5, -31.5];
  const MAP_ZOOM_OUT_FACTOR = 0.8;
  const defaultGeocodingSearchUrlTemplate = "https://nominatim.openstreetmap.org/search?format=jsonv2&addressdetails=1&limit=5&countrycodes=ar,uy,py&q={query}";
  const defaultReverseGeocodingUrlTemplate = "https://nominatim.openstreetmap.org/reverse?format=jsonv2&addressdetails=1&zoom=18&lat={lat}&lon={lng}";
  const detailDebugEnabled = window.location.hostname === "localhost"
    || window.location.hostname === "127.0.0.1"
    || window.location.search.includes("debugDetail=1");

  function detailDebugLog(label, data) {
    if (!detailDebugEnabled) return;
    if (typeof data === "undefined") {
      console.log(`[ventagram-detail] ${label}`);
      return;
    }
    console.log(`[ventagram-detail] ${label}`, data);
  }

  function syncPreviewOpenState() {
    const hasOpenOverlay = Boolean(document.querySelector(".preview-overlay.is-open"));
    document.body.classList.toggle("preview-open", hasOpenOverlay);
  }

  class MediaPreloadService {
    constructor(config) {
      this.config = config;
      this.preloadedImageUrls = new Set();
      this.preparedVideoUrls = new Set();
      this.inflightImageUrls = new Set();
      this.inflightVideoUrls = new Set();
      this.videoQueue = [];
      this.activeVideoPreloads = 0;
      this.galleryDeferredWarmups = new WeakSet();
      this.feedStates = new WeakMap();
      this.refreshFeedPreloads = this.throttle(this.refreshFeedPreloads.bind(this), 140);
    }

    bindFeed(feed, rail) {
      if (!feed || !rail) return;
      if (this.feedStates.has(feed)) {
        this.refreshFeedPreloads(feed);
        return;
      }

      const refresh = () => this.refreshFeedPreloads(feed);
      const state = {
        rail,
        observer: null,
        resizeObserver: null,
        mutationObserver: null
      };

      if ("IntersectionObserver" in window) {
        state.observer = new IntersectionObserver(entries => {
          if (entries.some(entry => entry.isIntersecting)) {
            refresh();
          }
        }, {
          root: null,
          rootMargin: "600px 0px",
          threshold: 0
        });
        state.observer.observe(feed);
      }

      if ("ResizeObserver" in window) {
        state.resizeObserver = new ResizeObserver(refresh);
        state.resizeObserver.observe(rail);
      } else {
        window.addEventListener("resize", refresh, { passive: true });
      }

      if ("MutationObserver" in window) {
        state.mutationObserver = new MutationObserver(refresh);
        state.mutationObserver.observe(rail, {
          childList: true,
          subtree: true
        });
      }

      window.addEventListener("scroll", refresh, { passive: true });
      this.feedStates.set(feed, state);
      refresh();
    }

    refreshFeedPreloads(feed) {
      const state = this.feedStates.get(feed);
      const rail = state?.rail || feed?.querySelector(".gallery-rail");
      if (!feed || !rail) return;

      const cards = Array.from(rail.querySelectorAll(".listing-card .card-image-wrap"));
      if (!cards.length) return;

      const firstVisibleIndex = this.findFirstVisibleIndex(cards);
      const upcomingCards = this.selectUpcomingCards(cards, firstVisibleIndex, rail);
      upcomingCards.forEach(card => this.preloadCardPrimaryMedia(card));
    }

    preloadCardPrimaryMedia(card) {
      if (!card) return;

      const videoUrl = String(card.dataset.videoUrl || "").trim();
      if (videoUrl) {
        this.prepareVideo(videoUrl, { preload: "auto" });
        return;
      }

      const images = this.parseCardImages(card);
      if (images.length) {
        this.preloadImage(images[0]);
      }
    }

    primeCardNavigation(card) {
      if (!card) return;

      const images = this.parseCardImages(card);
      if (images.length <= 1) return;

      const currentSource = card.querySelector(".gallery-carousel-image")?.currentSrc
        || card.querySelector(".gallery-carousel-image")?.src
        || "";
      const currentIndex = Math.max(0, images.findIndex(src => src === currentSource));
      const nextIndexes = Array.from(new Set([
        (currentIndex + 1) % images.length,
        (currentIndex - 1 + images.length) % images.length
      ]));

      nextIndexes.forEach(index => {
        const src = images[index];
        if (src && src !== currentSource) {
          this.preloadImage(src);
        }
      });
    }

    prepareDetailGallery(gallery) {
      if (!gallery) return;
      const items = this.getDetailGalleryItems(gallery);
      items.slice(0, this.config.galleryInitialItems).forEach(item => this.preloadDetailItem(item));
    }

    warmRemainingDetailGallery(gallery) {
      if (!gallery || this.galleryDeferredWarmups.has(gallery)) return;
      this.galleryDeferredWarmups.add(gallery);
      const items = this.getDetailGalleryItems(gallery);
      items.slice(this.config.galleryInitialItems).forEach(item => this.preloadDetailItem(item));
    }

    preloadDetailItem(item) {
      if (!item) return;

      const type = String(item.getAttribute("data-detail-media-type") || "image").toLowerCase();
      const src = String(item.getAttribute("data-detail-media-src") || item.getAttribute("src") || "").trim();
      if (!src) return;

      if (type === "video") {
        this.prepareVideo(src, { preload: "auto" });
      } else {
        this.preloadImage(src);
      }
    }

    prepareVideo(src, options = {}) {
      const normalizedSrc = String(src || "").trim();
      if (!normalizedSrc || this.preparedVideoUrls.has(normalizedSrc) || this.inflightVideoUrls.has(normalizedSrc)) {
        return;
      }

      this.videoQueue.push({
        src: normalizedSrc,
        preload: options.preload || "auto",
        poster: String(options.poster || "").trim()
      });
      this.flushVideoQueue();
    }

    flushVideoQueue() {
      while (this.activeVideoPreloads < this.config.maxConcurrentVideoPreloads && this.videoQueue.length) {
        const next = this.videoQueue.shift();
        if (!next || this.preparedVideoUrls.has(next.src) || this.inflightVideoUrls.has(next.src)) {
          continue;
        }

        this.inflightVideoUrls.add(next.src);
        this.activeVideoPreloads += 1;

        const video = document.createElement("video");
        video.muted = true;
        video.playsInline = true;
        video.preload = next.preload;
        if (next.poster) {
          video.poster = next.poster;
        }

        const finish = succeeded => {
          video.removeAttribute("src");
          video.load?.();
          if (succeeded) {
            this.preparedVideoUrls.add(next.src);
          }
          this.inflightVideoUrls.delete(next.src);
          this.activeVideoPreloads = Math.max(0, this.activeVideoPreloads - 1);
          this.flushVideoQueue();
        };

        video.addEventListener("loadedmetadata", () => finish(true), { once: true });
        video.addEventListener("canplay", () => finish(true), { once: true });
        video.addEventListener("error", () => finish(false), { once: true });
        video.src = next.src;
        video.load?.();
      }
    }

    preloadImage(src) {
      const normalizedSrc = String(src || "").trim();
      if (!normalizedSrc || this.preloadedImageUrls.has(normalizedSrc) || this.inflightImageUrls.has(normalizedSrc)) {
        return;
      }

      this.inflightImageUrls.add(normalizedSrc);
      const image = new Image();
      image.decoding = "async";
      image.loading = "eager";

      const clear = succeeded => {
        if (succeeded) {
          this.preloadedImageUrls.add(normalizedSrc);
        }
        this.inflightImageUrls.delete(normalizedSrc);
      };

      image.onload = () => clear(true);
      image.onerror = () => clear(false);
      image.src = normalizedSrc;
    }

    selectUpcomingCards(cards, firstVisibleIndex, rail) {
      if (!cards.length) return [];

      if (isMobileGalleryAutoplayContext()) {
        return cards.slice(firstVisibleIndex + 1, firstVisibleIndex + 1 + this.config.mobilePreloadAds);
      }

      const columns = Math.max(1, getGalleryColumnCount(rail));
      const totalCards = columns * this.config.desktopPreloadRows;
      return cards.slice(firstVisibleIndex + columns, firstVisibleIndex + columns + totalCards);
    }

    findFirstVisibleIndex(cards) {
      const viewportHeight = window.innerHeight || document.documentElement.clientHeight || 0;
      const viewportWidth = window.innerWidth || document.documentElement.clientWidth || 0;
      const index = cards.findIndex(card => {
        const rect = card.getBoundingClientRect();
        return rect.bottom > 0
          && rect.right > 0
          && rect.top < viewportHeight
          && rect.left < viewportWidth;
      });

      return index >= 0 ? index : 0;
    }

    parseCardImages(card) {
      return String(card?.dataset.images || "")
        .split("|||")
        .map(src => src.trim())
        .filter(Boolean);
    }

    getDetailGalleryItems(gallery) {
      return Array.from(gallery.querySelectorAll("[data-detail-media-item='true']"));
    }

    throttle(callback, waitMs) {
      let lastCallAt = 0;
      let timerId = null;
      return (...args) => {
        const now = Date.now();
        const remaining = waitMs - (now - lastCallAt);
        if (remaining <= 0) {
          lastCallAt = now;
          callback(...args);
          return;
        }

        window.clearTimeout(timerId);
        timerId = window.setTimeout(() => {
          lastCallAt = Date.now();
          callback(...args);
        }, remaining);
      };
    }
  }

  const mediaPreloadService = new MediaPreloadService(MEDIA_PRELOAD_CONFIG);

  function zoomOutLevel(zoom) {
    return Number((Number(zoom || 0) * MAP_ZOOM_OUT_FACTOR).toFixed(2));
  }

  function normalizePublicationOpenMode(value) {
    return String(value || "").trim().toLowerCase() === "page" ? "page" : "popup";
  }

  function getPublicationOpenMode() {
    try {
      return normalizePublicationOpenMode(localStorage.getItem(publicationOpenModeStorageKey));
    } catch {
      return "popup";
    }
  }

  function setPublicationOpenMode(mode) {
    const normalized = normalizePublicationOpenMode(mode);

    try {
      localStorage.setItem(publicationOpenModeStorageKey, normalized);
    } catch {
    }

    if (document.body) {
      document.body.dataset.publicationOpenMode = normalized;
    }

    document.querySelectorAll("[data-publication-open-mode]").forEach(control => {
      if (control.value !== normalized) {
        control.value = normalized;
      }
    });

    return normalized;
  }

  function wirePublicationOpenModePreference(root = document) {
    if (!document.body) return;

    setPublicationOpenMode(getPublicationOpenMode());

    if (document.body.dataset.publicationOpenModeBound === "true") return;

    document.body.dataset.publicationOpenModeBound = "true";

    root.addEventListener("change", event => {
      const control = event.target.closest?.("[data-publication-open-mode]");
      if (!control) return;
      setPublicationOpenMode(control.value);
    });

    const synchronizeAddedControls = new MutationObserver(records => {
      const hasOpenModeControl = records.some(record =>
        Array.from(record.addedNodes).some(node =>
          node instanceof Element
          && (node.matches("[data-publication-open-mode]")
            || node.querySelector("[data-publication-open-mode]"))
        )
      );

      if (hasOpenModeControl) {
        setPublicationOpenMode(getPublicationOpenMode());
      }
    });

    synchronizeAddedControls.observe(document.body, { childList: true, subtree: true });
  }

  function buildPublicationPageUrl(rawValue) {
    const value = String(rawValue || "").trim();
    if (!value) return "";

    try {
      const url = new URL(value, window.location.origin);
      if (url.pathname.startsWith("/api/content/details/")) {
        url.pathname = url.pathname.replace("/api/content/details/", "/Publications/Details/");
      }

      if (url.pathname.startsWith("/Publications/Details/") && !url.hash) {
        url.hash = "publication-start";
      }

      return url.origin === window.location.origin
        ? `${url.pathname}${url.search}${url.hash}`
        : url.toString();
    } catch {
      if (value.startsWith("/api/content/details/")) {
        return `${value.replace("/api/content/details/", "/Publications/Details/")}#publication-start`;
      }

      if (value.startsWith("/Publications/Details/") && !value.includes("#")) {
        return `${value}#publication-start`;
      }

      return value;
    }
  }

  function getPublicationPageUrl(trigger) {
    if (!trigger) return "";

    const href = trigger.getAttribute?.("href") || "";
    if (href && !href.startsWith("/api/content/details/")) {
      return buildPublicationPageUrl(href);
    }

    const publicUrl = trigger.getAttribute?.("data-public-url") || "";
    if (publicUrl) {
      return buildPublicationPageUrl(publicUrl);
    }

    const detailsUrl = trigger.getAttribute?.("data-details-url") || href;
    return buildPublicationPageUrl(detailsUrl);
  }

  function getDebugQuerySuffix() {
    return window.location.search.includes("debug=1") ? "?debug=1" : "";
  }

  function buildPublicationApiDetailsUrl(publicationId) {
    const normalizedId = String(publicationId || "").trim();
    if (!normalizedId) return "";
    return `/api/content/details/${normalizedId}${getDebugQuerySuffix()}`;
  }

  function addCompactAttributionControl(instance, sdk) {
    if (!instance || !sdk?.AttributionControl) return;
    instance.addControl(new sdk.AttributionControl({ compact: true }), "bottom-right");
  }

  async function detailDebugMeasure(label, callback, extra = undefined) {
    const start = performance.now();
    detailDebugLog(`${label}:start`, extra);
    try {
      const result = await callback();
      detailDebugLog(`${label}:done`, {
        ms: Number((performance.now() - start).toFixed(1)),
        ...extra
      });
      return result;
    } catch (error) {
      detailDebugLog(`${label}:error`, {
        ms: Number((performance.now() - start).toFixed(1)),
        error: error?.message || String(error),
        ...extra
      });
      throw error;
    }
  }

  document.addEventListener("DOMContentLoaded", async () => {
    wireSystemNavigationLoading();
    wirePublicationOpenModePreference(document);
    scheduleMapWarmup();
    wirePhoneMasks(document);
    wireHeaderGroupPreferences(document);
    wireGuestLocalityPrompt(document);
    wireRegisterAccountType(document);
    wireHybridLocalityPicker(document);
    wireReportModal();
    wireAuthRequiredModal();
    wireSuggestionModal();
    wirePublicationPreviewModal();
    wireDetailMediaOverlay();
    wireDetailGalleryLayout();
    wireFavoriteModal();
    wireFavoriteListModal();
    wireFavoriteActions(document);
    wireChatExperience(document);
    wireDynamicGalleryCards();
    initStaticGalleryFeeds(document);
    initFavoritesPage();
    await initRealtimeChat();
    await loadApiPage();
    setupMapSelectionLayoutSync(document);
    await initContentMaps();
    await resumePendingAuthAction();
    clearPersistedSystemLoading();
    forceHideSystemLoading();
  });

  function scheduleMapWarmup() {
    window.setTimeout(() => {
      warmupMapAssets().catch(error => {
        detailDebugLog("warmupMapAssets:error", {
          error: error?.message || String(error)
        });
      });
    }, 0);
  }

  function getDefaultMapConfig() {
    const body = document.body;
    if (!body) return null;

    const styleUrl = String(body.dataset.mapDefaultStyleUrl || "").trim();
    const tilesUrlTemplate = String(body.dataset.mapDefaultTilesUrl || "").trim();
    const attribution = String(body.dataset.mapDefaultAttribution || "").trim();
    if (!styleUrl && !tilesUrlTemplate) return null;

    return {
      styleUrl,
      tilesUrlTemplate,
      attribution
    };
  }

  async function warmupMapAssets() {
    if (mapWarmupPromise) {
      return mapWarmupPromise;
    }

    mapWarmupPromise = (async () => {
      const config = getDefaultMapConfig();
      if (!config) return;

      const sdk = await loadMapLibreSdk();
      if (!sdk?.Map) return;

      const warmupHost = document.createElement("div");
      warmupHost.setAttribute("aria-hidden", "true");
      warmupHost.style.position = "fixed";
      warmupHost.style.left = "-9999px";
      warmupHost.style.top = "-9999px";
      warmupHost.style.width = "256px";
      warmupHost.style.height = "256px";
      warmupHost.style.pointerEvents = "none";
      warmupHost.style.opacity = "0";
      document.body.appendChild(warmupHost);

      let warmupMap = null;

      try {
        await detailDebugMeasure("mapWarmup:init", async () => {
          warmupMap = new sdk.Map({
            container: warmupHost,
            style: buildMapStyle(config.styleUrl, config.tilesUrlTemplate, config.attribution),
            center: supportedMapCenter,
            zoom: 4.8,
            maxBounds: supportedMapBounds,
            interactive: false,
            attributionControl: false,
            fadeDuration: 0
          });

          await new Promise(resolve => {
            let settled = false;
            const finish = () => {
              if (settled) return;
              settled = true;
              resolve();
            };

            warmupMap.once("idle", finish);
            warmupMap.once("load", () => {
              window.setTimeout(finish, 250);
            });
            window.setTimeout(finish, 2500);
          });
        });
      } finally {
        try {
          warmupMap?.remove?.();
        } catch {
          // Ignore cleanup failures.
        }
        warmupHost.remove();
      }
    })();

    return mapWarmupPromise;
  }

  function getCurrentReturnUrl() {
    return `${window.location.pathname}${window.location.search}${window.location.hash}`;
  }

  function writePendingAuthAction(action) {
    try {
      if (!action || !action.type) {
        sessionStorage.removeItem(pendingAuthActionStorageKey);
        return;
      }

      sessionStorage.setItem(pendingAuthActionStorageKey, JSON.stringify({
        ...action,
        createdAt: Date.now(),
        returnUrl: action.returnUrl || getCurrentReturnUrl()
      }));
    } catch {
      // Ignore storage failures.
    }
  }

  function readPendingAuthAction() {
    try {
      const raw = sessionStorage.getItem(pendingAuthActionStorageKey);
      if (!raw) return null;
      const parsed = JSON.parse(raw);
      if (!parsed || typeof parsed !== "object" || !parsed.type) return null;
      return parsed;
    } catch {
      return null;
    }
  }

  function clearPendingAuthAction() {
    try {
      sessionStorage.removeItem(pendingAuthActionStorageKey);
    } catch {
      // Ignore storage failures.
    }
  }

  async function resumePendingAuthAction() {
    if (document.body?.dataset.userAuthenticated !== "true") return;

    const action = readPendingAuthAction();
    if (!action) return;

    if (action.returnUrl && action.returnUrl !== getCurrentReturnUrl()) {
      return;
    }

    clearPendingAuthAction();

    if (action.type === "report") {
      openReportModalFromAction(action);
      return;
    }

    if (action.type === "favorite-toggle") {
      await openFavoriteModalByPayload(action);
    }
  }

  async function loadApiPage() {
    const host = document.getElementById("api-page");
    if (!host) return;
    const loadingTicket = beginSystemLoading();

    try {
      const response = await fetch(host.dataset.apiEndpoint, {
        headers: { "X-Requested-With": "fetch" }
      });

      host.innerHTML = await response.text();
      applyInitialViewportMapHeight(host);
      wirePhoneMasks(host);
      wireHeaderGroupPreferences(host);

      if (ventagramFlashMessage) {
        const banner = document.createElement("div");
        banner.className = "status-banner";
        banner.textContent = ventagramFlashMessage;
        host.prepend(banner);
        ventagramFlashMessage = "";
      }

      setupMapSelectionLayoutSync(host);

      try {
        await initContentMaps();
      } catch (error) {
        console.error(error);
      }
      await wireInfiniteGalleryFeeds(host);
      wireGalleryCards();
      wireGalleryActionMenus();
      wireDynamicGalleryCards();
      wireFavoriteActions(host);
      wireReportForm();
      wireCreateForm();
      wireBrowseSearchFilters(host);
      wireChatExperience(host);
      scrollToRequestedAnchor(host);
    } finally {
      endSystemLoading(loadingTicket);
    }
  }

  function beginSystemLoading() {
    systemLoadingCounter += 1;
    if (systemLoadingCounter === 1) {
      window.clearTimeout(systemLoadingDelayTimer);
      systemLoadingDelayTimer = window.setTimeout(() => {
        if (systemLoadingCounter > 0) {
          const overlay = document.getElementById("systemLoadingOverlay");
          overlay?.removeAttribute("hidden");
          document.body.classList.add("system-loading-active");
        }
      }, 220);
    }

    return Symbol("system-loading");
  }

  function endSystemLoading(_ticket) {
    systemLoadingCounter = Math.max(0, systemLoadingCounter - 1);
    if (systemLoadingCounter > 0) return;

    window.clearTimeout(systemLoadingDelayTimer);
    systemLoadingDelayTimer = null;
    const overlay = document.getElementById("systemLoadingOverlay");
    overlay?.setAttribute("hidden", "hidden");
    document.body.classList.remove("system-loading-active");
  }

  function showSystemLoadingImmediately() {
    const overlay = document.getElementById("systemLoadingOverlay");
    overlay?.removeAttribute("hidden");
    document.body.classList.add("system-loading-active");
  }

  function forceHideSystemLoading() {
    systemLoadingCounter = 0;
    window.clearTimeout(systemLoadingDelayTimer);
    systemLoadingDelayTimer = null;
    const overlay = document.getElementById("systemLoadingOverlay");
    overlay?.setAttribute("hidden", "hidden");
    document.body.classList.remove("system-loading-active");
  }

  function persistSystemLoading() {
    try {
      sessionStorage.setItem(systemLoadingStorageKey, "1");
    } catch {
      // Ignore storage failures.
    }
  }

  function clearPersistedSystemLoading() {
    try {
      sessionStorage.removeItem(systemLoadingStorageKey);
    } catch {
      // Ignore storage failures.
    }
  }

  function rememberLastClickedGalleryPublication(publicationId) {
    const normalizedPublicationId = String(publicationId || "").trim();
    if (!normalizedPublicationId) return;

    try {
      sessionStorage.setItem(lastClickedGalleryPublicationStorageKey, normalizedPublicationId);
    } catch {
      // Ignore storage failures.
    }
  }

  function readLastClickedGalleryPublication() {
    try {
      return String(sessionStorage.getItem(lastClickedGalleryPublicationStorageKey) || "").trim();
    } catch {
      return "";
    }
  }

  function syncLastClickedGalleryCard(root = document) {
    const lastPublicationId = readLastClickedGalleryPublication();
    const cards = root.matches?.(".listing-card[data-publication-id]")
      ? [root]
      : Array.from(root.querySelectorAll?.(".listing-card[data-publication-id]") || []);

    cards.forEach(card => {
      const isMatch = lastPublicationId && String(card.dataset.publicationId || "").trim() === lastPublicationId;
      card.classList.toggle("is-last-clicked", Boolean(isMatch));
    });
  }

  function shouldTrackNavigationLink(link) {
    if (!link) return false;
    if (link.hasAttribute("download")) return false;
    if ((link.getAttribute("target") || "").trim() === "_blank") return false;
    if ((link.getAttribute("rel") || "").includes("external")) return false;
    if ((link.getAttribute("href") || "").startsWith("#")) return false;

    const href = link.href;
    if (!href) return false;

    try {
      const url = new URL(href, window.location.href);
      return url.origin === window.location.origin;
    } catch {
      return false;
    }
  }

  function shouldSuppressSystemLoadingForLink(link) {
    if (!link) return false;

    const rawHref = String(link.getAttribute("href") || "").trim();
    if (!rawHref) return false;

    if (/^(mailto:|tel:|sms:|whatsapp:)/i.test(rawHref)) {
      return true;
    }

    try {
      const url = new URL(rawHref, window.location.href);
      if (url.origin !== window.location.origin) {
        return true;
      }

      return /(^|\.)wa\.me$/i.test(url.hostname);
    } catch {
      return false;
    }
  }

  function isInlineLinkControl(target, link) {
    if (!(target instanceof Element) || !link) return false;
    if (link.classList?.contains("publication-preview-trigger")) return true;
    if (link.matches?.("[data-auth-required-favorites='true']")) return true;
    if (link.matches?.("[data-auth-required-login-trigger='true']")) return true;

    const control = target.closest([
      ".gallery-nav",
      ".report-trigger",
      ".favorite-toggle",
      "[data-gallery-play-toggle='true']",
      "[data-gallery-audio-toggle='true']",
      "[data-gallery-menu-toggle='true']",
      "[data-gallery-menu]",
      ".upload-action"
    ].join(","));

    return Boolean(control && link.contains(control));
  }

  function opensLinkInSeparateContext(event, link) {
    if (!link) return false;
    if (event.defaultPrevented) return true;
    if (event.button !== 0) return true;
    if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return true;

    const target = (link.getAttribute("target") || "").trim().toLowerCase();
    if (target && target !== "_self") return true;

    return false;
  }

  function wireSystemNavigationLoading() {
    if (document.body?.dataset.systemLoadingBound === "true") return;
    document.body.dataset.systemLoadingBound = "true";

    document.addEventListener("click", event => {
      const link = event.target.closest("a[href]");
      suppressNextBeforeUnloadSystemLoading = shouldSuppressSystemLoadingForLink(link);
      if (!shouldTrackNavigationLink(link)) return;
      if (link?.dataset.skipSystemLoading === "true") return;
      if (isInlineLinkControl(event.target, link)) return;
      if (opensLinkInSeparateContext(event, link)) return;
      suppressNextBeforeUnloadSystemLoading = false;
      persistSystemLoading();
      showSystemLoadingImmediately();
    }, true);

    document.addEventListener("submit", event => {
      const form = event.target;
      if (!(form instanceof HTMLFormElement)) return;
      if (event.defaultPrevented) return;
      if (form.dataset.skipSystemLoading === "true") return;
      persistSystemLoading();
      showSystemLoadingImmediately();
    });

    window.addEventListener("beforeunload", () => {
      if (suppressNextBeforeUnloadSystemLoading) {
        suppressNextBeforeUnloadSystemLoading = false;
        forceHideSystemLoading();
        clearPersistedSystemLoading();
        return;
      }

      persistSystemLoading();
      showSystemLoadingImmediately();
    });

    window.addEventListener("pageshow", () => {
      suppressNextBeforeUnloadSystemLoading = false;
      clearPersistedSystemLoading();
      forceHideSystemLoading();
    });
  }

  function getViewportMapHeight() {
    return Math.max(
      420,
      Math.floor(window.visualViewport?.height || window.innerHeight || 0)
    );
  }

  function applyInitialViewportMapHeight(root = document) {
    if (window.matchMedia("(max-width: 780px)").matches) return;

    const layout = root.querySelector?.("[data-map-layout]");
    const mapCanvas = layout?.querySelector(".map-canvas");
    const sidebar = layout?.querySelector(".map-sidebar");
    const panel = layout?.querySelector("[data-map-selection-panel]");
    if (!layout || !mapCanvas || !panel) return;

    const viewportHeight = getViewportMapHeight();
    layout.style.setProperty("--map-desktop-shared-height", `${viewportHeight}px`);
    mapCanvas.style.height = `${viewportHeight}px`;
    mapCanvas.style.minHeight = `${viewportHeight}px`;
    mapCanvas.style.maxHeight = `${viewportHeight}px`;
    panel.style.height = `${viewportHeight}px`;
    panel.style.minHeight = `${viewportHeight}px`;
    panel.style.maxHeight = `${viewportHeight}px`;

    if (sidebar) {
      sidebar.style.minHeight = `${viewportHeight}px`;
    }
  }

  function setupMapSelectionLayoutSync(root = document) {
    mapSelectionLayoutObserver?.disconnect?.();
    mapSelectionLayoutObserver = null;

    if (mapSelectionLayoutResizeHandler) {
      window.removeEventListener("resize", mapSelectionLayoutResizeHandler);
      mapSelectionLayoutResizeHandler = null;
    }

    const layout = root.querySelector?.("[data-map-layout]");
    const mapCanvas = layout?.querySelector(".map-canvas");
    const panel = layout?.querySelector("[data-map-selection-panel]");
    const card = layout?.querySelector("[data-map-selection-card]");

    if (!layout || !mapCanvas || !panel) return;

    const sync = () => {
      if (window.matchMedia("(max-width: 780px)").matches) {
        layout.style.removeProperty("--map-desktop-shared-height");
        mapCanvas.style.height = "";
        mapCanvas.style.minHeight = "";
        mapCanvas.style.maxHeight = "";
        panel.style.height = "";
        panel.style.minHeight = "";
        panel.style.maxHeight = "";
        return;
      }

      panel.style.height = "auto";
      panel.style.minHeight = "0";
      const viewportHeight = getViewportMapHeight();
      const contentHeight = Math.max(
        420,
        Math.ceil(panel.scrollHeight),
        Math.ceil(card?.scrollHeight || 0)
      );
      const sharedHeight = Math.max(viewportHeight, contentHeight);

      layout.style.setProperty("--map-desktop-shared-height", `${sharedHeight}px`);
      mapCanvas.style.height = `${sharedHeight}px`;
      mapCanvas.style.minHeight = `${sharedHeight}px`;
      mapCanvas.style.maxHeight = `${sharedHeight}px`;
      panel.style.maxHeight = `${sharedHeight}px`;

      const mapInstance =
        mapCanvas?._map ||
        mapCanvas?.map ||
        window.map ||
        window.contentMap;

      mapInstance?.resize?.();
    };

    mapSelectionLayoutObserver = new ResizeObserver(sync);
    mapSelectionLayoutObserver.observe(panel);

    if (card) {
      mapSelectionLayoutObserver.observe(card);
    }

    mapSelectionLayoutResizeHandler = () => sync();
    window.addEventListener("resize", mapSelectionLayoutResizeHandler);

    window.requestAnimationFrame(sync);
    window.setTimeout(sync, 100);
    window.setTimeout(sync, 400);
  }

  function scrollToRequestedAnchor(root = document) {
    const hash = window.location.hash;
    let target = null;

    if (!hash && /^\/Publications\/Details\/\d+$/i.test(window.location.pathname)) {
      target = document.getElementById("publication-start");
    } else if (hash === "#publication-start") {
      target = document.getElementById("publication-start");
    } else if (hash === "#search-panel") {
      target = document.getElementById("search-panel");
    } else if (hash === "#browse-results") {
      const currentMode = new URLSearchParams(window.location.search).get("mode");
      const normalizedMode = String(currentMode || "").trim().toLowerCase();
      target = normalizedMode === "galeria"
        ? document.getElementById("search-panel")
        : root.querySelector?.("[data-browse-scroll-target]");
    }

    if (!target) return;

    window.requestAnimationFrame(() => {
      target.scrollIntoView({
        behavior: hash === "#search-panel" || hash === "#browse-results" ? "smooth" : "auto",
        block: "start"
      });
    });
  }

  function scrollMapIntoViewAfterRender(mapElement) {
    if (window.location.hash !== "#browse-results") return;
    const target =
      mapElement?.closest?.("[data-browse-scroll-target]") ||
      mapElement?.closest?.("[data-map-layout]");
    if (!target) return;

    window.requestAnimationFrame(() => {
      target.scrollIntoView({ behavior: "smooth", block: "start" });
    });
  }

  function wireReportModal() {
    const reportModal = document.getElementById("reportModal");
    if (!reportModal) return;

    const closeReport = () => {
      reportModal.hidden = true;
      reportModal.classList.remove("is-open");
      syncPreviewOpenState();
    };

    reportModal.addEventListener("click", event => {
      const closeTrigger = event.target.closest("[data-report-close='true']");
      if (!closeTrigger) return;
      event.preventDefault();
      event.stopPropagation();
      closeReport();
    });

    document.addEventListener("keydown", event => {
      if (event.key === "Escape" && reportModal.classList.contains("is-open")) {
        closeReport();
      }
    });

    document.addEventListener("click", event => {
      const trigger = event.target.closest(".report-trigger");
      if (!trigger) return;

      event.preventDefault();
      event.stopPropagation();
      const publicationId = trigger.getAttribute("data-publication-id");
      const publicationCode = trigger.getAttribute("data-publication-code");
      const title = stripOpportunitySuffix(trigger.getAttribute("data-publication-title"));

      const reportModalAllowed = document.body?.dataset.reportModalAllowed === "true";
      const reportBlockMessage = String(document.body?.dataset.reportBlockMessage || "").trim();
      const isAuthenticated = document.body?.dataset.userAuthenticated === "true";

      if (!isAuthenticated) {
        showAuthRequiredModal({
          title: "Debes iniciar sesión para denunciar",
          message: "Para denunciar una publicación debes ingresar con tu usuario.",
          showRegister: true,
          showLogin: true,
          pendingAction: {
            type: "report",
            publicationId,
            publicationCode,
            publicationTitle: title || ""
          }
        });
        return;
      }

      if (!reportModalAllowed) {
        showAuthRequiredModal({
          title: "No puedes denunciar por el momento",
          message: reportBlockMessage || "Tu cuenta no cumple los requisitos para denunciar publicaciones.",
          showRegister: false,
          showLogin: false
        });
        return;
      }

      openReportModalFromAction({
        publicationId,
        publicationCode,
        publicationTitle: title || ""
      });
    });
  }

  function openReportModalFromAction(action = {}) {
    const reportModal = document.getElementById("reportModal");
    if (!reportModal) return;

    const publicationId = action.publicationId || "0";
    const publicationCode = action.publicationCode || "";
    const title = stripOpportunitySuffix(action.publicationTitle || "");
    const idInput = reportModal.querySelector('input[name="publicationId"]');
    const titleNode = reportModal.querySelector("#reportModalTitle");
    const defaultReason = reportModal.querySelector('input[name="reason"]:checked')
      || reportModal.querySelector('input[name="reason"]');

    if (idInput) idInput.value = publicationId || "0";
    if (titleNode) {
      const titlePrefix = publicationCode ? `${publicationCode} · ` : "";
      titleNode.textContent = title ? `${titlePrefix}Denunciar: ${title}` : "Selecciona un motivo";
    }
    if (defaultReason) defaultReason.checked = true;

    reportModal.hidden = false;
    reportModal.classList.add("is-open");
    document.body.classList.add("preview-open");
  }

  function wireAuthRequiredModal() {
    const modal = document.getElementById("authRequiredModal");
    if (!modal) return;
    if (modal.dataset.bound === "true") return;
    modal.dataset.bound = "true";

    const close = () => {
      modal.hidden = true;
      modal.classList.remove("is-open");
      syncPreviewOpenState();
    };

    modal.addEventListener("click", event => {
      const closeTrigger = event.target.closest("[data-auth-required-close='true']");
      if (!closeTrigger) return;
      event.preventDefault();
      close();
    });

    document.addEventListener("keydown", event => {
      if (event.key === "Escape" && modal.classList.contains("is-open")) {
        close();
      }
    });

    document.addEventListener("click", event => {
      const link = event.target.closest("[data-auth-required-favorites='true']");
      if (!link) return;

      event.preventDefault();
      event.stopPropagation();
      showAuthRequiredModal({
        title: "Debes iniciar sesión",
        message: "Puedes crear listas de anuncios favoritos para hacer seguimiento solo con una cuenta registrada.",
        showRegister: true,
        showLogin: true,
        desiredReturnUrl: link.getAttribute("href") || "/Favorites"
      });
    });

    document.addEventListener("click", event => {
      const link = event.target.closest("[data-auth-required-login-trigger='true']");
      if (!link) return;

      event.preventDefault();
      event.stopPropagation();
      showAuthRequiredModal({
        title: "Iniciar sesión",
        message: "Ingresa con tu cuenta para continuar.",
        showRegister: true,
        showLogin: true,
        desiredReturnUrl: getCurrentReturnUrl()
      });
    });
  }

  function showAuthRequiredModal(options = {}) {
    const modal = document.getElementById("authRequiredModal");
    if (!modal) return;
    clearPersistedSystemLoading();
    forceHideSystemLoading();
    const title = modal.querySelector("[data-auth-required-title]");
    const message = modal.querySelector("[data-auth-required-message]");
    const loginLink = modal.querySelector("[data-auth-required-login]");
    const registerLink = modal.querySelector("[data-auth-required-register]");
    const loginForm = modal.querySelector("[data-auth-required-login-form]");
    const loginShell = modal.querySelector(".auth-required-input-shell");
    const returnUrlInput = modal.querySelector("[data-auth-required-return-url]");
    const actions = modal.querySelector("[data-auth-required-actions]");
    const loginUrl = document.body?.dataset.reportLoginUrl || "/Account/Login";
    const loginSubmitUrl = options.loginSubmitUrl || "/Account/Login";
    const showLogin = options.showLogin !== false;
    const showRegister = options.showRegister !== false;

    if (title) {
      title.textContent = options.title || "Acción no disponible";
    }

    if (message) {
      message.textContent = options.message || "Debes iniciar sesión para continuar.";
    }

    if (loginLink) {
      loginLink.hidden = !showLogin;
      loginLink.setAttribute("href", options.loginUrl || loginUrl);
    }

    if (loginForm) {
      loginForm.hidden = !showLogin;
      loginForm.setAttribute("action", loginSubmitUrl);
    }

    if (loginShell) {
      loginShell.hidden = !showLogin;
    }

    if (returnUrlInput) {
      const desiredReturnUrl = options.desiredReturnUrl;
      const loginTarget = options.loginUrl || loginUrl;
      const fallbackReturnUrl = `${window.location.pathname}${window.location.search}${window.location.hash}`;

      if (desiredReturnUrl) {
        returnUrlInput.value = desiredReturnUrl;
      } else {
        try {
          const parsed = new URL(loginTarget, window.location.origin);
          returnUrlInput.value = parsed.searchParams.get("returnUrl") || fallbackReturnUrl;
        } catch {
          returnUrlInput.value = fallbackReturnUrl;
        }
      }
    }

    if (registerLink) {
      registerLink.hidden = !showRegister;
    }

    if (actions) {
      actions.hidden = !showLogin && !showRegister;
    }

    writePendingAuthAction(options.pendingAction || null);

    modal.hidden = false;
    modal.classList.add("is-open");
    document.body.classList.add("preview-open");
  }

  function wireSuggestionModal() {
    const modal = document.getElementById("suggestionModal");
    const form = document.getElementById("suggestionForm");
    if (!modal || !form) return;

    const titleNode = modal.querySelector(".modal-title");
    const introNode = form.querySelector("[data-suggestion-intro]");
    const labelNode = form.querySelector("[data-suggestion-label]");
    const textarea = form.querySelector("[data-suggestion-textarea]");
    const prefixInput = form.querySelector("[data-suggestion-prefix]");
    const feedback = form.querySelector("[data-suggestion-feedback]");
    const defaultTitle = titleNode?.textContent || "Sugerencias para Ventagram";
    const defaultIntro = introNode?.textContent || "A Ventagram lo mejoramos entre todos.";
    const defaultLabel = labelNode?.textContent || "Tu sugerencia";
    const defaultPlaceholder = textarea?.getAttribute("placeholder") || "Escribe aquí tu sugerencia para el sitio.";

    const configureSuggestionModal = button => {
      const title = String(button?.getAttribute("data-suggestion-title") || "").trim();
      const intro = String(button?.getAttribute("data-suggestion-intro") || "").trim();
      const label = String(button?.getAttribute("data-suggestion-label") || "").trim();
      const placeholder = String(button?.getAttribute("data-suggestion-placeholder") || "").trim();
      const prefix = String(button?.getAttribute("data-suggestion-prefix") || "").trim();

      if (titleNode) {
        titleNode.textContent = title || defaultTitle;
      }
      if (introNode) {
        introNode.textContent = intro || defaultIntro;
      }
      if (labelNode) {
        labelNode.textContent = label || defaultLabel;
      }
      if (textarea) {
        textarea.setAttribute("placeholder", placeholder || defaultPlaceholder);
      }
      if (prefixInput) {
        prefixInput.value = prefix;
      }
    };

    const close = () => {
      modal.hidden = true;
      modal.classList.remove("is-open");
      syncPreviewOpenState();
      if (feedback) {
        feedback.hidden = true;
        feedback.className = "status-banner";
        feedback.textContent = "";
      }
    };

    if (modal.dataset.bound === "true") return;
    modal.dataset.bound = "true";

    modal.addEventListener("click", event => {
      const closeTrigger = event.target.closest("[data-suggestion-close='true']");
      if (!closeTrigger) return;
      event.preventDefault();
      close();
    });

    document.addEventListener("keydown", event => {
      if (event.key === "Escape" && modal.classList.contains("is-open")) {
        close();
      }
    });

    document.addEventListener("click", event => {
      const button = event.target.closest("[data-suggestion-open='true']");
      if (!button) {
        return;
      }

      event.preventDefault();
      configureSuggestionModal(button);
      if (textarea) {
        textarea.value = "";
      }
      if (feedback) {
        feedback.hidden = true;
        feedback.className = "status-banner";
        feedback.textContent = "";
      }
      modal.hidden = false;
      modal.classList.add("is-open");
      document.body.classList.add("preview-open");
      textarea?.focus();
    });

    form.addEventListener("submit", async event => {
      event.preventDefault();

      const message = String(textarea?.value || "").trim();
      const prefix = String(prefixInput?.value || "").trim();
      if (!message) {
        if (feedback) {
          feedback.hidden = false;
          feedback.className = "status-banner warning";
          feedback.textContent = "Escribe una sugerencia antes de enviarla.";
        }
        return;
      }

      const submitButton = form.querySelector('button[type="submit"]');
      if (submitButton) {
        submitButton.disabled = true;
      }

      try {
        const response = await fetch(form.getAttribute("action") || "/api/content/suggestions", {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            "X-Requested-With": "fetch"
          },
          body: JSON.stringify({ message: `${prefix}${message}` })
        });

        const payload = await response.json().catch(() => ({}));
        if (!response.ok) {
          throw new Error(payload.message || "No se pudo enviar la sugerencia.");
        }

        if (feedback) {
          feedback.hidden = false;
          feedback.className = "status-banner success-banner";
          feedback.textContent = payload.message || "Sugerencia enviada.";
        }

        if (textarea) {
          textarea.value = "";
        }

        window.setTimeout(() => close(), 1200);
      } catch (error) {
        if (feedback) {
          feedback.hidden = false;
          feedback.className = "status-banner danger-banner";
          feedback.textContent = error?.message || "No se pudo enviar la sugerencia.";
        }
      } finally {
        if (submitButton) {
          submitButton.disabled = false;
        }
      }
    });
  }

  function wirePhoneMasks(root) {
    const inputs = root.querySelectorAll('input[data-phone-mask="ar"]');
    inputs.forEach(input => {
      if (input.dataset.phoneMaskBound === "true") return;
      input.dataset.phoneMaskBound = "true";

      const form = input.closest("form");
      const country = form?.querySelector("[data-phone-country]");
      const formatPhone = () => {
        if (country?.value === "AR") {
          const digits = extractArgPhoneDigits(input.value);
          input.value = formatArgPhoneDigits(digits);
        }
      };

      const syncPhoneMode = () => {
        if (country?.value === "AR") {
          input.placeholder = "+54 9 1145666454";
          input.inputMode = "numeric";
          input.autocomplete = "tel-national";
          if (!input.value.trim()) {
            input.value = "+54 9 ";
          } else {
            formatPhone();
          }
        } else {
          input.placeholder = "+1 212 555 0101";
          input.inputMode = "tel";
          input.autocomplete = "tel";
          if (input.value.startsWith("+54 9 ")) {
            input.value = input.value.replace(/^\+54 9\s*/, "");
          }
        }
      };

      country?.addEventListener("change", syncPhoneMode);
      input.addEventListener("focus", () => {
        if (country?.value === "AR" && !input.value.trim()) {
          input.value = "+54 9 ";
        }
      });

      input.addEventListener("input", formatPhone);
      input.addEventListener("blur", formatPhone);
      syncPhoneMode();
    });
  }

  function wireHeaderGroupPreferences(root = document) {
    root.querySelectorAll("[data-header-group-preferences]").forEach(container => {
      if (container.dataset.headerGroupPreferencesBound === "true") return;
      container.dataset.headerGroupPreferencesBound = "true";

      const maxGroups = Math.max(1, Number(container.dataset.maxGroups || 5));
      const options = Array.from(container.querySelectorAll("[data-header-group-option]"));
      const status = container.querySelector("[data-header-group-status]");

      const sync = changedOption => {
        const checked = options.filter(option => option.checked);
        if (checked.length > maxGroups && changedOption) {
          changedOption.checked = false;
          if (status) {
            status.textContent = `Puedes elegir hasta ${maxGroups} tipos.`;
          }
          return;
        }

        if (status) {
          status.textContent = checked.length > 0
            ? `${checked.length} de ${maxGroups} seleccionados.`
            : "";
        }
      };

      options.forEach(option => {
        option.addEventListener("change", () => sync(option));
      });
      sync();
    });
  }

  function wireGuestLocalityPrompt(root = document) {
    const modal = root.getElementById("guestLocalityModal");
    if (!modal) return;
    let shouldForceOpenBrowseLocalityPrompt = false;

    const syncBrowseLocalityPrompt = () => {
      const browseRequiresLocality = Boolean(document.querySelector("[data-browse-locality-required='true']"));
      if (!browseRequiresLocality) return;

      modal.dataset.guestLocalityRequired = "true";
      shouldForceOpenBrowseLocalityPrompt = true;
    };

    if (modal.dataset.bound === "true") {
      syncBrowseLocalityPrompt();
      return;
    }

    const form = modal.querySelector("[data-guest-locality-form]");
    const input = modal.querySelector("[data-guest-locality-input]");
    const hiddenId = modal.querySelector("[data-locality-id]");
    const hiddenExternalId = modal.querySelector("[data-locality-external-id]");
    const suggestions = modal.querySelector("[data-locality-suggestions]");
    const suggestionsList = modal.querySelector("[data-locality-suggestions-list]");
    const status = modal.querySelector("[data-guest-locality-status]");
    const detectButton = modal.querySelector("[data-guest-locality-detect]");
    const confirmButton = modal.querySelector("[data-guest-locality-confirm]");
    const closeButtons = Array.from(modal.querySelectorAll("[data-guest-locality-close='true']"));
    if (!form || !input || !hiddenId || !hiddenExternalId || !suggestions || !suggestionsList || !status || !detectButton || !confirmButton) return;

    modal.dataset.bound = "true";
    let pendingNavigationUrl = "";
    let shouldPersistHeaderGroups = modal.dataset.guestLocalityRequired === "true";
    let searchTimer = 0;
    let requestVersion = 0;
    let lastResults = [];
    let lastQuery = "";
    const options = getLocalityCatalogOptions(root);

    const setStatus = (message, isError = false) => {
      status.textContent = message;
      status.classList.toggle("text-danger", isError);
      status.classList.toggle("is-visible", Boolean(message));
    };

    const hideSuggestions = () => {
      suggestionsList.hidden = true;
      suggestionsList.innerHTML = "";
    };

    const close = () => {
      if (modal.dataset.guestLocalityRequired === "true") return;
      modal.hidden = true;
      modal.classList.remove("is-open");
      syncPreviewOpenState();
    };

    const open = (sourceLabel, navigationUrl = "", persistHeaderGroups = false) => {
      pendingNavigationUrl = String(navigationUrl || "").trim();
      shouldPersistHeaderGroups = persistHeaderGroups || modal.dataset.guestLocalityRequired === "true";
      input.value = String(sourceLabel || modal.dataset.currentLocalityLabel || "").trim();
      const existing = options.find(option =>
        normalizeLocalityText(option.label) === normalizeLocalityText(input.value)
        || normalizeLocalityText(option.locality) === normalizeLocalityText(input.value));
      if (existing) {
        applySelection(existing);
      } else {
        input.dataset.selectedLabel = input.value;
        hiddenId.value = "";
        hiddenExternalId.value = "";
      }
      setStatus("");
      hideSuggestions();
      modal.hidden = false;
      modal.classList.add("is-open");
      document.body.classList.add("preview-open");
      window.setTimeout(() => {
        input.focus();
        input.select();
      }, 60);
    };

    const clearSelection = () => {
      hiddenId.value = "";
      hiddenExternalId.value = "";
    };

    const renderSuggestions = (results, includeExternalResults = false) => {
      lastResults = Array.isArray(results) ? results : [];
      suggestions.innerHTML = "";
      suggestionsList.innerHTML = "";

      lastResults.forEach(result => {
        const option = document.createElement("option");
        option.value = result.label || "";
        option.dataset.localId = result.localId ? String(result.localId) : "";
        option.dataset.externalId = result.externalId || "";
        option.dataset.locality = result.locality || "";
        option.dataset.province = result.province || "";
        suggestions.appendChild(option);

        const button = document.createElement("button");
        button.type = "button";
        button.className = "guest-locality-suggestion";
        button.dataset.localitySuggestion = "true";
        button.dataset.label = result.label || "";
        button.innerHTML = `
          <strong>${escapeHtml(result.locality || result.label || "")}</strong>
          <span>${escapeHtml(result.province || "")}</span>
        `;
        button.addEventListener("click", () => {
          applySelection(result);
          hideSuggestions();
          setStatus(`Localidad seleccionada: ${result.label}.`);
        });
        suggestionsList.appendChild(button);
      });

      if (!includeExternalResults && lastQuery.length >= 2) {
        const actionButton = document.createElement("button");
        actionButton.type = "button";
        actionButton.className = "guest-locality-suggestion guest-locality-suggestion-secondary";
        actionButton.dataset.localitySuggestion = "external-search";
        actionButton.innerHTML = `
          <strong>No esta en la lista</strong>
          <span>Buscar "${escapeHtml(lastQuery)}" en toda Argentina</span>
        `;
        actionButton.addEventListener("click", () => {
          fetchSuggestions(lastQuery, true);
        });
        suggestionsList.appendChild(actionButton);
      }

      suggestionsList.hidden = suggestionsList.children.length === 0;
    };

    const applySelection = result => {
      hiddenId.value = result?.localId ? String(result.localId) : "";
      hiddenExternalId.value = result?.externalId || "";
      input.value = result?.label || "";
      input.dataset.selectedLabel = result?.label || "";
      lastQuery = result?.locality || result?.label || lastQuery;
    };

    const matchResult = rawValue => {
      const normalized = normalizeLocalityText(rawValue);
      if (!normalized) return null;

      return lastResults.find(option =>
        normalizeLocalityText(option.label) === normalized
        || normalizeLocalityText(option.locality) === normalized) || null;
    };

    const ensureSelectionMatchesInput = () => {
      const selected = matchResult(input.value);
      if (selected) {
        applySelection(selected);
        return selected;
      }

      const localSelected = options.find(option =>
        normalizeLocalityText(option.label) === normalizeLocalityText(input.value)
        || normalizeLocalityText(option.locality) === normalizeLocalityText(input.value));
      if (localSelected) {
        applySelection(localSelected);
        return localSelected;
      }

      const selectedLabel = normalizeLocalityText(input.dataset.selectedLabel || "");
      const typedLabel = normalizeLocalityText(input.value);
      if (typedLabel && typedLabel === selectedLabel && (hiddenId.value || hiddenExternalId.value)) {
        return {
          localId: hiddenId.value ? Number(hiddenId.value) : null,
          externalId: hiddenExternalId.value || null,
          label: input.value
        };
      }

      clearSelection();
      return null;
    };

    const fetchSuggestions = async (query, includeExternalResults = false) => {
      const currentVersion = ++requestVersion;
      lastQuery = String(query || "").trim();

      try {
        const endpoint = includeExternalResults ? "/api/localities/search-external" : "/api/localities/search";
        const response = await fetch(`${endpoint}?q=${encodeURIComponent(query)}`, {
          headers: { "X-Requested-With": "XMLHttpRequest" }
        });

        if (!response.ok) {
          throw new Error(`HTTP ${response.status}`);
        }

        const payload = await response.json();
        if (currentVersion !== requestVersion) return;

        if (!includeExternalResults && Array.isArray(payload) && payload.length === 0) {
          fetchSuggestions(query, true);
          return;
        }

        renderSuggestions(payload, includeExternalResults);
        if (includeExternalResults) {
          setStatus(payload.length > 0
            ? "Mostrando coincidencias de toda Argentina."
            : "No encontramos esa localidad ni en la base externa.", payload.length === 0);
        }
      } catch (error) {
        if (currentVersion !== requestVersion) return;
        renderSuggestions([], includeExternalResults);
        setStatus("No pudimos buscar localidades en este momento.", true);
      }
    };

    const applyLocality = locality => {
      writeCookie(NAVIGATION_LOCALITY_COOKIE, String(locality.localId), 365);
      if (shouldPersistHeaderGroups) {
        const selectedHeaderGroups = Array.from(modal.querySelectorAll("[data-guest-header-group-option]:checked"))
          .map(option => String(option.value || "").trim())
          .filter(Boolean)
          .slice(0, 5);
        if (selectedHeaderGroups.length > 0
          && selectedHeaderGroups.length < 4) {
          const availableHeaderGroups = Array.from(modal.querySelectorAll("[data-guest-header-group-option]"))
            .map(option => String(option.value || "").trim())
            .filter(Boolean);
          ["Generales", "Inmuebles"].forEach(group => {
            if (selectedHeaderGroups.length >= 5) return;
            if (!availableHeaderGroups.some(option => option.toLowerCase() === group.toLowerCase())) return;
            if (selectedHeaderGroups.some(selected => selected.toLowerCase() === group.toLowerCase())) return;
            selectedHeaderGroups.push(group);
          });
        }
        if (selectedHeaderGroups.length > 0) {
          writeCookie(HEADER_PUBLICATION_GROUPS_COOKIE, selectedHeaderGroups.join(","), 365);
        } else {
          eraseCookie(HEADER_PUBLICATION_GROUPS_COOKIE);
        }
      }
      setStatus(`Mostrando anuncios cerca de ${locality.label}...`);
      if (pendingNavigationUrl) {
        window.location.href = pendingNavigationUrl;
        return;
      }

      window.location.reload();
    };

    const detectNearestLocality = () => {
      if (!navigator.geolocation) {
        setStatus("Tu navegador no permite detectar ubicacion automaticamente.", true);
        return;
      }

      const originalLabel = detectButton.textContent;
      detectButton.disabled = true;
      detectButton.textContent = "Detectando...";
      setStatus("Esperando permiso para acceder a tu ubicacion.");

      navigator.geolocation.getCurrentPosition(position => {
        const nearest = findNearestLocalityFromCollection(options, position.coords.latitude, position.coords.longitude);
        detectButton.disabled = false;
        detectButton.textContent = originalLabel;

        if (!nearest) {
          setStatus("No encontramos una localidad cercana en la base local.", true);
          return;
        }

        applySelection(nearest);
        applyLocality(nearest);
      }, error => {
        detectButton.disabled = false;
        detectButton.textContent = originalLabel;
        setStatus(mapRegisterGeolocationError(error), true);
      }, {
        enableHighAccuracy: true,
        timeout: 10000,
        maximumAge: 300000
      });
    };

    const confirmLocality = async () => {
      const selected = ensureSelectionMatchesInput();
      if (!selected) {
        setStatus("Escribe una localidad valida de la lista para mostrar resultados cercanos.", true);
        return;
      }

      confirmButton.disabled = true;
      setStatus("Guardando localidad seleccionada...");

      try {
        const response = await fetch("/api/localities/resolve", {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            "X-Requested-With": "XMLHttpRequest"
          },
          body: JSON.stringify({
            localId: selected.localId ?? null,
            externalId: selected.externalId || null
          })
        });

        const payload = await response.json();
        if (!response.ok) {
          throw new Error(payload?.error || "No se pudo validar la localidad.");
        }

        applySelection(payload);
        applyLocality(payload);
      } catch (error) {
        setStatus(error?.message || "No se pudo validar la localidad elegida.", true);
      } finally {
        confirmButton.disabled = false;
      }
    };

    input.addEventListener("input", () => {
      if (normalizeLocalityText(input.value) !== normalizeLocalityText(input.dataset.selectedLabel || "")) {
        clearSelection();
      }

      window.clearTimeout(searchTimer);

      const query = input.value.trim();
      lastQuery = query;
      if (query.length < 2) {
        renderSuggestions([]);
        if (!query) {
          setStatus("");
        }
        return;
      }

      searchTimer = window.setTimeout(() => {
        fetchSuggestions(query);
      }, 250);
    });

    input.addEventListener("change", () => {
      const selected = ensureSelectionMatchesInput();
      if (!selected) {
        setStatus("Elige una localidad de la lista para continuar.", true);
      }
    });

    input.addEventListener("blur", () => {
      window.setTimeout(() => {
        hideSuggestions();
      }, 180);
    });

    input.addEventListener("focus", () => {
      if (lastResults.length > 0) {
        suggestionsList.hidden = false;
      }
    });

    detectButton.addEventListener("click", detectNearestLocality);
    confirmButton.addEventListener("click", confirmLocality);
    closeButtons.forEach(button => {
      button.addEventListener("click", event => {
        event.preventDefault();
        close();
      });
    });

    document.addEventListener("click", event => {
      const trigger = event.target.closest("[data-open-locality-picker='true']");
      if (!trigger) return;
      event.preventDefault();
      open(trigger.getAttribute("data-locality-label"), "", false);
    });

    document.addEventListener("click", event => {
      if (modal.dataset.guestLocalityRequired !== "true") return;

      const link = event.target.closest("a[href]");
      if (!link) return;

      const href = link.getAttribute("href") || "";
      if (!href.startsWith("/Buscar")) return;

      event.preventDefault();
      open("", link.href, true);
    });

    document.addEventListener("submit", event => {
      if (modal.dataset.guestLocalityRequired !== "true") return;

      const formElement = event.target;
      if (!(formElement instanceof HTMLFormElement)) return;
      if (!formElement.matches("[data-browse-search-form]")) return;

      event.preventDefault();
      const targetUrl = new URL(formElement.getAttribute("action") || window.location.pathname, window.location.origin);
      const params = new URLSearchParams(new FormData(formElement));
      targetUrl.search = params.toString();
      open("", targetUrl.toString(), true);
    });

    input.addEventListener("keydown", event => {
      if (event.key !== "Enter") return;
      event.preventDefault();
      confirmLocality();
    });

    input.addEventListener("input", () => {
      status.classList.remove("text-danger");
      status.classList.remove("is-visible");
      status.textContent = "";
    });

    if (modal.dataset.guestLocalityRequired !== "true") {
      close();
    }

    syncBrowseLocalityPrompt();
    if (shouldForceOpenBrowseLocalityPrompt && !modal.classList.contains("is-open")) {
      open("", window.location.href, true);
    }
  }

  function wireRegisterLocalityDetection(root = document) {
    wireHybridLocalityPicker(root);
  }

  function wireAccountLocalityPicker(root = document) {
    wireHybridLocalityPicker(root);
  }

  function wireHybridLocalityPicker(root = document) {
    const localCatalog = getLocalityCatalogOptions(root);

    root.querySelectorAll("[data-hybrid-locality-root]").forEach(container => {
      if (container.dataset.bound === "true") return;

      const input = container.querySelector("[data-locality-input]");
      const hiddenId = container.querySelector("[data-locality-id]");
      const hiddenExternalId = container.querySelector("[data-locality-external-id]");
      const datalist = container.querySelector("[data-locality-suggestions]");
      const suggestionsList = container.querySelector("[data-locality-suggestions-list]");
      const detectButton = container.querySelector("[data-detect-locality]");
      const hostForm = container.closest("form");
      const status = container.parentElement?.querySelector("[data-register-locality-status], [data-account-locality-status]");
      if (!input || !hiddenId || !hiddenExternalId || !datalist || !detectButton || !hostForm || !status) return;

      container.dataset.bound = "true";

      let searchTimer = 0;
      let requestVersion = 0;
      let lastResults = [];
      let lastQuery = "";
      const externalSearchButton = document.createElement("button");
      externalSearchButton.type = "button";
      externalSearchButton.className = "ghost-pill compact locality-external-search";
      externalSearchButton.textContent = "No esta en la lista";
      externalSearchButton.hidden = true;
      container.insertAdjacentElement("afterend", externalSearchButton);

      const setStatus = (message, isError = false) => {
        status.textContent = message;
        status.classList.toggle("text-danger", isError);
        status.classList.toggle("is-visible", Boolean(message));
      };

      const clearSelection = () => {
        hiddenId.value = "";
        hiddenExternalId.value = "";
      };

      const toggleExternalSearchButton = (visible, query = "") => {
        externalSearchButton.hidden = !visible;
        externalSearchButton.dataset.query = String(query || "").trim();
        externalSearchButton.title = query ? `Buscar "${query}" en toda Argentina` : "";
      };

      const renderSuggestions = (results, includeExternalResults = false) => {
        lastResults = Array.isArray(results) ? results : [];
        datalist.innerHTML = "";

        if (suggestionsList) {
          suggestionsList.innerHTML = "";
        }

        lastResults.forEach(result => {
          const option = document.createElement("option");
          option.value = result.label || "";
          option.dataset.localId = result.localId ? String(result.localId) : "";
          option.dataset.externalId = result.externalId || "";
          option.dataset.locality = result.locality || "";
          option.dataset.province = result.province || "";
          option.dataset.latitude = Number.isFinite(Number(result.latitude)) ? String(result.latitude) : "";
          option.dataset.longitude = Number.isFinite(Number(result.longitude)) ? String(result.longitude) : "";
          datalist.appendChild(option);

          if (suggestionsList) {
            const button = document.createElement("button");
            button.type = "button";
            button.className = "guest-locality-suggestion";
            button.dataset.localitySuggestion = "true";
            button.innerHTML = `
              <strong>${escapeHtml(result.locality || result.label || "")}</strong>
              <span>${escapeHtml(result.province || "")}</span>
            `;
            button.addEventListener("click", () => applySelection(result));
            suggestionsList.appendChild(button);
          }
        });

        if (suggestionsList) {
          suggestionsList.hidden = suggestionsList.children.length === 0;
        }

        toggleExternalSearchButton(!includeExternalResults && lastQuery.length >= 2, lastQuery);
      };

      const applySelection = (result, message = null) => {
        hiddenId.value = result?.localId ? String(result.localId) : "";
        hiddenExternalId.value = result?.externalId || "";
        input.value = result?.label || "";
        input.dataset.selectedLabel = result?.label || "";
        if (suggestionsList) {
          suggestionsList.hidden = true;
        }
        toggleExternalSearchButton(false);
        setStatus(message ?? (result ? `Localidad seleccionada: ${result.label}.` : ""), false);
      };

      const matchResult = rawValue => {
        const normalized = normalizeLocalityText(rawValue);
        if (!normalized) return null;

        return lastResults.find(option =>
          normalizeLocalityText(option.label) === normalized
          || normalizeLocalityText(option.locality) === normalized) || null;
      };

      const ensureSelectionMatchesInput = () => {
        const selected = matchResult(input.value);
        if (selected) {
          applySelection(selected);
          return selected;
        }

        const selectedLabel = normalizeLocalityText(input.dataset.selectedLabel || "");
        const typedLabel = normalizeLocalityText(input.value);
        if (typedLabel && typedLabel === selectedLabel && (hiddenId.value || hiddenExternalId.value)) {
          return {
            localId: hiddenId.value ? Number(hiddenId.value) : null,
            externalId: hiddenExternalId.value || null,
            label: input.value
          };
        }

        clearSelection();
        return null;
      };

      const fetchSuggestions = async (query, includeExternalResults = false) => {
        const currentVersion = ++requestVersion;
        lastQuery = String(query || "").trim();

        try {
          const endpoint = includeExternalResults ? "/api/localities/search-external" : "/api/localities/search";
          const response = await fetch(`${endpoint}?q=${encodeURIComponent(query)}`, {
            headers: { "X-Requested-With": "XMLHttpRequest" }
          });

          if (!response.ok) {
            throw new Error(`HTTP ${response.status}`);
          }

          const payload = await response.json();
          if (currentVersion !== requestVersion) return;
          if (!includeExternalResults && Array.isArray(payload) && payload.length === 0) {
            fetchSuggestions(query, true);
            return;
          }

          renderSuggestions(payload, includeExternalResults);
          if (includeExternalResults) {
            setStatus(payload.length > 0
              ? "Mostrando coincidencias de toda Argentina. Elige una localidad de la lista."
              : "No encontramos esa localidad ni en la base externa.", payload.length === 0);
          } else {
            setStatus("");
          }
        } catch (error) {
          if (currentVersion !== requestVersion) return;
          renderSuggestions([], includeExternalResults);
          setStatus("No pudimos buscar localidades en este momento.", true);
        }
      };

      input.addEventListener("input", () => {
        if (normalizeLocalityText(input.value) !== normalizeLocalityText(input.dataset.selectedLabel || "")) {
          clearSelection();
        }

        window.clearTimeout(searchTimer);

        const query = input.value.trim();
        lastQuery = query;
        if (query.length < 2) {
          renderSuggestions([]);
          toggleExternalSearchButton(false);
          if (!query) {
            setStatus("");
          }
          return;
        }

        searchTimer = window.setTimeout(() => {
          fetchSuggestions(query);
        }, 250);
      });

      externalSearchButton.addEventListener("click", () => {
        const query = String(externalSearchButton.dataset.query || input.value || "").trim();
        if (query.length < 2) {
          setStatus("Escribe al menos dos letras para buscar.", true);
          input.focus();
          return;
        }

        fetchSuggestions(query, true);
      });

      input.addEventListener("change", () => {
        const selected = ensureSelectionMatchesInput();
        if (!selected) {
          setStatus("Elige una localidad de la lista para continuar.", true);
        }
      });

      input.addEventListener("blur", () => {
        window.setTimeout(() => {
          ensureSelectionMatchesInput();
        }, 0);
      });

      detectButton.addEventListener("click", () => {
        if (!navigator.geolocation) {
          setStatus("Tu navegador no permite detectar ubicacion automaticamente.", true);
          return;
        }

        const originalLabel = detectButton.textContent;
        detectButton.disabled = true;
        detectButton.textContent = "Detectando...";
        setStatus("Esperando permiso para acceder a tu ubicacion.");

        navigator.geolocation.getCurrentPosition(position => {
          const nearest = findNearestLocalityFromCollection(localCatalog, position.coords.latitude, position.coords.longitude);
          detectButton.disabled = false;
          detectButton.textContent = originalLabel;

          if (!nearest) {
            setStatus("No encontramos una localidad cercana en la base local.", true);
            return;
          }

          applySelection(nearest, `Ubicacion detectada. Seleccionamos ${nearest.label}.`);
        }, error => {
          detectButton.disabled = false;
          detectButton.textContent = originalLabel;
          setStatus(mapRegisterGeolocationError(error), true);
        }, {
          enableHighAccuracy: true,
          timeout: 10000,
          maximumAge: 300000
        });
      });

      hostForm.addEventListener("submit", event => {
        const selected = ensureSelectionMatchesInput();
        if (selected) {
          setStatus("");
          return;
        }

        event.preventDefault();
        setStatus("Selecciona una localidad valida antes de continuar.", true);
        input.focus();
      });

      if (input.value.trim()) {
        input.dataset.selectedLabel = input.value.trim();
      }
    });
  }

  function getLocalityCatalogOptions(root = document) {
    const datalist = root.getElementById("header-locality-options");
    if (!datalist) return [];

    return Array.from(datalist.options).map(option => ({
      localId: Number(option.dataset.localityId || 0),
      label: option.value || "",
      locality: option.dataset.locality || "",
      province: option.dataset.province || "",
      latitude: Number(option.dataset.latitude),
      longitude: Number(option.dataset.longitude)
    })).filter(option =>
      option.localId > 0
      && Number.isFinite(option.latitude)
      && Number.isFinite(option.longitude));
  }

  function wireRegisterAccountType(root = document) {
    const form = root.querySelector("[data-account-type-form='true']");
    if (!form || form.dataset.accountTypeBound === "true") return;

    const options = Array.from(form.querySelectorAll("[data-account-type-option]"));
    const personSection = form.querySelector("[data-account-type-section='person']");
    const companySection = form.querySelector("[data-account-type-section='company']");
    const companyNameInput = form.querySelector('input[name="Input.CompanyName"]');
    const companyTaglineInput = form.querySelector('input[name="Input.CompanyTagline"]');
    const companyPublicUrlInput = form.querySelector("[data-company-public-url]");
    const personNameInput = form.querySelector('input[name="Input.Name"]');
    const emailCheckbox = form.querySelector('input[name="Input.RespondsEmails"]');
    const whatsappCheckbox = form.querySelector('input[name="Input.RespondsWhatsApp"]');
    const phoneCheckbox = form.querySelector('input[name="Input.AcceptsCalls"]');
    form.dataset.accountTypeBound = "true";

    const syncCompanyPublicUrl = () => {
      if (!companyPublicUrlInput) return;

      const slug = String(companyNameInput?.value || "")
        .trim()
        .toLowerCase()
        .normalize("NFD")
        .replace(/[\u0300-\u036f]/g, "")
        .replace(/[^\p{L}\p{N}]+/gu, "-")
        .replace(/^-+|-+$/g, "");

      companyPublicUrlInput.value = slug ? `/${slug}` : "/tuempresa";
    };

    const sync = () => {
      const selected = options.find(option => option.checked)?.value || "Person";
      const isCompany = selected === "Company";

      if (personSection) {
        personSection.hidden = isCompany;
      }

      if (companySection) {
        companySection.hidden = !isCompany;
      }

      if (companyNameInput) {
        companyNameInput.required = isCompany;
      }

      if (companyTaglineInput) {
        companyTaglineInput.required = false;
      }

      if (personNameInput) {
        personNameInput.required = !isCompany;
      }

      if (isCompany) {
        if (emailCheckbox) {
          emailCheckbox.checked = true;
        }
        if (whatsappCheckbox) {
          whatsappCheckbox.checked = true;
        }
        if (phoneCheckbox) {
          phoneCheckbox.checked = true;
        }
      }
    };

    options.forEach(option => option.addEventListener("change", sync));
    companyNameInput?.addEventListener("input", syncCompanyPublicUrl);
    sync();
    syncCompanyPublicUrl();
  }

  function findNearestRegisterLocality(select, latitude, longitude) {
    const localities = Array.from(select.options)
      .map(option => ({
        value: option.value,
        locality: option.textContent?.trim() || "",
        province: option.dataset.province || "",
        latitude: Number(option.dataset.latitude),
        longitude: Number(option.dataset.longitude)
      }))
      .filter(option => option.value && Number.isFinite(option.latitude) && Number.isFinite(option.longitude));

    if (!localities.length) return null;
    return findNearestLocalityFromCollection(localities, latitude, longitude);
  }

  function findNearestLocalityFromCollection(localities, latitude, longitude) {
    let nearest = null;
    for (const locality of localities) {
      const distance = haversineDistanceKm(latitude, longitude, locality.latitude, locality.longitude);
      if (!nearest || distance < nearest.distance) {
        nearest = { ...locality, distance };
      }
    }

    return nearest;
  }

  function matchHeaderLocality(localities, rawValue) {
    const normalized = normalizeLocalityText(rawValue);
    if (!normalized) return null;

    return localities.find(option =>
      normalizeLocalityText(option.label) === normalized
      || normalizeLocalityText(option.locality) === normalized) || null;
  }

  function haversineDistanceKm(lat1, lng1, lat2, lng2) {
    const toRadians = degrees => degrees * (Math.PI / 180);
    const earthRadiusKm = 6371;
    const deltaLat = toRadians(lat2 - lat1);
    const deltaLng = toRadians(lng2 - lng1);
    const a = Math.sin(deltaLat / 2) ** 2
      + Math.cos(toRadians(lat1)) * Math.cos(toRadians(lat2)) * Math.sin(deltaLng / 2) ** 2;
    const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
    return earthRadiusKm * c;
  }

  function mapRegisterGeolocationError(error) {
    switch (error?.code) {
      case error.PERMISSION_DENIED:
        return "No diste permiso para detectar tu ubicacion.";
      case error.POSITION_UNAVAILABLE:
        return "No pudimos obtener tu ubicacion actual.";
      case error.TIMEOUT:
        return "La deteccion de ubicacion tardo demasiado. Intenta otra vez.";
      default:
        return "No se pudo detectar tu ubicacion.";
    }
  }

  function normalizeLocalityText(value) {
    return String(value || "")
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .trim()
      .toLowerCase();
  }

  function writeCookie(name, value, maxAgeDays) {
    const maxAgeSeconds = Math.max(1, Math.floor(maxAgeDays * 24 * 60 * 60));
    document.cookie = `${name}=${encodeURIComponent(value)}; path=/; max-age=${maxAgeSeconds}; SameSite=Lax`;
  }

  function readCookie(name) {
    const prefix = `${name}=`;
    return document.cookie
      .split(";")
      .map(item => item.trim())
      .find(item => item.startsWith(prefix))
      ?.slice(prefix.length) || "";
  }

  function eraseCookie(name) {
    document.cookie = `${name}=; path=/; max-age=0; SameSite=Lax`;
  }

  function extractArgPhoneDigits(value) {
    let digits = String(value || "").replace(/\D/g, "");
    if (digits.startsWith("549")) {
      digits = digits.slice(3);
    } else if (digits.startsWith("54")) {
      digits = digits.slice(2);
    }

    if (digits.startsWith("9")) {
      digits = digits.slice(1);
    }

    return digits.slice(0, 10);
  }

  function formatArgPhoneDigits(digits) {
    const value = String(digits || "").slice(0, 10);
    if (!value) {
      return "";
    }

    return `+54 9 ${value}`.trim();
  }

  function wireReportForm() {
    const form = document.getElementById("reportForm");
    if (!form || form.dataset.bound === "true") return;

    form.dataset.bound = "true";
    form.addEventListener("submit", async event => {
      event.preventDefault();

      const payload = {
        publicationId: Number(form.querySelector('input[name="publicationId"]').value),
        reasonId: Number(form.querySelector('input[name="reason"]:checked')?.value || 0),
        comment: String(form.querySelector('textarea[name="comment"]')?.value || "").trim()
      };

      const response = await fetch("/api/content/report", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "X-Requested-With": "fetch"
        },
        body: JSON.stringify(payload)
      });

      const result = await response.json().catch(() => ({}));
      if (!response.ok) {
        if (response.status === 401) {
          showAuthRequiredModal({
            title: "Debes iniciar sesión para denunciar",
            message: result?.message || "Para denunciar una publicación debes ingresar con tu usuario.",
            showRegister: true,
            showLogin: true
          });
          return;
        }

        ventagramFlashMessage = result?.message || "No se pudo enviar la denuncia.";
        await loadApiPage();
        return;
      }

      ventagramFlashMessage = result.message || "La denuncia fue enviada.";

      const modal = document.getElementById("reportModal");
      if (modal) {
        modal.hidden = true;
        modal.classList.remove("is-open");
      }
      syncPreviewOpenState();
      const commentField = form.querySelector('textarea[name="comment"]');
      if (commentField) commentField.value = "";
      await loadApiPage();
    });
  }

  function wireFavoriteActions(root = document) {
    root.querySelectorAll("[data-favorite-toggle='true']").forEach(button => {
      if (button.dataset.bound === "true") return;
      button.dataset.bound = "true";
      button.addEventListener("click", async event => {
        event.preventDefault();
        event.stopPropagation();
        await openFavoriteModal(button);
      });
    });

    root.querySelectorAll("[data-favorite-list-open='true']").forEach(button => {
      if (button.dataset.bound === "true") return;
      button.dataset.bound = "true";
      button.addEventListener("click", async event => {
        event.preventDefault();
        await openFavoriteListModal(button.getAttribute("data-list-id"), button.getAttribute("data-list-name"));
      });
    });

    root.querySelectorAll("[data-like-toggle='true']").forEach(button => {
      if (button.dataset.bound === "true") return;
      button.dataset.bound = "true";
      button.addEventListener("click", event => {
        event.preventDefault();
        event.stopPropagation();
        const publicationId = button.getAttribute("data-publication-id");
        togglePublicationLike(publicationId);
      });
    });
  }

  function initFavoritesPage() {
    const container = document.querySelector("[data-favorites-page-results]");
    if (!container || container.dataset.bound === "true") return;

    container.dataset.bound = "true";
    const buttons = Array.from(document.querySelectorAll("[data-favorite-list-open='true'][data-list-id]"));
    if (!buttons.length) return;

    const lastListId = readLastFavoriteList();
    const initialButton = buttons.find(button => String(button.getAttribute("data-list-id")) === String(lastListId))
      || buttons[0];
    initialButton?.click();
  }

  function wireGalleryActionMenus(root = document) {
    root.querySelectorAll("[data-gallery-menu-toggle='true']").forEach(button => {
      if (button.dataset.bound === "true") return;
      button.dataset.bound = "true";
      button.addEventListener("click", event => {
        event.preventDefault();
        event.stopPropagation();

        const menu = button.closest(".card-image-wrap")?.querySelector("[data-gallery-menu]");
        if (!menu) return;
        const willOpen = menu.hidden;

        closeAllGalleryActionMenus();
        menu.hidden = !willOpen;
        button.setAttribute("aria-expanded", willOpen ? "true" : "false");
      });
    });

    root.querySelectorAll("[data-gallery-menu] button").forEach(button => {
      if (button.dataset.menuBound === "true") return;
      button.dataset.menuBound = "true";
      button.addEventListener("click", () => {
        closeAllGalleryActionMenus();
      });
    });
  }

  function closeAllGalleryActionMenus() {
    document.querySelectorAll("[data-gallery-menu]").forEach(menu => {
      menu.hidden = true;
    });
    document.querySelectorAll("[data-gallery-menu-toggle='true']").forEach(button => {
      button.setAttribute("aria-expanded", "false");
    });
  }

  function wireFavoriteModal() {
    const modal = document.getElementById("favoriteModal");
    const form = document.getElementById("favoriteForm");
    if (!modal || !form) return;
    if (modal.dataset.bound === "true") return;
    modal.dataset.bound = "true";

    const close = () => {
      modal.hidden = true;
      modal.classList.remove("is-open");
      syncPreviewOpenState();
    };
    const syncNewListFieldVisibility = () => {
      const select = form.querySelector("[data-favorite-list-select]");
      const newListField = form.querySelector("[data-favorite-new-list-field]");
      const newListInput = form.querySelector('input[name="newListName"]');
      if (!select || !newListField || !newListInput) return;

      const creatingNew = !select.value;
      newListField.hidden = !creatingNew;
      newListInput.disabled = !creatingNew;
    };

    modal.addEventListener("click", event => {
      const closeTrigger = event.target.closest("[data-favorite-close='true']");
      if (!closeTrigger) return;
      event.preventDefault();
      close();
    });

    form.querySelector("[data-favorite-list-select]")?.addEventListener("change", syncNewListFieldVisibility);

    form.addEventListener("submit", async event => {
      event.preventDefault();
      const payload = {
        publicationId: Number(form.querySelector('input[name="publicationId"]')?.value || 0),
        listId: numberOrNull(form.querySelector('[name="listId"]')?.value || ""),
        newListName: String(form.querySelector('input[name="newListName"]')?.value || "").trim() || null,
        suggestedListName: String(form.querySelector('input[name="suggestedListName"]')?.value || "").trim() || null
      };

      const response = await fetch("/api/content/favorites", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "X-Requested-With": "fetch"
        },
        body: JSON.stringify(payload)
      });

      const result = await response.json().catch(() => ({}));
      if (!response.ok) {
        if (response.status === 401) {
          close();
          showAuthRequiredModal({
            title: "Debes iniciar sesión",
            message: "Puedes crear listas de anuncios favoritos para hacer seguimiento solo con una cuenta registrada.",
            showRegister: true,
            showLogin: true,
            pendingAction: {
              type: "favorite-toggle",
              publicationId: payload.publicationId,
              suggestedListName: payload.suggestedListName || null
            }
          });
          return;
        }
        ventagramFlashMessage = result?.message || "No se pudo guardar en favoritos.";
        close();
        await loadApiPage();
        return;
      }

      if (result?.listId) {
        rememberLastFavoriteList(result.listId);
      }
      markPublicationFavorite(payload.publicationId);
      await refreshFavoriteSummaries();
      ventagramFlashMessage = result?.message || "Guardado en favoritos.";
      close();
      await loadApiPage();
    });

    syncNewListFieldVisibility();
  }

  async function openFavoriteModalByPayload(payload = {}) {
    const modal = document.getElementById("favoriteModal");
    const form = document.getElementById("favoriteForm");
    if (!modal || !form) return;

    const publicationId = payload.publicationId || "0";
    const publicationTitle = stripOpportunitySuffix(payload.publicationTitle || "Publicacion");
    const suggestedListName = payload.suggestedListName || "Inmuebles";
    const select = form.querySelector("[data-favorite-list-select]");
    const newListInput = form.querySelector('input[name="newListName"]');
    form.querySelector('input[name="publicationId"]').value = publicationId;
    form.querySelector('input[name="suggestedListName"]').value = suggestedListName;
    if (newListInput) {
      newListInput.value = suggestedListName;
      newListInput.placeholder = suggestedListName;
    }

    const response = await fetch("/api/content/favorite-lists", {
      headers: { "X-Requested-With": "fetch" }
    });
    const result = await response.json().catch(() => ({}));
    if (!response.ok) {
      if (response.status === 401) {
        showAuthRequiredModal({
          title: "Debes iniciar sesión",
          message: "Puedes crear listas de anuncios favoritos para hacer seguimiento solo con una cuenta registrada.",
          showRegister: true,
          showLogin: true,
          pendingAction: {
            type: "favorite-toggle",
            publicationId,
            publicationTitle,
            suggestedListName
          }
        });
        return;
      }
      ventagramFlashMessage = result?.message || "Tenes que iniciar sesion para usar favoritos.";
      await loadApiPage();
      return;
    }

    const lists = Array.isArray(result?.lists) ? result.lists : [];
    if (select) {
      select.innerHTML = `<option value="">Crear una nueva</option>${lists.map(list => `<option value="${escapeAttribute(list.id)}">${escapeHtml(list.name)} (${escapeHtml(String(list.itemCount || 0))})</option>`).join("")}`;
      const defaultListId = resolveDefaultFavoriteListId(lists, suggestedListName);
      select.value = defaultListId ? String(defaultListId) : "";
    }
    const newListField = form.querySelector("[data-favorite-new-list-field]");
    if (newListField && newListInput) {
      const creatingNew = !select?.value;
      newListField.hidden = !creatingNew;
      newListInput.disabled = !creatingNew;
    }

    const titleNode = document.getElementById("favoriteModalTitle");
    if (titleNode) {
      titleNode.textContent = publicationTitle ? `Guardar: ${publicationTitle}` : "Guardar en favoritos";
    }

    modal.hidden = false;
    modal.classList.add("is-open");
    document.body.classList.add("preview-open");
  }

  async function openFavoriteModal(trigger) {
    if (!trigger) return;
    await openFavoriteModalByPayload({
      publicationId: trigger.getAttribute("data-publication-id") || "0",
      publicationTitle: trigger.getAttribute("data-publication-title") || "Publicacion",
      suggestedListName: trigger.getAttribute("data-suggested-list-name") || "Inmuebles"
    });
  }

  function wireFavoriteListModal() {
    const modal = document.getElementById("favoriteListModal");
    if (!modal) return;
    if (modal.dataset.bound === "true") return;
    modal.dataset.bound = "true";

    const close = () => {
      modal.hidden = true;
      modal.classList.remove("is-open");
      syncPreviewOpenState();
    };

    modal.addEventListener("click", event => {
      const closeTrigger = event.target.closest("[data-favorite-list-close='true']");
      if (!closeTrigger) return;
      event.preventDefault();
      close();
    });
  }

  async function openFavoriteListModal(listId, fallbackName = "Mi lista") {
    if (!listId) return;
    rememberLastFavoriteList(listId);

    const inlineContainer = document.querySelector("[data-favorites-page-results]");
    if (inlineContainer) {
      await renderFavoriteListInline(inlineContainer, listId, fallbackName);
      return;
    }

    const modal = document.getElementById("favoriteListModal");
    const body = document.getElementById("favoriteListModalBody");
    const title = document.getElementById("favoriteListModalTitle");
    if (!modal || !body || !title) return;

    title.textContent = `Favoritos: ${fallbackName}`;
    body.innerHTML = `<div class="preview-modal-loading">Cargando favoritos...</div>`;
    modal.hidden = false;
    modal.classList.add("is-open");
    document.body.classList.add("preview-open");

    const response = await fetch(`/api/content/favorite-lists/${encodeURIComponent(listId)}`, {
      headers: { "X-Requested-With": "fetch" }
    });
    const result = await response.json().catch(() => ({}));
    if (!response.ok) {
      title.textContent = fallbackName;
      body.innerHTML = `<section class="empty-state"><h2>No se pudo abrir la lista</h2><p>${escapeHtml(result?.message || "Intenta nuevamente.")}</p></section>`;
      return;
    }

    const items = Array.isArray(result?.items) ? result.items : [];
    title.textContent = `Favoritos: ${result?.list?.name || fallbackName}`;
    body.innerHTML = items.length
      ? `<section class="favorites-modal-grid">${items.map(item => buildGalleryCard(item, false)).join("")}</section>`
      : `<section class="empty-state"><h2>La lista esta vacia</h2><p>Guarda publicaciones con la estrella para verlas aca.</p></section>`;
    wireGalleryCards();
    wireFavoriteActions(body);
  }

  async function renderFavoriteListInline(container, listId, fallbackName) {
    const title = container.querySelector("[data-favorites-page-title]");
    const loading = container.querySelector("[data-favorites-page-loading]");
    const empty = container.querySelector("[data-favorites-page-empty]");
    const gallery = container.querySelector("[data-favorites-page-gallery]");
    if (!title || !loading || !empty || !gallery) return;

    syncFavoriteListSelection(listId);
    container.hidden = false;
    title.textContent = `Favoritos: ${fallbackName}`;
    loading.hidden = false;
    empty.hidden = true;
    gallery.innerHTML = "";

    const response = await fetch(`/api/content/favorite-lists/${encodeURIComponent(listId)}`, {
      headers: { "X-Requested-With": "fetch" }
    });
    const result = await response.json().catch(() => ({}));
    loading.hidden = true;

    if (!response.ok) {
      title.textContent = fallbackName;
      empty.innerHTML = `<h2>No se pudo abrir la lista</h2><p>${escapeHtml(result?.message || "Intenta nuevamente.")}</p>`;
      empty.hidden = false;
      return;
    }

    const items = Array.isArray(result?.items) ? result.items : [];
    const listName = result?.list?.name || fallbackName;
    title.textContent = `Favoritos: ${listName}`;
    updateFavoritesPageManagement(container, listId, listName);

    if (!items.length) {
      empty.innerHTML = `<h2>La lista esta vacia</h2><p>Guarda publicaciones con la estrella para verlas aca.</p>`;
      empty.hidden = false;
      return;
    }

    gallery.innerHTML = items.map(item => buildGalleryCard(item, false)).join("");
    wireGalleryCards();
    wireFavoriteActions(gallery);
    container.scrollIntoView({ behavior: "smooth", block: "start" });
  }

  function updateFavoritesPageManagement(container, listId, listName) {
    const panel = container.querySelector("[data-favorites-page-management]");
    if (!panel) return;

    const title = panel.querySelector("[data-favorites-management-name]");
    const renameListId = panel.querySelector("[data-favorites-management-list-id]");
    const renameInput = panel.querySelector("[data-favorites-management-name-input]");
    const deleteListId = panel.querySelector("[data-favorites-management-delete-list-id]");
    const renameForm = panel.querySelector("[data-favorites-management-rename-form]");
    const renameOpen = panel.querySelector("[data-favorites-management-rename-open]");

    panel.hidden = false;
    if (title) title.textContent = listName;
    if (renameListId) renameListId.value = listId;
    if (renameInput) renameInput.value = listName;
    if (deleteListId) deleteListId.value = listId;
    if (renameForm) renameForm.hidden = true;
    if (renameOpen) renameOpen.hidden = false;
  }

  function syncFavoriteListSelection(activeListId) {
    document.querySelectorAll("[data-favorite-list-open='true'][data-list-id]").forEach(button => {
      const isActive = String(button.getAttribute("data-list-id")) === String(activeListId);
      button.classList.toggle("active", isActive);
      button.setAttribute("aria-pressed", isActive ? "true" : "false");
    });
  }

  async function refreshFavoriteSummaries() {
    const summary = document.querySelector("[data-favorites-summary]");
    const actions = document.querySelector("[data-favorites-summary-actions]");
    if (!summary || !actions) return;

    const response = await fetch("/api/content/favorite-lists", {
      headers: { "X-Requested-With": "fetch" }
    });
    const result = await response.json().catch(() => ({}));
    if (!response.ok) return;

    const lists = Array.isArray(result?.lists) ? result.lists : [];
    actions.innerHTML = lists.length
      ? lists.map(list => `<button type="button" class="view-pill" data-favorite-list-open="true" data-list-id="${escapeAttribute(list.id)}" data-list-name="${escapeAttribute(list.name)}">${escapeHtml(list.name)} (${escapeHtml(String(list.itemCount || 0))})</button>`).join("")
      : `<span class="field-hint">Todavia no guardaste publicaciones en tus listas.</span>`;
    wireFavoriteActions(summary);
  }

  function markPublicationFavorite(publicationId) {
    document.querySelectorAll(`[data-favorite-toggle='true'][data-publication-id='${publicationId}']`).forEach(button => {
      button.classList.add("is-active");
      button.innerHTML = renderFavoriteIcon(true);
    });
  }

  function togglePublicationLike(publicationId) {
    if (!publicationId) return;
    const liked = readLikedPublicationIds();
    const key = String(publicationId);
    if (liked.has(key)) {
      liked.delete(key);
    } else {
      liked.add(key);
    }

    writeLikedPublicationIds(liked);
    syncLikeButtonsForPublication(key);
  }

  function syncLikeButtonsForPublication(publicationId) {
    const liked = readLikedPublicationIds().has(String(publicationId));
    document.querySelectorAll(`[data-like-toggle='true'][data-publication-id='${publicationId}']`).forEach(button => {
      button.classList.toggle("is-active", liked);
      button.innerHTML = renderLikeIcon(liked);
      button.setAttribute("aria-label", liked ? "Quitar me gusta" : "Marcar como me gusta");
      button.setAttribute("title", liked ? "Quitar me gusta" : "Marcar como me gusta");
    });
  }

  function renderFavoriteIcon(isActive) {
    return `<i class="fa-${isActive ? "solid" : "regular"} fa-star" aria-hidden="true"></i>`;
  }

  function renderLikeIcon(isActive) {
    return `<i class="fa-${isActive ? "solid" : "regular"} fa-heart" aria-hidden="true"></i>`;
  }

  function renderPreviewIcon() {
    return `<i class="fa-regular fa-eye" aria-hidden="true"></i>`;
  }

  function readLikedPublicationIds() {
    try {
      const raw = localStorage.getItem(likedPublicationsStorageKey);
      const parsed = raw ? JSON.parse(raw) : [];
      return new Set(Array.isArray(parsed) ? parsed.map(String) : []);
    } catch {
      return new Set();
    }
  }

  function writeLikedPublicationIds(ids) {
    try {
      localStorage.setItem(likedPublicationsStorageKey, JSON.stringify([...ids]));
    } catch {
      // Ignore storage failures and keep in-memory behavior only.
    }
  }

  function resolveDefaultFavoriteListId(lists, suggestedListName) {
    const normalizedSuggestedName = normalizeFavoriteListName(suggestedListName);
    const exactGroupMatch = lists.find(list => normalizeFavoriteListName(list?.name) === normalizedSuggestedName);
    if (exactGroupMatch?.id) {
      return exactGroupMatch.id;
    }

    const lastListId = readLastFavoriteList();
    if (!lastListId) {
      return null;
    }

    const lastSelected = lists.find(list => String(list?.id) === String(lastListId));
    return lastSelected?.id || null;
  }

  function normalizeFavoriteListName(value) {
    return String(value || "").trim().toLowerCase();
  }

  function rememberLastFavoriteList(listId) {
    try {
      localStorage.setItem(favoriteLastListStorageKey, String(listId));
    } catch {
      // Ignore storage failures and keep default behavior.
    }
  }

  function readLastFavoriteList() {
    try {
      return localStorage.getItem(favoriteLastListStorageKey);
    } catch {
      return null;
    }
  }

  async function initContentMaps() {
    const start = performance.now();
    const homeMap = document.getElementById("map");
    const publicationMap = document.querySelector("[data-publication-map]");
    const createMap = document.querySelector("[data-create-map]");
    if (!homeMap && !publicationMap && !createMap) return;
    detailDebugLog("initContentMaps:targets", {
      homeMap: Boolean(homeMap),
      publicationMap: Boolean(publicationMap),
      createMap: Boolean(createMap)
    });

    const sdk = await detailDebugMeasure("loadMapLibreSdk", () => loadMapLibreSdk());
    if (homeMap) {
      await detailDebugMeasure("initHomeMap:home", () => initHomeMap(homeMap, sdk, { showLoading: false }));
    }

    if (publicationMap) {
      await detailDebugMeasure("initHomeMap:publication", () => initHomeMap(publicationMap, sdk, { showLoading: false }));
      wireDetailMapFullscreen(publicationMap, sdk);
    }

    if (createMap) {
      await detailDebugMeasure("initCreateMap", () => initCreateMap(createMap, sdk));
    }
    detailDebugLog("initContentMaps:done", {
      ms: Number((performance.now() - start).toFixed(1))
    });
  }

  function schedulePublicationMapInitialization(publicationMap, options = {}) {
    if (!publicationMap || publicationMap.dataset.mapInitialized === "true") {
      return () => {};
    }

    const scrollRoot = options.scrollRoot instanceof Element ? options.scrollRoot : null;
    let observer = null;
    let fallbackTimer = null;

    const start = async () => {
      if (publicationMap.dataset.mapInitialized === "true" || publicationMap.dataset.mapLoading === "true") {
        detailDebugLog("publicationMap:init:skip", {
          initialized: publicationMap.dataset.mapInitialized === "true",
          loading: publicationMap.dataset.mapLoading === "true"
        });
        return;
      }

      observer?.disconnect?.();
      observer = null;
      publicationMap.dataset.mapLoading = "true";

      try {
        await detailDebugMeasure("publicationMap:init", async () => {
          const sdk = await detailDebugMeasure("publicationMap:loadMapLibreSdk", () => loadMapLibreSdk());
          await detailDebugMeasure("publicationMap:initHomeMap", () => initHomeMap(publicationMap, sdk, { showLoading: false }));
          wireDetailMapFullscreen(publicationMap, sdk);
        });
      } finally {
        delete publicationMap.dataset.mapLoading;
      }
    };

    const isVisible = () => {
      const rect = publicationMap.getBoundingClientRect();
      if (scrollRoot) {
        const rootRect = scrollRoot.getBoundingClientRect();
        return rect.top < rootRect.bottom + 160 && rect.bottom > rootRect.top - 160;
      }

      return rect.top < window.innerHeight + 160 && rect.bottom > -160;
    };

    const queueImmediateStart = () => {
      window.requestAnimationFrame(() => {
        window.requestAnimationFrame(() => {
          detailDebugLog("publicationMap:queuedImmediateStart", {
            root: scrollRoot ? "preview-modal-body" : "viewport"
          });
          start().catch(console.error);
        });
      });
    };

    if (scrollRoot) {
      queueImmediateStart();
      fallbackTimer = window.setTimeout(() => {
        if (publicationMap.dataset.mapInitialized === "true" || publicationMap.dataset.mapLoading === "true") {
          return;
        }

        detailDebugLog("publicationMap:fallbackObserverAfterImmediateStart");
        observer = new IntersectionObserver(entries => {
          if (entries.some(entry => entry.isIntersecting)) {
            detailDebugLog("publicationMap:observerIntersected");
            start().catch(console.error);
          }
        }, {
          root: scrollRoot,
          rootMargin: "160px 0px",
          threshold: 0.01
        });
        observer.observe(publicationMap);
      }, 350);

      return () => {
        if (fallbackTimer) {
          window.clearTimeout(fallbackTimer);
          fallbackTimer = null;
        }
        observer?.disconnect?.();
      };
    }

    if (isVisible()) {
      detailDebugLog("publicationMap:visibleImmediately");
      start().catch(console.error);
      return () => {};
    }

    observer = new IntersectionObserver(entries => {
      if (entries.some(entry => entry.isIntersecting)) {
        detailDebugLog("publicationMap:observerIntersected");
        start().catch(console.error);
      }
    }, {
      root: scrollRoot,
      rootMargin: "160px 0px",
      threshold: 0.01
    });
    detailDebugLog("publicationMap:observerBound", {
      root: scrollRoot ? "preview-modal-body" : "viewport"
    });
    observer.observe(publicationMap);

    return () => {
      if (fallbackTimer) {
        window.clearTimeout(fallbackTimer);
        fallbackTimer = null;
      }
      observer?.disconnect?.();
    };
  }

  function wireDetailGalleryLayout() {
    const galleries = Array.from(document.querySelectorAll(".detail-gallery"));
    if (!galleries.length) return;

    const applyLayout = gallery => {
      const styles = window.getComputedStyle(gallery);
      const gap = Number.parseFloat(styles.columnGap || styles.gap || "16") || 16;
      const minCardWidth = 180;
      const availableWidth = gallery.clientWidth || gallery.parentElement?.clientWidth || window.innerWidth;
      const columnCount = Math.max(1, Math.floor((availableWidth + gap) / (minCardWidth + gap)));
      gallery.style.gridTemplateColumns = `repeat(${columnCount}, minmax(0, 1fr))`;
    };

    galleries.forEach(gallery => {
      if (gallery.dataset.layoutBound === "true") return;
      gallery.dataset.layoutBound = "true";
      applyLayout(gallery);
      mediaPreloadService.prepareDetailGallery(gallery);

      const observer = new ResizeObserver(() => applyLayout(gallery));
      observer.observe(gallery);
    });
  }

  function loadMapLibreSdk() {
    if (mapLibreSdkPromise) {
      detailDebugLog("loadMapLibreSdk:reusePromise");
      return mapLibreSdkPromise;
    }

    mapLibreSdkPromise = new Promise((resolve, reject) => {
      if (!document.querySelector('link[data-maplibre-css="true"]')) {
        const link = document.createElement("link");
        link.dataset.maplibreCss = "true";
        link.rel = "stylesheet";
        link.href = "https://unpkg.com/maplibre-gl@4.7.1/dist/maplibre-gl.css";
        document.head.appendChild(link);
      }

      if (window.maplibregl) {
        detailDebugLog("loadMapLibreSdk:windowReady");
        resolve(window.maplibregl);
        return;
      }

      const existingScript = document.querySelector('script[data-maplibre-sdk="true"]');
      if (existingScript) {
        detailDebugLog("loadMapLibreSdk:waitingExistingScript");
        existingScript.addEventListener("load", () => resolve(window.maplibregl), { once: true });
        existingScript.addEventListener("error", () => reject(new Error("No se pudo cargar MapLibre.")), { once: true });
        return;
      }

      const script = document.createElement("script");
      script.dataset.maplibreSdk = "true";
      script.src = "https://unpkg.com/maplibre-gl@4.7.1/dist/maplibre-gl.js";
      script.onload = () => {
        detailDebugLog("loadMapLibreSdk:scriptLoaded");
        resolve(window.maplibregl);
      };
      script.onerror = () => reject(new Error("No se pudo cargar MapLibre."));
      document.head.appendChild(script);
    });

    return mapLibreSdkPromise;
  }

  async function initHomeMap(mapElement, sdk, options = {}) {
    if (mapElement.dataset.mapInitialized === "true") return;
    const mapStart = performance.now();
    const loadingTicket = options.showLoading ? beginSystemLoading() : null;
    const finishLoading = () => {
      if (loadingTicket) {
        endSystemLoading(loadingTicket);
      }
    };
    const styleUrl = String(mapElement.dataset.mapStyleUrl || "").trim();
    const tilesUrlTemplate = String(mapElement.dataset.mapTilesUrl || "").trim();
    if (!styleUrl && !tilesUrlTemplate) {
      finishLoading();
      return;
    }
    const attribution = String(mapElement.dataset.mapAttribution || "").trim();
    const mapMode = mapElement.dataset.mapMode || "home";
    const initialLat = Number.parseFloat(mapElement.dataset.mapInitialLat || "");
    const initialLng = Number.parseFloat(mapElement.dataset.mapInitialLng || "");
    const hasInitialCenter = Number.isFinite(initialLat) && Number.isFinite(initialLng);
    detailDebugLog("initHomeMap:start", {
      mapMode,
      hasStyleUrl: Boolean(styleUrl),
      hasTilesTemplate: Boolean(tilesUrlTemplate),
      hasInitialCenter
    });

    let markers = JSON.parse(mapElement.dataset.markers || "[]");
    if (!markers.length) {
      finishLoading();
      return;
    }

    applyInitialViewportMapHeight(mapElement.closest("[data-map-layout]") || document);
    mapElement.innerHTML = "";
    mapElement.getBoundingClientRect();

    const instance = new sdk.Map({
      container: mapElement,
      style: buildMapStyle(styleUrl, tilesUrlTemplate, attribution, { preferRaster: mapMode === "detail" }),
      attributionControl: false,
      maxBounds: supportedMapBounds,
      center: hasInitialCenter ? [initialLng, initialLat] : [markers[0].lng, markers[0].lat],
      zoom: mapMode === "detail" ? zoomOutLevel(17.5) : (hasInitialCenter ? zoomOutLevel(11) : 5)
    });
    addCompactAttributionControl(instance, sdk);
    const ensureInitialMapSize = () => {
      instance.resize?.();
      window.requestAnimationFrame(() => instance.resize?.());
      window.setTimeout(() => instance.resize?.(), 60);
    };
    const mapLayout = mapElement.closest("[data-map-layout]");
    const selectionPanel = mapLayout?.querySelector("[data-map-selection-card]");
    const markersEndpoint = String(mapElement.dataset.mapMarkersEndpoint || "").trim();
    const mapSelectionAction = mapElement.dataset.mapSelectionAction || "";
    const selectionViewToggle = mapLayout?.querySelector("[data-map-selection-view-toggle]");
    const hoverPopup = new sdk.Popup({
      closeButton: false,
      closeOnClick: false,
      offset: 18,
      className: "map-hover-popup"
    });
    const mobileTapPopup = new sdk.Popup({
      closeButton: false,
      closeOnClick: true,
      offset: 20,
      className: "map-tap-popup"
    });
    let selectedMarker = null;
    let selectedMarkerView = selectionViewToggle?.dataset.currentView === "text" ? "text" : "gallery";
    let markerInstances = [];

    const syncSelectionViewButtons = () => {
      selectionViewToggle?.querySelectorAll("[data-map-selection-view]").forEach(button => {
        const isActive = button.dataset.mapSelectionView === selectedMarkerView;
        button.classList.toggle("is-active", isActive);
        button.setAttribute("aria-pressed", isActive ? "true" : "false");
      });
    };

    const renderEmptySelection = (message = "No hay anuncios en esta zona del mapa.") => {
      if (!selectionPanel) return;
      selectionPanel.innerHTML = `<section class="empty-state compact-empty"><h3>Sin anuncios visibles</h3><p>${escapeHtml(message)}</p></section>`;
    };

    const renderSelectedMarker = () => {
      if (!selectionPanel) return;
      if (!selectedMarker) {
        renderEmptySelection();
        return;
      }

      const isSharedListSelection = mapSelectionAction === "shared-list";
      const sharedListAction = isSharedListSelection
        ? `<button type="submit" class="primary-pill compact shared-list-map-add" name="publicationIds" value="${escapeAttribute(selectedMarker.id)}" data-shared-list-map-add="true"><i class="fa-solid fa-check" aria-hidden="true"></i><span>Agregar a la lista</span></button>`
        : "";
      selectionPanel.innerHTML = selectedMarkerView === "text"
        ? buildMapSelectionTextCard(selectedMarker)
        : buildGalleryCard({
            id: selectedMarker.id,
            title: selectedMarker.title,
            shortDescription: selectedMarker.shortDescription,
            publicationCode: selectedMarker.code,
            price: selectedMarker.price,
            priceTooltip: selectedMarker.priceTooltip,
            operationLabel: selectedMarker.operationLabel,
            categoryLabel: selectedMarker.categoryLabel,
            detailsUrl: selectedMarker.detailsUrl,
            videoUrl: selectedMarker.videoUrl,
            images: Array.isArray(selectedMarker.images) && selectedMarker.images.length
              ? selectedMarker.images
              : [selectedMarker.image || "/images/logo4.png"],
            isFavorite: Boolean(selectedMarker.isFavorite),
            groupName: selectedMarker.groupName || "Inmuebles"
          }, false, {
            wrapCard: false,
            showReportButton: !isSharedListSelection,
            showFavoriteButton: !isSharedListSelection,
            extraActionHtml: sharedListAction
          });

      wireGalleryCards();
      wireFavoriteActions(selectionPanel);
      wireReportForm();
      syncMobileGalleryVideoAutoplay(selectionPanel);
    };

    const setSelectedMarker = marker => {
      if (!selectionPanel) return;
      selectedMarker = marker;
      renderSelectedMarker();
    };

    if (selectionViewToggle && selectionViewToggle.dataset.bound !== "true") {
      selectionViewToggle.dataset.bound = "true";
      selectionViewToggle.querySelectorAll("[data-map-selection-view]").forEach(button => {
        button.addEventListener("click", event => {
          event.preventDefault();
          const nextView = button.dataset.mapSelectionView === "text" ? "text" : "gallery";
          if (nextView === selectedMarkerView) return;

          selectedMarkerView = nextView;
          selectionViewToggle.dataset.currentView = selectedMarkerView;
          syncSelectionViewButtons();
          renderSelectedMarker();
        });
      });
    }

    syncSelectionViewButtons();
    const handleMarkerSelection = marker => {
      if (isMobileMapInteractionContext()) {
        hoverPopup.remove();
        mobileTapPopup
          .setLngLat([marker.lng, marker.lat])
          .setHTML(buildMapMarkerTapCard(marker))
          .addTo(instance);
        return;
      }

      setSelectedMarker(marker);
    };

    const renderMapMarkers = ({ preserveSelection = false } = {}) => {
      hoverPopup.remove();
      mobileTapPopup.remove();
      markerInstances.forEach(markerInstance => markerInstance.remove());
      markerInstances = [];

      if (!markers.length) {
        selectedMarker = null;
        renderEmptySelection();
        return null;
      }

      const bounds = new sdk.LngLatBounds();
      markers.forEach(marker => {
        const markerInstance = new sdk.Marker({ color: "#ff5a5f" })
          .setLngLat([marker.lng, marker.lat])
          .addTo(instance);
        const markerElement = markerInstance.getElement();
        let lastTouchSelectionAt = 0;
        markerElement.addEventListener("click", event => {
          if (Date.now() - lastTouchSelectionAt < 500) {
            event.preventDefault();
            event.stopPropagation();
            return;
          }

          event.preventDefault();
          event.stopPropagation();
          handleMarkerSelection(marker);
        });
        markerElement.addEventListener("touchend", event => {
          lastTouchSelectionAt = Date.now();
          event.preventDefault();
          event.stopPropagation();
          handleMarkerSelection(marker);
        }, { passive: false });
        markerElement.addEventListener("pointerup", event => {
          if (event.pointerType !== "touch") return;
          lastTouchSelectionAt = Date.now();
          event.preventDefault();
          event.stopPropagation();
          handleMarkerSelection(marker);
        });
        markerElement.addEventListener("mouseenter", () => {
          hoverPopup
            .setLngLat([marker.lng, marker.lat])
            .setHTML(buildMapMarkerHoverCard(marker))
            .addTo(instance);
        });
        markerElement.addEventListener("mouseleave", () => {
          hoverPopup.remove();
        });

        markerInstances.push(markerInstance);
        bounds.extend([marker.lng, marker.lat]);
      });

      const nextSelectedMarker = preserveSelection && selectedMarker
        ? markers.find(marker => String(marker.id) === String(selectedMarker.id)) || null
        : null;

      if (!isMobileMapInteractionContext()) {
        setSelectedMarker(nextSelectedMarker || markers[0]);
      } else {
        selectedMarker = nextSelectedMarker || markers[0] || null;
      }

      instance.resize?.();
      return bounds;
    };

    const fitInitialViewport = bounds => {
      if (!bounds || !markers.length) return;

      if (mapMode === "home" && hasInitialCenter) {
        instance.flyTo({ center: [initialLng, initialLat], zoom: zoomOutLevel(11) });
      } else if (markers.length > 1) {
        instance.fitBounds(bounds, { padding: 60 });
      } else if (mapMode === "detail") {
        instance.flyTo({ center: [markers[0].lng, markers[0].lat], zoom: zoomOutLevel(17.5) });
      }
    };

    const syncRefreshButtonVisibility = button => {
      if (!button) return;
      button.hidden = !(mapMode !== "detail" && markersEndpoint);
    };

    const refreshVisibleMapMarkers = async () => {
      if (!markersEndpoint) return;
      const currentBounds = instance.getBounds?.();
      if (!currentBounds) return;

      const endpointUrl = new URL(markersEndpoint, window.location.origin);
      endpointUrl.searchParams.set("north", String(currentBounds.getNorth()));
      endpointUrl.searchParams.set("south", String(currentBounds.getSouth()));
      endpointUrl.searchParams.set("east", String(currentBounds.getEast()));
      endpointUrl.searchParams.set("west", String(currentBounds.getWest()));

      const response = await fetch(endpointUrl.toString(), {
        headers: { "X-Requested-With": "fetch" }
      });
      if (!response.ok) {
        throw new Error(`Map refresh failed with status ${response.status}`);
      }

      const result = await response.json().catch(() => ({}));
      markers = Array.isArray(result?.items) ? result.items : [];
      mapElement.dataset.markers = JSON.stringify(markers);
      renderMapMarkers({ preserveSelection: true });
    };

    const initialBounds = renderMapMarkers();
    fitInitialViewport(initialBounds);

    if (mapMode !== "detail" && markersEndpoint) {
      const refreshHost = mapElement.parentElement?.classList.contains("map-canvas-shell")
        ? mapElement.parentElement
        : mapElement;
      let refreshButton = refreshHost.querySelector("[data-map-refresh='true']");
      if (!refreshButton) {
        refreshButton = document.createElement("button");
        refreshButton.type = "button";
        refreshButton.className = "map-refresh-button";
        refreshButton.dataset.mapRefresh = "true";
        refreshButton.innerHTML = `<i class="fa-solid fa-rotate-right" aria-hidden="true"></i><span>Actualizar</span>`;
        refreshButton.setAttribute("aria-label", "Actualizar anuncios en esta zona");
        refreshHost.appendChild(refreshButton);
      }

      syncRefreshButtonVisibility(refreshButton);

      if (refreshButton.dataset.bound !== "true") {
        refreshButton.dataset.bound = "true";
        refreshButton.addEventListener("click", async event => {
          event.preventDefault();
          const label = refreshButton.querySelector("span");
          try {
            refreshButton.disabled = true;
            refreshButton.classList.add("is-loading");
            if (label) {
              label.textContent = "Actualizando...";
            }
            await refreshVisibleMapMarkers();
            syncRefreshButtonVisibility(refreshButton);
          } catch (error) {
            console.error(error);
          } finally {
            refreshButton.classList.remove("is-loading");
            if (label) {
              label.textContent = "Actualizar";
            }
            refreshButton.disabled = false;
          }
        });
      }
    }

    if (!isMobileMapInteractionContext()) {
      setSelectedMarker(selectedMarker || markers[0] || null);
    }

    instance.once?.("idle", () => {
      detailDebugLog("initHomeMap:idle", {
        mapMode,
        ms: Number((performance.now() - mapStart).toFixed(1))
      });
      ensureInitialMapSize();
      scrollMapIntoViewAfterRender(mapElement);
      finishLoading();
    });
    instance.once?.("load", () => {
      detailDebugLog("initHomeMap:load", {
        mapMode,
        ms: Number((performance.now() - mapStart).toFixed(1))
      });
      ensureInitialMapSize();
    });
    instance.once?.("error", () => {
      detailDebugLog("initHomeMap:error", {
        mapMode,
        ms: Number((performance.now() - mapStart).toFixed(1))
      });
      finishLoading();
    });
    ensureInitialMapSize();

    mapElement._mapInstance = instance;
    mapElement.dataset.mapInitialized = "true";
    return instance;
  }

  function wireDetailMapFullscreen(mapElement, sdk) {
    if (!mapElement || mapElement.dataset.fullscreenBound === "true") return;
    mapElement.dataset.fullscreenBound = "true";

    const panel = mapElement.closest(".detail-panel");
    if (!panel) return;
    if (!mapElement._mapInstance) return;

    const actions = document.createElement("div");
    actions.className = "detail-panel-actions";
    actions.innerHTML = `
      <button type="button" class="ghost-pill compact map-fullscreen-trigger" data-map-fullscreen-trigger>
        Ver mapa a pantalla completa
      </button>
    `;

    const footer = panel.querySelector("[data-detail-map-footer]");
    if (footer) {
      footer.appendChild(actions);
    } else {
      mapElement.insertAdjacentElement("afterend", actions);
    }

    const trigger = actions.querySelector("[data-map-fullscreen-trigger]");
    const syncTriggerLabel = () => {
      const isFullscreen = document.fullscreenElement === mapElement || mapElement.classList.contains("is-map-fullscreen");
      if (trigger) {
        trigger.textContent = isFullscreen ? "Salir de pantalla completa" : "Ver mapa a pantalla completa";
      }
      document.body.classList.toggle("map-fullscreen-open", mapElement.classList.contains("is-map-fullscreen"));
    };

    const toggleFullscreen = async () => {
      const mapInstance = mapElement._mapInstance;
      if (!mapInstance) return;

      if (document.fullscreenElement === mapElement) {
        await document.exitFullscreen?.();
        syncTriggerLabel();
        return;
      }

      if (mapElement.requestFullscreen) {
        await mapElement.requestFullscreen();
        mapInstance.resize?.();
        syncTriggerLabel();
        return;
      }

      mapElement.classList.toggle("is-map-fullscreen");
      syncTriggerLabel();
      mapInstance.resize?.();
    };

    trigger?.addEventListener("click", event => {
      event.preventDefault();
      toggleFullscreen().catch(() => {
        mapElement.classList.toggle("is-map-fullscreen");
        syncTriggerLabel();
        mapElement._mapInstance?.resize?.();
      });
    });

    document.addEventListener("fullscreenchange", () => {
      syncTriggerLabel();
      mapElement._mapInstance?.resize?.();
    });

    syncTriggerLabel();
  }

  function requestCreateMapResize(mapElement) {
    const instance = mapElement?._mapInstance;
    if (!mapElement || !instance) return;

    const resize = () => instance.resize?.();
    resize();
    window.requestAnimationFrame(resize);
    window.setTimeout(resize, 120);
    window.setTimeout(resize, 360);
  }

  async function initCreateMap(mapElement, sdk) {
    if (mapElement.dataset.mapInitialized === "true") return;
    const styleUrl = String(mapElement.dataset.mapStyleUrl || "").trim();
    const tilesUrlTemplate = String(mapElement.dataset.mapTilesUrl || "").trim();
    if (!styleUrl && !tilesUrlTemplate) return;
    const attribution = String(mapElement.dataset.mapAttribution || "").trim();
    const geocodingSearchUrlTemplate = String(mapElement.dataset.mapGeocodingSearchUrl || "").trim() || defaultGeocodingSearchUrlTemplate;
    const reverseGeocodingUrlTemplate = String(mapElement.dataset.mapReverseGeocodingUrl || "").trim() || defaultReverseGeocodingUrlTemplate;

    const form = mapElement.closest("form");
    if (!form) return;

    const latitudeInput = form.querySelector('input[name="latitude"]');
    const longitudeInput = form.querySelector('input[name="longitude"]');
    const localityInput = form.querySelector('input[name="locality"]');
    const addressInput = form.querySelector('input[name="address"]');
    const searchInput = form.querySelector('input[name="locationSearch"]');
    const noLocationInput = form.querySelector("[data-create-no-location]");
    const searchButton = form.querySelector("[data-create-address-search]");
    const summary = form.querySelector("[data-create-location-summary]");
    let noLocationMode = Boolean(noLocationInput?.checked);
    const fallbackLocality = String(localityInput?.value || "").trim();
    const fallbackAddress = String(addressInput?.value || searchInput?.value || "").trim();
    const fallbackLocalityLabel = String(searchInput?.defaultValue || fallbackAddress || fallbackLocality || "").trim();
    const fallbackLatitude = numberOrNull(mapElement.dataset.mapInitialLat || "");
    const fallbackLongitude = numberOrNull(mapElement.dataset.mapInitialLng || "");
    mapElement.innerHTML = "";

    const defaultCenter = getCreateMapCenter(
      latitudeInput?.value || "",
      longitudeInput?.value || "",
      mapElement.dataset.mapInitialLat || "",
      mapElement.dataset.mapInitialLng || ""
    );
    const instance = new sdk.Map({
      container: mapElement,
      style: buildMapStyle(styleUrl, tilesUrlTemplate, attribution, { preferRaster: true }),
      attributionControl: false,
      maxBounds: supportedMapBounds,
      center: defaultCenter.center,
      zoom: defaultCenter.zoom
    });
    addCompactAttributionControl(instance, sdk);

    const marker = new sdk.Marker({ color: "#ff4b5f", draggable: true })
      .setLngLat(defaultCenter.center)
      .addTo(instance);

    const syncLocation = ({ lat, lng, locality, address, searchValue, flyTo = true }) => {
      if (noLocationMode) {
        return;
      }

      if (latitudeInput) latitudeInput.value = String(lat);
      if (longitudeInput) longitudeInput.value = String(lng);
      if (localityInput) localityInput.value = locality || "";
      if (addressInput) addressInput.value = address || locality || "";
      if (searchInput && searchValue) searchInput.value = searchValue;
      if (summary) {
        summary.textContent = formatCreateLocationSummary({ locality, address, lat, lng });
      }
      syncCreateTitle(form);
      if (flyTo) {
        instance.flyTo({ center: [lng, lat], zoom: zoomOutLevel(15) });
      }
      marker.setLngLat([lng, lat]);
    };

    const setNoLocationMode = enabled => {
      noLocationMode = enabled;
      mapElement.classList.toggle("is-disabled", enabled);

      if (searchInput) {
        searchInput.disabled = enabled;
      }

      if (searchButton) {
        searchButton.disabled = enabled || !geocodingSearchUrlTemplate;
      }

      if (enabled) {
        if (latitudeInput) latitudeInput.value = fallbackLatitude !== null ? String(fallbackLatitude) : "";
        if (longitudeInput) longitudeInput.value = fallbackLongitude !== null ? String(fallbackLongitude) : "";
        if (localityInput) localityInput.value = fallbackLocality;
        if (addressInput) addressInput.value = "";
        if (searchInput) searchInput.value = fallbackLocalityLabel;
        if (summary) {
          summary.innerHTML = fallbackLocalityLabel
            ? `Se publicará para tu localidad de usuario: <strong>${escapeHtml(fallbackLocalityLabel)}</strong>, sin punto en el mapa.`
            : "Se publicará sin punto en el mapa.";
        }
        syncCreateTitle(form);
        return;
      }

      const currentLat = numberOrNull(latitudeInput?.value || "");
      const currentLng = numberOrNull(longitudeInput?.value || "");
      if (currentLat !== null && currentLng !== null) {
        syncLocation({
          lat: currentLat,
          lng: currentLng,
          locality: localityInput?.value || "",
          address: addressInput?.value || "",
          searchValue: addressInput?.value || localityInput?.value || "",
          flyTo: false
        });
        return;
      }

      if (summary) {
        summary.textContent = "Elegí una ubicación en el mapa o buscala por dirección.";
      }
    };

    if (searchInput) {
      searchInput.disabled = noLocationMode;
    }

    if (searchButton) {
      searchButton.disabled = !geocodingSearchUrlTemplate || noLocationMode;
    }

    const initialLat = numberOrNull(latitudeInput?.value || "");
    const initialLng = numberOrNull(longitudeInput?.value || "");
    if (noLocationMode) {
      setNoLocationMode(true);
    } else if (initialLat !== null && initialLng !== null) {
      syncLocation({
        lat: initialLat,
        lng: initialLng,
        locality: localityInput?.value || "",
        address: addressInput?.value || "",
        searchValue: addressInput?.value || localityInput?.value || "",
        flyTo: false
      });
    } else if (summary) {
      summary.textContent = "Elegí una ubicación en el mapa o buscala por dirección.";
    }

    marker.on("dragend", async () => {
      const position = marker.getLngLat();
      await resolveCreateLocationFromCoordinates(reverseGeocodingUrlTemplate, position.lng, position.lat, syncLocation);
    });

    instance.on("click", async event => {
      await resolveCreateLocationFromCoordinates(reverseGeocodingUrlTemplate, event.lngLat.lng, event.lngLat.lat, syncLocation);
    });

    const runSearch = async () => {
      if (noLocationMode) {
        return;
      }

      const query = String(searchInput?.value || "").trim();
      if (!query) return;

      const results = await geocodeCreateLocation(geocodingSearchUrlTemplate, query);
      const feature = results?.[0];
      if (!feature) {
        if (summary) summary.textContent = "No encontramos esa dirección. Probá con otra búsqueda.";
        return;
      }

      if (!isWithinSupportedRegion(feature.lng, feature.lat)) {
        if (summary) summary.textContent = "Por ahora solo admitimos ubicaciones en Argentina, Paraguay y Uruguay.";
        return;
      }

      syncLocation({
        lat: Number(feature.lat),
        lng: Number(feature.lng),
        locality: extractLocalityFromFeature(feature),
        address: feature.address,
        searchValue: feature.address
      });
    };

    noLocationInput?.addEventListener("change", () => {
      setNoLocationMode(Boolean(noLocationInput.checked));
    });

    searchButton?.addEventListener("click", event => {
      event.preventDefault();
      runSearch();
    });

    searchInput?.addEventListener("keydown", event => {
      if (event.key === "Enter") {
        event.preventDefault();
        runSearch();
      }
    });

    if ("ResizeObserver" in window) {
      const resizeObserver = new ResizeObserver(() => {
        requestCreateMapResize(mapElement);
      });
      resizeObserver.observe(mapElement);
      const sectionBody = mapElement.closest('[data-section-body="location"]');
      if (sectionBody) {
        resizeObserver.observe(sectionBody);
      }
    }

    instance.once?.("load", () => requestCreateMapResize(mapElement));
    instance.once?.("idle", () => requestCreateMapResize(mapElement));
    mapElement._mapInstance = instance;
    mapElement.dataset.mapInitialized = "true";
  }

  function renderMapPlaceholder(mapElement, title, message) {
    mapElement.innerHTML = `
      <div class="map-placeholder">
        <h2>${escapeHtml(title)}</h2>
        <p>${escapeHtml(message)}</p>
      </div>
    `;
  }

  function enableCreateMapFallback(mapElement, noLocationInput, searchInput, searchButton, summary, message) {
    mapElement.classList.add("is-disabled");
    renderMapPlaceholder(
      mapElement,
      "Mapa no disponible",
      message || "No se pudo cargar el mapa para esta publicacion."
    );

    if (noLocationInput) {
      noLocationInput.checked = true;
    }

    if (searchInput) {
      searchInput.disabled = true;
      searchInput.value = "";
    }

    if (searchButton) {
      searchButton.disabled = true;
    }

    if (summary) {
      summary.textContent = "Mapa no disponible. La publicacion se guardara sin ubicacion.";
    }
  }

  function getCreateMapCenter(latValue, lngValue, fallbackLatValue = "", fallbackLngValue = "") {
    const lat = numberOrNull(latValue);
    const lng = numberOrNull(lngValue);
    if (lat !== null && lng !== null && isWithinSupportedRegion(lng, lat)) {
      return { center: [lng, lat], zoom: 15 };
    }

    const fallbackLat = numberOrNull(fallbackLatValue);
    const fallbackLng = numberOrNull(fallbackLngValue);
    if (fallbackLat !== null && fallbackLng !== null && isWithinSupportedRegion(fallbackLng, fallbackLat)) {
      return { center: [fallbackLng, fallbackLat], zoom: 13 };
    }

    return { center: supportedMapCenter, zoom: 4.8 };
  }

  async function resolveCreateLocationFromCoordinates(reverseGeocodingUrlTemplate, lng, lat, syncLocation) {
    if (!isWithinSupportedRegion(lng, lat)) {
      return;
    }

    const feature = await reverseGeocodeCreateLocation(reverseGeocodingUrlTemplate, lng, lat);
    syncLocation({
      lat,
      lng,
      locality: feature ? extractLocalityFromFeature(feature) : `Lat ${Number(lat).toFixed(5)}, Lng ${Number(lng).toFixed(5)}`,
      address: feature?.address || `Lat ${Number(lat).toFixed(5)}, Lng ${Number(lng).toFixed(5)}`,
      searchValue: feature?.address || `Lat ${Number(lat).toFixed(5)}, Lng ${Number(lng).toFixed(5)}`
    });
  }

  async function geocodeCreateLocation(geocodingSearchUrlTemplate, query) {
    const endpoint = geocodingSearchUrlTemplate.replace("{query}", encodeURIComponent(query));
    const response = await fetch(endpoint, {
      headers: {
        Accept: "application/json"
      }
    });
    if (!response.ok) return [];
    const payload = await response.json();
    return Array.isArray(payload) ? payload.map(normalizeNominatimFeature) : [];
  }

  async function reverseGeocodeCreateLocation(reverseGeocodingUrlTemplate, lng, lat) {
    if (!reverseGeocodingUrlTemplate) return null;

    const endpoint = reverseGeocodingUrlTemplate
      .replace("{lng}", encodeURIComponent(String(lng)))
      .replace("{lat}", encodeURIComponent(String(lat)));
    const response = await fetch(endpoint, {
      headers: {
        Accept: "application/json"
      }
    });
    if (!response.ok) return null;
    const payload = await response.json();
    return normalizeNominatimFeature(payload);
  }

  function extractLocalityFromFeature(feature) {
    const address = feature?.rawAddress || feature?.addressParts || {};
    return address.city
      || address.town
      || address.village
      || address.municipality
      || address.suburb
      || address.county
      || feature?.displayName
      || feature?.address
      || "";
  }

  function normalizeNominatimFeature(feature) {
    if (!feature) return null;

    const latitude = Number.parseFloat(feature.lat);
    const longitude = Number.parseFloat(feature.lon);
    if (!Number.isFinite(latitude) || !Number.isFinite(longitude)) {
      return null;
    }

    return {
      lat: latitude,
      lng: longitude,
      address: feature.display_name || "",
      displayName: feature.display_name || "",
      rawAddress: feature.address || {},
      addressParts: feature.address || {}
    };
  }

  function buildMapStyle(styleUrl, tilesUrlTemplate, attribution, options = {}) {
    const preferRaster = options?.preferRaster === true;
    if (styleUrl && !(preferRaster && tilesUrlTemplate)) {
      return styleUrl;
    }

    return {
      version: 8,
      sources: {
        "osm-raster": {
          type: "raster",
          tiles: [tilesUrlTemplate],
          tileSize: 256,
          attribution
        }
      },
      layers: [
        {
          id: "osm-raster",
          type: "raster",
          source: "osm-raster"
        }
      ]
    };
  }

  function isWithinSupportedRegion(lng, lat) {
    return lng >= supportedMapBounds[0][0]
      && lng <= supportedMapBounds[1][0]
      && lat >= supportedMapBounds[0][1]
      && lat <= supportedMapBounds[1][1];
  }

  function formatCreateLocationSummary(location) {
    const pieces = [];
    if (location.locality) {
      pieces.push(location.locality);
    }
    if (location.address && location.address !== location.locality) {
      pieces.push(location.address);
    }
    if (!pieces.length) {
      pieces.push(`Lat ${Number(location.lat).toFixed(5)} · Lng ${Number(location.lng).toFixed(5)}`);
    }

    return `Ubicación seleccionada: ${pieces.join(" · ")}`;
  }

  function syncCreateTitle(form) {
    const category = String(form.querySelector('[name="category"]')?.selectedOptions?.[0]?.textContent || "").trim();
    const locality = String(form.querySelector('input[name="locality"]')?.value || "").trim();
    const latitude = numberOrNull(form.querySelector('input[name="latitude"]')?.value || "");
    const longitude = numberOrNull(form.querySelector('input[name="longitude"]')?.value || "");
    const noLocation = Boolean(form.querySelector('[name="noLocation"]')?.checked);
    const titleInput = form.querySelector('input[name="title"]');
    if (!titleInput) return;

    const hasSelectedMapLocation = !noLocation && latitude !== null && longitude !== null;
    if (category && locality && hasSelectedMapLocation) {
      titleInput.value = `${category} en ${locality}`;
      return;
    }

    titleInput.value = category || "Nueva publicaciÃ³n";
  }

  function wireBrowseSearchFilters(root = document) {
    root.querySelectorAll("[data-price-range-filter]").forEach(wirePriceRangeFilter);
    root.querySelectorAll("[data-radius-range-filter]").forEach(wireRadiusRangeFilter);
    root.querySelectorAll("[data-group-aware-search-form='true']").forEach(wireGroupAwareSearchPlaceholder);

    const form = root.querySelector?.("[data-required-filter-form='true']");
    if (!form || form.dataset.requiredFiltersBound === "true") return;

    const groupSelect = form.querySelector('select[name="group"]');
    const endpoint = form.dataset.requiredFilterFieldsEndpoint || "";
    const panel = form.querySelector("[data-required-filter-panel]");
    const list = form.querySelector("[data-required-filter-list]");
    const fieldsScript = form.querySelector("[data-required-filter-fields-json]");
    const advancedPanel = panel?.closest(".search-advanced-panel");
    form.dataset.requiredFiltersBound = "true";

    let fields = parseRequiredFilterFields(fieldsScript?.textContent);

    const renderFields = () => {
      if (!panel || !list) return;

      panel.hidden = fields.length === 0;
      list.innerHTML = fields.map(field => buildRequiredFilterRow(field)).join("");
    };

    const loadFieldsForGroup = async () => {
      if (!endpoint || !groupSelect) return;

      const response = await fetch(`${endpoint}?group=${encodeURIComponent(groupSelect.value)}`, {
        headers: { "X-Requested-With": "fetch" }
      });
      fields = response.ok ? parseRequiredFilterFields(await response.text()) : [];
      renderFields();
    };

    groupSelect?.addEventListener("change", async () => {
      await loadFieldsForGroup();
    });

    renderFields();
    if (fields.length === 0) {
      loadFieldsForGroup().catch(console.error);
    }
  }

  function wireGroupAwareSearchPlaceholder(form) {
    if (!form || form.dataset.groupAwarePlaceholderBound === "true") return;

    const groupSelect = form.querySelector('select[name="group"]');
    const queryInput = form.querySelector("[data-group-aware-query-input='true']");
    const categorySelect = form.querySelector("[data-group-category-select='true']");
    const operationSelect = form.querySelector("[data-group-operation-select='true']");
    if (!groupSelect || !queryInput) return;

    form.dataset.groupAwarePlaceholderBound = "true";

    const placeholders = {
      inmuebles: groupSelect.dataset.placeholderInmuebles || "Ej. departamento, casa con patio, lote",
      rodados: groupSelect.dataset.placeholderRodados || "Ej. Ford Fiesta, moto, camioneta",
      embarcaciones: groupSelect.dataset.placeholderEmbarcaciones || "Ej. lancha, velero, semirrígido",
      agro: groupSelect.dataset.placeholderAgro || "Ej. tractor, sembradora, generador",
      electronica: groupSelect.dataset.placeholderElectronica || "Ej. celular, notebook, playstation",
      generales: groupSelect.dataset.placeholderGenerales || "Ej. muebles, bicicleta, herramientas",
      moda: groupSelect.dataset.placeholderModa || "Ej. zapatillas, campera, cartera",
      todos: groupSelect.dataset.placeholderTodos || "Ej. departamento, Ford Fiesta, iPhone"
    };

    const syncPlaceholder = () => {
      const key = String(groupSelect.value || "Todos").trim().toLowerCase();
      queryInput.placeholder = placeholders[key] || placeholders.todos;
    };

    const syncCategoryVisibility = () => {
      const wrapper = categorySelect?.closest("[data-group-category-wrapper]");
      const isAllGroups = String(groupSelect.value || "").trim().toLowerCase() === "todos";
      if (wrapper) {
        wrapper.hidden = isAllGroups;
      }

      if (categorySelect) {
        categorySelect.disabled = isAllGroups;
        if (isAllGroups) {
          categorySelect.value = "";
        }
      }
    };

    const syncCategories = async () => {
      if (!categorySelect) return;

      syncCategoryVisibility();
      if (categorySelect.disabled) {
        return;
      }

      const endpoint = categorySelect.dataset.categoriesEndpoint || "";
      if (!endpoint) return;

      const selectedCategoryId = String(categorySelect.dataset.selectedCategoryId || categorySelect.value || "").trim();

      try {
        const response = await fetch(`${endpoint}?group=${encodeURIComponent(groupSelect.value)}`, {
          headers: { "X-Requested-With": "fetch" }
        });
        const categories = response.ok ? await response.json() : [];
        const items = Array.isArray(categories) ? categories : [];

        categorySelect.innerHTML = '<option value="">Todas</option>';
        items.forEach(category => {
          const option = document.createElement("option");
          option.value = String(category?.id ?? "");
          option.textContent = String(category?.name ?? "");
          if (option.value && option.value === selectedCategoryId) {
            option.selected = true;
          }
          categorySelect.appendChild(option);
        });

        categorySelect.dataset.selectedCategoryId = "";
      } catch (error) {
        console.error(error);
      }
    };

    const syncOperations = async () => {
      if (!operationSelect) return;

      const endpoint = form.dataset.requiredFilterFieldsEndpoint || "";
      if (!endpoint) return;

      const selectedOperation = String(operationSelect.dataset.selectedOperation || operationSelect.value || "").trim();

      try {
        const response = await fetch(`${endpoint}?group=${encodeURIComponent(groupSelect.value)}`, {
          headers: { "X-Requested-With": "fetch" }
        });
        const fields = response.ok ? parseRequiredFilterFields(await response.text()) : [];
        const operationField = Array.isArray(fields)
          ? fields.find(field => String(field?.internalName || "").trim().toLowerCase() === "operacion")
          : null;
        const options = Array.isArray(operationField?.options) ? operationField.options : [];
        const defaultOperation = options.find(operation => String(operation || "").trim().toLowerCase() === "venta") || "";
        const nextSelectedOperation = options.some(operation => String(operation || "").trim() === selectedOperation)
          ? selectedOperation
          : String(defaultOperation || "").trim();

        operationSelect.innerHTML = '<option value="">Tipo de operación</option>';
        options.forEach(operation => {
          const value = String(operation || "").trim();
          if (!value) return;

          const option = document.createElement("option");
          option.value = value;
          option.textContent = value;
          if (value === nextSelectedOperation) {
            option.selected = true;
          }

          operationSelect.appendChild(option);
        });

        operationSelect.dataset.selectedOperation = "";
      } catch (error) {
        console.error(error);
      }
    };

    syncPlaceholder();
    syncCategories().catch(console.error);
    syncOperations().catch(console.error);
    groupSelect.addEventListener("change", () => {
      syncPlaceholder();
      if (categorySelect) {
        categorySelect.dataset.selectedCategoryId = "";
      }
      if (operationSelect) {
        operationSelect.dataset.selectedOperation = "";
      }
      syncCategories().catch(console.error);
      syncOperations().catch(console.error);
    });
  }

  function wirePriceRangeFilter(wrapper) {
    if (!wrapper || wrapper.dataset.priceRangeBound === "true") return;

    const max = Number(wrapper.dataset.priceMax || 0) || 100000;
    const fromRange = wrapper.querySelector("[data-price-from-range]");
    const toRange = wrapper.querySelector("[data-price-to-range]");
    const fromValue = wrapper.querySelector("[data-price-from-value]");
    const toValue = wrapper.querySelector("[data-price-to-value]");
    const fromLabel = wrapper.querySelector("[data-price-from-label]");
    const toLabel = wrapper.querySelector("[data-price-to-label]");
    const fill = wrapper.querySelector("[data-price-range-fill]");
    if (!fromRange || !toRange || !fromValue || !toValue) return;

    wrapper.dataset.priceRangeBound = "true";

    const formatPrice = value => new Intl.NumberFormat("es-AR", {
      maximumFractionDigits: 0
    }).format(Math.max(0, Number(value || 0)));

    const sync = source => {
      let from = Number(fromRange.value || 0);
      let to = Number(toRange.value || max);

      if (from > to) {
        if (source === "from") {
          to = from;
          toRange.value = String(to);
        } else {
          from = to;
          fromRange.value = String(from);
        }
      }

      fromValue.value = String(from);
      toValue.value = String(to);
      if (fromLabel) fromLabel.textContent = formatPrice(from);
      if (toLabel) toLabel.textContent = formatPrice(to);

      if (fill) {
        fill.style.left = `${Math.max(0, Math.min(100, (from / max) * 100))}%`;
        fill.style.right = `${Math.max(0, Math.min(100, 100 - ((to / max) * 100)))}%`;
      }
    };

    fromRange.addEventListener("input", () => sync("from"));
    toRange.addEventListener("input", () => sync("to"));
    sync();
  }

  function wireRadiusRangeFilter(wrapper) {
    if (!wrapper || wrapper.dataset.radiusRangeBound === "true") return;

    const max = Number(wrapper.dataset.radiusMax || 200) || 200;
    const range = wrapper.querySelector("[data-radius-range]");
    const hiddenValue = wrapper.querySelector("[data-radius-value]");
    const label = wrapper.querySelector("[data-radius-label]");
    const fill = wrapper.querySelector("[data-radius-range-fill]");
    if (!range || !hiddenValue || !label) return;

    wrapper.dataset.radiusRangeBound = "true";

    const sync = () => {
      const value = Math.max(0, Math.min(max, Number(range.value || 0)));
      range.value = String(value);
      hiddenValue.value = value > 0 ? String(value) : "";
      label.textContent = value > 0 ? `${value} km` : "Sin limite";

      if (fill) {
        fill.style.left = "0%";
        fill.style.right = `${Math.max(0, Math.min(100, 100 - ((value / max) * 100)))}%`;
      }
    };

    range.addEventListener("input", sync);
    sync();
  }

  function buildRequiredFilterRow(field) {
    return `
      <label data-required-filter-row>
        <span>${escapeHtml(field.label || "")}</span>
        <input type="hidden" name="filterFieldId" value="${escapeAttribute(field.id)}" />
        ${buildRequiredFilterControl(field)}
      </label>
    `;
  }

  function buildRequiredFilterControl(field) {
    if (field.dataType === "lista" && Array.isArray(field.options) && field.options.length) {
      const options = field.options
        .map(option => `<option value="${escapeAttribute(option)}">${escapeHtml(option)}</option>`)
        .join("");
      return `<select name="filterValue"><option value="">Todos</option>${options}</select>`;
    }

    if (field.dataType === "booleano") {
      return `
        <select name="filterValue">
          <option value="">Todos</option>
          <option value="true">Si</option>
          <option value="false">No</option>
        </select>
      `;
    }

    const type = field.dataType === "numero" ? "number" : "text";
    const step = field.dataType === "numero" ? ` step="0.01"` : "";
    return `<input name="filterValue" type="${type}"${step} placeholder="Todos" />`;
  }

  function parseRequiredFilterFields(raw) {
    if (!raw) return [];

    try {
      const parsed = JSON.parse(raw);
      return Array.isArray(parsed)
        ? parsed.map(field => ({
          id: Number(field.id || 0),
          label: String(field.label || ""),
          internalName: String(field.internalName || ""),
          dataType: String(field.dataType || "texto").toLowerCase(),
          options: Array.isArray(field.options) ? field.options.map(option => String(option || "")).filter(Boolean) : []
        })).filter(field => (field.id > 0 || field.internalName === "operacion") && field.label)
        : [];
    } catch {
      return [];
    }
  }

  function buildMapGalleryPopup(marker) {
    const title = escapeHtml(marker.title || "");
    const code = escapeHtml(marker.code || "");
    const price = escapeHtml(marker.price || "");
    const operationLabel = escapeHtml(marker.operationLabel || "");
    const operationLetter = escapeHtml(String(marker.operationLabel || "").trim().charAt(0).toUpperCase());
    const priceTooltipLabel = buildPriceTooltipLabel(marker);
    const detailsUrl = escapeAttribute(marker.detailsUrl || "#");
    const publicationId = escapeAttribute(marker.id || "");
    const videoUrl = escapeAttribute(marker.videoUrl || "");
    const images = Array.isArray(marker.images) && marker.images.length
      ? marker.images
      : [marker.image || "/images/logo4.png"];
    const escapedImages = images.map(image => escapeAttribute(image || "/images/logo4.png"));
    const firstImage = escapedImages[0];
    const galleryTitle = escapeHtml(getGalleryDescription(marker));
    const mediaCount = escapedImages.length + (videoUrl ? 1 : 0);
    const navButtons = mediaCount > 1
      ? `
          <span class="gallery-nav gallery-nav-prev" data-direction="-1" data-gallery-nav="true" role="button" tabindex="0" aria-label="Foto anterior">&#8249;</span>
          <span class="gallery-nav gallery-nav-next" data-direction="1" data-gallery-nav="true" role="button" tabindex="0" aria-label="Foto siguiente">&#8250;</span>
        `
      : "";

        const priceCluster = `
          <span class="map-popup-price-cluster">
            <span class="map-popup-price gallery-badge gallery-tooltip-trigger gallery-tooltip-bottom" data-tooltip-label="${priceTooltipLabel}">${operationLabel && operationLetter ? `<strong class="gallery-operation-letter">${operationLetter}</strong>` : ""}${price}</span>
          </span>
        `;

    return `
      <article class="map-popup-card listing-card listing-card-compact">
        <a href="${detailsUrl}" class="card-image-wrap map-popup-image-wrap publication-preview-trigger" data-publication-id="${publicationId}" data-details-url="${buildPublicationApiDetailsUrl(publicationId)}" data-images="${escapedImages.join("|||")}" data-video-url="${videoUrl}" data-media-index="0">
          ${videoUrl
            ? `<video src="${videoUrl}" class="gallery-carousel-video" preload="metadata" muted playsinline></video><button type="button" class="gallery-play-toggle gallery-tooltip-trigger gallery-tooltip-top" data-gallery-play-toggle="true" aria-label="Reproducir video" data-tooltip-label="Reproducir video"></button><button type="button" class="gallery-audio-toggle gallery-tooltip-trigger gallery-tooltip-side" data-gallery-audio-toggle="true" aria-label="Activar audio" data-tooltip-label="Activar audio"><i class="fa-solid fa-volume-xmark" aria-hidden="true"></i></button>`
            : `<img src="${firstImage}" alt="${title}" class="gallery-carousel-image" />`}
          ${priceCluster}
          ${navButtons}
          <button type="button" class="gallery-action-button gallery-report-overlay gallery-tooltip-trigger gallery-tooltip-side report-trigger" data-publication-id="${publicationId}" data-publication-code="${code}" data-publication-title="${title}" data-tooltip-label="Denunciar" aria-label="Denunciar ${title}"><span class="gallery-report-letter" aria-hidden="true">D</span></button>
          <span class="gallery-title-overlay gallery-description-tooltip gallery-tooltip-trigger" data-tooltip-label="${buildDescriptionTooltipLabel(marker)}" tabindex="0"><span>${galleryTitle}</span></span>
        </a>
      </article>
    `;
  }

  function buildMapMarkerHoverCard(marker) {
    const title = escapeHtml(getGalleryDescription(marker));
    const price = escapeHtml(marker?.price || "");

    return `
      <div class="map-hover-card">
        <strong>${title || "Publicacion"}</strong>
        <span>${price || "Precio sin informar"}</span>
      </div>
    `;
  }

  function buildMapMarkerTapCard(marker) {
    const title = escapeHtml(getGalleryDescription(marker));
    const price = escapeHtml(marker?.price || "");
    const detailsUrl = escapeAttribute(buildPublicationApiDetailsUrl(marker?.id || ""));

    return `
      <div class="map-tap-card">
        <strong>${title || "Publicacion"}</strong>
        <span>${price || "Precio sin informar"}</span>
        <button type="button" class="primary-pill compact" data-map-open-preview="true" data-details-url="${detailsUrl}">
          Mostrar anuncio
        </button>
      </div>
    `;
  }

  function buildMapSelectionTextCard(marker) {
    const rawTitle = stripOpportunitySuffix(marker?.title || "");
    const title = escapeHtml(rawTitle);
    const price = escapeHtml(marker?.price || "");
    const operationLabel = escapeHtml(marker?.operationLabel || "");
    const operationLetter = escapeHtml(String(marker?.operationLabel || "").trim().charAt(0).toUpperCase());
    const priceTooltipLabel = buildPriceTooltipLabel(marker);
    const location = escapeHtml(marker?.locality || "Ubicación no informada");
    const shortDescription = escapeHtml(marker?.shortDescription || "Sin descripción breve.");
    const detailsUrl = escapeAttribute(marker?.detailsUrl || "#");
    const publicationId = escapeAttribute(marker?.id || "");
    const publicationCode = escapeAttribute(marker?.code || "");
    const isFavorite = Boolean(marker?.isFavorite);
    const suggestedListName = escapeAttribute(marker?.groupName || "Inmuebles");

    return `
      <article class="map-selection-text-card">
        <div class="map-selection-text-meta">
          <p class="map-selection-text-location">${location}</p>
        </div>
        <h3 class="map-selection-text-title">${title || "Publicación"}</h3>
        <p class="map-selection-text-description">${shortDescription}</p>
        <div class="map-selection-text-actions">
          <span class="map-selection-text-price gallery-tooltip-trigger gallery-tooltip-bottom" data-tooltip-label="${priceTooltipLabel}">
            <span>${operationLabel && operationLetter ? `<strong class="gallery-operation-letter">${operationLetter}</strong>` : ""}${price || "Precio sin informar"}</span>
          </span>
          <a href="${detailsUrl}" class="primary-pill compact map-selection-text-link publication-preview-trigger" data-publication-id="${publicationId}" data-details-url="${buildPublicationApiDetailsUrl(publicationId)}">
            Ver anuncio
          </a>
          <button type="button" class="favorite-toggle ghost-pill compact ${isFavorite ? "is-active" : ""}" data-favorite-toggle="true" data-publication-id="${publicationId}" data-publication-title="${title}" data-suggested-list-name="${suggestedListName}" aria-label="Añadir a mi lista de favoritos">
            ${renderFavoriteIcon(isFavorite)}
          </button>
          <button type="button" class="report-trigger map-selection-text-report gallery-tooltip-trigger gallery-tooltip-top" data-publication-id="${publicationId}" data-publication-code="${publicationCode}" data-publication-title="${title}" aria-label="Denunciar ${title}" data-tooltip-label="Denunciar">
            <span class="gallery-report-letter" aria-hidden="true">D</span>
          </button>
        </div>
      </article>
    `;
  }

  function escapeHtml(value) {
    return String(value)
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");
  }

  function createClientId() {
    const cryptoApi = globalThis.crypto;
    if (cryptoApi?.randomUUID) {
      return cryptoApi.randomUUID();
    }

    if (cryptoApi?.getRandomValues) {
      const bytes = new Uint8Array(16);
      cryptoApi.getRandomValues(bytes);
      return Array.from(bytes, value => value.toString(16).padStart(2, "0")).join("");
    }

    return `upload-${Date.now()}-${Math.random().toString(16).slice(2, 10)}`;
  }

  function supportsCanvasWebp() {
    try {
      const canvas = document.createElement("canvas");
      return canvas.toDataURL("image/webp").startsWith("data:image/webp");
    } catch {
      return false;
    }
  }

  async function loadImageElementFromFile(file) {
    const objectUrl = URL.createObjectURL(file);

    try {
      const image = await new Promise((resolve, reject) => {
        const element = new Image();
        element.onload = () => resolve(element);
        element.onerror = () => reject(new Error("No se pudo leer la imagen seleccionada."));
        element.src = objectUrl;
      });

      return image;
    } finally {
      URL.revokeObjectURL(objectUrl);
    }
  }

  async function readImageDimensions(file) {
    const image = await loadImageElementFromFile(file);
    return {
      width: Number(image.naturalWidth || image.width || 0),
      height: Number(image.naturalHeight || image.height || 0)
    };
  }

  async function readVideoMetadata(file) {
    const objectUrl = URL.createObjectURL(file);

    try {
      return await new Promise((resolve, reject) => {
        const video = document.createElement("video");
        video.preload = "metadata";
        video.muted = true;
        video.playsInline = true;
        video.onloadedmetadata = () => {
          resolve({
            width: Number(video.videoWidth || 0),
            height: Number(video.videoHeight || 0),
            duration: Number(video.duration || 0)
          });
        };
        video.onerror = () => reject(new Error("No pudimos procesar ese archivo de video."));
        video.src = objectUrl;
      });
    } finally {
      URL.revokeObjectURL(objectUrl);
    }
  }

  function getSupportedMediaRecorderMimeType() {
    if (typeof MediaRecorder === "undefined") {
      return "";
    }

    const candidates = [
      "video/webm;codecs=vp9,opus",
      "video/webm;codecs=vp8,opus",
      "video/webm"
    ];

    return candidates.find(type => MediaRecorder.isTypeSupported(type)) || "";
  }

  async function optimizeVideoForUpload(file, options = {}) {
    const mimeType = getSupportedMediaRecorderMimeType();
    if (!mimeType || typeof MediaRecorder === "undefined") {
      throw new Error("Este navegador no permite comprimir el video antes de subirlo.");
    }

    const sourceUrl = URL.createObjectURL(file);
    const video = document.createElement("video");
    video.src = sourceUrl;
    video.muted = true;
    video.playsInline = true;
    video.preload = "auto";

    try {
      await new Promise((resolve, reject) => {
        video.onloadedmetadata = () => resolve();
        video.onerror = () => reject(new Error("No pudimos procesar ese archivo de video."));
      });

      if (video.videoWidth <= 0 || video.videoHeight <= 0) {
        throw new Error("No pudimos leer el tamaño del video.");
      }

      const maxWidth = Number(options.maxWidth || 1080);
      const maxHeight = Number(options.maxHeight || 1920);
      const maxSideScale = Math.min(1, maxWidth / video.videoWidth, maxHeight / video.videoHeight);
      const targetWidth = Math.max(2, Math.floor((video.videoWidth * maxSideScale) / 2) * 2);
      const targetHeight = Math.max(2, Math.floor((video.videoHeight * maxSideScale) / 2) * 2);

      const canvas = document.createElement("canvas");
      canvas.width = targetWidth;
      canvas.height = targetHeight;
      const context = canvas.getContext("2d", { alpha: false });
      if (!context || typeof canvas.captureStream !== "function") {
        throw new Error("Este navegador no permite redimensionar el video antes de subirlo.");
      }

      const renderedStream = canvas.captureStream(30);
      const sourceStream = typeof video.captureStream === "function"
        ? video.captureStream()
        : typeof video.mozCaptureStream === "function"
          ? video.mozCaptureStream()
          : null;
      if (!sourceStream) {
        throw new Error("Este navegador no permite capturar el video para comprimirlo.");
      }

      sourceStream.getAudioTracks().forEach(track => renderedStream.addTrack(track));

      const recorder = new MediaRecorder(renderedStream, {
        mimeType,
        videoBitsPerSecond: Number(options.videoBitsPerSecond || 6_500_000),
        audioBitsPerSecond: Number(options.audioBitsPerSecond || 128_000)
      });

      const chunks = [];
      recorder.ondataavailable = event => {
        if (event.data && event.data.size > 0) {
          chunks.push(event.data);
        }
      };

      await new Promise((resolve, reject) => {
        let animationFrameId = 0;

        const paintFrame = () => {
          if (!video.paused && !video.ended) {
            context.drawImage(video, 0, 0, targetWidth, targetHeight);
            animationFrameId = requestAnimationFrame(paintFrame);
          }
        };

        recorder.onerror = () => reject(new Error("No pudimos comprimir el video antes de subirlo."));
        recorder.onstop = () => {
          cancelAnimationFrame(animationFrameId);
          resolve();
        };
        video.onended = () => recorder.stop();

        recorder.start(1000);
        video.play().then(() => {
          paintFrame();
        }).catch(reject);
      });

      const blob = new Blob(chunks, { type: mimeType });
      if (!blob.size || blob.size >= file.size) {
        throw new Error("La compresion previa no mejoro el tamaño del video.");
      }

      return new File([blob], `${String(file.name || "video").replace(/\.[^.]+$/, "")}.webm`, {
        type: "video/webm",
        lastModified: file.lastModified || Date.now()
      });
    } finally {
      video.pause();
      URL.revokeObjectURL(sourceUrl);
    }
  }

  async function optimizeImageForUpload(file, options = {}) {
    const maxSide = Number(options.maxSide || 2000);
    const preferredQuality = Number(options.quality || 0.9);
    const preferredType = supportsCanvasWebp() ? "image/webp" : "image/jpeg";
    const originalType = String(file?.type || "").toLowerCase();

    if (!(file instanceof File)) {
      throw new Error("El archivo seleccionado no es valido.");
    }

    if (!originalType.startsWith("image/") || originalType === "image/gif") {
      return file;
    }

    const image = await loadImageElementFromFile(file);
    const width = Number(image.naturalWidth || image.width || 0);
    const height = Number(image.naturalHeight || image.height || 0);
    if (!width || !height) {
      throw new Error("No se pudo obtener el tamaño de la imagen.");
    }

    const largestSide = Math.max(width, height);
    if (largestSide <= maxSide) {
      return file;
    }

    const scale = Math.min(1, maxSide / largestSide);
    const targetWidth = Math.max(1, Math.round(width * scale));
    const targetHeight = Math.max(1, Math.round(height * scale));
    const canvas = document.createElement("canvas");
    canvas.width = targetWidth;
    canvas.height = targetHeight;

    const context = canvas.getContext("2d", { alpha: false });
    if (!context) {
      throw new Error("Este navegador no permite procesar la imagen antes de subirla.");
    }

    context.drawImage(image, 0, 0, targetWidth, targetHeight);

    const blob = await new Promise((resolve, reject) => {
      canvas.toBlob(result => {
        if (result) {
          resolve(result);
          return;
        }

        reject(new Error("Este navegador no pudo comprimir la imagen antes de subirla."));
      }, preferredType, preferredQuality);
    });

    if (!(blob instanceof Blob)) {
      throw new Error("No se pudo preparar la imagen para la subida.");
    }

    if (blob.size >= file.size && targetWidth === width && targetHeight === height) {
      return file;
    }

    const originalName = String(file.name || "imagen");
    const sanitizedBaseName = originalName.replace(/\.[^.]+$/, "") || "imagen";
    const extension = preferredType === "image/webp" ? ".webp" : ".jpg";
    return new File([blob], `${sanitizedBaseName}${extension}`, {
      type: preferredType,
      lastModified: file.lastModified || Date.now()
    });
  }

  function uploadImagesRequest(formData) {
    return new Promise((resolve, reject) => {
      const request = new XMLHttpRequest();
      request.open("POST", "/api/content/upload-images", true);
      request.setRequestHeader("X-Requested-With", "fetch");

      request.onload = () => {
        let payload = {};
        try {
          payload = request.responseText ? JSON.parse(request.responseText) : {};
        } catch {
          payload = {};
        }

        resolve({
          ok: request.status >= 200 && request.status < 300,
          status: request.status,
          message: payload?.message,
          urls: payload?.urls
        });
      };

      request.onerror = () => {
        reject(new Error("Fallo la conexion al subir imagenes desde este navegador."));
      };

      request.ontimeout = () => {
        reject(new Error("La subida de imagenes agoto el tiempo de espera."));
      };

      request.send(formData);
    });
  }

  function uploadVideoRequest(formData) {
    return new Promise((resolve, reject) => {
      const request = new XMLHttpRequest();
      request.open("POST", "/api/content/upload-video", true);
      request.setRequestHeader("X-Requested-With", "fetch");

      request.onload = () => {
        let payload = {};
        try {
          payload = request.responseText ? JSON.parse(request.responseText) : {};
        } catch {
          payload = {};
        }

        resolve({
          ok: request.status >= 200 && request.status < 300,
          status: request.status,
          message: payload?.message,
          url: payload?.url
        });
      };

      request.onerror = () => {
        reject(new Error("Fallo la conexion al subir el video desde este navegador."));
      };

      request.ontimeout = () => {
        reject(new Error("La subida del video agoto el tiempo de espera."));
      };

      request.send(formData);
    });
  }

  async function deleteUploadedMediaRequest(urls) {
    const mediaUrls = Array.isArray(urls)
      ? urls.map(url => String(url || "").trim()).filter(Boolean)
      : [];
    if (!mediaUrls.length) {
      return { ok: true, deleted: 0 };
    }

    const response = await fetch("/api/content/delete-uploaded-media", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-Requested-With": "fetch"
      },
      body: JSON.stringify({ urls: mediaUrls })
    });

    const payload = await response.json().catch(() => ({}));
    return {
      ok: response.ok,
      status: response.status,
      deleted: payload?.deleted || 0,
      message: payload?.message || ""
    };
  }

  function escapeAttribute(value) {
    return escapeHtml(value);
  }

  function buildPriceTooltipLabel(item) {
    const operation = String(item?.operationLabel || "").trim();
    const category = String(item?.categoryLabel || "").trim();
    const price = String(item?.priceTooltip || item?.price || "").trim();

    return escapeAttribute([operation, category, price].filter(Boolean).join(" · "));
  }

  function wirePublicationPreviewModal() {
    if (document.body.dataset.previewModalBound === "true") return;

    document.body.dataset.previewModalBound = "true";
    const modalElement = document.getElementById("publicationPreviewModal");
    const body = document.getElementById("publicationPreviewBody");
    const title = document.getElementById("publicationPreviewTitle");
    if (!modalElement || !body || !title) return;
    let disposePreviewMapObserver = null;

    const closePreview = () => {
      disposePreviewMapObserver?.();
      disposePreviewMapObserver = null;
      modalElement.hidden = true;
      modalElement.classList.remove("is-open");
      syncPreviewOpenState();
      title.textContent = "Detalle de publicaciÃ³n";
      body.innerHTML = "";
    };

    modalElement.addEventListener("click", event => {
      const closeTrigger = event.target.closest("[data-preview-close='true']");
      if (!closeTrigger) return;
      event.preventDefault();
      event.stopPropagation();
      closePreview();
    });

    document.addEventListener("keydown", event => {
      if (event.key === "Escape" && modalElement.classList.contains("is-open")) {
        closePreview();
      }
    });

    openPublicationPreview = async detailsUrl => {
      if (!detailsUrl) return;
      detailDebugLog("openPublicationPreview:called", { detailsUrl });
      clearPersistedSystemLoading();
      forceHideSystemLoading();

      title.textContent = "Cargando publicaciÃƒÂ³n";
      body.innerHTML = `<div class="preview-modal-loading">Cargando detalle...</div>`;

      let response;
      try {
        response = await detailDebugMeasure("openPublicationPreview:fetch", () => fetch(detailsUrl, {
          headers: { "X-Requested-With": "fetch" }
        }), { detailsUrl });
      } catch {
        title.textContent = "No se pudo cargar";
        body.innerHTML = `<section class="empty-state"><h2>Error al abrir la publicacion</h2><p>Revisa la conexion e intenta nuevamente.</p></section>`;
        modalElement.hidden = false;
        modalElement.classList.add("is-open");
        document.body.classList.add("preview-open");
        return;
      }

      if (!response.ok) {
        title.textContent = "No se pudo cargar";
        body.innerHTML = `<section class="empty-state"><h2>Error al abrir la publicacion</h2><p>Intenta nuevamente en unos segundos.</p></section>`;
        modalElement.hidden = false;
        modalElement.classList.add("is-open");
        document.body.classList.add("preview-open");
        return;
      }

      const html = await detailDebugMeasure("openPublicationPreview:responseText", () => response.text(), {
        detailsUrl
      });
      body.innerHTML = html;
      detailDebugLog("openPublicationPreview:htmlInjected", {
        detailsUrl,
        htmlLength: html.length
      });
      wireDetailGalleryLayout();
      const publicationTitle = body.querySelector(".detail-hero h1")?.textContent?.trim();
      title.textContent = stripOpportunitySuffix(publicationTitle) || "Detalle de publicacion";
      modalElement.hidden = false;
      modalElement.classList.add("is-open");
      document.body.classList.add("preview-open");
      body.scrollTop = 0;
      detailDebugLog("openPublicationPreview:modalShown", { detailsUrl });
      disposePreviewMapObserver?.();
      disposePreviewMapObserver = null;
      const publicationMap = body.querySelector("[data-publication-map]");
      detailDebugLog("openPublicationPreview:mapFound", {
        detailsUrl,
        hasPublicationMap: Boolean(publicationMap)
      });
      if (publicationMap) {
        disposePreviewMapObserver = schedulePublicationMapInitialization(publicationMap, { scrollRoot: body });
      }
    };

    document.addEventListener("click", async event => {
      const mapPreviewTrigger = event.target.closest("[data-map-open-preview='true']");
      if (mapPreviewTrigger) {
        const pageUrl = getPublicationPageUrl(mapPreviewTrigger);
        if (getPublicationOpenMode() === "page" && pageUrl) {
          event.preventDefault();
          event.stopPropagation();
          window.open(pageUrl, "_blank", "noopener");
          return;
        }

        event.preventDefault();
        event.stopPropagation();
        const detailsUrl = mapPreviewTrigger.getAttribute("data-details-url");
        detailDebugLog("previewTrigger:mapButtonClick", { detailsUrl });
        await openPublicationPreview(detailsUrl);
        return;
      }

      const trigger = event.target.closest(".publication-preview-trigger");
      if (!trigger) return;
      if (event.target.closest(".gallery-nav") || event.target.closest(".report-trigger") || event.target.closest("[data-gallery-play-toggle='true']") || event.target.closest("[data-gallery-audio-toggle='true']") || event.target.closest("[data-favorite-toggle='true']") || event.target.closest("[data-like-toggle='true']") || event.target.closest("[data-gallery-menu-toggle='true']") || event.target.closest("[data-gallery-menu]")) {
        event.preventDefault();
        return;
      }

      event.preventDefault();

      const detailsUrl = trigger.getAttribute("data-details-url") || trigger.getAttribute("href");
      const pageUrl = getPublicationPageUrl(trigger);
      if (getPublicationOpenMode() === "page" && pageUrl) {
        window.open(pageUrl, "_blank", "noopener");
        return;
      }

      detailDebugLog("previewTrigger:cardClick", { detailsUrl });
      await openPublicationPreview(detailsUrl);
      return;

      title.textContent = "Cargando publicaciÃ³n";
      body.innerHTML = `<div class="preview-modal-loading">Cargando detalleâ€¦</div>`;

      let response;
      try {
        response = await fetch(detailsUrl, {
          headers: { "X-Requested-With": "fetch" }
        });
      } catch {
        title.textContent = "No se pudo cargar";
        body.innerHTML = `<section class="empty-state"><h2>Error al abrir la publicaciÃ³n</h2><p>RevisÃ¡ la conexiÃ³n e intentÃ¡ nuevamente.</p></section>`;
        modalElement.hidden = false;
        modalElement.classList.add("is-open");
        document.body.classList.add("preview-open");
        return;
      }

      if (!response.ok) {
        title.textContent = "No se pudo cargar";
        body.innerHTML = `<section class="empty-state"><h2>Error al abrir la publicaciÃ³n</h2><p>IntentÃ¡ nuevamente en unos segundos.</p></section>`;
        modalElement.hidden = false;
        modalElement.classList.add("is-open");
        document.body.classList.add("preview-open");
        return;
      }

      body.innerHTML = await response.text();
      const publicationTitle = body.querySelector(".detail-hero h1")?.textContent?.trim();
      title.textContent = stripOpportunitySuffix(publicationTitle) || "Detalle de publicaciÃ³n";
      modalElement.hidden = false;
      modalElement.classList.add("is-open");
      document.body.classList.add("preview-open");
      await initContentMaps();
    });
  }

  function wireDetailMediaOverlay() {
    if (document.body.dataset.detailMediaOverlayBound === "true") return;

    document.body.dataset.detailMediaOverlayBound = "true";
    const overlayState = {
      items: [],
      index: 0
    };

    const closeOverlay = overlay => {
      if (!overlay) return;
      const body = overlay.querySelector("[data-detail-media-body]");
      if (body) {
        body.innerHTML = "";
      }
      overlayState.items = [];
      overlayState.index = 0;
      overlay.hidden = true;
      overlay.classList.remove("is-open");
      syncPreviewOpenState();
    };

    const renderOverlayItem = (overlay, index) => {
      const body = overlay?.querySelector("[data-detail-media-body]");
      const caption = overlay?.querySelector("[data-detail-media-caption]");
      const prevButton = overlay?.querySelector("[data-detail-media-nav='prev']");
      const nextButton = overlay?.querySelector("[data-detail-media-nav='next']");
      const item = overlayState.items[index];
      if (!overlay || !body || !item) return;

      overlayState.index = index;
      if (index >= MEDIA_PRELOAD_CONFIG.galleryInitialItems - 1) {
        const activeGallery = overlay?.closest("#publicationPreviewBody")?.querySelector(".detail-gallery")
          || document.querySelector(".detail-gallery");
        mediaPreloadService.warmRemainingDetailGallery(activeGallery);
      }
      body.innerHTML = item.type === "video"
        ? `<video src="${escapeAttribute(item.src)}" controls autoplay playsinline preload="metadata"></video>`
        : `<img src="${escapeAttribute(item.src)}" alt="${escapeAttribute(item.title)}" />`;
      if (caption) {
        caption.textContent = `${item.type === "video" ? "Video" : "Imagen"} ${index + 1} de ${overlayState.items.length}`;
      }
      if (prevButton) {
        prevButton.hidden = overlayState.items.length <= 1;
      }
      if (nextButton) {
        nextButton.hidden = overlayState.items.length <= 1;
      }
    };

    const syncDetailVideoPlayButton = frame => {
      const video = frame?.querySelector("video");
      const button = frame?.querySelector("[data-detail-video-play='true']");
      if (!video || !button) return;

      const isPlaying = !video.paused && !video.ended;
      button.innerHTML = isPlaying
        ? `<i class="fa-solid fa-pause" aria-hidden="true"></i>`
        : `<i class="fa-solid fa-play" aria-hidden="true"></i>`;
      button.setAttribute("aria-label", isPlaying ? "Pausar video" : "Reproducir video");
      button.setAttribute("title", isPlaying ? "Pausar video" : "Reproducir video");
    };

    document.addEventListener("click", event => {
      const playTrigger = event.target.closest("[data-detail-video-play='true']");
      if (playTrigger) {
        const frame = playTrigger.closest("[data-detail-media-item='true']");
        const video = frame?.querySelector("video");
        if (!video) return;
        event.preventDefault();
        event.stopPropagation();
        if (video.paused || video.ended) {
          video.play?.().catch(() => {});
        } else {
          video.pause?.();
        }
        syncDetailVideoPlayButton(frame);
        return;
      }

      const closeTrigger = event.target.closest("[data-detail-media-close='true']");
      if (closeTrigger) {
        const overlay = closeTrigger.closest("[data-detail-media-overlay]");
        event.preventDefault();
        event.stopPropagation();
        closeOverlay(overlay);
        return;
      }

      const navTrigger = event.target.closest("[data-detail-media-nav]");
      if (navTrigger) {
        const overlay = navTrigger.closest("[data-detail-media-overlay]");
        if (!overlayState.items.length) return;
        event.preventDefault();
        event.stopPropagation();
        const direction = navTrigger.getAttribute("data-detail-media-nav") === "next" ? 1 : -1;
        const nextIndex = (overlayState.index + direction + overlayState.items.length) % overlayState.items.length;
        renderOverlayItem(overlay, nextIndex);
        return;
      }

      const trigger = event.target.closest("[data-detail-media-item='true']");
      if (!trigger) return;
      if (!trigger.closest(".detail-gallery")) return;

      event.preventDefault();

      const detailGrid = trigger.closest(".detail-grid");
      const siblingOverlay = detailGrid?.nextElementSibling?.matches?.("[data-detail-media-overlay]")
        ? detailGrid.nextElementSibling
        : null;
      const overlay = siblingOverlay
        || trigger.closest("#publicationPreviewBody")?.querySelector("[data-detail-media-overlay]")
        || document.querySelector("[data-detail-media-overlay]");
      const mediaType = trigger.getAttribute("data-detail-media-type") || "image";
      const mediaSrc = trigger.getAttribute("data-detail-media-src") || trigger.getAttribute("src") || "";
      const mediaTitle = stripOpportunitySuffix(trigger.getAttribute("data-detail-media-title") || trigger.getAttribute("alt") || "Vista ampliada");

      if (!overlay || !mediaSrc) return;

      if (mediaType === "video") {
        const frame = trigger.closest(".detail-gallery-video-frame");
        const video = frame?.querySelector("video");
        if (video && !video.paused) {
          video.pause?.();
          syncDetailVideoPlayButton(frame);
        }
      }

      const gallery = trigger.closest(".detail-gallery");
      const items = Array.from(gallery?.querySelectorAll("[data-detail-media-item='true']") || []).map(el => ({
        type: el.getAttribute("data-detail-media-type") || "image",
        src: el.getAttribute("data-detail-media-src") || el.getAttribute("src") || "",
        title: stripOpportunitySuffix(el.getAttribute("data-detail-media-title") || el.getAttribute("alt") || mediaTitle || "Vista ampliada")
      })).filter(item => item.src);

      const triggerIndex = Number(trigger.getAttribute("data-detail-media-index") || 0);
      if (triggerIndex >= MEDIA_PRELOAD_CONFIG.galleryInitialItems - 1) {
        mediaPreloadService.warmRemainingDetailGallery(gallery);
      }

      overlayState.items = items.length ? items : [{ type: mediaType, src: mediaSrc, title: mediaTitle }];
      overlayState.index = Math.max(0, overlayState.items.findIndex(item => item.src === mediaSrc));
      if (overlayState.index < 0) overlayState.index = 0;

      renderOverlayItem(overlay, overlayState.index);

      overlay.hidden = false;
      overlay.classList.add("is-open");
      document.body.classList.add("preview-open");
    });

    document.addEventListener("play", event => {
      const video = event.target.closest?.(".detail-gallery-video-frame video");
      if (!video) return;
      syncDetailVideoPlayButton(video.closest(".detail-gallery-video-frame"));
    }, true);

    document.addEventListener("pause", event => {
      const video = event.target.closest?.(".detail-gallery-video-frame video");
      if (!video) return;
      syncDetailVideoPlayButton(video.closest(".detail-gallery-video-frame"));
    }, true);

    document.addEventListener("ended", event => {
      const video = event.target.closest?.(".detail-gallery-video-frame video");
      if (!video) return;
      syncDetailVideoPlayButton(video.closest(".detail-gallery-video-frame"));
    }, true);

    document.addEventListener("keydown", event => {
      if (event.key === "ArrowLeft" && overlayState.items.length) {
        const overlay = document.querySelector("[data-detail-media-overlay].is-open");
        if (overlay) {
          event.preventDefault();
          renderOverlayItem(overlay, (overlayState.index - 1 + overlayState.items.length) % overlayState.items.length);
        }
        return;
      }

      if (event.key === "ArrowRight" && overlayState.items.length) {
        const overlay = document.querySelector("[data-detail-media-overlay].is-open");
        if (overlay) {
          event.preventDefault();
          renderOverlayItem(overlay, (overlayState.index + 1) % overlayState.items.length);
        }
        return;
      }

      if (event.key !== "Escape") return;
      document.querySelectorAll("[data-detail-media-overlay].is-open").forEach(overlay => {
        closeOverlay(overlay);
      });
    });
  }

  function wireCreateForm() {
    const form = document.getElementById("createPublicationForm");
    if (!form || form.dataset.bound === "true") return;

    form.dataset.bound = "true";
    wireCreateLivePreview(form);
    syncCreateTitle(form);
    syncCreateDescriptionExamples(form);
    wireCreateDescriptionHyperlinkGuards(form);
    wireCreateSectionToggles(form);

    const groupInput = form.querySelector('[name="group"]');
    const categorySelect = form.querySelector("[data-category-select]");
    const operationSelect = form.querySelector("[data-create-operation-select]");
    const localityInput = form.querySelector('input[name="locality"]');
    const addressInput = form.querySelector('input[name="address"]');
    syncCreatePricePeriodHint(form);
    [categorySelect, localityInput, addressInput].forEach(input => {
      input?.addEventListener("input", () => syncCreateTitle(form));
      input?.addEventListener("change", () => syncCreateTitle(form));
    });

    categorySelect?.addEventListener("change", async () => {
      categorySelect.dataset.selectedCategoryId = categorySelect.value || "";
      await reloadDynamicCategoryFields(form);
      enhanceSearchableSelects(form);
      syncCreateTitle(form);
      syncCreateDescriptionExamples(form);
    });

    groupInput?.addEventListener("change", async () => {
      await reloadCategoryOptions(form);
      await reloadDynamicCategoryFields(form);
      enhanceSearchableSelects(form);
      syncCreateTitle(form);
      syncCreateDescriptionExamples(form);
    });

    operationSelect?.addEventListener("change", () => {
      operationSelect.dataset.selectedOperation = operationSelect.value || "";
      syncCreatePricePeriodHint(form);
    });

    const uploader = wireCreateImageUploader(form);
    const videoUploader = wireCreateVideoUploader(form);

    form.addEventListener("submit", async event => {
      event.preventDefault();
      clearCreateFormErrors(form);

      const hyperlinkErrors = validateCreateDescriptionHyperlinks(form);
      if (hyperlinkErrors.length) {
        renderCreateFormErrors(form, hyperlinkErrors);
        focusFirstCreateError(form, hyperlinkErrors);
        return;
      }

      await uploader.waitForUploads();
      await videoUploader.waitForUploads();
      const payload = serializeCreateForm(form);
      const feedback = document.getElementById("create-feedback");

      if (uploader.hasPendingFiles() || videoUploader.hasPendingFiles()) {
        if (feedback) {
          feedback.innerHTML = `<div class="status-banner warning">Espera a que terminen las subidas de imagenes y video.</div>`;
        }
        return;
      }

      const submitEndpoint = form.dataset.submitEndpoint || "/api/content/create";
      const response = await fetch(submitEndpoint, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "X-Requested-With": "fetch"
        },
        body: JSON.stringify(payload)
      });

      const result = await readJsonResponse(response);
      if (!response.ok) {
        console.log("create publication failed", {
          status: response.status,
          result,
          payload
        });
        const fieldErrors = normalizeCreateFieldErrors(result.errors);
        renderCreateFormErrors(form, fieldErrors);

        if (feedback) {
          const errorMessage = result.message || "No se pudo crear la publicacion.";
          feedback.innerHTML = `<div class="status-banner warning">${escapeHtml(errorMessage)}</div>`;
        }

        focusFirstCreateError(form, fieldErrors);
        return;
      }

      if (feedback) {
        feedback.innerHTML = "";
      }

      uploader.markPersisted();
      if (result.redirectUrl) {
        window.location.href = result.redirectUrl;
      }
    });

    reloadCategoryOptions(form, true)
      .then(() => reloadDynamicCategoryFields(form))
      .then(() => {
        enhanceSearchableSelects(form);
        syncCreateDescriptionExamples(form);
      });
  }

  function wireCreateSectionToggles(form) {
    const toggles = form.querySelectorAll("[data-section-toggle]");
    const noLocationInput = form.querySelector("[data-create-no-location]");
    toggles.forEach(toggle => {
      const section = toggle.closest("section");
      const sectionKey = toggle.dataset.sectionToggle || "";
      const heading = section?.querySelector("[data-section-heading]");
      const body = section?.querySelector(`[data-section-body="${sectionKey}"]`);
      const technicalPanel = form.querySelector("[data-technical-panel]");
      if (!body) return;

      const sync = () => {
        if (heading) {
          heading.hidden = false;
          heading.querySelectorAll(":scope > *").forEach(node => {
            node.hidden = false;
          });
        }
        body.hidden = !toggle.checked;
        if (sectionKey === "location" && noLocationInput) {
          noLocationInput.checked = !toggle.checked;
          noLocationInput.dispatchEvent(new Event("change", { bubbles: true }));
          if (toggle.checked) {
            const mapElement = body.querySelector("[data-create-map]");
            requestCreateMapResize(mapElement);
          }
        }
        if (sectionKey === "technical") {
          syncTechnicalCategoryNotice(form);
        }
      };

      toggle.addEventListener("change", sync);
      sync();
    });
  }

  function syncCreateDescriptionExamples(form) {
    const shortNode = form.querySelector('[data-description-example="short"]');
    const longNode = form.querySelector('[data-description-example="long"]');
    if (!shortNode && !longNode) return;

    const categorySelect = form.querySelector("[data-category-select]");
    const groupSelect = form.querySelector('[name="group"]');
    const categoryName = categorySelect?.selectedOptions?.[0]?.textContent?.trim() || "";
    const groupName = groupSelect?.selectedOptions?.[0]?.textContent?.trim() || "";
    const examples = getCreateDescriptionExamples(categoryName, groupName);

    if (shortNode) {
      shortNode.textContent = `Ejemplo: ${examples.short}`;
    }
    if (longNode) {
      longNode.textContent = `Ejemplo: ${examples.long}`;
    }
  }

  function getCreateDescriptionExamples(categoryName, groupName) {
    const text = `${categoryName} ${groupName}`.toLocaleLowerCase("es");

    if (text.includes("departamento") || text.includes("casa") || text.includes("ph")) {
      return {
        short: "Depto de 2 ambientes con balcón y luz en cada rincón.",
        long: "Departamento de 2 ambientes con balcón, cocina equipada y living lleno de luz. Un espacio cómodo para disfrutar todos los días, cerca de comercios y transporte."
      };
    }

    if (text.includes("terreno") || text.includes("campo") || text.includes("quinta") || text.includes("inmueble")) {
      return {
        short: "Amplio terreno con mucho verde, ideal para construir tu casa",
        long: "Amplio terreno rodeado de verde, en un entorno tranquilo y con buen acceso. Espacio para construir tu casa, sumar un jardín y disfrutar de la vida al aire libre."
      };
    }

    if (text.includes("auto") || text.includes("camioneta") || text.includes("utilitario") || text.includes("moto") || text.includes("rodado")) {
      return {
        short: "Toyota Hilux SRV 2020, confort y fuerza para cada camino.",
        long: "Toyota Hilux SRV 2020 con interior amplio y cómodo, ideal para trabajar y salir de viaje. De uso particular, con mantenimiento al día y espacio para llevar todo lo que necesitás."
      };
    }

    if (text.includes("celular") || text.includes("computacion") || text.includes("consola") || text.includes("camara") || text.includes("electronica")) {
      return {
        short: "iPhone 13 de 128 GB, gran cámara y diseño que enamora.",
        long: "iPhone 13 de 128 GB, con una gran cámara para guardar tus mejores momentos y espacio para tus fotos y apps. Muy cuidado y con funda incluida."
      };
    }

    if (text.includes("ropa") || text.includes("indumentaria") || text.includes("calzado") || text.includes("moda")) {
      return {
        short: "Zapatillas Nike talle 42, comodidad y estilo para cada día.",
        long: "Zapatillas Nike talle 42, livianas y cómodas para acompañarte todos los días. Un diseño fácil de combinar, con poco uso y muy bien cuidadas."
      };
    }

    if (text.includes("lancha") || text.includes("velero") || text.includes("nautic") || text.includes("embarcacion")) {
      return {
        short: "Lancha Bermuda 180 con Yamaha, ideal para disfrutar del río.",
        long: "Lancha Bermuda 180 con motor Yamaha y cómodos asientos para compartir paseos por el río. Ideal para salir a pescar o disfrutar una tarde en el agua con amigos."
      };
    }

    if (text.includes("agro") || text.includes("maquina") || text.includes("insumo") || text.includes("animal") || text.includes("rural")) {
      return {
        short: "Tractor John Deere 5075E, fuerza para trabajar tu campo.",
        long: "Tractor John Deere 5075E, una herramienta versátil para las tareas de tu campo. Bien cuidado, con mantenimiento al día y listo para acompañar la próxima temporada."
      };
    }

    return {
      short: "Mesa de madera maciza de 1,60 m, calidez para tu comedor.",
      long: "Mesa de comedor de 1,60 m en madera maciza, con vetas naturales y una terminación cálida. Ideal para compartir comidas y sumar un detalle especial a tu casa."
    };
  }

  function syncTechnicalCategoryNotice(form) {
    const categorySelect = form.querySelector("[data-category-select]");
    const optionalEmptyNode = form.querySelector("[data-dynamic-optional-fields-empty]");
    const technicalToggle = form.querySelector('[data-section-toggle="technical"]');
    if (!categorySelect || !optionalEmptyNode) return;

    const hasCategory = String(categorySelect.value || "").trim().length > 0;
    const isTryingTechnicalSection = Boolean(technicalToggle?.checked);
    const isMissingCategoryMessage = optionalEmptyNode.textContent.trim() === "Seleccioná una categoría para cargar la ficha técnica opcional.";
    optionalEmptyNode.classList.toggle("is-error", !hasCategory && isTryingTechnicalSection && isMissingCategoryMessage);
  }

  function wireCreateDescriptionHyperlinkGuards(form) {
    const shortInput = form.querySelector('input[name="shortDescription"]');
    const counter = form.querySelector("[data-short-description-count]");
    if (shortInput && counter && shortInput.dataset.counterBound !== "true") {
      shortInput.dataset.counterBound = "true";
      const updateCounter = () => {
        const length = shortInput.value.length;
        counter.textContent = `${length}/60 caracteres${length > 60 ? ". Acortá la descripción para guardar el anuncio." : ""}`;
        shortInput.setCustomValidity(length > 60 ? "La descripción corta debe tener como máximo 60 caracteres, incluidos los espacios." : "");
      };
      shortInput.addEventListener("input", updateCounter);
      updateCounter();
    }
    form.querySelectorAll('input[name="shortDescription"], textarea[name="longDescription"]').forEach(input => {
      if (input.dataset.hyperlinkGuardBound === "true") return;
      input.dataset.hyperlinkGuardBound = "true";

      input.addEventListener("paste", event => {
        const pastedText = event.clipboardData?.getData("text") || "";
        if (!containsHyperlink(pastedText)) return;

        event.preventDefault();
        const field = input.name === "shortDescription" ? "shortDescription" : "longDescription";
        renderCreateFormErrors(form, [{
          field,
          message: "No se permiten hipervinculos en la descripcion."
        }]);
      });

      input.addEventListener("input", () => {
        if (containsHyperlink(input.value)) return;
        const container = input.closest("[data-field-container]");
        const errorNode = form.querySelector(`[data-field-error="${input.name}"]`);
        if (errorNode?.textContent === "No se permiten hipervinculos en la descripcion.") {
          errorNode.textContent = "";
        }
        if (!container?.querySelector(".field-error:not(:empty)")) {
          container?.classList.remove("field-invalid");
          input.classList.remove("input-invalid");
        }
      });
    });
  }

  function validateCreateDescriptionHyperlinks(form) {
    const errors = ["shortDescription", "longDescription"]
      .map(field => ({
        field,
        message: containsHyperlink(form.querySelector(`[name="${field}"]`)?.value || "")
          ? "No se permiten hipervinculos en la descripcion."
          : ""
      }))
      .filter(error => error.message);
    if ((form.querySelector('[name="shortDescription"]')?.value || "").length > 60) {
      errors.push({ field: "shortDescription", message: "La descripción corta debe tener como máximo 60 caracteres, incluidos los espacios." });
    }
    return errors;
  }

  function containsHyperlink(value) {
    return /(https?:\/\/|ftp:\/\/|mailto:|www\.|\b[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.(?:com\.ar|net\.ar|org\.ar|com|net|org|info|io|app|co|uy|py|br|cl|es|dev|site|online|store|shop|me|ly)\b)/i.test(String(value || ""));
  }

  function normalizeCreateFieldErrors(errors) {
    if (!errors) return [];

    if (Array.isArray(errors)) {
      return errors
        .map(item => ({
          field: typeof item?.field === "string" ? item.field.trim() : "",
          message: typeof item?.message === "string" ? item.message.trim() : ""
        }))
        .filter(item => item.field && item.message);
    }

    if (typeof errors === "object") {
      return Object.entries(errors)
        .flatMap(([field, value]) => {
          const normalizedField = normalizeCreateFieldName(field);
          const messages = Array.isArray(value) ? value : [value];

          return messages
            .map(message => ({
              field: normalizedField,
              message: typeof message === "string" ? message.trim() : String(message ?? "").trim()
            }))
            .filter(item => item.field && item.message);
        });
    }

    return [];
  }

  function normalizeCreateFieldName(field) {
    const raw = String(field || "").trim();
    if (!raw) return "";

    const bare = raw
      .replace(/^\$\./, "")
      .split(".")
      .pop()
      .trim();

    const map = {
      group: "group",
      categoryid: "category",
      category: "category",
      price: "price",
      operation: "operation",
      currency: "currency",
      locality: "locationSearch",
      latitude: "locationSearch",
      longitude: "locationSearch",
      shortdescription: "shortDescription",
      longdescription: "longDescription",
      imagescsv: "imagesCsv",
      videourl: "videoUrl"
    };

    const mapped = map[bare.toLowerCase()];
    if (mapped) return mapped;
    if (bare.length === 1) return bare.toLowerCase();

    return bare[0].toLowerCase() + bare.slice(1);
  }

  function stripOpportunitySuffix(value) {
    return String(value || "").split(" - oportunidad")[0].trim();
  }

  async function readJsonResponse(response) {
    const text = await response.text();
    if (!text) {
      return {};
    }

    try {
      return JSON.parse(text);
    } catch {
      return { message: text };
    }
  }

  function clearCreateFormErrors(form) {
    form.querySelectorAll("[data-field-container]").forEach(node => {
      node.classList.remove("field-invalid");
    });

    form.querySelectorAll(".input-invalid").forEach(node => {
      node.classList.remove("input-invalid");
    });

    form.querySelectorAll("[data-field-error]").forEach(node => {
      node.textContent = "";
    });
  }

  function renderCreateFormErrors(form, errors) {
    const groupedErrors = new Map();
    errors.forEach(({ field, message }) => {
      if (!field || !message) return;
      if (!groupedErrors.has(field)) {
        groupedErrors.set(field, []);
      }

      const messages = groupedErrors.get(field);
      if (!messages.includes(message)) {
        messages.push(message);
      }
    });

    groupedErrors.forEach((messages, field) => {
      const container = form.querySelector(`[data-field-container="${field}"]`);
      const errorNode = form.querySelector(`[data-field-error="${field}"]`);
      container?.classList.add("field-invalid");
      container?.querySelectorAll("input, select, textarea").forEach(node => {
        node.classList.add("input-invalid");
      });
      container?.querySelectorAll(".searchable-select-input").forEach(node => {
        node.classList.add("input-invalid");
      });
      if (errorNode) {
        errorNode.textContent = messages.join(" ");
      }
    });
  }

  function focusFirstCreateError(form, errors) {
    const firstError = errors[0];
    if (!firstError) return;

    const container = form.querySelector(`[data-field-container="${firstError.field}"]`);
    if (!container) return;

    container.scrollIntoView({ behavior: "smooth", block: "center" });

    const target =
      container.querySelector(".searchable-select-input")
      || container.querySelector("input, select, textarea, button")
      || container;
    if (typeof target.focus === "function") {
      target.focus({ preventScroll: true });
    }
  }

  async function reloadCategoryOptions(form, preserveCurrentSelection = false) {
    const groupInput = form.querySelector('[name="group"]');
    const categorySelect = form.querySelector("[data-category-select]");
    if (!groupInput || !categorySelect) return;

    const endpoint = categorySelect.dataset.categoryEndpoint;
    if (!endpoint) return;

    const selectedCategoryId = String(categorySelect.dataset.selectedCategoryId || "").trim();
    const currentValue = preserveCurrentSelection
      ? (selectedCategoryId && selectedCategoryId !== "0" ? selectedCategoryId : "")
      : (categorySelect.value || "").trim();

    const response = await fetch(`${endpoint}?group=${encodeURIComponent(groupInput.value)}`, {
      headers: { "X-Requested-With": "fetch" }
    });

    if (!response.ok) {
      categorySelect.innerHTML = `<option value="">No se pudieron cargar las categorías</option>`;
      return;
    }

    const items = await response.json();
    const options = Array.isArray(items) ? items : [];
    categorySelect.innerHTML = `<option value="">Seleccioná una categoría</option>`;

    options.forEach(item => {
      const option = document.createElement("option");
      option.value = item.id ? String(item.id) : "";
      option.textContent = item.name || "";
      if (option.value === currentValue) {
        option.selected = true;
      }
      categorySelect.appendChild(option);
    });

    if (currentValue && !options.some(item => String(item.id) === currentValue)) {
      categorySelect.value = "";
    }

    if (!String(categorySelect.value || "").trim() && categorySelect.options.length > 0) {
      categorySelect.selectedIndex = 0;
    }

    categorySelect.dataset.selectedCategoryId = categorySelect.value || "";
    enhanceSearchableSelects(form);
  }

  async function reloadDynamicCategoryFields(form) {
    const categorySelect = form.querySelector("[data-category-select]");
    const requiredContainer = form.querySelector("[data-dynamic-required-fields-container]");
    const optionalContainer = form.querySelector("[data-dynamic-optional-fields-container]");
    const requiredEmptyNode = form.querySelector("[data-dynamic-required-fields-empty]");
    const optionalEmptyNode = form.querySelector("[data-dynamic-optional-fields-empty]");
    const technicalPanel = form.querySelector("[data-technical-panel]");
    const shouldKeepTechnicalPanelVisible = () => (categorySelect.options?.length || 0) > 1;
    if (!categorySelect || !requiredContainer || !optionalContainer) return;

    const previousValues = new Map([
      ...collectDynamicFieldValues(requiredContainer),
      ...collectDynamicFieldValues(optionalContainer)
    ]);

    const categoryId = String(categorySelect.value || "").trim();
    const template = requiredContainer.dataset.categoryFieldsEndpointTemplate || optionalContainer.dataset.categoryFieldsEndpointTemplate || "";
    if (!categoryId || !template) {
      syncCreateOperationOptions(form, []);
      requiredContainer.querySelectorAll("[data-dynamic-field]").forEach(node => node.remove());
      optionalContainer.querySelectorAll("[data-dynamic-field]").forEach(node => node.remove());
      if (technicalPanel) {
        technicalPanel.hidden = !shouldKeepTechnicalPanelVisible();
      }
      if (requiredEmptyNode) {
        requiredEmptyNode.hidden = false;
        requiredEmptyNode.textContent = "Seleccioná una categoría para completar los datos mínimos adicionales.";
      }
      if (optionalEmptyNode) {
        optionalEmptyNode.hidden = false;
        optionalEmptyNode.textContent = "Seleccioná una categoría para cargar la ficha técnica opcional.";
        syncTechnicalCategoryNotice(form);
      }
      return;
    }

    const endpoint = template.replace("__CATEGORY_ID__", encodeURIComponent(categoryId));
    const response = await fetch(endpoint, {
      headers: { "X-Requested-With": "fetch" }
    });

    requiredContainer.querySelectorAll("[data-dynamic-field]").forEach(node => node.remove());
    optionalContainer.querySelectorAll("[data-dynamic-field]").forEach(node => node.remove());

    if (!response.ok) {
      syncCreateOperationOptions(form, []);
      if (technicalPanel) {
        technicalPanel.hidden = false;
      }
      if (requiredEmptyNode) {
        requiredEmptyNode.hidden = false;
        requiredEmptyNode.textContent = "No se pudieron cargar los campos adicionales de esta categoría.";
      }
      if (optionalEmptyNode) {
        optionalEmptyNode.hidden = false;
        optionalEmptyNode.textContent = "No se pudo cargar la ficha técnica de esta categoría.";
        optionalEmptyNode.classList.remove("is-error");
      }
      return;
    }

      const payload = await response.json();
    const fields = Array.isArray(payload?.fields) ? payload.fields : (Array.isArray(payload) ? payload : []);
    const operationOptions = Array.isArray(payload?.operationOptions) ? payload.operationOptions : [];
    syncCreateOperationOptions(form, operationOptions);
    if (!fields.length) {
      if (technicalPanel) {
        technicalPanel.hidden = false;
      }
      if (requiredEmptyNode) {
        requiredEmptyNode.hidden = false;
        requiredEmptyNode.textContent = "Esta categoría no tiene campos adicionales configurados.";
      }
      if (optionalEmptyNode) {
        optionalEmptyNode.hidden = false;
        optionalEmptyNode.textContent = "Esta categoría no tiene ficha técnica opcional.";
        optionalEmptyNode.classList.remove("is-error");
      }
      return;
    }

    const requiredFields = fields.filter(field => field.mostrarEnDatosMinimos ?? field.obligatorio);
    const optionalFields = fields.filter(field => !(field.mostrarEnDatosMinimos ?? field.obligatorio));

    if (technicalPanel) {
      technicalPanel.hidden = optionalFields.length === 0;
    }

    if (requiredEmptyNode) {
      requiredEmptyNode.hidden = requiredFields.length > 0;
        if (!requiredFields.length) {
          requiredEmptyNode.textContent = "Esta categoría no tiene campos adicionales en datos mínimos.";
        }
    }

    if (optionalEmptyNode) {
      optionalEmptyNode.hidden = optionalFields.length > 0;
      optionalEmptyNode.classList.remove("is-error");
      if (!optionalFields.length) {
        optionalEmptyNode.textContent = "Esta categoría no tiene ficha técnica opcional.";
      }
    }

    requiredFields.forEach(field => {
      requiredContainer.appendChild(buildDynamicFieldNode(field, previousValues.get(field.nombreInterno), "required"));
    });

    optionalFields
      .slice()
      .sort(compareOptionalDynamicFields)
      .forEach(field => {
      optionalContainer.appendChild(buildDynamicFieldNode(field, previousValues.get(field.nombreInterno), "optional"));
      });

    enhanceSearchableSelects(form);
  }

  function enhanceSearchableSelects(root = document) {
    root.querySelectorAll("[data-searchable-select-root]").forEach(wrapper => {
      const select = wrapper.querySelector("select");
      if (select) {
        wrapper.parentNode?.insertBefore(select, wrapper);
      }
      wrapper.remove();
    });
    root.querySelectorAll(".searchable-select-native").forEach(select => {
      select.classList.remove("searchable-select-native");
    });

    root.querySelectorAll("select").forEach(select => {
      if (select.multiple || select.disabled) return;

      let options = Array.from(select.options || []);
      const searchableOptions = options.filter(option => String(option.value || "").trim() !== "");
      const shouldAlwaysBeSearchable = select.matches("[data-category-select]");
      if (searchableOptions.length <= 10 && !shouldAlwaysBeSearchable) return;

      const hasEmptyOption = options.some(option => String(option.value || "").trim() === "");
      if (!hasEmptyOption && !String(select.dataset.selectedCategoryId || select.value || "").trim()) {
        const emptyOption = document.createElement("option");
        emptyOption.value = "";
        emptyOption.textContent = "Debe seleccionar";
        emptyOption.selected = true;
        select.insertBefore(emptyOption, select.firstChild);
        options = Array.from(select.options || []);
      }

      const wrapper = document.createElement("div");
      wrapper.className = "searchable-select";
      wrapper.dataset.searchableSelectRoot = "true";

      const input = document.createElement("input");
      input.type = "text";
      input.className = "searchable-select-input";
      input.placeholder = options[0]?.textContent?.trim() || "Buscar y seleccionar";
      input.autocomplete = "off";
      input.setAttribute("role", "combobox");
      input.setAttribute("aria-autocomplete", "list");
      input.setAttribute("aria-expanded", "false");

      const listId = `searchable-select-${createClientId()}`;
      const menu = document.createElement("div");
      menu.className = "searchable-select-menu";
      menu.id = listId;
      menu.hidden = true;
      menu.setAttribute("role", "listbox");
      input.setAttribute("aria-controls", listId);

      const searchableItems = searchableOptions.map(option => ({
        value: String(option.value || ""),
        label: String(option.textContent || "").trim()
      }));
      const emptyOptionLabel = select.options[0]?.textContent?.trim() || "Debe seleccionar";
      let activeIndex = -1;
      const createBasicsPanel = select.closest("[data-create-basics-panel]");

      const syncOverlayState = isOpen => {
        wrapper.classList.toggle("is-open", isOpen);
        if (createBasicsPanel) {
          createBasicsPanel.classList.toggle("has-open-searchable-select", isOpen);
        }
      };

      const normalizeSearchText = value => String(value || "").trim().toLocaleLowerCase("es");

      const syncInputFromSelect = () => {
        const selectedOption = select.selectedOptions?.[0];
        const selectedValue = String(selectedOption?.value || select.value || "").trim();
        input.value = selectedValue.length > 0
          ? (selectedOption?.textContent?.trim() || "")
          : "";
        input.placeholder = emptyOptionLabel;
      };

      const resetToSelectableState = () => {
        select.value = "";
        input.value = "";
        input.placeholder = emptyOptionLabel;
        select.dispatchEvent(new Event("change", { bubbles: true }));
      };

      const syncSearchableSelectErrorState = (showMessage = false) => {
        const container = select.closest("[data-field-container]");
        if (!container) return;
        const errorNode = container.querySelector(`[data-field-error="${select.dataset.internalName || container.getAttribute("data-field-container") || ""}"]`);
        const isRequired = select.getAttribute("aria-required") === "true" || select.name === "category";
        const hasValue = String(select.value || "").trim().length > 0;

        input.classList.toggle("input-invalid", isRequired && !hasValue);
        if (!isRequired || hasValue) {
          if (errorNode && errorNode.textContent === "Debe seleccionar una opción.") {
            errorNode.textContent = "";
          }
          if (!container.querySelector(".field-error:not(:empty)")) {
            container.classList.remove("field-invalid");
          }
          return;
        }

        if (!showMessage && !container.classList.contains("field-invalid")) {
          input.classList.remove("input-invalid");
          return;
        }

        container.classList.add("field-invalid");
        if (errorNode && !errorNode.textContent.trim()) {
          errorNode.textContent = "Debe seleccionar una opción.";
        }
      };

      const getFilteredItems = () => {
        const query = normalizeSearchText(input.value);
        if (!query) return searchableItems;

        return searchableItems.filter(item => normalizeSearchText(item.label).includes(query));
      };

      const closeMenu = () => {
        activeIndex = -1;
        menu.hidden = true;
        input.setAttribute("aria-expanded", "false");
        input.removeAttribute("aria-activedescendant");
        syncOverlayState(false);
      };

      const setActiveItem = index => {
        const items = Array.from(menu.querySelectorAll("[data-searchable-option]"));
        if (!items.length) {
          activeIndex = -1;
          input.removeAttribute("aria-activedescendant");
          return;
        }

        activeIndex = Math.max(0, Math.min(index, items.length - 1));
        items.forEach((item, itemIndex) => {
          item.classList.toggle("is-active", itemIndex === activeIndex);
        });

        const activeItem = items[activeIndex];
        input.setAttribute("aria-activedescendant", activeItem.id);
        activeItem.scrollIntoView({ block: "nearest" });
      };

      const selectItem = item => {
        if (!item?.value) return;

        if (select.value !== item.value) {
          select.value = item.value;
          select.dispatchEvent(new Event("change", { bubbles: true }));
        }

        syncInputFromSelect();
        syncSearchableSelectErrorState();
        closeMenu();
      };

      const renderMenu = () => {
        const filteredItems = getFilteredItems().slice(0, 80);
        menu.innerHTML = "";

        if (!filteredItems.length) {
          const emptyItem = document.createElement("div");
          emptyItem.className = "searchable-select-empty";
          emptyItem.textContent = "Sin resultados";
          menu.appendChild(emptyItem);
        } else {
          filteredItems.forEach((item, index) => {
            const option = document.createElement("button");
            option.type = "button";
            option.id = `${listId}-option-${index}`;
            option.className = "searchable-select-option";
            option.dataset.searchableOption = "true";
            option.setAttribute("role", "option");
            option.textContent = item.label;
            option.addEventListener("mousedown", event => event.preventDefault());
            option.addEventListener("click", () => selectItem(item));
            menu.appendChild(option);
          });
        }

        menu.hidden = false;
        input.setAttribute("aria-expanded", "true");
        activeIndex = -1;
        syncOverlayState(true);
      };

      const openMenu = () => {
        renderMenu();
      };

      const syncSelectFromInput = () => {
        const raw = String(input.value || "").trim();
        if (!raw) {
          select.value = "";
          select.dispatchEvent(new Event("change", { bubbles: true }));
          return;
        }

        const match = searchableItems.find(option =>
          option.label.localeCompare(raw, "es", { sensitivity: "base" }) === 0
        );

        if (!match) return;

        if (select.value !== match.value) {
          select.value = match.value;
          select.dispatchEvent(new Event("change", { bubbles: true }));
        }
      };

      input.addEventListener("change", syncSelectFromInput);
      input.addEventListener("click", openMenu);
      input.addEventListener("input", () => {
        if (!String(input.value || "").trim()) {
          select.value = "";
          select.dispatchEvent(new Event("change", { bubbles: true }));
        }

        renderMenu();
      });
      input.addEventListener("keydown", event => {
        if (event.key === "ArrowDown") {
          event.preventDefault();
          if (menu.hidden) {
            renderMenu();
          }
          setActiveItem(activeIndex + 1);
          return;
        }

        if (event.key === "ArrowUp") {
          event.preventDefault();
          if (menu.hidden) {
            renderMenu();
          }
          setActiveItem(activeIndex <= 0 ? menu.querySelectorAll("[data-searchable-option]").length - 1 : activeIndex - 1);
          return;
        }

        if (event.key === "Enter" && !menu.hidden) {
          const items = Array.from(menu.querySelectorAll("[data-searchable-option]"));
          const selectedButton = items[activeIndex];
          if (selectedButton) {
            event.preventDefault();
            selectedButton.click();
          }
          return;
        }

        if (event.key === "Escape") {
          closeMenu();
        }
      });
      input.addEventListener("blur", () => {
        window.setTimeout(() => {
          const raw = String(input.value || "").trim();
          if (!raw) {
            resetToSelectableState();
            syncSearchableSelectErrorState(true);
            closeMenu();
            return;
          }

          const match = searchableItems.find(option =>
            option.label.localeCompare(raw, "es", { sensitivity: "base" }) === 0
          );

          if (!match) {
            resetToSelectableState();
            syncSearchableSelectErrorState(true);
            closeMenu();
            return;
          }

          selectItem(match);
        }, 120);
      });
      select.addEventListener("change", () => {
        syncInputFromSelect();
        syncSearchableSelectErrorState();
      });
      syncInputFromSelect();
      syncSearchableSelectErrorState();

      select.classList.add("searchable-select-native");
      select.parentNode?.insertBefore(wrapper, select);
      wrapper.appendChild(input);
      wrapper.appendChild(menu);
      wrapper.appendChild(select);
    });
  }

  function collectDynamicFieldValues(container) {
    const values = new Map();
    container.querySelectorAll("[data-dynamic-field]").forEach(node => {
      const internalName = node.getAttribute("data-field-container");
      if (!internalName) return;

      const input = node.querySelector("[data-dynamic-input]");
      if (!input) return;

      if (input.type === "checkbox") {
        values.set(internalName, input.checked);
        return;
      }

      values.set(internalName, input.value);
    });

    return values;
  }

  function syncCreateOperationOptions(form, options) {
    const field = form.querySelector("[data-create-operation-field]");
    const select = form.querySelector("[data-create-operation-select]");
    if (!field || !select) return;

    const normalizedOptions = Array.isArray(options)
      ? options
        .map(option => String(option || "").trim())
        .filter(Boolean)
      : [];
    const selectedValue = String(select.value || select.dataset.selectedOperation || "").trim();
    const defaultValue = normalizedOptions.find(option => option.toLowerCase() === "venta") || "";
    const nextValue = normalizedOptions.some(option => option.localeCompare(selectedValue, "es", { sensitivity: "base" }) === 0)
      ? selectedValue
      : defaultValue;

    select.innerHTML = '<option value="">Seleccioná una opción</option>';
    normalizedOptions.forEach(optionValue => {
      const option = document.createElement("option");
      option.value = optionValue;
      option.textContent = optionValue;
      option.selected = optionValue.localeCompare(nextValue, "es", { sensitivity: "base" }) === 0;
      select.appendChild(option);
    });

    select.value = nextValue;
    select.dataset.selectedOperation = nextValue;
    field.hidden = normalizedOptions.length === 0;

    if (!normalizedOptions.length) {
      select.value = "";
      select.dataset.selectedOperation = "";
    }

    select.dispatchEvent(new Event("change", { bubbles: true }));
  }

  function syncCreatePricePeriodHint(form) {
    const operation = String(form.querySelector("[data-create-operation-select]")?.value || "").trim().toLocaleLowerCase("es-AR");
    const label = form.querySelector("[data-create-price-label]");
    const hint = form.querySelector("[data-create-price-period-hint]");
    if (!label || !hint) return;

    if (operation === "alquiler") {
      label.textContent = "Precio por mes";
      hint.textContent = "El precio publicado se mostrará como valor mensual.";
      return;
    }

    if (operation === "temporario") {
      label.textContent = "Precio por día";
      hint.textContent = "El precio publicado se mostrará como valor diario.";
      return;
    }

    label.textContent = "Precio";
    hint.textContent = "Para alquileres indicá el valor mensual; para temporarios, el valor diario.";
  }

  function buildDynamicFieldNode(field, currentValue, mode = "optional") {
    const wrapper = document.createElement("label");
    wrapper.dataset.dynamicField = "true";
    wrapper.dataset.fieldContainer = field.nombreInterno || "";
    const isRequiredMode = mode === "required";
    const options = Array.isArray(field.opciones) ? field.opciones.filter(option => String(option || "").trim().length > 0) : [];
    const behavesAsSelect = options.length > 0;
    const effectiveFieldType = behavesAsSelect ? "lista" : String(field.tipoDato || "texto");
    const isHalfWidthOptional = !isRequiredMode && (effectiveFieldType === "numero" || effectiveFieldType === "booleano");
    const optionalWidthClass = isRequiredMode
      ? ""
      : (isHalfWidthOptional ? "dynamic-field-half" : "dynamic-field-full");
    const normalizedLabel = normalizeDynamicFieldLabel(
      field.obligatorio
        ? `${field.etiqueta} *`
        : field.etiqueta || field.nombreInterno || "Campo"
    );
    const example = String(field.ejemplo || "").trim();

    wrapper.className = isRequiredMode
      ? ""
      : (effectiveFieldType === "booleano"
        ? `dynamic-field-inline ${optionalWidthClass}`.trim()
        : `dynamic-field-inline ${optionalWidthClass}`.trim());

    const labelText = document.createElement("span");
    labelText.textContent = normalizedLabel;

    const error = document.createElement("span");
    error.className = "field-error";
    error.dataset.fieldError = field.nombreInterno || "";

    let input;
    switch (effectiveFieldType) {
      case "numero":
        input = document.createElement("input");
        input.type = "number";
        input.step = "1";
        input.value = currentValue ?? "";
        break;
      case "booleano":
        input = document.createElement("select");
        {
          const neutralOption = document.createElement("option");
          neutralOption.value = "";
          neutralOption.textContent = "Seleccionar";
          input.appendChild(neutralOption);

          const falseOption = document.createElement("option");
          falseOption.value = "false";
          falseOption.textContent = "No";
          input.appendChild(falseOption);

          const trueOption = document.createElement("option");
          trueOption.value = "true";
          trueOption.textContent = "Sí";
          input.appendChild(trueOption);

          input.value = String(currentValue) === "true"
            ? "true"
            : (String(currentValue) === "false" ? "false" : "");
        }
        break;
      case "lista":
        input = document.createElement("select");
        {
          const placeholder = document.createElement("option");
          placeholder.value = "";
          placeholder.textContent = "Seleccioná una opción";
          input.appendChild(placeholder);

          options.forEach(optionValue => {
            const option = document.createElement("option");
            option.value = String(optionValue || "");
            option.textContent = normalizeDynamicFieldLabel(String(optionValue || ""));
            if (option.value === String(currentValue ?? "")) {
              option.selected = true;
            }
            input.appendChild(option);
          });
        }
        break;
      default:
        input = document.createElement("input");
        input.type = "text";
        input.value = currentValue ?? "";
        break;
    }

    input.dataset.dynamicInput = "true";
    input.dataset.fieldId = String(field.id || "");
    input.dataset.fieldType = effectiveFieldType;
    input.dataset.internalName = String(field.nombreInterno || "");
    input.name = `dynamic_${field.nombreInterno || field.id || createClientId()}`;

    if (field.obligatorio) {
      input.setAttribute("aria-required", "true");
    }

    if ((effectiveFieldType === "texto" || effectiveFieldType === "numero") && example) {
      input.placeholder = normalizeDynamicFieldLabel(example);
    }

    const unit = normalizeDynamicFieldLabel(String(field.unidad || "").trim());
    if (!isRequiredMode && effectiveFieldType !== "booleano" && unit && !behavesAsSelect) {
      const fieldRow = document.createElement("div");
      fieldRow.className = "field-input-with-unit";
      fieldRow.appendChild(input);

      const unitBadge = document.createElement("span");
      unitBadge.className = "field-unit";
      unitBadge.textContent = `(${unit})`;
      fieldRow.appendChild(unitBadge);

      wrapper.appendChild(labelText);
      wrapper.appendChild(fieldRow);
      wrapper.appendChild(error);
      return wrapper;
    }

    wrapper.appendChild(labelText);
    wrapper.appendChild(input);
    wrapper.appendChild(error);
    return wrapper;
  }

  function compareOptionalDynamicFields(left, right) {
    const typeDiff = getOptionalDynamicFieldOrder(left) - getOptionalDynamicFieldOrder(right);
    if (typeDiff !== 0) {
      return typeDiff;
    }

    const leftOrder = Number.isFinite(Number(left?.orden)) ? Number(left.orden) : Number.MAX_SAFE_INTEGER;
    const rightOrder = Number.isFinite(Number(right?.orden)) ? Number(right.orden) : Number.MAX_SAFE_INTEGER;
    if (leftOrder !== rightOrder) {
      return leftOrder - rightOrder;
    }

    return String(left?.etiqueta || left?.nombreInterno || "")
      .localeCompare(String(right?.etiqueta || right?.nombreInterno || ""), "es", { sensitivity: "base" });
  }

  function getOptionalDynamicFieldOrder(field) {
    const options = Array.isArray(field?.opciones) ? field.opciones.filter(option => String(option || "").trim().length > 0) : [];
    if (options.length > 0) {
      return 0;
    }

    switch (String(field?.tipoDato || "").toLowerCase()) {
      case "lista":
        return 0;
      case "numero":
        return 1;
      case "booleano":
        return 2;
      case "texto":
        return 3;
      default:
        return 4;
    }
  }

  function normalizeDynamicFieldLabel(value) {
    return String(value || "")
      .replace(/\bAntiguedad\b/gi, matchCase("Antigüedad"))
      .replace(/\bBanios\b/gi, matchCase("Baños"))
      .replace(/\bAnios\b/gi, matchCase("Años"));
  }

  function matchCase(replacement) {
    return source => {
      if (source === source.toUpperCase()) {
        return replacement.toUpperCase();
      }

      if (source[0] === source[0]?.toUpperCase()) {
        return replacement[0].toUpperCase() + replacement.slice(1);
      }

      return replacement.toLowerCase();
    };
  }

  function wireCreateImageUploader(form) {
    const dropzone = form.querySelector("[data-image-dropzone]");
    const input = form.querySelector("[data-create-images-input]");
    const pickButton = form.querySelector("[data-create-images-pick]");
    const clearButton = form.querySelector("[data-create-images-clear]");
    const previews = form.querySelector("[data-image-previews]");
    const countNode = form.querySelector("[data-image-count]");
    const imagesCsvInput = form.querySelector('input[name="imagesCsv"]');
    const videoUrlInput = form.querySelector('input[name="videoUrl"]');
    const feedback = document.getElementById("create-feedback");

    const state = [];
    let uploadChain = Promise.resolve();
    let draggingImageId = null;
    let didPersistPublication = false;
    let cleanupQueued = false;

    const allowedImageExtensions = new Set([".jpg", ".jpeg", ".jfif", ".png", ".webp", ".gif", ".bmp"]);
    const allowedImageTypes = new Set(["image/jpeg", "image/png", "image/webp", "image/gif", "image/bmp"]);

    const showDeleteWarning = message => {
      if (!feedback) return;
      feedback.innerHTML = `<div class="status-banner warning">${escapeHtml(message || "No se pudo borrar alguna imagen en R2.")}</div>`;
    };

    const showUploadWarning = message => {
      if (!feedback) return;
      feedback.innerHTML = `<div class="status-banner warning">${escapeHtml(message || "No se pudieron subir las imagenes.")}</div>`;
    };

    const showVerticalImageWarning = () => {
      window.alert('Ventagram fue optimizada para mostrar imagenes "Verticales" tamaño celular si pones otro tipo de imagenes pueden cortarse en los listados de anuncios.\n\nLo óptimo sería que todas sean verticales o al menos la principal.');
    };

    const isSupportedImageFile = file => {
      const type = String(file?.type || "").trim().toLowerCase();
      const name = String(file?.name || "");
      const dotIndex = name.lastIndexOf(".");
      const extension = dotIndex >= 0 ? name.slice(dotIndex).toLowerCase() : "";
      return allowedImageTypes.has(type) || allowedImageExtensions.has(extension);
    };

    const deleteUploadedUrls = async urls => {
      const result = await deleteUploadedMediaRequest(urls);
      if (!result.ok) {
        throw new Error(result.message || "No se pudo borrar alguna imagen en R2.");
      }
    };

    const collectTemporaryUploadedUrls = () => state
      .filter(item => item.deleteOnRemove && item.uploadedUrl)
      .map(item => item.uploadedUrl);

    const queueTemporaryCleanup = () => {
      if (didPersistPublication || cleanupQueued) {
        return;
      }

      const urls = collectTemporaryUploadedUrls();
      if (!urls.length) {
        return;
      }

      cleanupQueued = true;
      const payload = JSON.stringify({ urls });

      try {
        if (navigator.sendBeacon) {
          const blob = new Blob([payload], { type: "application/json" });
          if (navigator.sendBeacon("/api/content/delete-uploaded-media", blob)) {
            return;
          }
        }
      } catch {
        // Fallback below.
      }

      fetch("/api/content/delete-uploaded-media", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "X-Requested-With": "fetch"
        },
        body: payload,
        keepalive: true
      }).catch(() => {});
    };

    const seedExistingImages = () => {
      const existingUrls = String(imagesCsvInput?.value || "")
        .split(",")
        .map(item => item.trim())
        .filter(Boolean);

      existingUrls.forEach(url => {
        state.push({
          id: createClientId(),
          file: { name: "Imagen actual" },
          previewUrl: url,
          uploadedUrl: url,
          statusText: "Listo",
          deleteOnRemove: false,
          removed: false
        });
      });
    };

    const updateHiddenValue = () => {
      if (imagesCsvInput) {
        imagesCsvInput.value = state
          .map(item => item.uploadedUrl)
          .filter(Boolean)
          .join(",");
      }
    };

    const hasPrimaryVideo = () => String(videoUrlInput?.value || "").trim().length > 0;

    const moveImage = (fromId, toId) => {
      if (!fromId || !toId || fromId === toId) return;
      const fromIndex = state.findIndex(item => item.id === fromId);
      const toIndex = state.findIndex(item => item.id === toId);
      if (fromIndex < 0 || toIndex < 0 || fromIndex === toIndex) return;

      const [item] = state.splice(fromIndex, 1);
      state.splice(toIndex, 0, item);
      render();
    };

    const render = () => {
      if (countNode) {
        countNode.textContent = `${state.length}/11 imágenes`;
      }

      if (!previews) return;

      if (!state.length) {
        previews.innerHTML = `<div class="upload-preview upload-preview-empty"><span>Las imágenes que subas aparecerán acá.</span></div>`;
        updateHiddenValue();
        return;
      }

      previews.innerHTML = state.map((item, index) => `
        <article class="upload-preview ${index === 0 && !hasPrimaryVideo() ? "main" : ""}" draggable="true" data-upload-item-id="${item.id}">
          <img src="${escapeAttribute(item.previewUrl)}" alt="${escapeAttribute(item.file.name)}" />
          <span class="gallery-badge ${index === 0 && !hasPrimaryVideo() ? "upload-primary-badge" : ""}">${index === 0 && !hasPrimaryVideo() ? "Principal" : `#${index + 1}`}</span>
          <span class="upload-status">${escapeHtml(item.statusText || (item.uploadedUrl ? "Listo" : "Subiendo..."))}</span>
          <button type="button" class="gallery-nav gallery-nav-next upload-action" data-upload-action="remove" data-upload-id="${item.id}" aria-label="Quitar imagen">&times;</button>
        </article>
      `).join("");

      updateHiddenValue();
    };

    const uploadFiles = async files => {
      const selectedFiles = Array.from(files).filter(Boolean);
      const unsupportedFiles = selectedFiles.filter(file => !isSupportedImageFile(file));
      const validFiles = selectedFiles
        .filter(file => isSupportedImageFile(file))
        .slice(0, Math.max(0, 11 - state.length));

      if (unsupportedFiles.length) {
        const unsupportedNames = unsupportedFiles
          .map(file => file.name)
          .filter(Boolean)
          .slice(0, 3)
          .join(", ");
        showUploadWarning(
          unsupportedNames
            ? `Estas imagenes no son compatibles: ${unsupportedNames}. Usa JPG, PNG o WEBP.`
            : "Alguna imagen no es compatible. Usa JPG, PNG o WEBP."
        );
      }

      if (!validFiles.length) {
        return;
      }

      const items = validFiles.map(file => ({
        id: createClientId(),
        file,
        previewUrl: URL.createObjectURL(file),
        uploadedUrl: null,
        statusText: "Preparando...",
        shouldWarnAboutOrientation: false,
        deleteOnRemove: true,
        removed: false
      }));

      state.push(...items);
      render();

      const currentUpload = uploadChain.then(async () => {
        let shouldShowVerticalWarning = false;

        for (const item of items) {
          const currentItem = state.find(entry => entry.id === item.id);
          if (!currentItem || currentItem.removed) {
            continue;
          }

          let fileToUpload = currentItem.file;
          try {
            currentItem.statusText = "Comprimiendo...";
            render();
            const dimensions = await readImageDimensions(currentItem.file);
            currentItem.shouldWarnAboutOrientation = dimensions.width >= dimensions.height;
            shouldShowVerticalWarning = shouldShowVerticalWarning || currentItem.shouldWarnAboutOrientation;
            fileToUpload = await optimizeImageForUpload(currentItem.file, {
              maxSide: 2000,
              quality: 0.9
            });
          } catch {
            fileToUpload = currentItem.file;
          }

          currentItem.statusText = "Subiendo...";
          render();

          const formData = new FormData();
          formData.append("files", fileToUpload);

          const result = await uploadImagesRequest(formData);
          if (!result.ok) {
            const index = state.findIndex(x => x.id === item.id);
            if (index >= 0) {
              URL.revokeObjectURL(state[index].previewUrl);
              state.splice(index, 1);
            }
            render();
            throw new Error(result.message || "No se pudo subir una de las imagenes.");
          }

          const uploadedUrl = Array.isArray(result.urls) ? result.urls[0] : null;
          if (!uploadedUrl) {
            const index = state.findIndex(x => x.id === item.id);
            if (index >= 0) {
              URL.revokeObjectURL(state[index].previewUrl);
              state.splice(index, 1);
            }
            render();
            throw new Error("La subida termino sin devolver la imagen procesada.");
          }

          currentItem.uploadedUrl = uploadedUrl;
          currentItem.statusText = "Listo";

          if (currentItem.removed && currentItem.deleteOnRemove) {
            try {
              await deleteUploadedUrls([uploadedUrl]);
            } catch (error) {
              showDeleteWarning(error?.message || "No se pudo borrar alguna imagen descartada.");
            }
          }

          render();
        }

        if (shouldShowVerticalWarning) {
          showVerticalImageWarning();
        }
      });

      try {
        uploadChain = currentUpload.catch(() => {});
        await currentUpload;
      } catch (error) {
        showUploadWarning(error.message || "No se pudieron subir las imagenes.");
      }
    };

    const removeById = async id => {
      const index = state.findIndex(item => item.id === id);
      if (index < 0) return;
      const [item] = state.splice(index, 1);
      item.removed = true;
      URL.revokeObjectURL(item.previewUrl);
      render();

      if (item.deleteOnRemove && item.uploadedUrl) {
        try {
          await deleteUploadedUrls([item.uploadedUrl]);
        } catch (error) {
          showDeleteWarning(error?.message || "No se pudo borrar la imagen quitada.");
        }
      }
    };

    pickButton?.addEventListener("click", event => {
      event.preventDefault();
      input?.click();
    });

    clearButton?.addEventListener("click", async event => {
      event.preventDefault();
      const urlsToDelete = [];
      while (state.length) {
        const item = state.pop();
        item.removed = true;
        if (item.deleteOnRemove && item.uploadedUrl) {
          urlsToDelete.push(item.uploadedUrl);
        }
        URL.revokeObjectURL(item.previewUrl);
      }
      if (input) {
        input.value = "";
      }
      render();
      if (urlsToDelete.length) {
        try {
          await deleteUploadedUrls(urlsToDelete);
        } catch (error) {
          showDeleteWarning(error?.message || "No se pudieron borrar algunas imagenes quitadas.");
        }
      }
    });

    input?.addEventListener("change", () => {
      if (input.files?.length) {
        uploadFiles(input.files).finally(() => {
          input.value = "";
        });
      }
    });

    dropzone?.addEventListener("dragover", event => {
      event.preventDefault();
      dropzone.classList.add("is-dragover");
    });

    dropzone?.addEventListener("dragleave", () => {
      dropzone.classList.remove("is-dragover");
    });

    dropzone?.addEventListener("drop", event => {
      event.preventDefault();
      dropzone.classList.remove("is-dragover");
      const files = event.dataTransfer?.files;
      if (files?.length) {
        uploadFiles(files);
      }
    });

    previews?.addEventListener("click", event => {
      const button = event.target.closest("[data-upload-action]");
      if (!button) return;

      const id = button.getAttribute("data-upload-id");
      if (!id) return;

      if (button.getAttribute("data-upload-action") === "remove") {
        removeById(id);
      }
    });

    previews?.addEventListener("dragstart", event => {
      const card = event.target.closest("[data-upload-item-id]");
      if (!card) return;
      draggingImageId = card.getAttribute("data-upload-item-id");
      card.classList.add("is-dragging");
      if (event.dataTransfer) {
        event.dataTransfer.effectAllowed = "move";
        event.dataTransfer.setData("text/plain", draggingImageId || "");
      }
    });

    previews?.addEventListener("dragend", event => {
      const card = event.target.closest("[data-upload-item-id]");
      draggingImageId = null;
      card?.classList.remove("is-dragging");
      previews.querySelectorAll(".upload-preview.is-drop-target").forEach(node => {
        node.classList.remove("is-drop-target");
      });
    });

    previews?.addEventListener("dragover", event => {
      const card = event.target.closest("[data-upload-item-id]");
      if (!card || !draggingImageId) return;
      event.preventDefault();
      event.stopPropagation();
      if (event.dataTransfer) {
        event.dataTransfer.dropEffect = "move";
      }
      previews.querySelectorAll(".upload-preview.is-drop-target").forEach(node => {
        if (node !== card) {
          node.classList.remove("is-drop-target");
        }
      });
      if (card.getAttribute("data-upload-item-id") !== draggingImageId) {
        card.classList.add("is-drop-target");
      }
    });

    previews?.addEventListener("dragleave", event => {
      const card = event.target.closest("[data-upload-item-id]");
      if (!card) return;
      const nextTarget = event.relatedTarget;
      if (nextTarget instanceof Node && card.contains(nextTarget)) {
        return;
      }
      card.classList.remove("is-drop-target");
    });

    previews?.addEventListener("drop", event => {
      const card = event.target.closest("[data-upload-item-id]");
      if (!card || !draggingImageId) return;
      event.preventDefault();
      event.stopPropagation();
      card.classList.remove("is-drop-target");
      moveImage(draggingImageId, card.getAttribute("data-upload-item-id"));
    });

    form.addEventListener("create:video-state-changed", render);
    window.addEventListener("pagehide", queueTemporaryCleanup);

    seedExistingImages();
    render();

    return {
      waitForUploads: () => uploadChain,
      markPersisted: () => {
        didPersistPublication = true;
      },
      hasPendingFiles: () => state.some(item => !item.uploadedUrl)
    };
  }

  function wireCreateVideoUploader(form) {
    const dropzone = form.querySelector("[data-video-dropzone]");
    const input = form.querySelector("[data-create-video-input]");
    const pickButton = form.querySelector("[data-create-video-pick]");
    const clearButton = form.querySelector("[data-create-video-clear]");
    const previews = form.querySelector("[data-video-previews]");
    const statusNode = form.querySelector("[data-video-status]");
    const videoUrlInput = form.querySelector('input[name="videoUrl"]');
    const feedback = document.getElementById("create-feedback");

    let state = null;
    let uploadChain = Promise.resolve();

    const showDeleteWarning = message => {
      if (!feedback) return;
      feedback.innerHTML = `<div class="status-banner warning">${escapeHtml(message || "No se pudo borrar el video en R2.")}</div>`;
    };

    const deleteUploadedUrls = async urls => {
      const result = await deleteUploadedMediaRequest(urls);
      if (!result.ok) {
        throw new Error(result.message || "No se pudo borrar el video en R2.");
      }
    };

    const seedExistingVideo = () => {
      const existingUrl = String(videoUrlInput?.value || "").trim();
      if (!existingUrl) return;

      state = {
        id: createClientId(),
        file: { name: "Video actual" },
        previewUrl: existingUrl,
        uploadedUrl: existingUrl,
        statusText: "Listo",
        deleteOnRemove: false,
        removed: false
      };
    };

    const updateHiddenValue = () => {
      if (videoUrlInput) {
        videoUrlInput.value = state?.uploadedUrl || "";
      }
      form.dispatchEvent(new CustomEvent("create:video-state-changed", { bubbles: true }));
    };

    const render = () => {
      if (statusNode) {
        statusNode.textContent = state
          ? (state.statusText || (state.uploadedUrl ? "Video listo" : "Subiendo video..."))
          : "Sin video";
      }

      if (!previews) return;

      if (!state) {
        previews.innerHTML = `<div class="upload-preview upload-preview-empty upload-preview-video-empty"><span>Si subis un video, se mostrara primero en la publicacion.</span></div>`;
        updateHiddenValue();
        return;
      }

      previews.innerHTML = `
        <article class="upload-preview upload-preview-video main">
          <video src="${escapeAttribute(state.previewUrl)}" preload="metadata" muted playsinline controls></video>
          <span class="gallery-badge">Video principal</span>
          <span class="upload-status">${escapeHtml(state.statusText || (state.uploadedUrl ? "Listo" : "Subiendo..."))}</span>
          <button type="button" class="gallery-nav gallery-nav-next upload-action" data-video-action="remove" aria-label="Quitar video">&times;</button>
        </article>
      `;

      updateHiddenValue();
    };

    const clearState = async () => {
      if (!state) {
        if (input) {
          input.value = "";
        }
        render();
        return;
      }

      const currentState = state;
      currentState.removed = true;
      if (currentState.previewUrl) {
        URL.revokeObjectURL(currentState.previewUrl);
      }

      state = null;
      if (input) {
        input.value = "";
      }
      render();

      if (currentState.deleteOnRemove && currentState.uploadedUrl) {
        try {
          await deleteUploadedUrls([currentState.uploadedUrl]);
        } catch (error) {
          showDeleteWarning(error?.message || "No se pudo borrar el video quitado.");
        }
      }
    };

    const uploadFile = async file => {
      if (!file?.type?.startsWith("video/")) {
        throw new Error("Selecciona un archivo de video valido.");
      }

      const metadata = await readVideoMetadata(file);
      if (!Number.isFinite(metadata.width) || !Number.isFinite(metadata.height) || metadata.width <= 0 || metadata.height <= 0) {
        throw new Error("No pudimos leer el tamaño del video.");
      }

      if (metadata.width >= metadata.height) {
        window.alert("Ventagram sólo permite videos Verticales");
        throw new Error("Ventagram sólo permite videos Verticales");
      }

      if (!Number.isFinite(metadata.duration) || metadata.duration <= 0) {
        throw new Error("No pudimos leer la duracion del video.");
      }

      if (metadata.duration > 60) {
        throw new Error("El video no puede durar mas de 1 minuto.");
      }

      await clearState();

      state = {
        id: createClientId(),
        file,
        previewUrl: URL.createObjectURL(file),
        uploadedUrl: null,
        statusText: "Preparando...",
        deleteOnRemove: true,
        removed: false
      };
      const currentState = state;
      render();

      const currentUpload = uploadChain.then(async () => {
        let fileToUpload = file;
        try {
          currentState.statusText = "Comprimiendo...";
          render();
          fileToUpload = await optimizeVideoForUpload(file, {
            maxWidth: 1080,
            maxHeight: 1920,
            videoBitsPerSecond: 6_500_000,
            audioBitsPerSecond: 128_000
          });
        } catch {
          fileToUpload = file;
        }

        currentState.statusText = "Subiendo...";
        render();
        const formData = new FormData();
        formData.append("file", fileToUpload);

        const result = await uploadVideoRequest(formData);
        if (!result.ok || !result.url) {
          clearState();
          throw new Error(result.message || "No se pudo subir el video.");
        }

        if (state?.id === currentState.id) {
          state.uploadedUrl = result.url;
          state.statusText = "Listo";
        }
        if (currentState.removed && currentState.deleteOnRemove) {
          try {
            await deleteUploadedUrls([result.url]);
          } catch (error) {
            showDeleteWarning(error?.message || "No se pudo borrar el video descartado.");
          }
        }
        render();
      });

      uploadChain = currentUpload.catch(() => {});
      await currentUpload;
    };

    const consumeFileList = files => {
      const file = Array.from(files || []).find(item => item.type.startsWith("video/"));
      if (!file) {
        return;
      }

      uploadFile(file).catch(error => {
        clearState();
        if (feedback) {
          feedback.innerHTML = `<div class="status-banner warning">${escapeHtml(error.message || "No se pudo subir el video.")}</div>`;
        }
      });
    };

    pickButton?.addEventListener("click", event => {
      event.preventDefault();
      input?.click();
    });

    clearButton?.addEventListener("click", async event => {
      event.preventDefault();
      await clearState();
    });

    input?.addEventListener("change", () => {
      if (input.files?.length) {
        consumeFileList(input.files);
      }
    });

    dropzone?.addEventListener("dragover", event => {
      event.preventDefault();
      dropzone.classList.add("is-dragover");
    });

    dropzone?.addEventListener("dragleave", () => {
      dropzone.classList.remove("is-dragover");
    });

    dropzone?.addEventListener("drop", event => {
      event.preventDefault();
      dropzone.classList.remove("is-dragover");
      const files = event.dataTransfer?.files;
      if (files?.length) {
        consumeFileList(files);
      }
    });

    previews?.addEventListener("click", event => {
      const button = event.target.closest("[data-video-action='remove']");
      if (!button) return;
      clearState();
    });

    seedExistingVideo();
    render();

    return {
      waitForUploads: () => uploadChain,
      hasPendingFiles: () => Boolean(state && !state.uploadedUrl)
    };
  }

  async function wireInfiniteGalleryFeeds(root = document) {
    const feeds = Array.from(root.querySelectorAll("[data-gallery-feed='true']"));
    for (const feed of feeds) {
      if (feed.dataset.galleryFeedBound === "true") continue;
      feed.dataset.galleryFeedBound = "true";
      await initInfiniteGalleryFeed(feed, {
        desktopVisibleRows: Number(feed.dataset.galleryDesktopVisibleRows || 3),
        desktopPreloadRows: Number(feed.dataset.galleryDesktopPreloadRows || 3),
        mobilePreloadItems: Number(feed.dataset.galleryMobilePreloadItems || 20)
      });
    }
  }

  async function initInfiniteGalleryFeed(feed, options) {
    const rail = feed.querySelector(".gallery-rail");
    const loader = feed.querySelector("[data-gallery-loader]");
    const sentinel = feed.querySelector("[data-gallery-sentinel]");
    const endpoint = feed.dataset.galleryEndpoint || "";
    if (!rail || !loader || !sentinel || !endpoint) return;

    const state = {
      endpoint,
      offset: 0,
      hasMore: true,
      loading: false,
      observer: null,
      usingExpandedRadius: false
    };

    const syncLoader = message => {
      loader.textContent = message;
      loader.hidden = false;
    };

    const getConfig = () => {
      const mobile = isMobileGalleryAutoplayContext();
      if (mobile) {
        const mobileBatch = Math.max(1, options.mobilePreloadItems || MEDIA_PRELOAD_CONFIG.mobilePreloadAds);
        return {
          initialLimit: mobileBatch,
          appendLimit: mobileBatch,
          rootMargin: "1200px 0px"
        };
      }

      const columns = getGalleryColumnCount(rail);
      const visibleRows = Math.max(1, options.desktopVisibleRows || 3);
      const preloadRows = Math.max(1, options.desktopPreloadRows || MEDIA_PRELOAD_CONFIG.desktopPreloadRows);
      return {
        initialLimit: columns * (visibleRows + preloadRows),
        appendLimit: columns * preloadRows,
        rootMargin: "1600px 0px"
      };
    };

    const loadMore = async () => {
      if (state.loading || !state.hasMore) return;

      state.loading = true;
      syncLoader(state.offset === 0 ? "Cargando publicaciones..." : "Cargando mas publicaciones...");
      const loadingTicket = state.offset === 0 ? beginSystemLoading() : null;
      const config = getConfig();
      const limit = state.offset === 0 ? config.initialLimit : config.appendLimit;

      try {
        const url = new URL(state.endpoint, window.location.origin);
        url.searchParams.set("offset", String(state.offset));
        url.searchParams.set("limit", String(limit));

        const response = await fetch(url.toString(), {
          headers: { "X-Requested-With": "fetch" }
        });

        if (!response.ok) {
          throw new Error(`Gallery request failed with status ${response.status}`);
        }

        const payload = await response.json();
        const items = Array.isArray(payload?.items) ? payload.items : [];
        const usedExpandedRadius = Boolean(payload?.usedExpandedRadius);
        const expandedRadiusKm = Number(payload?.expandedRadiusKm || 0);
        const expandedRadiusTotalResults = Number(payload?.expandedRadiusTotalResults || 0);
        if (state.offset === 0 && usedExpandedRadius && !state.usingExpandedRadius) {
          rail.insertAdjacentHTML("beforeend", `
            <section class="classified-toolbar">
              <div class="classified-summary">Resultados fuera de ese radio</div>
              <div class="classified-page-size">Hasta ${expandedRadiusKm} km${expandedRadiusTotalResults > 0 ? ` · ${expandedRadiusTotalResults} resultados` : ""}</div>
            </section>
          `);
          state.usingExpandedRadius = true;
        }
        rail.insertAdjacentHTML("beforeend", items.map((item, index) => buildGalleryCard(item, state.offset + index === 0)).join(""));
        state.offset = Number(payload?.nextOffset ?? (state.offset + items.length));
        state.hasMore = Boolean(payload?.hasMore);

        if (!items.length && state.offset === 0) {
          syncLoader("No se encontraron resultados dentro del radio seleccionado.");
          sentinel.hidden = true;
        } else if (!items.length && !state.hasMore) {
          syncLoader("No hay mas publicaciones para mostrar.");
        } else if (!state.hasMore) {
          syncLoader("Llegaste al final de la galeria.");
          sentinel.hidden = true;
        } else {
          loader.hidden = true;
          sentinel.hidden = false;
        }

        wireGalleryCards();
        syncMobileGalleryVideoAutoplay(document);
        mediaPreloadService.refreshFeedPreloads(feed);
      } catch (error) {
        console.error(error);
        syncLoader("No se pudieron cargar mas publicaciones.");
      } finally {
        if (loadingTicket) {
          endSystemLoading(loadingTicket);
        }
        state.loading = false;
      }
    };

    const observeMore = () => {
      state.observer?.disconnect?.();
      const config = getConfig();
      state.observer = new IntersectionObserver(entries => {
        if (entries.some(entry => entry.isIntersecting)) {
          loadMore().catch(console.error);
        }
      }, {
        root: null,
        rootMargin: config.rootMargin,
        threshold: 0
      });
      state.observer.observe(sentinel);
    };

    observeMore();
    mediaPreloadService.bindFeed(feed, rail);
    await loadMore();
    window.addEventListener("resize", observeMore, { passive: true });
  }

  function initStaticGalleryFeeds(root = document) {
    root.querySelectorAll("[data-static-gallery-feed='true']").forEach(feed => {
      if (feed.dataset.bound === "true") return;

      const rail = feed.querySelector(".gallery-rail");
      const dataNodeId = String(feed.dataset.staticGalleryItemsId || "").trim();
      const dataNode = dataNodeId ? document.getElementById(dataNodeId) : null;
      if (!rail || !dataNode) return;

      let items = [];
      try {
        const parsed = JSON.parse(dataNode.textContent || "[]");
        items = Array.isArray(parsed) ? parsed : [];
      } catch (error) {
        console.error("No se pudo leer la galeria estatica.", error);
        items = [];
      }

      rail.innerHTML = items.map((item, index) => buildGalleryCard(item, index === 0)).join("");
      wireGalleryCards();
      wireFavoriteActions(feed);
      syncMobileGalleryVideoAutoplay(feed);
      mediaPreloadService.bindFeed(feed, rail);
      mediaPreloadService.refreshFeedPreloads(feed);
      feed.dataset.bound = "true";
    });
  }

  function getGalleryColumnCount(rail) {
    const styles = window.getComputedStyle(rail);
    const gap = Number.parseFloat(styles.columnGap || styles.gap || "16") || 16;
    const minCardWidth = 220;
    const availableWidth = rail.clientWidth || rail.parentElement?.clientWidth || window.innerWidth;
    return Math.max(1, Math.floor((availableWidth + gap) / (minCardWidth + gap)));
  }

  function wireCreateLivePreview(form) {
    const preview = form.closest(".create-editor-layout")?.querySelector(".create-live-preview");
    if (!preview) return;
    const media = preview.querySelector("[data-create-preview-media]");
    const price = preview.querySelector("[data-create-preview-price]");
    const description = preview.querySelector("[data-create-preview-description]");
    const value = name => String(form.elements.namedItem(name)?.value || "").trim();
    let mediaKey = "";
    let frame = 0;
    const update = () => {
      const title = value("title");
      const shortDescription = value("shortDescription");
      description.firstElementChild.textContent = shortDescription || "Completa descripción corta";
      description.dataset.tooltipLabel = [title, shortDescription].filter(Boolean).join("\n");
      const operation = value("operation");
      const amount = value("price");
      const period = operation === "Alquiler" ? " / mes" : operation === "Temporario" ? " / día" : "";
      const formattedPrice = amount && Number.isFinite(Number(amount))
        ? `${value("currency")} ${Number(amount).toLocaleString("es-AR", { maximumFractionDigits: 2 })}${period}`
        : "Precio a completar";
      price.replaceChildren();
      if (operation) {
        const letter = document.createElement("strong");
        letter.className = "gallery-operation-letter";
        letter.textContent = operation.charAt(0).toUpperCase();
        price.append(letter);
      }
      price.append(document.createTextNode(formattedPrice));
      const category = form.querySelector("[data-category-select]")?.selectedOptions[0]?.textContent?.trim();
      price.dataset.tooltipLabel = [operation, category, formattedPrice].filter(Boolean).join(" · ");
      const videoUrl = form.querySelector("[data-video-previews] video")?.getAttribute("src") || value("videoUrl");
      const imageUrl = form.querySelector("[data-image-previews] img")?.getAttribute("src") || value("imagesCsv").split(",").filter(Boolean)[0];
      const nextKey = videoUrl ? `video:${videoUrl}` : imageUrl ? `image:${imageUrl}` : "empty";
      if (nextKey !== mediaKey) {
        mediaKey = nextKey;
        media.querySelector("video")?.pause();
        media.replaceChildren();
        if (videoUrl || imageUrl) {
          const element = document.createElement(videoUrl ? "video" : "img");
          element.src = videoUrl || imageUrl;
          if (videoUrl) {
            element.muted = true;
            element.playsInline = true;
            element.className = "gallery-carousel-video";
            element.controls = false;
            element.preload = "metadata";
          } else {
            element.alt = "Portada del anuncio";
          }
          media.append(element);
          if (videoUrl) {
            const card = media.closest(".card-image-wrap");
            const playButton = document.createElement("button");
            playButton.type = "button";
            playButton.className = "gallery-play-toggle gallery-tooltip-trigger gallery-tooltip-top";
            playButton.dataset.galleryPlayToggle = "true";
            playButton.dataset.bound = "true";
            const audioButton = document.createElement("button");
            audioButton.type = "button";
            audioButton.className = "gallery-audio-toggle gallery-tooltip-trigger gallery-tooltip-side";
            audioButton.dataset.galleryAudioToggle = "true";
            audioButton.dataset.bound = "true";
            playButton.addEventListener("click", event => {
              event.preventDefault();
              event.stopPropagation();
              toggleGalleryVideoPlayback(card);
            });
            audioButton.addEventListener("click", event => {
              event.preventDefault();
              event.stopPropagation();
              toggleGalleryVideoAudio(card);
            });
            media.append(playButton, audioButton);
            bindGalleryVideoState(element);
            element.addEventListener("volumechange", () => syncGalleryVideoAudioButton(card));
            syncGalleryVideoVisualState(card);
            syncGalleryVideoAudioButton(card);
          }
        } else {
          media.textContent = "Tu foto o video de portada aparecerá acá";
        }
      }
    };
    const schedule = () => {
      cancelAnimationFrame(frame);
      frame = requestAnimationFrame(update);
    };
    form.addEventListener("input", schedule);
    form.addEventListener("change", schedule);
    form.addEventListener("create:video-state-changed", schedule);
    new MutationObserver(schedule).observe(form, { childList: true, subtree: true });
    schedule();
  }

  function getGalleryDescription(item) {
    return String(item?.shortDescription || "").trim()
      || String(item?.title || "").split(" - oportunidad")[0];
  }

  function buildDescriptionTooltipLabel(item) {
    return escapeAttribute([
      String(item?.title || "").trim(),
      String(item?.shortDescription || "").trim()
    ].filter(Boolean).join("\n"));
  }

  function buildGalleryCard(item, isFirstCard, options = {}) {
    const title = escapeHtml(item?.title || "");
    const galleryTitle = escapeHtml(getGalleryDescription(item));
    const descriptionTooltipLabel = buildDescriptionTooltipLabel(item);
    const publicationId = escapeAttribute(item?.id || "");
    const publicationCode = escapeAttribute(item?.publicationCode || "");
    const detailsUrl = escapeAttribute(item?.detailsUrl || "#");
    const price = escapeHtml(item?.price || "");
    const operationLabel = escapeHtml(item?.operationLabel || "");
    const operationLetter = escapeHtml(String(item?.operationLabel || "").trim().charAt(0).toUpperCase());
    const priceTooltipLabel = buildPriceTooltipLabel(item);
    const videoUrl = escapeAttribute(item?.videoUrl || "");
    const showReportButton = options.showReportButton !== false;
    const showFavoriteButton = options.showFavoriteButton !== false;
    const extraActionHtml = options.extraActionHtml || "";
    const images = Array.isArray(item?.images) && item.images.length
      ? item.images
      : ["/images/logo4.png"];
    const escapedImages = images.map(image => escapeAttribute(image || "/images/logo4.png"));
    const firstImage = escapedImages[0];
    const mediaCount = escapedImages.length + (videoUrl ? 1 : 0);
    const isFavorite = Boolean(item?.isFavorite);
    const suggestedListName = escapeAttribute(item?.groupName || "Inmuebles");
    const navButtons = mediaCount > 1
      ? `
          <span class="gallery-nav gallery-nav-prev" data-direction="-1" data-gallery-nav="true" role="button" tabindex="0" aria-label="Foto anterior">&#8249;</span>
          <span class="gallery-nav gallery-nav-next" data-direction="1" data-gallery-nav="true" role="button" tabindex="0" aria-label="Foto siguiente">&#8250;</span>
        `
      : "";
    const priceCluster = `
        <span class="gallery-price-cluster">
          <span class="gallery-badge gallery-tooltip-trigger gallery-tooltip-bottom" data-tooltip-label="${priceTooltipLabel}">${operationLabel && operationLetter ? `<strong class="gallery-operation-letter">${operationLetter}</strong>` : ""}${price}</span>
        </span>
      `;
    const cardContent = `
      <a href="${detailsUrl}" class="card-image-wrap gallery-card-with-caption publication-preview-trigger" data-publication-id="${publicationId}" data-details-url="${buildPublicationApiDetailsUrl(publicationId)}" data-images="${escapedImages.join("|||")}" data-video-url="${videoUrl}" data-media-index="0">
        ${videoUrl
          ? `<video src="${videoUrl}" class="gallery-carousel-video" preload="metadata" muted playsinline></video><button type="button" class="gallery-play-toggle gallery-tooltip-trigger gallery-tooltip-top" data-gallery-play-toggle="true" aria-label="Reproducir video" data-tooltip-label="Reproducir video"></button><button type="button" class="gallery-audio-toggle gallery-tooltip-trigger gallery-tooltip-side" data-gallery-audio-toggle="true" aria-label="Activar audio" data-tooltip-label="Activar audio"><i class="fa-solid fa-volume-xmark" aria-hidden="true"></i></button>`
          : `<img src="${firstImage}" alt="${title}" class="gallery-carousel-image" loading="lazy" decoding="async" />`}
        ${priceCluster}
        ${showReportButton ? `<button type="button" class="gallery-action-button gallery-report-overlay gallery-tooltip-trigger gallery-tooltip-side report-trigger" data-publication-id="${publicationId}" data-publication-code="${publicationCode}" data-publication-title="${title}" data-tooltip-label="Denunciar" aria-label="Denunciar ${title}"><span class="gallery-report-letter" aria-hidden="true">D</span></button>` : ""}
        ${navButtons}
        <span class="gallery-card-caption gallery-description-tooltip gallery-tooltip-trigger" data-tooltip-label="${descriptionTooltipLabel}" tabindex="0" aria-label="${descriptionTooltipLabel}"><span>${galleryTitle}</span></span>
        ${showFavoriteButton ? `<button type="button" class="favorite-toggle gallery-favorite-corner gallery-tooltip-trigger gallery-tooltip-side ${isFavorite ? "is-active" : ""}" data-favorite-toggle="true" data-publication-id="${publicationId}" data-publication-title="${title}" data-suggested-list-name="${suggestedListName}" data-tooltip-label="Añadir a favoritos" aria-label="Añadir a mi lista de favoritos">${renderFavoriteIcon(isFavorite)}</button>` : ""}
      </a>
      ${extraActionHtml}
    `;

    if (options.wrapCard === false) {
      return cardContent;
    }

    return `
      <article class="listing-card listing-card-compact" data-publication-id="${publicationId}"${isFirstCard ? ' id="gallery-first"' : ""}>
        ${cardContent}
      </article>
    `;
  }
  window.__ventagramBuildGalleryCard = buildGalleryCard;
  window.__ventagramWireGalleryCards = wireGalleryCards;
  window.__ventagramWireFavoriteActions = wireFavoriteActions;
  window.__ventagramWireReportForm = wireReportForm;
  window.__ventagramSyncMobileGalleryVideoAutoplay = syncMobileGalleryVideoAutoplay;

  function wireGalleryCards() {
    syncGalleryOverlayLayout();
    syncLastClickedGalleryCard();

    document.querySelectorAll(".gallery-nav").forEach(button => {
      if (button.dataset.bound === "true") return;

      button.dataset.bound = "true";
      const handleGalleryNavInteraction = event => {
        event.preventDefault();
        event.stopPropagation();

        // Mobile browsers emit a synthetic click right after touchend.  Without
        // this guard, a two-image gallery advances twice and appears unchanged.
        if (event.type === "click" && Number(button.dataset.ignoreClickUntil || 0) > Date.now()) {
          return;
        }
        if (event.type === "touchend") {
          button.dataset.ignoreClickUntil = String(Date.now() + 700);
        }

        const card = button.closest(".card-image-wrap");
        if (!card) return;
        card.dataset.suppressClickUntil = String(Date.now() + 450);
        const direction = Number(button.dataset.direction || 1);
        advanceGalleryMedia(card, direction);
        syncGalleryOverlayLayout(card?.closest(".listing-card, .map-selection-card, .card-image-wrap") || document);
      };
      button.addEventListener("click", handleGalleryNavInteraction);
      button.addEventListener("touchend", handleGalleryNavInteraction, { passive: false });
      button.addEventListener("keydown", event => {
        if (event.key !== "Enter" && event.key !== " ") return;
        handleGalleryNavInteraction(event);
      });
    });

    document.querySelectorAll("[data-gallery-audio-toggle='true']").forEach(button => {
      if (button.dataset.bound === "true") return;

      button.dataset.bound = "true";
      button.addEventListener("click", event => {
        event.preventDefault();
        event.stopPropagation();

        const card = button.closest(".card-image-wrap");
        toggleGalleryVideoAudio(card);
      });
    });

    document.querySelectorAll("[data-gallery-play-toggle='true']").forEach(button => {
      if (button.dataset.bound === "true") return;

      button.dataset.bound = "true";
      button.addEventListener("click", event => {
        event.preventDefault();
        event.stopPropagation();

        const card = button.closest(".card-image-wrap");
        toggleGalleryVideoPlayback(card);
      });
    });

    syncMobileGalleryVideoAutoplay(document);
    document.querySelectorAll(".card-image-wrap .gallery-carousel-video").forEach(video => {
      bindGalleryVideoState(video);
      syncGalleryVideoVisualState(video.closest(".card-image-wrap"));
    });
  }

  function syncGalleryOverlayLayout(root = document) {
    const cards = root.matches?.(".card-image-wrap")
      ? [root]
      : Array.from(root.querySelectorAll?.(".card-image-wrap") || []);

    cards.forEach(card => {
      const reportButton = card.querySelector(".gallery-report-overlay");
      if (!reportButton) return;
      reportButton.classList.remove("is-compact");
    });
  }

  function wireDynamicGalleryCards() {
    if (document.body.dataset.dynamicGalleryBound === "true") return;

    document.body.dataset.dynamicGalleryBound = "true";
    const autoplayVisibleVideos = () => syncMobileGalleryVideoAutoplay(document);
    let scrollResumeTimer = null;

    document.addEventListener("click", event => {
      const button = event.target.closest(".gallery-nav");
      if (!button || !button.closest(".map-popup-card")) return;

      event.preventDefault();
      event.stopPropagation();
      if (Number(button.dataset.ignoreClickUntil || 0) > Date.now()) return;

      const card = button.closest(".card-image-wrap");
      if (!card) return;
      card.dataset.suppressClickUntil = String(Date.now() + 450);
      const direction = Number(button.dataset.direction || 1);
      advanceGalleryMedia(card, direction);
    });

    document.addEventListener("touchend", event => {
      const button = event.target.closest(".gallery-nav");
      if (!button || !button.closest(".map-popup-card")) return;

      event.preventDefault();
      event.stopPropagation();

      const card = button.closest(".card-image-wrap");
      if (!card) return;
      button.dataset.ignoreClickUntil = String(Date.now() + 700);
      card.dataset.suppressClickUntil = String(Date.now() + 450);
      const direction = Number(button.dataset.direction || 1);
      advanceGalleryMedia(card, direction);
    }, { passive: false });

    document.addEventListener("keydown", event => {
      const button = event.target.closest(".gallery-nav");
      if (!button || !button.closest(".map-popup-card")) return;
      if (event.key !== "Enter" && event.key !== " ") return;

      event.preventDefault();
      event.stopPropagation();

      const card = button.closest(".card-image-wrap");
      card.dataset.suppressClickUntil = String(Date.now() + 450);
      const direction = Number(button.dataset.direction || 1);
      advanceGalleryMedia(card, direction);
    });

    document.addEventListener("click", event => {
      const trigger = event.target.closest(".card-image-wrap.publication-preview-trigger");
      if (!trigger) return;

      rememberLastClickedGalleryPublication(trigger.dataset.publicationId);
      syncLastClickedGalleryCard();

      const suppressUntil = Number(trigger.dataset.suppressClickUntil || 0);
      if (suppressUntil > Date.now()) {
        event.preventDefault();
        event.stopPropagation();
      }
    }, true);

    document.addEventListener("click", event => {
      const button = event.target.closest("[data-gallery-audio-toggle='true']");
      if (!button || !button.closest(".map-popup-card")) return;

      event.preventDefault();
      event.stopPropagation();

      const card = button.closest(".card-image-wrap");
      toggleGalleryVideoAudio(card);
    });

    document.addEventListener("click", event => {
      const button = event.target.closest("[data-gallery-play-toggle='true']");
      if (!button || !button.closest(".map-popup-card")) return;

      event.preventDefault();
      event.stopPropagation();

      const card = button.closest(".card-image-wrap");
      toggleGalleryVideoPlayback(card);
    });

    document.addEventListener("mouseover", event => {
      const video = event.target.closest(".gallery-carousel-video");
      if (!video) return;
      if (!video.closest(".card-image-wrap, .map-popup-card")) return;
      video.play?.().catch(() => {});
    });

    document.addEventListener("mouseout", event => {
      const video = event.target.closest(".gallery-carousel-video");
      if (!video) return;
      if (!video.closest(".card-image-wrap, .map-popup-card")) return;
      video.pause?.();
      syncGalleryVideoVisualState(video.closest(".card-image-wrap"));
    });

    document.addEventListener("visibilitychange", () => {
      if (!document.hidden) {
        autoplayVisibleVideos();
      }
    });

    document.addEventListener("scroll", () => {
      pauseGalleryVideosForScroll(document);
      window.clearTimeout(scrollResumeTimer);
      scrollResumeTimer = window.setTimeout(() => {
        autoplayVisibleVideos();
      }, 160);
    }, { passive: true });
    window.addEventListener("resize", autoplayVisibleVideos);

    document.addEventListener("touchstart", event => {
      if (!isMobileGallerySwipeContext()) return;

      const card = event.target.closest(".card-image-wrap");
      if (!card || event.touches.length !== 1) return;

      const touch = event.touches[0];
      card.dataset.touchStartX = String(touch.clientX);
      card.dataset.touchStartY = String(touch.clientY);
      card.dataset.touchSwiping = "false";
    }, { passive: true });

    document.addEventListener("touchmove", event => {
      if (!isMobileGallerySwipeContext()) return;

      const card = event.target.closest(".card-image-wrap");
      if (!card || event.touches.length !== 1) return;

      const startX = Number(card.dataset.touchStartX || NaN);
      const startY = Number(card.dataset.touchStartY || NaN);
      if (!Number.isFinite(startX) || !Number.isFinite(startY)) return;

      const touch = event.touches[0];
      const deltaX = touch.clientX - startX;
      const deltaY = touch.clientY - startY;

      if (Math.abs(deltaX) > 18 && Math.abs(deltaX) > Math.abs(deltaY) * 1.15) {
        card.dataset.touchSwiping = "true";
        event.preventDefault();
      }
    }, { passive: false });

    document.addEventListener("touchend", event => {
      if (!isMobileGallerySwipeContext()) return;

      const card = event.target.closest(".card-image-wrap");
      if (!card) return;

      const startX = Number(card.dataset.touchStartX || NaN);
      const startY = Number(card.dataset.touchStartY || NaN);
      const touch = event.changedTouches?.[0];
      delete card.dataset.touchStartX;
      delete card.dataset.touchStartY;

      if (!touch || !Number.isFinite(startX) || !Number.isFinite(startY)) {
        delete card.dataset.touchSwiping;
        return;
      }

      const deltaX = touch.clientX - startX;
      const deltaY = touch.clientY - startY;
      const isSwipe = Math.abs(deltaX) >= 42 && Math.abs(deltaX) > Math.abs(deltaY) * 1.2;

      if (isSwipe) {
        event.preventDefault();
        advanceGalleryMedia(card, deltaX < 0 ? 1 : -1);
        syncGalleryOverlayLayout(card?.closest(".listing-card, .map-selection-card, .card-image-wrap") || document);
        card.dataset.suppressClickUntil = String(Date.now() + 450);
      }

      delete card.dataset.touchSwiping;
    }, { passive: false });

    document.addEventListener("touchcancel", event => {
      const card = event.target.closest(".card-image-wrap");
      if (!card) return;

      delete card.dataset.touchStartX;
      delete card.dataset.touchStartY;
      delete card.dataset.touchSwiping;
    }, { passive: true });
  }

  function advanceGalleryMedia(card, direction) {
    if (!card) return;

    const images = (card.dataset.images || "")
      .split("|||")
      .map(x => x.trim())
      .filter(Boolean);
    const videoUrl = (card.dataset.videoUrl || "").trim();
    const mediaCount = images.length + (videoUrl ? 1 : 0);

    if (mediaCount <= 1) return;

    const currentIndex = Number(card.dataset.mediaIndex || 0);
    const nextIndex = (currentIndex + direction + mediaCount) % mediaCount;
    const playToggle = card.querySelector(".gallery-play-toggle");
    const currentVideo = card.querySelector(".gallery-carousel-video");
    const currentImage = card.querySelector(".gallery-carousel-image");

    if (currentVideo) {
      currentVideo.pause?.();
    }

    if (nextIndex === 0 && videoUrl) {
      if (currentImage) {
        currentImage.remove();
      }

      let video = currentVideo;
      if (!video) {
        video = document.createElement("video");
        video.className = "gallery-carousel-video";
        video.preload = "metadata";
        video.muted = true;
        video.playsInline = true;
        bindGalleryVideoState(video);
        const anchor = playToggle || card.querySelector(".gallery-badge") || card.firstChild;
        card.insertBefore(video, anchor);
      }

      video.src = videoUrl;
      mediaPreloadService.prepareVideo(videoUrl, { preload: "auto" });
      tryAutoplayGalleryVideo(video);
      card.dataset.mediaIndex = String(nextIndex);
      syncGalleryVideoAudioButton(card);
      syncGalleryVideoVisualState(card);
      mediaPreloadService.primeCardNavigation(card);
      return;
    } else {
      const imageIndex = videoUrl ? nextIndex - 1 : nextIndex;
      const nextImageUrl = images[imageIndex] || images[0] || "";
      if (!nextImageUrl) return;

      if (currentVideo) {
        currentVideo.remove();
      }

      let image = currentImage;
      if (!image) {
        image = document.createElement("img");
        image.className = "gallery-carousel-image";
        image.alt = card.querySelector(".gallery-flag")?.dataset.publicationTitle || "";
        image.loading = "eager";
        image.decoding = "async";
        const anchor = playToggle || card.querySelector(".gallery-badge") || card.firstChild;
        card.insertBefore(image, anchor);
      }

      // Change immediately.  Navigation must not wait for an Image promise;
      // the images were already warmed when the card was rendered.
      image.src = nextImageUrl;
      card.dataset.mediaIndex = String(nextIndex);
      syncGalleryVideoAudioButton(card);
      syncGalleryVideoVisualState(card);
      mediaPreloadService.primeCardNavigation(card);
      return;
    }
  }

  function isMobileGallerySwipeContext() {
    return window.matchMedia?.("(max-width: 900px)")?.matches
      || window.matchMedia?.("(pointer: coarse)")?.matches
      || navigator.maxTouchPoints > 0;
  }

  function isMobileGalleryAutoplayContext() {
    return window.matchMedia?.("(max-width: 768px)")?.matches
      || window.matchMedia?.("(pointer: coarse)")?.matches
      || navigator.maxTouchPoints > 0;
  }

  function isMobileMapInteractionContext() {
    return window.matchMedia?.("(max-width: 780px)")?.matches
      || window.matchMedia?.("(pointer: coarse)")?.matches;
  }

  function tryAutoplayGalleryVideo(video) {
    if (!video || !isMobileGalleryAutoplayContext()) return;
    video.muted = true;
    video.playsInline = true;
    video.autoplay = true;
    video.play?.().catch(() => {});
    syncGalleryVideoVisualState(video.closest(".card-image-wrap"));
  }

  function syncMobileGalleryVideoAutoplay(root = document) {
    if (!isMobileGalleryAutoplayContext()) return;

    root.querySelectorAll(".card-image-wrap .gallery-carousel-video").forEach(video => {
      const rect = video.getBoundingClientRect();
      const isVisible = rect.bottom > 0
        && rect.right > 0
        && rect.top < window.innerHeight
        && rect.left < window.innerWidth;

      if (isVisible) {
        bindGalleryVideoState(video);
        tryAutoplayGalleryVideo(video);
        syncGalleryVideoAudioButton(video.closest(".card-image-wrap"));
      } else {
        video.pause?.();
        syncGalleryVideoVisualState(video.closest(".card-image-wrap"));
      }
    });
  }

  function pauseGalleryVideosForScroll(root = document) {
    root.querySelectorAll(".card-image-wrap .gallery-carousel-video").forEach(video => {
      if (!video.paused) {
        video.pause?.();
      }
      syncGalleryVideoVisualState(video.closest(".card-image-wrap"));
    });
  }

  function toggleGalleryVideoAudio(card) {
    const video = card?.querySelector(".gallery-carousel-video");
    const button = card?.querySelector("[data-gallery-audio-toggle='true']");
    if (!video || !button) return;

    video.muted = !video.muted;
    if (!video.paused) {
      video.play?.().catch(() => {});
    } else {
      video.play?.().catch(() => {});
    }

    syncGalleryVideoAudioButton(card);
  }

  function toggleGalleryVideoPlayback(card) {
    const video = card?.querySelector(".gallery-carousel-video");
    if (!video) return;

    if (video.paused) {
      video.play?.().catch(() => {});
    } else {
      video.pause?.();
    }

    syncGalleryVideoVisualState(card);
  }

  function syncGalleryVideoAudioButton(card) {
    const button = card?.querySelector("[data-gallery-audio-toggle='true']");
    const video = card?.querySelector(".gallery-carousel-video");
    if (!button) return;

    const showingVideo = Boolean(video);
    button.hidden = !showingVideo;
    if (!showingVideo) return;

    const isMuted = video.muted;
    const label = isMuted ? "Activar audio" : "Silenciar";
    button.dataset.tooltipLabel = label;
    button.setAttribute("aria-label", label);
    button.classList.toggle("is-muted", isMuted);
    button.classList.toggle("is-unmuted", !isMuted);
    button.innerHTML = isMuted
      ? `<i class="fa-solid fa-volume-xmark" aria-hidden="true"></i>`
      : `<i class="fa-solid fa-volume-high" aria-hidden="true"></i>`;
  }

  function bindGalleryVideoState(video) {
    if (!video || video.dataset.galleryStateBound === "true") return;

    video.dataset.galleryStateBound = "true";
    const sync = () => syncGalleryVideoVisualState(video.closest(".card-image-wrap"));
    video.addEventListener("play", sync);
    video.addEventListener("pause", sync);
    video.addEventListener("ended", sync);
  }

  function syncGalleryVideoVisualState(card) {
    const video = card?.querySelector(".gallery-carousel-video");
    const playToggle = card?.querySelector(".gallery-play-toggle");
    if (!playToggle) return;

    const showingVideo = Boolean(video);
    playToggle.hidden = !showingVideo || !video.paused;
    const label = showingVideo && !video.paused ? "Pausar video" : "Reproducir video";
    playToggle.setAttribute("aria-label", label);
    playToggle.dataset.tooltipLabel = label;
  }

  async function initRealtimeChat() {
    return;
  }

  async function refreshChatUnreadCount() {
    if (document.body?.dataset.userAuthenticated !== "true") {
      return;
    }

    const url = buildChatServiceUrl("/api/chat/unread-count");
    if (!url) {
      return;
    }

    try {
      const response = await fetch(url, {
        headers: { "X-Requested-With": "fetch" },
        credentials: "include"
      });
      if (!response.ok) return;
      const payload = await response.json();
      syncChatUnreadBadges(Number(payload?.unreadCount || 0));
    } catch (error) {
      console.warn(buildChatNetworkErrorMessage(url, error));
    }
  }

  function syncChatUnreadBadges(unreadCount) {
    const safeCount = Math.max(0, Number(unreadCount || 0));
    const unreadLabel = safeCount <= 0
      ? "Mis mensajes"
      : safeCount === 1
        ? "1 mensaje sin leer"
        : `${safeCount} mensajes sin leer`;

    document.querySelectorAll("[data-chat-unread-count]").forEach(node => {
      node.textContent = String(safeCount);
      node.hidden = safeCount <= 0;
    });

    document.querySelectorAll("[data-chat-menu-link]").forEach(button => {
      const state = safeCount <= 0
        ? "0"
        : safeCount <= 3
          ? "1"
          : safeCount <= 9
            ? "2"
            : "3";
      button.dataset.chatUnreadState = state;
      button.setAttribute("aria-label", unreadLabel);
      button.setAttribute("title", unreadLabel);
    });
  }

  function wireChatExperience(root = document) {
    wireStartChatButtons(root);
    wireChatMenuRefresh();
  }

  function wireChatMenuRefresh() {
    if (document.body.dataset.chatMenuRefreshBound === "true") return;
    document.body.dataset.chatMenuRefreshBound = "true";

    document.addEventListener("toggle", event => {
      const menu = event.target;
      if (!(menu instanceof HTMLDetailsElement)) return;
      if (!menu.matches("[data-chat-menu]")) return;
      if (!menu.open) return;
      refreshChatUnreadCount().catch(console.warn);
    }, true);
  }

  function wireStartChatButtons(root = document) {
    if (document.body.dataset.chatStartBound === "true") return;

    document.body.dataset.chatStartBound = "true";
    document.addEventListener("click", async event => {
      const target = event.target instanceof Element ? event.target : event.target?.parentElement;
      const button = target?.closest?.("[data-start-chat='true']");
      if (!button) return;

      const publicationId = Number(button.dataset.publicationId || 0);
      if (publicationId <= 0) return;

      event.preventDefault();
      button.disabled = true;
      let url = "";
      try {
        url = buildChatServiceUrl("/api/chat/conversations");
        if (!url) {
          throw new Error("Configura la URL del servicio de chat.");
        }

        const response = await fetch(url, {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            "X-Requested-With": "fetch"
          },
          body: JSON.stringify({ publicationId }),
          credentials: "include"
        });

        const payload = await response.json().catch(() => ({}));
        if (!response.ok) {
          throw new Error(payload?.message || "No se pudo abrir el chat.");
        }

        window.location.href = payload.redirectUrl || "/Mensajes";
      } catch (error) {
        const fallbackMessage = buildChatNetworkErrorMessage(url, error);
        window.alert(error?.message && error.message !== "Failed to fetch" ? error.message : fallbackMessage);
      } finally {
        button.disabled = false;
      }
    }, true);
  }

  // Keep chat actions independent from the main page bootstrap. Catalog cards are
  // rendered dynamically and must remain clickable even if another initializer fails.
  const ensureChatExperienceWired = () => wireChatExperience(document);
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", ensureChatExperienceWired, { once: true });
  } else {
    ensureChatExperienceWired();
  }

  function buildChatServiceUrl(path) {
    const baseUrl = String(chatConfig.baseUrl || "").trim().replace(/\/+$/, "");
    const normalizedPath = path
      ? (path.startsWith("/") ? path : `/${path}`)
      : "";

    if (!baseUrl) {
      return normalizedPath;
    }

    if (!normalizedPath) {
      return baseUrl;
    }

    return `${baseUrl}${normalizedPath}`;
  }

  function buildChatNetworkErrorMessage(url, error) {
    const rawMessage = String(error?.message || "").trim();
    if (rawMessage && rawMessage !== "Failed to fetch") {
      return rawMessage;
    }

    const origin = getOriginLabel(url);
    if (origin && isLocalhostOrigin(origin)) {
      return `No se pudo conectar al servicio de chat en ${origin}. En debug inicia Ventagram.ChatService junto con Ventagram.Web.`;
    }

    return "No se pudo conectar con el servicio de chat.";
  }

  function getOriginLabel(url) {
    try {
      return new URL(url).origin;
    } catch {
      return "";
    }
  }

  function isLocalhostOrigin(origin) {
    try {
      const parsed = new URL(origin);
      return parsed.hostname === "localhost" || parsed.hostname === "127.0.0.1";
    } catch {
      return false;
    }
  }

  function serializeCreateForm(form) {
    const value = name => form.querySelector(`[name="${name}"]`)?.value ?? "";
    const checked = name => Boolean(form.querySelector(`[name="${name}"]`)?.checked);
    const noLocation = checked("noLocation");
    const dynamicFields = Array.from(form.querySelectorAll("[data-dynamic-input]"))
      .map(input => {
        const fieldId = Number(input.dataset.fieldId || 0);
        const fieldType = input.dataset.fieldType || "texto";
        const payload = { fieldId, valueText: null, valueNumber: null, valueBoolean: null };

        if (fieldType === "booleano") {
          payload.valueBoolean = input.value === ""
            ? null
            : String(input.value).toLowerCase() === "true";
        } else if (fieldType === "numero") {
          payload.valueNumber = input.value === "" ? null : Number(input.value);
        } else {
          payload.valueText = input.value === "" ? null : input.value;
        }

        return payload;
      })
      .filter(item => item.fieldId > 0);

    return {
      group: Number(value("group") || 0),
      categoryId: Number(value("category") || 0),
      title: value("title"),
      price: Number(value("price") || 0),
      operation: value("operation") || null,
      currency: value("currency") || "ARS",
      locality: noLocation ? "" : value("locality"),
      shortDescription: value("shortDescription"),
      longDescription: value("longDescription") || null,
      imagesCsv: value("imagesCsv"),
      videoUrl: value("videoUrl") || null,
      contactEmail: null,
      contactName: null,
      contactPhone: null,
      featured: checked("featured"),
      latitude: noLocation ? null : numberOrNull(value("latitude")),
      longitude: noLocation ? null : numberOrNull(value("longitude")),
      address: noLocation ? null : value("address") || null,
      noLocation,
      propertyType: null,
      zone: null,
      totalAreaM2: null,
      coveredAreaM2: null,
      roomsOrBedrooms: null,
      bathrooms: null,
      garageSpaces: null,
      ageYears: null,
      expenses: null,
      condition: null,
      mortgageEligible: false,
      professionalUseAllowed: false,
      services: null,
      amenities: null,
      vehicleType: null,
      brand: null,
      model: null,
      year: null,
      kilometers: null,
      fuel: null,
      transmission: null,
      version: null,
      color: null,
      licensePlate: null,
      engine: null,
      traction: null,
      doors: null,
      ownersCount: null,
      acceptsTrade: false,
      financingAvailable: false,
      equipment: null,
      generalCondition: null,
      subcategory: null,
      itemCondition: null,
      sku: null,
      stock: null,
      measure: null,
      weight: null,
      dimensions: null,
      warranty: null,
      shipping: null,
      dynamicFields,
      publisherMode: "Account"
    };
  }

  function numberOrNull(value) {
    return value === "" ? null : Number(value);
  }
})();

document.addEventListener("click", event => {
  const trigger = event.target.closest("[data-rating-popover-trigger]");
  document.querySelectorAll("[data-rating-popover].is-open").forEach(popover => {
    if (!trigger || !popover.contains(trigger)) {
      popover.classList.remove("is-open");
      popover.querySelector("[data-rating-popover-trigger]")?.setAttribute("aria-expanded", "false");
    }
  });

  if (!trigger) return;
  const popover = trigger.closest("[data-rating-popover]");
  if (!popover) return;
  const isOpen = popover.classList.toggle("is-open");
  trigger.setAttribute("aria-expanded", isOpen ? "true" : "false");
});

document.addEventListener("keydown", event => {
  if (event.key !== "Escape") return;
  document.querySelectorAll("[data-rating-popover].is-open").forEach(popover => {
    popover.classList.remove("is-open");
    const trigger = popover.querySelector("[data-rating-popover-trigger]");
    trigger?.setAttribute("aria-expanded", "false");
    trigger?.blur();
  });
});


