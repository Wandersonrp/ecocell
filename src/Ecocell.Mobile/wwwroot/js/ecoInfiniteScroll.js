window.ecoInfiniteScroll = {
    _observers: {},

    observe: function (sentinelId, dotNetRef) {
        this.dispose(sentinelId);

        const el = document.getElementById(sentinelId);
        if (!el) {
            return;
        }

        const observer = new IntersectionObserver(function (entries) {
            for (const entry of entries) {
                if (entry.isIntersecting) {
                    dotNetRef.invokeMethodAsync('LoadMoreAsync');
                }
            }
        }, { root: null, rootMargin: '120px', threshold: 0 });

        observer.observe(el);
        this._observers[sentinelId] = observer;
    },

    dispose: function (sentinelId) {
        const observer = this._observers[sentinelId];
        if (observer) {
            observer.disconnect();
            delete this._observers[sentinelId];
        }
    }
};
