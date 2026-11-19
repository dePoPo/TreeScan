# Werken aan TreeScan

TreeScan heeft twee Git-repositories. In de interne werkmap blijft GitLab de remote `origin`. De aparte werkmap `../TreeScan-public` gebruikt de remote `github` voor `dePoPo/TreeScan`. Beide gebruiken de branch `master`.

## Commits en pushes

- Leg bij een commit dezelfde publieke wijzigingen vast in beide werkmappen. Maak in de publieke werkmap een eigen commit op de bestaande publieke geschiedenis.
- Push bij een pushopdracht beide werkmappen. Gebruik intern `git push origin master` en vanuit de publieke werkmap `git push github master`. Verifieer dat beide remotes de bedoelde commits hebben ontvangen.
- Controleer vóór iedere publieke commit welke bestanden en metadata openbaar worden. Houd interne URLs, referenties, geheimen en lokale buildbestanden buiten de publieke kopie. Vraag bij twijfel of een wijziging openbaar mag worden.
- Houd de twee geschiedenissen gescheiden. Voeg de publieke remote alleen toe aan de publieke werkmap. Gebruik geen force-push of mirror-push om verschillen op te lossen.
- Laat de gebruiker het weten als een commit of push naar een van beide repositories is geblokkeerd, bijvoorbeeld door een aanmelding die nodig is.
