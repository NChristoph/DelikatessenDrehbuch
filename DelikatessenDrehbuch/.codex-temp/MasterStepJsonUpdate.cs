using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class Program
{
    public static void Main()
    {
        var path = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "wwwroot", "data", "master_steps.json"));
        var root = JObject.Parse(File.ReadAllText(path));
        var meta = (JObject)root["_meta"];

        meta["version"] = new JValue("2.0.0");
        meta["updated_at"] = new JValue("2026-03-12");
        meta["supported_languages"] = new JArray("de", "en", "esp", "prt", "id", "nl", "sv", "da", "no", "ms");
        meta["stable_key_policy"] = ParseObject(@"{
          'de': 'Neue Metadaten verwenden nur stabile semantische Schlüssel wie category_keys oder ingredient_family_keys. Es werden keine DB-IDs und keine freien User-Eingaben als persistente Referenz gespeichert.',
          'en': 'New metadata uses only stable semantic keys such as category_keys or ingredient_family_keys. No DB IDs and no free-form user inputs are stored as persistent references.',
          'esp': 'Los nuevos metadatos usan solo claves semánticas estables como category_keys o ingredient_family_keys. No se guardan IDs de base de datos ni entradas libres del usuario como referencias persistentes.',
          'prt': 'Os novos metadados usam apenas chaves semânticas estáveis como category_keys ou ingredient_family_keys. Não são armazenados IDs de banco de dados nem entradas livres do usuário como referências persistentes.',
          'id': 'Metadata baru hanya memakai kunci semantik stabil seperti category_keys atau ingredient_family_keys. Tidak ada ID database maupun input bebas pengguna yang disimpan sebagai referensi permanen.',
          'nl': 'Nieuwe metadata gebruikt alleen stabiele semantische sleutels zoals category_keys of ingredient_family_keys. Er worden geen database-IDs en geen vrije gebruikersinvoer als blijvende referentie opgeslagen.',
          'sv': 'Nya metadata använder bara stabila semantiska nycklar som category_keys eller ingredient_family_keys. Inga databas-ID:n och inga fria användarinmatningar lagras som beständiga referenser.',
          'da': 'Nye metadata bruger kun stabile semantiske nøgler som category_keys eller ingredient_family_keys. Der lagres ingen database-id''er og ingen frie brugerinput som permanente referencer.',
          'no': 'Ny metadata bruker bare stabile semantiske nøkl...'
        }");

        var changes = (JArray)meta["changes"];
        AddUnique(changes, "Added multilingual taxonomy definitions for variable types, step intents, recipe categories, diets, ingredient families, and quality rules.");
        AddUnique(changes, "Added stable key policy: master-step metadata stores only semantic keys, never DB IDs or user-entered database references.");
        AddUnique(changes, "Extended all master steps with backwards-compatible fields: required_variables, optional_variables, variable_types, step_intent, stable_taxonomy_keys, quality_rule_keys, and creator_guidance.");

        root["variable_type_definitions"] = ParseObject(@"{
          'ingredient': { 'de': 'Einzelne Zutat', 'en': 'Single ingredient', 'esp': 'Ingrediente individual', 'prt': 'Ingrediente individual', 'id': 'Bahan tunggal', 'nl': 'Enkel ingrediënt', 'sv': 'Enskild ingrediens', 'da': 'Enkelt ingrediens', 'no': 'Enkelt ingrediens', 'ms': 'Bahan tunggal' },
          'ingredient_list': { 'de': 'Liste mehrerer Zutaten', 'en': 'List of ingredients', 'esp': 'Lista de ingredientes', 'prt': 'Lista de ingredientes', 'id': 'Daftar bahan', 'nl': 'Lijst met ingrediënten', 'sv': 'Lista med ingredienser', 'da': 'Liste over ingredienser', 'no': 'Liste over ingredienser', 'ms': 'Senarai bahan' },
          'tool': { 'de': 'Werkzeug oder Küchenhilfe', 'en': 'Tool or kitchen aid', 'esp': 'Herramienta o utensilio', 'prt': 'Ferramenta ou utensílio', 'id': 'Alat atau perkakas dapur', 'nl': 'Gereedschap of keukentool', 'sv': 'Verktyg eller kökshjälp', 'da': 'Værktøj eller køkkenhjælp', 'no': 'Verktøy eller kjøkkenhjelp', 'ms': 'Alat atau perkakas dapur' },
          'equipment': { 'de': 'Küchengerät', 'en': 'Kitchen equipment', 'esp': 'Equipo de cocina', 'prt': 'Equipamento de cozinha', 'id': 'Peralatan dapur', 'nl': 'Keukenapparaat', 'sv': 'Köksutrustning', 'da': 'Køkkenudstyr', 'no': 'Kjøkkenutstyr', 'ms': 'Peralatan dapur' },
          'duration': { 'de': 'Zeitangabe', 'en': 'Duration', 'esp': 'Duración', 'prt': 'Duração', 'id': 'Durasi', 'nl': 'Tijdsduur', 'sv': 'Tid', 'da': 'Varighed', 'no': 'Varighet', 'ms': 'Tempoh' },
          'temperature': { 'de': 'Temperaturangabe', 'en': 'Temperature', 'esp': 'Temperatura', 'prt': 'Temperatura', 'id': 'Suhu', 'nl': 'Temperatuur', 'sv': 'Temperatur', 'da': 'Temperatur', 'no': 'Temperatur', 'ms': 'Suhu' },
          'state': { 'de': 'Zielzustand', 'en': 'Target state', 'esp': 'Estado objetivo', 'prt': 'Estado desejado', 'id': 'Keadaan sasaran', 'nl': 'Doeltoestand', 'sv': 'Måltillstånd', 'da': 'Måltilstand', 'no': 'Måltilstand', 'ms': 'Keadaan sasaran' },
          'liquid': { 'de': 'Flüssigkeit', 'en': 'Liquid', 'esp': 'Líquido', 'prt': 'Líquido', 'id': 'Cecair', 'nl': 'Vloeistof', 'sv': 'Vätska', 'da': 'Væske', 'no': 'Væske', 'ms': 'Cecair' },
          'quantity': { 'de': 'Mengenangabe', 'en': 'Quantity', 'esp': 'Cantidad', 'prt': 'Quantidade', 'id': 'Kuantiti', 'nl': 'Hoeveelheid', 'sv': 'Mängd', 'da': 'Mængde', 'no': 'Mengde', 'ms': 'Kuantiti' },
          'pronoun': { 'de': 'Pronomenform', 'en': 'Pronoun form', 'esp': 'Forma de pronombre', 'prt': 'Forma de pronome', 'id': 'Bentuk kata ganti', 'nl': 'Voornaamwoordvorm', 'sv': 'Pronomenform', 'da': 'Pronomenform', 'no': 'Pronomenform', 'ms': 'Bentuk kata ganti' },
          'free_text': { 'de': 'Freitext mit Fallback', 'en': 'Free text with fallback', 'esp': 'Texto libre con fallback', 'prt': 'Texto livre com fallback', 'id': 'Teks bebas dengan fallback', 'nl': 'Vrije tekst met fallback', 'sv': 'Fri text med fallback', 'da': 'Fri tekst med fallback', 'no': 'Fritekst med fallback', 'ms': 'Teks bebas dengan fallback' }
        }");

        root["step_intent_definitions"] = ParseObject(@"{
          'prep_base': { 'de': 'Grundvorbereitung', 'en': 'Base prep', 'esp': 'Preparación base', 'prt': 'Preparação base', 'id': 'Persiapan asas', 'nl': 'Basisvoorbereiding', 'sv': 'Grundförberedelse', 'da': 'Grundforberedelse', 'no': 'Grunnforberedelse', 'ms': 'Persediaan asas' },
          'prep_cut': { 'de': 'Schneiden und Zerkleinern', 'en': 'Cutting and chopping', 'esp': 'Corte y troceado', 'prt': 'Corte e picado', 'id': 'Memotong dan mencincang', 'nl': 'Snijden en hakken', 'sv': 'Skära och hacka', 'da': 'Skære og hakke', 'no': 'Skjære og hakke', 'ms': 'Memotong dan mencincang' },
          'prep_mix': { 'de': 'Mischen und Vorbereiten', 'en': 'Mixing and preparation', 'esp': 'Mezcla y preparación', 'prt': 'Mistura e preparação', 'id': 'Mencampur dan persiapan', 'nl': 'Mengen en voorbereiden', 'sv': 'Blanda och förbereda', 'da': 'Blande og forberede', 'no': 'Blande og forberede', 'ms': 'Mencampur dan persediaan' },
          'cook_heat': { 'de': 'Erhitzen und Garen', 'en': 'Heating and cooking', 'esp': 'Calentar y cocinar', 'prt': 'Aquecer e cozinhar', 'id': 'Memanaskan dan memasak', 'nl': 'Verhitten en garen', 'sv': 'Värma och tillaga', 'da': 'Opvarme og tilberede', 'no': 'Varme opp og tilberede', 'ms': 'Memanaskan dan memasak' },
          'cook_liquid': { 'de': 'Kochen mit Flüssigkeit', 'en': 'Liquid cooking', 'esp': 'Cocción con líquido', 'prt': 'Cozimento com líquido', 'id': 'Memasak dengan cecair', 'nl': 'Koken met vloeistof', 'sv': 'Tillagning med vätska', 'da': 'Tilberedning med væske', 'no': 'Tilberedning med væske', 'ms': 'Memasak dengan cecair' },
          'finish_season': { 'de': 'Abschmecken und Finalisieren', 'en': 'Season and finish', 'esp': 'Sazonar y terminar', 'prt': 'Temperar e finalizar', 'id': 'Perasakan dan kemaskan', 'nl': 'Afmaken en op smaak brengen', 'sv': 'Krydda och avsluta', 'da': 'Smage til og afslutte', 'no': 'Smake til og fullføre', 'ms': 'Perasakan dan kemaskan' },
          'finish_serve': { 'de': 'Anrichten und Servieren', 'en': 'Plate and serve', 'esp': 'Emplatar y servir', 'prt': 'Empratar e servir', 'id': 'Hidang', 'nl': 'Opdienen', 'sv': 'Servera', 'da': 'Anrette og servere', 'no': 'Anrette og servere', 'ms': 'Hidang' }
        }");

        root["category_taxonomy"] = ParseObject(@"{
          'appetizer': { 'de': 'Vorspeise', 'en': 'Appetizer', 'esp': 'Entrante', 'prt': 'Entrada', 'id': 'Pembuka selera', 'nl': 'Voorgerecht', 'sv': 'Förrätt', 'da': 'Forret', 'no': 'Forrett', 'ms': 'Pembuka selera' },
          'main': { 'de': 'Hauptspeise', 'en': 'Main course', 'esp': 'Plato principal', 'prt': 'Prato principal', 'id': 'Hidangan utama', 'nl': 'Hoofdgerecht', 'sv': 'Huvudrätt', 'da': 'Hovedret', 'no': 'Hovedrett', 'ms': 'Hidangan utama' },
          'dessert': { 'de': 'Dessert', 'en': 'Dessert', 'esp': 'Postre', 'prt': 'Sobremesa', 'id': 'Pencuci mulut', 'nl': 'Dessert', 'sv': 'Dessert', 'da': 'Dessert', 'no': 'Dessert', 'ms': 'Pencuci mulut' },
          'soup': { 'de': 'Suppe', 'en': 'Soup', 'esp': 'Sopa', 'prt': 'Sopa', 'id': 'Sup', 'nl': 'Soep', 'sv': 'Soppa', 'da': 'Suppe', 'no': 'Suppe', 'ms': 'Sup' },
          'salad': { 'de': 'Salat', 'en': 'Salad', 'esp': 'Ensalada', 'prt': 'Salada', 'id': 'Salad', 'nl': 'Salade', 'sv': 'Sallad', 'da': 'Salat', 'no': 'Salat', 'ms': 'Salad' },
          'baked': { 'de': 'Ofengericht', 'en': 'Baked dish', 'esp': 'Plato al horno', 'prt': 'Prato assado', 'id': 'Hidangan bakar', 'nl': 'Ovengerecht', 'sv': 'Ugnsrätt', 'da': 'Ovnbagt ret', 'no': 'Ovnsrett', 'ms': 'Hidangan bakar' },
          'pan': { 'de': 'Pfannengericht', 'en': 'Pan dish', 'esp': 'Plato en sartén', 'prt': 'Prato de frigideira', 'id': 'Hidangan kuali', 'nl': 'Pangerecht', 'sv': 'Pannrätt', 'da': 'Panderet', 'no': 'Pannerett', 'ms': 'Hidangan kuali' }
        }");

        root["diet_taxonomy"] = ParseObject(@"{
          'all': { 'de': 'Alles', 'en': 'All', 'esp': 'Todo', 'prt': 'Tudo', 'id': 'Semua', 'nl': 'Alles', 'sv': 'Allt', 'da': 'Alle', 'no': 'Alle', 'ms': 'Semua' },
          'vegetarian': { 'de': 'Vegetarisch', 'en': 'Vegetarian', 'esp': 'Vegetariano', 'prt': 'Vegetariano', 'id': 'Vegetarian', 'nl': 'Vegetarisch', 'sv': 'Vegetarisk', 'da': 'Vegetarisk', 'no': 'Vegetarisk', 'ms': 'Vegetarian' },
          'vegan': { 'de': 'Vegan', 'en': 'Vegan', 'esp': 'Vegano', 'prt': 'Vegano', 'id': 'Vegan', 'nl': 'Vegan', 'sv': 'Vegansk', 'da': 'Vegansk', 'no': 'Vegansk', 'ms': 'Vegan' },
          'pescatarian': { 'de': 'Pescetarisch', 'en': 'Pescatarian', 'esp': 'Pescetariano', 'prt': 'Pescetariano', 'id': 'Pescatarian', 'nl': 'Pescotarisch', 'sv': 'Pescetariansk', 'da': 'Pescetarisk', 'no': 'Pescetarisk', 'ms': 'Pescatarian' },
          'protein_focus': { 'de': 'Proteinreich', 'en': 'High protein', 'esp': 'Alto en proteína', 'prt': 'Rico em proteína', 'id': 'Tinggi protein', 'nl': 'Eiwitrijk', 'sv': 'Proteinrik', 'da': 'Proteinrig', 'no': 'Proteinrik', 'ms': 'Tinggi protein' }
        }");

        root["ingredient_family_definitions"] = ParseObject(@"{
          'vegetable': { 'de': 'Gemüse', 'en': 'Vegetable', 'esp': 'Verdura', 'prt': 'Legume', 'id': 'Sayur', 'nl': 'Groente', 'sv': 'Grönsak', 'da': 'Grøntsag', 'no': 'Grønnsak', 'ms': 'Sayur' },
          'fruit': { 'de': 'Obst', 'en': 'Fruit', 'esp': 'Fruta', 'prt': 'Fruta', 'id': 'Buah', 'nl': 'Fruit', 'sv': 'Frukt', 'da': 'Frugt', 'no': 'Frukt', 'ms': 'Buah' },
          'herb': { 'de': 'Kräuter', 'en': 'Herb', 'esp': 'Hierba', 'prt': 'Erva', 'id': 'Herba', 'nl': 'Kruid', 'sv': 'Ört', 'da': 'Urt', 'no': 'Urt', 'ms': 'Herba' },
          'dairy': { 'de': 'Milchprodukt', 'en': 'Dairy', 'esp': 'Lácteo', 'prt': 'Laticínio', 'id': 'Produk tenusu', 'nl': 'Zuivel', 'sv': 'Mejeri', 'da': 'Mejeri', 'no': 'Meieri', 'ms': 'Produk tenusu' },
          'meat': { 'de': 'Fleisch', 'en': 'Meat', 'esp': 'Carne', 'prt': 'Carne', 'id': 'Daging', 'nl': 'Vlees', 'sv': 'Kött', 'da': 'Kød', 'no': 'Kjøtt', 'ms': 'Daging' },
          'fish': { 'de': 'Fisch', 'en': 'Fish', 'esp': 'Pescado', 'prt': 'Peixe', 'id': 'Ikan', 'nl': 'Vis', 'sv': 'Fisk', 'da': 'Fisk', 'no': 'Fisk', 'ms': 'Ikan' },
          'dough': { 'de': 'Teig', 'en': 'Dough', 'esp': 'Masa', 'prt': 'Massa', 'id': 'Doh', 'nl': 'Deeg', 'sv': 'Deg', 'da': 'Dej', 'no': 'Deig', 'ms': 'Doh' },
          'sauce': { 'de': 'Sauce', 'en': 'Sauce', 'esp': 'Salsa', 'prt': 'Molho', 'id': 'Sos', 'nl': 'Saus', 'sv': 'Sås', 'da': 'Sauce', 'no': 'Saus', 'ms': 'Sos' },
          'liquid': { 'de': 'Flüssigkeit', 'en': 'Liquid', 'esp': 'Líquido', 'prt': 'Líquido', 'id': 'Cecair', 'nl': 'Vloeistof', 'sv': 'Vätska', 'da': 'Væske', 'no': 'Væske', 'ms': 'Cecair' },
          'seasoning': { 'de': 'Gewürz', 'en': 'Seasoning', 'esp': 'Condimento', 'prt': 'Tempero', 'id': 'Perasa', 'nl': 'Kruiding', 'sv': 'Krydda', 'da': 'Krydderi', 'no': 'Krydder', 'ms': 'Perasa' }
        }");

        root["quality_rule_definitions"] = ParseObject(@"{
          'requires_previous_phase': { 'de': 'Benötigt eine frühere Phase', 'en': 'Requires an earlier phase', 'esp': 'Requiere una fase anterior', 'prt': 'Requer uma fase anterior', 'id': 'Memerlukan fasa sebelumnya', 'nl': 'Vereist een eerdere fase', 'sv': 'Kräver en tidigare fas', 'da': 'Kræver en tidligere fase', 'no': 'Krever en tidligere fase', 'ms': 'Memerlukan fasa sebelumnya' },
          'requires_heat_source': { 'de': 'Benötigt Hitzequelle', 'en': 'Requires heat source', 'esp': 'Requiere fuente de calor', 'prt': 'Requer fonte de calor', 'id': 'Memerlukan sumber haba', 'nl': 'Vereist warmtebron', 'sv': 'Kräver värmekälla', 'da': 'Kræver varmekilde', 'no': 'Krever varmekilde', 'ms': 'Memerlukan sumber haba' },
          'best_with_visible_result': { 'de': 'Ideal für sichtbares Ergebnis im Video', 'en': 'Best for visible video result', 'esp': 'Ideal para un resultado visible en video', 'prt': 'Ideal para resultado visível em vídeo', 'id': 'Sesuai untuk hasil video yang jelas', 'nl': 'Ideaal voor zichtbaar videoresultaat', 'sv': 'Bra för synligt videoresultat', 'da': 'God til synligt videoresultat', 'no': 'God for synlig videoeffekt', 'ms': 'Sesuai untuk hasil video yang jelas' }
        }");

        var variableTypeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "ingredient", "ingredient" }, { "ingredients", "ingredient_list" }, { "pronoun", "pronoun" }, { "tool", "tool" },
            { "shape", "free_text" }, { "grind_size", "free_text" }, { "marinade", "free_text" }, { "duration", "duration" },
            { "liquid", "liquid" }, { "equipment", "equipment" }, { "quantity", "quantity" }, { "temperature", "temperature" },
            { "temp", "temperature" }, { "heat", "state" }, { "spice_mix", "free_text" }, { "seasonings", "free_text" },
            { "spices", "free_text" }, { "sauce", "free_text" }, { "target_consistency", "state" }, { "garnish", "free_text" },
            { "serving_style", "free_text" }, { "side", "free_text" }, { "item", "ingredient" }, { "base", "ingredient" },
            { "state", "state" }, { "balance", "free_text" }, { "count", "quantity" }
        };

        foreach (JObject step in (JArray)root["master_steps"])
        {
            var variables = step["variables"] != null ? step["variables"].Values<string>().ToList() : new List<string>();
            var aliases = step["aliases"] != null ? step["aliases"].Values<string>().ToList() : new List<string>();
            var action = (string)(step["action"] ?? "");
            var phase = step["phase"] != null ? (int)step["phase"] : 0;

            step["required_variables"] = new JArray(variables);
            step["optional_variables"] = new JArray(aliases);

            var types = new JObject();
            foreach (var name in variables.Concat(aliases).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                types[name] = new JValue(variableTypeMap.ContainsKey(name) ? variableTypeMap[name] : "free_text");
            }
            step["variable_types"] = types;

            var intent = GetStepIntent(action, phase);
            step["step_intent"] = new JValue(intent);
            step["stable_taxonomy_keys"] = new JObject
            {
                { "category_keys", new JArray(GetCategoryKeys(action, phase)) },
                { "diet_keys", new JArray(GetDietKeys(action)) },
                { "ingredient_family_keys", new JArray(GetIngredientFamilyKeys(action, variables)) },
                { "keyword_keys", new JArray(action, intent) }
            };
            step["quality_rule_keys"] = new JArray(GetQualityRuleKeys(action, phase));
            step["creator_guidance"] = BuildCreatorGuidance(GetEstimatedClipSeconds(action));
        }

        File.WriteAllText(path, root.ToString(Formatting.Indented));
    }

    static JObject ParseObject(string json)
    {
        return JObject.Parse(json);
    }

    static void AddUnique(JArray array, string value)
    {
        if (!array.Values<string>().Contains(value))
        {
            array.Add(value);
        }
    }

    static string GetStepIntent(string action, int phase)
    {
        switch (action)
        {
            case "wash":
            case "peel": return "prep_base";
            case "cut":
            case "grate":
            case "mince": return "prep_cut";
            case "marinate":
            case "soak":
            case "mix_dry":
            case "knead":
            case "dough_rise":
            case "portion":
            case "roll_out":
            case "bread_3step":
            case "whisk":
            case "fold_in":
            case "layer": return "prep_mix";
            case "heat":
            case "saute":
            case "fry":
            case "deepfry":
            case "bake":
            case "roast":
            case "grill":
            case "stir":
            case "add":
            case "gratinate": return "cook_heat";
            case "boil":
            case "simmer":
            case "steam":
            case "deglaze":
            case "reduce":
            case "thicken":
            case "drain":
            case "strain":
            case "cover":
            case "braise": return "cook_liquid";
            case "blend":
            case "season_adjust": return "finish_season";
            case "cool":
            case "serve": return "finish_serve";
            default:
                if (phase <= 1) return "prep_base";
                if (phase == 2) return "cook_heat";
                if (phase == 3) return "finish_season";
                return "finish_serve";
        }
    }

    static IEnumerable<string> GetCategoryKeys(string action, int phase)
    {
        var keys = new List<string>();
        if (phase == 4) keys.Add("main");
        if (new[] { "boil", "simmer", "blend", "cover" }.Contains(action)) keys.Add("soup");
        if (new[] { "bake", "roast", "gratinate" }.Contains(action)) keys.Add("baked");
        if (new[] { "saute", "fry", "deepfry", "stir" }.Contains(action)) keys.Add("pan");
        if (phase <= 1) keys.Add("main");
        return keys.Distinct();
    }

    static IEnumerable<string> GetDietKeys(string action)
    {
        return new[] { "grill", "roast", "braise", "deepfry" }.Contains(action)
            ? new[] { "all" }
            : new[] { "all", "vegetarian", "vegan" };
    }

    static IEnumerable<string> GetIngredientFamilyKeys(string action, List<string> variables)
    {
        var keys = new List<string>();
        if (variables.Contains("ingredient") || variables.Contains("ingredients")) keys.Add("vegetable");
        if (variables.Contains("liquid")) keys.Add("liquid");
        if (variables.Contains("sauce")) keys.Add("sauce");
        if (variables.Contains("spice_mix") || variables.Contains("seasonings") || variables.Contains("spices")) keys.Add("seasoning");
        if (new[] { "mix_dry", "knead", "roll_out", "dough_rise" }.Contains(action)) keys.Add("dough");
        if (new[] { "marinate", "braise", "roast", "grill", "fry", "deepfry" }.Contains(action)) keys.Add("meat");
        if (new[] { "whisk", "fold_in", "grate" }.Contains(action)) keys.Add("dairy");
        return keys.Distinct();
    }

    static IEnumerable<string> GetQualityRuleKeys(string action, int phase)
    {
        var keys = new List<string>();
        if (phase >= 2) keys.Add("requires_previous_phase");
        if (new[] { "saute", "fry", "deepfry", "boil", "simmer", "bake", "roast", "grill", "gratinate", "braise" }.Contains(action)) keys.Add("requires_heat_source");
        if (new[] { "grate", "fry", "deepfry", "gratinate", "serve" }.Contains(action)) keys.Add("best_with_visible_result");
        return keys.Distinct();
    }

    static int GetEstimatedClipSeconds(string action)
    {
        switch (action)
        {
            case "wash": return 4;
            case "cut":
            case "grate":
            case "mince":
            case "boil":
            case "simmer":
            case "bake":
            case "roast":
            case "grill":
            case "serve": return 5;
            case "mix_dry":
            case "saute":
            case "fry":
            case "deepfry":
            case "blend": return 6;
            case "knead": return 7;
            case "dough_rise":
            case "season_adjust": return 4;
            default: return 5;
        }
    }

    static JObject BuildCreatorGuidance(int estimatedClipSeconds)
    {
        return new JObject
        {
            { "estimated_clip_seconds", estimatedClipSeconds },
            { "notes", ParseObject(@"{
              'de': 'Verwendet stabile semantische Schlüssel und ist für mehrsprachiges Rendering vorbereitet.',
              'en': 'Uses stable semantic keys and is prepared for multilingual rendering.',
              'esp': 'Usa claves semánticas estables y está preparado para renderizado multilingüe.',
              'prt': 'Usa chaves semânticas estáveis e está pronto para renderização multilíngue.',
              'id': 'Menggunakan kunci semantik stabil dan siap untuk rendering multibahasa.',
              'nl': 'Gebruikt stabiele semantische sleutels en is voorbereid op meertalige rendering.',
              'sv': 'Använder stabila semantiska nycklar och är förberedd för flerspråkig rendering.',
              'da': 'Bruger stabile semantiske nøgler og er klar til flersproget rendering.',
              'no': 'Bruker stabile semantiske nøkler og er klar for flerspråklig rendering.',
              'ms': 'Menggunakan kunci semantik yang stabil dan sedia untuk paparan berbilang bahasa.'
            }") },
            { "video_shot_hint", ParseObject(@"{
              'de': 'Zeige den sichtbarsten Moment dieses Schritts im Clip.',
              'en': 'Show the most visible moment of this step in the clip.',
              'esp': 'Muestra el momento más visible de este paso en el clip.',
              'prt': 'Mostre o momento mais visível deste passo no clipe.',
              'id': 'Tunjukkan momen paling jelas daripada langkah ini dalam klip.',
              'nl': 'Toon het meest zichtbare moment van deze stap in de clip.',
              'sv': 'Visa det mest synliga ögonblicket i detta steg i klippet.',
              'da': 'Vis det mest synlige øjeblik i dette trin i klippet.',
              'no': 'Vis det mest synlige øyeblikket i dette trinnet i klippet.',
              'ms': 'Tunjukkan momen paling jelas bagi langkah ini dalam klip.'
            }") }
        };
    }
}
