/**
 * Feed Interaction Tracker
 * Automatisches Tracking von User-Interaktionen für den personalisierten Feed-Algorithmus
 */

class FeedInteractionTracker {
    constructor(userHash) {
        this.userHash = userHash;
        this.currentPostingId = null;
        this.videoStartTime = null;
        this.watchDurations = new Map(); // postingId -> total watch time
        this.trackingQueue = [];
        this.isTracking = false;

        // Interaction Types (muss mit C# Enum übereinstimmen)
        this.InteractionType = {
            View: 1,
            Like: 2,
            Unlike: 3,
            Skip: 4,
            WatchComplete: 5,
            AddToMealPlan: 6,
            FollowCreator: 7,
            UnfollowCreator: 8
        };

        this.init();
    }

    init() {
        // Intersection Observer für View-Tracking
        this.setupViewTracking();

        // Video-Event-Listener
        this.setupVideoTracking();

        // Periodisches Senden der Queue (alle 5 Sekunden)
        setInterval(() => this.flushQueue(), 5000);

        // Before Unload: Queue leeren
        window.addEventListener('beforeunload', () => this.flushQueue(true));
    }

    /**
     * Trackt, welches Video gerade im Viewport sichtbar ist
     */
    setupViewTracking() {
        const observerOptions = {
            root: null,
            rootMargin: '0px',
            threshold: 0.5 // 50% sichtbar = View
        };

        this.observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                const videoCard = entry.target;
                const postingId = parseInt(videoCard.dataset.postingId);

                if (entry.isIntersecting) {
                    // Video ist sichtbar
                    this.onVideoVisible(postingId, videoCard);
                } else {
                    // Video ist nicht mehr sichtbar
                    this.onVideoHidden(postingId);
                }
            });
        }, observerOptions);

        // Beobachte alle Video-Cards
        document.querySelectorAll('.video-card').forEach(card => {
            this.observer.observe(card);
        });
    }

    /**
     * Trackt Video-Wiedergabe
     */
    setupVideoTracking() {
        document.addEventListener('play', (e) => {
            if (e.target.tagName === 'VIDEO') {
                const postingId = this.getPostingIdFromVideo(e.target);
                if (postingId) {
                    this.onVideoPlay(postingId);
                }
            }
        }, true);

        document.addEventListener('pause', (e) => {
            if (e.target.tagName === 'VIDEO') {
                const postingId = this.getPostingIdFromVideo(e.target);
                if (postingId) {
                    this.onVideoPause(postingId);
                }
            }
        }, true);

        document.addEventListener('ended', (e) => {
            if (e.target.tagName === 'VIDEO') {
                const postingId = this.getPostingIdFromVideo(e.target);
                if (postingId) {
                    this.onVideoComplete(postingId);
                }
            }
        }, true);
    }

    getPostingIdFromVideo(videoElement) {
        const card = videoElement.closest('.video-card');
        return card ? parseInt(card.dataset.postingId) : null;
    }

    onVideoVisible(postingId, videoCard) {
        // Track View
        this.trackInteraction(postingId, this.InteractionType.View);

        this.currentPostingId = postingId;
        this.videoStartTime = Date.now();

        // Initialisiere Watch Duration
        if (!this.watchDurations.has(postingId)) {
            this.watchDurations.set(postingId, 0);
        }
    }

    onVideoHidden(postingId) {
        if (this.currentPostingId === postingId && this.videoStartTime) {
            // Berechne Watch Duration
            const duration = (Date.now() - this.videoStartTime) / 1000; // in Sekunden
            const totalDuration = this.watchDurations.get(postingId) || 0;
            this.watchDurations.set(postingId, totalDuration + duration);

            // Wenn User schnell weiter-scrollt (< 2 Sekunden) = Skip
            if (duration < 2) {
                this.trackInteraction(postingId, this.InteractionType.Skip);
            }

            this.videoStartTime = null;
        }
    }

    onVideoPlay(postingId) {
        if (!this.videoStartTime) {
            this.videoStartTime = Date.now();
        }
    }

    onVideoPause(postingId) {
        if (this.videoStartTime) {
            const duration = (Date.now() - this.videoStartTime) / 1000;
            const totalDuration = this.watchDurations.get(postingId) || 0;
            this.watchDurations.set(postingId, totalDuration + duration);
            this.videoStartTime = null;
        }
    }

    onVideoComplete(postingId) {
        const totalDuration = this.watchDurations.get(postingId) || 0;
        this.trackInteraction(postingId, this.InteractionType.WatchComplete, totalDuration);
    }

    /**
     * Public Methods für manuelle Tracking-Calls
     */
    trackLike(postingId) {
        this.trackInteraction(postingId, this.InteractionType.Like);
    }

    trackUnlike(postingId) {
        this.trackInteraction(postingId, this.InteractionType.Unlike);
    }

    trackAddToMealPlan(postingId) {
        this.trackInteraction(postingId, this.InteractionType.AddToMealPlan);
    }

    trackFollow(creatorId, postingId) {
        this.trackInteraction(postingId, this.InteractionType.FollowCreator);
    }

    trackUnfollow(creatorId, postingId) {
        this.trackInteraction(postingId, this.InteractionType.UnfollowCreator);
    }

    /**
     * Fügt Interaction zur Queue hinzu
     */
    trackInteraction(postingId, interactionType, duration = null) {
        if (!this.userHash || !postingId) return;

        const interaction = {
            userHash: this.userHash,
            postingId: postingId,
            interactionType: interactionType,
            duration: duration
        };

        this.trackingQueue.push(interaction);

        // Sofort senden bei wichtigen Interactions
        if ([this.InteractionType.Like, this.InteractionType.FollowCreator, this.InteractionType.AddToMealPlan].includes(interactionType)) {
            this.flushQueue();
        }
    }

    /**
     * Sendet alle getrackten Interaktionen an den Server
     */
    async flushQueue(isSync = false) {
        if (this.trackingQueue.length === 0 || this.isTracking) return;

        this.isTracking = true;
        const batch = [...this.trackingQueue];
        this.trackingQueue = [];

        try {
            if (isSync) {
                // Synchroner Request bei Page Unload (mit sendBeacon)
                const blob = new Blob([JSON.stringify(batch)], { type: 'application/json' });
                navigator.sendBeacon('/WorldMiniApp/Feed/TrackInteractionBatch', blob);
            } else {
                // Asynchroner Request
                for (const interaction of batch) {
                    await fetch('/WorldMiniApp/Feed/TrackInteraction', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json'
                        },
                        body: JSON.stringify(interaction)
                    });
                }
            }
        } catch (error) {
            console.error('Fehler beim Tracking:', error);
            // Bei Fehler: Interactions zurück in Queue
            this.trackingQueue.unshift(...batch);
        } finally {
            this.isTracking = false;
        }
    }

    /**
     * Observer aufräumen
     */
    destroy() {
        if (this.observer) {
            this.observer.disconnect();
        }
        this.flushQueue(true);
    }
}

// Globale Instanz für einfachen Zugriff
window.feedTracker = null;

// Auto-Init wenn userHash vorhanden
document.addEventListener('DOMContentLoaded', () => {
    const userHashElement = document.querySelector('[data-user-hash]');
    if (userHashElement) {
        const userHash = userHashElement.dataset.userHash;
        if (userHash) {
            window.feedTracker = new FeedInteractionTracker(userHash);
            console.log('Feed Interaction Tracker aktiviert');
        }
    }
});
