window.ecoMap = (function () {
    let map = null;
    let markersLayer = null;

    function pinHtml() {
        // Pin de Ponto de Coleta — verde primary. Cor via token global (var resolve do :root).
        return '<div class="eco-pin">' +
            '<svg viewBox="0 0 24 24" width="40" height="48" aria-hidden="true">' +
            '<path fill="var(--eco-color-primary)" d="M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7z"/>' +
            '<circle cx="12" cy="9" r="2.5" fill="#fff"/>' +
            '</svg></div>';
    }

    return {
        init: function (elementId, lat, lng, tileUrl, attribution) {
            if (map) { map.remove(); map = null; }
            map = L.map(elementId, { zoomControl: false }).setView([lat, lng], 14);
            L.tileLayer(tileUrl, { attribution: attribution, maxZoom: 19 }).addTo(map);
            markersLayer = L.layerGroup().addTo(map);
        },
        setMarkers: function (points, dotNetRef) {
            if (!markersLayer) { return; }
            markersLayer.clearLayers();
            points.forEach(function (p) {
                const icon = L.divIcon({
                    html: pinHtml(),
                    className: 'eco-pin-wrapper',
                    iconSize: [40, 48],
                    iconAnchor: [20, 48]
                });
                const marker = L.marker([p.latitude, p.longitude], { icon: icon });
                marker.on('click', function () {
                    dotNetRef.invokeMethodAsync('OnPinSelected', p.id);
                });
                marker.addTo(markersLayer);
            });
        },
        flyTo: function (lat, lng, zoom) {
            if (map) { map.flyTo([lat, lng], zoom || 14); }
        }
    };
})();
