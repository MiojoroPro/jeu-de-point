namespace jeu_de_point
{
    public class Joueur
    {
        public Joueur(string nom, Color couleur)
        {
            Nom = nom;
            Couleur = couleur;
        }

        public string Nom { get; }
        public Color Couleur { get; }
        public int Score { get; private set; }

        public void AjouterPoint()
        {
            Score++;
        }

        public void DefinirScore(int score)
        {
            Score = Math.Max(0, score);
        }
    }
}
