# CLOCK_WATCHDOG_TIMEOUT (0x101) sous charge de build — 26/07/2026

Freeze de ~5 s puis BSOD `CLOCK_WATCHDOG_TIMEOUT` pendant les builds JS/TS (TanStack :
Vite / esbuild / tsc / Node en parallèle). Analyse de l'état BIOS live confronté au catalogue.

## Ce que signifie le code

`0x101` = un processeur logique **n'a pas répondu à l'interruption d'horloge** (IPI) dans le
délai imparti. Ce n'est pas une erreur mémoire (`0x124` / WHEA) ni une erreur logicielle : un
cœur a cessé de progresser. Sur un Ryzen X3D, les causes matérielles sont, dans l'ordre :
undervolt trop agressif, tension de boost non tenue lors d'un transitoire de courant,
et gestion d'états d'alimentation forcée hors des chemins validés par l'AGESA.

## Pourquoi précisément pendant un build

Un build TanStack est le **pire régime possible** pour un CO négatif : des centaines de tâches
très courtes, massivement parallèles, entrecoupées de micro-pauses I/O. Le CPU passe son temps
à osciller entre repos et boost mono/faible-charge — c'est exactement là que le Curve Optimizer
applique le plus gros décalage de tension, et là que la marge est la plus faible. Un jeu, qui
maintient une charge continue, ne sollicite jamais ce point de fonctionnement.

## État BIOS constaté (export SCEWIN du 23/07, run `20260723_201709_apply`)

| Réglage | Valeur lue | Effet |
|---|---|---|
| All Core Curve Optimizer | **−30** (All Cores / Negative / 30) | Undervolt maximal du catalogue |
| Max CPU Boost Clock Override | **+200 MHz** | Fréquence au-delà du bin validé |
| PBO Scalar | **10X** (Manual) | Tensions de boost tenues bien plus longtemps |
| Prochot VRM Throttling | **Disable** | Le VRM ne throttle plus, même en surchauffe |
| Peak Current Control | **Disable** | Aucune limitation des pics de courant |
| CPU / VDDSOC Power Phase + Duty | **Extreme** | Plus d'équilibrage thermique entre phases |
| Core Watchdog Timer Enable | **Disabled** | Le matériel ne signale plus le décrochage |
| AER / PFEH / MCA thresh / Log Transparent Errors | **Disabled** | Aucune remontée WHEA |
| MONITOR and MWAIT disable | **Enabled** (= MWAIT interdit) | Tous les cœurs restent en C0 |
| Global C-state Control | **Disabled** | Plus d'états de repos profonds |
| FCLK | **2067 MHz** forcé (3 chemins) | Hors Auto |
| DDR5 Nitro Mode | **Enable** | Marges de training mémoire réduites |
| ECC | **Disabled** | Erreurs mémoire non corrigées |

Sur les 40 tweaks du catalogue, **35 sont actifs ou partiellement actifs**, y compris tous les
tweaks classés `High`. Le profil cumule donc undervolt maximal, sur-boost, protections
électriques coupées et reporting d'erreurs éteint.

## Chaîne causale retenue

1. Le build lance N workers → tous les cœurs montent simultanément → **transitoire de courant**.
2. `Peak Current Control` et `Prochot VRM Throttling` étant désactivés, le VRM ne limite ni ne
   lisse ce pic → **chute de tension transitoire** (Vdroop) non régulée.
3. La tension effective est déjà abaissée de **−30** par le Curve Optimizer, à une fréquence
   relevée de **+200 MHz** et maintenue par un **Scalar 10X**.
4. La marge devient nulle : un cœur perd son état, **rate son interruption d'horloge** → `0x101`.
5. `Core Watchdog Timer` + AER + PFEH + MCA étant coupés, **rien n'est journalisé** : le
   journal WHEA-Logger est vide, ce qui rend le diagnostic impossible sans revenir en arrière.

Facteurs aggravants secondaires : `MONITOR/MWAIT` désactivé (les threads en attente bouclent
activement au lieu de se mettre en veille, tous les cœurs restent chauds), phases VRM en
`Extreme` (plus d'équilibrage thermique), FCLK forcé et Nitro actif.

> Note Windows : `HKLM\SYSTEM\CurrentControlSet\Control\CrashControl\CrashDumpEnabled` = **0**.
> Aucun minidump n'est écrit, d'où l'absence totale de trace exploitable après le BSOD.
> À repasser à `7` (dump automatique) ou `3` (kernel) pour toute campagne de diagnostic.

## Remédiation — désactiver les tweaks en cause

Depuis la refonte du 26/07/2026, **désactiver un tweak le défait** : `PlanService` réécrit chaque
paramètre qu'il occupe encore à la valeur par défaut du BIOS (voir README, « L'interrupteur décide
dans les deux sens »). Il n'y a donc rien à cocher pour revenir en arrière — il faut décocher.

### Vague 1 — arrêter les crashs (à désactiver d'un bloc)

| Tweak à désactiver | Ce qui est rétabli | Source du défaut |
|---|---|---|
| `curve_neg30` | Curve Optimizer → Disable, Sign → Positive, Magnitude → 0 | catalogue |
| `pbo_boost` | Boost Override → Disabled, +0 MHz | catalogue |
| `vrm_protect` | Prochot VRM & Peak Current Control → Auto | BIOS / Auto |
| `error_reporting_off` | Core Watchdog, AER, PFEH, MCA, ECRC → valeurs BIOS | BIOS |
| `mwait_off` | MONITOR/MWAIT → valeur BIOS | BIOS |

Soit **26 écritures** mesurées sur l'export du 23/07, toutes des retours au défaut. Appliquer,
redémarrer, puis relancer le build en boucle (`npm run build` × 20) pour valider.

`error_reporting_off` est le plus important pour la suite : tant qu'il est actif, aucune erreur
matérielle n'est journalisée et le prochain crash restera aussi opaque que les précédents.

### Vague 2 — si le crash persiste

Désactiver `pbo_base` (PBO revient à Auto, Scalar 1X), `vrm_extreme` (phases VRM) et `cstates`.

### Vague 3 — mémoire / fabric

Désactiver `fclk_2067`, `nitro`, `mem_latency`. Si les BSOD deviennent des `0x124` ou font
apparaître des WHEA-Logger une fois le reporting réactivé, commencer directement par cette vague.

### Limite connue : 24 paramètres non réinitialisables

Ce firmware n'expose ni ligne `BIOS Default` ni option `Auto` pour 24 des paramètres actifs —
essentiellement `soc_d3_off` (14), `power_gating_off` (3), `usb4_off` (2), `aspm` (2),
`misc_power` (2), `misc_flags` (1). Les désactiver ne les remet donc pas par défaut : l'app le
signale au lieu de deviner une valeur. Aucun de ces réglages n'est impliqué dans la chaîne causale
du 0x101 ; pour les remettre réellement à zéro, il faut passer par un **Load Optimized Defaults**
dans le BIOS ou un clear CMOS.

### Retour progressif vers la performance

Une fois stable, remonter **un seul cran à la fois**, avec 48 h de validation entre chaque :
`pbo_base` seul → `curve_neg30` avec une magnitude réduite (15 plutôt que 30, à éditer dans
`catalog.json`) → `pbo_boost`. Ne jamais réactiver `vrm_protect` ni `error_reporting_off` : ces
deux tweaks n'apportent aucun gain mesurable et suppriment les garde-fous et le diagnostic.

> `curve_neg30` et `pbo_boost` ont été retirés du profil **Sélection recommandée** à la suite de
> cet incident. Ils restent disponibles, mais ne sont plus proposés par défaut.
