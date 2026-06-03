/**
 * Shared Feed Internationalization (i18n)
 * Übersetzungen für Shared Feed / Groups Bereich
 * Sprachen: DE, EN, ES, PT, ID, NL, SV, DA, NO, MS
 */

(function() {
    'use strict';

    const translations = {
        // === SHARED FEED / GROUPS ===
        'SharedFeed.MyGroups': {
            de: 'Meine Gruppen',
            en: 'My Groups',
            es: 'Mis Grupos',
            pt: 'Meus Grupos',
            id: 'Grup Saya',
            nl: 'Mijn Groepen',
            sv: 'Mina Grupper',
            da: 'Mine Grupper',
            no: 'Mine Grupper',
            ms: 'Kumpulan Saya'
        },
        'SharedFeed.Members': {
            de: 'Mitglieder',
            en: 'Members',
            es: 'miembros',
            pt: 'membros',
            id: 'anggota',
            nl: 'leden',
            sv: 'medlemmar',
            da: 'medlemmer',
            no: 'medlemmer',
            ms: 'ahli'
        },
        'SharedFeed.Items': {
            de: 'Elemente',
            en: 'Items',
            es: 'elementos',
            pt: 'itens',
            id: 'item',
            nl: 'items',
            sv: 'objekt',
            da: 'elementer',
            no: 'elementer',
            ms: 'item'
        },
        'SharedFeed.InviteLink': {
            de: 'Einladungslink',
            en: 'Invite Link',
            es: 'Enlace de invitación',
            pt: 'Link de convite',
            id: 'Tautan Undangan',
            nl: 'Uitnodigingslink',
            sv: 'Inbjudningslänk',
            da: 'Invitationslink',
            no: 'Invitasjonslenke',
            ms: 'Pautan Jemputan'
        },
        'SharedFeed.CreateNewFeed': {
            de: 'Neuen Gruppenfeed erstellen',
            en: 'Create New Group Feed',
            es: 'Crear Nuevo Feed de Grupo',
            pt: 'Criar Novo Feed de Grupo',
            id: 'Buat Feed Grup Baru',
            nl: 'Nieuwe Groepsfeed Maken',
            sv: 'Skapa Nytt Gruppflöde',
            da: 'Opret Nyt Gruppefeed',
            no: 'Opprett Ny Gruppefeed',
            ms: 'Cipta Feed Kumpulan Baru'
        },
        'Common.New': {
            de: 'Neu',
            en: 'New',
            es: 'Nuevo',
            pt: 'Novo',
            id: 'Baru',
            nl: 'Nieuw',
            sv: 'Ny',
            da: 'Ny',
            no: 'Ny',
            ms: 'Baru'
        },
        'Nav.Feed': {
            de: 'Feed',
            en: 'Feed',
            es: 'Feed',
            pt: 'Feed',
            id: 'Feed',
            nl: 'Feed',
            sv: 'Flöde',
            da: 'Feed',
            no: 'Feed',
            ms: 'Feed'
        },
        'Chat.Title': {
            de: 'Chat',
            en: 'Chat',
            es: 'Chat',
            pt: 'Chat',
            id: 'Obrolan',
            nl: 'Chat',
            sv: 'Chatt',
            da: 'Chat',
            no: 'Chat',
            ms: 'Sembang'
        },
        'Todo.Title': {
            de: 'Aufgaben',
            en: 'Tasks',
            es: 'Tareas',
            pt: 'Tarefas',
            id: 'Tugas',
            nl: 'Taken',
            sv: 'Uppgifter',
            da: 'Opgaver',
            no: 'Oppgaver',
            ms: 'Tugasan'
        },
        'SharedFeed.InviteLinkCopied': {
            de: 'Einladungslink kopiert',
            en: 'Invite link copied',
            es: 'Enlace de invitación copiado',
            pt: 'Link de convite copiado',
            id: 'Tautan undangan disalin',
            nl: 'Uitnodigingslink gekopieerd',
            sv: 'Inbjudningslänk kopierad',
            da: 'Invitationslink kopieret',
            no: 'Invitasjonslenke kopiert',
            ms: 'Pautan jemputan disalin'
        },
        'SharedFeed.LoadingMealPlans': {
            de: 'Lade deine Essenspläne...',
            en: 'Loading your meal plans...',
            es: 'Cargando tus planes de comidas...',
            pt: 'Carregando seus planos de refeições...',
            id: 'Memuat rencana makan Anda...',
            nl: 'Je maaltijdplannen laden...',
            sv: 'Laddar dina måltidsplaner...',
            da: 'Indlæser dine måltidsplaner...',
            no: 'Laster måltidsplanene dine...',
            ms: 'Memuatkan pelan makanan anda...'
        }
    };

    // Get current language from cookie
    function getCurrentLanguage() {
        const cookies = document.cookie.split(';');
        for (let cookie of cookies) {
            const [name, value] = cookie.trim().split('=');
            if (name === 'deli-lang') {
                // Normalize: esp → es, prt → pt, no → nb
                const normalized = value.toLowerCase();
                switch (normalized) {
                    case 'esp': return 'es';
                    case 'prt': return 'pt';
                    case 'no': return 'no';
                    case 'se': return 'sv';
                    case 'dk': return 'da';
                    default: return normalized;
                }
            }
        }
        return 'de'; // Default
    }

    // Translate function
    function t(key) {
        const lang = getCurrentLanguage();
        const translation = translations[key];

        if (!translation) {
            console.warn(`Translation key not found: ${key}`);
            return key;
        }

        return translation[lang] || translation['de'] || key;
    }

    // Export globally
    window.SharedFeedI18n = {
        t: t,
        getCurrentLanguage: getCurrentLanguage
    };

    // Auto-translate elements with data-i18n attribute
    function translatePage() {
        document.querySelectorAll('[data-i18n]').forEach(element => {
            const key = element.getAttribute('data-i18n');
            element.textContent = t(key);
        });
    }

    // Translate on load
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', translatePage);
    } else {
        translatePage();
    }

    // Re-translate when language changes
    window.addEventListener('languageChanged', translatePage);
})();
