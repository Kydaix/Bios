# Diff firmware 2306 → 2402 (analyse des fichiers .CAP) — 23/07/2026

Méthode : UEFIExtract NE A75 (décompression des deux images, ~4 000 modules chacune)
puis IFRExtractor-RS 1.6.1 sur les 489 corps PE32 → 44 modules contenant de l'IFR
(définition des formulaires Setup) par version.

## Validation des images

| Fichier | Projet | Version ($FID) | Carte |
|---|---|---|---|
| ROG-STRIX-X870-A-...-2402.CAP | A5570 | 05.26.**24.02** | ROG STRIX X870-A GAMING WIFI |
| ROG-STRIX-X870-A-...-2306.CAP | A5570 | 05.26.**23.06** | ROG STRIX X870-A GAMING WIFI |

Le 2402.CAP correspond bien au BIOS installé (SMBIOS : 2402, build 13/07/2026).
Les 141 questions distinctes du `catalog.json` sont toutes présentes dans l'IFR du 2402
(les préfixes d'espaces des sous-menus — «  SOC GPU D3 », «  Nitro RX Data »… — sont conservés).

## Résultat du diff Setup 2306 → 2402

| Indicateur | Valeur |
|---|---|
| Questions distinctes (2306) | 2 043 |
| Questions distinctes (2402) | 2 043 |
| **Paramètres ajoutés** | **0** |
| **Paramètres supprimés** | **0** |
| **Options modifiées** (enums/valeurs) | **0** |

**Le Setup du 2402 est strictement identique à celui du 2306** : le texte IFR extrait est
identique au caractère près (seul le hash du binaire d'entrée diffère dans l'en-tête).
Sur les 44 modules IFR, seuls 4 binaires ont changé (Setup DXE `899407D7…`, `70E1A818…`,
`BBB77CB9…` ×2) — du **code**, pas des formulaires.

## Interprétation

- La MAJ 2402 (« enhanced memory performance, stability, compatibility with CXMT chips »)
  est purement **AGESA/code** : aucun nouveau réglage à cataloguer, aucun retiré.
- Le catalogue n'a donc **aucune adaptation structurelle** à faire pour le 2402 —
  cohérent avec la vérification live (0 question absente, 0 token/offset obsolète,
  cf. `VERIF_BIOS2402_2026-07-23.md`).
- Correctif d'analyse : `Above 4G Decoding` reste un booléen (01 = Enabled, conforme au
  catalogue). L'enum « 40bit (1TB) / 41bit (2TB) » appartient à la question distincte
  `Above 4GB MMIO Limit` (token 318, **commentée** dans l'export SCEWIN), qu'un parseur
  naïf peut confondre avec le bloc précédent. `ScewinParser.cs` gère ce cas correctement.
- Le reset des menus AMD CBS/OC constaté en live n'est pas dû à de nouveaux paramètres :
  c'est le comportement normal de la MAJ (réinitialisation NVRAM), à réappliquer via BiosTuner.

## Artefacts

- IFR extraits : `C:\BiosWork\ifr_2402\` et `C:\BiosWork\ifr_2306\` (44 fichiers texte/version)
- Dumps complets : `C:\BiosWork\2402.cap.dump\`, `C:\BiosWork\2306.cap.dump\`
  (supprimables une fois l'analyse archivée)
