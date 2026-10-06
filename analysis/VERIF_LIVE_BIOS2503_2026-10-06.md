# Vérification live de BIOS Tuner sous BIOS 2503

Export effectué le **6 octobre 2026 à 13 h 32, heure de Paris**, sur la machine cible : **ASUS ROG STRIX X870-A GAMING WIFI, Ryzen 7 9800X3D, BIOS installé 2503**.

**Mise à jour du catalogue après cet audit :** le profil CO a été remplacé par `curve_neg15`, cible **All Cores / Negative / 15**. Le rapport et ses fichiers JSON/CSV conservent la cible historique −30 et l'empreinte du catalogue audité. La nouvelle cible −15 a été [vérifiée séparément](BIOS2503_curve_neg15_validation.json) sur le même export et dans l'IFR, sans écriture BIOS. Le CO lu sur la machine reste −5.

**L'export confirme la compatibilité de 177 règles sur 179 et des 59 sélecteurs explicites du catalogue. Les deux règles SecureBio restent obsolètes.** Toutes les cibles des règles présentes sont disponibles dans l'export de cette machine ; aucun token ou offset fixé par le catalogue n'est périmé.

## Export et contrôles

SCEWIN 5.05.01.0002 a été exécuté en administrateur avec les seuls arguments `/O /S <fichier>`. Il a terminé avec le code de sortie 0 et produit un export de 896 518 octets. Aucun import `/I`, réglage UEFI ou flash n'a été effectué.

- [Export brut before.txt](../runs/bios2503_live_20261006T113225479894Z/before.txt)
- [Métadonnées et commande exécutée](../runs/bios2503_live_20261006T113225479894Z/metadata.json)
- [Comparaison complète CSV](BIOS2503_live_rules.csv)
- [Résultats et preuves JSON](BIOS2503_live_audit.json)

L'export brut et ses métadonnées restent locaux dans `runs/`, ignoré par Git. Les résultats JSON et CSV sont versionnés dans `analysis/`.

SHA-256 de l'export :

```text
db20919e6148d2755ca05b7769cf2b76e9ba9984e92931c38dfb410bc2b69944
```

| Contrôle | Résultat |
|---|---|
| Blocs Setup Question actifs exportés | 2 676 |
| Règles du catalogue | 179 |
| Règles présentes et cibles disponibles | 177 |
| Règles absentes | 2 |
| Règles avec token ou offset explicite retrouvées | 59 / 59 |
| Règles entièrement conformes aux valeurs cibles | 147 |
| Règles présentes avec au moins un écart de valeur | 30 |
| Correspondances concrètes entre règles et blocs | 184 |
| Correspondances conformes / différentes | 151 / 33 |

Certaines règles ciblent plusieurs blocs ; plusieurs profils partagent les mêmes paramètres. Les 33 écarts sont donc des **comparaisons**, pas 33 paramètres indépendants à modifier. Les paliers FCLK et tREFI sont mutuellement exclusifs et ne peuvent pas tous être conformes simultanément.

Le parseur de contrôle reprend les critères de `ScewinParser` : libellé exact après nettoyage, token et offset insensibles à la casse, occurrence éventuelle, codes d'options et valeurs actives. Les blocs et lignes commentés sont exclus. L'export est encodé en Windows-1252 ; les lignes utiles aux règles contrôlées sont ASCII, donc non affectées par la lecture UTF-8 utilisée dans l'application. Les empreintes de l'export et du catalogue ont été recontrôlées après l'analyse.

## État des principaux profils

| Profil ou réglage | Valeur lue | Comparaison avec le catalogue |
|---|---|---|
| PBO AMD Overclocking | Advanced, limites Motherboard, Scalar manuel 10X | Conforme, 4 / 4 |
| Boost CPU AMD Overclocking | Enabled Positive, +200 MHz | Conforme, 2 / 2 |
| Curve Optimizer | All Cores, Negative, magnitude 5 | **−5 actuellement**, cible du profil −30 |
| Neutralisation des copies ASUS | 1 / 8 conforme | 7 comparaisons différentes ; copies PBO/boost/CO pas toutes sur les valeurs neutres du catalogue |
| Latence mémoire | 3 / 9 conformes | Power Down Enable ×3, Gear Down Mode et Memory Context Restore ×2 restent sur Auto |
| FCLK | ASUS 2100 MHz ; AMD OC et FCLK Frequency sur Auto | Profil 2100 partiellement appliqué, 3 / 5 |
| tREFI | Auto / 0 | Aucun des profils 60000 ou 65535 appliqué |
| tWR | Auto / 0 | Profil 48 non appliqué |
| Nitro | 16 / 16 conformes | Conforme |
| PSPP Policy | Disabled | Différent de la cible Performance |
| PCIEX16 G5 Link Mode | GEN 5 | Conforme |
| ACPI Sleep State | S3 Suspend to RAM | Différent de la cible Suspend Disabled |

Les valeurs des copies ASUS et AMD sont celles renvoyées par SCEWIN ; leur synchronisation interne n'est pas démontrée par cet export. Par exemple, une copie textuelle ASUS du boost indique `199`, alors que les champs numériques ASUS et AMD indiquent `200`. Cela doit être distingué de la valeur effectivement utilisée par le processeur.

Les écarts ci-dessus décrivent l'état actuel. Sans export effectué immédiatement avant la mise à jour, ils ne peuvent pas être attribués avec certitude au passage au 2503. Ils ne constituent pas non plus une demande d'appliquer les valeurs du catalogue, notamment le CO −30.

## Règles supprimées et valeurs de retour par défaut

`SecureBio Support` et `SecureBio Camera Support` sont les **seules questions absentes**. Les quatre autres règles de `security_off` sont présentes et conformes. L'application peut afficher ce tweak comme actif, car elle évalue les seules règles applicables, tout en signalant les deux absentes dans le détail. Le catalogue doit encore être adapté sur ce point.

La résolution des valeurs de retour par défaut, sur les 184 correspondances, donne :

| Source du défaut selon le logiciel | Correspondances |
|---|---|
| Ligne BIOS Default de l'export | 124 |
| Valeur explicite du catalogue | 10 |
| Option Auto de repli | 26 |
| Aucune valeur déterminable | 24 |

Les 24 correspondances sans défaut déterminable sont actuellement conformes à leur cible. Elles concernent notamment CLKREQ, PM L1 SS, les états D3 du SOC, certains réglages de power gating, USB4 et Processor Aggregator Device. Le logiciel les laissera inchangées lors d'une désactivation et les signalera, conformément à sa logique actuelle. L'option Auto est une solution de repli du logiciel, pas une preuve du défaut constructeur.

## Conclusion

Le contrôle live confirme le constat de l'[audit des fichiers CAP](VERIF_BIOS2503_2026-10-06.md) : aucune adaptation des 59 sélecteurs explicites n'est nécessaire pour cet export, mais les deux règles SecureBio doivent être retirées du catalogue dédié au 2503 ou conditionnées à la version de BIOS.

Le fonctionnement de l'export SCEWIN est validé sur cette machine. L'import, la restauration et la stabilité des profils sous charge n'ont pas été testés, car ils ne font pas partie de cet export en lecture seule. Le code applicatif, le catalogue et les réglages BIOS sont restés inchangés.
