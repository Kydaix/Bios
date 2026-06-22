# BIOS Tuner

Application Windows (`.exe` autonome) pour appliquer des profils BIOS optimisés
sur une carte **ASUS ROG STRIX X870-A GAMING WIFI** équipée d'un **Ryzen 7 9800X3D**,
via l'utilitaire AMI **SCEWIN**.

L'app permet de :

- **Importer** l'état réel du BIOS (export live SCEWIN, jamais un fichier obsolète) ;
- parcourir des **catégories** de réglages (Système/Latence, CPU/PBO, Mémoire DDR5, PCIe)
  avec, pour chaque option, une **description explicative** et un **niveau de risque** ;
- voir précisément **quels paramètres seront modifiés** (valeur actuelle → cible) avant d'écrire ;
- **appliquer** la sélection (sauvegarde automatique + vérification post-écriture) ;
- **restaurer** l'état précédent en un clic.

Interface **Fluent / Windows 11** (WPF + [WPF-UI](https://github.com/lepoco/wpfui)), thème sombre, Mica.

> ⚠️ Modifier le BIOS comporte des risques (instabilité, no-boot). L'app crée une sauvegarde
> avant chaque écriture, mais un **clear CMOS** reste le filet de sécurité ultime.
> Un **redémarrage** est nécessaire pour que les changements prennent effet.

---

## Comment ça marche

1. L'app embarque `SCEWIN_64.exe` + ses drivers (`amifldrv64.sys`, `amigendrv64.sys`).
   Au premier lancement ils sont extraits dans `%LOCALAPPDATA%\BiosTuner\scewin`.
2. **Importer** → `SCEWIN_64.exe /O /S` exporte toutes les variables Setup dans un fichier texte,
   que l'app parse en blocs `Setup Question`.
3. Le **catalogue** ([`catalog/catalog.json`](catalog/catalog.json)) décrit les réglages.
   Chaque *tweak* cible un ou plusieurs paramètres par `question` + `(token, offset)`.
   > Les tokens **ne sont pas uniques** (mêmes numéros réutilisés dans plusieurs menus :
   > AMD CBS, AMD Overclocking, ASUS Ai Tweaker). Le couple `(token, offset)` désambiguïse.
4. **Aperçu** calcule le diff sans rien écrire.
5. **Appliquer** : ré-export frais → réécriture des lignes → `SCEWIN_64.exe /I /S` (import)
   → ré-export → vérification ligne par ligne. Tous les artefacts (before/target/after/plan.csv)
   sont conservés dans `%LOCALAPPDATA%\BiosTuner\runs\<horodatage>_<action>`.

### Principe clé : PBO uniquement via AMD Overclocking

Les réglages PBO / Curve Optimizer sont pilotés **uniquement** dans le menu **AMD Overclocking**.
Les copies du menu **ASUS Ai/Extreme Tweaker** sont remises sur **Auto** (tweak
« Neutraliser les copies ASUS ») pour éviter tout état ambigu.

---

## Catégories du catalogue

| Catégorie | Contenu | Risque |
|---|---|---|
| **Système & Latence** | ReBAR, C-states, virtualisation, TSME, iGPU, ASPM, Spread Spectrum, firmware sécurité | Sûr → Moyen |
| **CPU – PBO (AMD OC)** | PBO Advanced + Scalar 3X, Boost +200 MHz, Curve Optimizer −30, neutralisation ASUS | Sûr → Élevé |
| **Mémoire DDR5** | Latence (UCLK/Gear Down…), FCLK 2067→2167 (exclusifs), tREFI, tWR, Nitro | Expérimental |
| **PCIe** | PSPP, Extended Tag, Gen5 link mode… | Expérimental |

Les paliers **FCLK** et **tREFI** sont mutuellement exclusifs (radio) : sélectionner l'un désélectionne l'autre.

Le catalogue peut être édité sans recompiler : placez un `catalog.json` **à côté de l'exe**,
il prime sur la version embarquée.

---

## Construire

### En local

```powershell
dotnet publish src/Bios.App/Bios.App.csproj -c Release -r win-x64 -o publish
```

Produit un unique `publish/BiosTuner.exe` (self-contained, aucune install .NET requise).

### Via GitHub Actions (release automatique)

Le workflow [`.github/workflows/build.yml`](.github/workflows/build.yml) est entièrement automatisé :
**chaque push sur `main`** déclenche une nouvelle version, sans action manuelle.

À chaque push sur `main`, le workflow :

1. **versionne automatiquement** : `v1.0.<numéro de run>` (monotone, injecté aussi dans l'exe) ;
2. construit le `BiosTuner.exe` single-file sur `windows-latest` ;
3. crée le **tag** et la **Release GitHub** avec l'exe attaché (notes générées auto) ;
4. **nettoie** : supprime les anciennes releases + tags (ne garde que la dernière),
   tous les caches Actions, tous les artifacts, et les runs au-delà des 10 plus récents.

Aucun `git tag` manuel n'est nécessaire — il suffit de pousser sur `main`.

---

## Lancer

Double-cliquer `BiosTuner.exe`. L'app demande l'**élévation administrateur**
(obligatoire : SCEWIN charge des drivers noyau et écrit des variables UEFI).

Workflow recommandé :

1. **Importer le BIOS actuel**
2. cocher les réglages voulus (commencer par **Recommandé**)
3. **Aperçu** pour vérifier le diff
4. **Appliquer**
5. **Redémarrer**, puis valider la stabilité (Observateur d'événements → aucun WHEA-Logger,
   y-cruncher / OCCT / TM5).

---

## Cible matérielle

`ASUS ROG STRIX X870-A GAMING WIFI / Ryzen 7 9800X3D / RTX 5070 / 4× DDR5-6200 (DOCP I, CL30, 1.35 V)`

Le catalogue (tokens/offsets) est spécifique à **ce BIOS**. Sur un autre firmware,
ré-exportez et ré-adaptez `catalog.json`.

---

## Licence

Le code de cette application est sous licence MIT (voir [LICENSE](LICENSE)).

⚠️ **SCEWIN_64.exe et les drivers `ami*.sys` sont des outils propriétaires AMI**,
inclus ici uniquement pour un usage personnel sur la machine cible. Ils ne sont **pas**
couverts par la licence MIT. Si vous publiez ce dépôt, vérifiez vos droits de redistribution
de ces binaires (ou retirez-les de `tools/scewin/` et fournissez-les séparément).
