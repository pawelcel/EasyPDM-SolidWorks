// The add-in's texts in English, Polish and German, chosen by the Windows display language as
// the macros choose theirs (GetUserDefaultUILanguage there, CurrentUICulture here). The English
// text is the key; a text missing from a table is shown in English.

using System.Collections.Generic;
using System.Globalization;

namespace EasyPDM.SolidWorksAddin
{
    internal static class Texts
    {
        private static readonly Dictionary<string, string> Pl = new Dictionary<string, string>
        {
            ["Lock"] = "Blokada",
            ["Log in/out"] = "Logowanie",
            ["Panel"] = "Panel",
            ["Send the active document to EasyPDM"] = "Wyślij aktywny dokument do EasyPDM",
            ["Download an item from EasyPDM with its components"] = "Pobierz element z EasyPDM razem z komponentami",
            ["Lock the active document's item for editing, or release your lock"] = "Zablokuj element aktywnego dokumentu do edycji albo zwolnij swoją blokadę",
            ["Log in to EasyPDM (approved in the browser), or log out"] = "Zaloguj się do EasyPDM (zatwierdzając w przeglądarce) albo wyloguj",
            ["Show the EasyPDM document panel"] = "Pokaż panel dokumentu EasyPDM",
            ["EasyPDM document panel"] = "Panel dokumentu EasyPDM",
            ["Record"] = "Rekord",
            ["Name"] = "Nazwa",
            ["Revision"] = "Rewizja",
            ["Status"] = "Status",
            ["Locked by"] = "Zablokowany przez",
            ["nobody"] = "nikt",
            ["Refresh"] = "Odśwież",
            ["Open in browser"] = "Otwórz w przeglądarce",
            ["Lock for editing"] = "Zablokuj do edycji",
            ["Release lock"] = "Zwolnij blokadę",
            ["Log in"] = "Zaloguj",
            ["Log out"] = "Wyloguj",
            ["Signed in as {0} on {1}"] = "Zalogowany jako {0} na {1}",
            ["Signed in on {0}"] = "Zalogowany na {0}",
            ["Not signed in"] = "Niezalogowany",
            ["No document open."] = "Brak otwartego dokumentu.",
            ["This document is not in EasyPDM yet (upload it first)."] = "Tego dokumentu nie ma jeszcze w EasyPDM (najpierw go wyślij).",
            ["Released or cancelled items cannot be changed."] = "Wydanych ani anulowanych elementów nie można zmieniać.",
            ["Locked by {0}"] = "Zablokowany przez {0}",
            ["Locked for you: {0}"] = "Zablokowany dla Ciebie: {0}",
            ["Lock released: {0}"] = "Blokada zwolniona: {0}",
            ["w_pracy"] = "W pracy",
            ["sprawdzany"] = "Sprawdzany",
            ["wydany"] = "Wydany",
            ["anulowana"] = "Anulowany",
            ["✓ newest"] = "✓ najnowszy",
            ["⚠ newer file in EasyPDM"] = "⚠ nowszy plik w EasyPDM",
            ["• changed here, not uploaded"] = "• zmieniony tutaj, niewysłany",
            ["– not in EasyPDM yet"] = "– jeszcze nie w EasyPDM",
            ["– no file in EasyPDM"] = "– brak pliku w EasyPDM",
            ["– not loaded (lightweight)"] = "– niewczytany (odciążony)",
            ["here {0}"] = "tutaj {0}",
            ["EasyPDM {0}"] = "EasyPDM {0}",
            ["In EasyPDM: {0}, uploaded by {1} on {2}"] = "W EasyPDM: {0}, wysłał {1} {2}",
            ["Older than in EasyPDM: {0}. Download the newest before uploading."] = "Starsze niż w EasyPDM: {0}. Przed wysłaniem pobierz najnowsze.",
            ["All components are the newest files in EasyPDM."] = "Wszystkie komponenty to najnowsze pliki z EasyPDM.",
            ["Could not check the components: {0}"] = "Nie udało się sprawdzić komponentów: {0}",
            ["Upload refused: this work is built on files older than those in EasyPDM. Download the newest first (Download), then upload again:\n\n{0}"] =
                "Wysyłanie wstrzymane: ta praca opiera się na plikach starszych niż w EasyPDM. Najpierw pobierz najnowsze (Download), potem wyślij ponownie:\n\n{0}",
            ["{0}: here {1}, in EasyPDM {2} (uploaded by {3} on {4})"] = "{0}: tutaj {1}, w EasyPDM {2} (wysłał {3} {4})",
            ["{0} is missing next to the add-in ({1}). Put the compiled EasyPDM macros there."] =
                "Brak {0} obok dodatku ({1}). Umieść tam skompilowane makra EasyPDM.",
            ["The macro {0} ({1}) did not run, error {2}."] = "Makro {0} ({1}) nie uruchomiło się, błąd {2}.",
            ["The macro {0} has no procedure {1}. It has: {2}"] = "Makro {0} nie ma procedury {1}. Ma: {2}",
            ["Could not create the EasyPDM menu: {0}"] = "Nie udało się utworzyć menu EasyPDM: {0}",
        };

        private static readonly Dictionary<string, string> De = new Dictionary<string, string>
        {
            ["Lock"] = "Sperre",
            ["Log in/out"] = "Anmeldung",
            ["Panel"] = "Bereich",
            ["Send the active document to EasyPDM"] = "Das aktive Dokument an EasyPDM senden",
            ["Download an item from EasyPDM with its components"] = "Ein Element mit seinen Komponenten aus EasyPDM herunterladen",
            ["Lock the active document's item for editing, or release your lock"] = "Das Element des aktiven Dokuments zur Bearbeitung sperren oder die eigene Sperre aufheben",
            ["Log in to EasyPDM (approved in the browser), or log out"] = "Bei EasyPDM anmelden (im Browser bestätigt) oder abmelden",
            ["Show the EasyPDM document panel"] = "Den EasyPDM-Dokumentbereich anzeigen",
            ["EasyPDM document panel"] = "EasyPDM-Dokumentbereich",
            ["Record"] = "Datensatz",
            ["Name"] = "Name",
            ["Revision"] = "Revision",
            ["Status"] = "Status",
            ["Locked by"] = "Gesperrt von",
            ["nobody"] = "niemand",
            ["Refresh"] = "Aktualisieren",
            ["Open in browser"] = "Im Browser öffnen",
            ["Lock for editing"] = "Zur Bearbeitung sperren",
            ["Release lock"] = "Sperre aufheben",
            ["Log in"] = "Anmelden",
            ["Log out"] = "Abmelden",
            ["Signed in as {0} on {1}"] = "Angemeldet als {0} bei {1}",
            ["Signed in on {0}"] = "Angemeldet bei {0}",
            ["Not signed in"] = "Nicht angemeldet",
            ["No document open."] = "Kein Dokument geöffnet.",
            ["This document is not in EasyPDM yet (upload it first)."] = "Dieses Dokument ist noch nicht in EasyPDM (zuerst hochladen).",
            ["Released or cancelled items cannot be changed."] = "Freigegebene oder stornierte Elemente können nicht geändert werden.",
            ["Locked by {0}"] = "Gesperrt von {0}",
            ["Locked for you: {0}"] = "Für Sie gesperrt: {0}",
            ["Lock released: {0}"] = "Sperre aufgehoben: {0}",
            ["w_pracy"] = "In Arbeit",
            ["sprawdzany"] = "In Prüfung",
            ["wydany"] = "Freigegeben",
            ["anulowana"] = "Storniert",
            ["✓ newest"] = "✓ neueste",
            ["⚠ newer file in EasyPDM"] = "⚠ neuere Datei in EasyPDM",
            ["• changed here, not uploaded"] = "• hier geändert, nicht hochgeladen",
            ["– not in EasyPDM yet"] = "– noch nicht in EasyPDM",
            ["– no file in EasyPDM"] = "– keine Datei in EasyPDM",
            ["– not loaded (lightweight)"] = "– nicht geladen (reduziert)",
            ["here {0}"] = "hier {0}",
            ["EasyPDM {0}"] = "EasyPDM {0}",
            ["In EasyPDM: {0}, uploaded by {1} on {2}"] = "In EasyPDM: {0}, hochgeladen von {1} am {2}",
            ["Older than in EasyPDM: {0}. Download the newest before uploading."] = "Älter als in EasyPDM: {0}. Vor dem Hochladen die neuesten herunterladen.",
            ["All components are the newest files in EasyPDM."] = "Alle Komponenten sind die neuesten Dateien aus EasyPDM.",
            ["Could not check the components: {0}"] = "Komponenten konnten nicht geprüft werden: {0}",
            ["Upload refused: this work is built on files older than those in EasyPDM. Download the newest first (Download), then upload again:\n\n{0}"] =
                "Hochladen angehalten: Diese Arbeit beruht auf älteren Dateien als in EasyPDM. Zuerst die neuesten herunterladen (Download), dann erneut hochladen:\n\n{0}",
            ["{0}: here {1}, in EasyPDM {2} (uploaded by {3} on {4})"] = "{0}: hier {1}, in EasyPDM {2} (hochgeladen von {3} am {4})",
            ["{0} is missing next to the add-in ({1}). Put the compiled EasyPDM macros there."] =
                "{0} fehlt neben dem Add-in ({1}). Legen Sie die kompilierten EasyPDM-Makros dorthin.",
            ["The macro {0} ({1}) did not run, error {2}."] = "Das Makro {0} ({1}) wurde nicht ausgeführt, Fehler {2}.",
            ["The macro {0} has no procedure {1}. It has: {2}"] = "Das Makro {0} hat keine Prozedur {1}. Es hat: {2}",
            ["Could not create the EasyPDM menu: {0}"] = "Das EasyPDM-Menü konnte nicht erstellt werden: {0}",
        };

        private static readonly Dictionary<string, string> Table =
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pl" ? Pl
            : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de" ? De
            : null;

        public static string T(string english, params object[] values)
        {
            var text = Table != null && Table.TryGetValue(english, out var translated) ? translated : english;
            return values.Length == 0 ? text : string.Format(CultureInfo.CurrentCulture, text, values);
        }

        private static readonly Dictionary<string, string> StatusNames = new Dictionary<string, string>
        {
            ["w_pracy"] = "In progress", ["sprawdzany"] = "Under review", ["wydany"] = "Released", ["anulowana"] = "Cancelled",
        };

        /// An item status code from the server ("w_pracy", ...) in words.
        public static string Status(string code)
        {
            if (string.IsNullOrEmpty(code)) return "";
            if (Table != null && Table.TryGetValue(code, out var translated)) return translated;
            return StatusNames.TryGetValue(code, out var english) ? english : code;
        }

        /// The texts each language table must have: every key of the other table.
        internal static IEnumerable<string> Missing()
        {
            foreach (var key in Pl.Keys) if (!De.ContainsKey(key)) yield return "de: " + key;
            foreach (var key in De.Keys) if (!Pl.ContainsKey(key)) yield return "pl: " + key;
        }
    }
}
