import json
import os

# Define new simple steps with translations
new_steps = [
    {
        "master_id": "COOK_BLEND_SIMPLE_01",
        "phase": 2,
        "action": "blend",
        "description": "Püriere alles mit einem Stabmixer",
        "equipment": 0,
        "variables": [],
        "required_variables": [],
        "templates": {
            "de": "Püriere alles mit einem Stabmixer.",
            "en": "Blend everything with an immersion blender.",
            "esp": "Tritura todo con una batidora de mano.",
            "prt": "Triture tudo com uma varinha mágica.",
            "id": "Haluskan semuanya dengan blender tangan.",
            "nl": "Pureer alles met een staafmixer.",
            "sv": "Mixa allt med en stavmixer.",
            "da": "Blend alt med en stavblender.",
            "no": "Blend alt med en stavmikser.",
            "ms": "Kisar semua dengan pengisar погружении."
        }
    },
    {
        "master_id": "COOK_BOIL_SIMPLE_01",
        "phase": 2,
        "action": "boil",
        "description": "Lass alles aufkochen",
        "equipment": 3,
        "variables": [],
        "required_variables": [],
        "templates": {
            "de": "Lass alles aufkochen.",
            "en": "Bring everything to a boil.",
            "esp": "Deja que todo hierva.",
            "prt": "Deixe tudo ferver.",
            "id": "Biarkan semuanya mendidih.",
            "nl": "Laat alles aan de kook komen.",
            "sv": "Låt allt koka upp.",
            "da": "Lad alt koge op.",
            "no": "La alt koke opp.",
            "ms": "Biarkan semuanya mendidih."
        }
    },
    {
        "master_id": "COOK_BOIL_AGAIN_01",
        "phase": 2,
        "action": "boil",
        "description": "Lass alles nochmal kurz aufkochen",
        "equipment": 3,
        "variables": [],
        "required_variables": [],
        "templates": {
            "de": "Lass alles nochmal kurz aufkochen.",
            "en": "Bring everything to a boil again briefly.",
            "esp": "Deja que todo hierva brevemente de nuevo.",
            "prt": "Deixe tudo ferver brevemente novamente.",
            "id": "Biarkan semuanya mendidih sebentar lagi.",
            "nl": "Laat alles nog even aan de kook komen.",
            "sv": "Låt allt koka upp igen kort.",
            "da": "Lad alt koge op igen kort.",
            "no": "La alt koke opp igjen kort.",
            "ms": "Biarkan semuanya mendidih sebentar lagi."
        }
    },
    {
        "master_id": "COOK_REDUCE_HEAT_01",
        "phase": 2,
        "action": "adjust_heat",
        "description": "Reduziere die Hitze",
        "equipment": 0,
        "variables": [],
        "required_variables": [],
        "templates": {
            "de": "Reduziere die Hitze.",
            "en": "Reduce the heat.",
            "esp": "Reduce el fuego.",
            "prt": "Reduza o fogo.",
            "id": "Kurangi panasnya.",
            "nl": "Verlaag het vuur.",
            "sv": "Sänk värmen.",
            "da": "Skru ned for varmen.",
            "no": "Reduser varmen.",
            "ms": "Kurangkan haba."
        }
    }
]

# Language file mapping
lang_files = {
    "de": "master_steps.de.json",
    "en": "master_steps.en.json",
    "esp": "master_steps.esp.json",
    "prt": "master_steps.prt.json",
    "id": "master_steps.id.json",
    "nl": "master_steps.nl.json",
    "sv": "master_steps.sv.json",
    "da": "master_steps.da.json",
    "no": "master_steps.no.json",
    "ms": "master_steps.ms.json"
}

# Process each language file
for lang_key, filename in lang_files.items():
    filepath = os.path.join(os.path.dirname(__file__), filename)

    if not os.path.exists(filepath):
        print(f"[SKIP] {filename} not found, skipping...")
        continue

    # Read existing file
    with open(filepath, 'r', encoding='utf-8') as f:
        data = json.load(f)

    # Add new steps with language-specific templates
    for step_def in new_steps:
        # Create step with single template for this language
        new_step = {
            "master_id": step_def["master_id"],
            "phase": step_def["phase"],
            "action": step_def["action"],
            "description": step_def["description"],
            "equipment": step_def["equipment"],
            "template": step_def["templates"][lang_key],  # Single template field
            "variables": step_def["variables"],
            "required_variables": step_def["required_variables"]
        }

        # Check if step already exists
        if not any(s.get("master_id") == new_step["master_id"] for s in data["master_steps"]):
            data["master_steps"].append(new_step)
            print(f"[OK] Added {new_step['master_id']} to {filename}")
        else:
            print(f"[EXISTS] {new_step['master_id']} already exists in {filename}")

    # Save updated file
    with open(filepath, 'w', encoding='utf-8') as f:
        json.dump(data, f, ensure_ascii=False, indent=2)

    print(f"[SAVED] {filename}\n")

print("[DONE] Added 4 new simple steps to all language files.")
