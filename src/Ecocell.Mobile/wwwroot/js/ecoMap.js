window.ecoMap = (function () {
    let map = null;
    let markersLayer = null;
    let userMarker = null;

    function pinHtml(fillVar) {
        // Cor via token global (var resolve do :root).
        return '<div class="eco-pin">' +
            '<svg viewBox="0 0 24 24" width="40" height="48" aria-hidden="true">' +
            '<path fill="var(' + fillVar + ')" d="M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7z"/>' +
            '<circle cx="12" cy="9" r="2.5" fill="#fff"/>' +
            '</svg></div>';
    }

    function pinIcon(fillVar) {
        return L.divIcon({
            html: pinHtml(fillVar),
            className: 'eco-pin-wrapper',
            iconSize: [40, 48],
            iconAnchor: [20, 48]
        });
    }

    return {
        init: function (elementId, lat, lng, tileUrl, attribution) {
            if (map) { map.remove(); map = null; userMarker = null; }
            map = L.map(elementId, { zoomControl: false }).setView([lat, lng], 14);
            L.tileLayer(tileUrl, { attribution: attribution, maxZoom: 19 }).addTo(map);
            markersLayer = L.layerGroup().addTo(map);
        },
        setMarkers: function (points, dotNetRef) {
            if (!markersLayer) { return; }
            markersLayer.clearLayers();
            points.forEach(function (p) {
                const icon = pinIcon('--eco-color-primary');
                const marker = L.marker([p.latitude, p.longitude], { icon: icon });
                marker.on('click', function () {
                    dotNetRef.invokeMethodAsync('OnPinSelected', p.id);
                });
                marker.addTo(markersLayer);
            });
        },
        flyTo: function (lat, lng, zoom) {
            if (map) { map.flyTo([lat, lng], zoom || 14); }
        },
        // Pin vermelho da posição do usuário. Marker próprio, fora do markersLayer,
        // pra sobreviver ao redesenho dos pontos de coleta; chamadas seguintes só movem.
        setUserLocation: function (lat, lng) {
            if (!map) { return; }
            if (userMarker) {
                userMarker.setLatLng([lat, lng]);
                return;
            }
            userMarker = L.marker([lat, lng], {
                icon: pinIcon('--eco-color-error'),
                zIndexOffset: 1000,
                interactive: false
            }).addTo(map);
        }
    };
})();
