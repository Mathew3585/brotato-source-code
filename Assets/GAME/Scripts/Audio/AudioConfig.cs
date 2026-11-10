using UnityEngine;

namespace BrotatoLike
{
    /// <summary>Événements sonores utilisés par le code (chacun correspond à un champ d'une catégorie).</summary>
    public enum SoundId
    {
        // Combat
        Shoot, MeleeSwing, EnemyHit, EnemyDeath, EnemyShoot, Explosion, TurretDeploy,
        PlayerHurt, PlayerDeath,
        // Ramassage / progression
        PickupMaterial, PickupCrate, LevelUp,
        // Partie
        CountdownTick, WaveStart, WaveEnd, BossSpawn, BossDeath, Victory, GameOver,
        // Boutique
        Buy, BuyFail, Reroll, Lock, Merge, Sell, Upgrade,
        // Interface
        UiSelect, UiConfirm, UiBack, UiJoin, UiReady, UiPause, CharacterChange,
    }

    /// <summary>
    /// Configuration audio générale : pointe vers une bibliothèque de sons par catégorie.
    /// Pour changer un son : ouvre la catégorie (ex : CombatSounds) et glisse un autre clip dans le champ voulu.
    /// </summary>
    [CreateAssetMenu(menuName = "BrotatoLike/Sons/Configuration audio", fileName = "AudioConfig")]
    public class AudioConfig : ScriptableObject
    {
        public CombatSounds combat;
        public UISounds interfaceUtilisateur;
        public ShopSounds boutique;
        public GameSounds partie;
        public PickupSounds ramassage;

        /// <summary>Retrouve le son correspondant à un événement (null si la catégorie n'est pas assignée).</summary>
        public SoundEvent Get(SoundId id) => id switch
        {
            SoundId.Shoot => combat ? combat.tir : null,
            SoundId.MeleeSwing => combat ? combat.coupDeMelee : null,
            SoundId.TurretDeploy => combat ? combat.poseTourelle : null,
            SoundId.Explosion => combat ? combat.explosion : null,
            SoundId.EnemyHit => combat ? combat.impactEnnemi : null,
            SoundId.EnemyDeath => combat ? combat.mortEnnemi : null,
            SoundId.EnemyShoot => combat ? combat.tirEnnemi : null,
            SoundId.PlayerHurt => combat ? combat.joueurTouche : null,
            SoundId.PlayerDeath => combat ? combat.mortJoueur : null,

            SoundId.PickupMaterial => ramassage ? ramassage.materiau : null,
            SoundId.PickupCrate => ramassage ? ramassage.caisse : null,
            SoundId.LevelUp => ramassage ? ramassage.niveauSuperieur : null,

            SoundId.CountdownTick => partie ? partie.decompte : null,
            SoundId.WaveStart => partie ? partie.debutVague : null,
            SoundId.WaveEnd => partie ? partie.finVague : null,
            SoundId.BossSpawn => partie ? partie.arriveeBoss : null,
            SoundId.BossDeath => partie ? partie.mortBoss : null,
            SoundId.Victory => partie ? partie.victoire : null,
            SoundId.GameOver => partie ? partie.gameOver : null,

            SoundId.Buy => boutique ? boutique.achat : null,
            SoundId.BuyFail => boutique ? boutique.achatRefuse : null,
            SoundId.Reroll => boutique ? boutique.relance : null,
            SoundId.Lock => boutique ? boutique.cadenas : null,
            SoundId.Merge => boutique ? boutique.fusion : null,
            SoundId.Sell => boutique ? boutique.vente : null,
            SoundId.Upgrade => boutique ? boutique.amelioration : null,

            SoundId.UiSelect => interfaceUtilisateur ? interfaceUtilisateur.selection : null,
            SoundId.UiConfirm => interfaceUtilisateur ? interfaceUtilisateur.valider : null,
            SoundId.UiBack => interfaceUtilisateur ? interfaceUtilisateur.retour : null,
            SoundId.UiPause => interfaceUtilisateur ? interfaceUtilisateur.pause : null,
            SoundId.UiJoin => interfaceUtilisateur ? interfaceUtilisateur.joueurRejoint : null,
            SoundId.UiReady => interfaceUtilisateur ? interfaceUtilisateur.pret : null,
            SoundId.CharacterChange => interfaceUtilisateur ? interfaceUtilisateur.changementPersonnage : null,
            _ => null,
        };
    }
}
