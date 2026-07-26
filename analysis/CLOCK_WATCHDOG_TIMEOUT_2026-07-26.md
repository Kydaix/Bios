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

## Remédiation — catégorie « Stabilité » du catalogue

Décocher un tweak dans BiosTuner **n'annule rien** : `PlanService` n'écrit que les règles des
tweaks cochés. Il faut donc des règles inverses explicites. La catégorie `stability`
(en tête du catalogue) fournit ces contreparties, chacune en `exclusiveGroup` avec son tweak
agressif — la cocher décoche automatiquement l'autre.

### Vague 1 — arrêter les crashs (à appliquer d'un bloc)

| Tweak | Action |
|---|---|
| `stab_curve_off` | Curve Optimizer → 0 |
| `stab_boost_off` | Boost Override → +0 MHz |
| `stab_vrm_protect_on` | Prochot VRM + Peak Current Control → Enable |
| `stab_ras_on` | Core Watchdog + AER + PFEH + MCA → Enabled |
| `stab_mwait_on` | MONITOR/MWAIT → disponibles |

Ces cinq tweaks sont marqués `recommended` : le bouton **Recommandé** les sélectionne.
Redémarrer, puis relancer le build en boucle (`npm run build` × 20) pour valider.

### Vague 2 — si le crash persiste

`stab_pbo_off` (PBO complètement stock), `stab_vrm_phase_opt` (phases → Optimized / T.Probe),
`stab_cstates_auto` (C-states → Auto).

### Vague 3 — mémoire / fabric

`stab_fclk_auto`, `stab_nitro_off`. Si les BSOD deviennent des `0x124` ou des erreurs
WHEA-Logger après réactivation du reporting, commencer directement par cette vague.

### Retour progressif vers la performance

Une fois stable, remonter **un seul cran à la fois**, avec 48 h de validation entre chaque :
`stab_curve_15` (CO −15) → `pbo_base` seul (sans boost override) → `pbo_boost`.
Ne jamais recocher `vrm_protect` (Prochot/PCC désactivés) ni `error_reporting_off` : ces deux
tweaks n'apportent aucun gain mesurable et suppriment les garde-fous et le diagnostic.
