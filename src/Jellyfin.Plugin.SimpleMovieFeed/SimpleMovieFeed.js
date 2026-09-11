(function () {
    "use strict";

    const configurationControllerScriptId =
        "SimpleMovieFeedConfigurationControllerScript";

    let configurationControllerPage = null;

    function ensureSimpleMovieFeedConfigurationController() {
        const page = document.querySelector(
            "#SimpleMovieFeedConfigurationPage"
        );

        if (!page) {
            configurationControllerPage = null;
            return;
        }

        if (configurationControllerPage === page) {
            return;
        }

        configurationControllerPage = page;

        const previousScript =
            document.getElementById(
                configurationControllerScriptId
            );

        if (previousScript) {
            previousScript.remove();
        }

        const script = document.createElement(
            "script"
        );

        script.id = configurationControllerScriptId;
        script.src =
            "configurationpage?name=SimpleMovieFeed.Configuration.js";
        script.async = false;

        script.addEventListener(
            "load",
            function () {
                log(
                    "Configuration controller loaded"
                );
            },
            { once: true }
        );

        script.addEventListener(
            "error",
            function () {
                error(
                    "Configuration controller failed to load"
                );

                if (configurationControllerPage === page) {
                    configurationControllerPage = null;
                }

                script.remove();
            },
            { once: true }
        );

        document.head.appendChild(script);
    }

    function scheduleConfigurationControllerCheck() {
        window.setTimeout(
            ensureSimpleMovieFeedConfigurationController,
            0
        );

        window.setTimeout(
            ensureSimpleMovieFeedConfigurationController,
            250
        );

        window.setTimeout(
            ensureSimpleMovieFeedConfigurationController,
            750
        );
    }
    /*
     * Global authentication helper.
     * Needed by the Continue Watching bridge, which runs
     * outside openMovie().
     */
    function globalSimpleMovieFeedAuthHeaders(json) {
        const client =
            window.ApiClient || null;

        if (
            !client ||
            typeof client.accessToken !==
                "function"
        ) {
            throw new Error(
                "Jellyfin ApiClient/access token is unavailable."
            );
        }

        const headers = {
            "Authorization":
                "MediaBrowser Token=" +
                client.accessToken()
        };

        if (json) {
            headers["Content-Type"] =
                "application/json";
        }

        return headers;
    }


    if (window.__simpleMovieFeedLoaded) {
        return;
    }

    window.__simpleMovieFeedLoaded = true;

    const PLUGIN_NAME = "Simple Movie Feed";
    const API_BASE = "/SimpleMovieFeed";

    function log(message, ...args) {
        console.log("[SimpleMovieFeed] " + message, ...args);
    }

    function error(message, ...args) {
        console.error("[SimpleMovieFeed] " + message, ...args);
    }

    function installSimpleMovieFeedNativeResumeBridge() {
        if (
            window.__simpleMovieFeedNativeResumeBridgeInstalled
        ) {
            return;
        }

        window.__simpleMovieFeedNativeResumeBridgeInstalled =
            true;

        document.addEventListener(
            "click",
            async function (event) {
                if (!event.isTrusted) {
                    return;
                }

                const target =
                    event.target;

                if (
                    !target ||
                    typeof target.closest !==
                        "function"
                ) {
                    return;
                }

                const playButton =
                    target.closest(
                        '.btnPlay, ' +
                        '[data-action="play"]'
                    );

                if (!playButton) {
                    return;
                }

                const match =
                    window.location.hash.match(
                        /[?&]id=([^&]+)/i
                    );

                if (!match) {
                    return;
                }

                const jellyfinItemId =
                    decodeURIComponent(
                        match[1]
                    );

                const client =
                    getApiClient();

                const userId =
                    client &&
                    client.getCurrentUserId
                        ? client.getCurrentUserId()
                        : "";

                if (!userId) {
                    return;
                }

                /*
                 * Temporarily stop Jellyfin's normal Play action
                 * while we ask whether this is one of our persistent
                 * SimpleMovieFeed items.
                 */
                event.preventDefault();
                event.stopPropagation();
                event.stopImmediatePropagation();

                try {
                    const response =
                        await fetch(
                            getServerUrl() +
                            API_BASE +
                            "/resume/item/" +
                            encodeURIComponent(
                                jellyfinItemId
                            ) +
                            "?userId=" +
                            encodeURIComponent(
                                userId
                            ),
                            {
                                headers:
                                    globalSimpleMovieFeedAuthHeaders(false)
                            }
                        );

                    if (!response.ok) {
                        throw new Error(
                            "Item lookup failed: HTTP " +
                            response.status
                        );
                    }

                    const result =
                        await response.json();

                    if (
                        !result ||
                        !result.found
                    ) {
                        /*
                         * Normal Jellyfin movie. Re-issue the click.
                         * Synthetic clicks have isTrusted=false, so
                         * this handler will not intercept it again.
                         */
                        playButton.click();
                        return;
                    }

                    log(
                        "Rehydrating Continue Watching movie:",
                        result.movieId,
                        result.quality
                    );

                    /*
                     * Run through the exact same preparation path as
                     * a movie selected from What's New/Search.
                     * This recreates a deleted torrent/cache and then
                     * applies the saved per-user resume position.
                     */
                    await openMovie({
                        Id:
                            result.movieId,

                        Title:
                            result.movieTitle ||
                            "Movie",

                        Year:
                            result.year || 0,

                        StreamOptions: [
                            {
                                Quality:
                                    result.quality ||
                                    "",

                                MagnetLink:
                                    result.magnetLink,

                                Seeds:
                                    0,

                                Peers:
                                    0
                            }
                        ]
                    });
                }
                catch (err) {
                    error(
                        "Continue Watching bridge failed.",
                        err
                    );

                    /*
                     * Never break normal Jellyfin playback if our
                     * lookup/rehydration fails.
                     */
                    playButton.click();
                }
            },
            true
        );

        log(
            "Native Jellyfin Continue Watching bridge installed"
        );
    }

    installSimpleMovieFeedNativeResumeBridge();
function getApiClient() {
        return window.ApiClient || null;
    }

    function getServerUrl() {
        const api = getApiClient();

        if (api && typeof api.serverAddress === "function") {
            return api.serverAddress();
        }

        return window.location.origin;
    }

    async function apiGet(path) {
        const api = getApiClient();

        if (!api) {
            throw new Error("Jellyfin ApiClient is not available");
        }

        const response = await fetch(
            getServerUrl() + path,
            {
                method: "GET",
                headers: {
                    "Authorization":
                        "MediaBrowser Token=" +
                        api.accessToken()
                }
            }
        );

        if (!response.ok) {
            throw new Error(
                "Simple Movie Feed returned HTTP " +
                response.status
            );
        }

        return await response.json();
    }

    async function getLatestMovies() {
        return await apiGet(
            API_BASE + "/whatsnew?page=1"
        );
    }

    async function searchMovies(query) {
        return await apiGet(
            API_BASE +
            "/search?query=" +
            encodeURIComponent(query) +
            "&page=1"
        );
    }

    function createStyles() {
        if (
            document.getElementById(
                "simpleMovieFeedStyles"
            )
        ) {
            return;
        }

        const style =
            document.createElement("style");

        style.id =
            "simpleMovieFeedStyles";

        style.textContent = `
            #simpleMovieFeedSection,
#simpleMovieFeedSearchSection {
    width: 100%;
    margin-top: 2em;
    margin-bottom: 2em;
    padding-left: 3.3% !important;
    padding-right: 3.3% !important;
    box-sizing: border-box;
}

.simpleMovieFeedTitle {
    font-family: inherit;
}
.simpleMovieFeedWhatsNewCover {
    width: 180px;
    min-width: 180px;
    max-width: 180px;
    margin: 0;
    cursor: pointer;
    border-radius: 8px;
    overflow: hidden;
    background: #171717;
    box-shadow: 0 8px 30px rgba(0,0,0,.35);
    transition: transform .15s ease;
    pointer-events: auto;
}

            .simpleMovieFeedWhatsNewCover:hover {
                transform: scale(1.03);
            }

.simpleMovieFeedWhatsNewImage {
    display: block;
    width: 100%;
    aspect-ratio: 2 / 3;
    object-fit: cover;
    background:
        radial-gradient(
            circle at 50% 35%,
            rgba(0,164,220,.35),
            transparent 35%
        ),
        linear-gradient(
            145deg,
            #111827,
            #050505 55%,
            #18202b
        );
}

            .simpleMovieFeedWhatsNewCaption {
                padding: .45em .15em .35em .15em;
                font-family: inherit;
                font-size: var(
                    --simpleMovieFeedNativeFontSize,
                    1em
                );
                font-weight: var(
                    --simpleMovieFeedNativeFontWeight,
                    400
                );
                line-height: var(
                    --simpleMovieFeedNativeLineHeight,
                    1.25
                );
                text-transform: none;
                text-align: center;
            }

            /* What's New modal */

            .simpleMovieFeedModal {
                position: fixed;
                inset: 0;
                z-index: 99999;
                background: rgba(0,0,0,.88);
                overflow-y: auto;
                padding: 5vh 4vw;
                box-sizing: border-box;
            }

            .simpleMovieFeedModalInner {
                max-width: 1400px;
                margin: 0 auto;
            }

            .simpleMovieFeedModalHeader {
                display: flex;
                align-items: center;
                justify-content: space-between;
                gap: 1em;
                margin-bottom: 1.5em;
            }

            .simpleMovieFeedModalTitle {
                font-size: 2em;
                font-weight: 500;
            }

            .simpleMovieFeedClose {
                border: 0;
                background: rgba(255,255,255,.12);
                color: white;
                border-radius: 4px;
                padding: .55em .9em;
                cursor: pointer;
                font-size: 1.2em;
            }

            .simpleMovieFeedClose:hover {
                background: rgba(255,255,255,.2);
            }

            .simpleMovieFeedMovieGrid {
                display: grid;
                grid-template-columns:
                    repeat(auto-fill, 180px);
                gap: 1.2em;
            }

            /* Movie cards */

            .simpleMovieFeedCard {
                width: 180px;
                cursor: pointer;
                border-radius: .3em;
                overflow: hidden;
                background: transparent;
                transition: transform .15s ease;
                box-sizing: border-box;
                font-family: inherit;
                font-style: normal;
                text-transform: none;
            }

            .simpleMovieFeedMovieGrid
            .simpleMovieFeedCard {
                width: 180px;
            }

            .simpleMovieFeedCard:hover {
                transform: scale(1.03);
            }

            .simpleMovieFeedPoster {
                display: block;
                width: 100%;
                aspect-ratio: 2 / 3;
                object-fit: cover;
                background: #202020;
            }

            .simpleMovieFeedInfo {
                padding: .45em .15em .35em .15em;
                font-family: inherit;
                text-transform: none;
            }

            .simpleMovieFeedName {
                font-family: inherit;
                font-size: var(
                    --simpleMovieFeedNativeFontSize,
                    1em
                );
                font-weight: var(
                    --simpleMovieFeedNativeFontWeight,
                    400
                );
                line-height: var(
                    --simpleMovieFeedNativeLineHeight,
                    1.25
                );
                font-style: normal;
                text-transform: none;
                white-space: nowrap;
                overflow: hidden;
                text-overflow: ellipsis;
            }

            .simpleMovieFeedMeta {
                margin-top: .25em;
                font-family: inherit;
                font-size: .85em;
                font-weight: 400;
                line-height: 1.25;
                text-transform: none;
                opacity: .7;
            }

            /* Search */

            .simpleMovieFeedSearchBar {
                display: flex;
                gap: .6em;
                margin-bottom: 1em;
                max-width: 700px;
            }

            .simpleMovieFeedSearchInput {
                flex: 1;
                min-width: 0;
                padding: .75em 1em;
                border: 1px solid rgba(255,255,255,.25);
                border-radius: 4px;
                background: rgba(255,255,255,.08);
                color: inherit;
                font-size: 1em;
                outline: none;
            }

            .simpleMovieFeedSearchInput:focus {
                border-color:
                    rgba(255,255,255,.55);
            }

            .simpleMovieFeedSearchButton {
                padding: .75em 1.2em;
                border: 0;
                border-radius: 4px;
                cursor: pointer;
                background: #00a4dc;
                color: white;
                font-size: 1em;
            }

            .simpleMovieFeedSearchButton:hover {
                opacity: .9;
            }

.simpleMovieFeedSearchResults {
    display: grid;
    grid-template-columns:
        repeat(auto-fill, 180px);
    justify-content: start;
    gap: 1.2em;
}

            .simpleMovieFeedStatus {
                opacity: .7;
                padding: 1em 0;
            }

            @media (max-width: 600px) {
                .simpleMovieFeedSearchBar {
                    max-width: none;
                }

.simpleMovieFeedMovieGrid {
    display: grid;
    grid-template-columns:
        repeat(auto-fill, 180px);
    justify-content: start;
    gap: 1.2em;
}

                .simpleMovieFeedModal {
                    padding: 2vh 3vw;
                }
            }
        `;

        document.head.appendChild(style);
    }

    function findSimpleMovieFeedNativeHomeReference() {
        const container =
            document.querySelector(
                ".homeSectionsContainer"
            );

        if (!container) {
            return null;
        }

        const titles =
            Array.from(
                container.querySelectorAll(
                    ".sectionTitle, " +
                    ".sectionTitle-cards, " +
                    "h2, h3"
                )
            );

        const usable =
            titles.filter(
                function (candidate) {
                    return (
                        candidate &&
                        !candidate.closest(
                            "#simpleMovieFeedSection, " +
                            "#simpleMovieFeedSearchSection, " +
                            "#simpleMovieFeedSuggestionsSection"
                        ) &&
                        candidate.getBoundingClientRect()
                            .width > 0
                    );
                }
            );

        let referenceTitle =
            usable.find(
                function (candidate) {
                    const value =
                        String(
                            candidate.textContent || ""
                        )
                            .trim()
                            .toLowerCase();

                    return (
                        value === "my media" ||
                        value ===
                            "continue watching"
                    );
                }
            );

        if (!referenceTitle) {
            referenceTitle =
                usable.length
                    ? usable[0]
                    : null;
        }

        if (!referenceTitle) {
            return null;
        }

        return {
            title: referenceTitle
        };
    }

    function syncSimpleMovieFeedNativeHomeSection(
        section,
        title
    ) {
        if (!section || !title) {
            return;
        }

        const reference =
            findSimpleMovieFeedNativeHomeReference();

        if (!reference || !reference.title) {
            return;
        }

        const nativeTitle = reference.title;
        const computed =
            window.getComputedStyle(nativeTitle);

        title.style.fontFamily = computed.fontFamily;
        title.style.fontSize = computed.fontSize;
        title.style.fontWeight = computed.fontWeight;
        title.style.lineHeight = computed.lineHeight;
        title.style.letterSpacing = computed.letterSpacing;
        title.style.textTransform = computed.textTransform;
        title.style.marginTop = computed.marginTop;
        title.style.marginBottom = computed.marginBottom;

        const parent = section.parentElement;

        if (!parent) {
            return;
        }

        const parentRect =
            parent.getBoundingClientRect();

        const nativeRect =
            nativeTitle.getBoundingClientRect();

        const inset =
            Math.max(
                0,
                nativeRect.left - parentRect.left
            );

        section.style.width = "100%";
        section.style.marginLeft = "0";
        section.style.marginRight = "0";
        section.style.paddingLeft = inset + "px";
        section.style.paddingRight = inset + "px";
        section.style.boxSizing = "border-box";
    }



    function syncSimpleMovieFeedDetailsCardStyle(
        section,
        nativeMoreLikeThis
    ) {
        if (!section || !nativeMoreLikeThis) {
            return;
        }

        const nativeCards =
            Array.from(
                nativeMoreLikeThis.querySelectorAll(
                    ".card"
                )
            );

        const nativeCard =
            nativeCards.find(
                function (candidate) {
                    if (!candidate) {
                        return false;
                    }

                    const rect =
                        candidate.getBoundingClientRect();

                    return (
                        rect.width >= 80 &&
                        rect.width <= 400 &&
                        rect.height > rect.width
                    );
                }
            );

        if (!nativeCard) {
            return;
        }

        const rect =
            nativeCard.getBoundingClientRect();

        section.style.setProperty("--simpleMovieFeedDetailsCardWidth", "180px");

        const nativeText =
            nativeCard.querySelector(
                ".cardText, " +
                ".cardText-first, " +
                ".cardTextCentered"
            );

        if (nativeText) {
            const computed =
                window.getComputedStyle(
                    nativeText
                );

            section.style.setProperty(
                "--simpleMovieFeedDetailsFontSize",
                computed.fontSize
            );

            section.style.setProperty(
                "--simpleMovieFeedDetailsFontWeight",
                computed.fontWeight
            );

            section.style.setProperty(
                "--simpleMovieFeedDetailsLineHeight",
                computed.lineHeight
            );
        }
    }
    function syncSimpleMovieFeedNativeCardStyle() {
        let nativeCard = null;

        const candidates = Array.from(
            document.querySelectorAll(
                ".homeSectionsContainer .card, " +
                ".detailPageContent .card, " +
                ".itemsContainer .card"
            )
        );

        nativeCard = candidates.find(
            function (candidate) {
                if (
                    !candidate ||
                    candidate.closest(
                        "#simpleMovieFeedSection, " +
                        "#simpleMovieFeedSearchSection, " +
                        "#simpleMovieFeedModal, " +
                        "#simpleMovieFeedSuggestionsSection"
                    )
                ) {
                    return false;
                }

                const rect =
                    candidate.getBoundingClientRect();

                if (
                    rect.width < 80 ||
                    rect.width > 400 ||
                    rect.height < 100
                ) {
                    return false;
                }

                const image =
                    candidate.querySelector(
                        "img, .cardImage"
                    );

                return !!image;
            }
        );

        let width = 180;

        if (nativeCard) {
            const rect =
                nativeCard.getBoundingClientRect();

            if (
                Number.isFinite(rect.width) &&
                rect.width >= 80 &&
                rect.width <= 400
            ) {
                width = rect.width;
            }

            const nativeText =
                nativeCard.querySelector(
                    ".cardText, " +
                    ".cardText-first, " +
                    ".cardTextCentered"
                );

            if (nativeText) {
                const computed =
                    window.getComputedStyle(
                        nativeText
                    );

                document.documentElement.style.setProperty(
                    "--simpleMovieFeedNativeFontSize",
                    computed.fontSize
                );

                document.documentElement.style.setProperty(
                    "--simpleMovieFeedNativeFontWeight",
                    computed.fontWeight
                );

                document.documentElement.style.setProperty(
                    "--simpleMovieFeedNativeLineHeight",
                    computed.lineHeight
                );
            }
        }

        document.documentElement.style.setProperty(
            "--simpleMovieFeedNativeCardWidth",
            width + "px"
        );
    }
    function createCard(movie) {
        syncSimpleMovieFeedNativeCardStyle();

        const card =
            document.createElement("div");

        card.className =
            "simpleMovieFeedCard";

        const poster =
            document.createElement("img");

        poster.className =
            "simpleMovieFeedPoster";

        poster.src =
            movie.PosterUrl ||
            movie.BackdropUrl ||
            "";

        poster.alt =
            movie.Title || "";

        poster.loading = "lazy";

        const info =
            document.createElement("div");

        info.className =
            "simpleMovieFeedInfo";

        const name =
            document.createElement("div");

        name.className =
            "simpleMovieFeedName";

        name.textContent =
            movie.Title || "Untitled";

        const meta =
            document.createElement("div");

        meta.className =
            "simpleMovieFeedMeta";

        const parts = [];

        if (movie.Year) {
            parts.push(
                String(movie.Year)
            );
        }

        if (movie.Rating) {
            parts.push(
                "★ " +
                Number(movie.Rating)
                    .toFixed(1)
            );
        }

        meta.textContent =
            parts.join(" • ");

        info.appendChild(name);
        info.appendChild(meta);

        card.appendChild(poster);
        card.appendChild(info);

        card.addEventListener(
            "click",
            function () {
                openMovieDetails(
                    movie
                );
            }
        );

        return card;
    }

    function selectBestStreamOptionForDetails(
        movie
    ) {
        const options =
            movie &&
            Array.isArray(
                movie.StreamOptions
            )
                ? movie.StreamOptions
                : [];

        function resolutionOf(
            option
        ) {
            const match =
                String(
                    option &&
                    option.Quality
                        ? option.Quality
                        : ""
                ).match(
                    /(\d{3,4})p/i
                );

            return match
                ? Number(match[1])
                : 0;
        }

        return (
            options
                .filter(
                    function (option) {
                        return (
                            option &&
                            option.MagnetLink
                        );
                    }
                )
                .slice()
                .sort(
                    function (
                        left,
                        right
                    ) {
                        const resolutionDifference =
                            resolutionOf(
                                right
                            ) -
                            resolutionOf(
                                left
                            );

                        if (
                            resolutionDifference !==
                            0
                        ) {
                            return resolutionDifference;
                        }

                        return (
                            Number(
                                right.Seeds ||
                                0
                            ) -
                            Number(
                                left.Seeds ||
                                0
                            )
                        );
                    }
                )[0] ||
            null
        );
    }

    function rememberDetailsMovieMapping(
        jellyfinItemId,
        movieId
    ) {
        try {
            const api =
                getApiClient();

            if (
                !api ||
                typeof api.getCurrentUserId !==
                    "function"
            ) {
                return;
            }

            const userId =
                api.getCurrentUserId();

            if (
                !userId ||
                !jellyfinItemId ||
                !movieId
            ) {
                return;
            }

            const key =
                "SimpleMovieFeed:item:" +
                String(userId)
                    .toLowerCase() +
                ":" +
                String(jellyfinItemId)
                    .toLowerCase();

            localStorage.setItem(
                key,
                String(movieId)
            );

            log(
                "Remembered details mapping:",
                jellyfinItemId,
                "movie:",
                movieId
            );
        }
        catch (error) {
            log(
                "Unable to remember details mapping:",
                error
            );
        }
    }

    async function openMovieDetails(
        movie
    ) {
        try {
            const quality =
                selectBestStreamOptionForDetails(
                    movie
                );

            if (
                !quality ||
                !quality.MagnetLink
            ) {
                alert(
                    (movie.Title || "Movie") +
                    "\n\nNo stream option is available."
                );

                return;
            }

            const api =
                getApiClient();

            if (!api) {
                alert(
                    "Jellyfin ApiClient is not available."
                );

                return;
            }

            const oldOverlay =
                document.getElementById(
                    "simpleMovieFeedDetailsPreparing"
                );

            if (oldOverlay) {
                oldOverlay.remove();
            }

            const overlay =
                document.createElement(
                    "div"
                );

            overlay.id =
                "simpleMovieFeedDetailsPreparing";

            overlay.style.position =
                "fixed";

            overlay.style.inset =
                "0";

            overlay.style.zIndex =
                "100001";

            overlay.style.background =
                "rgba(0,0,0,.82)";

            overlay.style.display =
                "flex";

            overlay.style.alignItems =
                "center";

            overlay.style.justifyContent =
                "center";

            const box =
                document.createElement(
                    "div"
                );

            box.style.padding =
                "28px";

            box.style.width =
                "min(480px, 86vw)";

            box.style.borderRadius =
                "14px";

            box.style.background =
                "#181818";

            box.style.boxShadow =
                "0 12px 50px rgba(0,0,0,.55)";

            const title =
                document.createElement(
                    "div"
                );

            title.style.fontSize =
                "1.3em";

            title.style.fontWeight =
                "600";

            title.style.marginBottom =
                "10px";

            title.textContent =
                movie.Title ||
                "Movie";

            const status =
                document.createElement(
                    "div"
                );

            status.style.opacity =
                ".8";

            status.textContent =
                "Opening movie details…";

            box.appendChild(
                title
            );

            box.appendChild(
                status
            );

            overlay.appendChild(
                box
            );

            document.body.appendChild(
                overlay
            );

            const response =
                await fetch(
                    getServerUrl() +
                    API_BASE +
                    "/details",
                    {
                        method:
                            "POST",

                        headers:
                            globalSimpleMovieFeedAuthHeaders(
                                true
                            ),

                        body:
                            JSON.stringify({
                                UserId:
                                    api.getCurrentUserId(),

                                MovieId:
                                    movie.Id,

                                MagnetLink:
                                    quality.MagnetLink,

                                MovieTitle:
                                    movie.Title ||
                                    "Movie",

                                Year:
                                    Number(
                                        movie.Year ||
                                        0
                                    ),

                                Quality:
                                    quality.Quality ||
                                    "",
                                PosterUrl:
                                    movie.PosterUrl || "",
                                Description:
                                    movie.Description || ""
                            })
                    }
                );

            let result =
                await response.json();

            if (
                !response.ok &&
                response.status !== 202
            ) {
                throw new Error(
                    result &&
                    result.error
                        ? result.error
                        : (
                            "HTTP " +
                            response.status
                        )
                );
            }

            /*
             * A brand-new .strm can take a few seconds before
             * Jellyfin's library monitor indexes it.
             *
             * Keep the overlay open and retry automatically.
             */
            if (
                response.status === 202 ||
                !result.jellyfinItemId
            ) {
                status.textContent =
                    "Adding movie to Jellyfin library…";

                let preparedResult =
                    null;

                for (
                    let attempt = 0;
                    attempt < 30;
                    attempt++
                ) {
                    await new Promise(
                        function (resolve) {
                            setTimeout(
                                resolve,
                                1500
                            );
                        }
                    );

                    const retryResponse =
                        await fetch(
                            getServerUrl() +
                            API_BASE +
                            "/details",
                            {
                                method:
                                    "POST",

                                headers:
                                    globalSimpleMovieFeedAuthHeaders(
                                        true
                                    ),

                                body:
                                    JSON.stringify({
                                        UserId:
                                            api.getCurrentUserId(),

                                        MovieId:
                                            movie.Id,

                                        MagnetLink:
                                            quality.MagnetLink,

                                        MovieTitle:
                                            movie.Title ||
                                            "Movie",

                                        Year:
                                            Number(
                                                movie.Year ||
                                                0
                                            ),

                                        Quality:
                                            quality.Quality ||
                                            "",
                                PosterUrl:
                                    movie.PosterUrl || "",
                                Description:
                                    movie.Description || ""
                                    })
                            }
                        );

                    const retryResult =
                        await retryResponse.json();

                    if (
                        retryResponse.ok &&
                        retryResponse.status !== 202 &&
                        retryResult.jellyfinItemId
                    ) {
                        preparedResult =
                            retryResult;

                        break;
                    }

                    status.textContent =
                        "Adding movie to Jellyfin library… " +
                        "(" +
                        (attempt + 1) +
                        "/30)";
                }

                if (!preparedResult) {
                    throw new Error(
                        "Jellyfin did not index the new movie placeholder in time."
                    );
                }

                result =
                    preparedResult;
            }

            rememberDetailsMovieMapping(
                result.jellyfinItemId,
                movie.Id
            );

            overlay.remove();

            /*
             * Close the Whats New modal after opening details.
             * On Search there is no modal, so this is harmless.
             */
            const whatsNewModal =
                document.getElementById(
                    "simpleMovieFeedModal"
                );

            if (whatsNewModal) {
                whatsNewModal.remove();
            }

            window.location.hash =
                "#/details?id=" +
                encodeURIComponent(
                    result.jellyfinItemId
                );

            /*
             * Let Jellyfin render its native details page,
             * then add our streamable YTS recommendations row.
             */
            setTimeout(
                function () {
                    renderMovieSuggestions(
                        0
                    );
                },
                750
            );
        }
        catch (error) {
            const overlay =
                document.getElementById(
                    "simpleMovieFeedDetailsPreparing"
                );

            if (overlay) {
                overlay.remove();
            }

            log(
                "Unable to open movie details:",
                error
            );

            alert(
                (movie.Title || "Movie") +
                "\n\nUnable to open movie details.\n\n" +
                (
                    error &&
                    error.message
                        ? error.message
                        : error
                )
            );
        }
    }
    async function openMovie(movie) {
        const options = movie.StreamOptions || [];

        if (!options.length) {
            alert(
                (movie.Title || "Movie") +
                "\n\nNo stream options are available."
            );

            return;
        }

        function resolutionOf(option) {
            const match =
                String(
                    option &&
                    option.Quality
                        ? option.Quality
                        : ""
                ).match(
                    /(\d{3,4})p/i
                );

            return match
                ? Number(match[1])
                : 0;
        }

        const quality =
            options
                .filter(function (option) {
                    return (
                        option &&
                        option.MagnetLink
                    );
                })
                .slice()
                .sort(function (left, right) {
                    const resolutionDifference =
                        resolutionOf(right) -
                        resolutionOf(left);

                    if (
                        resolutionDifference !==
                        0
                    ) {
                        return resolutionDifference;
                    }

                    return (
                        Number(
                            right.Seeds || 0
                        ) -
                        Number(
                            left.Seeds || 0
                        )
                    );
                })[0];

        if (quality) {
            log(
                "Selected highest available quality:",
                quality.Quality,
                "seeds:",
                quality.Seeds || 0
            );
        }

        if (!quality || !quality.MagnetLink) {
            alert(
                (movie.Title || "Movie") +
                "\n\nThe selected stream has no magnet link."
            );

            return;
        }

        const api = getApiClient();

        if (!api) {
            alert(
                "Jellyfin ApiClient is not available."
            );

            return;
        }

        const previousStartup =
            window.__simpleMovieFeedStartupSession;

        if (
            previousStartup &&
            typeof previousStartup.cancel ===
                "function"
        ) {
            previousStartup.cancel(
                "superseded"
            );
        }

        const currentUserId =
            (
                api.getCurrentUserId
            )
                ? api.getCurrentUserId()
                : "";

        const startupSession = {
            id:
                (
                    window.crypto &&
                    typeof window.crypto.randomUUID ===
                        "function"
                )
                    ? window.crypto.randomUUID()
                    : (
                        "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx"
                    ).replace(
                        /[xy]/g,
                        function (character) {
                            const random =
                                Math.random() * 16 | 0;

                            const value =
                                character === "x"
                                    ? random
                                    : (
                                        random & 0x3 |
                                        0x8
                                    );

                            return value.toString(16);
                        }
                    ),
            cancelled: false,
            completed: false,
            cleanupSent: false,
            cancelReason: "",
            hash: extractHash(
                quality.MagnetLink
            ),
            abortController:
                new AbortController(),
            cancel: null
        };

        window.__simpleMovieFeedStartupSession =
            startupSession;

        const oldOverlay =
            document.getElementById(
                "simpleMovieFeedPreparing"
            );

        if (oldOverlay) {
            oldOverlay.remove();
        }

        const overlay =
            document.createElement("div");

        overlay.id =
            "simpleMovieFeedPreparing";

        overlay.style.position = "fixed";
        overlay.style.inset = "0";
        overlay.style.zIndex = "100001";
        overlay.style.background =
            "rgba(0,0,0,.86)";
        overlay.style.display = "flex";
        overlay.style.alignItems = "center";
        overlay.style.justifyContent = "center";

        const box =
            document.createElement("div");

        box.style.width = "min(520px, 86vw)";
        box.style.padding = "28px";
        box.style.background = "#181818";
        box.style.borderRadius = "14px";
        box.style.boxShadow =
            "0 12px 50px rgba(0,0,0,.55)";

        const title =
            document.createElement("div");

        title.style.fontSize = "1.35em";
        title.style.fontWeight = "600";
        title.style.marginBottom = "18px";

        title.textContent =
            "Preparing " +
            (movie.Title || "movie") +
            "…";

        const statusLine =
            document.createElement("div");

        statusLine.style.fontSize = "1em";
        statusLine.style.marginBottom = "12px";

        statusLine.textContent =
            "Starting torrent…";

        const progressOuter =
            document.createElement("div");

        progressOuter.style.height = "10px";
        progressOuter.style.background =
            "rgba(255,255,255,.16)";
        progressOuter.style.borderRadius = "999px";
        progressOuter.style.overflow = "hidden";
        progressOuter.style.marginBottom = "14px";

        const progressInner =
            document.createElement("div");

        progressInner.style.width = "0%";
        progressInner.style.height = "100%";
        progressInner.style.background =
            "currentColor";
        progressInner.style.transition =
            "width .25s ease";

        progressOuter.appendChild(
            progressInner
        );

        const details =
            document.createElement("div");

        details.style.opacity = ".8";
        details.style.fontSize = ".9em";
        details.style.lineHeight = "1.6";

        details.textContent =
            "Waiting for qBittorrent…";

        const activityLine =
            document.createElement("div");

        activityLine.style.opacity = ".72";
        activityLine.style.fontSize = ".85em";
        activityLine.style.marginTop = "10px";

        const startupDisplayStartedAt =
            Date.now();

        function formatElapsed(milliseconds) {
            const totalSeconds =
                Math.max(
                    0,
                    Math.floor(
                        Number(milliseconds || 0) /
                        1000
                    )
                );

            const minutes =
                Math.floor(
                    totalSeconds / 60
                );

            const seconds =
                totalSeconds % 60;

            return (
                String(minutes).padStart(2, "0") +
                ":" +
                String(seconds).padStart(2, "0")
            );
        }

        function updateActivityLine() {
            activityLine.textContent =
                "Active for " +
                formatElapsed(
                    Date.now() -
                    startupDisplayStartedAt
                ) +
                " — checking qBittorrent every second";
        }

        updateActivityLine();

        const cancelButton =
            document.createElement("button");

        cancelButton.type = "button";
        cancelButton.textContent = "Cancel";
        cancelButton.style.marginTop = "18px";
        cancelButton.style.padding =
            "10px 18px";
        cancelButton.style.border = "0";
        cancelButton.style.borderRadius =
            "6px";
        cancelButton.style.cursor = "pointer";
        cancelButton.style.fontSize = "1em";

        cancelButton.addEventListener(
            "click",
            function () {
                cancelStartup("user");
            }
        );

        box.appendChild(title);
        box.appendChild(statusLine);
        box.appendChild(progressOuter);
        box.appendChild(details);
        box.appendChild(activityLine);
        box.appendChild(cancelButton);

        overlay.appendChild(box);
        document.body.appendChild(overlay);

        function sleep(ms) {
            return new Promise(function (resolve) {
                setTimeout(resolve, ms);
            });
        }

        function isStartupCancelled() {
            return (
                startupSession.cancelled ||
                (
                    window.__simpleMovieFeedStartupSession !==
                    startupSession
                )
            );
        }

        function throwIfStartupCancelled() {
            if (!isStartupCancelled()) {
                return;
            }

            const cancellationError =
                new Error(
                    "Movie startup cancelled."
                );

            cancellationError.name =
                "AbortError";

            throw cancellationError;
        }

        async function cleanupCancelledStartup() {
            if (
                startupSession.cleanupSent ||
                startupSession.completed ||
                !startupSession.hash ||
                !currentUserId
            ) {
                return;
            }

            startupSession.cleanupSent = true;

            const cancelUrl =
                getServerUrl() +
                API_BASE +
                "/stream/cancel/" +
                encodeURIComponent(
                    startupSession.hash
                ) +
                "?userId=" +
                encodeURIComponent(
                    currentUserId
                ) +
                "&movieId=" +
                encodeURIComponent(
                    movie.Id || 0
                ) +
                "&startupId=" +
                encodeURIComponent(
                    startupSession.id
                );

            try {
                const response =
                    await fetch(
                        cancelUrl,
                        {
                            method: "POST",
                            headers:
                                authHeaders(false)
                        }
                    );

                if (!response.ok) {
                    log(
                        "Cancelled startup cleanup returned HTTP",
                        response.status
                    );
                }
            }
            catch (cleanupError) {
                log(
                    "Cancelled startup cleanup failed:",
                    cleanupError
                );
            }
        }

        function cancelStartup(reason) {
            if (
                startupSession.cancelled ||
                startupSession.completed
            ) {
                return;
            }

            startupSession.cancelled = true;
            startupSession.cancelReason =
                reason || "cancelled";

            try {
                startupSession
                    .abortController
                    .abort();
            }
            catch (_) {
            }

            if (
                overlay &&
                overlay.parentNode
            ) {
                overlay.remove();
            }

            if (
                window.__simpleMovieFeedStartupSession ===
                startupSession
            ) {
                window.__simpleMovieFeedStartupSession =
                    null;
            }

            cleanupCancelledStartup();
        }

        startupSession.cancel =
            cancelStartup;

        function formatBytesPerSecond(value) {
            value = Number(value) || 0;

            if (value <= 0) {
                return "0 B/s";
            }

            const units = [
                "B/s",
                "KB/s",
                "MB/s",
                "GB/s"
            ];

            let index = 0;

            while (
                value >= 1024 &&
                index < units.length - 1
            ) {
                value /= 1024;
                index++;
            }

            return (
                value.toFixed(
                    index >= 2 ? 1 : 0
                ) +
                " " +
                units[index]
            );
        }

        function formatEta(seconds) {
            seconds = Number(seconds) || 0;

            if (
                seconds <= 0 ||
                seconds >= 8640000
            ) {
                return "—";
            }

            if (seconds < 60) {
                return (
                    Math.ceil(seconds) +
                    "s"
                );
            }

            if (seconds < 3600) {
                return (
                    Math.floor(seconds / 60) +
                    "m " +
                    Math.floor(seconds % 60) +
                    "s"
                );
            }

            return (
                Math.floor(seconds / 3600) +
                "h " +
                Math.floor(
                    (seconds % 3600) / 60
                ) +
                "m"
            );
        }

        function extractHash(magnet) {
            const match =
                /[?&]xt=urn:btih:([^&]+)/i
                    .exec(magnet);

            if (!match) {
                return null;
            }

            try {
                return decodeURIComponent(
                    match[1]
                );
            }
            catch (_) {
                return match[1];
            }
        }

        function authHeaders(json) {
            const headers = {
                "Authorization":
                    "MediaBrowser Token=" +
                    api.accessToken()
            };

            if (json) {
                headers["Content-Type"] =
                    "application/json";
            }

            return headers;
        }

        async function seedJellyfinResumePosition(
            jellyfinItemId,
            resumeTicks
        ) {
            const ticks =
                Number(resumeTicks) || 0;

            if (
                !jellyfinItemId ||
                ticks <= 0
            ) {
                return;
            }

            const response =
                await fetch(
                    getServerUrl() +
                    "/UserItems/" +
                    encodeURIComponent(
                        jellyfinItemId
                    ) +
                    "/UserData",
                    {
                        method: "POST",

                        headers:
                            authHeaders(true),

                        body:
                            JSON.stringify({
                                PlaybackPositionTicks:
                                    ticks
                            })
                    }
                );

            if (!response.ok) {
                const responseText =
                    await response.text();

                throw new Error(
                    "Unable to set Jellyfin resume position: " +
                    response.status +
                    " " +
                    responseText
                );
            }

            log(
                "Jellyfin native resume position set:",
                ticks
            );
        }
        async function applyResumePosition(
            resumeTicks
        ) {
            const ticks =
                Number(resumeTicks) || 0;

            if (ticks <= 0) {
                return;
            }

            const seconds =
                ticks / 10000000;

            const deadline =
                Date.now() + 30000;

            let lastSeekAttempt = 0;

            while (Date.now() < deadline) {
                const videos =
                    Array.from(
                        document.querySelectorAll(
                            "video"
                        )
                    );

                const video =
                    videos.find(
                        element =>
                            element.offsetParent !== null
                    ) ||
                    videos[0];

                if (
                    video &&
                    video.readyState >= 1 &&
                    Number.isFinite(video.duration) &&
                    video.duration > 0
                ) {
                    const difference =
                        Math.abs(
                            video.currentTime -
                            seconds
                        );

                    if (difference <= 0.75) {
                        log(
                            "Resume seek confirmed:",
                            video.currentTime,
                            "seconds"
                        );

                        return;
                    }

                    if (
                        Date.now() -
                            lastSeekAttempt >=
                            750
                    ) {
                        try {
                            video.currentTime =
                                Math.min(
                                    seconds,
                                    Math.max(
                                        0,
                                        video.duration - 1
                                    )
                                );

                            lastSeekAttempt =
                                Date.now();

                            log(
                                "Resume seek attempt:",
                                seconds,
                                "current:",
                                video.currentTime
                            );
                        }
                        catch (error) {
                            log(
                                "Resume seek failed:",
                                error
                            );
                        }
                    }
                }

                await sleep(250);
            }

            log(
                "Resume seek timed out."
            );
        }

        async function playNative(
            jellyfinItemId
        ) {
            if (!jellyfinItemId) {
                throw new Error(
                    "Jellyfin item ID is unavailable."
                );
            }

            if (resumePositionTicks > 0) {
                await seedJellyfinResumePosition(
                    jellyfinItemId,
                    resumePositionTicks
                );
            }

            const movieFeedModal =
                document.getElementById(
                    "simpleMovieFeedModal"
                );

            if (movieFeedModal) {
                movieFeedModal.remove();
            }

            overlay.remove();

            const targetHash =
                "#/details?id=" +
                encodeURIComponent(
                    jellyfinItemId
                );

            if (
                window.location.hash !==
                targetHash
            ) {
                window.location.hash =
                    targetHash;
            }

            const deadline =
                Date.now() +
                15000;

            while (
                Date.now() <
                deadline
            ) {
                const playButton =
                    document.querySelector(
                        '.btnPlay, ' +
                        '[data-action="play"]'
                    );

                if (
                    playButton &&
                    playButton.offsetParent !==
                        null
                ) {
            playButton.click();

            if (
                resumePositionTicks > 0
            ) {
                log(
                    "Applying exact saved resume:",
                    resumePositionTicks
                );

                await sleep(1000);

                await applyResumePosition(
                    resumePositionTicks
                );
            }

            return;
                }

                await sleep(250);
            }

            throw new Error(
                "Jellyfin Play button did not appear."
            );
        }
        let hash =
            startupSession.hash;

        let libraryPath = null;
        let jellyfinItemId = null;
        let resumePositionTicks = 0;

        let downloadedBytes = 0;

        let firstTorrentStatusSeen = false;
        let resumeCacheAlreadyReady = false;

        let startupBufferMiB = 256;

        try {
            const runtimeResponse =
                await fetchWithAuth(
                    "/SimpleMovieFeed/runtime",
                    { method: "GET" }
                );

            if (runtimeResponse.ok) {
                const runtimeSettings =
                    await runtimeResponse.json();

                const configuredBuffer =
                    Number(
                        runtimeSettings.startupBufferMiB
                    );

                if (
                    Number.isFinite(configuredBuffer) &&
                    configuredBuffer >= 1
                ) {
                    startupBufferMiB =
                        configuredBuffer;
                }
            }
        }
        catch (error) {
            console.warn(
                "SimpleMovieFeed: unable to read runtime buffer setting.",
                error
            );
        }

        const minimumStartupBytes =
            startupBufferMiB * 1024 * 1024;

        let startInFlight = false;
        let startError = null;
        let nextStartAttempt = 0;

        let mediaUpdateSent = false;
        let playbackStarted = false;

        async function notifyJellyfin(
            path
        ) {
            if (
                mediaUpdateSent ||
                !path
            ) {
                return;
            }

            mediaUpdateSent = true;

            try {
                await fetch(
                    getServerUrl() +
                    "/Library/Media/Updated",
                    {
                        method: "POST",

                        headers:
                            authHeaders(true),

                        body: JSON.stringify({
                            Updates: [
                                {
                                    Path: path,
                                    UpdateType:
                                        "Created"
                                }
                            ]
                        })
                    }
                );
            }
            catch (err) {
                error(
                    "Unable to notify Jellyfin about new media.",
                    err
                );
            }
        }

        async function startOrPrepare() {
            throwIfStartupCancelled();

            if (startInFlight) {
                return;
            }

            startInFlight = true;
            startError = null;

            try {
                const response =
                    await fetch(
                        getServerUrl() +
                        API_BASE +
                        "/stream/start",
                        {
                            method: "POST",

                            signal:
                                startupSession
                                    .abortController
                                    .signal,

                            headers:
                                authHeaders(true),

                            body:
                                JSON.stringify({
                                    UserId:
                                        currentUserId,


                                    StartupId:
                                        startupSession.id,

                                    MovieId:
                                        movie.Id || 0,

                                    MagnetLink:
                                        quality.MagnetLink,

                                    MovieTitle:
                                        movie.Title ||
                                        "Movie",

                                    Year:
                                        movie.Year || 0,

                                    Quality:
                                        quality.Quality ||
                                        "",
                                PosterUrl:
                                    movie.PosterUrl || "",
                                Description:
                                    movie.Description || ""
                                })
                        }
                    );

                const result =
                    await response.json();

                if (
                    !response.ok &&
                    response.status !== 202
                ) {
                    throw new Error(
                        result.error ||
                        "HTTP " +
                        response.status
                    );
                }

                if (result.gid) {
                    hash = result.gid;
                    startupSession.hash =
                        result.gid;
                }

                if (result.libraryPath) {
                    libraryPath =
                        result.libraryPath;

                    await notifyJellyfin(
                        libraryPath
                    );
                }

                if (
                    result.jellyfinItemId
                ) {
                    jellyfinItemId =
                        result.jellyfinItemId;
                }

                if (
                    result.resumePositionTicks
                ) {
                    resumePositionTicks =
                        Number(
                            result.resumePositionTicks
                        ) || 0;
                }
            }
            catch (err) {
                startError = err;
            }
            finally {
                startInFlight = false;

                nextStartAttempt =
                    Date.now() + 2000;
            }
        }

        try {
            statusLine.textContent =
                "Starting torrent…";

            details.textContent =
                "Waiting for qBittorrent to accept the torrent…";

            await startOrPrepare();

            if (startError) {
                throw startError;
            }

            const startedAt =
                Date.now();

            while (!playbackStarted) {
                throwIfStartupCancelled();

                updateActivityLine();

                if (startError) {
                    throw startError;
                }

                if (
                    jellyfinItemId &&
                    downloadedBytes >=
                        minimumStartupBytes
                ) {
                    playbackStarted = true;

                    statusLine.textContent =
                        "Starting Jellyfin player…";

                    startupSession.completed =
                        true;

                    if (
                        window.__simpleMovieFeedStartupSession ===
                        startupSession
                    ) {
                        window.__simpleMovieFeedStartupSession =
                            null;
                    }

                    await playNative(
                        jellyfinItemId
                    );

                    return;
                }

                if (
                    !startInFlight &&
                    !libraryPath &&
                    Date.now() >=
                        nextStartAttempt
                ) {
                    startOrPrepare();
                }

                if (hash) {
                    let statusUrl =
                        getServerUrl() +
                        API_BASE +
                        "/stream/status/" +
                        encodeURIComponent(hash);

                    if (libraryPath) {
                        statusUrl +=
                            "?libraryPath=" +
                            encodeURIComponent(
                                libraryPath
                            );
                    }

                    try {
                        const response =
                            await fetch(
                                statusUrl,
                                {
                                    signal:
                                        startupSession
                                            .abortController
                                            .signal,

                                    headers:
                                        authHeaders(false)
                                }
                            );

                        if (!response.ok) {
                            const responseText =
                                await response.text();

                            statusLine.textContent =
                                "Status request failed: HTTP " +
                                response.status;

                            details.textContent =
                                responseText ||
                                statusUrl;
                        }

                        if (response.ok) {
                            const status =
                                await response.json();

                            throwIfStartupCancelled();

                            if (!status.found) {
                                statusLine.textContent =
                                    "Torrent not found in qBittorrent";

                                details.textContent =
                                    "Hash: " + hash;
                            }

                            if (status.found) {
                                const percent =
                                    Number(
                                        status.percent ||
                                        0
                                    );

                                progressInner
                                    .style.width =
                                    Math.max(
                                        0,
                                        Math.min(
                                            100,
                                            percent
                                        )
                                    ) +
                                    "%";

                                statusLine.textContent =
                                    percent.toFixed(1) +
                                    "% cached";

                                details.textContent =
                                    formatBytesPerSecond(
                                        status.downloadSpeed
                                    ) +
                                    "  •  ETA " +
                                    formatEta(
                                        status.eta
                                    ) +
                                    "  •  " +
                                    (
                                        status.state ||
                                        "working"
                                    );

                                details.textContent =
                                    "State: " +
                                    (
                                        status.state ||
                                        "unknown"
                                    ) +
                                    " | Peers: " +
                                    Number(
                                        status.peers || 0
                                    ) +
                                    " | Seeds: " +
                                    Number(
                                        status.seeds || 0
                                    ) +
                                    " | " +
                                    formatBytesPerSecond(
                                        status.downloadSpeed
                                    ) +
                                    " | ETA " +
                                    formatEta(
                                        status.eta
                                    );

                                if (
                                    percent <= 0 &&
                                    Number(
                                        status.peers || 0
                                    ) <= 0 &&
                                    Number(
                                        status.seeds || 0
                                    ) <= 0 &&
                                    Number(
                                        status.downloadSpeed || 0
                                    ) <= 0
                                ) {
                                    details.textContent =
                                        "Waiting for peers — no peers or seeders are currently available. " +
                                        details.textContent;
                                }

                                downloadedBytes =
                                    Number(
                                        status.downloaded
                                    ) || 0;

                                if (
                                    status.jellyfinItemId
                                ) {
                                    jellyfinItemId =
                                        status
                                            .jellyfinItemId;

                                    if (
                                        downloadedBytes <
                                            minimumStartupBytes
                                    ) {
                                        statusLine.textContent =
                                            "Buffering before playback…";

                                        details.textContent =
                                            (
                                                downloadedBytes /
                                                1024 /
                                                1024
                                            ).toFixed(1) +
                                            " / " +
                                            startupBufferMiB +
                                            " MiB cached" +
                                            " | State: " +
                                            (
                                                status.state ||
                                                "unknown"
                                            ) +
                                            " | Peers: " +
                                            Number(
                                                status.peers || 0
                                            ) +
                                            " | Seeds: " +
                                            Number(
                                                status.seeds || 0
                                            ) +
                                            " | " +
                                            formatBytesPerSecond(
                                                status.downloadSpeed
                                            );
                                    }

                                    continue;
                                }

                                if (
                                    percent >= 100 &&
                                    libraryPath
                                ) {
                                    statusLine.textContent =
                                        "Downloaded — Jellyfin is indexing…";
                                }
                                else if (
                                    libraryPath
                                ) {
                                    statusLine.textContent =
                                        percent.toFixed(1) +
                                        "% cached — Jellyfin is indexing…";
                                }
                            }
                        }
                    }
                    catch (statusError) {
                        log(
                            "Status poll failed:",
                            statusError
                        );
                    }
                }
                else {
                    details.textContent =
                        "Waiting for torrent metadata…";
                }

                if (
                    Date.now() - startedAt >
                    5 * 60 * 1000
                ) {
                    throw new Error(
                        "Timed out waiting for Jellyfin to prepare the movie."
                    );
                }

                await sleep(1000);
            }
        }
        catch (err) {
            if (isStartupCancelled()) {
                log(
                    "Movie startup cancelled:",
                    startupSession.cancelReason
                );

                return;
            }

            if (
                overlay &&
                overlay.parentNode
            ) {
                overlay.remove();
            }

            error(
                "Unable to start movie stream.",
                err
            );

            alert(
                (movie.Title || "Movie") +
                "\n\nUnable to start streaming.\n\n" +
                (err.message || err)
            );
        }
    }
    function showWhatsNewModal(movies) {
        const existing =
            document.getElementById(
                "simpleMovieFeedModal"
            );

        if (existing) {
            existing.remove();
        }

        const modal =
            document.createElement("div");

        modal.id =
            "simpleMovieFeedModal";

        modal.className =
            "simpleMovieFeedModal";

        const inner =
            document.createElement("div");

        inner.className =
            "simpleMovieFeedModalInner";

        const header =
            document.createElement("div");

        header.className =
            "simpleMovieFeedModalHeader";

        const title =
            document.createElement("div");

        title.className =
            "simpleMovieFeedModalTitle";

        title.textContent =
            "What's New";

        const close =
            document.createElement("button");

        close.className =
            "simpleMovieFeedClose";

        close.textContent =
            "✕";

        close.addEventListener(
            "click",
            function () {
                modal.remove();
            }
        );

        header.appendChild(title);
        header.appendChild(close);

        const grid =
            document.createElement("div");

        grid.className =
            "simpleMovieFeedMovieGrid";

        if (!movies || !movies.length) {
            const empty =
                document.createElement("div");

            empty.className =
                "simpleMovieFeedStatus";

            empty.textContent =
                "No movies found.";

            grid.appendChild(empty);
        } else {
            movies.forEach(
                function (movie) {
                    grid.appendChild(
                        createCard(movie)
                    );
                }
            );
        }

        inner.appendChild(header);
        inner.appendChild(grid);

        modal.appendChild(inner);
        document.body.appendChild(modal);

        modal.addEventListener(
            "click",
            function (event) {
                if (event.target === modal) {
                    modal.remove();
                }
            }
        );
    }

    async function renderWhatsNew(container) {
        if (
            document.getElementById(
                "simpleMovieFeedSection"
            )
        ) {
            return;
        }

        const section =
            document.createElement("section");

        section.id =
            "simpleMovieFeedSection";

        const title =
            document.createElement("div");

        title.className =
            "simpleMovieFeedTitle";

        title.textContent =
            "What's New";

        const cover =
            document.createElement("div");

        cover.className =
            "simpleMovieFeedWhatsNewCover";

        const image =
            document.createElement("img");

        image.className =
            "simpleMovieFeedWhatsNewImage";

        const caption =
            document.createElement("div");

        caption.className =
            "simpleMovieFeedWhatsNewCaption";

        caption.textContent =
            "What's New";

        image.alt =
            "What's New";

        cover.appendChild(image);
        cover.appendChild(caption);

        section.appendChild(title);
        section.appendChild(cover);

        container.appendChild(section);

        syncSimpleMovieFeedNativeHomeSection(
            section,
            title
        );

        try {
            const movies =
                await getLatestMovies();

            window.__simpleMovieFeedLatestMovies =
                movies;

            if (movies && movies.length) {
                const latest =
                    movies[0];

                image.src =
                    latest.PosterUrl ||
                    latest.BackdropUrl ||
                    "";

                image.alt =
                    (latest.Title || "Latest movie") +
                    " — What's New";
            }

            log(
                "Loaded " +
                (movies || []).length +
                " latest movies."
            );
        } catch (err) {
            const preparingOverlay =
                document.getElementById(
                    "simpleMovieFeedPreparing"
                );

            if (preparingOverlay) {
                preparingOverlay.remove();
            }

            error(
                "Unable to load What's New.",
                err
            );

            caption.textContent =
                "What's New";
        }

        cover.addEventListener(
            "click",
            function () {
                if (
                    window.__simpleMovieFeedLatestMovies
                ) {
                    showWhatsNewModal(
                        window.__simpleMovieFeedLatestMovies
                    );
                }
            }
        );
    }

    function createSearchSection(container) {
        if (
            document.getElementById(
                "simpleMovieFeedSearchSection"
            )
        ) {
            return;
        }

        const section =
            document.createElement("section");

        section.id =
            "simpleMovieFeedSearchSection";

        const title =
            document.createElement("div");

        title.className =
            "simpleMovieFeedTitle";

        title.textContent =
            "Search Movies";

        const searchBar =
            document.createElement("form");

        searchBar.className =
            "simpleMovieFeedSearchBar";

        const input =
            document.createElement("input");

        input.className =
            "simpleMovieFeedSearchInput";

        input.type =
            "search";

        input.placeholder =
            "Search for a movie…";

        input.autocomplete =
            "off";

        const button =
            document.createElement("button");

        button.className =
            "simpleMovieFeedSearchButton";

        button.type =
            "submit";

        button.textContent =
            "Search";

        searchBar.appendChild(input);
        searchBar.appendChild(button);

        const results =
            document.createElement("div");

        results.className =
            "simpleMovieFeedSearchResults";

        section.appendChild(title);
        section.appendChild(searchBar);
        section.appendChild(results);

        container.appendChild(section);

        syncSimpleMovieFeedNativeHomeSection(
            section,
            title
        );

        searchBar.addEventListener(
            "submit",
            async function (event) {
                event.preventDefault();

                const query =
                    input.value.trim();

                if (!query) {
                    results.innerHTML = "";

                    const message =
                        document.createElement(
                            "div"
                        );

                    message.className =
                        "simpleMovieFeedStatus";

                    message.textContent =
                        "Enter a movie title to search.";

                    results.appendChild(
                        message
                    );

                    return;
                }

                button.disabled =
                    true;

                button.textContent =
                    "Searching…";

                results.innerHTML = "";

                const loading =
                    document.createElement(
                        "div"
                    );

                loading.className =
                    "simpleMovieFeedStatus";

                loading.textContent =
                    "Searching for \"" +
                    query +
                    "\"…";

                results.appendChild(
                    loading
                );

                try {
                    const movies =
                        await searchMovies(
                            query
                        );

                    results.innerHTML =
                        "";

                    if (
                        !movies ||
                        !movies.length
                    ) {
                        const empty =
                            document.createElement(
                                "div"
                            );

                        empty.className =
                            "simpleMovieFeedStatus";

                        empty.textContent =
                            "No movies found.";

                        results.appendChild(
                            empty
                        );

                        return;
                    }

                    movies.forEach(
                        function (movie) {
                            results.appendChild(
                                createCard(
                                    movie
                                )
                            );
                        }
                    );

                    log(
                        "Search \"" +
                        query +
                        "\" returned " +
                        movies.length +
                        " movies."
                    );
                } catch (err) {
                    error(
                        "Search failed.",
                        err
                    );

                    results.innerHTML =
                        "";

                    const failed =
                        document.createElement(
                            "div"
                        );

                    failed.className =
                        "simpleMovieFeedStatus";

                    failed.textContent =
                        "Unable to search for movies.";

                    results.appendChild(
                        failed
                    );
                } finally {
                    button.disabled =
                        false;

                    button.textContent =
                        "Search";
                }
            }
        );
    }

    async function getMovieSuggestions(
        movieId
    ) {
        return await apiGet(
            API_BASE +
            "/suggestions/" +
            encodeURIComponent(
                movieId
            )
        );
    }

    function getMappedYtsMovieId(
        jellyfinItemId
    ) {
        try {
            const api =
                getApiClient();

            if (
                !api ||
                !api.getCurrentUserId
            ) {
                return null;
            }

            const userId =
                api.getCurrentUserId();

            if (
                !userId ||
                !jellyfinItemId
            ) {
                return null;
            }

            const key =
                "SimpleMovieFeed:item:" +
                String(userId)
                    .toLowerCase() +
                ":" +
                String(jellyfinItemId)
                    .toLowerCase();

            return localStorage.getItem(
                key
            );
        }
        catch (error) {
            log(
                "Unable to read SimpleMovieFeed item mapping:",
                error
            );

            return null;
        }
    }

    function getVisibleDetailsContainer() {
        const candidates =
            Array.from(
                document.querySelectorAll(
                    "#itemDetailPage .detailPageContent, " +
                    ".itemDetailPage .detailPageContent, " +
                    ".detailPageContent, " +
                    "#itemDetailPage, " +
                    ".itemDetailPage"
                )
            );

        return (
            candidates.find(
                function (element) {
                    return (
                        element &&
                        element.offsetParent !== null
                    );
                }
            ) ||
            null
        );
    }

    function findJellyfinMoreLikeThisSection(
        container
    ) {
        if (!container) {
            return null;
        }

        const candidates =
            Array.from(
                container.querySelectorAll(
                    "section, " +
                    ".verticalSection, " +
                    ".detailSection, " +
                    ".detailVerticalSection"
                )
            );

        return (
            candidates.find(
                function (section) {
                    if (
                        !section ||
                        section.id ===
                            "simpleMovieFeedSuggestionsSection"
                    ) {
                        return false;
                    }

                    const headings =
                        Array.from(
                            section.querySelectorAll(
                                "h1, h2, h3, " +
                                ".sectionTitle, " +
                                ".sectionTitle-cards"
                            )
                        );

                    return headings.some(
                        function (heading) {
                            return (
                                String(
                                    heading.textContent ||
                                    ""
                                )
                                    .trim()
                                    .toLowerCase() ===
                                "more like this"
                            );
                        }
                    );
                }
            ) ||
            null
        );
    }
    async function renderMovieSuggestions(
        attempt
    ) {
        attempt =
            Number(attempt) || 0;

        const hash =
            window.location.hash ||
            "";

        const match =
            /[?&]id=([^&]+)/i.exec(
                hash
            );

        const existing =
            document.getElementById(
                "simpleMovieFeedSuggestionsSection"
            );

        if (!match) {
            if (existing) {
                existing.remove();
            }

            return;
        }

        const jellyfinItemId =
            decodeURIComponent(
                match[1]
            );

        const movieId =
            getMappedYtsMovieId(
                jellyfinItemId
            );

        /*
         * Normal Jellyfin item.
         * Do not add anything.
         */
        if (!movieId) {
            if (existing) {
                existing.remove();
            }

            return;
        }

        /*
         * Same details page already rendered.
         */
        if (
            existing &&
            existing.dataset &&
            existing.dataset.jellyfinItemId ===
                String(jellyfinItemId)
                    .toLowerCase()
        ) {
            return;
        }

        const container =
            getVisibleDetailsContainer();

        /*
         * Jellyfin details page may still be rendering.
         */
        if (!container) {
            if (attempt < 20) {
                setTimeout(
                    function () {
                        renderMovieSuggestions(
                            attempt + 1
                        );
                    },
                    400
                );
            }

            return;
        }

        if (existing) {
            existing.remove();
        }

        const section =
            document.createElement(
                "section"
            );

        section.id =
            "simpleMovieFeedSuggestionsSection";

        section.dataset.jellyfinItemId =
            String(jellyfinItemId)
                .toLowerCase();

        const nativeMoreLikeThis =
            findJellyfinMoreLikeThisSection(
                container
            );

        syncSimpleMovieFeedDetailsCardStyle(
            section,
            nativeMoreLikeThis
        );

        /*
         * Match Jellyfin's details layout instead of using
         * the standalone SimpleMovieFeed home-page width.
         */
        section.style.width =
            "";

        section.style.margin =
            "1.5em 0";

        if (
            nativeMoreLikeThis &&
            nativeMoreLikeThis.className
        ) {
            section.className =
                nativeMoreLikeThis.className;
        }

        const title =
            document.createElement(
                "div"
            );

        const nativeTitle =
            nativeMoreLikeThis
                ? nativeMoreLikeThis.querySelector(
                    "h1, h2, h3, " +
                    ".sectionTitle, " +
                    ".sectionTitle-cards"
                )
                : null;

        title.className =
            nativeTitle &&
            nativeTitle.className
                ? nativeTitle.className
                : "sectionTitle sectionTitle-cards";

        title.textContent =
            "Streamable recommendations";

        const results =
            document.createElement(
                "div"
            );

        results.className =
            "simpleMovieFeedMovieGrid";

        results.style.justifyContent =
            "start";

        results.style.gridTemplateColumns = "repeat(auto-fill, 180px)";

        results.style.gap =
            "1em";

        const loading =
            document.createElement(
                "div"
            );

        loading.className =
            "simpleMovieFeedStatus";

        loading.textContent =
            "Loading movie suggestions…";

        results.appendChild(
            loading
        );

        section.appendChild(
            title
        );

        section.appendChild(
            results
        );

        if (
            nativeMoreLikeThis &&
            nativeMoreLikeThis.parentNode
        ) {
            nativeMoreLikeThis.insertAdjacentElement(
                "afterend",
                section
            );
        }
        else {
            container.appendChild(
                section
            );
        }

        try {
            const movies =
                await getMovieSuggestions(
                    movieId
                );

            /*
             * User may have navigated to another movie
             * while the request was running.
             */
            if (
                !section.isConnected ||
                section.dataset.jellyfinItemId !==
                    String(jellyfinItemId)
                        .toLowerCase()
            ) {
                return;
            }

            results.innerHTML =
                "";

            if (
                !movies ||
                !movies.length
            ) {
                const empty =
                    document.createElement(
                        "div"
                    );

                empty.className =
                    "simpleMovieFeedStatus";

                empty.textContent =
                    "No movie suggestions available.";

                results.appendChild(
                    empty
                );

                return;
            }

            movies.forEach(
                function (movie) {
                    results.appendChild(
                        createCard(
                            movie
                        )
                    );
                }
            );

            log(
                "Loaded " +
                movies.length +
                " suggestions for YTS movie " +
                movieId
            );
        }
        catch (error) {
            log(
                "Movie suggestions failed:",
                error
            );

            results.innerHTML =
                "";

            const failed =
                document.createElement(
                    "div"
                );

            failed.className =
                "simpleMovieFeedStatus";

            failed.textContent =
                "Unable to load movie suggestions.";

            results.appendChild(
                failed
            );
        }
    }
    async function render() {
        if (!isHomePage()) {
            return;
        }

        const indexPage =
            document.getElementById(
                "indexPage"
            );

        if (!indexPage) {
            return;
        }

        const container =
            indexPage.querySelector(
                ".homeSectionsContainer"
            ) ||
            indexPage.querySelector(
                "#homeTab"
            ) ||
            indexPage;

        if (!container) {
            return;
        }

        createStyles();

        await renderWhatsNew(
            container
        );

        createSearchSection(
            container
        );
    }

    function isHomePage() {
        return !!document.getElementById(
            "indexPage"
        );
    }

    function start() {
        log(
            "Starting " +
            PLUGIN_NAME
        );

        let attempts = 0;

        const timer =
            setInterval(
                function () {
                    attempts++;

                    if (
                        window.ApiClient &&
                        isHomePage()
                    ) {
                        clearInterval(timer);

                        render();

                        return;
                    }

                    if (attempts >= 60) {
                        clearInterval(timer);

                        error(
                            "Timed out waiting for Jellyfin Home."
                        );
                    }
                },
                1000
            );
    }

    window.__simpleMovieFeedSuggestionsNavigationHook =
        true;

    /*
     * Covers initial direct loading of a details page.
     */
    setTimeout(
        function () {
            renderMovieSuggestions(
                0
            );
        },
        750
    );
    if (
        !window.__simpleMovieFeedNativeCardResizeHook
    ) {
        window.__simpleMovieFeedNativeCardResizeHook =
            true;

        window.addEventListener(
            "resize",
            function () {
                syncSimpleMovieFeedNativeCardStyle();
            }
        );
    }

    syncSimpleMovieFeedNativeCardStyle();
    scheduleConfigurationControllerCheck();
    start();

    window.addEventListener(
        "hashchange",
        function () {
            scheduleConfigurationControllerCheck();
            setTimeout(
                function () {
                    render();

                    renderMovieSuggestions(
                        0
                    );
                },
                500
            );
        }
    );

    document.addEventListener(
        "viewshow",
        function () {
            scheduleConfigurationControllerCheck();
            setTimeout(
                function () {
                    render();

                    renderMovieSuggestions(
                        0
                    );
                },
                500
            );
        }
    );
})();








































