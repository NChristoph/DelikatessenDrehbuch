/**
 * Create Posting Internationalization (i18n)
 * Übersetzungen für alle Texte im Create Posting Bereich
 * Sprachen: DE, EN, ES, PT, ID, NL, SV, DA, NO, MS
 */

(function() {
    'use strict';

    const translations = {
        // === BUTTONS & ACTIONS ===
        'btn.accept': {
            de: 'Akzeptieren',
            en: 'Accept',
            es: 'Aceptar',
            pt: 'Aceitar',
            id: 'Terima',
            nl: 'Accepteren',
            sv: 'Acceptera',
            da: 'Accepter',
            no: 'Godta',
            ms: 'Terima'
        },
        'btn.add': {
            de: 'Hinzufügen',
            en: 'Add',
            es: 'Añadir',
            pt: 'Adicionar',
            id: 'Tambah',
            nl: 'Toevoegen',
            sv: 'Lägg till',
            da: 'Tilføj',
            no: 'Legg til',
            ms: 'Tambah'
        },
        'btn.addAgain': {
            de: 'Erneut hinzufügen',
            en: 'Add Again',
            es: 'Añadir otra vez',
            pt: 'Adicionar novamente',
            id: 'Tambah lagi',
            nl: 'Opnieuw toevoegen',
            sv: 'Lägg till igen',
            da: 'Tilføj igen',
            no: 'Legg til igjen',
            ms: 'Tambah semula'
        },
        'btn.saveAndAdd': {
            de: 'Speichern und hinzufügen',
            en: 'Save and Add',
            es: 'Guardar y añadir',
            pt: 'Salvar e adicionar',
            id: 'Simpan dan tambah',
            nl: 'Opslaan en toevoegen',
            sv: 'Spara och lägg till',
            da: 'Gem og tilføj',
            no: 'Lagre og legg til',
            ms: 'Simpan dan tambah'
        },
        'btn.analyze': {
            de: 'Analysieren',
            en: 'Analyze',
            es: 'Analizar',
            pt: 'Analisar',
            id: 'Analisis',
            nl: 'Analyseren',
            sv: 'Analysera',
            da: 'Analyser',
            no: 'Analyser',
            ms: 'Analisis'
        },
        'btn.delete': {
            de: 'Löschen',
            en: 'Delete',
            es: 'Eliminar',
            pt: 'Excluir',
            id: 'Hapus',
            nl: 'Verwijderen',
            sv: 'Ta bort',
            da: 'Slet',
            no: 'Slett',
            ms: 'Padam'
        },
        'btn.save': {
            de: 'Speichern',
            en: 'Save',
            es: 'Guardar',
            pt: 'Salvar',
            id: 'Simpan',
            nl: 'Opslaan',
            sv: 'Spara',
            da: 'Gem',
            no: 'Lagre',
            ms: 'Simpan'
        },
        'btn.publish': {
            de: 'Veröffentlichen',
            en: 'Publish',
            es: 'Publicar',
            pt: 'Publicar',
            id: 'Terbitkan',
            nl: 'Publiceren',
            sv: 'Publicera',
            da: 'Udgiv',
            no: 'Publiser',
            ms: 'Terbitkan'
        },

        // === LABELS & HEADINGS ===
        'label.recognizedStep': {
            de: 'Erkannter Step',
            en: 'Recognized Step',
            es: 'Paso reconocido',
            pt: 'Passo reconhecido',
            id: 'Langkah yang dikenali',
            nl: 'Herkende stap',
            sv: 'Känt steg',
            da: 'Genkendt trin',
            no: 'Gjenkjent trinn',
            ms: 'Langkah yang dikenali'
        },
        'label.currentStep': {
            de: 'Aktueller Step',
            en: 'Current Step',
            es: 'Paso actual',
            pt: 'Passo atual',
            id: 'Langkah saat ini',
            nl: 'Huidige stap',
            sv: 'Aktuellt steg',
            da: 'Nuværende trin',
            no: 'Nåværende trinn',
            ms: 'Langkah semasa'
        },
        'label.ingredients': {
            de: 'Zutaten',
            en: 'Ingredients',
            es: 'Ingredientes',
            pt: 'Ingredientes',
            id: 'Bahan',
            nl: 'Ingrediënten',
            sv: 'Ingredienser',
            da: 'Ingredienser',
            no: 'Ingredienser',
            ms: 'Ramuan'
        },
        'label.steps': {
            de: 'Schritte',
            en: 'Steps',
            es: 'Pasos',
            pt: 'Passos',
            id: 'Langkah',
            nl: 'Stappen',
            sv: 'Steg',
            da: 'Trin',
            no: 'Trinn',
            ms: 'Langkah'
        },
        'label.ingredient': {
            de: 'Zutat',
            en: 'Ingredient',
            es: 'Ingrediente',
            pt: 'Ingrediente',
            id: 'Bahan',
            nl: 'Ingrediënt',
            sv: 'Ingrediens',
            da: 'Ingrediens',
            no: 'Ingrediens',
            ms: 'Ramuan'
        },

        // === TOASTS & MESSAGES ===
        'toast.ingredientAdded': {
            de: 'Zutat hinzugefügt',
            en: 'Ingredient added',
            es: 'Ingrediente añadido',
            pt: 'Ingrediente adicionado',
            id: 'Bahan ditambahkan',
            nl: 'Ingrediënt toegevoegd',
            sv: 'Ingrediens tillagd',
            da: 'Ingrediens tilføjet',
            no: 'Ingrediens lagt til',
            ms: 'Ramuan ditambah'
        },
        'toast.planSaved': {
            de: 'Plan gespeichert!',
            en: 'Plan saved!',
            es: '¡Plan guardado!',
            pt: 'Plano salvo!',
            id: 'Rencana disimpan!',
            nl: 'Plan opgeslagen!',
            sv: 'Plan sparad!',
            da: 'Plan gemt!',
            no: 'Plan lagret!',
            ms: 'Rancangan disimpan!'
        },
        'toast.ingredientAlreadyAdded': {
            de: 'Zutat bereits hinzugefügt',
            en: 'Ingredient already added',
            es: 'Ingrediente ya añadido',
            pt: 'Ingrediente já adicionado',
            id: 'Bahan sudah ditambahkan',
            nl: 'Ingrediënt al toegevoegd',
            sv: 'Ingrediens redan tillagd',
            da: 'Ingrediens allerede tilføjet',
            no: 'Ingrediens allerede lagt til',
            ms: 'Ramuan sudah ditambah'
        },
        'toast.recipeSaved': {
            de: 'Neue Zutat gespeichert',
            en: 'New ingredient saved',
            es: 'Nuevo ingrediente guardado',
            pt: 'Novo ingrediente salvo',
            id: 'Bahan baru disimpan',
            nl: 'Nieuw ingrediënt opgeslagen',
            sv: 'Ny ingrediens sparad',
            da: 'Ny ingrediens gemt',
            no: 'Ny ingrediens lagret',
            ms: 'Ramuan baru disimpan'
        },
        'toast.existingIngredientUsed': {
            de: 'Vorhandene Zutat verwendet',
            en: 'Existing ingredient used',
            es: 'Ingrediente existente usado',
            pt: 'Ingrediente existente usado',
            id: 'Bahan yang ada digunakan',
            nl: 'Bestaand ingrediënt gebruikt',
            sv: 'Befintlig ingrediens använd',
            da: 'Eksisterende ingrediens brugt',
            no: 'Eksisterende ingrediens brukt',
            ms: 'Ramuan sedia ada digunakan'
        },

        // === AI INGREDIENT ===
        'ai.analyzing': {
            de: 'Prüfe Katalog und KI-Vorschlag …',
            en: 'Checking catalog and AI suggestion …',
            es: 'Verificando catálogo y sugerencia de IA …',
            pt: 'Verificando catálogo e sugestão de IA …',
            id: 'Memeriksa katalog dan saran AI …',
            nl: 'Catalogus en AI-suggestie controleren …',
            sv: 'Kontrollerar katalog och AI-förslag …',
            da: 'Kontrollerer katalog og AI-forslag …',
            no: 'Sjekker katalog og AI-forslag …',
            ms: 'Memeriksa katalog dan cadangan AI …'
        },
        'ai.saving': {
            de: 'Speichere neue Zutat …',
            en: 'Saving new ingredient …',
            es: 'Guardando nuevo ingrediente …',
            pt: 'Salvando novo ingrediente …',
            id: 'Menyimpan bahan baru …',
            nl: 'Nieuw ingrediënt opslaan …',
            sv: 'Sparar ny ingrediens …',
            da: 'Gemmer ny ingrediens …',
            no: 'Lagrer ny ingrediens …',
            ms: 'Menyimpan ramuan baru …'
        },
        'ai.matchPercent': {
            de: '% Treffer',
            en: '% Match',
            es: '% Coincidencia',
            pt: '% Correspondência',
            id: '% Kecocokan',
            nl: '% Overeenkomst',
            sv: '% Matchning',
            da: '% Match',
            no: '% Treff',
            ms: '% Padanan'
        },

        // === PLACEHOLDERS ===
        'placeholder.quantity': {
            de: '100',
            en: '100',
            es: '100',
            pt: '100',
            id: '100',
            nl: '100',
            sv: '100',
            da: '100',
            no: '100',
            ms: '100'
        },
        'placeholder.searchIngredient': {
            de: 'Zutat suchen...',
            en: 'Search ingredient...',
            es: 'Buscar ingrediente...',
            pt: 'Buscar ingrediente...',
            id: 'Cari bahan...',
            nl: 'Ingrediënt zoeken...',
            sv: 'Sök ingrediens...',
            da: 'Søg ingrediens...',
            no: 'Søk ingrediens...',
            ms: 'Cari ramuan...'
        },

        // === VALIDATION ===
        'validation.titleRequired': {
            de: 'Bitte Titel eingeben',
            en: 'Please enter title',
            es: 'Por favor ingrese título',
            pt: 'Por favor insira o título',
            id: 'Silakan masukkan judul',
            nl: 'Voer titel in',
            sv: 'Ange titel',
            da: 'Indtast titel',
            no: 'Skriv inn tittel',
            ms: 'Sila masukkan tajuk'
        },
        'validation.categoryRequired': {
            de: 'Bitte Kategorie wählen',
            en: 'Please select category',
            es: 'Por favor seleccione categoría',
            pt: 'Por favor selecione categoria',
            id: 'Silakan pilih kategori',
            nl: 'Selecteer categorie',
            sv: 'Välj kategori',
            da: 'Vælg kategori',
            no: 'Velg kategori',
            ms: 'Sila pilih kategori'
        },
        'validation.mediaRequired': {
            de: 'Bitte Bild/Video hochladen',
            en: 'Please upload image/video',
            es: 'Por favor suba imagen/video',
            pt: 'Por favor faça upload de imagem/vídeo',
            id: 'Silakan unggah gambar/video',
            nl: 'Upload afbeelding/video',
            sv: 'Ladda upp bild/video',
            da: 'Upload billede/video',
            no: 'Last opp bilde/video',
            ms: 'Sila muat naik imej/video'
        }
    };

    // Helper: Get current language from HTML lang attribute
    function getCurrentLanguage() {
        const lang = document.documentElement.lang || 'de';
        const langMap = {
            'de': 'de',
            'en': 'en',
            'es': 'es',
            'pt': 'pt',
            'id': 'id',
            'nl': 'nl',
            'sv': 'sv',
            'da': 'da',
            'no': 'no',
            'nb': 'no',
            'ms': 'ms'
        };
        return langMap[lang.toLowerCase()] || 'de';
    }

    // Helper: Get translation for a key
    function t(key, fallback) {
        const lang = getCurrentLanguage();
        const translation = translations[key];

        if (!translation) {
            console.warn(`[i18n] Missing translation key: ${key}`);
            return fallback || key;
        }

        return translation[lang] || translation['de'] || fallback || key;
    }

    // Helper: Translate all elements with data-i18n attribute
    function translatePage() {
        const elements = document.querySelectorAll('[data-i18n]');
        elements.forEach(el => {
            const key = el.getAttribute('data-i18n');
            const translation = t(key);

            // Handle different element types
            if (el.tagName === 'INPUT' && el.hasAttribute('placeholder')) {
                el.placeholder = translation;
            } else if (el.hasAttribute('title')) {
                el.title = translation;
            } else {
                el.textContent = translation;
            }
        });
    }

    // Export to window
    window.CreatePostingI18n = {
        t: t,
        getCurrentLanguage: getCurrentLanguage,
        translatePage: translatePage,
        translations: translations
    };

    // Auto-translate on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', translatePage);
    } else {
        translatePage();
    }
})();
