using System.Collections.Generic;
using Godot;

namespace Poopsweeper;

/// <summary>
/// Übersetzungen Deutsch/Englisch. Bewusst im Code statt in Godot-CSV, damit kein Import-Schritt nötig ist
/// und fehlende Schlüssel sofort auffallen (sie werden als "!key" angezeigt).
/// </summary>
public static class Loc
{
    public static string Language { get; private set; } = "de";

    /// <summary>setting: "auto", "de" oder "en".</summary>
    public static void Init(string setting)
    {
        if (setting is "de" or "en") Language = setting;
        else Language = OS.GetLocale().StartsWith("de") ? "de" : "en";
    }

    public static string T(string key)
    {
        if (!Table.TryGetValue(key, out var v)) return "!" + key;
        return Language == "de" ? v.de : v.en;
    }

    public static string T(string key, params object[] args) => string.Format(T(key), args);

    public static bool Has(string key) => Table.ContainsKey(key);

    private static readonly Dictionary<string, (string de, string en)> Table = new()
    {
        ["common.back"] = ("Zurück", "Back"),

        ["home.sub"] = ("Räume die Hundewiese – aber tritt nicht rein!", "Clear the dog park – and don't step in it!"),
        ["home.campaign"] = ("Kampagne", "Campaign"),
        ["home.quick"] = ("Schnelles Spiel", "Quick Game"),
        ["home.compendium"] = ("Kompendium", "Compendium"),
        ["home.settings"] = ("Einstellungen", "Settings"),
        ["home.quit"] = ("Beenden", "Quit"),

        ["campaign.title"] = ("Kampagne", "Campaign"),
        ["campaign.sub"] = ("Räume die Hundewiese – Sterne: {0}", "Clear the dog park – stars: {0}"),

        ["quick.title"] = ("Schnelles Spiel", "Quick Game"),
        ["quick.easy"] = ("Einfach", "Easy"),
        ["quick.medium"] = ("Mittel", "Medium"),
        ["quick.hard"] = ("Schwer", "Hard"),
        ["quick.custom"] = ("Individuell", "Custom"),
        ["quick.width"] = ("Breite", "Width"),
        ["quick.height"] = ("Höhe", "Height"),
        ["quick.density"] = ("Haufen-Dichte", "Poop density"),
        ["quick.obstacles"] = ("Hindernisse", "Obstacles"),
        ["quick.roughness"] = ("Unruhiger Waldrand", "Ragged forest edge"),
        ["quick.dog"] = ("Schnüffelhund-Einsätze", "Sniffer dog uses"),
        ["quick.puddles"] = ("Pfützen", "Puddles"),
        ["quick.doubles"] = ("Doppelhaufen", "Double piles"),
        ["quick.sprinklers"] = ("Sprinkler", "Sprinklers"),
        ["quick.lives"] = ("Leben", "Lives"),
        ["quick.noguess"] = ("Ohne Raten lösbar", "Solvable without guessing"),
        ["quick.summary"] = ("Ca. {0} Wiesenfelder, {1} Haufen", "About {0} meadow tiles, {1} piles"),
        ["quick.start"] = ("Spiel starten", "Start game"),

        ["game.dig"] = ("Graben", "Dig"),
        ["game.flag"] = ("Tüte", "Bag"),
        ["game.dog"] = ("Hund ({0})", "Dog ({0})"),
        ["game.hint"] = ("Tipp", "Hint"),
        ["game.next"] = ("Weiter", "Next"),
        ["game.restart"] = ("Neu", "New"),
        ["game.menu"] = ("Menü", "Menu"),
        ["game.go"] = ("Los geht's", "Let's go"),
        ["game.nohint"] = ("Gerade lässt sich nichts sicher ableiten.", "Nothing can be deduced for sure right now."),
        ["hud.level"] = ("Level {0}", "Level {0}"),
        ["hud.boss"] = ("Level {0} (Boss)", "Level {0} (Boss)"),
        ["hud.quick"] = ("Schnelles Spiel", "Quick Game"),
        ["hud.par"] = ("Par {0}", "Par {0}"),

        ["result.won"] = ("Wiese geräumt!", "Meadow cleared!"),
        ["result.lost"] = ("Reingetreten!", "Stepped in it!"),
        ["result.wonbody"] = ("Zeit {0} (Par {1})", "Time {0} (par {1})"),
        ["result.lostbody"] = ("Alle Leben verbraucht. Der Hund guckt dich vorwurfsvoll an.", "No lives left. The dog gives you a reproachful look."),
        ["result.next"] = ("Nächstes Level", "Next level"),
        ["result.again"] = ("Nochmal", "Again"),
        ["result.newgame"] = ("Neues Spiel", "New game"),
        ["result.view"] = ("Feld ansehen", "View field"),

        ["intro.1"] = (
            "Willkommen auf der Hundewiese!\n\nKlick = graben. Rechtsklick (am Handy: lange drücken) = Tüte auf einen vermuteten Haufen.\n" +
            "Die Zahl sagt, wie viele Haufen in den 8 Nachbarfeldern liegen. Klick auf eine Zahl mit genug Tüten räumt die Nachbarn.\n" +
            "Tritt nicht rein – du hast 3 Leben!\nMausrad = Zoom, Ziehen = Verschieben.",
            "Welcome to the dog park!\n\nClick = dig. Right-click (long press on a phone) = put a bag on a suspected pile.\n" +
            "A number tells you how many piles are in the 8 neighbouring tiles. Click a number with enough bags around it to clear its neighbours.\n" +
            "Don't step in it – you have 3 lives!\nMouse wheel = zoom, drag = move the field."),
        ["intro.4"] = (
            "Hindernisse!\n\nBäume, Bänke, Büsche und Steine sind gesperrt. Sie zählen nie als Haufen, und der Waldrand ist nicht mehr gerade.",
            "Obstacles!\n\nTrees, benches, bushes and rocks are blocked. They never count as piles, and the forest edge is no longer straight."),
        ["intro.6"] = (
            "Der Schnüffelhund hilft dir!\n\nWähle \"Hund\" und tippe ein verdecktes Feld: Liegt dort ein Haufen, kommt eine Tüte drauf – sonst wird das Feld aufgedeckt. " +
            "\"Tipp\" zeigt dir ein Feld, das garantiert sicher ist. Nur wenige Einsätze pro Level.",
            "The sniffer dog helps you!\n\nPick \"Dog\" and tap a hidden tile: if there is a pile, it gets a bag – otherwise the tile is uncovered. " +
            "\"Hint\" shows you a tile that is guaranteed to be safe. Only a few uses per level."),
        ["intro.8"] = (
            "Pfützen!\n\nAuf Pfützen steht eine Spanne wie \"1-2\": Die echte Zahl ist einer der beiden Werte.",
            "Puddles!\n\nPuddles show a range like \"1-2\": the real number is one of the two values."),
        ["intro.12"] = (
            "Doppelhaufen!\n\nEin Doppelhaufen zählt 2 in den Nachbarzahlen. Tüte mehrfach tippen setzt zwei Tüten.",
            "Double piles!\n\nA double pile counts as 2 in neighbouring numbers. Tapping the bag repeatedly places two bags."),
        ["intro.15"] = (
            "Sprinkler!\n\nEin aufgedeckter Sprinkler zeigt nur \"?\" – bis der Hund daran schnüffelt.",
            "Sprinklers!\n\nAn uncovered sprinkler only shows \"?\" – until the dog sniffs at it."),

        ["comp.title"] = ("Kompendium", "Compendium"),
        ["comp.goal.t"] = ("Ziel", "Goal"),
        ["comp.goal.b"] = ("Räume die Wiese: Decke alle Felder ohne Haufen auf. Haufen musst du nicht anfassen – markiere sie mit Tüten.",
            "Clear the meadow: uncover every tile without a pile. You never have to touch the piles – mark them with bags."),
        ["comp.numbers.t"] = ("Zahlen", "Numbers"),
        ["comp.numbers.b"] = ("Eine aufgedeckte Zahl verrät, wie viele Haufen in den bis zu 8 Nachbarfeldern liegen. Felder ohne Haufen in der Nähe öffnen sich von selbst. Hindernisse und Wald zählen nie mit.",
            "An uncovered number tells you how many piles are in the up to 8 neighbouring tiles. Tiles with no piles nearby open by themselves. Obstacles and forest never count."),
        ["comp.controls.t"] = ("Steuerung", "Controls"),
        ["comp.controls.b"] = (
            "Maus: Linksklick gräbt, Rechtsklick markiert, Mausrad zoomt, Ziehen verschiebt das Feld.\nTouch: Tippen gräbt, lang drücken markiert, zwei Finger zoomen.\nKlick auf eine Zahl, um die der passende Satz Tüten liegt, räumt alle Nachbarn auf einmal.\nTasten: R = neu, Esc = zurück, 1/2/3 = Werkzeug.",
            "Mouse: left-click digs, right-click marks, wheel zooms, dragging moves the field.\nTouch: tap digs, long press marks, two fingers zoom.\nClick a number that has the matching number of bags around it to clear all its neighbours at once.\nKeys: R = new, Esc = back, 1/2/3 = tool."),
        ["comp.bag.t"] = ("Tüte", "Bag"),
        ["comp.bag.b"] = ("Markiert ein Feld, auf dem du einen Haufen vermutest. Auf einem Feld mit Tüte kannst du nicht versehentlich graben. Der Zähler oben zeigt, wie viele Tüten noch gebraucht werden.",
            "Marks a tile where you suspect a pile. You can't dig on a tile with a bag by accident. The counter at the top shows how many bags are still needed."),
        ["comp.question.t"] = ("Fragezeichen", "Question mark"),
        ["comp.question.b"] = ("Dritter Rechtsklick-Schritt: leer → Tüte → Fragezeichen → leer. Das Fragezeichen merkt sich nur „bin unsicher“. Es zählt nicht als Tüte, und du kannst das Feld trotzdem aufdecken.",
            "Third right-click step: empty → bag → question mark → empty. It only remembers \"not sure\". It doesn't count as a bag, and you can still uncover the tile."),
        ["comp.lives.t"] = ("Leben", "Lives"),
        ["comp.lives.b"] = ("Wer in einen Haufen tritt, verliert ein Leben, der Haufen wird sichtbar und das Spiel geht weiter. Sind alle Leben weg, ist das Level verloren.",
            "Stepping in a pile costs a life, the pile is revealed and the game goes on. When all lives are gone, the level is lost."),
        ["comp.fair.t"] = ("Fair gespielt", "Fair play"),
        ["comp.fair.b"] = ("Die ersten 12 Level lassen sich komplett mit Logik lösen, ohne ein einziges Mal raten zu müssen. Danach darf es knifflig werden – mit dem Tipp des Hundes als Rettungsanker.",
            "The first 12 levels can be solved entirely with logic, without ever having to guess. After that it may get tricky – with the dog's hint as a lifeline."),
        ["comp.obstacles.t"] = ("Hindernisse & Waldrand", "Obstacles & forest edge"),
        ["comp.obstacles.tag"] = ("ab Level 4", "from level 4"),
        ["comp.obstacles.b"] = ("Bäume (2x2), Bänke, Büsche und Steine sind gesperrt. Auch der Waldrand ist nicht gerade, er wird mit höheren Leveln unruhiger. Gesperrte Felder haben nie Haufen und zählen in keiner Zahl.",
            "Trees (2x2), benches, bushes and rocks are blocked. The forest edge isn't straight either, and gets more ragged in higher levels. Blocked tiles never hold piles and don't count in any number."),
        ["comp.dog.t"] = ("Schnüffelhund", "Sniffer dog"),
        ["comp.dog.tag"] = ("ab Level 6", "from level 6"),
        ["comp.dog.b"] = ("Wähle „Hund“ und tippe ein verdecktes Feld: Liegt dort ein Haufen, bekommt es eine Tüte, sonst wird es aufgedeckt. Pro Level gibt es nur wenige Einsätze (Zahl neben der Pfote). Er löst auch Sprinkler auf.",
            "Pick \"Dog\" and tap a hidden tile: if there is a pile it gets a bag, otherwise the tile is uncovered. Only a few uses per level (number next to the paw). It also resolves sprinklers."),
        ["comp.hint.t"] = ("Tipp", "Hint"),
        ["comp.hint.tag"] = ("ab Level 6", "from level 6"),
        ["comp.hint.b"] = ("Der Tipp-Knopf kostet einen Hunde-Einsatz und zeigt dir ein Feld, das per Logik garantiert sicher ist (grün) – oder einen sicheren Haufen (rot). Gibt es nichts Sicheres, kostet er nichts.",
            "The hint button costs one dog use and shows you a tile that is guaranteed safe by logic (green) – or a guaranteed pile (red). If nothing is certain, it costs nothing."),
        ["comp.puddle.t"] = ("Pfützen", "Puddles"),
        ["comp.puddle.tag"] = ("ab Level 8", "from level 8"),
        ["comp.puddle.b"] = ("Auf einer Pfütze steht eine Spanne wie „1-2“: Die echte Zahl ist einer der beiden Werte.",
            "A puddle shows a range like \"1-2\": the real number is one of the two values."),
        ["comp.double.t"] = ("Doppelhaufen", "Double piles"),
        ["comp.double.tag"] = ("ab Level 12", "from level 12"),
        ["comp.double.b"] = ("Ein großer Haufen zählt zweifach in den Nachbarzahlen und braucht zwei Tüten. Tippe die Tüte mehrfach, um zwei zu setzen.",
            "A big pile counts twice in neighbouring numbers and needs two bags. Tap the bag repeatedly to place two."),
        ["comp.sprinkler.t"] = ("Sprinkler", "Sprinklers"),
        ["comp.sprinkler.tag"] = ("ab Level 15", "from level 15"),
        ["comp.sprinkler.b"] = ("Ein aufgedeckter Sprinkler zeigt nur „?“ statt einer Zahl. Erst wenn der Hund daran schnüffelt, siehst du den Wert.",
            "An uncovered sprinkler only shows \"?\" instead of a number. Only when the dog sniffs at it do you see the value."),
        ["comp.stars.t"] = ("Sterne & Par-Zeit", "Stars & par time"),
        ["comp.stars.b"] = ("★ fürs Räumen, ★★ wenn du unter der Par-Zeit bleibst, ★★★ wenn du dabei kein Leben verlierst. In der Kampagne schaltet jedes geräumte Level das nächste frei; jedes 5. Level ist ein großes Bosslevel.",
            "★ for clearing, ★★ if you stay under par time, ★★★ if you also lose no life. In the campaign every cleared level unlocks the next; every 5th level is a big boss level."),

        ["tut.title"] = ("Tutorial", "Tutorial"),
        ["tut.next"] = ("Weiter", "Next"),
        ["tut.skip"] = ("Überspringen", "Skip"),
        ["tut.s1"] = ("Tippe auf das leuchtende Feld, um zu graben. Ein Feld ohne Haufen in der Nähe öffnet auch seine Nachbarn.",
            "Tap the glowing tile to dig. A tile with no piles around it opens its neighbours too."),
        ["tut.s2"] = ("Zahlen verraten, wie viele Haufen in den 8 Nachbarfeldern liegen. Schau auf die 1-2-1 rechts: Jede 1 hat zwei Kandidaten, die 2 braucht aber zwei Haufen – also muss das leuchtende Feld in der Mitte sicher sein!",
            "Numbers tell you how many piles are in the 8 neighbouring tiles. Look at the 1-2-1 on the right: each 1 has two candidates, but the 2 needs two piles – so the glowing tile in the middle must be safe!"),
        ["tut.s3"] = ("Grabe das leuchtende Feld – es ist sicher.", "Dig the glowing tile – it is safe."),
        ["tut.s4"] = ("Damit müssen die beiden äußeren Felder Haufen sein. Wähle das Tüten-Werkzeug (oder Rechtsklick / lange drücken) und markiere beide.",
            "That makes the two outer tiles piles. Pick the Bag tool (or right-click / long press) and mark both."),
        ["tut.s5"] = ("Die 2 hat jetzt genau zwei Tüten um sich. Wechsle zurück zu „Graben“ und tippe die 2: Alle anderen Nachbarn öffnen sich auf einmal.",
            "The 2 now has exactly two bags around it. Switch back to Dig and tap the 2: all its other neighbours open at once."),
        ["tut.s6"] = ("Fast geschafft! Grabe das leuchtende Feld, um den Rest zu räumen.", "Almost there! Dig the glowing tile to clear the rest."),
        ["tut.done.title"] = ("Das war's!", "That's it!"),
        ["tut.done.body"] = ("Mehr brauchst du nicht. Leben, Schnüffelhund und alles Weitere erklärt das Kompendium. Viel Spaß auf der Wiese!",
            "That's all you need. Lives, the sniffer dog and everything else are explained in the Compendium. Have fun on the meadow!"),
        ["tut.done.go"] = ("Zur Kampagne", "To the campaign"),

        ["set.title"] = ("Einstellungen", "Settings"),
        ["set.tutorial"] = ("Tutorial wiederholen", "Replay tutorial"),
        ["set.language"] = ("Sprache", "Language"),
        ["set.lang.auto"] = ("Automatisch", "Automatic"),
        ["set.sfx"] = ("Effekt-Lautstärke", "Sound effects volume"),
        ["set.vibration"] = ("Vibration", "Vibration"),
        ["set.colorblind"] = ("Farbenblind-Modus", "Colour-blind mode"),
        ["set.uiscale"] = ("Oberflächengröße", "Interface size"),
        ["set.reset"] = ("Fortschritt zurücksetzen", "Reset progress"),
        ["set.reset.confirm"] = ("Wirklich alle Level-Fortschritte löschen?", "Really delete all level progress?"),
        ["set.reset.yes"] = ("Ja, zurücksetzen", "Yes, reset"),
        ["set.reset.no"] = ("Abbrechen", "Cancel"),
        ["set.about"] = ("Open Source (MIT) – keine Werbung, kein Tracking.", "Open source (MIT) – no ads, no tracking."),
        ["set.feedback"] = ("Feedback & Quellcode (GitHub)", "Feedback & source code (GitHub)"),
    };
}
