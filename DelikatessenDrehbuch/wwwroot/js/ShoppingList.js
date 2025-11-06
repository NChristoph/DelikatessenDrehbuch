let shareUrl = localStorage.getItem("lastShareUrl")?? null;

function CreateShoppingList() {
    const resultList = [];
    var rows = document.getElementsByName("IngredientRowShoppingList").forEach(row => {
        var ing = row.querySelector(".Ingredient").value;
        var qua = row.querySelector(".Quantity").value;
        var mas = row.querySelector(".Measure").value;
        var group = row.querySelector(".Group").value;
        var name = row.querySelector(".Name").getAttribute("name");

        const combined = ing + ";" + qua + ";" + mas+";"+group;
        if (name != "Have") {
            resultList.push(combined);
        }

    });
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

    fetch(`/CreateNewMealPlan/CreateNewShoppingList`, {
        method: 'POST',
        // 2. Content-Type Header setzen, um dem Server mitzuteilen, dass es JSON ist
        headers: {
            'Content-Type': 'application/json',
            // Anti-Forgery Token nur setzen, wenn es existiert
            ...(token ? { 'RequestVerificationToken': token } : {})
        },
        // 3. Daten im Body als JSON-String senden
        body: JSON.stringify(resultList)
    }).then(response => {
        if (!response.ok) {
            throw new Error("Fehler beim Speichern der Einkaufsliste.");
        }
        return response.text(); // hier MUSS ein return sein!
    }).then(token => {
        // Token erfolgreich zurückbekommen
        shareUrl = `${window.location.origin}/ShoppingList/${token}`;
       

        // Jetzt den Share-Button anzeigen
        const btn = document.getElementById("shareBtn");
        if (btn) {
            btn.hidden = false;
        }
    }).catch(error => {
            console.error(error);
            alert("Fehler beim Erstellen der Einkaufsliste!");
    });
        
    localStorage.setItem("lastShareUrl", shareUrl);
    localStorage.setItem("lastShopping", true);

}

document.getElementById("shareBtn").addEventListener("click", async () => {
    const shareData = {
        title: "Meine Einkaufsliste",
        text: "Hier ist der Link zu deiner Einkaufsliste:",
        url: shareUrl
    };

    if (navigator.share) {
        try {
            await navigator.share(shareData);
           
        } catch (err) {
            console.error("Teilen abgebrochen oder fehlgeschlagen:", err);
        }
    } else {
        alert("Teilen wird auf diesem Gerät oder Browser nicht unterstützt.");
    }
});