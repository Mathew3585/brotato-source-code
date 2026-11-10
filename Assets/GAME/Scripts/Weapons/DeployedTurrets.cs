using System.Collections.Generic;
using PrimeTween;
using UnityEngine;

namespace BrotatoLike
{
    /// <summary>
    /// Tourelles posées par les armes de type Tourelle : elles tirent seules sur l'ennemi le plus proche
    /// pendant quelques secondes, avec les stats (dégâts, vitesse d'attaque...) du joueur qui les a posées.
    /// </summary>
    public class DeployedTurrets : MonoBehaviour
    {
        const float BlinkTime = 1.5f;

        class Turret
        {
            public SpriteRenderer body;
            public Vector2 position;
            public float life;
            public float fireTimer;
            public WeaponData data;
            public Player owner;
        }

        const string PrefabName = "Turret";

        static DeployedTurrets instance;
        SpriteRenderer template;

        readonly Stack<SpriteRenderer> free = new();
        readonly List<Turret> active = new();

        static DeployedTurrets Instance
        {
            get
            {
                if (instance == null) instance = new GameObject("DeployedTurrets").AddComponent<DeployedTurrets>();
                return instance;
            }
        }

        public static void Deploy(Player owner, WeaponData data, Vector2 position) =>
            Deploy(owner, data, position, data.turretDuration);

        /// <summary>Pose une tourelle pour une durée donnée (float.MaxValue = jusqu'à la fin de la vague).</summary>
        public static void Deploy(Player owner, WeaponData data, Vector2 position, float duration)
        {
            var self = Instance;
            var sr = self.free.Count > 0 ? self.free.Pop() : self.Create();
            sr.gameObject.SetActive(true);
            sr.sprite = data.turretSprite != null ? data.turretSprite : data.sprite;
            sr.color = Color.white;
            sr.transform.position = position;
            sr.sortingOrder = SortingUtil.OrderFromY(position.y);

            // Petite apparition qui rebondit
            Tween.StopAll(sr.transform);
            sr.transform.localScale = Vector3.zero;
            Tween.Scale(sr.transform, Vector3.one * data.turretScale, 0.3f, Ease.OutBack);

            self.active.Add(new Turret
            {
                body = sr,
                position = position,
                life = duration,
                fireTimer = 0.3f,
                data = data,
                owner = owner,
            });
        }

        public static void ClearAll()
        {
            if (instance == null) return;
            for (int i = instance.active.Count - 1; i >= 0; i--) instance.Release(i);
        }

        SpriteRenderer Create()
        {
            // Prefab « Resources/Turret » (le sprite vient de l'arme ou de l'objet ; ajoute-y ombre, animation...)
            if (template == null) template = Resources.Load<SpriteRenderer>(PrefabName);
            if (template != null) return Instantiate(template, transform);

            var go = new GameObject("Turret");
            go.transform.SetParent(transform);
            return go.AddComponent<SpriteRenderer>();
        }

        void Release(int index)
        {
            var t = active[index];
            Tween.StopAll(t.body.transform);
            t.body.gameObject.SetActive(false);
            free.Push(t.body);
            active[index] = active[^1];
            active.RemoveAt(active.Count - 1);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            for (int i = active.Count - 1; i >= 0; i--)
            {
                var t = active[i];
                t.life -= dt;
                if (t.life <= 0f || t.owner == null)
                {
                    SpriteEffects.Play(GameManager.Instance.explosionFrames, t.position, 0.8f, 20f, new Color(0.8f, 0.8f, 0.8f));
                    Release(i);
                    continue;
                }

                // Clignote avant de disparaître
                var c = t.body.color;
                c.a = t.life < BlinkTime && Mathf.Repeat(t.life, 0.2f) < 0.1f ? 0.35f : 1f;
                t.body.color = c;

                t.fireTimer -= dt * t.owner.Stats.AttackSpeedMultiplier;
                if (t.fireTimer > 0f) continue;

                float range = t.data.range + t.owner.Stats[StatType.Range];
                var target = EnemyGrid.FindNearest(t.position, range);
                if (target == null) continue;

                Vector2 dir = (target.Position - t.position).normalized;
                t.body.flipX = dir.x < 0f;
                Vector2 muzzle = t.position + Vector2.up * 0.25f + dir * 0.3f;
                float damage = Weapon.RollDamage(t.data, t.owner, out bool crit);
                GameManager.Instance.ProjectilePool.Get(muzzle).Launch(dir, t.data, damage, crit, range, t.owner);
                AudioManager.Play(SoundId.Shoot, 0.6f, 1.2f);
                Tween.CompleteAll(t.body.transform);
                Tween.PunchScale(t.body.transform, new Vector3(0.1f, -0.1f, 0f), 0.15f);
                t.fireTimer = t.data.turretFireInterval;
            }
        }
    }
}
