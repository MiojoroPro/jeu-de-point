# Jeu de Point

Un jeu de strategie sur grille pour 2 joueurs, developpe en C# avec Windows Forms.

## Description

Le **Jeu de Point** est un jeu au tour par tour ou deux joueurs s'affrontent sur une grille. L'objectif est d'aligner 5 points consecutifs pour marquer des points tout en empechant l'adversaire de faire de meme.

## Regles du jeu
 
### Objectif
Aligner **5 points consecutifs** (horizontalement, verticalement ou en diagonale) pour marquer 1 point.

### Deroulement d'une partie

1. **Joueur 1** (Rouge) et **Joueur 2** (Bleu) jouent a tour de role
2. A chaque tour, le joueur peut choisir entre deux modes d'action :
   - **Mode Pose** : Placer un point sur une intersection libre de la grille
   - **Mode Tir** : Tirer sur un point adverse pour le detruire

### Mode Pose
- Cliquez sur une intersection vide de la grille pour y placer votre point
- Si vous alignez 5 points ou plus, une ligne est tracee et vous gagnez 1 point de score

### Mode Tir
Le mode tir permet de detruire les points adverses non proteges :

| Parametre | Description |
|-----------|-------------|
| **Puissance** | Intensite du tir (1-9) |
| **Hauteur** | Position verticale du canon sur la grille |

**Restrictions du tir :**
- Vous ne pouvez pas detruire vos propres points
- Les points faisant partie d'une ligne validee (5 points alignes) sont **proteges** et ne peuvent pas etre detruits

### Regles sur les lignes
- Une nouvelle ligne ne peut pas **croiser** une ligne diagonale adverse
- Deux lignes ne peuvent pas partager plus d'**1 point en commun**

## Configuration

Au demarrage d'une nouvelle partie, vous pouvez configurer :
- **Nombre de lignes de la grille** : de 5 a 30 (par defaut : 10)

## Prérequis

- **.NET 10 SDK** (ou version compatible)
- **PostgreSQL** (pour la sauvegarde des parties)

### Configuration de la base de données

La connexion PostgreSQL utilise par defaut :
```
Host=localhost;Port=5432;Username=postgres;Password=root;Database=jeu_de_point
```

Vous pouvez personnaliser cette connexion via la variable d'environnement :
```bash
set JEU_DB_CONNECTION="Host=localhost;Port=5432;Username=votre_user;Password=votre_mdp;Database=jeu_de_point"
```

## Installation et lancement

### Option 1 : Ligne de commande
```bash
# Cloner le projet
git clone <url-du-repo>
cd jeu-de-point

# Lancer le jeu
dotnet run
```

### Option 2 : Visual Studio
1. Ouvrir `jeu-de-point.sln` dans Visual Studio 2022
2. Appuyer sur **F5** ou cliquer sur **Demarrer**

### Option 3 : Build et execution
```bash
dotnet build
dotnet run --project jeu-de-point.csproj
```

## Fonctionnalites

- **Nouvelle partie** : Demarrer une partie avec configuration personnalisee
- **Charger une partie** : Reprendre une partie sauvegardee depuis PostgreSQL
- **Sauvegarder** : Enregistrer l'etat actuel de la partie dans la base de donnees
- **Affichage du score** : Score en temps reel pour chaque joueur
- **Animation de tir** : Animation visuelle lors des tirs de canon

## Structure du projet

```
jeu-de-point/
├── Form1.cs              # Logique principale du jeu et interface
├── Form1.Designer.cs     # Code genere pour l'interface
├── Joueur.cs             # Classe representant un joueur
├── Program.cs            # Point d'entree de l'application
├── jeu-de-point.csproj   # Configuration du projet .NET
└── jeu-de-point.sln      # Solution Visual Studio
```

## Technologies utilisees

- **C# / .NET 10** (Windows Forms)
- **PostgreSQL** (stockage des sauvegardes)
- **Npgsql** (driver PostgreSQL pour .NET)
- **System.Text.Json** (serialisation des donnees)

## Captures d'ecran

Le jeu affiche :
- Un **menu principal** avec options Nouvelle partie / Charger
- Un **ecran de configuration** pour parametrer la grille
- La **grille de jeu** avec les points des deux joueurs
- Un **panneau lateral** avec les controles (mode, puissance, hauteur)
- L'**affichage des scores** des deux joueurs

## Auteur

Projet developpe en C# Windows Forms.
