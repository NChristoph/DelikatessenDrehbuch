#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Migrates recipe_type_step_variables.json from v3.0 to v4.0
Converts all string values to multilingual objects with 10 languages
"""

import json
import re
from pathlib import Path

# Translation dictionaries for common cooking terms
TRANSLATIONS = {
    # Time units
    "Minuten": {
        "de": "Minuten", "en": "minutes", "es": "minutos", "pt": "minutos",
        "id": "menit", "nl": "minuten", "sv": "minuter", "da": "minutter",
        "no": "minutter", "ms": "minit"
    },
    "Sekunden": {
        "de": "Sekunden", "en": "seconds", "es": "segundos", "pt": "segundos",
        "id": "detik", "nl": "seconden", "sv": "sekunder", "da": "sekunder",
        "no": "sekunder", "ms": "saat"
    },

    # Tools
    "Sparschäler": {
        "de": "Sparschäler", "en": "peeler", "es": "pelador", "pt": "descascador",
        "id": "pengupas", "nl": "dunschiller", "sv": "potatisskalare", "da": "skræller",
        "no": "potetskrell", "ms": "pengupas"
    },
    "Reibe": {
        "de": "Reibe", "en": "grater", "es": "rallador", "pt": "ralador",
        "id": "parutan", "nl": "rasp", "sv": "rivjärn", "da": "rivejern",
        "no": "rivjern", "ms": "parut"
    },
    "Schneebesen": {
        "de": "Schneebesen", "en": "whisk", "es": "batidor", "pt": "batedor",
        "id": "pengocok", "nl": "garde", "sv": "visp", "da": "piskeris",
        "no": "visp", "ms": "pengocok"
    },
    "Handrührgerät": {
        "de": "Handrührgerät", "en": "hand mixer", "es": "batidora", "pt": "batedeira",
        "id": "mixer tangan", "nl": "handmixer", "sv": "elvisp", "da": "håndmixer",
        "no": "håndmikser", "ms": "pengadun tangan"
    },
    "Nudelholz": {
        "de": "Nudelholz", "en": "rolling pin", "es": "rodillo", "pt": "rolo",
        "id": "gilingan", "nl": "deegroller", "sv": "kavel", "da": "kagerullehoved",
        "no": "kjevle", "ms": "penggelek"
    },

    # Equipment
    "Backofen": {
        "de": "Backofen", "en": "oven", "es": "horno", "pt": "forno",
        "id": "oven", "nl": "oven", "sv": "ugn", "da": "ovn",
        "no": "ovn", "ms": "ketuhar"
    },
    "Pfanne": {
        "de": "Pfanne", "en": "pan", "es": "sartén", "pt": "frigideira",
        "id": "wajan", "nl": "pan", "sv": "stekpanna", "da": "pande",
        "no": "panne", "ms": "kuali"
    },
    "Topf": {
        "de": "Topf", "en": "pot", "es": "olla", "pt": "panela",
        "id": "panci", "nl": "pan", "sv": "kastrull", "da": "gryde",
        "no": "gryte", "ms": "periuk"
    },
    "Dampfgarer": {
        "de": "Dampfgarer", "en": "steamer", "es": "vaporera", "pt": "vaporizador",
        "id": "kukusan", "nl": "stomer", "sv": "ångkokare", "da": "dampkoger",
        "no": "dampkoker", "ms": "pengukus"
    },
    "Auflaufform": {
        "de": "Auflaufform", "en": "baking dish", "es": "fuente para horno", "pt": "forma de forno",
        "id": "loyang", "nl": "ovenschaal", "sv": "ugnsform", "da": "ovnfast fad",
        "no": "ildfast form", "ms": "pinggan bakar"
    },
    "Sieb": {
        "de": "Sieb", "en": "strainer", "es": "colador", "pt": "peneira",
        "id": "saringan", "nl": "zeef", "sv": "sil", "da": "si",
        "no": "sil", "ms": "penapis"
    },
    "Grill": {
        "de": "Grill", "en": "grill", "es": "parrilla", "pt": "grelha",
        "id": "panggangan", "nl": "grill", "sv": "grill", "da": "grill",
        "no": "grill", "ms": "pemanggang"
    },

    # States
    "goldbraun": {
        "de": "goldbraun", "en": "golden brown", "es": "dorado", "pt": "dourado",
        "id": "keemasan", "nl": "goudbruin", "sv": "gyllenbrun", "da": "gylden",
        "no": "gyllen", "ms": "keemasan"
    },
    "glasig": {
        "de": "glasig", "en": "translucent", "es": "translúcido", "pt": "translúcido",
        "id": "transparan", "nl": "glazig", "sv": "glasig", "da": "glasagtig",
        "no": "glassaktig", "ms": "lut sinar"
    },
    "knusprig": {
        "de": "knusprig", "en": "crispy", "es": "crujiente", "pt": "crocante",
        "id": "renyah", "nl": "knapperig", "sv": "krispig", "da": "sprød",
        "no": "sprø", "ms": "rangup"
    },
    "gar": {
        "de": "gar", "en": "cooked", "es": "cocido", "pt": "cozido",
        "id": "matang", "nl": "gaar", "sv": "genomkokt", "da": "gennemkogt",
        "no": "gjennomkokt", "ms": "masak"
    },
    "zart": {
        "de": "zart", "en": "tender", "es": "tierno", "pt": "macio",
        "id": "empuk", "nl": "mals", "sv": "mör", "da": "mør",
        "no": "mør", "ms": "lembut"
    },
    "geschmeidig": {
        "de": "geschmeidig", "en": "smooth", "es": "suave", "pt": "macio",
        "id": "halus", "nl": "soepel", "sv": "smidig", "da": "smidig",
        "no": "smidig", "ms": "lembut"
    },
    "cremig": {
        "de": "cremig", "en": "creamy", "es": "cremoso", "pt": "cremoso",
        "id": "lembut", "nl": "romig", "sv": "krämig", "da": "cremet",
        "no": "kremete", "ms": "berkrim"
    },
    "schaumig": {
        "de": "schaumig", "en": "foamy", "es": "espumoso", "pt": "espumoso",
        "id": "berbusa", "nl": "schuimig", "sv": "skummig", "da": "skummende",
        "no": "skummende", "ms": "berbuih"
    },
    "eingedickt": {
        "de": "eingedickt", "en": "thickened", "es": "espesado", "pt": "espessado",
        "id": "mengental", "nl": "ingedikt", "sv": "tjocknat", "da": "indkogt",
        "no": "tyknet", "ms": "pekat"
    },
    "sämig": {
        "de": "sämig", "en": "creamy", "es": "cremoso", "pt": "cremoso",
        "id": "kental", "nl": "romig", "sv": "gräddig", "da": "cremet",
        "no": "kremaktig", "ms": "pekat"
    },
    "verdoppelt": {
        "de": "verdoppelt", "en": "doubled", "es": "duplicado", "pt": "duplicado",
        "id": "dua kali lipat", "nl": "verdubbeld", "sv": "fördubblad", "da": "fordoblet",
        "no": "doblet", "ms": "dua kali ganda"
    },
    "fest": {
        "de": "fest", "en": "firm", "es": "firme", "pt": "firme",
        "id": "padat", "nl": "stevig", "sv": "fast", "da": "fast",
        "no": "fast", "ms": "pejal"
    },

    # Shapes
    "Würfel": {
        "de": "Würfel", "en": "cubes", "es": "cubos", "pt": "cubos",
        "id": "dadu", "nl": "blokjes", "sv": "tärningar", "da": "tern",
        "no": "terninger", "ms": "kiub"
    },
    "Scheiben": {
        "de": "Scheiben", "en": "slices", "es": "rodajas", "pt": "fatias",
        "id": "irisan", "nl": "plakjes", "sv": "skivor", "da": "skiver",
        "no": "skiver", "ms": "hirisan"
    },
    "Streifen": {
        "de": "Streifen", "en": "strips", "es": "tiras", "pt": "tiras",
        "id": "potongan", "nl": "reepjes", "sv": "remsor", "da": "strimler",
        "no": "strimler", "ms": "jalur"
    },
    "rund": {
        "de": "rund", "en": "round", "es": "redondo", "pt": "redondo",
        "id": "bulat", "nl": "rond", "sv": "rund", "da": "rund",
        "no": "rund", "ms": "bulat"
    },

    # Ingredients
    "Teig": {
        "de": "Teig", "en": "dough", "es": "masa", "pt": "massa",
        "id": "adonan", "nl": "deeg", "sv": "deg", "da": "dej",
        "no": "deig", "ms": "doh"
    },
    "Eigelb": {
        "de": "Eigelb", "en": "egg yolk", "es": "yema de huevo", "pt": "gema de ovo",
        "id": "kuning telur", "nl": "eigeel", "sv": "äggula", "da": "æggeblomme",
        "no": "eggeplomme", "ms": "kuning telur"
    },
    "Mehl": {
        "de": "Mehl", "en": "flour", "es": "harina", "pt": "farinha",
        "id": "tepung", "nl": "meel", "sv": "mjöl", "da": "mel",
        "no": "mel", "ms": "tepung"
    },
    "Semmelbröseln": {
        "de": "Semmelbröseln", "en": "breadcrumbs", "es": "pan rallado", "pt": "pão ralado",
        "id": "remah roti", "nl": "paneermeel", "sv": "ströbröd", "da": "rasp",
        "no": "strøbrød", "ms": "serbuk roti"
    },
    "Olivenöl": {
        "de": "Olivenöl", "en": "olive oil", "es": "aceite de oliva", "pt": "azeite",
        "id": "minyak zaitun", "nl": "olijfolie", "sv": "olivolja", "da": "olivenolie",
        "no": "olivenolje", "ms": "minyak zaitun"
    },
    "Öl": {
        "de": "Öl", "en": "oil", "es": "aceite", "pt": "óleo",
        "id": "minyak", "nl": "olie", "sv": "olja", "da": "olie",
        "no": "olje", "ms": "minyak"
    },
    "Wasser": {
        "de": "Wasser", "en": "water", "es": "agua", "pt": "água",
        "id": "air", "nl": "water", "sv": "vatten", "da": "vand",
        "no": "vann", "ms": "air"
    },
    "Wein": {
        "de": "Wein", "en": "wine", "es": "vino", "pt": "vinho",
        "id": "anggur", "nl": "wijn", "sv": "vin", "da": "vin",
        "no": "vin", "ms": "wain"
    },
    "Sauce": {
        "de": "Sauce", "en": "sauce", "es": "salsa", "pt": "molho",
        "id": "saus", "nl": "saus", "sv": "sås", "da": "sauce",
        "no": "saus", "ms": "sos"
    },
    "Masse": {
        "de": "Masse", "en": "mixture", "es": "mezcla", "pt": "mistura",
        "id": "adonan", "nl": "mengsel", "sv": "massa", "da": "masse",
        "no": "masse", "ms": "campuran"
    },
    "Käse": {
        "de": "Käse", "en": "cheese", "es": "queso", "pt": "queijo",
        "id": "keju", "nl": "kaas", "sv": "ost", "da": "ost",
        "no": "ost", "ms": "keju"
    },
    "Zutaten": {
        "de": "Zutaten", "en": "ingredients", "es": "ingredientes", "pt": "ingredientes",
        "id": "bahan", "nl": "ingrediënten", "sv": "ingredienser", "da": "ingredienser",
        "no": "ingredienser", "ms": "bahan"
    },
    "Speisestärke": {
        "de": "Speisestärke", "en": "cornstarch", "es": "maicena", "pt": "amido de milho",
        "id": "tepung maizena", "nl": "maïzena", "sv": "majsstärkelse", "da": "maizena",
        "no": "maisstivelse", "ms": "tepung jagung"
    },

    # Pronouns
    "es": {
        "de": "es", "en": "it", "es": "lo", "pt": "o",
        "id": "itu", "nl": "het", "sv": "det", "da": "det",
        "no": "det", "ms": "ia"
    },
    "er": {
        "de": "er", "en": "it", "es": "él", "pt": "ele",
        "id": "dia", "nl": "het", "sv": "den", "da": "den",
        "no": "den", "ms": "dia"
    },
    "sie": {
        "de": "sie", "en": "it", "es": "ella", "pt": "ela",
        "id": "dia", "nl": "ze", "sv": "den", "da": "den",
        "no": "den", "ms": "dia"
    },
    "ihn": {
        "de": "ihn", "en": "it", "es": "lo", "pt": "o",
        "id": "itu", "nl": "het", "sv": "den", "da": "den",
        "no": "den", "ms": "ia"
    },

    # Actions
    "tupfe": {
        "de": "tupfe", "en": "pat", "es": "seca", "pt": "seque",
        "id": "keringkan", "nl": "dep", "sv": "torka", "da": "tør",
        "no": "tørk", "ms": "lap"
    },
    "dünste": {
        "de": "dünste", "en": "sauté", "es": "saltea", "pt": "refogue",
        "id": "tumis", "nl": "fruit", "sv": "fräs", "da": "sautér",
        "no": "sautér", "ms": "tumis"
    },
    "schlage": {
        "de": "schlage", "en": "beat", "es": "bate", "pt": "bata",
        "id": "kocok", "nl": "klop", "sv": "vispa", "da": "pisk",
        "no": "visp", "ms": "pukul"
    },
    "rühre": {
        "de": "rühre", "en": "stir", "es": "revuelve", "pt": "mexa",
        "id": "aduk", "nl": "roer", "sv": "rör", "da": "rør",
        "no": "rør", "ms": "kacau"
    },

    # Copula
    "ist": {
        "de": "ist", "en": "is", "es": "está", "pt": "está",
        "id": "adalah", "nl": "is", "sv": "är", "da": "er",
        "no": "er", "ms": "adalah"
    },
    "sind": {
        "de": "sind", "en": "are", "es": "están", "pt": "estão",
        "id": "adalah", "nl": "zijn", "sv": "är", "da": "er",
        "no": "er", "ms": "adalah"
    },

    # Sizes
    "fein": {
        "de": "fein", "en": "fine", "es": "fino", "pt": "fino",
        "id": "halus", "nl": "fijn", "sv": "fin", "da": "fin",
        "no": "fin", "ms": "halus"
    },
    "mittel": {
        "de": "mittel", "en": "medium", "es": "medio", "pt": "médio",
        "id": "sedang", "nl": "middel", "sv": "medel", "da": "mellem",
        "no": "middels", "ms": "sederhana"
    },
    "grob": {
        "de": "grob", "en": "coarse", "es": "grueso", "pt": "grosso",
        "id": "kasar", "nl": "grof", "sv": "grov", "da": "grov",
        "no": "grov", "ms": "kasar"
    },
    "dünn": {
        "de": "dünn", "en": "thin", "es": "fino", "pt": "fino",
        "id": "tipis", "nl": "dun", "sv": "tunn", "da": "tynd",
        "no": "tynn", "ms": "nipis"
    },

    # Heat levels
    "mittlerer": {
        "de": "mittlerer", "en": "medium", "es": "medio", "pt": "médio",
        "id": "sedang", "nl": "middel", "sv": "medel", "da": "mellem",
        "no": "middels", "ms": "sederhana"
    },
    "hoher": {
        "de": "hoher", "en": "high", "es": "alto", "pt": "alto",
        "id": "tinggi", "nl": "hoog", "sv": "hög", "da": "høj",
        "no": "høy", "ms": "tinggi"
    },

    # Methods
    "vorsichtig": {
        "de": "vorsichtig", "en": "carefully", "es": "con cuidado", "pt": "cuidadosamente",
        "id": "hati-hati", "nl": "voorzichtig", "sv": "försiktigt", "da": "forsigtigt",
        "no": "forsiktig", "ms": "berhati-hati"
    },

    # Modes
    "Ober-/Unterhitze": {
        "de": "Ober-/Unterhitze", "en": "top/bottom heat", "es": "calor superior e inferior", "pt": "calor superior e inferior",
        "id": "panas atas/bawah", "nl": "boven-/onderwarmte", "sv": "över-/undervärme", "da": "over-/undervarme",
        "no": "over-/undervarme", "ms": "haba atas/bawah"
    },

    # Surfaces
    "einer bemehlten Arbeitsfläche": {
        "de": "einer bemehlten Arbeitsfläche", "en": "a floured surface", "es": "una superficie enharinada", "pt": "uma superfície enfarinhada",
        "id": "permukaan yang ditaburi tepung", "nl": "een bebloemd werkblad", "sv": "en mjölad yta", "da": "en meldrysset overflade",
        "no": "en melstrødd flate", "ms": "permukaan yang ditabur tepung"
    },

    # Combinations
    "verquirltem Ei": {
        "de": "verquirltem Ei", "en": "beaten egg", "es": "huevo batido", "pt": "ovo batido",
        "id": "telur kocok", "nl": "geklopt ei", "sv": "uppvispad ägg", "da": "pisket æg",
        "no": "pisket egg", "ms": "telur pukul"
    },
    "Olivenöl, Salz und Gewürzen": {
        "de": "Olivenöl, Salz und Gewürzen", "en": "olive oil, salt and spices", "es": "aceite de oliva, sal y especias", "pt": "azeite, sal e especiarias",
        "id": "minyak zaitun, garam dan rempah", "nl": "olijfolie, zout en kruiden", "sv": "olivolja, salt och kryddor", "da": "olivenolie, salt og krydderier",
        "no": "olivenolje, salt og krydder", "ms": "minyak zaitun, garam dan rempah"
    },
    "eine homogene Masse": {
        "de": "eine homogene Masse", "en": "a homogeneous mixture", "es": "una mezcla homogénea", "pt": "uma mistura homogênea",
        "id": "campuran homogen", "nl": "een homogeen mengsel", "sv": "en homogen massa", "da": "en homogen masse",
        "no": "en homogen masse", "ms": "campuran seragam"
    },
}

def translate_value(value):
    """Translate a string value to all 10 languages"""
    if not isinstance(value, str):
        return value

    # Check if it's a technical value that doesn't need translation
    if re.match(r'^\d+[°C]?$', value):  # Temperature or count
        return value

    if value.startswith('🍞') or value.startswith('🍳') or value.startswith('♨️') or value.startswith('🔥'):
        # Has emoji prefix - translate the rest
        parts = value.split(' ', 1)
        if len(parts) == 2 and parts[1] in TRANSLATIONS:
            emoji = parts[0]
            result = {}
            for lang, trans in TRANSLATIONS[parts[1]].items():
                result[lang] = f"{emoji} {trans}"
            return result

    # Check direct translation
    if value in TRANSLATIONS:
        return TRANSLATIONS[value]

    # Check for time patterns like "30 Minuten", "3-5 Minuten pro Seite"
    time_match = re.match(r'^(\d+-?\d*)\s+(Minuten|Sekunden)(.*)$', value)
    if time_match:
        number, unit, suffix = time_match.groups()
        if unit in TRANSLATIONS:
            result = {}
            unit_trans = TRANSLATIONS[unit]
            for lang in unit_trans:
                trans_suffix = ""
                if " pro Seite" in suffix:
                    trans_suffix = {
                        "de": " pro Seite", "en": " per side", "es": " por lado", "pt": " por lado",
                        "id": " per sisi", "nl": " per kant", "sv": " per sida", "da": " per side",
                        "no": " per side", "ms": " setiap sisi"
                    }[lang]
                result[lang] = f"{number} {unit_trans[lang]}{trans_suffix}"
            return result

    # Check for "laut Packungsangabe" patterns
    if "laut Packungsangabe" in value or "Packungsangabe" in value:
        return {
            "de": "laut Packungsangabe", "en": "according to package instructions",
            "es": "según las instrucciones del paquete", "pt": "de acordo com as instruções da embalagem",
            "id": "sesuai petunjuk kemasan", "nl": "volgens pakketinstructies",
            "sv": "enligt förpackningens anvisningar", "da": "ifølge pakkens anvisninger",
            "no": "ifølge pakkens instruksjoner", "ms": "mengikut arahan bungkusan"
        }

    # If no translation found, return the original value (will need manual review)
    print(f"WARNING: No translation found for: '{value}'")
    return {"de": value}  # At least keep German

def migrate_object(obj):
    """Recursively migrate all string values in an object"""
    if isinstance(obj, dict):
        result = {}
        for key, value in obj.items():
            if isinstance(value, str):
                result[key] = translate_value(value)
            elif isinstance(value, dict):
                result[key] = migrate_object(value)
            else:
                result[key] = value
        return result
    return obj

def main():
    input_file = Path(__file__).parent.parent / "DelikatessenDrehbuch" / "wwwroot" / "data" / "recipe_type_step_variables.json"
    output_file = input_file.parent / "recipe_type_step_variables_v4.json"

    print(f"Reading: {input_file}")
    with open(input_file, 'r', encoding='utf-8') as f:
        data = json.load(f)

    print("Migrating to v4.0...")
    data['version'] = '4.0'
    data['description'] = data['description'] + ' V4.0: Vollständig mehrsprachig (10 Sprachen).'

    # Migrate defaults
    if 'defaults' in data:
        print("Migrating defaults...")
        data['defaults'] = migrate_object(data['defaults'])

    # Migrate types
    if 'types' in data:
        print("Migrating types...")
        data['types'] = migrate_object(data['types'])

    print(f"Writing: {output_file}")
    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(data, f, ensure_ascii=False, indent=2)

    print("Migration complete!")
    print(f"Review the output file and check for any WARNING messages above.")

if __name__ == '__main__':
    main()
