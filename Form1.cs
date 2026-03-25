using System.Drawing.Drawing2D;
using System.Text.Json;
using Npgsql;

namespace jeu_de_point
{
    public partial class Form1 : Form
    {
        private enum EtatEcran
        {
            MenuPrincipal,
            ConfigurationNouvellePartie,
            Partie
        }

        private enum ModeActionTour
        {
            Pose,
            Tir
        }

        private int gridLines = 10;
        private const int PaddingAroundGrid = 60;

        private readonly Dictionary<(int Col, int Row), Joueur> pointsPoses = new();
        private Joueur[] joueurs = Array.Empty<Joueur>();
        private int indexJoueurCourant;

        private EtatEcran etatEcran = EtatEcran.MenuPrincipal;

        private Panel panelMenu = null!;
        private Panel panelConfiguration = null!;
        private Panel carteMenu = null!;
        private Panel carteConfiguration = null!;
        private Panel panelActionsPartie = null!;

        private Button boutonNouvellePartie = null!;
        private Button boutonChargerPartie = null!;
        private Button boutonRetourMenuConfiguration = null!;
        private NumericUpDown inputGridLines = null!;
        private Button boutonDemarrerPartie = null!;
        private Button boutonMenuPrincipalPartie = null!;
        private Button boutonNouvellePartiePartie = null!;
        private Button boutonSauvegarderPartie = null!;
        private ComboBox inputModeAction = null!;
        private NumericUpDown inputPuissanceTir = null!;
        private TrackBar inputHauteurCanon = null!;
        private Label labelModeAction = null!;
        private Label labelPuissanceTir = null!;
        private Label labelHauteurCanon = null!;

        private const string NomSauvegardeParDefaut = "derniere_partie";

        private readonly record struct LigneValidee((int Col, int Row) Debut, (int Col, int Row) Fin, Joueur Proprietaire)
        {
            public Color Couleur => Proprietaire.Couleur;
            public bool EstDiagonale => Debut.Col != Fin.Col && Debut.Row != Fin.Row;
        }

        private sealed class EtatPartieSauvegarde
        {
            public int GridLines { get; set; }
            public int IndexJoueurCourant { get; set; }
            public int ModeTour { get; set; }
            public int PuissanceTir { get; set; }
            public int[] HauteursCanons { get; set; } = [];
            public List<JoueurSauvegarde> Joueurs { get; set; } = [];
            public List<PointSauvegarde> Points { get; set; } = [];
            public List<LigneSauvegarde> Lignes { get; set; } = [];
        }

        private sealed class JoueurSauvegarde
        {
            public string Nom { get; set; } = string.Empty;
            public int CouleurArgb { get; set; }
            public int Score { get; set; }
        }

        private sealed class PointSauvegarde
        {
            public int Col { get; set; }
            public int Row { get; set; }
            public int Proprietaire { get; set; }
        }

        private sealed class LigneSauvegarde
        {
            public int DebutCol { get; set; }
            public int DebutRow { get; set; }
            public int FinCol { get; set; }
            public int FinRow { get; set; }
            public int Proprietaire { get; set; }
        }

        // Conserver toutes les lignes valid�es pour le score et le rendu
        private readonly List<LigneValidee> lignesAlignements = new();

        private ModeActionTour modeTour = ModeActionTour.Pose;
        private readonly int[] hauteursCanonsParJoueur = [0, 0];

        private System.Windows.Forms.Timer timerAnimationTir = null!;
        private bool tirEnCours;
        private PointF origineBoulet;
        private PointF destinationBoulet;
        private PointF positionBoulet;
        private float progressionTir;
        private int ticksPauseImpact;
        private bool impactTraite;
        private bool tirDetruitPoint;
        private (int Col, int Row) pointDetruit;

        // M�thode isol�e pour brancher les �v�nements souris
        private void InitialiserEcouteSouris()
        {
            MouseClick += Form1_MouseClick;
        }

        private void ConfigurerStyleBouton(Button bouton, Color fond, Color texte)
        {
            bouton.FlatStyle = FlatStyle.Flat;
            bouton.FlatAppearance.BorderSize = 0;
            bouton.BackColor = fond;
            bouton.ForeColor = texte;
            bouton.Cursor = Cursors.Hand;
            bouton.UseVisualStyleBackColor = false;
        }

        private void InitialiserEcranAccueil()
        {
            panelMenu = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(240, 246, 255)
            };

            panelConfiguration = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(245, 250, 255),
                Visible = false
            };

            var titreMenu = new Label
            {
                AutoSize = true,
                Text = "Jeu de point",
                Font = new Font(Font.FontFamily, 22f, FontStyle.Bold),
                ForeColor = Color.FromArgb(32, 63, 110),
                Margin = new Padding(0, 0, 0, 18),
                TextAlign = ContentAlignment.MiddleCenter
            };

            boutonNouvellePartie = new Button
            {
                Text = "Nouvelle partie",
                Font = new Font(Font.FontFamily, 12f, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 12)
            };
            ConfigurerStyleBouton(boutonNouvellePartie, Color.FromArgb(38, 112, 233), Color.White);
            boutonNouvellePartie.Click += (_, _) => AfficherConfigurationNouvellePartie();

            boutonChargerPartie = new Button
            {
                Text = "Charger une partie",
                Font = new Font(Font.FontFamily, 12f, FontStyle.Bold),
                Margin = new Padding(0)
            };
            ConfigurerStyleBouton(boutonChargerPartie, Color.FromArgb(92, 138, 214), Color.White);
            boutonChargerPartie.Click += (_, _) => ChargerSauvegardeDepuisMenu();

            int largeurBoutonsMenu = Math.Max(
                TextRenderer.MeasureText(boutonNouvellePartie.Text, boutonNouvellePartie.Font).Width,
                TextRenderer.MeasureText(boutonChargerPartie.Text, boutonChargerPartie.Font).Width) + 90;

            var tailleBoutonMenu = new Size(largeurBoutonsMenu, 54);
            boutonNouvellePartie.Size = tailleBoutonMenu;
            boutonChargerPartie.Size = tailleBoutonMenu;

            carteMenu = new Panel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.White,
                Padding = new Padding(34),
                BorderStyle = BorderStyle.FixedSingle
            };

            var layoutMenu = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.Transparent
            };
            layoutMenu.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutMenu.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutMenu.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            layoutMenu.Controls.Add(titreMenu, 0, 0);
            layoutMenu.Controls.Add(boutonNouvellePartie, 0, 1);
            layoutMenu.Controls.Add(boutonChargerPartie, 0, 2);
            carteMenu.Controls.Add(layoutMenu);
            panelMenu.Controls.Add(carteMenu);

            boutonRetourMenuConfiguration = new Button
            {
                Text = "Menu principal",
                Font = new Font(Font.FontFamily, 10.5f, FontStyle.Bold),
                Size = new Size(150, 38)
            };
            ConfigurerStyleBouton(boutonRetourMenuConfiguration, Color.FromArgb(88, 105, 128), Color.White);
            boutonRetourMenuConfiguration.Click += (_, _) => AfficherMenuPrincipal();

            var titreConfiguration = new Label
            {
                AutoSize = true,
                Text = "Nouvelle partie",
                Font = new Font(Font.FontFamily, 18f, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 74, 122),
                Margin = new Padding(0, 0, 0, 18),
                TextAlign = ContentAlignment.MiddleCenter
            };

            var labelGridLines = new Label
            {
                AutoSize = true,
                Text = "Nombre de lignes de la grille :",
                Font = new Font(Font.FontFamily, 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(55, 71, 95),
                Margin = new Padding(0, 0, 0, 10)
            };

            inputGridLines = new NumericUpDown
            {
                Minimum = 5,
                Maximum = 30,
                Value = 10,
                Width = 170,
                Font = new Font(Font.FontFamily, 12f, FontStyle.Regular),
                Margin = new Padding(0, 0, 0, 16)
            };

            boutonDemarrerPartie = new Button
            {
                Text = "Valider",
                Font = new Font(Font.FontFamily, 12f, FontStyle.Bold),
                Size = new Size(170, 48),
                Margin = new Padding(0)
            };
            ConfigurerStyleBouton(boutonDemarrerPartie, Color.FromArgb(39, 166, 117), Color.White);
            boutonDemarrerPartie.Click += (_, _) => DemarrerNouvellePartie((int)inputGridLines.Value);

            carteConfiguration = new Panel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.White,
                Padding = new Padding(34),
                BorderStyle = BorderStyle.FixedSingle
            };

            var layoutConfiguration = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Color.Transparent
            };
            layoutConfiguration.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutConfiguration.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutConfiguration.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutConfiguration.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            layoutConfiguration.Controls.Add(titreConfiguration, 0, 0);
            layoutConfiguration.Controls.Add(labelGridLines, 0, 1);
            layoutConfiguration.Controls.Add(inputGridLines, 0, 2);
            layoutConfiguration.Controls.Add(boutonDemarrerPartie, 0, 3);
            carteConfiguration.Controls.Add(layoutConfiguration);

            panelConfiguration.Controls.Add(boutonRetourMenuConfiguration);
            panelConfiguration.Controls.Add(carteConfiguration);

            panelActionsPartie = new Panel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.White,
                Padding = new Padding(10),
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false,
                Dock = DockStyle.Right
            };

            var colonneActions = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.Transparent,
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };

            var panelNavigation = new Panel
            {
                AutoSize = true,
                BackColor = Color.FromArgb(245, 248, 252),
                Padding = new Padding(10),
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 0, 10)
            };

            var layoutNavigation = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };

            var panelCommandes = new Panel
            {
                AutoSize = true,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            var layoutCommandes = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 3,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            layoutCommandes.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layoutCommandes.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layoutCommandes.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutCommandes.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutCommandes.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            boutonMenuPrincipalPartie = new Button
            {
                Text = "Menu principal",
                Font = new Font(Font.FontFamily, 10f, FontStyle.Bold),
                Size = new Size(180, 38),
                Margin = new Padding(0, 0, 0, 8)
            };
            ConfigurerStyleBouton(boutonMenuPrincipalPartie, Color.FromArgb(88, 105, 128), Color.White);
            boutonMenuPrincipalPartie.Click += (_, _) => AfficherMenuPrincipal();

            boutonNouvellePartiePartie = new Button
            {
                Text = "Nouvelle partie",
                Font = new Font(Font.FontFamily, 10f, FontStyle.Bold),
                Size = new Size(180, 38),
                Margin = new Padding(0, 0, 0, 8)
            };
            ConfigurerStyleBouton(boutonNouvellePartiePartie, Color.FromArgb(38, 112, 233), Color.White);
            boutonNouvellePartiePartie.Click += (_, _) => AfficherConfigurationNouvellePartie();

            boutonSauvegarderPartie = new Button
            {
                Text = "Sauvegarder",
                Font = new Font(Font.FontFamily, 10f, FontStyle.Bold),
                Size = new Size(180, 38),
                Margin = new Padding(0)
            };
            ConfigurerStyleBouton(boutonSauvegarderPartie, Color.FromArgb(34, 148, 83), Color.White);
            boutonSauvegarderPartie.Click += (_, _) => SauvegarderPartieEnCours();

            labelModeAction = new Label
            {
                AutoSize = true,
                Text = "Mode",
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 8, 6, 0),
                ForeColor = Color.FromArgb(45, 58, 78),
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold)
            };

            inputModeAction = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 110,
                Margin = new Padding(0, 4, 14, 0)
            };
            inputModeAction.Items.Add("Pose");
            inputModeAction.Items.Add("Tir");
            inputModeAction.SelectedIndex = 0;
            inputModeAction.SelectedIndexChanged += (_, _) =>
            {
                modeTour = inputModeAction.SelectedIndex == 1 ? ModeActionTour.Tir : ModeActionTour.Pose;
                MettreAJourVisibiliteControlesTir();
                Invalidate();
            };

            labelPuissanceTir = new Label
            {
                AutoSize = true,
                Text = "Puissance",
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(14, 4, 8, 0),
                ForeColor = Color.FromArgb(45, 58, 78),
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold)
            };

            inputPuissanceTir = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 9,
                Value = 5,
                Width = 55,
                Margin = new Padding(0, 2, 0, 0)
            };

            labelHauteurCanon = new Label
            {
                AutoSize = true,
                Text = "Hauteur",
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 4, 8, 0),
                ForeColor = Color.FromArgb(45, 58, 78),
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold)
            };

            inputHauteurCanon = new TrackBar
            {
                Minimum = 0,
                Maximum = 9,
                Value = 5,
                TickStyle = TickStyle.None,
                Width = 130,
                Height = 32,
                Margin = new Padding(0, -2, 0, 0)
            };
            inputHauteurCanon.ValueChanged += (_, _) =>
            {
                if (joueurs.Length == 0 || tirEnCours)
                {
                    return;
                }

                hauteursCanonsParJoueur[indexJoueurCourant] = inputHauteurCanon.Value;
                Invalidate();
            };

            layoutNavigation.Controls.Add(boutonMenuPrincipalPartie);
            layoutNavigation.Controls.Add(boutonNouvellePartiePartie);
            layoutNavigation.Controls.Add(boutonSauvegarderPartie);
            panelNavigation.Controls.Add(layoutNavigation);

            layoutCommandes.Controls.Add(labelModeAction, 0, 0);
            layoutCommandes.Controls.Add(inputModeAction, 1, 0);
            layoutCommandes.Controls.Add(labelPuissanceTir, 0, 1);
            layoutCommandes.Controls.Add(inputPuissanceTir, 1, 1);
            layoutCommandes.Controls.Add(labelHauteurCanon, 0, 2);
            layoutCommandes.Controls.Add(inputHauteurCanon, 1, 2);
            panelCommandes.Controls.Add(layoutCommandes);

            colonneActions.Controls.Add(panelNavigation);
            colonneActions.Controls.Add(panelCommandes);
            panelActionsPartie.Controls.Add(colonneActions);

            Controls.Add(panelMenu);
            Controls.Add(panelConfiguration);
            Controls.Add(panelActionsPartie);

            Resize += (_, _) =>
            {
                RecentrerLayout();
                PositionnerBoutonsHautDroite();
            };

            RecentrerLayout();
            PositionnerBoutonsHautDroite();
            MettreAJourVisibiliteControlesTir();
        }

        private void MettreAJourVisibiliteControlesTir()
        {
            bool estTir = modeTour == ModeActionTour.Tir;
            labelPuissanceTir.Visible = estTir;
            inputPuissanceTir.Visible = estTir;
            labelHauteurCanon.Visible = estTir;
            inputHauteurCanon.Visible = estTir;
        }

        private void PositionnerBoutonsHautDroite()
        {
            if (panelConfiguration != null && boutonRetourMenuConfiguration != null)
            {
                boutonRetourMenuConfiguration.Left = panelConfiguration.ClientSize.Width - boutonRetourMenuConfiguration.Width - 18;
                boutonRetourMenuConfiguration.Top = 18;
            }

            if (panelActionsPartie != null && panelActionsPartie.Dock == DockStyle.None)
            {
                panelActionsPartie.Left = ClientSize.Width - panelActionsPartie.Width - 18;
                panelActionsPartie.Top = 18;
            }
        }

        private void RecentrerLayout()
        {
            if (carteMenu != null)
            {
                carteMenu.Left = (panelMenu.ClientSize.Width - carteMenu.Width) / 2;
                carteMenu.Top = (panelMenu.ClientSize.Height - carteMenu.Height) / 2;
            }

            if (carteConfiguration != null)
            {
                carteConfiguration.Left = (panelConfiguration.ClientSize.Width - carteConfiguration.Width) / 2;
                carteConfiguration.Top = (panelConfiguration.ClientSize.Height - carteConfiguration.Height) / 2;
            }
        }

        private void AfficherMenuPrincipal()
        {
            etatEcran = EtatEcran.MenuPrincipal;
            panelMenu.Visible = true;
            panelConfiguration.Visible = false;
            panelActionsPartie.Visible = false;
            panelMenu.BringToFront();
            RecentrerLayout();
            Invalidate();
        }

        private void AfficherConfigurationNouvellePartie()
        {
            etatEcran = EtatEcran.ConfigurationNouvellePartie;
            panelMenu.Visible = false;
            panelConfiguration.Visible = true;
            panelActionsPartie.Visible = false;
            panelConfiguration.BringToFront();
            RecentrerLayout();
            PositionnerBoutonsHautDroite();
            Invalidate();
        }

        private void DemarrerNouvellePartie(int valeurGridLines)
        {
            gridLines = Math.Max(2, valeurGridLines);
            pointsPoses.Clear();
            lignesAlignements.Clear();
            tirEnCours = false;
            progressionTir = 0f;

            InitialiserJoueursEtTour();

            modeTour = ModeActionTour.Pose;
            inputModeAction.SelectedIndex = 0;

            int hauteurInitiale = (gridLines - 1) / 2;
            hauteursCanonsParJoueur[0] = hauteurInitiale;
            hauteursCanonsParJoueur[1] = hauteurInitiale;

            inputHauteurCanon.Minimum = 0;
            inputHauteurCanon.Maximum = Math.Max(0, gridLines - 1);
            inputHauteurCanon.Value = Math.Clamp(hauteurInitiale, inputHauteurCanon.Minimum, inputHauteurCanon.Maximum);
            MettreAJourVisibiliteControlesTir();

            etatEcran = EtatEcran.Partie;
            panelMenu.Visible = false;
            panelConfiguration.Visible = false;
            panelActionsPartie.Visible = true;
            panelActionsPartie.BringToFront();
            PositionnerBoutonsHautDroite();
            SynchroniserControlesTour();
            Invalidate();
        }

        // M�thode isol�e pour initialiser les joueurs et le tour
        private void InitialiserJoueursEtTour()
        {
            joueurs =
            [
                new Joueur("Joueur 1", Color.Red),
                new Joueur("Joueur 2", Color.Blue)
            ];

            indexJoueurCourant = 0;
        }

        private Joueur JoueurCourant => joueurs[indexJoueurCourant];

        private void PasserAuJoueurSuivant()
        {
            indexJoueurCourant = (indexJoueurCourant + 1) % joueurs.Length;
            SynchroniserControlesTour();
        }

        private void SynchroniserControlesTour()
        {
            if (joueurs.Length == 0)
            {
                return;
            }

            int min = inputHauteurCanon.Minimum;
            int max = inputHauteurCanon.Maximum;
            int hauteur = Math.Clamp(hauteursCanonsParJoueur[indexJoueurCourant], min, max);
            hauteursCanonsParJoueur[indexJoueurCourant] = hauteur;
            if (inputHauteurCanon.Value != hauteur)
            {
                inputHauteurCanon.Value = hauteur;
            }
        }

        private string ObtenirConnectionStringPostgres()
        {
            return Environment.GetEnvironmentVariable("JEU_DB_CONNECTION")
                ?? "Host=localhost;Port=5432;Username=postgres;Password=root;Database=jeu_de_point";
        }

        private bool InitialiserTableSauvegardes(out string erreur)
        {
            erreur = string.Empty;

            try
            {
                using var connexion = new NpgsqlConnection(ObtenirConnectionStringPostgres());
                connexion.Open();

                const string sql = """
                    CREATE TABLE IF NOT EXISTS parties_sauvegardes (
                        nom TEXT PRIMARY KEY,
                        data JSONB NOT NULL,
                        updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
                    );
                    """;

                using var commande = new NpgsqlCommand(sql, connexion);
                commande.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                erreur = ex.Message;
                return false;
            }
        }

        private EtatPartieSauvegarde ConstruireEtatSauvegarde()
        {
            var etat = new EtatPartieSauvegarde
            {
                GridLines = gridLines,
                IndexJoueurCourant = indexJoueurCourant,
                ModeTour = modeTour == ModeActionTour.Tir ? 1 : 0,
                PuissanceTir = (int)inputPuissanceTir.Value,
                HauteursCanons =
                [
                    hauteursCanonsParJoueur[0],
                    hauteursCanonsParJoueur[1]
                ]
            };

            for (int i = 0; i < joueurs.Length; i++)
            {
                etat.Joueurs.Add(new JoueurSauvegarde
                {
                    Nom = joueurs[i].Nom,
                    CouleurArgb = joueurs[i].Couleur.ToArgb(),
                    Score = joueurs[i].Score
                });
            }

            foreach (var (position, proprietaire) in pointsPoses)
            {
                int indexProprietaire = Array.IndexOf(joueurs, proprietaire);
                if (indexProprietaire < 0)
                {
                    continue;
                }

                etat.Points.Add(new PointSauvegarde
                {
                    Col = position.Col,
                    Row = position.Row,
                    Proprietaire = indexProprietaire
                });
            }

            foreach (var ligne in lignesAlignements)
            {
                int indexProprietaire = Array.IndexOf(joueurs, ligne.Proprietaire);
                if (indexProprietaire < 0)
                {
                    continue;
                }

                etat.Lignes.Add(new LigneSauvegarde
                {
                    DebutCol = ligne.Debut.Col,
                    DebutRow = ligne.Debut.Row,
                    FinCol = ligne.Fin.Col,
                    FinRow = ligne.Fin.Row,
                    Proprietaire = indexProprietaire
                });
            }

            return etat;
        }

        private bool SauvegarderDansPostgres(string nomSauvegarde, out string erreur)
        {
            erreur = string.Empty;

            if (!InitialiserTableSauvegardes(out erreur))
            {
                return false;
            }

            try
            {
                string json = JsonSerializer.Serialize(ConstruireEtatSauvegarde());

                using var connexion = new NpgsqlConnection(ObtenirConnectionStringPostgres());
                connexion.Open();

                const string sql = """
                    INSERT INTO parties_sauvegardes (nom, data, updated_at)
                    VALUES (@nom, CAST(@data AS jsonb), NOW())
                    ON CONFLICT (nom)
                    DO UPDATE SET data = EXCLUDED.data, updated_at = NOW();
                    """;

                using var commande = new NpgsqlCommand(sql, connexion);
                commande.Parameters.AddWithValue("nom", nomSauvegarde);
                commande.Parameters.AddWithValue("data", json);
                commande.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                erreur = ex.Message;
                return false;
            }
        }

        private bool ChargerDepuisPostgres(string nomSauvegarde, out string erreur)
        {
            erreur = string.Empty;

            if (!InitialiserTableSauvegardes(out erreur))
            {
                return false;
            }

            try
            {
                using var connexion = new NpgsqlConnection(ObtenirConnectionStringPostgres());
                connexion.Open();

                const string sql = "SELECT data::text FROM parties_sauvegardes WHERE nom = @nom;";
                using var commande = new NpgsqlCommand(sql, connexion);
                commande.Parameters.AddWithValue("nom", nomSauvegarde);

                string? json = commande.ExecuteScalar() as string;
                if (string.IsNullOrWhiteSpace(json))
                {
                    erreur = "Aucune sauvegarde disponible.";
                    return false;
                }

                var etat = JsonSerializer.Deserialize<EtatPartieSauvegarde>(json);
                if (etat is null)
                {
                    erreur = "Sauvegarde invalide.";
                    return false;
                }

                AppliquerEtatSauvegarde(etat);
                return true;
            }
            catch (Exception ex)
            {
                erreur = ex.Message;
                return false;
            }
        }

        private void AppliquerEtatSauvegarde(EtatPartieSauvegarde etat)
        {
            gridLines = Math.Max(2, etat.GridLines);

            joueurs = etat.Joueurs
                .Select(j =>
                {
                    var joueur = new Joueur(j.Nom, Color.FromArgb(j.CouleurArgb));
                    joueur.DefinirScore(j.Score);
                    return joueur;
                })
                .ToArray();

            if (joueurs.Length < 2)
            {
                InitialiserJoueursEtTour();
            }

            pointsPoses.Clear();
            lignesAlignements.Clear();

            foreach (var point in etat.Points)
            {
                if (point.Col < 0 || point.Col >= gridLines || point.Row < 0 || point.Row >= gridLines)
                {
                    continue;
                }

                if (point.Proprietaire < 0 || point.Proprietaire >= joueurs.Length)
                {
                    continue;
                }

                pointsPoses[(point.Col, point.Row)] = joueurs[point.Proprietaire];
            }

            foreach (var ligne in etat.Lignes)
            {
                if (ligne.Proprietaire < 0 || ligne.Proprietaire >= joueurs.Length)
                {
                    continue;
                }

                var debut = (ligne.DebutCol, ligne.DebutRow);
                var fin = (ligne.FinCol, ligne.FinRow);
                var normalisee = NormaliserLigne(debut, fin);
                lignesAlignements.Add(new LigneValidee(normalisee.Debut, normalisee.Fin, joueurs[ligne.Proprietaire]));
            }

            indexJoueurCourant = Math.Clamp(etat.IndexJoueurCourant, 0, joueurs.Length - 1);
            modeTour = etat.ModeTour == 1 ? ModeActionTour.Tir : ModeActionTour.Pose;

            inputModeAction.SelectedIndex = modeTour == ModeActionTour.Tir ? 1 : 0;
            inputPuissanceTir.Value = Math.Clamp(etat.PuissanceTir, (int)inputPuissanceTir.Minimum, (int)inputPuissanceTir.Maximum);

            int hauteur0 = etat.HauteursCanons.Length > 0 ? etat.HauteursCanons[0] : 0;
            int hauteur1 = etat.HauteursCanons.Length > 1 ? etat.HauteursCanons[1] : 0;
            hauteursCanonsParJoueur[0] = hauteur0;
            hauteursCanonsParJoueur[1] = hauteur1;

            inputHauteurCanon.Minimum = 0;
            inputHauteurCanon.Maximum = Math.Max(0, gridLines - 1);
            SynchroniserControlesTour();

            tirEnCours = false;
            timerAnimationTir.Stop();
            progressionTir = 0f;
            ticksPauseImpact = 0;
            impactTraite = false;

            etatEcran = EtatEcran.Partie;
            panelMenu.Visible = false;
            panelConfiguration.Visible = false;
            panelActionsPartie.Visible = true;
            panelActionsPartie.BringToFront();
            PositionnerBoutonsHautDroite();
            MettreAJourVisibiliteControlesTir();
            Invalidate();
        }

        private void SauvegarderPartieEnCours()
        {
            if (etatEcran != EtatEcran.Partie)
            {
                return;
            }

            if (SauvegarderDansPostgres(NomSauvegardeParDefaut, out string erreur))
            {
                MessageBox.Show("Partie sauvegard�e dans PostgreSQL.", "Sauvegarde", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show($"Impossible de sauvegarder la partie.\n\nD�tail: {erreur}", "Erreur PostgreSQL", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ChargerSauvegardeDepuisMenu()
        {
            if (ChargerDepuisPostgres(NomSauvegardeParDefaut, out string erreur))
            {
                MessageBox.Show("Sauvegarde charg�e depuis PostgreSQL.", "Chargement", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show($"Impossible de charger la sauvegarde.\n\nD�tail: {erreur}", "Erreur PostgreSQL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // Normaliser une ligne pour que Debut <= Fin (lexicographique) afin d'�viter les doublons invers�s
        private ((int Col, int Row) Debut, (int Col, int Row) Fin) NormaliserLigne((int Col, int Row) a, (int Col, int Row) b)
        {
            if (a.Col < b.Col) return (a, b);
            if (a.Col > b.Col) return (b, a);
            if (a.Row <= b.Row) return (a, b);
            return (b, a);
        }

        // M�thode isol�e: d�tecter toutes les lignes candidates (5 points cons�cutifs ou plus)
        private List<((int Col, int Row) Debut, (int Col, int Row) Fin)> TrouverLignesCandidatesPose((int Col, int Row) pointJoue, Joueur joueur)
        {
            var resultats = new List<((int Col, int Row) Debut, (int Col, int Row) Fin)>();

            (int dCol, int dRow)[] directions =
            [
                (1, 0),
                (0, 1),
                (1, 1),
                (1, -1)
            ];

            foreach (var (dCol, dRow) in directions)
            {
                var sequence = new List<(int Col, int Row)>();

                int col = pointJoue.Col - dCol;
                int row = pointJoue.Row - dRow;
                while (pointsPoses.TryGetValue((col, row), out var jNeg) && ReferenceEquals(jNeg, joueur))
                {
                    sequence.Insert(0, (col, row));
                    col -= dCol;
                    row -= dRow;
                }

                sequence.Add(pointJoue);

                col = pointJoue.Col + dCol;
                row = pointJoue.Row + dRow;
                while (pointsPoses.TryGetValue((col, row), out var jPos) && ReferenceEquals(jPos, joueur))
                {
                    sequence.Add((col, row));
                    col += dCol;
                    row += dRow;
                }

                if (sequence.Count < 5)
                {
                    continue;
                }

                var normalisee = NormaliserLigne(sequence[0], sequence[^1]);
                if (!resultats.Any(l => l.Debut == normalisee.Debut && l.Fin == normalisee.Fin))
                {
                    resultats.Add(normalisee);
                }
            }

            return resultats;
        }

        private IEnumerable<(int Col, int Row)> EnumererPointsLigne((int Col, int Row) debut, (int Col, int Row) fin)
        {
            int dCol = Math.Sign(fin.Col - debut.Col);
            int dRow = Math.Sign(fin.Row - debut.Row);
            int longueur = Math.Max(Math.Abs(fin.Col - debut.Col), Math.Abs(fin.Row - debut.Row));

            for (int i = 0; i <= longueur; i++)
            {
                yield return (debut.Col + (i * dCol), debut.Row + (i * dRow));
            }
        }

        private static long ProduitVectoriel((int Col, int Row) a, (int Col, int Row) b, (int Col, int Row) c)
        {
            return ((long)(b.Col - a.Col) * (c.Row - a.Row)) - ((long)(b.Row - a.Row) * (c.Col - a.Col));
        }

        private static bool PointSurSegment((int Col, int Row) a, (int Col, int Row) b, (int Col, int Row) p)
        {
            return p.Col >= Math.Min(a.Col, b.Col) && p.Col <= Math.Max(a.Col, b.Col)
                && p.Row >= Math.Min(a.Row, b.Row) && p.Row <= Math.Max(a.Row, b.Row);
        }

        private static bool SegmentsSeCroisent((int Col, int Row) a, (int Col, int Row) b, (int Col, int Row) c, (int Col, int Row) d)
        {
            long o1 = ProduitVectoriel(a, b, c);
            long o2 = ProduitVectoriel(a, b, d);
            long o3 = ProduitVectoriel(c, d, a);
            long o4 = ProduitVectoriel(c, d, b);

            if ((o1 > 0 && o2 < 0 || o1 < 0 && o2 > 0) && (o3 > 0 && o4 < 0 || o3 < 0 && o4 > 0))
            {
                return true;
            }

            if (o1 == 0 && PointSurSegment(a, b, c)) return true;
            if (o2 == 0 && PointSurSegment(a, b, d)) return true;
            if (o3 == 0 && PointSurSegment(c, d, a)) return true;
            if (o4 == 0 && PointSurSegment(c, d, b)) return true;

            return false;
        }

        private bool EstLigneValideSelonRegles((int Col, int Row) debut, (int Col, int Row) fin, Joueur joueur)
        {
            var pointsNouvelleLigne = EnumererPointsLigne(debut, fin).ToHashSet();

            foreach (var ligneExistante in lignesAlignements)
            {
                if (!ReferenceEquals(ligneExistante.Proprietaire, joueur)
                    && ligneExistante.EstDiagonale
                    && SegmentsSeCroisent(debut, fin, ligneExistante.Debut, ligneExistante.Fin))
                {
                    return false;
                }

                int pointsCommuns = EnumererPointsLigne(ligneExistante.Debut, ligneExistante.Fin)
                    .Count(pointsNouvelleLigne.Contains);
                if (pointsCommuns > 1)
                {
                    return false;
                }
            }

            return true;
        }

        private bool PointAppartientALigneValidee((int Col, int Row) point)
        {
            foreach (var ligne in lignesAlignements)
            {
                if (EnumererPointsLigne(ligne.Debut, ligne.Fin).Contains(point))
                {
                    return true;
                }
            }

            return false;
        }

        private PointF ObtenirPositionCanonPourJoueur(int indexJoueur, int startX, int startY, int gridSize, float step)
        {
            float x = indexJoueur == 0 ? startX - (step * 0.9f) : startX + gridSize + (step * 0.9f);
            int row = Math.Clamp(hauteursCanonsParJoueur[indexJoueur], 0, gridLines - 1);
            float y = startY + (row * step);
            return new PointF(x, y);
        }

        private void DemarrerAnimationTir(PointF origine, PointF destination, bool detruitPoint, (int Col, int Row) pointCible)
        {
            origineBoulet = origine;
            destinationBoulet = destination;
            positionBoulet = origine;
            progressionTir = 0f;
            ticksPauseImpact = 0;
            impactTraite = false;
            tirEnCours = true;
            tirDetruitPoint = detruitPoint;
            pointDetruit = pointCible;
            timerAnimationTir.Start();
        }

        private void TimerAnimationTir_Tick(object? sender, EventArgs e)
        {
            const float vitesseAnimation = 0.04f;
            const int pauseImpactTicks = 14;

            if (progressionTir < 1f)
            {
                progressionTir += vitesseAnimation;
                progressionTir = Math.Min(1f, progressionTir);

                float x = origineBoulet.X + ((destinationBoulet.X - origineBoulet.X) * progressionTir);
                float y = origineBoulet.Y + ((destinationBoulet.Y - origineBoulet.Y) * progressionTir);
                positionBoulet = new PointF(x, y);
            }
            else
            {
                positionBoulet = destinationBoulet;

                if (!impactTraite)
                {
                    if (tirDetruitPoint
                        && pointsPoses.TryGetValue(pointDetruit, out var proprietaire)
                        && !ReferenceEquals(proprietaire, JoueurCourant)
                        && !PointAppartientALigneValidee(pointDetruit))
                    {
                        pointsPoses.Remove(pointDetruit);
                    }

                    impactTraite = true;
                }

                ticksPauseImpact++;
                if (ticksPauseImpact >= pauseImpactTicks)
                {
                    timerAnimationTir.Stop();
                    tirEnCours = false;
                    PasserAuJoueurSuivant();
                }
            }

            Invalidate();
        }

        private void JouerModePose(Point clickPosition)
        {
            if (!TryGetNearestIntersection(clickPosition, out var intersection))
            {
                return;
            }

            if (pointsPoses.ContainsKey(intersection))
            {
                return;
            }

            var joueurQuiJoue = JoueurCourant;
            pointsPoses[intersection] = joueurQuiJoue;

            var lignesCandidates = TrouverLignesCandidatesPose(intersection, joueurQuiJoue);
            int lignesValideesCeTour = 0;

            foreach (var ligne in lignesCandidates)
            {
                var normalisee = NormaliserLigne(ligne.Debut, ligne.Fin);

                bool existe = lignesAlignements.Any(l => l.Debut == normalisee.Debut && l.Fin == normalisee.Fin && ReferenceEquals(l.Proprietaire, joueurQuiJoue));
                if (existe)
                {
                    continue;
                }

                if (EstLigneValideSelonRegles(normalisee.Debut, normalisee.Fin, joueurQuiJoue))
                {
                    lignesAlignements.Add(new LigneValidee(normalisee.Debut, normalisee.Fin, joueurQuiJoue));
                    joueurQuiJoue.AjouterPoint();
                    lignesValideesCeTour++;
                }
            }

            if (lignesValideesCeTour == 0)
            {
                PasserAuJoueurSuivant();
            }
        }

        private void JouerModeTir(Point clickPosition)
        {
            if (!TryGetGridGeometry(out int startX, out int startY, out int gridSize, out float step))
            {
                return;
            }

            int indexJoueur = indexJoueurCourant;
            PointF origineCanon = ObtenirPositionCanonPourJoueur(indexJoueur, startX, startY, gridSize, step);
            int rowCanon = Math.Clamp(hauteursCanonsParJoueur[indexJoueur], 0, gridLines - 1);
            int puissance = (int)inputPuissanceTir.Value; // 1 = proche, 9 = fond

            int indexPortee = (int)Math.Round((puissance - 1) * (gridLines - 1) / 8f);
            indexPortee = Math.Clamp(indexPortee, 0, gridLines - 1);

            int colImpact = indexJoueur == 0
                ? indexPortee // joueur 0 tire de la gauche vers la droite
                : (gridLines - 1) - indexPortee; // joueur 1 tire de la droite vers la gauche

            var intersection = (Col: colImpact, Row: rowCanon);

            float cibleX = startX + (colImpact * step);
            float cibleY = origineCanon.Y; // tir horizontal uniquement
            PointF impact = new(cibleX, cibleY);

            float distance = Math.Abs(cibleX - origineCanon.X);
            if (distance < 0.001f)
            {
                return;
            }

            bool detruitPoint = false;
            if (pointsPoses.TryGetValue(intersection, out var proprietaire)
                && !ReferenceEquals(proprietaire, JoueurCourant)
                && !PointAppartientALigneValidee(intersection))
            {
                detruitPoint = true;
            }

            DemarrerAnimationTir(origineCanon, impact, detruitPoint, intersection);
        }

        private void Form1_MouseClick(object? sender, MouseEventArgs e)
        {
            if (etatEcran != EtatEcran.Partie)
            {
                return;
            }

            if (tirEnCours)
            {
                return;
            }

            if (modeTour == ModeActionTour.Pose)
            {
                JouerModePose(e.Location);
            }
            else
            {
                JouerModeTir(e.Location);
            }

            Invalidate();
        }

        // M�thode isol�e: transforme un clic en intersection la plus proche
        private bool TryGetNearestIntersection(Point clickPoint, out (int Col, int Row) intersection)
        {
            intersection = default;

            if (!TryGetGridGeometry(out int startX, out int startY, out int gridSize, out float step))
            {
                return false;
            }

            int nearestCol = (int)Math.Round((clickPoint.X - startX) / step);
            int nearestRow = (int)Math.Round((clickPoint.Y - startY) / step);

            nearestCol = Math.Clamp(nearestCol, 0, gridLines - 1);
            nearestRow = Math.Clamp(nearestRow, 0, gridLines - 1);

            float intersectionX = startX + (nearestCol * step);
            float intersectionY = startY + (nearestRow * step);

            float dx = clickPoint.X - intersectionX;
            float dy = clickPoint.Y - intersectionY;
            float distance = (float)Math.Sqrt((dx * dx) + (dy * dy));

            float maxSnapDistance = step * 0.45f;
            if (distance > maxSnapDistance)
            {
                return false;
            }

            intersection = (nearestCol, nearestRow);
            return true;
        }

        private bool TryGetGridGeometry(out int startX, out int startY, out int gridSize, out float step)
        {
            int reserveDroite = 0;
            if (etatEcran == EtatEcran.Partie && panelActionsPartie != null && panelActionsPartie.Visible)
            {
                reserveDroite = panelActionsPartie.Width + 16;
            }

            int usableWidth = ClientSize.Width - (PaddingAroundGrid * 2) - reserveDroite;
            int usableHeight = ClientSize.Height - (PaddingAroundGrid * 2);
            gridSize = Math.Max(100, Math.Min(usableWidth, usableHeight));

            int leftMargin = PaddingAroundGrid;
            startX = leftMargin + ((usableWidth - gridSize) / 2);
            startY = (ClientSize.Height - gridSize) / 2;

            if (gridLines < 2)
            {
                step = 0;
                return false;
            }

            step = gridSize / (float)(gridLines - 1);
            return true;
        }

        // M�thode isol�e pour dessiner l'information du tour
        private void DessinerTourCourant(Graphics g)
        {
            if (joueurs.Length == 0)
            {
                return;
            }

            string texte = $"Tour: {JoueurCourant.Nom} | Mode: {modeTour}";
            using var brush = new SolidBrush(JoueurCourant.Couleur);
            g.DrawString(texte, Font, brush, 20, 20);
        }

        private void DessinerCanons(Graphics g, int startX, int startY, int gridSize, float step)
        {
            if (joueurs.Length < 2)
            {
                return;
            }

            for (int i = 0; i < joueurs.Length; i++)
            {
                PointF position = ObtenirPositionCanonPourJoueur(i, startX, startY, gridSize, step);
                bool estJoueurCourant = i == indexJoueurCourant;

                float rayon = Math.Max(6f, step * 0.18f);
                using var brush = new SolidBrush(joueurs[i].Couleur);
                using var pen = new Pen(estJoueurCourant ? Color.Gold : Color.FromArgb(30, 30, 30), estJoueurCourant ? 2.8f : 1.6f);

                g.FillEllipse(brush, position.X - rayon, position.Y - rayon, rayon * 2, rayon * 2);
                g.DrawEllipse(pen, position.X - rayon, position.Y - rayon, rayon * 2, rayon * 2);

                float direction = i == 0 ? 1f : -1f;
                float canonLongueur = Math.Max(12f, step * 0.55f);
                float x2 = position.X + (direction * canonLongueur);
                using var penCanon = new Pen(joueurs[i].Couleur, 4f);
                g.DrawLine(penCanon, position.X, position.Y, x2, position.Y);
            }
        }

        private void DessinerBouletSiNecessaire(Graphics g, float step)
        {
            if (!tirEnCours)
            {
                return;
            }

            float rayon = Math.Max(4f, step * 0.16f);
            using var brush = new SolidBrush(Color.FromArgb(40, 40, 40));
            g.FillEllipse(brush, positionBoulet.X - rayon, positionBoulet.Y - rayon, rayon * 2, rayon * 2);
        }

        // M�thode isol�e pour dessiner le score des joueurs au-dessus de la grille
        private void DessinerScores(Graphics g)
        {
            if (joueurs.Length == 0)
            {
                return;
            }

            if (!TryGetGridGeometry(out int startX, out int startY, out int gridSize, out _))
            {
                return;
            }

            using var scoreFont = new Font(Font.FontFamily, 16f, FontStyle.Bold);
            float espace = 24f;

            float largeurTotale = 0f;
            foreach (var joueur in joueurs)
            {
                string nom = joueur.Nom;
                string valeur = $": {joueur.Score}";
                largeurTotale += g.MeasureString(nom, scoreFont).Width;
                largeurTotale += g.MeasureString(valeur, scoreFont).Width;
            }

            if (joueurs.Length > 1)
            {
                largeurTotale += espace * (joueurs.Length - 1);
            }

            float x = startX + ((gridSize - largeurTotale) / 2f);
            float y = Math.Max(8f, startY - scoreFont.Height - 12f);

            using var brushValeur = new SolidBrush(ForeColor);
            for (int i = 0; i < joueurs.Length; i++)
            {
                string nom = joueurs[i].Nom;
                string valeur = $": {joueurs[i].Score}";

                using var brushNom = new SolidBrush(joueurs[i].Couleur);
                g.DrawString(nom, scoreFont, brushNom, x, y);
                x += g.MeasureString(nom, scoreFont).Width;

                g.DrawString(valeur, scoreFont, brushValeur, x, y);
                x += g.MeasureString(valeur, scoreFont).Width + espace;
            }
        }

        // M�thode isol�e pour dessiner toutes les lignes d'alignement d�j� trouv�es
        private void DessinerLignesAlignement(Graphics g, int startX, int startY, float step)
        {
            if (lignesAlignements.Count == 0)
            {
                return;
            }

            foreach (var ligne in lignesAlignements)
            {
                var debut = ligne.Debut;
                var fin = ligne.Fin;
                float x1 = startX + (debut.Col * step);
                float y1 = startY + (debut.Row * step);
                float x2 = startX + (fin.Col * step);
                float y2 = startY + (fin.Row * step);

                using var pen = new Pen(ligne.Couleur, 4.5f);
                g.DrawLine(pen, x1, y1, x2, y2);
            }
        }

        // M�thode isol�e pour dessiner la grille, appelable depuis d'autres endroits
        private void dessinerTerrain(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (!TryGetGridGeometry(out int startX, out int startY, out int gridSize, out float step))
            {
                return;
            }

            using var pen = new Pen(Color.LightGray, 2.4f) { DashStyle = DashStyle.Solid };

            for (int i = 0; i < gridLines; i++)
            {
                float y = startY + (i * step);
                g.DrawLine(pen, startX, y, startX + gridSize, y);

                float x = startX + (i * step);
                g.DrawLine(pen, x, startY, x, startY + gridSize);
            }

            float rayonPoint = Math.Max(4f, step * 0.15f);

            foreach (var pointPose in pointsPoses)
            {
                var (col, row) = pointPose.Key;
                var joueur = pointPose.Value;

                float x = startX + (col * step);
                float y = startY + (row * step);

                using var brush = new SolidBrush(joueur.Couleur);
                g.FillEllipse(brush, x - rayonPoint, y - rayonPoint, rayonPoint * 2, rayonPoint * 2);
            }

            DessinerCanons(g, startX, startY, gridSize, step);

            // Dessiner toutes les lignes d�j� trouv�es (persistantes)
            DessinerLignesAlignement(g, startX, startY, step);
            DessinerBouletSiNecessaire(g, step);
        }

        public Form1()
        {
            InitializeComponent();
            DoubleBuffered = true;
            ResizeRedraw = true;
            KeyPreview = true; // pour capter les raccourcis clavier (Ctrl+chiffre)

            timerAnimationTir = new System.Windows.Forms.Timer { Interval = 20 };
            timerAnimationTir.Tick += TimerAnimationTir_Tick;

            InitialiserJoueursEtTour();
            InitialiserEcouteSouris();
            InitialiserEcranAccueil();
            AfficherMenuPrincipal();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (etatEcran != EtatEcran.Partie)
            {
                return;
            }

            dessinerTerrain(e.Graphics);
            DessinerScores(e.Graphics);
            DessinerTourCourant(e.Graphics);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Raccourcis Ctrl+1..9 pour régler rapidement la puissance de tir
            if ((keyData & Keys.Control) == Keys.Control)
            {
                int? puissance = keyData switch
                {
                    Keys.Control | Keys.D1 or Keys.Control | Keys.NumPad1 => 1,
                    Keys.Control | Keys.D2 or Keys.Control | Keys.NumPad2 => 2,
                    Keys.Control | Keys.D3 or Keys.Control | Keys.NumPad3 => 3,
                    Keys.Control | Keys.D4 or Keys.Control | Keys.NumPad4 => 4,
                    Keys.Control | Keys.D5 or Keys.Control | Keys.NumPad5 => 5,
                    Keys.Control | Keys.D6 or Keys.Control | Keys.NumPad6 => 6,
                    Keys.Control | Keys.D7 or Keys.Control | Keys.NumPad7 => 7,
                    Keys.Control | Keys.D8 or Keys.Control | Keys.NumPad8 => 8,
                    Keys.Control | Keys.D9 or Keys.Control | Keys.NumPad9 => 9,
                    _ => null
                };

                if (puissance.HasValue)
                {
                    inputPuissanceTir.Value = puissance.Value;
                    return true; // on gère le raccourci
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
