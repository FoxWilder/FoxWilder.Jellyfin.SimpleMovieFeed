    (function () {
        "use strict";

        const pluginId = "d3d9d8e8-4a53-4a0d-9a5b-8a1b5c3b0d21";
        const page = document.querySelector("#SimpleMovieFeedConfigurationPage");
        const form = document.querySelector("#SimpleMovieFeedConfigurationForm");

        function value(id) {
            return document.querySelector("#" + id).value.trim();
        }

        function integerValue(id) {
            return Number.parseInt(value(id), 10);
        }

        function setValue(id, newValue) {
            document.querySelector("#" + id).value =
                newValue === null || newValue === undefined
                    ? ""
                    : newValue;
        }

        function getServerUrl() {
            if (
                window.ApiClient &&
                typeof window.ApiClient.serverAddress === "function"
            ) {
                return window.ApiClient.serverAddress();
            }

            return window.location.origin;
        }

        function authenticatedFetch(path, options) {
            if (
                !window.ApiClient ||
                typeof window.ApiClient.accessToken !== "function"
            ) {
                throw new Error(
                    "Jellyfin ApiClient/access token is unavailable."
                );
            }

            const request = Object.assign({}, options || {});
            request.headers = Object.assign({}, request.headers || {});
            request.headers["Authorization"] =
                "MediaBrowser Token=" +
                window.ApiClient.accessToken();

            return fetch(getServerUrl() + path, request);
        }

        async function loadCredentialStatus() {
            const statusElement =
                document.querySelector("#QBitTorrentCredentialStatus");

            try {
                const response = await authenticatedFetch(
                    "/SimpleMovieFeed/configuration/status",
                    { method: "GET" }
                );

                if (!response.ok) {
                    throw new Error("HTTP " + response.status);
                }

                const status = await response.json();

                if (!status.credentialStorageSupported) {
                    statusElement.textContent =
                        "Protected credential storage is unavailable on this platform.";
                } else if (status.credentialConfigured) {
                    statusElement.textContent =
                        "A protected qBittorrent credential is configured.";
                } else {
                    statusElement.textContent =
                        "No qBittorrent credential is configured.";
                }
            } catch (error) {
                statusElement.textContent =
                    "Unable to read credential status.";
            }
        }

        async function loadConfiguration() {
            Dashboard.showLoadingMsg();

            try {
                const response = await authenticatedFetch(
                    "/SimpleMovieFeed/configuration/status",
                    { method: "GET" }
                );

                if (!response.ok) {
                    throw new Error(
                        "Unable to load SimpleMovieFeed configuration (HTTP " +
                        response.status +
                        ")."
                    );
                }

                const config = await response.json();

                setValue("RssFeedUrl", config.rssFeedUrl);
                setValue("MovieSearchApiUrl", config.movieSearchApiUrl);
                setValue("CacheDirectory", config.cacheDirectory);
                setValue("LibraryDirectory", config.libraryDirectory);
                setValue("QBitTorrentApiUrl", config.qBitTorrentApiUrl);
                setValue("StartupBufferMiB", config.startupBufferMiB);
                setValue("CleanupGraceSeconds", config.cleanupGraceSeconds);
                setValue("QBitTorrentTimeoutSeconds", config.qBitTorrentTimeoutSeconds);
                setValue("QBitTorrentApiKey", "");

                await loadCredentialStatus();
            } finally {
                Dashboard.hideLoadingMsg();
            }
        }

        async function saveCredentialIfProvided() {
            const secret = value("QBitTorrentApiKey");

            if (!secret) {
                return;
            }

            const response = await authenticatedFetch(
                "/SimpleMovieFeed/configuration/credential",
                {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json"
                    },
                    body: JSON.stringify({
                        ApiKey: secret
                    })
                }
            );

            if (!response.ok) {
                throw new Error(
                    "Unable to save protected qBittorrent credential (HTTP " +
                    response.status +
                    ")."
                );
            }

            setValue("QBitTorrentApiKey", "");
        }

        async function saveConfiguration(event) {
            event.preventDefault();

            if (!form.reportValidity()) {
                return false;
            }

            Dashboard.showLoadingMsg();

            try {
                const config = {
                    RssFeedUrl: "https://atlas.rssly.org/feed/0/all/all/0/en",
                    MovieSearchApiUrl: "https://movies-api.accel.li/api/v2",
                    CacheDirectory: value("CacheDirectory"),
                    LibraryDirectory: value("LibraryDirectory"),
                    QBitTorrentApiUrl: "http://127.0.0.1:8080/api/v2/",
                    StartupBufferMiB: integerValue("StartupBufferMiB"),
                    CleanupGraceSeconds: integerValue("CleanupGraceSeconds"),
                    QBitTorrentTimeoutSeconds: integerValue("QBitTorrentTimeoutSeconds")
                };

                const saveResponse = await authenticatedFetch(
                    "/SimpleMovieFeed/configuration",
                    {
                        method: "POST",
                        headers: {
                            "Content-Type": "application/json"
                        },
                        body: JSON.stringify(config)
                    }
                );

                if (!saveResponse.ok) {
                    let message =
                        "Unable to save SimpleMovieFeed configuration.";

                    try {
                        const failure = await saveResponse.json();

                        if (failure && failure.error) {
                            message = failure.error;
                        }
                    } catch (error) {
                        console.warn(
                            "SimpleMovieFeed: unable to read configuration error response.",
                            error
                        );
                    }

                    throw new Error(message);
                }

                await saveCredentialIfProvided();
                await loadCredentialStatus();

                if (Dashboard.processPluginConfigurationUpdateResult) {
                    Dashboard.processPluginConfigurationUpdateResult({});
                }
            } catch (error) {
                console.error(
                    "SimpleMovieFeed configuration save failed.",
                    error
                );

                if (Dashboard.alert) {
                    Dashboard.alert(
                        error.message ||
                        "Unable to save SimpleMovieFeed configuration."
                    );
                } else {
                    window.alert(
                        error.message ||
                        "Unable to save SimpleMovieFeed configuration."
                    );
                }
            } finally {
                Dashboard.hideLoadingMsg();
            }

            return false;
        }

        function startConfigurationLoad() {
            loadConfiguration().catch(function (error) {
                console.error(
                    "SimpleMovieFeed configuration load failed.",
                    error
                );
            });
        }

        page.addEventListener("pageshow", startConfigurationLoad);
        form.addEventListener("submit", saveConfiguration);

        startConfigurationLoad();
    }());
