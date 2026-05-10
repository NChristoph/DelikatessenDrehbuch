#!/usr/bin/env python3
"""
Split master_steps.json and master_step_variables.json into separate language files.
Each file will contain only templates and variable labels for its specific language.
Variable IDs remain the same across all languages for easy comparison and DB storage.
"""

import json
import os
from pathlib import Path
import re

# Supported languages
LANGUAGES = ["de", "en", "esp", "prt", "id", "nl", "sv", "da", "no", "ms"]

def sanitize_id_part(text):
    """Sanitize text for use in IDs (remove special chars, lowercase)."""
    if not text:
        return ""
    # Replace umlauts
    text = text.replace('ä', 'ae').replace('ö', 'oe').replace('ü', 'ue')
    text = text.replace('Ä', 'Ae').replace('Ö', 'Oe').replace('Ü', 'Ue')
    text = text.replace('ß', 'ss')
    # Keep only alphanumeric and underscore
    text = re.sub(r'[^a-zA-Z0-9_]', '_', text)
    # Remove multiple underscores
    text = re.sub(r'_+', '_', text)
    return text.lower().strip('_')

def load_variable_options(variables_file):
    """Load master_step_variables.json and prepare for splitting."""
    if not os.path.exists(variables_file):
        print(f"Warning: {variables_file} not found, skipping variable options.")
        return None

    with open(variables_file, 'r', encoding='utf-8') as f:
        return json.load(f)

def create_language_variable_options(variables_data, language):
    """
    Create variable_options for a specific language with language-independent IDs.
    IDs are the same across all languages, only labels change.
    """
    if not variables_data or 'variables' not in variables_data:
        return {}

    result = {}

    for var_name, var_data in variables_data['variables'].items():
        if 'options' not in var_data:
            continue

        options_list = []
        for idx, option in enumerate(var_data['options'], start=1):
            key = option.get('key', '')
            labels = option.get('labels', {})
            tags = option.get('tags', [])

            # Create language-independent ID based on variable name and key
            # Format: variablename_key_index
            sanitized_key = sanitize_id_part(key)
            option_id = f"{var_name}_{sanitized_key}_{idx}"

            # Get label for this specific language
            label = labels.get(language, labels.get('de', labels.get('en', key)))

            option_obj = {
                "id": option_id,
                "key": key,
                "label": label
            }

            # Include tags if they exist
            if tags:
                option_obj["tags"] = tags

            options_list.append(option_obj)

        if options_list:
            result[var_name] = options_list

    return result

def extract_language_from_object(obj, language):
    """Extract only the specified language from a multilingual object."""
    if not isinstance(obj, dict):
        return obj

    # If this is a multilingual dictionary (has language keys)
    if language in obj and all(k in LANGUAGES or k in ["de", "en", "esp", "prt", "id", "nl", "sv", "da", "no", "ms"] for k in obj.keys()):
        return obj.get(language, obj.get("de", ""))

    # Otherwise, recursively process all values
    result = {}
    for key, value in obj.items():
        if isinstance(value, dict):
            result[key] = extract_language_from_object(value, language)
        elif isinstance(value, list):
            result[key] = [extract_language_from_object(item, language) for item in value]
        else:
            result[key] = value
    return result

def split_master_steps(input_file, variables_file, output_dir):
    """Split master_steps.json and master_step_variables.json into language-specific files."""

    # Read the master steps file
    print(f"Reading {input_file}...")
    with open(input_file, 'r', encoding='utf-8') as f:
        data = json.load(f)

    # Read the variables file
    print(f"Reading {variables_file}...")
    variables_data = load_variable_options(variables_file)

    # Process each language
    for lang in LANGUAGES:
        print(f"Processing language: {lang}...")

        # Create a deep copy for this language
        lang_data = {}

        # Process _meta
        if "_meta" in data:
            lang_data["_meta"] = {
                "version": data["_meta"]["version"],
                "created_at": data["_meta"]["created_at"],
                "updated_at": data["_meta"]["updated_at"],
                "language": lang,
                "description": data["_meta"]["description"],
                "usage": extract_language_from_object(data["_meta"]["usage"], lang) if "usage" in data["_meta"] else {},
            }

            # Add stable_key_policy for this language if available
            if "stable_key_policy" in data["_meta"] and lang in data["_meta"]["stable_key_policy"]:
                lang_data["_meta"]["stable_key_policy"] = data["_meta"]["stable_key_policy"][lang]

        # Process phases - extract only this language's labels
        if "phases" in data:
            lang_data["phases"] = {}
            for phase_key, phase_value in data["phases"].items():
                lang_data["phases"][phase_key] = {
                    "label": phase_value["label"].get(lang, phase_value["label"].get("de", "")),
                    "icon": phase_value.get("icon", ""),
                }
                if "description" in phase_value:
                    lang_data["phases"][phase_key]["description"] = phase_value["description"].get(lang, phase_value["description"].get("de", ""))

        # Process sub_groups - extract only this language's labels
        if "sub_groups" in data:
            lang_data["sub_groups"] = {}
            for group_key, group_value in data["sub_groups"].items():
                lang_data["sub_groups"][group_key] = {
                    "label": group_value["label"].get(lang, group_value["label"].get("de", "")),
                    "icon": group_value.get("icon", ""),
                }
                if "description" in group_value:
                    lang_data["sub_groups"][group_key]["description"] = group_value["description"].get(lang, group_value["description"].get("de", ""))

        # Process variable_types (if present)
        if "variable_types" in data:
            lang_data["variable_types"] = {}
            for var_key, var_value in data["variable_types"].items():
                lang_data["variable_types"][var_key] = {
                    "label": var_value["label"].get(lang, var_value["label"].get("de", "")),
                }
                if "description" in var_value:
                    lang_data["variable_types"][var_key]["description"] = var_value["description"].get(lang, var_value["description"].get("de", ""))

        # Process master_steps - keep structure but only this language's template
        if "master_steps" in data:
            lang_data["master_steps"] = []
            for step in data["master_steps"]:
                lang_step = {}

                # Copy all non-template fields
                for key, value in step.items():
                    if key == "templates":
                        # Only include this language's template
                        if lang in value:
                            lang_step["template"] = value[lang]
                    else:
                        lang_step[key] = value

                lang_data["master_steps"].append(lang_step)

        # Add variable_options for this language (with language-independent IDs)
        if variables_data:
            print(f"  Adding variable_options for {lang}...")
            lang_data["variable_options"] = create_language_variable_options(variables_data, lang)

        # Write the language-specific file
        output_file = os.path.join(output_dir, f"master_steps.{lang}.json")
        print(f"Writing {output_file}...")
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(lang_data, f, ensure_ascii=False, indent=2)

        # Get file size
        size_kb = os.path.getsize(output_file) / 1024
        print(f"  - {output_file} ({size_kb:.1f} KB)")

    print("\nDone! Split complete.")

    # Summary
    original_size = os.path.getsize(input_file) / 1024
    print(f"\nOriginal file: {original_size:.1f} KB")
    print(f"Split into {len(LANGUAGES)} files of ~{original_size / len(LANGUAGES):.1f} KB each")

if __name__ == "__main__":
    # Get the directory of this script
    script_dir = Path(__file__).parent
    input_file = script_dir / "master_steps.json"
    variables_file = script_dir / "master_step_variables.json"
    output_dir = script_dir

    if not input_file.exists():
        print(f"Error: {input_file} not found!")
        exit(1)

    split_master_steps(str(input_file), str(variables_file), str(output_dir))
